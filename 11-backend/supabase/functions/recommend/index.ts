/**
 * 보호견 추천 (와이어프레임 D-01 · D-02 · D-03)
 *
 * 설문을 다시 요구하지 않는다. 온보딩에서 만든 analyses 레코드를 그대로 쓴다
 * (PRD §4.3 — "보호견을 추천받는 시점에 설문을 다시 요구하지 않는다").
 *
 * 추천 이유는 사용자별로 생성해 recommendations에 저장한다.
 * 같은 보호견이라도 사람마다 다른 이유를 받는다.
 */
import { json, admin, requireUser } from "../_shared/http.ts";
import { fromEnv } from "../_shared/llm.ts";
import {
  buildRecommendMessages, validateRecommendations, candidateFilter,
  RecommendInvalid, type Candidate,
} from "../_shared/recommend.ts";
import type { Analysis } from "../_shared/analysis.ts";

Deno.serve(async (req) => {
  const db = admin();
  const auth = await requireUser(req, db);
  if (auth instanceof Response) return auth;
  const { userId } = auth;

  try {
    // 가장 최근 분석 하나를 쓴다. 설문 수정 시 superseded_by로 이어진다 (D-06).
    const { data: analysisRow } = await db.from("analyses")
      .select("id, input, result").eq("user_id", userId)
      .is("superseded_by", null).order("created_at", { ascending: false })
      .limit(1).maybeSingle();

    if (!analysisRow) {
      return json({ error: "먼저 설문 분석이 필요합니다", needsAnalysis: true }, 409);
    }

    const analysis = analysisRow.result as Analysis;

    // 이미 만든 추천이 있으면 재생성하지 않는다 (LLM 호출·비용 절약).
    const { data: cached } = await db.from("recommendations")
      .select("animal_seq, reason, rank").eq("analysis_id", analysisRow.id)
      .order("rank");
    if (cached?.length === 3 && !new URL(req.url).searchParams.has("refresh")) {
      // 신규 생성 경로와 같은 형태(seq·reason)로 정규화한다.
      // 캐시냐 아니냐에 따라 키가 달라지면 클라가 두 형태를 다 다뤄야 한다.
      return json({
        analysisId: analysisRow.id,
        cached: true,
        picks: cached.map((r) => ({ seq: r.animal_seq, reason: r.reason })),
      });
    }

    // 후보 좁히기는 SQL이 한다. 목록 전체를 LLM에 넘기지 않는다.
    //
    // traits가 없는 개체는 후보에서 뺀다. 성격 정보 없이 추천하면
    // "당신은 A라고 하셨고 이 아이는 B입니다"라는 이유를 쓸 근거가 없고,
    // 모델이 없는 사실을 지어낼 여지가 생긴다 (D-03 창작 금지).
    const SELECT = "seq, name, animal_type, breed, sex, weight_kg, foster_ok, traits";
    const { fosterOnly } = candidateFilter(analysis.participation.recommended);

    let q = db.from("shelter_animals").select(SELECT)
      .eq("adopt_status", "입양문의가능").not("traits", "is", null).limit(20);
    if (fosterOnly) q = q.eq("foster_ok", true);

    // 쿼리 오류를 삼키면 "후보 0건"으로 보여 원인을 못 찾는다. 반드시 확인한다.
    let { data: candidates, error: candErr } = await q;
    if (candErr) throw candErr;

    // 임보 가능한 아이가 부족하면(현재 24건 중 2건뿐) 임보 조건만 풀고 넓힌다.
    if (fosterOnly && (candidates?.length ?? 0) < 3) {
      const { data: wider, error: widerErr } = await db.from("shelter_animals").select(SELECT)
        .eq("adopt_status", "입양문의가능").not("traits", "is", null).limit(20);
      if (widerErr) throw widerErr;
      candidates = wider;
    }
    if (!candidates || candidates.length < 3) {
      return json({ error: "추천할 보호견이 부족합니다", available: candidates?.length ?? 0 }, 503);
    }

    // 사용자가 직접 쓴 문장 — 추천 이유가 이걸 인용해야 한다
    const input = analysisRow.input as { answers?: Record<string, unknown> };
    const quotes = [input.answers?.q4, input.answers?.q5]
      .filter((v): v is string => typeof v === "string");

    const llm = fromEnv((k) => Deno.env.get(k));
    const messages = buildRecommendMessages(analysis, quotes, candidates as Candidate[]);

    let picks;
    try {
      picks = validateRecommendations(await llm.json(messages, { maxTokens: 1000 }), candidates as Candidate[]);
    } catch (first) {
      if (!(first instanceof RecommendInvalid)) throw first;
      picks = validateRecommendations(
        await llm.json(messages, { maxTokens: 1000, temperature: 0.3 }),
        candidates as Candidate[],
      );
    }

    await db.from("recommendations").delete().eq("analysis_id", analysisRow.id);
    const { error: insErr } = await db.from("recommendations").insert(
      picks.map((p, i) => ({
        user_id: userId, analysis_id: analysisRow.id,
        animal_seq: p.seq, reason: p.reason, rank: i + 1,
      })),
    );
    if (insErr) throw insErr;

    return json({ analysisId: analysisRow.id, cached: false, picks });
  } catch (e) {
    if (e instanceof RecommendInvalid) {
      return json({ error: "추천 결과 검증 실패", detail: e.message, retryable: true }, 502);
    }
    return json({ error: String(e), retryable: true }, 503);
  }
});
