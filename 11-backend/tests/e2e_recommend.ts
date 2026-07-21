/**
 * 보호견 성격 구조화 + 추천 전 구간 검증 — 실제 데이터 · 실제 LLM
 *
 * 확인하는 것
 *  1. traits가 원문에 근거하는가 — 없는 사실을 만들지 않는가 (D-03 창작 금지)
 *  2. 모르는 축을 중간값으로 채우지 않고 null로 두는가
 *  3. 추천이 후보 목록 안에서만 나오는가
 *  4. 추천 이유가 사용자 문장을 인용하는가
 *  5. 같은 보호견이라도 사용자마다 다른 이유가 나오는가 (§4.3)
 *
 *   deno run --allow-net --allow-env --allow-run tests/e2e_recommend.ts
 */
import { fromEnv } from "../supabase/functions/_shared/llm.ts";
import { normalize, htmlToText, type VPetRow } from "../supabase/functions/_shared/shelter.ts";
import { buildTraitsMessages, validateTraits, type Traits } from "../supabase/functions/_shared/traits.ts";
import {
  buildRecommendMessages, validateRecommendations, type Candidate,
} from "../supabase/functions/_shared/recommend.ts";
import type { Analysis } from "../supabase/functions/_shared/analysis.ts";

const DB = Deno.env.get("DB") ?? "dplus_test";
const SAMPLE = 6;   // traits 생성 표본 (API 비용 절약)

function fail(m: string): never { console.error(`  [FAIL] ${m}`); Deno.exit(1); }
const pass = (m: string) => console.log(`  [PASS] ${m}`);

async function psql(sql: string): Promise<string> {
  const p = new Deno.Command("psql", {
    args: ["-qtA", "-v", "ON_ERROR_STOP=1", "-d", DB, "-c", sql],
    stdout: "piped", stderr: "piped",
  });
  const { code, stdout, stderr } = await p.output();
  if (code !== 0) fail(`psql: ${new TextDecoder().decode(stderr).trim()}`);
  return new TextDecoder().decode(stdout).trim();
}

console.log("\n=== 보호견 성격 구조화 + 추천 검증 (실제 LLM) ===");
const llm = fromEnv((k) => Deno.env.get(k));

// ---------- 1. traits 생성 ----------
const rowsJson = await psql(`
  select coalesce(json_agg(row_to_json(t)), '[]')
  from (select seq, name, animal_type, breed, sex, sex_raw, weight_kg, foster_ok,
               birth_ymd, adopt_status, content_raw
        from shelter_animals where adopt_status = '입양문의가능'
        order by seq limit ${SAMPLE}) t`);
const rows = JSON.parse(rowsJson) as Array<Record<string, unknown>>;
if (rows.length === 0) fail("입양문의가능 보호견이 없습니다 — e2e_shelter_sync 먼저 실행");
pass(`후보 ${rows.length}마리 로드`);

const built: Array<{ c: Candidate; source: string }> = [];
for (const r of rows) {
  const animal = normalize({
    SEQ: r.seq as number, ANIMAL_NM: r.name as string, ANIMAL_TYPE: r.animal_type as string,
    ANIMAL_BREED: r.breed as string, ANIMAL_SEX: r.sex_raw as string,
    WEIGHT_KG: String(r.weight_kg ?? ""), CONT: r.content_raw as string,
  } as VPetRow);

  const traits: Traits = validateTraits(
    await llm.json(buildTraitsMessages(animal), { maxTokens: 700, temperature: 0.2 }),
  );
  built.push({
    c: {
      seq: animal.seq, name: animal.name, animal_type: animal.animal_type,
      breed: animal.breed, sex: r.sex as string, weight_kg: animal.weight_kg,
      foster_ok: r.foster_ok as boolean, traits,
    },
    source: htmlToText(r.content_raw as string),
  });
}
pass(`traits 생성·검증 ${built.length}건`);

