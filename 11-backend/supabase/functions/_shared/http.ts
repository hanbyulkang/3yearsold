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

/** CRON·내부 호출용. service_role 키를 헤더로 확인한다. */
export function requireServiceRole(req: Request): Response | null {
  const key = req.headers.get("apikey") ?? req.headers.get("Authorization")?.replace("Bearer ", "");
  if (!key || key !== Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")) {
    return json({ error: "service_role 권한이 필요합니다" }, 403);
  }
  return null;
}
