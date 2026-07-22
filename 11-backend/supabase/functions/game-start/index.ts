/**
 * 미니게임 시작 (와이어프레임 C-01)
 *
 * 발바닥을 차감하고 **서버가 보드 시드를 발급**한다.
 * 클라가 시드를 정하면 유리한 판을 골라 만들 수 있으므로 받지 않는다.
 *
 * 응답의 seed로 클라가 보드를 그리고, 같은 시드로 서버가 나중에 재계산한다.
 * 클라와 서버가 _shared/game.ts의 같은 PRNG를 써야 한다.
 */
import { json, admin, requireUser, preflight } from "../_shared/http.ts";
import { COLS, ROWS, COLORS, MAX_MOVES } from "../_shared/game.ts";

Deno.serve(async (req) => {
  const pre = preflight(req);
  if (pre) return pre;

  const db = admin();
  const auth = await requireUser(req, db);
  if (auth instanceof Response) return auth;

  const body = await req.json().catch(() => ({}));
  const game = typeof body.game === "string" ? body.game : "mg1";

  const { data, error } = await db.rpc("game_start", { p_user: auth.userId, p_game: game });
  if (error) {
    // 발바닥 부족은 정상 흐름이다. 클라는 충전 시트(C-04)를 연다.
    if (/발바닥/.test(error.message)) {
      return json({ error: "발바닥이 부족합니다", code: "NO_PAW" }, 409);
    }
    return json({ error: error.message }, 500);
  }

  // 보드 규격도 함께 내려보낸다. 클라에 상수를 복제하면 서버와 어긋난다(A-05·B-04).
  return json({ ...data, board: { cols: COLS, rows: ROWS, colors: COLORS, maxMoves: MAX_MOVES } });
});
