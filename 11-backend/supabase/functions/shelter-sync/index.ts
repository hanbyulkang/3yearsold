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
import { fetchAll, fetchAllImages } from "../_shared/vpet.ts";

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

    // 사진은 별도 서비스다 (vPetImg, 0013). SEQ가 같은 키.
    // 개체를 먼저 넣어야 FK가 걸리므로 순서를 지킨다.
    let photoCount = 0;
    try {
      const known = new Set(records.map((r) => r.seq));
      const photos = (await fetchAllImages(key))
        .map((p) => ({
          seq: Number(p.SEQ),
          img_type: String(p.IMG_TYPE).toUpperCase(),
          img_num: Number(p.IMG_NUM),
          img_url: p.IMG_URL,
          synced_at: new Date().toISOString(),
        }))
        // 스냅샷에 없는 개체의 사진은 FK에 걸리므로 거른다
        .filter((p) => known.has(p.seq) && (p.img_type === "THUMB" || p.img_type === "IMG"));

      if (photos.length) {
        const { error: pErr } = await db.from("shelter_animal_photos")
          .upsert(photos, { onConflict: "seq,img_type,img_num" });
        if (pErr) throw pErr;
        photoCount = photos.length;
      }
    } catch (e) {
      // 사진 실패가 개체 동기화를 되돌리지 않는다. 사진은 다음 실행에서 다시 시도된다.
      console.error("사진 동기화 실패:", e);
    }

    // 목록에서 사라진 개체는 지우지 않는다 — 입양 완료된 아이의 추천 이력이 끊기면
    // "이 아이는 가족을 만났어요" 같은 폐루프 연출을 만들 수 없다.
    return json({
      synced: records.length,
      fosterable: records.filter((r) => r.foster_ok).length,
      photos: photoCount,
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
