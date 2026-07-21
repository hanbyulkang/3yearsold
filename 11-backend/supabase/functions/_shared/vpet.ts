/**
 * 서울 vPetInfo API 호출 (D-019)
 *
 * Edge Function 핸들러와 분리해 둔다. 핸들러 파일은 `Deno.serve()`를 최상단에서
 * 호출하므로, import 하는 것만으로 서버가 떠 버린다. 테스트에서 재사용하려면
 * 호출 로직이 부작용 없는 모듈에 있어야 한다.
 */
import type { VPetRow } from "./shelter.ts";

const ENDPOINT = "http://openapi.seoul.go.kr:8088";
const SERVICE = "vPetInfo";
const IMG_SERVICE = "vPetImg";   // 사진은 별도 서비스다 (0013)
const PAGE = 100; // 현재 전체 24건. 늘어날 여지만 둔다.

export interface VPetImgRow {
  SEQ: string | number;
  IMG_TYPE: string;   // THUMB | IMG
  IMG_NUM: string | number;
  IMG_URL: string;
}

/**
 * 보호견 사진 목록.
 *
 * vPetInfo에는 사진 필드가 없고 이 서비스가 따로 있다. SEQ가 같은 키다.
 * THUMB이 마리당 1장(목록 대표), IMG가 상세 갤러리다.
 */
export async function fetchAllImages(key: string): Promise<VPetImgRow[]> {
  return await fetchService<VPetImgRow>(key, IMG_SERVICE, 300);
}

export async function fetchAll(key: string, opts: { signal?: AbortSignal } = {}): Promise<VPetRow[]> {
  return await fetchService<VPetRow>(key, SERVICE, PAGE, opts);
}

/** 서울 열린데이터광장 공통 페이징 — 서비스명만 다르고 응답 형태가 같다. */
async function fetchService<T>(
  key: string,
  service: string,
  page: number,
  opts: { signal?: AbortSignal } = {},
): Promise<T[]> {
  const out: T[] = [];
  let start = 1;

  for (;;) {
    const url = `${ENDPOINT}/${key}/json/${service}/${start}/${start + page - 1}/`;
    const res = await fetch(url, { signal: opts.signal });
    if (!res.ok) throw new Error(`${service} HTTP ${res.status}`);

    const body = await res.json();
    const block = body[service];
    // 서울 API는 실패도 HTTP 200으로 준다. 본문의 RESULT.CODE를 봐야 안다.
    if (!block) {
      throw new Error(`${service} 응답 오류: ${JSON.stringify(body).slice(0, 200)}`);
    }
    const code = block.RESULT?.CODE;
    if (code && code !== "INFO-000") {
      throw new Error(`${service} ${code}: ${block.RESULT?.MESSAGE}`);
    }

    const rows: T[] = block.row ?? [];
    out.push(...rows);

    const total = block.list_total_count ?? out.length;
    if (out.length >= total || rows.length === 0) break;
    start += page;
  }
  return out;
}
