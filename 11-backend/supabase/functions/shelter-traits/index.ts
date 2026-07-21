/**
 * 보호견 성격 구조화 (CRON, service_role 전용)
 *
 * shelter-sync가 원문만 싣고 가면, 이 함수가 traits를 채운다.
 * 전체가 24건 수준이라 한 번에 다 돌려도 부담이 없다.
 *
 * D-03: 원문에 없는 사실을 만들지 않는다. 프롬프트에는 groundingFacts()가
 *       만든 원문 발췌만 들어간다 (_shared/traits.ts).
 */
import { json, admin, requireServiceRole } from "../_shared/http.ts";
import { fromEnv } from "../_shared/llm.ts";
import { normalize, type VPetRow } from "../_shared/shelter.ts";
import { buildTraitsMessages, validateTraits, TraitsInvalid } from "../_shared/traits.ts";

Deno.serve(async (req) => {
  const denied = requireServiceRole(req);
  if (denied) return denied;

  const db = admin();
  const url = new URL(req.url);
  // 기본은 traits가 비어 있는 것만. ?all=1 이면 전부 재생성 (원문이 갱신된 경우).
  const all = url.searchParams.get("all") === "1";
  const limit = Number(url.searchParams.get("limit") ?? "50");

  let q = db.from("shelter_animals")
    .select("seq, name, animal_type, breed, sex_raw, weight_kg, birth_ymd, content_raw")
    .not("content_raw", "is", null)
    .limit(limit);
  if (!all) q = q.is("traits", null);

  const { data: rows, error } = await q;
  if (error) return json({ error: error.message }, 500);
  if (!rows?.length) return json({ processed: 0, message: "대상 없음" });

  const llm = fromEnv((k) => Deno.env.get(k));
  const failures: Array<{ seq: number; reason: string }> = [];
  let ok = 0;

  for (const r of rows) {
    try {
      const animal = normalize({
        SEQ: r.seq, ANIMAL_NM: r.name, ANIMAL_TYPE: r.animal_type,
        ANIMAL_BREED: r.breed ?? undefined, ANIMAL_SEX: r.sex_raw ?? undefined,
        WEIGHT_KG: r.weight_kg != null ? String(r.weight_kg) : undefined,
        ANIMAL_BRITH_YMD: r.birth_ymd ?? undefined,
        CONT: r.content_raw ?? undefined,
      } as VPetRow);

      const traits = validateTraits(
        // 성격 추출은 창작이 아니라 정리다. 온도를 낮춰 원문에 붙인다.
        await llm.json(buildTraitsMessages(animal), { maxTokens: 700, temperature: 0.2 }),
      );

      const { error: upErr } = await db.from("shelter_animals")
        .update({ traits }).eq("seq", r.seq);
      if (upErr) throw upErr;
      ok++;
    } catch (e) {
      // 한 마리가 실패해도 나머지는 진행한다.
      failures.push({ seq: r.seq, reason: e instanceof TraitsInvalid ? e.message : String(e) });
    }
  }

  return json({ processed: ok, failed: failures.length, failures });
});
