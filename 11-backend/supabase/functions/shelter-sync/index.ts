/**
 * 보호견 동기화 (CRON) — 서울 vPetInfo (D-019)
 *
 * 개별 조회 API가 없어 목록을 통째로 받아 스냅샷으로 덮는다.
 * 전체가 24건 수준이라 증분 동기화를 만들 이유가 없다.
 *
 * 인증키는 환경변수로만 받는다. Vault·코드에 두지 않는다 (팀 규칙 §8).
 *   SEOUL_API_KEY               서울 열린데이터광장 인증키
 *   SUPABASE_URL
 *   SUPABASE_SERVICE_ROLE_KEY   shelter_animals 쓰기는 service_role만 (0003_rls.sql)
 */
import { createClient } from "jsr:@supabase/supabase-js@2";
import { normalize } from "../_shared/shelter.ts";
import { fetchAll } from "../_shared/vpet.ts";

Deno.serve(async () => {
  const key = Deno.env.get("SEOUL_API_KEY");
  if (!key) return json({ error: "SEOUL_API_KEY 미설정" }, 500);

  try {
    const rows = await fetchAll(key);
    const records = rows.map(normalize).map((a) => ({
      seq: a.seq,
      name: a.name,
      animal_type: a.animal_type,
      breed: a.breed,
      sex_raw: a.sex_raw,
      birth_ymd: a.birth_ymd,
      weight_kg: a.weight_kg,
      adopt_status: a.adopt_status,
      foster_ok: a.foster_ok,
      movie_url: a.movie_url,
      content_raw: a.content_raw,
      // traits(성격 5축 구조화)는 별도 LLM 단계에서 채운다. 여기서는 원문만 싣는다.
      synced_at: new Date().toISOString(),
    }));

    const db = createClient(
      Deno.env.get("SUPABASE_URL")!,
      Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
    );

    const { error } = await db.from("shelter_animals").upsert(records, { onConflict: "seq" });
    if (error) throw error;

    // 목록에서 사라진 개체는 지우지 않는다 — 입양 완료된 아이의 추천 이력이 끊기면
    // "이 아이는 가족을 만났어요" 같은 폐루프 연출을 만들 수 없다.
    return json({
      synced: records.length,
      fosterable: records.filter((r) => r.foster_ok).length,
    });
  } catch (e) {
    return json({ error: String(e) }, 502);
  }
});

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}
