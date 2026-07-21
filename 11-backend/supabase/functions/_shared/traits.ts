/**
 * 보호견 성격 구조화 (shelter_animals.traits)
 *
 * CONT의 성격 서술을 5축으로 정리한다. 추천 매칭의 입력이 된다.
 *
 * 핵심 제약 (와이어프레임 D-03)
 *   "AI 소개문은 공고 원본 필드만 근거로 생성 — 없는 사실(건강 상태 등) 창작 금지,
 *    프롬프트에 원본 필드 화이트리스트 주입."
 *
 * 그래서 프롬프트에는 groundingFacts()가 만든 원문 발췌만 넣는다.
 * 모르는 축은 지어내지 말고 null로 두게 한다 — 그것이 이 파일의 유일한 목적이다.
 */
import type { LlmMessage } from "./llm.ts";
import { groundingFacts, findPersonality, type ShelterAnimal } from "./shelter.ts";

export interface Traits {
  people_affinity: number | null;   // 사람 친화력 1~5
  animal_affinity: number | null;   // 타 동물 친화력 1~5
  energy: number | null;            // 에너지 레벨 1~5
  likes: string[];                  // 좋아하는 것
  care_needs: string[];             // 보호자가 알아야 할 것 (건강·교육)
  one_liner: string;                // 한 문장 소개 (원문 근거)
}

const SYSTEM = `당신은 보호소 공고문을 구조화하는 도구입니다.

주어진 것은 보호소 담당자가 직접 쓴 공고 원문입니다.
이 원문에 **적혀 있는 것만** 사용해 아래 형식으로 정리하세요.

# 절대 규칙
- **원문에 없는 사실을 만들지 마세요.** 특히 건강 상태, 나이, 훈련 여부를 추측하지 마세요.
- 원문에서 판단할 수 없는 축은 반드시 null로 두세요. 중간값(3)으로 채우지 마세요.
- one_liner도 원문에 적힌 표현을 바탕으로 씁니다. 없는 성격을 붙이지 마세요.

# 척도 (1~5)
- people_affinity: 사람을 얼마나 좋아하고 잘 따르는가. 원문의 "사람 친화력: 상/중/하"가 있으면 상=5, 중=3, 하=1을 기준으로 서술을 함께 반영.
- animal_affinity: 다른 동물과 얼마나 잘 지내는가.
- energy: 활동량. "에너지 레벨"이 있으면 그것을 우선.

# 용어
- "유기견"이라고 쓰지 않습니다. 반드시 "보호견"입니다.

# 출력 (JSON만)
{
  "people_affinity": 1~5 또는 null,
  "animal_affinity": 1~5 또는 null,
  "energy": 1~5 또는 null,
  "likes": ["원문에 적힌 좋아하는 것"],
  "care_needs": ["보호자가 알아야 할 것 — 원문에 적힌 것만"],
  "one_liner": "한 문장"
}`;

export function buildTraitsMessages(animal: ShelterAnimal): LlmMessage[] {
  const facts = groundingFacts(animal);
  const personality = findPersonality(animal.sections);

  const lines = ["[공고 원문 발췌 — 이것 외의 정보는 없습니다]"];
  for (const [k, v] of Object.entries(facts)) lines.push(`${k}: ${v}`);
  if (personality) lines.push("", "[성격란 원문]", personality);

  return [
    { role: "system", content: SYSTEM },
    { role: "user", content: lines.join("\n") },
  ];
}

export class TraitsInvalid extends Error {}

function scale(v: unknown, name: string): number | null {
  if (v === null || v === undefined) return null;
  const n = Number(v);
  if (!Number.isInteger(n) || n < 1 || n > 5) {
    throw new TraitsInvalid(`${name}는 1~5 정수 또는 null이어야 합니다 (받은 값: ${v})`);
  }
  return n;
}

function stringList(v: unknown, name: string): string[] {
  if (v === null || v === undefined) return [];
  if (!Array.isArray(v)) throw new TraitsInvalid(`${name}는 배열이어야 합니다`);
  return v.map((x) => String(x).trim()).filter(Boolean).slice(0, 8);
}

export function validateTraits(raw: unknown): Traits {
  const o = raw as Record<string, unknown>;
  if (!o || typeof o !== "object") throw new TraitsInvalid("응답이 객체가 아님");

  const t: Traits = {
    people_affinity: scale(o.people_affinity, "people_affinity"),
    animal_affinity: scale(o.animal_affinity, "animal_affinity"),
    energy: scale(o.energy, "energy"),
    likes: stringList(o.likes, "likes"),
    care_needs: stringList(o.care_needs, "care_needs"),
    one_liner: String(o.one_liner ?? "").trim(),
  };

  if (!t.one_liner) throw new TraitsInvalid("one_liner 누락");
  if (/유기견/.test(JSON.stringify(t))) {
    throw new TraitsInvalid('금칙어 "유기견" 사용 — "보호견"으로만 씁니다');
  }
  return t;
}
