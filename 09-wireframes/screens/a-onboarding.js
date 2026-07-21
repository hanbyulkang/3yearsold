/* A. 온보딩 — 로그인 · 설문 · 견종 선택 · 캐릭터견 생성 (PRD §4.1, §4.3) */
(function (S) {

S.push({
  id: 'a01-start',
  no: 'A-01',
  group: 'A. 온보딩',
  prd: '§4.1',
  title: '시작 / 로그인',
  purpose: '서비스 한 줄 소개 + 로그인. 권한 요청 없음(GPS 미사용).',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="body" style="justify-content:center;gap:16px">
      <div class="img" style="height:160px"><span>로고 · 키비주얼</span></div>
      <div class="center col" style="gap:6px">
        <div class="h1">D+ 디플러스</div>
        <div class="p">AI 반려견을 키우며<br>나에게 맞는 보호견 참여 방법을 찾아요</div>
      </div>
      <div class="btn wide">Google로 계속하기</div>
      <div class="btn wide">카카오로 계속하기</div>
      <div class="btn gho wide">이메일로 계속하기</div>
    </div>
    <div class="footer">
      <div class="xs center">계속하면 이용약관·개인정보처리방침에 동의하게 됩니다</div>
    </div>
  `,
  notes: [
    'Supabase Auth. 가입 시 생년월일 1회 입력 — 결제 한도·법정대리인 동의(§7.7) 판단에만 사용, 기능 분기에 사용 금지.',
    'WebGL 로딩 진행률을 이 화면에 겹쳐 표시.'
  ]
});

S.push({
  id: 'a02-survey-quant',
  no: 'A-02',
  group: 'A. 온보딩',
  prd: '§4.3',
  title: '설문 — 정량 문항 (Q1~Q4, Q6, Q9~Q10)',
  purpose: '문항 옆에 근거 카드가 붙는 것이 이 설문의 정체성. 한 화면에 문항 1~2개.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">설문</span><span class="act">3/11</span></div>
    <div class="body">
      <div class="gauge sm"><i style="width:27%"></i></div>
      <div class="card">
        <div class="h3">Q3. 하루 중 강아지와 함께 있을 수 있는 시간은?</div>
        <div class="evidence">
          <div class="cap">왜 묻나요?</div>
          <div class="txt">파양 고려 이유 3위가 '예상보다 많은 시간'(25.7%)이었어요</div>
          <div class="src">— 2025 동물복지 국민의식조사</div>
        </div>
        <div class="col" style="gap:5px">
          <div class="opt">2시간 미만</div>
          <div class="opt on">2~4시간</div>
          <div class="opt">4~8시간</div>
          <div class="opt">8시간 이상</div>
        </div>
      </div>
      <div class="card">
        <div class="h3">Q4. 월 지출로 감당 가능한 범위는?</div>
        <div class="evidence">
          <div class="cap">왜 묻나요?</div>
          <div class="txt">파양 고려 이유 2위가 '예상보다 큰 지출'(35.2%)이었어요</div>
          <div class="src">— 2025 동물복지 국민의식조사</div>
        </div>
        <div class="row wrap" style="gap:5px">
          <span class="chip">5만원 미만</span><span class="chip on">5~10만원</span>
          <span class="chip">10~20만원</span><span class="chip">20만원 이상</span>
        </div>
      </div>
    </div>
    <div class="footer">
      <div class="row" style="gap:8px">
        <div class="btn sec" style="flex:0 0 90px">이전</div>
        <div class="btn pri grow" data-goto="a03-survey-free">다음</div>
      </div>
    </div>
  `,
  notes: [
    '통계 수치·조사명은 하드코딩하지 않고 05-data 원본 기반 JSON에서 주입(매년 갱신).',
    '응답은 문항 단위 즉시 저장 — 이탈 후 이어하기.',
    '근거 카드에 겁주는 톤 금지(§4.3).'
  ]
});

S.push({
  id: 'a03-survey-free',
  no: 'A-03',
  group: 'A. 온보딩',
  prd: '§4.3',
  title: '설문 — 필수 자유 서술 (Q5·Q8·Q11)',
  purpose: '자유 서술이 추천의 주 재료. 짧으면 AI가 1회 되묻는다.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">설문</span><span class="act">5/11</span></div>
    <div class="body">
      <div class="gauge sm"><i style="width:45%"></i></div>
      <div class="card">
        <div class="row between">
          <div class="h3" style="flex:1">Q5. 강아지가 밤새 짖거나 물건을 망가뜨린다면 어떻게 하실 것 같으세요?</div>
          <span class="badge warn">필수</span>
        </div>
        <div class="evidence">
          <div class="cap">왜 묻나요?</div>
          <div class="txt">파양 고려 이유 1위가 '행동 문제'(42.7%)였어요</div>
          <div class="src">— 2025 동물복지 국민의식조사</div>
        </div>
        <div class="field area tall">이웃한테 미안해서 스트레스 받을 것 같아요. 그래도 왜 짖는지 먼저 찾아볼 것 같은데…</div>
        <div class="xs">정답이 없는 질문이에요. 솔직할수록 추천이 정확해집니다</div>
      </div>
      <div class="aibox">
        <div class="cap">AI · 되묻기</div>
        <div class="s">이웃 항의가 걱정이신가요, 잠을 못 자는 게 더 힘드실까요? 한 줄만 더 적어주세요.</div>
        <div class="field">한 줄 더 적기 (건너뛰어도 됩니다)</div>
      </div>
    </div>
    <div class="footer">
      <div class="row" style="gap:8px">
        <div class="btn sec" style="flex:0 0 90px">이전</div>
        <div class="btn pri grow" data-goto="a04-analyzing">다음</div>
      </div>
    </div>
  `,
  notes: [
    '되묻기는 입력 종료 후 1회만. 응답은 원문을 덮지 않고 followup 필드로 저장.',
    '자유 서술이 정량 응답과 충돌하면 자유 서술 우선(§4.3) — 추천 프롬프트에 명시.',
    'AI 프롬프트에도 "유기견" 금칙 → "보호견"(부록 A).'
  ]
});

S.push({
  id: 'a04-analyzing',
  no: 'A-04',
  group: 'A. 온보딩',
  prd: '§4.3',
  title: 'AI 분석 (로딩)',
  purpose: 'LLM 분석 대기. 이 결과가 견종·보호견·참여 추천에 공통으로 쓰인다.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="body" style="justify-content:center;gap:16px">
      <div class="img" style="height:110px"><span>분석 애니메이션</span></div>
      <div class="center col" style="gap:4px">
        <div class="h1">답변을 읽고 있어요</div>
        <div class="s">보통 10~20초 걸려요</div>
      </div>
      <div class="aibox">
        <div class="cap">AI · 진행</div>
        <div class="col" style="gap:5px">
          <div class="row"><span class="badge ok">완료</span><span class="s">생활 여건 정리</span></div>
          <div class="row"><span class="badge">진행</span><span class="s">서술 답변 해석</span></div>
          <div class="row"><span class="badge">대기</span><span class="s">견종 3개 정리</span></div>
        </div>
      </div>
    </div>
  `,
  notes: [
    'LLM 호출은 Edge Function 경유 — API 키를 WebGL 클라에 노출 금지.',
    '타임아웃 30초 → 재시도 1회 → 실패 시에도 설문 응답은 보존.',
    '분석 결과는 단일 레코드로 저장, 견종·보호견·참여 추천이 같은 레코드 참조(§4.3 단일 엔진).'
  ]
});

S.push({
  id: 'a05-breed-3',
  no: 'A-05',
  group: 'A. 온보딩',
  prd: '§4.1',
  title: '견종 3개 추천 · 선택',
  purpose: 'AI가 정하지 않고 유저가 고른다. 이유에 유저의 문장을 인용.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="t">추천 견종</span><span class="act">1개 선택</span></div>
    <div class="body">
      <div class="card sel">
        <div class="row top">
          <div class="img" style="width:64px;height:64px;flex:none"><span>견종</span></div>
          <div class="grow col" style="gap:3px">
            <div class="row between"><span class="h2">시바견</span><span class="badge ok">선택됨</span></div>
            <div class="s">독립적이고 혼자 있는 시간을 잘 견뎌요</div>
          </div>
        </div>
        <div class="aibox">
          <div class="cap">왜 당신에게</div>
          <div class="s">"왜 짖는지 먼저 찾아보겠다"고 적어주신 태도가 이 견종에 잘 맞아요. 분리불안도 적은 편이에요.</div>
        </div>
      </div>
      <div class="card">
        <div class="row top">
          <div class="img" style="width:64px;height:64px;flex:none"><span>견종</span></div>
          <div class="grow col" style="gap:3px">
            <div class="h2">웰시코기</div>
            <div class="s">사람을 좋아하지만 매일 산책량이 필요해요</div>
          </div>
        </div>
        <div class="aibox">
          <div class="cap">왜 당신에게</div>
          <div class="s">"퇴근하고 같이 산책하는 하루"라고 적어주셨죠. 산책이 이 견종의 핵심 욕구예요.</div>
        </div>
      </div>
      <div class="card">
        <div class="row top">
          <div class="img" style="width:64px;height:64px;flex:none"><span>견종</span></div>
          <div class="grow col" style="gap:3px">
            <div class="h2">믹스견 (중형)</div>
            <div class="s">보호소에 가장 많은 유형이에요</div>
          </div>
        </div>
        <div class="aibox">
          <div class="cap">왜 당신에게</div>
          <div class="s">나중에 실제 보호견을 만날 때 선택지가 가장 넓어요.</div>
        </div>
      </div>
    </div>
    <div class="footer">
      <div class="btn pri wide" data-goto="a06-persona">시바견으로 시작하기</div>
      <div class="btn gho wide">다시 추천받기</div>
    </div>
  `,
  notes: [
    '3개 고정. "다시 추천"은 2회까지.',
    '견종은 사전 정의 목록에서만 — LLM 응답을 화이트리스트 검증.'
  ]
});

S.push({
  id: 'a06-persona',
  no: 'A-06',
  group: 'A. 온보딩',
  prd: '§4.1',
  title: '성격 설정 · 이름 짓기',
  purpose: '외형이 아니라 성격을 설정한다. 이 값이 AI 페르소나와 돌봄 요구량이 된다.',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="appbar"><span class="back">←</span><span class="t">성격 정하기</span></div>
    <div class="body">
      <div class="h1">어떤 성격의 아이와 지내고 싶으세요?</div>
      <div class="card">
        <div class="row between"><span class="h3">겁 많음</span><span class="s mono">3/5</span></div>
        <div class="gauge"><i style="width:60%"></i></div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">활동성</span><span class="s mono">4/5</span></div>
        <div class="gauge"><i style="width:80%"></i></div>
      </div>
      <div class="card">
        <div class="row between"><span class="h3">사람 좋아함</span><span class="s mono">4/5</span></div>
        <div class="gauge"><i style="width:80%"></i></div>
      </div>
      <div class="aibox">
        <div class="cap">AI · 이 조합이면</div>
        <div class="s">"처음엔 낯을 가리지만, 산책 나가자는 말에는 제일 먼저 뛰어오는 아이."</div>
      </div>
      <div class="col">
        <div class="label">이름</div>
        <div class="field">단추</div>
      </div>
    </div>
    <div class="footer">
      <div class="btn pri wide" data-goto="a07-created">이 성격으로 만들기</div>
    </div>
  `,
  notes: [
    '성격 값이 LLM 페르소나 프롬프트와 돌봄 요구량 계산의 입력(활동성↑ → 산책 요구↑, 겁많음↑ → 초반 목욕 거부).',
    '견종 선택에 따라 기본값 프리필. 생성 후 변경 불가.'
  ]
});

S.push({
  id: 'a07-created',
  no: 'A-07',
  group: 'A. 온보딩',
  prd: '§4.1',
  title: '캐릭터견 첫 만남',
  purpose: '설정한 성격이 첫 연출에서 바로 드러난다(겁 많으면 다가오지 않음).',
  html: `
    <div class="sb"><span>9:41</span><span></span><span>100%</span></div>
    <div class="body" style="gap:14px">
      <div class="yard" style="min-height:300px">
        <div class="dog">첫 등장 연출<br>(구석에서 이쪽을 보는 자세)</div>
      </div>
      <div class="center col" style="gap:4px">
        <div class="h1">단추를 만났어요</div>
        <div class="s">시바견 · 겁 많음 3 · 활동성 4 · 사람 좋아함 4</div>
      </div>
      <div class="aibox">
        <div class="cap">단추</div>
        <div class="s">(고개만 빼꼼 내밀고 이쪽을 봐요)<br>아직 조금 무서운가 봐요. 밥그릇부터 채워볼까요?</div>
      </div>
    </div>
    <div class="footer">
      <div class="btn pri wide" data-goto="b01-yard">마당으로 들어가기</div>
    </div>
  `,
  notes: [
    '첫 연출은 성격 기반 분기(겁많음 ≥3 → 관찰, ≤2 → 달려옴).',
    '푸시 권한 요청은 첫 돌봄 완료 직후에.'
  ]
});

})(window.SCREENS);
