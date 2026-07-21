/**
 * 전 구간 검증 — 실제 API → 정규화 → Postgres 적재
 *
 * 픽스처가 아니라 **살아있는 vPetInfo API**를 호출한다.
 * 파서 단위 테스트(shelter_test.ts)는 오프라인 픽스처를 쓰지만,
 * 이 스크립트는 "운영에서 실제로 도는가"를 확인한다.
 *
 *   SEOUL_API_KEY=... deno run --allow-net --allow-env --allow-run tests/e2e_shelter_sync.ts
 */
// 핸들러(index.ts)가 아니라 부작용 없는 모듈에서 가져온다.
// 핸들러는 최상단에서 Deno.serve()를 호출하므로 import 만으로 서버가 뜬다.
import { fetchAll } from "../supabase/functions/_shared/vpet.ts";
import { normalize, findPersonality } from "../supabase/functions/_shared/shelter.ts";

const DB = Deno.env.get("DB") ?? "dplus_test";
const KEY = Deno.env.get("SEOUL_API_KEY");
if (!KEY) {
  console.error("SEOUL_API_KEY 가 필요합니다 (.env.local 참조)");
  Deno.exit(1);
}

function fail(msg: string): never {
  console.error(`  [FAIL] ${msg}`);
  Deno.exit(1);
}
const pass = (msg: string) => console.log(`  [PASS] ${msg}`);

async function psql(sql: string): Promise<string> {
  const p = new Deno.Command("psql", {
    args: ["-qtA", "-v", "ON_ERROR_STOP=1", "-d", DB, "-c", sql],
    stdout: "piped",
    stderr: "piped",
  });
  const { code, stdout, stderr } = await p.output();
  if (code !== 0) fail(`psql: ${new TextDecoder().decode(stderr).trim()}`);
  return new TextDecoder().decode(stdout).trim();
}

console.log("\n=== 전 구간 검증: 실제 vPetInfo API → Postgres ===");

// 1) 살아있는 API 호출
const rows = await fetchAll(KEY);
if (rows.length === 0) fail("API가 0건을 반환");
pass(`실제 API 응답 ${rows.length}건 수신`);

// 2) 정규화 — 파서가 실데이터에서 깨지지 않는가
const records = rows.map(normalize);
const noPersonality = records.filter((a) => !findPersonality(a.sections));
if (noPersonality.length > 0) {
  fail(`성격 섹션 추출 실패: ${noPersonality.map((a) => `${a.seq}:${a.name}`).join(", ")}`);
}
pass(`정규화 ${records.length}건, 성격 섹션 전건 추출`);

// 3) 적재 — 스키마 제약을 실제로 통과하는가
await psql("truncate shelter_animals cascade");
for (const a of records) {
  const q = (v: unknown) =>
    v === null || v === undefined ? "null" : `'${String(v).replace(/'/g, "''")}'`;
  await psql(`
    insert into shelter_animals
      (seq, name, animal_type, breed, sex_raw, birth_ymd, weight_kg,
       adopt_status, foster_ok, movie_url, content_raw)
    values (${a.seq}, ${q(a.name)}, ${q(a.animal_type)}, ${q(a.breed)}, ${q(a.sex_raw)},
            ${a.birth_ymd ? q(a.birth_ymd) : "null"}, ${a.weight_kg ?? "null"},
            ${a.adopt_status ? `${q(a.adopt_status)}::adopt_status` : "null"},
            ${a.foster_ok}, ${q(a.movie_url)}, ${q(a.content_raw)})
    on conflict (seq) do update set
      name = excluded.name, adopt_status = excluded.adopt_status,
      foster_ok = excluded.foster_ok, content_raw = excluded.content_raw,
      synced_at = now()`);
}
const stored = Number(await psql("select count(*) from shelter_animals"));
if (stored !== records.length) fail(`적재 건수 불일치 (${stored}/${records.length})`);
pass(`Postgres 적재 ${stored}건`);

// 4) 생성 컬럼 — vPetInfo 'W' → female 매핑이 DB에서 실제로 되는가
const sexes = await psql(
  "select sex || ':' || count(*) from shelter_animals group by sex order by 1",
);
if (!sexes.includes("female")) fail(`성별 매핑 실패 (W→female). 실제: ${sexes}`);
pass(`성별 생성 컬럼 매핑 (${sexes.split("\n").join(", ")})`);

// 5) 추천 대상 필터가 실제로 걸러지는가
const adoptable = Number(
  await psql("select count(*) from shelter_animals where adopt_status = '입양문의가능'"),
);
const fosterable = Number(await psql("select count(*) from shelter_animals where foster_ok"));
if (adoptable === 0) fail("입양문의가능 개체가 0건 — 추천 대상이 비어 있음");
pass(`추천 대상 입양문의가능 ${adoptable}건 / 임시보호가능 ${fosterable}건`);

// 6) 멱등 — 같은 동기화를 두 번 돌려도 늘어나지 않는가
const before = await psql("select count(*) from shelter_animals");
for (const a of records.slice(0, 3)) {
  await psql(
    `insert into shelter_animals (seq, name, animal_type) values (${a.seq}, '${a.name}', '${a.animal_type}')
     on conflict (seq) do update set synced_at = now()`,
  );
}
const after = await psql("select count(*) from shelter_animals");
if (before !== after) fail(`재동기화로 건수가 변함 (${before} → ${after})`);
pass(`재동기화 멱등 (${after}건 유지)`);

console.log("\n=== 전 구간 통과 ===\n");
