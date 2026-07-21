/**
 * AI 상황 분석 전 구간 검증 — 실제 LLM 호출
 *
 * 확인하는 것
 *  1. 화이트리스트 밖 견종이 나오지 않는가 (A-09)
 *  2. 사용자 문장을 실제로 인용하는가 (§4.3 "당신이 쓴 문장을 읽고 추천했다")
 *  3. 서로 다른 설문에 서로 다른 결과가 나오는가 (개인화가 실재하는가)
 *  4. 금칙어 "유기견"이 나오지 않는가 (부록 A)
 *  5. 입양을 서두르게 하지 않는가 — 여건이 부족한 사용자에게 adopt를 권하지 않는가
 *
 *   OPENAI_API_KEY=... deno run --allow-net --allow-env --allow-run tests/e2e_analysis.ts
 */
import { fromEnv } from "../supabase/functions/_shared/llm.ts";
import { buildMessages, validate, personalityFor, type Breed, type SurveyInput } from "../supabase/functions/_shared/analysis.ts";

const DB = Deno.env.get("DB") ?? "dplus_test";

function fail(msg: string): never {
  console.error(`  [FAIL] ${msg}`);
  Deno.exit(1);
}
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

console.log("\n=== AI 상황 분석 전 구간 검증 (실제 LLM 호출) ===");

// 견종 목록은 서버 설정에서 읽는다. 클라·코드에 복제하지 않는다(A-05).
const breeds: Breed[] = JSON.parse(
  await psql("select value->'list' from config where key = 'breeds'"),
);
const pinned: string[] = JSON.parse(
  await psql("select coalesce(value->'pinned','[]'::jsonb) from config where key = 'breeds'"),
);
if (!breeds.length) fail("config.breeds 가 비어 있음 — 0006 마이그레이션 확인");
pass(`견종 화이트리스트 ${breeds.length}종 로드 (고정: ${pinned.join(", ") || "없음"})`);

const llm = fromEnv((k) => Deno.env.get(k));

// 여건이 넉넉한 사용자
const rich: SurveyInput = {
  answers: {
    q1: { age: "30대", housing: "단독주택", household: "배우자·파트너" },
    q2: { hours: "8시간 이상" },
    q3: { budget: "30만원 이상" },
    q4: "짖는 데는 이유가 있을 테니 왜 그러는지 먼저 관찰하고, 필요하면 훈련사 상담도 받아볼 생각이에요.",
    q5: "주말마다 같이 등산 다니고, 마당에서 뛰어노는 하루를 보내고 싶어요.",
  },
  followups: [],
};

// 여건이 빠듯한 사용자
const tight: SurveyInput = {
  answers: {
    q1: { age: "20대", housing: "원룸·오피스텔", household: "혼자" },
    q2: { hours: "2시간 미만" },
    q3: { budget: "5만원 미만" },
    q4: "솔직히 이웃 항의가 제일 걱정돼요. 밤에 잠도 못 잘 것 같고요.",
    q5: "퇴근하고 왔을 때 조용히 옆에 있어주면 좋겠어요. 저도 체력이 없어서요.",
  },
  followups: [
    { forId: "q4", probe: "이웃 항의가 걱정이신가요, 잠을 못 자는 게 더 힘드실까요?",
      answer: "둘 다요. 지금도 야근이 잦아서 집에 거의 없어요.", skipped: false },
  ],
};

async function analyze(name: string, input: SurveyInput) {
  const t0 = Date.now();
  const raw = await llm.json(buildMessages(input, breeds, pinned), { maxTokens: 1200 });
  const a = validate(raw, breeds, pinned);
  console.log(`\n  ── ${name} (${Date.now() - t0}ms)`);
  console.log(`     요약: ${a.summary.slice(0, 90)}…`);
  console.log(`     견종: ${a.breeds.map((b) => b.name).join(" / ")}`);
  console.log(`     참여: ${a.participation.recommended} (${a.participation.readiness})`);
  return a;
}

const A = await analyze("여건 넉넉한 사용자", rich);
const B = await analyze("여건 빠듯한 사용자", tight);
console.log();

// 1. 화이트리스트 — validate()가 이미 던지지만 명시적으로 확인
const allowed = new Set(breeds.map((b) => b.name));
for (const a of [A, B]) {
  for (const b of a.breeds) if (!allowed.has(b.name)) fail(`화이트리스트 밖 견종: ${b.name}`);
}
pass("견종 화이트리스트 준수 (각 3종)");

// 2. 사용자 문장 인용 — 설문에서 쓴 단어가 이유에 실제로 등장하는가
function quotesUser(a: typeof A, input: SurveyInput): boolean {
  const src = [input.answers.q4, input.answers.q5].join(" ");
  const words = src.match(/[가-힣]{2,}/g) ?? [];
  const reasons = a.breeds.map((b) => b.reason).join(" ");
  const hits = words.filter((w) => w.length >= 2 && reasons.includes(w));
  return hits.length >= 3;
}
if (!quotesUser(A, rich) || !quotesUser(B, tight)) {
  fail("추천 이유가 사용자 문장을 인용하지 않음 — 개인화 근거 부족");
}
pass("추천 이유가 사용자 문장을 인용");

// 3. 개인화 — 서로 다른 설문에 다른 결과
const sameBreeds = A.breeds.map((b) => b.name).sort().join() === B.breeds.map((b) => b.name).sort().join();
if (sameBreeds && A.participation.recommended === B.participation.recommended) {
  fail("두 사용자의 견종·참여 추천이 완전히 동일 — 개인화가 작동하지 않음");
}
pass("서로 다른 설문 → 서로 다른 결과 (개인화 확인)");

// 4. 금칙어
for (const a of [A, B]) {
  if (/유기견/.test(JSON.stringify(a))) fail('금칙어 "유기견" 사용');
}
pass('금칙어 "유기견" 미사용 (부록 A)');

// 5. 입양을 서두르게 하지 않는가 (§1.2 원칙 4)
if (B.participation.recommended === "adopt") {
  fail("원룸·2시간 미만·5만원 미만 사용자에게 즉시 입양을 권함 — 참여 장벽 원칙 위반");
}
pass(`여건 빠듯한 사용자에게 입양을 서두르지 않음 (→ ${B.participation.recommended})`);

// 6. 고정 견종이 항상 포함되는가 (3D 에셋 렌더 보장)
for (const [name, a] of [["넉넉", A], ["빠듯", B]] as const) {
  for (const p of pinned) {
    if (!a.breeds.some((b) => b.name === p)) fail(`${name} 사용자 결과에 고정 견종 ${p} 누락`);
  }
}
if (pinned.length) {
  const reason = A.breeds.find((b) => b.name === pinned[0])!.reason;
  pass(`고정 견종 ${pinned[0]} 양쪽 결과에 포함 — "${reason.slice(0, 46)}…"`);
}

// 7. 견종 선택 시 성격 프리필 (A-10)
const p = personalityFor(A.breeds[0].name, breeds);
if (!(p.activity >= 1 && p.activity <= 5)) fail("성격 프리필 값 범위 오류");
pass(`견종 선택 시 성격 프리필 (${A.breeds[0].name} → 활동성 ${p.activity})`);

console.log("\n=== 전 구간 통과 ===\n");
