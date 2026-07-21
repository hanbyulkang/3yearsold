/**
 * MG1 — 3매치 서버 재계산 (와이어프레임 C-02)
 *
 * "점수·클리어 판정은 서버 검증(플레이 로그 제출 → Edge Function 재계산).
 *  클라 신뢰 금지(§5.5)"
 *
 * 이를 가능하게 하려면 게임이 **결정적**이어야 한다. 그래서
 *   · 보드 시드는 서버가 발급하고 (game/start)
 *   · 클라는 이동 목록만 제출하며 (game/submit)
 *   · 서버가 같은 시드로 처음부터 다시 돌려 점수를 확정한다.
 *
 * 클라가 점수를 조작해도 서버 재계산값과 다르면 거부된다.
 *
 * 설계 제약 (PRD §1.2 원칙 2)
 *   시간 제한 없음. 이동 수 제한만. 목표 미달도 점수 비례 보상 — "지는 판"이 없다.
 */

export const COLS = 7;
export const ROWS = 8;
export const COLORS = 5;
export const MAX_MOVES = 20;
const TILE_SCORE = 10;

export interface Move { r: number; c: number; dir: "right" | "down" }

/** mulberry32 — 짧고 결정적인 PRNG. 클라와 서버가 같은 구현을 쓴다. */
export function makeRng(seed: number) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

type Board = number[][];

function at(b: Board, r: number, c: number): number {
  return r >= 0 && r < ROWS && c >= 0 && c < COLS ? b[r][c] : -1;
}

/** 초기 보드. 시작부터 매치가 있으면 안 되므로 색을 다시 뽑는다. */
export function makeBoard(rng: () => number): Board {
  const b: Board = [];
  for (let r = 0; r < ROWS; r++) {
    b.push([]);
    for (let c = 0; c < COLS; c++) {
      let v: number;
      let guard = 0;
      do {
        v = Math.floor(rng() * COLORS);
        guard++;
      } while (
        guard < 20 &&
        ((at(b, r, c - 1) === v && at(b, r, c - 2) === v) ||
          (at(b, r - 1, c) === v && at(b, r - 2, c) === v))
      );
      b[r][c] = v;
    }
  }
  return b;
}

function findMatches(b: Board): boolean[][] {
  const hit = Array.from({ length: ROWS }, () => new Array(COLS).fill(false));

  for (let r = 0; r < ROWS; r++) {
    let run = 1;
    for (let c = 1; c <= COLS; c++) {
      if (c < COLS && b[r][c] === b[r][c - 1] && b[r][c] >= 0) run++;
      else {
        if (run >= 3) for (let k = c - run; k < c; k++) hit[r][k] = true;
        run = 1;
      }
    }
  }
  for (let c = 0; c < COLS; c++) {
    let run = 1;
    for (let r = 1; r <= ROWS; r++) {
      if (r < ROWS && b[r][c] === b[r - 1][c] && b[r][c] >= 0) run++;
      else {
        if (run >= 3) for (let k = r - run; k < r; k++) hit[k][c] = true;
        run = 1;
      }
    }
  }
  return hit;
}

function collapse(b: Board, hit: boolean[][], rng: () => number): number {
  let cleared = 0;
  for (let c = 0; c < COLS; c++) {
    const keep: number[] = [];
    for (let r = ROWS - 1; r >= 0; r--) {
      if (hit[r][c]) cleared++;
      else keep.push(b[r][c]);
    }
    for (let r = ROWS - 1, i = 0; r >= 0; r--, i++) {
      b[r][c] = i < keep.length ? keep[i] : Math.floor(rng() * COLORS);
    }
  }
  return cleared;
}

export interface GameResult {
  score: number;
  moves: number;        // 실제로 매치를 만든 이동 수
  invalid: number;      // 매치를 못 만들어 무시된 이동 수
  maxCascade: number;
}

/**
 * 시드와 이동 목록으로 점수를 재계산한다.
 * 클라가 제출한 점수는 쓰지 않는다 — 이 함수의 결과만 신뢰한다.
 */
export function replay(seed: number, moves: Move[]): GameResult {
  const rng = makeRng(seed);
  const b = makeBoard(rng);

  let score = 0, valid = 0, invalid = 0, maxCascade = 0;
  const list = moves.slice(0, MAX_MOVES);

  for (const m of list) {
    const { r, c, dir } = m;
    const r2 = dir === "down" ? r + 1 : r;
    const c2 = dir === "right" ? c + 1 : c;
    if (r < 0 || c < 0 || r2 >= ROWS || c2 >= COLS) { invalid++; continue; }

    // 교환
    [b[r][c], b[r2][c2]] = [b[r2][c2], b[r][c]];

    let hit = findMatches(b);
    if (!hit.some((row) => row.some(Boolean))) {
      // 매치가 없으면 되돌린다. 이동 수를 소모하지 않는다.
      [b[r][c], b[r2][c2]] = [b[r2][c2], b[r][c]];
      invalid++;
      continue;
    }

    valid++;
    let chain = 0;
    while (hit.some((row) => row.some(Boolean))) {
      chain++;
      const cleared = collapse(b, hit, rng);
      // 연쇄가 길수록 배수. 목표 미달이어도 점수는 쌓인다(지는 판 없음).
      score += cleared * TILE_SCORE * chain;
      hit = findMatches(b);
    }
    maxCascade = Math.max(maxCascade, chain);
  }

  return { score, moves: valid, invalid, maxCascade };
}

/** 클라 주장과 서버 재계산을 대조한다. */
export function verify(seed: number, moves: Move[], claimed: number) {
  const result = replay(seed, moves);
  return { ...result, accepted: result.score === claimed, claimed };
}

/** 점수 → 포인트. 상한은 서버 config가 최종적으로 막는다(ledger_append). */
export function pointsFor(score: number, perPoint = 20): number {
  return Math.max(0, Math.floor(score / perPoint));
}
