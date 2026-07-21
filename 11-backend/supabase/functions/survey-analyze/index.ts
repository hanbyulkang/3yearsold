/**
 * AI 상황 분석 (와이어프레임 A-08)
 *
 * 설문 제출 시 한 번 호출된다. 결과는 analyses 레코드 하나로 저장되고,
 * 이후 견종·보호견·참여 추천이 전부 이 레코드를 참조한다 (PRD §4.3 단일 엔진).
 *
 * 규칙
 *  · LLM 호출은 이 Edge Function 안에서만. 키를 WebGL 클라에 노출하지 않는다.
 *  · 타임아웃 30초 → 재시도 1회 → **실패해도 설문 응답은 보존한다.**
 *    설문은 이미 문항 단위로 저장돼 있으므로, 분석 실패는 재시도로 해결된다.
 */
import { createClient } from "jsr:@supabase/supabase-js@2";
import { fromEnv, LlmError } from "../_shared/llm.ts";
import { buildMessages, validate, AnalysisInvalid, type Breed } from "../_shared/analysis.ts";

Deno.serve(async (req) => {
  const auth = req.headers.get("Authorization") ?? "";
  if (!auth.startsWith("Bearer ")) return json({ error: "인증이 필요합니다" }, 401);

  const admin = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
  );

  // 토큰에서 사용자를 확인한다. 클라가 보낸 user_id를 믿지 않는다.
  const { data: userData, error: userErr } = await admin.auth.getUser(auth.replace("Bearer ", ""));
  if (userErr || !userData?.user) return json({ error: "유효하지 않은 토큰" }, 401);
  const userId = userData.user.id;

  try {
    await admin.rpc("ensure_profile", { p_user: userId });

    const [{ data: responses }, { data: followups }, { data: cfg }, { data: breedRows }] =
      await Promise.all([
        admin.from("survey_responses").select("question_id, value").eq("user_id", userId),
        admin.from("survey_followups").select("question_id, probe, answer, skipped").eq("user_id", userId),
        admin.from("config").select("value").eq("key", "breeds").maybeSingle(),
        // 견종 정본은 breeds 테이블이다 (0007). config에는 고정 견종 설정만 남는다.
        admin.from("breeds")
          .select("name, activity, timid, affection, image_url, image_license, image_author, attribution_required")
          .order("sort_order"),
      ]);

    const answers: Record<string, unknown> = {};
    for (const r of responses ?? []) answers[r.question_id] = r.value;

    // 필수 서술 2문항이 없으면 분석할 재료가 없다 (PRD §4.3)
    if (!answers.q4 || !answers.q5) {
      return json({ error: "필수 문항(Q4·Q5)이 아직 없습니다" }, 400);
    }

    const breeds: Breed[] = (breedRows ?? []).map((b) => ({
      name: b.name, activity: b.activity, timid: b.timid, affection: b.affection,
    }));
    if (!breeds.length) return json({ error: "견종 목록이 비어 있습니다 (breeds 테이블)" }, 500);
    // 3D 에셋이 준비된 견종은 항상 후보에 넣는다 (config로 관리 — 코드에 박지 않는다)
    const pinned: string[] = cfg?.value?.pinned ?? [];

    const input = {
      answers,
      followups: (followups ?? []).map((f) => ({
        forId: f.question_id, probe: f.probe, answer: f.answer, skipped: f.skipped,
      })),
    };

    const llm = fromEnv((k) => Deno.env.get(k));
    const messages = buildMessages(input, breeds, pinned);

    // 검증 실패(고정 견종 누락, 화이트리스트 위반 등)는 재생성으로 대부분 해결된다.
    // llm.json 내부 재시도는 호출 실패용이므로, 검증 실패는 여기서 한 번 더 돌린다.
    let analysis;
    try {
      analysis = validate(await llm.json(messages, { maxTokens: 1200 }), breeds, pinned);
    } catch (first) {
      if (!(first instanceof AnalysisInvalid)) throw first;
      analysis = validate(
        await llm.json(messages, { maxTokens: 1200, temperature: 0.3 }),
        breeds,
        pinned,
      );
    }

    // 설문 수정으로 재분석하는 경우, 이전 레코드를 superseded로 잇는다 (와이어프레임 D-06)
    const { data: prev } = await admin
      .from("analyses").select("id").eq("user_id", userId)
      .is("superseded_by", null).order("created_at", { ascending: false }).limit(1).maybeSingle();

    const { data: saved, error: saveErr } = await admin
      .from("analyses")
      .insert({ user_id: userId, input, result: analysis })
      .select("id").single();
    if (saveErr) throw saveErr;

    if (prev?.id) {
      await admin.from("analyses").update({ superseded_by: saved.id }).eq("id", prev.id);
    }

    // 화면(A-09)은 사진과 성격 프리필까지 한 번에 받아야 한다.
    // CC BY 계열은 출처 표기가 의무라 라이선스 정보를 함께 내려보낸다.
    const byName = new Map((breedRows ?? []).map((b) => [b.name, b]));
    const enriched = analysis.breeds.map((b) => {
      const meta = byName.get(b.name);
      return {
        ...b,
        imageUrl: meta?.image_url ?? null,
        personality: meta
          ? { activity: meta.activity, timid: meta.timid, affection: meta.affection }
          : null,
        attribution: meta?.attribution_required
          ? { author: meta.image_author, license: meta.image_license }
          : null,
      };
    });

    return json({ analysisId: saved.id, ...analysis, breeds: enriched });
  } catch (e) {
    // 분석 실패가 설문을 날리지 않는다. 클라는 재시도만 하면 된다.
    if (e instanceof AnalysisInvalid) {
      return json({ error: "분석 결과 검증 실패", detail: e.message, retryable: true }, 502);
    }
    if (e instanceof LlmError) {
      return json({ error: "AI 분석에 실패했습니다", detail: e.message, retryable: true }, 503);
    }
    return json({ error: String(e) }, 500);
  }
});

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}
