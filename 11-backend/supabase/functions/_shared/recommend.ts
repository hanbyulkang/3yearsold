/**
 * 보호견 추천 (와이어프레임 D-01 · D-02 · D-03)
 *
 * "같은 보호견이라도 사용자마다 다른 추천 이유를 받는다" (PRD §4.3)
 * 그래서 이유는 저장해 두고 재사용하는 게 아니라 사용자별로 생성한다.
 *
 * 후보를 좁히는 것(입양문의가능·임보가능)은 SQL이 하고,
 * 고르고 이유를 쓰는 것만 LLM이 한다. 목록 전체를 LLM에 넘기지 않는다.
 */
import type { LlmMessage } from "./llm.ts";
import type { Analysis, Participation } from "./analysis.ts";
import type { Traits } from "./traits.ts";

export interface Candidate {
  seq: number;
  name: string;
  animal_type: string;
  breed: string | null;
  sex: string;
  weight_kg: number | null;
  foster_ok: boolean;
  care_nm?: string | null;
  traits: Traits | null;
}

export interface Recommendation {
  seq: number;
  reason: string;
}

const SYSTEM = `당신은 보호견 참여 플랫폼의 추천 담당자입니다.

사용자의 상황 분석과 지금 보호 중인 보호견 목록이 주어집니다.
사용자에게 잘 맞는 보호견 3마리를 고르고, 각각 왜 맞는지 쓰세요.

# 이유 작성 규칙 (가장 중요)
- **사용자가 설문에 쓴 문장을 인용**하세요. "당신이 쓰신 '...'처럼" 형태.
- 보호견 쪽 근거는 **주어진 정보에 있는 것만** 씁니다. 성격·건강을 지어내지 마세요.
- 두 개를 연결하세요. "당신은 A라고 하셨고, 이 아이는 B입니다" 구조.
- 같은 보호견이라도 사람마다 다른 이유가 나와야 합니다. 일반론을 쓰지 마세요.

# 고르는 기준
- 사용자의 여건(시간·주거·지출)과 보호견의 활동량·돌봄 필요를 함께 봅니다.
- 여건이 빠듯한 사용자에게 손이 많이 가는 아이를 권하지 않습니다.
- 참여 방식이 foster(임시보호)라면 임시보호 가능한 아이를 우선합니다.

# 톤
- 겁주거나 재촉하지 않습니다. 입양을 압박하지 마세요.
- 보호견을 불쌍하게 그리지 않습니다. 지금 어떤 아이인지를 씁니다.

# 용어 (위반 시 실패)
- "유기견"이라고 쓰지 않습니다. 반드시 "보호견"입니다.

# 출력 (JSON만)
{"picks": [{"seq": 숫자, "reason": "이유"}]}  — 정확히 3개, seq는 주어진 목록에서만`;

function describe(c: Candidate): string {
  const bits = [`#${c.seq} ${c.name}`, c.animal_type === "DOG" ? "개" : "고양이"];
  if (c.breed) bits.push(c.breed);
  bits.push(c.sex === "female" ? "암컷" : c.sex === "male" ? "수컷" : "성별미상");
  if (c.weight_kg != null) bits.push(`${c.weight_kg}kg`);
  if (c.foster_ok) bits.push("임시보호가능");

  const t = c.traits;
  if (t) {
    const sc: string[] = [];
    if (t.people_affinity != null) sc.push(`사람친화 ${t.people_affinity}/5`);
    if (t.animal_affinity != null) sc.push(`동물친화 ${t.animal_affinity}/5`);
    if (t.energy != null) sc.push(`활동량 ${t.energy}/5`);
    if (sc.length) bits.push(sc.join(" "));
    if (t.likes.length) bits.push(`좋아함: ${t.likes.join(", ")}`);
    if (t.care_needs.length) bits.push(`돌봄 필요: ${t.care_needs.join(", ")}`);
    if (t.one_liner) bits.push(`"${t.one_liner}"`);
  }
  return bits.join(" · ");
}

export function buildRecommendMessages(
  analysis: Analysis,
  userQuotes: string[],
  candidates: Candidate[],
): LlmMessage[] {
  const lines = [
    "[사용자 상황]",
    analysis.summary,
    "",
    "[사용자가 직접 쓴 문장]",
    ...userQuotes.filter(Boolean).map((q) => `- ${q}`),
    "",
    `[지금 권하는 참여 방식] ${analysis.participation.recommended} (${analysis.participation.readiness})`,
    "",
    "[보호 중인 아이들]",
    ...candidates.map(describe),
  ];
  return [
    { role: "system", content: SYSTEM },
    { role: "user", content: lines.join("\n") },
  ];
}

export class RecommendInvalid extends Error {}

export function validateRecommendations(raw: unknown, candidates: Candidate[]): Recommendation[] {
  const allowed = new Set(candidates.map((c) => c.seq));
  const picks = (raw as { picks?: unknown })?.picks;

  if (!Array.isArray(picks)) throw new RecommendInvalid("picks 배열 누락");
  if (picks.length !== 3) throw new RecommendInvalid(`정확히 3개여야 함 (실제 ${picks.length})`);

  const seen = new Set<number>();
  const out: Recommendation[] = [];
  for (const p of picks) {
    const seq = Number((p as { seq?: unknown })?.seq);
    const reason = String((p as { reason?: unknown })?.reason ?? "").trim();
    if (!allowed.has(seq)) throw new RecommendInvalid(`목록에 없는 보호견: ${seq}`);
    if (seen.has(seq)) throw new RecommendInvalid(`중복 추천: ${seq}`);
    if (!reason) throw new RecommendInvalid(`#${seq} 이유 누락`);
    seen.add(seq);
    out.push({ seq, reason });
  }

  if (/유기견/.test(JSON.stringify(out))) {
    throw new RecommendInvalid('금칙어 "유기견" 사용 — "보호견"으로만 씁니다');
  }
  return out;
}

/** 참여 방식에 따라 후보를 좁힌다. LLM에 넘기기 전에 SQL 수준에서 거른다. */
export function candidateFilter(p: Participation): { fosterOnly: boolean } {
  return { fosterOnly: p === "foster" };
}
