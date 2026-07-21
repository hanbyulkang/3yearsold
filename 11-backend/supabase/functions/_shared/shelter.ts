/**
 * 서울 vPetInfo 보호견 데이터 정규화 (D-019)
 *
 * CONT는 보호소 담당자가 손으로 쓴 HTML이다. 템플릿이 2종이고
 * 섹션 마커가 `[성격]`과 `○ 성격`으로 섞여 있어 한 가지 규칙으로는 못 자른다.
 *
 * 여기서 하는 일은 "자르기"까지다. 성격을 5축으로 해석하는 것은
 * LLM의 몫이며(traits), 이 파일은 그 입력을 만든다.
 *
 * 왜 나누는가: 섹션 분리를 LLM에 맡기면 원문에 없는 사실이 섞여 들어갈 수 있다.
 * 와이어프레임 D-03이 "공고 원본 필드만 근거, 창작 금지"를 요구하므로,
 * 원문 텍스트는 결정적으로 자르고 해석만 LLM에 넘긴다.
 */

export interface VPetRow {
  SEQ: number;
  ANIMAL_NM: string;
  ADMISSION_DT?: string;
  ANIMAL_TYPE: string;
  ANIMAL_BREED?: string;
  ANIMAL_SEX?: string;
  ANIMAL_BRITH_YMD?: string;
  WEIGHT_KG?: string;
  ADOPT_STATUS?: string;
  FOSTER_STATUS?: string;
  MOVIE_URL?: string;
  CONT?: string;
}

export interface ShelterAnimal {
  seq: number;
  name: string;
  animal_type: string;
  breed: string | null;
  sex_raw: string | null;
  birth_ymd: string | null;
  weight_kg: number | null;
  adopt_status: string | null;
  foster_ok: boolean;
  movie_url: string | null;
  content_raw: string | null;
  sections: Record<string, string>;
}

const ENTITIES: Record<string, string> = {
  "&nbsp;": " ", "&bull;": "·", "&amp;": "&", "&lt;": "<",
  "&gt;": ">", "&quot;": '"', "&#39;": "'", "&middot;": "·",
};

/** HTML을 줄바꿈이 살아있는 평문으로 바꾼다. 블록 태그는 줄바꿈으로 본다. */
export function htmlToText(html: string): string {
  return html
    .replace(/<\s*br\s*\/?>/gi, "\n")
    .replace(/<\s*\/\s*(p|div|li|tr|h[1-6])\s*>/gi, "\n")
    .replace(/<[^>]*>/g, "")
    .replace(/&#(\d+);/g, (_, d) => String.fromCharCode(Number(d)))
    .replace(/&[a-z]+;/gi, (m) => ENTITIES[m.toLowerCase()] ?? " ")
    .replace(/\r/g, "")
    .split("\n")
    .map((l) => l.replace(/[ \t ]+/g, " ").trim())
    .filter((l) => l.length > 0)
    .join("\n");
}

// 섹션 머리. 두 템플릿을 모두 잡는다: `[성격]` / `○ 성격` / `○ 성격 :`
const SECTION_RE = /^(?:\[\s*(?<b>[^\]]{2,20}?)\s*\]|[○●]\s*(?<c>[^:\n]{2,20}?)\s*(?::|$))/;

/** 섹션 제목 → 본문. 제목 표기 흔들림(공백·중점)을 정규화해 키로 쓴다. */
export function splitSections(text: string): Record<string, string> {
  const out: Record<string, string> = {};
  let current: string | null = null;
  let buf: string[] = [];

  const flush = () => {
    if (current) {
      const body = buf.join("\n").trim();
      // 같은 제목이 두 번 나오면 이어붙인다. 덮어쓰면 내용이 사라진다.
      out[current] = out[current] ? `${out[current]}\n${body}` : body;
    }
    buf = [];
  };

  for (const line of text.split("\n")) {
    const m = line.match(SECTION_RE);
    if (m) {
      flush();
      current = (m.groups?.b ?? m.groups?.c ?? "").replace(/\s+/g, " ").trim();
      const rest = line.slice(m[0].length).replace(/^\s*:?\s*/, "");
      if (rest) buf.push(rest);
    } else if (current) {
      buf.push(line);
    }
  }
  flush();
  return out;
}

// 성격 항목의 라벨. 보호소마다 표기가 조금씩 다르다.
const TRAIT_LABEL =
  /^[·\-*•]?\s*(사람\s*\(?[^)]*\)?\s*친화력|타\s*동물\s*친화력|에너지\s*레벨|좋아하는\s*것|싫어하는\s*것|좋아\(?싫어\)?하는\s*것|기타\s*특징|보호자\s*필요\s*교육|새로운\s*환경\s*적응)\s*:/;

