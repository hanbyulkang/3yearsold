/**
 * Edge Function 공통 — 응답 형태와 인증 확인.
 *
 * 클라가 보낸 user_id를 절대 믿지 않는다. 토큰에서만 사용자를 얻는다.
 */
import { createClient, type SupabaseClient } from "jsr:@supabase/supabase-js@2";

/**
 * CORS — WebGL 빌드가 다른 도메인(netlify.app 등)에서 부르기 때문에 필요하다.
 *
 * `/rest/v1`·`/auth/v1`은 Supabase가 CORS를 대신 붙여주지만
 * `/functions/v1`은 함수가 직접 붙여야 한다. 없으면 브라우저가 preflight에서
 * 막아버려 서버 코드가 실행조차 되지 않는다 (네이티브 빌드는 preflight가
 * 없어 멀쩡히 동작하므로, WebGL에서만 터지는 형태로 나타난다).
 *
 * Origin을 `*`로 여는 이유: 호출에 필요한 anon key가 어차피 클라에 박히는
 * 공개값이라 Origin을 좁혀도 실질적인 방어가 되지 않는다. 실제 방어선은
 * RLS와 토큰 검증이다.
 */
export const corsHeaders: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-client-info",
  "Access-Control-Allow-Methods": "POST, GET, OPTIONS",
  "Access-Control-Max-Age": "86400",
};

/**
 * preflight(OPTIONS)면 즉시 응답할 Response를 준다. 아니면 null.
 *
 * **인증 검사보다 먼저** 불러야 한다. preflight에는 브라우저가 Authorization을
 * 싣지 않으므로, 인증을 먼저 보면 401이 나가고 브라우저는 본 요청을 포기한다.
 */
export function preflight(req: Request): Response | null {
  if (req.method !== "OPTIONS") return null;
  return new Response(null, { status: 204, headers: corsHeaders });
}

export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { ...corsHeaders, "content-type": "application/json" },
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