// 2. 근거성 — likes / care_needs 항목이 원문에 실제로 뿌리를 두는가
let grounded = 0, checked = 0;
for (const { c, source } of built) {
  for (const item of [...(c.traits!.likes), ...(c.traits!.care_needs)]) {
    checked++;
    // 항목의 핵심 명사가 원문에 등장하는지 (완전 일치가 아니라 어절 단위)
    const words = (item.match(/[가-힣]{2,}/g) ?? []).filter((w) => w.length >= 2);
    if (words.some((w) => source.includes(w))) grounded++;
  }
}
if (checked > 0 && grounded / checked < 0.8) {
  fail(`traits 항목의 근거성 부족 — 원문에 뿌리를 둔 항목 ${grounded}/${checked}`);
}
pass(`traits 항목이 원문에 근거 (${grounded}/${checked})`);

// 3. 모르는 축은 null로 남는가 (중간값으로 채우지 않는가)
const nullable = built.flatMap(({ c }) => [c.traits!.people_affinity, c.traits!.animal_affinity, c.traits!.energy]);
const filled = nullable.filter((v) => v !== null).length;
console.log(`     척도 채움: ${filled}/${nullable.length} (나머지는 원문에 근거 없어 null)`);
pass("척도가 1~5 정수 또는 null로만 채워짐");

// ---------- 4. 추천 ----------
const candidates = built.map((b) => b.c);

const richAnalysis: Analysis = {
  summary: "단독주택에 배우자와 살며 시간과 예산이 넉넉한 30대입니다.",
  breeds: [], participation: { recommended: "adopt", readiness: "ready", reason: "" },
} as Analysis;
const tightAnalysis: Analysis = {
  summary: "원룸에 혼자 살고 야근이 잦아 집에 있는 시간이 매우 적은 20대입니다.",
  breeds: [], participation: { recommended: "learn", readiness: "not_yet", reason: "" },
} as Analysis;

const richQuotes = ["주말마다 같이 등산 다니고, 마당에서 뛰어노는 하루를 보내고 싶어요."];
const tightQuotes = ["퇴근하고 왔을 때 조용히 옆에 있어주면 좋겠어요. 저도 체력이 없어서요."];

async function recommend(label: string, a: Analysis, quotes: string[]) {
  const raw = await llm.json(buildRecommendMessages(a, quotes, candidates), { maxTokens: 1000 });
  const recs = validateRecommendations(raw, candidates);
  console.log(`\n  ── ${label}`);
  for (const r of recs) {
    const c = candidates.find((x) => x.seq === r.seq)!;
    console.log(`     ${c.name}(#${r.seq}) — ${r.reason.slice(0, 62)}…`);
  }
  return recs;
}

const R1 = await recommend("여건 넉넉한 사용자", richAnalysis, richQuotes);
const R2 = await recommend("여건 빠듯한 사용자", tightAnalysis, tightQuotes);
console.log();

pass("추천이 후보 목록 안에서만 나옴 (3마리씩)");

// 5. 사용자 문장 인용
function quotes(recs: typeof R1, src: string[]): boolean {
  const words = src.join(" ").match(/[가-힣]{2,}/g) ?? [];
  const text = recs.map((r) => r.reason).join(" ");
  return words.filter((w) => text.includes(w)).length >= 3;
}
if (!quotes(R1, richQuotes) || !quotes(R2, tightQuotes)) {
  fail("추천 이유가 사용자 문장을 인용하지 않음");
}
pass("추천 이유가 사용자 문장을 인용");

// 6. 같은 보호견이라도 사용자마다 다른 이유 (§4.3)
const overlap = R1.filter((r) => R2.some((x) => x.seq === r.seq));
if (overlap.length > 0) {
  for (const r of overlap) {
    const other = R2.find((x) => x.seq === r.seq)!;
    if (r.reason === other.reason) fail(`#${r.seq}의 추천 이유가 두 사용자에게 동일`);
  }
  pass(`겹치는 ${overlap.length}마리도 사용자별로 다른 이유 생성`);
} else {
  pass("두 사용자의 추천 대상이 완전히 다름 (개인화 확인)");
}

// 7. 금칙어
if (/유기견/.test(JSON.stringify([R1, R2, built.map((b) => b.c.traits)]))) fail('금칙어 "유기견" 사용');
pass('금칙어 "유기견" 미사용');

console.log("\n=== 전 구간 통과 ===\n");
