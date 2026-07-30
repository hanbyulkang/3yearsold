using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Backend
{
    /// <summary>
    /// Supabase 호출 공통 래퍼.
    ///
    /// 규칙
    ///  · service_role 키를 클라이언트에 두지 않는다. anon 키와 사용자 토큰만 쓴다.
    ///    WebGL은 빌드 결과에서 문자열이 그대로 보인다.
    ///  · 재화 계산을 클라에서 하지 않는다. 서버 응답값을 그대로 표시한다 (PRD §5.5).
    /// </summary>
    public static class SupabaseClient
    {
        public static string AccessToken { get; private set; }
        public static string UserId { get; private set; }
        public static bool IsSignedIn => !string.IsNullOrEmpty(AccessToken);

        static BackendConfig Config => BackendConfig.Instance;

        // ---------- 인증 ----------

        [Serializable] class SignInBody { public string email; public string password; }
        [Serializable] class SignInUser { public string id; }
        [Serializable] class SignInResp { public string access_token; public SignInUser user; }

        public static async Task<bool> SignIn(string email, string password)
        {
            var body = JsonUtility.ToJson(new SignInBody { email = email, password = password });
            var resp = await Send<SignInResp>(
                $"{Config.Url}/auth/v1/token?grant_type=password", "POST", body, useAuth: false);
            AccessToken = resp?.access_token;
            UserId = resp?.user?.id;
            return IsSignedIn;
        }

        public static void SignOut() { AccessToken = null; UserId = null; }

        // ---------- Edge Function ----------

        public static Task<T> Invoke<T>(string function, string jsonBody = null)
            => Send<T>($"{Config.Url}/functions/v1/{function}", "POST", jsonBody ?? "{}");

        // ---------- REST ----------

        /// <summary>설문은 문항 단위로 즉시 저장한다 — 이탈 후 이어하기 (와이어프레임 A-03).</summary>
        public static async Task<bool> UpsertSurveyAnswer(string userId, string questionId, string valueJson)
        {
            var body = $"[{{\"user_id\":\"{userId}\",\"question_id\":\"{questionId}\",\"value\":{valueJson}}}]";
            var req = Build($"{Config.Url}/rest/v1/survey_responses?on_conflict=user_id,question_id",
                            "POST", body, useAuth: true);
            req.SetRequestHeader("Prefer", "resolution=merge-duplicates");
            try { await req.SendWebRequest(); return req.result == UnityWebRequest.Result.Success; }
            finally { req.Dispose(); }
        }

        /// <summary>REST GET — 응답 JSON 원문을 돌려준다. 실패 시 null.</summary>
        public static async Task<string> GetRaw(string pathAndQuery)
        {
            var req = Build($"{Config.Url}/rest/v1/{pathAndQuery}", "GET", null, useAuth: true);
            try
            {
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Supabase] GET {pathAndQuery} 실패 ({req.responseCode})");
                    return null;
                }
                return req.downloadHandler.text;
            }
            finally { req.Dispose(); }
        }

        /// <summary>RPC 호출 — 스칼라·단일행 응답이 있어 원문을 돌려준다. 실패 시 null.</summary>
        public static async Task<string> RpcRaw(string fn, string jsonBody = "{}")
        {
            var req = Build($"{Config.Url}/rest/v1/rpc/{fn}", "POST", jsonBody, useAuth: true);
            try
            {
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Supabase] rpc/{fn} 실패 ({req.responseCode}): {req.downloadHandler.text}");
                    return null;
                }
                return req.downloadHandler.text;
            }
            finally { req.Dispose(); }
        }

        // ---------- 내부 ----------

        static UnityWebRequest Build(string url, string method, string body, bool useAuth)
        {
            var req = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 40,   // AI 분석은 5~7초 걸린다. 넉넉히 둔다 (A-08은 30초 예산).
            };
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.SetRequestHeader("apikey", Config.AnonKey);
            // 로그인 전에는 anon 키로, 로그인 후에는 사용자 토큰으로 호출한다.
            req.SetRequestHeader("Authorization",
                $"Bearer {(useAuth && IsSignedIn ? AccessToken : Config.AnonKey)}");
            return req;
        }

        static async Task<T> Send<T>(string url, string method, string body, bool useAuth = true)
        {
            var req = Build(url, method, body ?? "{}", useAuth);
            try
            {
                await req.SendWebRequest();
                string responseText = req.downloadHandler?.text ?? string.Empty;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Supabase] {method} {url} 실패 ({req.responseCode}, {req.result}): {req.error}\n{responseText}");
                    return default;
                }
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    Debug.LogError($"[Supabase] {method} {url} HTTP {req.responseCode}지만 응답 본문이 비었습니다.");
                    return default;
                }
                try
                {
                    T parsed = JsonUtility.FromJson<T>(responseText);
                    if (parsed == null)
                        Debug.LogError($"[Supabase] {method} {url} HTTP {req.responseCode} JSON 파싱 결과가 null입니다: {responseText}");
                    else
                        Debug.Log($"[Supabase] {method} {url} 완료 ({req.responseCode}, {responseText.Length} bytes)");
                    return parsed;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Supabase] {method} {url} HTTP {req.responseCode} JSON 파싱 실패: {e.Message}\n{responseText}");
                    return default;
                }
            }
            finally { req.Dispose(); }
        }
    }
}
