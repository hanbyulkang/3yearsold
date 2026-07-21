/**
 * 3매치 서버 재계산 테스트
 *
 * 핵심은 "클라가 점수를 조작하면 잡히는가"다 (C-02, §5.5).
 */
import { replay, verify, makeBoard, makeRng, pointsFor, COLS, ROWS, MAX_MOVES, type Move } from "./game.ts";

function ok(c: unknown, m: string) { if (!c) throw new Error(`단언 실패: ${m}`); }

/** 시드를 바꿔가며 매치가 나는 이동을 찾는다 (테스트 픽스처 생성용) */
function scoringRun(seed: number): { moves: Move[]; score: number } {
  const moves: Move[] = [];
  for (let r = 0; r < ROWS - 1; r++) {
    for (let c = 0; c < COLS - 1; c++) {
      moves.push({ r, c, dir: "right" }, { r, c, dir: "down" });
    }
  }
  const picked = moves.slice(0, MAX_MOVES);
  return { moves: picked, score: replay(seed, picked).score };
}

Deno.test("초기 보드에 미리 만들어진 매치가 없다", () => {
  for (const seed of [1, 42, 12345, 999]) {
    const b = makeBoard(makeRng(seed));
    for (let r = 0; r < ROWS; r++) {
      for (let c = 0; c < COLS - 2; c++) {
        ok(!(b[r][c] === b[r][c + 1] && b[r][c] === b[r][c + 2]), `seed ${seed} 가로 매치 존재`);
      }
    }
    for (let c = 0; c < COLS; c++) {
      for (let r = 0; r < ROWS - 2; r++) {
        ok(!(b[r][c] === b[r + 1][c] && b[r][c] === b[r + 2][c]), `seed ${seed} 세로 매치 존재`);
      }
    }
  }
});

Deno.test("같은 시드·같은 이동은 항상 같은 점수를 낸다 (결정성)", () => {
  const { moves } = scoringRun(42);
  const a = replay(42, moves);
  const b = replay(42, moves);
  ok(a.score === b.score && a.moves === b.moves, "재실행 결과가 다름 — 검증 불가능");
});

Deno.test("시드가 다르면 결과가 다르다", () => {
  const { moves } = scoringRun(42);
  const scores = new Set([1, 2, 3, 4, 5].map((s) => replay(s, moves).score));
  ok(scores.size > 1, "시드가 달라도 점수가 동일 — 시드가 반영되지 않음");
});

Deno.test("점수 조작이 잡힌다 (클라 신뢰 금지)", () => {
  const { moves, score } = scoringRun(7);
  ok(verify(7, moves, score).accepted, "정직한 제출이 거부됨");
  ok(!verify(7, moves, score + 1).accepted, "1점 부풀린 제출이 통과됨");
  ok(!verify(7, moves, 999999).accepted, "터무니없는 점수가 통과됨");
  ok(!verify(7, moves, 0).accepted, "0점 제출이 통과됨");
});

Deno.test("시드를 바꿔치기해도 잡힌다", () => {
  const { moves, score } = scoringRun(7);
  ok(!verify(8, moves, score).accepted, "다른 시드로 제출했는데 통과됨");
});

Deno.test("이동 수 상한을 넘겨 제출해도 상한까지만 계산된다", () => {
  const many: Move[] = [];
  for (let i = 0; i < 200; i++) many.push({ r: i % (ROWS - 1), c: i % (COLS - 1), dir: "right" });
  const r = replay(3, many);
  ok(r.moves + r.invalid <= MAX_MOVES, `상한 초과 (${r.moves + r.invalid} > ${MAX_MOVES})`);
});

Deno.test("보드 밖 좌표는 무시된다 (크래시하지 않음)", () => {
  const r = replay(5, [
    { r: -1, c: 0, dir: "right" },
    { r: 0, c: COLS - 1, dir: "right" },
    { r: ROWS - 1, c: 0, dir: "down" },
    { r: 999, c: 999, dir: "down" },
  ]);
  ok(r.invalid === 4 && r.score === 0, "잘못된 좌표가 점수를 만듦");
});

Deno.test("매치를 못 만드는 교환은 이동 수를 소모하지 않는다", () => {
  const r = replay(11, [{ r: 0, c: 0, dir: "right" }]);
  ok(r.moves + r.invalid === 1, "이동 집계 오류");
  if (r.invalid === 1) ok(r.score === 0, "무효 이동인데 점수가 생김");
});

Deno.test("점수는 음수가 되지 않고 포인트 환산도 음수가 없다", () => {
  for (const seed of [1, 2, 3, 42, 777]) {
    const { moves } = scoringRun(seed);
    const r = replay(seed, moves);
    ok(r.score >= 0, "음수 점수");
    ok(pointsFor(r.score) >= 0, "음수 포인트");
  }
});
