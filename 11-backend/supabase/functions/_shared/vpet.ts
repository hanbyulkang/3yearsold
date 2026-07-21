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
const PAGE = 100; // 현재 전체 24건. 늘어날 여지만 둔다.

export async function fetchAll(key: string, opts: { signal?: AbortSignal } = {}): Promise<VPetRow[]> {
  const out: VPetRow[] = [];
  let start = 1;

  for (;;) {
    const url = `${ENDPOINT}/${key}/json/${SERVICE}/${start}/${start + PAGE - 1}/`;
    const res = await fetch(url, { signal: opts.signal });
    if (!res.ok) throw new Error(`vPetInfo HTTP ${res.status}`);

    const body = await res.json();
    const block = body[SERVICE];
    // 서울 API는 실패도 HTTP 200으로 준다. 본문의 RESULT.CODE를 봐야 안다.
    if (!block) {
      throw new Error(`vPetInfo 응답 오류: ${JSON.stringify(body).slice(0, 200)}`);
    }
    const code = block.RESULT?.CODE;
    if (code && code !== "INFO-000") {
      throw new Error(`vPetInfo ${code}: ${block.RESULT?.MESSAGE}`);
    }

    const rows: VPetRow[] = block.row ?? [];
    out.push(...rows);

    const total = block.list_total_count ?? out.length;
    if (out.length >= total || rows.length === 0) break;
    start += PAGE;
  }
  return out;
}