/**
 * 성격 섹션을 찾는다.
 *
 * `[성격]` 헤더가 아예 없고 항목만 나열된 레코드가 실제로 존재한다(seq 508 '미요').
 * 그 경우 헤더에 기대지 않고 항목 라벨로 찾아낸다.
 */
export function findPersonality(sections: Record<string, string>): string | null {
  for (const [k, v] of Object.entries(sections)) {
    if (k.includes("성격")) return v;
  }
  // 폴백: 어느 섹션에든 성격 항목이 나열돼 있으면 그 지점부터 섹션 끝까지를 성격으로 본다.
  for (const body of Object.values(sections)) {
    const lines = body.split("\n");
    const start = lines.findIndex((l) => TRAIT_LABEL.test(l));
    if (start >= 0) {
      const block = lines.slice(start).join("\n").trim();
      if (block) return block;
    }
  }
  return null;
}

/** `· 사람 친화력 : 상, 사람 손길을 좋아해요` → { label, value } */
export function parseBullets(body: string): Array<{ label: string; value: string }> {
  const out: Array<{ label: string; value: string }> = [];
  for (const raw of body.split("\n")) {
    const line = raw.replace(/^[·\-*•]\s*/, "").trim();
    if (line === raw.trim() && !/^[·\-*•]/.test(raw)) {
      // 불릿이 아닌 줄은 항목으로 보지 않는다 (설명 문단)
      if (!/^[^:]{2,20}\s*:/.test(line)) continue;
    }
    const m = line.match(/^([^:]{2,20}?)\s*:\s*(.+)$/);
    if (m) out.push({ label: m[1].trim(), value: m[2].trim() });
  }
  return out;
}

function toNumber(v?: string): number | null {
  if (!v) return null;
  const n = Number(String(v).replace(/[^0-9.]/g, ""));
  return Number.isFinite(n) ? n : null;
}

function toDate(v?: string): string | null {
  if (!v) return null;
  const m = String(v).match(/(\d{4})-?(\d{2})-?(\d{2})/);
  return m ? `${m[1]}-${m[2]}-${m[3]}` : null;
}

/**
 * API 원본 행 → DB 저장 형태.
 *
 * 성별 코드 주의: vPetInfo는 암컷이 'W'다. 국가 API의 'F'와 다르다.
 * 여기서 변환하지 않고 원본을 그대로 넘긴다 — DB의 생성 컬럼이 매핑한다.
 * (변환 지점을 하나로 두어야 소스가 늘어날 때 뒤집히지 않는다)
 */
export function normalize(row: VPetRow): ShelterAnimal {
  const text = row.CONT ? htmlToText(row.CONT) : "";
  return {
    seq: Number(row.SEQ),
    name: row.ANIMAL_NM,
    animal_type: row.ANIMAL_TYPE,
    breed: row.ANIMAL_BREED ?? null,
    sex_raw: row.ANIMAL_SEX ?? null,
    birth_ymd: toDate(row.ANIMAL_BRITH_YMD),
    weight_kg: toNumber(row.WEIGHT_KG),
    adopt_status: row.ADOPT_STATUS ?? null,
    // §4.4 참여 퍼널의 임보 단계와 연결되는 유일한 필드 (D-019)
    foster_ok: (row.FOSTER_STATUS ?? "").includes("가능"),
    movie_url: row.MOVIE_URL || null,
    content_raw: row.CONT ?? null,
    sections: splitSections(text),
  };
}

/**
 * AI 소개문 프롬프트에 넣을 근거 화이트리스트 (와이어프레임 D-03).
 * 여기 없는 사실은 프롬프트에 들어가지 않으므로 창작할 재료 자체가 없다.
 */
export function groundingFacts(a: ShelterAnimal): Record<string, string> {
  const facts: Record<string, string> = {
    이름: a.name,
    종류: a.animal_type === "DOG" ? "개" : "고양이",
  };
  if (a.breed) facts["품종"] = a.breed;
  if (a.sex_raw) facts["성별"] = a.sex_raw === "M" ? "수컷" : a.sex_raw === "W" ? "암컷" : "미상";
  if (a.birth_ymd) facts["출생"] = a.birth_ymd;
  if (a.weight_kg != null) facts["체중"] = `${a.weight_kg}kg`;
  for (const [k, v] of Object.entries(a.sections)) {
    if (/입양신청|신청 방법|링크/.test(k)) continue; // 안내문은 소개문 근거가 아니다
    facts[k] = v;
  }
  return facts;
}
