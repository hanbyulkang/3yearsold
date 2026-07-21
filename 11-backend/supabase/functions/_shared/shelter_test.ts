/**
 * 보호견 파서 테스트 — 실제 vPetInfo 24건으로 검증한다.
 * 합성 데이터로는 손으로 쓴 HTML의 흔들림을 재현할 수 없다.
 *
 *   deno test --allow-read supabase/functions/_shared/
 */
import {
  htmlToText, splitSections, findPersonality, parseBullets, normalize, groundingFacts,
  type VPetRow,
} from "./shelter.ts";

// 외부 의존 없이 쓰는 최소 단언 (네트워크 없이 실행되도록)
function ok(cond: unknown, msg: string) {
  if (!cond) throw new Error(`단언 실패: ${msg}`);
}
function eq<T>(a: T, b: T, msg: string) {
  if (a !== b) throw new Error(`단언 실패: ${msg} (기대 ${b}, 실제 ${a})`);
}

const FIXTURE = new URL(
  "../../../../05-data/sample-seoul-shelter-pets.json",
  import.meta.url,
);
const rows: VPetRow[] = JSON.parse(Deno.readTextFileSync(FIXTURE));

Deno.test("픽스처가 실제 24건이다", () => {
  eq(rows.length, 24, "레코드 수");
});

Deno.test("HTML 태그·엔티티가 평문으로 정리된다", () => {
  const t = htmlToText("<p><strong>가</strong></p>\r\n<p>나&bull;다&amp;라</p>");
  ok(!t.includes("<"), "태그 잔존");
  ok(!t.includes("&bull;"), "엔티티 잔존");
  ok(t.includes("가") && t.includes("나·다&라"), "본문 유실");
});

Deno.test("두 템플릿의 섹션 마커를 모두 인식한다", () => {
  const bracket = splitSections("[성격]\n· 사람 친화력 : 상");
  ok("성격" in bracket, "[성격] 형식 미인식");

  const circle = splitSections("○ 성격\n- 사람 친화력 : 상, 잘 따라요");
  ok("성격" in circle, "○ 성격 형식 미인식");
});

Deno.test("24건 전부에서 성격 섹션을 찾는다", () => {
  const missing: string[] = [];
  for (const r of rows) {
    const a = normalize(r);
    if (!findPersonality(a.sections)) missing.push(`${a.seq}:${a.name}`);
  }
  ok(missing.length === 0, `성격 섹션 누락: ${missing.join(", ")}`);
});

Deno.test("성격 항목이 label:value로 분해된다", () => {
  let withBullets = 0;
  for (const r of rows) {
    const body = findPersonality(normalize(r).sections);
    if (body && parseBullets(body).length > 0) withBullets++;
  }
  // 전부 불릿 형식은 아니다(서술형도 있음). 과반이면 구조화 가치가 있다.
  ok(withBullets >= rows.length / 2, `불릿 분해된 레코드 ${withBullets}/${rows.length}`);
});

Deno.test("성별 코드는 변환하지 않고 원본을 보존한다 (W=암컷)", () => {
  for (const r of rows) {
    eq(normalize(r).sex_raw, r.ANIMAL_SEX ?? null, `seq ${r.SEQ} 성별 원본 훼손`);
  }
  // vPetInfo에 'F'가 없어야 한다 — 있다면 국가 API 매핑 가정이 깨진 것
  ok(!rows.some((r) => r.ANIMAL_SEX === "F"), "vPetInfo에 F 코드가 존재 — 매핑 재확인 필요");
});

Deno.test("임시보호 가능 여부가 판정된다 (§4.4 임보 단계)", () => {
  const fosterable = rows.filter((r) => normalize(r).foster_ok);
  ok(fosterable.length > 0, "임보 가능 개체가 하나도 없음");
  for (const r of rows) {
    eq(
      normalize(r).foster_ok,
      (r.FOSTER_STATUS ?? "").includes("가능"),
      `seq ${r.SEQ} 임보 판정 불일치`,
    );
  }
});

Deno.test("숫자·날짜 필드가 파싱된다", () => {
  for (const r of rows) {
    const a = normalize(r);
    if (r.WEIGHT_KG) ok(typeof a.weight_kg === "number", `seq ${a.seq} 체중 파싱 실패`);
    if (r.ANIMAL_BRITH_YMD) {
      ok(/^\d{4}-\d{2}-\d{2}$/.test(a.birth_ymd ?? ""), `seq ${a.seq} 생년월일 형식`);
    }
  }
});

Deno.test("근거 화이트리스트에 입양신청 안내가 섞이지 않는다 (D-03 창작 금지)", () => {
  for (const r of rows) {
    const facts = groundingFacts(normalize(r));
    for (const k of Object.keys(facts)) {
      ok(!/입양신청/.test(k), `seq ${r.SEQ}: 안내문이 근거에 포함됨 (${k})`);
    }
    ok("이름" in facts && "종류" in facts, `seq ${r.SEQ}: 기본 사실 누락`);
  }
});

Deno.test("근거는 원문에서만 나온다 — 원문에 없는 문자열은 생성되지 않는다", () => {
  for (const r of rows) {
    const a = normalize(r);
    const source = htmlToText(r.CONT ?? "");
    for (const [k, v] of Object.entries(a.sections)) {
      // 섹션 본문의 첫 줄이 원문에 실제로 존재해야 한다
      const head = v.split("\n")[0]?.slice(0, 20);
      if (head && head.length > 5) {
        ok(source.includes(head), `seq ${a.seq} 섹션 '${k}'의 내용이 원문에 없음`);
      }
    }
  }
});
