/**
 * Edge Function 공통 — 응답 형태와 인증 확인.
 *
 * 클라가 보낸 user_id를 절대 믿지 않는다. 토큰에서만 사용자를 얻는다.
 */
import { createClient, type SupabaseClient } from "jsr:@supabase/supabase-js@2";

export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
  });
}

export function admin(): SupabaseClient {
  return createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
  );
}

/** 토큰에서 사용자를 확인한다. 실패하면 Response를 돌려준다. */
export async function requireUser(
  req: Request,
  db: SupabaseClient,
): Promise<{ userId: string } | Response> {
  const auth = req.headers.get("Authorization") ?? "";
  if (!auth.startsWith("Bearer ")) return json({ error: "인증이 필요합니다" }, 401);

  const { data, error } = await db.auth.getUser(auth.replace("Bearer ", ""));
  if (error || !data?.user) return json({ error: "유효하지 않은 토큰" }, 401);
  return { userId: data.user.id };
}

/**
 * CRON·내부 호출용 — service_role 권한 확인.
 *
 * 문자열 비교를 쓰지 않는다. Supabase가 프로젝트마다 레거시 JWT(`eyJ…`)와
 * 신규 시크릿 키(`sb_secret_…`)를 함께 발급하고, 함수에 주입되는 값과
 * 호출자가 쓰는 값이 다를 수 있기 때문이다.
 *
 * 게이트웨이가 이미 서명을 검증한 뒤 넘겨주므로, 여기서는 role 클레임만 본다.
 */
export function requireServiceRole(req: Request): Response | null {
  const raw = req.headers.get("Authorization")?.replace(/^Bearer\s+/i, "") ??
    req.headers.get("apikey") ?? "";

  if (raw && raw === Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")) return null;

  // JWT라면 role 클레임을 확인한다
  const parts = raw.split(".");
  if (parts.length === 3) {
    try {
      const pad = parts[1].replace(/-/g, "+").replace(/_/g, "/");
      const payload = JSON.parse(atob(pad + "=".repeat((4 - pad.length % 4) % 4)));
      if (payload?.role === "service_role") return null;
    } catch { /* 형식 오류는 아래에서 거부된다 */ }
  }

  return json({ error: "service_role 권한이 필요합니다" }, 403);
}
