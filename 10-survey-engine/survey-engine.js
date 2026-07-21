/* ============================================================
   D+ 온보딩 설문 엔진 (PRD §4.3)

   설계 원칙
   - UI를 모른다. DOM·프레임워크에 의존하지 않는다.
     엔진은 "지금 무엇을 보여줄지"(view)만 알려주고, 그리는 건 화면의 몫이다.
     → 디자인이 바뀌어도 이 파일은 그대로 쓴다.
   - 문항은 늘리지 않는다. 개인화는 '되묻기(probe)'로만 한다.
     PRD §4.3: "추가로 궁금한 것은 문항이 아니라 되묻기와 행동 데이터로 얻는다"
   - 원문을 덮지 않는다. 되묻기 답변은 followups[]에 따로 쌓는다.
     (와이어프레임 A-03 주석)

   사용법
     var s = SurveyEngine.create({ spec: SPEC, llm: SurveyEngine.mockLLM() });
     s.view();                    // 지금 그릴 것
     s.submit(값).then(view => …) // 답 제출 → 다음 view
     s.skip().then(view => …)     // 되묻기 건너뛰기
     s.result();                  // { answers, followups }
   ============================================================ */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.SurveyEngine = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  /* ---------- 되묻기가 필요한지 판단하는 규칙 (LLM 호출 전 1차 필터) ---------- */
  // LLM을 부르기 전에 명백한 경우를 먼저 거른다. 호출 비용과 지연을 줄인다.
  var SHORT_ANSWER = 40; // 이보다 짧으면 분량 확보용 되묻기를 우선 시도

  function create(opts) {
    opts = opts || {};
    var spec = opts.spec;
    if (!spec || !spec.questions) throw new Error('spec이 필요합니다');

    var llm = opts.llm || null;
    var budget = spec.probeBudget || { perQuestion: 1, total: 2 };
    var maxPerQ = opts.maxProbesPerQuestion || budget.perQuestion || 1;
    var maxTotal = opts.maxProbesTotal || budget.total || 2;

    var questions = spec.questions;
    var idx = 0;                 // 현재 문항 인덱스
    var answers = {};            // { q1: {...}, q4: "..." }
    var followups = [];          // { forId, probe, answer, skipped, at }
    var probeCount = {};         // { q4: 1 }
    var totalProbes = 0;
    var pendingProbe = null;     // 지금 되묻는 중이면 문자열
    var finished = false;
    var lastError = null;

    function progress() {
      return { step: Math.min(idx + 1, questions.length), total: questions.length };
    }

    function currentQuestion() {
      return questions[idx] || null;
    }

    /* ---------- 화면이 그릴 view ---------- */
    // UI는 이 4가지 type만 처리하면 된다. 디자인 교체 시 바뀌는 건 렌더러뿐이다.
    function view() {
      if (finished) return { type: 'done', payload: result() };
      if (pendingProbe) {
        return {
          type: 'probe',
          forId: currentQuestion().id,
          text: pendingProbe,
          skippable: true,
          progress: progress(),
          error: takeError()
        };
      }
      var q = currentQuestion();
      if (!q) return { type: 'analyzing' };
      return {
        type: q.type === 'free' ? 'free' : 'quant',
        id: q.id,
        title: q.title,
        required: !!q.required,
        fields: q.fields || null,
        placeholder: q.placeholder || null,
        hint: q.hint || null,
        examples: q.examples || null,
        evidence: q.evidence || null,
        progress: progress(),
        error: takeError()
      };
    }

    function takeError() {
      var e = lastError;
      lastError = null;
      return e;
    }

    /* ---------- 검증 ---------- */
    function validate(q, value) {
      if (q.type === 'free') {
        if (q.required && (!value || !String(value).trim())) {
          return '이 문항은 꼭 답변해주세요';
        }
        return null;
      }
      // 정량 문항: 모든 field가 채워져야 한다
      var missing = (q.fields || []).filter(function (f) {
        return !value || value[f.key] === undefined || value[f.key] === '';
      });
      return missing.length ? '모든 항목을 선택해주세요' : null;
    }

    /* ---------- 되묻기 판단 ---------- */
    function canProbe(q) {
      if (q.type !== 'free') return false;               // 정량 문항은 되묻지 않는다
      if (!llm) return false;
      if (totalProbes >= maxTotal) return false;
      if ((probeCount[q.id] || 0) >= maxPerQ) return false;
      return true;
    }

    function askProbe(q, answer) {
      if (!canProbe(q)) return Promise.resolve(null);
      return Promise.resolve(
        llm.decideProbe({
          question: q,
          answer: answer,
          answers: answers,          // 앞선 정량 응답도 함께 본다 (원룸+대형견 같은 충돌 감지)
          isShort: String(answer).trim().length < SHORT_ANSWER
        })
      ).then(function (res) {
        var probe = res && res.probe ? String(res.probe).trim() : null;
        return probe || null;
      }).catch(function () {
        return null; // LLM 실패는 설문을 막지 않는다. 되묻기는 부가 기능이다.
      });
    }

    /* ---------- 진행 ---------- */
    function advance() {
      idx += 1;
      if (idx >= questions.length) finished = true;
      return view();
    }

    function submit(value) {
      // 되묻기에 답한 경우
      if (pendingProbe) {
        var q0 = currentQuestion();
        followups.push({
          forId: q0.id,
          probe: pendingProbe,
          answer: value == null ? '' : String(value).trim(),
          skipped: false,
          at: new Date().toISOString()
        });
        pendingProbe = null;
        return Promise.resolve(advance());
      }

      var q = currentQuestion();
      if (!q) return Promise.resolve(view());

      var err = validate(q, value);
      if (err) {
        lastError = err;
        return Promise.resolve(view());
      }

      answers[q.id] = q.type === 'free' ? String(value).trim() : value;

      return askProbe(q, answers[q.id]).then(function (probe) {
        if (probe) {
          pendingProbe = probe;
          probeCount[q.id] = (probeCount[q.id] || 0) + 1;
          totalProbes += 1;
          return view();
        }
        return advance();
      });
    }

    function skip() {
      if (!pendingProbe) return Promise.resolve(view());
      followups.push({
        forId: currentQuestion().id,
        probe: pendingProbe,
        answer: null,
        skipped: true,
        at: new Date().toISOString()
      });
      pendingProbe = null;
      return Promise.resolve(advance());
    }

    function back() {
      if (pendingProbe) { pendingProbe = null; return view(); }
      if (idx > 0) { idx -= 1; finished = false; }
      return view();
    }

    function result() {
      return {
        specVersion: spec.version,
        answers: answers,
        followups: followups,
        probeCount: totalProbes
      };
    }

    return {
      view: view,
      submit: submit,
      skip: skip,
      back: back,
      result: result,
      restore: function (saved) {          // 이탈 후 복귀 지원
        if (!saved) return view();
        answers = saved.answers || {};
        followups = saved.followups || [];
        idx = Math.min(Object.keys(answers).length, questions.length);
        finished = idx >= questions.length;
        return view();
      }
    };
  }

  /* ============================================================
     LLM 어댑터
     - 엔진은 decideProbe(ctx) → { probe: string|null } 만 요구한다.
     - 실제 호출은 서버(Supabase Edge Function)에서 한다. 키를 클라이언트에 두지 않는다.
     ============================================================ */

  // 서버 엔드포인트로 위임하는 어댑터 (운영용)
  function httpLLM(endpoint, fetchImpl) {
    var f = fetchImpl || (typeof fetch !== 'undefined' ? fetch : null);
    if (!f) throw new Error('fetch 구현이 필요합니다');
    return {
      decideProbe: function (ctx) {
        return f(endpoint, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            questionId: ctx.question.id,
            questionTitle: ctx.question.title,
            answer: ctx.answer,
            answers: ctx.answers,
            isShort: ctx.isShort
          })
        })
          .then(function (r) { return r.ok ? r.json() : { probe: null }; });
      }
    };
  }

  // 데모·오프라인용 어댑터. 실제 LLM 없이도 "사람마다 다른 되묻기"를 보여준다.
  // 운영에서는 쓰지 않는다. survey-prompts.md의 프롬프트로 대체된다.
  function mockLLM() {
    var RULES = [
      { test: /이웃|민원|층간|시끄/, probe: '이웃 항의가 걱정이신가요, 아니면 잠을 못 자는 게 더 힘드실까요? 한 줄만 더 적어주세요.' },
      { test: /혼내|훈육|때리|엄하/, probe: '혼내는 방법 말고, 왜 그러는지 알아보는 쪽은 어떠세요? 그때 어떻게 하실 것 같은지 궁금해요.' },
      { test: /병원|아프|건강|다치/, probe: '건강 문제까지 생각하고 계시네요. 예상보다 치료비가 커진다면 어디까지 감당하실 수 있을까요?' },
      { test: /산책|운동|등산|뛰|활동/, probe: '활동적인 하루를 그리고 계시네요. 비 오는 날이나 야근한 날은 어떻게 하실 것 같으세요?' },
      { test: /조용|쉬|누워|집순이|집돌이/, probe: '차분한 하루를 원하시는군요. 강아지가 생각보다 활발하다면 어떠실 것 같으세요?' },
      { test: /모르|글쎄|생각.*없|잘 ?모르/, probe: '지금 떠오르는 게 없어도 괜찮아요. 대신 가장 걱정되는 상황 하나만 적어주실래요?' }
    ];
    return {
      decideProbe: function (ctx) {
        var a = String(ctx.answer || '');
        // 1) 주거 형태와 답변이 충돌하면 그것부터 되묻는다
        var housing = ctx.answers && ctx.answers.q1 && ctx.answers.q1.housing;
        if (housing === '원룸·오피스텔' && /짖|소리|시끄/.test(a)) {
          return { probe: '원룸이라고 하셨는데, 짖음이 생기면 이웃과의 문제가 먼저 걸리실까요? 어떻게 대응하실지 조금만 더 들려주세요.' };
        }
        for (var i = 0; i < RULES.length; i++) {
          if (RULES[i].test.test(a)) return { probe: RULES[i].probe };
        }
        // 2) 규칙에 안 걸려도 답변이 짧으면 분량을 확보한다 (PRD §4.3)
        if (ctx.isShort) {
          return { probe: '조금만 더 구체적으로 들려주실 수 있을까요? 실제로 그 상황이 생겼다고 상상하시면 좋아요.' };
        }
        return { probe: null };
      }
    };
  }

  return { create: create, httpLLM: httpLLM, mockLLM: mockLLM };
});
