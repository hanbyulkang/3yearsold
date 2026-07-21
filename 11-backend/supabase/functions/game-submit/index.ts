/**
 * 미니게임 결과 제출 (와이어프레임 C-02 · C-03)
 *
 * "점수·클리어 판정은 서버 검증(플레이 로그 제출 → Edge Function 재계산).
 *  클라 신뢰 금지(§5.5)"
 *
 * 클라가 보낸 점수는 참고값일 뿐이다. 세션의 시드로 처음부터 다시 돌려
 * 서버가 계산한 값만 신뢰하고, 두 값이 다르면 지급하지 않는다.
 */
import { json, admin, requireUser } from "../_shared/http.ts";
import { replay, pointsFor, type Move } from "../_shared/game.ts";

Deno.serve(async (req) => {
  const db = admin();
  const auth = await requireUser(req, db);
  if (auth instanceof Response) return auth;

  let body: { sessionId?: string; moves?: Move[]; score?: number };
  try {
    body = await req.json();
  } catch {
    return json({ error: "잘못된 요청 본문" }, 400);
  }

  const sessionId = String(body.sessionId ?? "");
  const moves = Array.isArray(body.moves) ? body.moves : [];
  const claimed = Number(body.score ?? 0);
  if (!sessionId) return json({ error: "sessionId가 필요합니다" }, 400);

  // 시드는 서버에만 있다. 클라가 보낸 시드를 쓰지 않는다.
  const { data: session, error: sErr } = await db.from("game_sessions")
    .select("id, seed, submitted_at").eq("id", sessionId).eq("user_id", auth.userId)
    .maybeSingle();
  if (sErr) return json({ error: sErr.message }, 500);
  if (!session) return json({ error: "세션을 찾을 수 없습니다" }, 404);
  if (session.seed == null) return json({ error: "세션에 시드가 없습니다" }, 500);

  // 여기가 핵심 — 같은 시드로 처음부터 다시 돌린다.
  const result = replay(Number(session.seed), moves);
  const points = result.score === claimed ? pointsFor(result.score) : 0;

  const { data, error } = await db.rpc("game_submit", {
    p_user: auth.userId, p_session: sessionId,
    p_moves: moves, p_claimed: claimed,
    p_verified: result.score, p_points: points,
  });
  if (error) return json({ error: error.message }, 500);

  return json({
    ...data,
    // 조작 여부를 클라에 숨기지 않는다. 디버깅과 신뢰 양쪽에 필요하다.
    detail: { moves: result.moves, invalid: result.invalid, maxCascade: result.maxCascade },
  });
});
