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

  let body: { sessionId?: string; moves?: Move[]; score?: number; bones?: number };
  try {
    body = await req.json();
  } catch {
    return json({ error: "잘못된 요청 본문" }, 400);
  }

  const sessionId = String(body.sessionId ?? "");
  const moves = Array.isArray(body.moves) ? body.moves : [];
  const claimed = Number(body.score ?? 0);
  if (!sessionId) return json({ error: "sessionId가 필요합니다" }, 400);

  // ---------- MG1 데모 경로 (D-023) ----------
  // 팀 MG1은 피버·특수블록 등 클라 전용 로직이라 서버 리플레이가 불가능하다.
  // bones로 제출하면 세션 상한 안에서 지급한다. 멱등(세션당 1회)은 그대로다.
  // 랭킹을 켜기 전에 반드시 리플레이 검증으로 교체할 것.
  if (body.bones != null) {
    const bones = Math.max(0, Math.floor(Number(body.bones)));
    const { data: cfg } = await db.from("config").select("value").eq("key", "economy").single();
    const cap = Number(cfg?.value?.mg1_session_bone_cap ?? 30);
    const granted = Math.min(bones, cap);

    const { data, error } = await db.rpc("game_submit", {
      p_user: auth.userId, p_session: sessionId,
      p_moves: [], p_claimed: granted, p_verified: granted, p_points: granted,
    });
    if (error) {
      if (/세션을 찾을 수 없습니다/.test(error.message)) {
        return json({ error: "세션을 찾을 수 없습니다" }, 404);
      }
      return json({ error: error.message }, 500);
    }
    return json({ ...data, mode: "capped", requested: bones, sessionCap: cap });
  }

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
