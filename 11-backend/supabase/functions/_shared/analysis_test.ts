/**
 * 분석 검증 로직 테스트 (오프라인 — LLM 호출 없음)
 *
 * 실제 LLM 호출 검증은 tests/e2e_analysis.ts 가 담당한다.
 * 여기서는 "모델이 이상한 걸 뱉었을 때 막히는가"만 본다.
 */
import { validate, buildMessages, personalityFor, AnalysisInvalid, type Breed } from "./analysis.ts";
import { extractJson } from "./llm.ts";

function ok(c: unknown, m: string) { if (!c) throw new Error(`단언 실패: ${m}`); }
function throws(fn: () => unknown, m: string) {
  try { fn(); } catch { return; }
  throw new Error(`단언 실패: ${m} — 예외가 발생하지 않음`);
}

const BREEDS: Breed[] = [
  { name: "믹스견", activity: 3, timid: 3, affection: 4 },
  { name: "푸들", activity: 4, timid: 2, affection: 4 },
  { name: "시츄", activity: 2, timid: 2, affection: 4 },
  { name: "비글", activity: 5, timid: 1, affection: 4 },
];

const valid = {
  summary: "혼자 지내시고 시간이 넉넉하지 않은 상황입니다.",
  breeds: [
    { name: "시츄", reason: "'조용히 옆에 있어주면'이라고 쓰신 것처럼" },
    { name: "푸들", reason: "활동량이 조절 가능해서" },
    { name: "믹스견", reason: "적응력이 좋아서" },
  ],
  participation: { recommended: "learn", readiness: "not_yet", reason: "지금은 알아가는 단계가 좋겠습니다" },
};

Deno.test("정상 응답은 통과한다", () => {
  const a = validate(structuredClone(valid), BREEDS);
  ok(a.breeds.length === 3, "견종 3개");
});

Deno.test("화이트리스트 밖 견종은 막는다 (A-09)", () => {
  const bad = structuredClone(valid);
  bad.breeds[0].name = "시베리안 허스키";   // 목록에 없음
  throws(() => validate(bad, BREEDS), "목록 밖 견종이 통과됨");
});

Deno.test("견종이 3개가 아니면 막는다", () => {
  const two = structuredClone(valid);
  two.breeds.pop();
  throws(() => validate(two, BREEDS), "2개인데 통과됨");
});

Deno.test("견종 중복을 막는다", () => {
  const dup = structuredClone(valid);
  dup.breeds[1].name = dup.breeds[0].name;
  throws(() => validate(dup, BREEDS), "중복 견종이 통과됨");
});

Deno.test('금칙어 "유기견"을 막는다 (부록 A)', () => {
  const bad = structuredClone(valid);
  bad.summary = "유기견 입양을 권합니다";
  throws(() => validate(bad, BREEDS), "금칙어가 통과됨");
});

Deno.test("참여 방식·준비도 값을 검증한다", () => {
  const badP = structuredClone(valid);
  (badP.participation as { recommended: string }).recommended = "buy";
  throws(() => validate(badP, BREEDS), "정의되지 않은 참여 방식이 통과됨");

  const badR = structuredClone(valid);
  (badR.participation as { readiness: string }).readiness = "maybe";
  throws(() => validate(badR, BREEDS), "정의되지 않은 준비도가 통과됨");
});

Deno.test("이유가 비어 있으면 막는다", () => {
  const bad = structuredClone(valid);
  bad.breeds[0].reason = "   ";
  throws(() => validate(bad, BREEDS), "빈 이유가 통과됨");
});

Deno.test("프롬프트에 견종 목록과 설문이 들어간다", () => {
  const msgs = buildMessages({
    answers: { q4: "짖으면 이유를 찾아볼래요", q5: "산책 많이 다니고 싶어요" },
    followups: [{ forId: "q4", probe: "어떤 점이 걱정이세요?", answer: "이웃 항의요", skipped: false }],
  }, BREEDS);

  const user = msgs[1].content;
  ok(user.includes("비글"), "견종 목록 누락");
  ok(user.includes("짖으면 이유를"), "Q4 응답 누락");
  ok(user.includes("이웃 항의요"), "되묻기 답변 누락");
  ok(msgs[0].content.includes("보호견"), "시스템 프롬프트에 용어 규칙 누락");
});

Deno.test("건너뛴 되묻기는 프롬프트에 넣지 않는다", () => {
  const msgs = buildMessages({
    answers: { q4: "a", q5: "b" },
    followups: [{ forId: "q4", probe: "건너뛴 질문", answer: null, skipped: true }],
  }, BREEDS);
  ok(!msgs[1].content.includes("건너뛴 질문"), "건너뛴 되묻기가 포함됨");
});

Deno.test("견종 선택 시 성격 기본값을 준다 (A-10)", () => {
  const p = personalityFor("비글", BREEDS);
  ok(p.activity === 5 && p.timid === 1, "성격 프리필 값 불일치");
  throws(() => personalityFor("없는견종", BREEDS), "없는 견종에 프리필이 나옴");
});

Deno.test("고정 견종이 빠지면 막는다 (3D 에셋 렌더 보장)", () => {
  // 보더콜리가 후보에 없으면 캐릭터견을 렌더할 모델이 없다
  throws(() => validate(structuredClone(valid), BREEDS, ["보더콜리"]), "고정 견종 누락이 통과됨");

  const withPin = structuredClone(valid);
  withPin.breeds[2] = { name: "보더콜리", reason: "'산책 많이'라고 쓰신 점을 보면" };
  const a = validate(withPin, [...BREEDS, { name: "보더콜리", activity: 5, timid: 1, affection: 3 }], ["보더콜리"]);
  ok(a.breeds.some((b) => b.name === "보더콜리"), "고정 견종 포함인데 실패");
});

Deno.test("고정 견종 지시가 프롬프트에 들어간다", () => {
  const msgs = buildMessages({ answers: { q4: "a", q5: "b" }, followups: [] }, BREEDS, ["보더콜리"]);
  ok(msgs[1].content.includes("반드시 포함할 견종"), "고정 지시 누락");
  ok(msgs[1].content.includes("보더콜리"), "고정 견종명 누락");
});

Deno.test("모델이 코드펜스로 감싸도 JSON을 뽑는다", () => {
  const a = extractJson<{ a: number }>('```json\n{"a":1}\n```');
  ok(a.a === 1, "코드펜스 파싱 실패");
  const b = extractJson<{ a: number }>('설명입니다.\n{"a":2}\n감사합니다.');
  ok(b.a === 2, "앞뒤 텍스트 제거 실패");
});
