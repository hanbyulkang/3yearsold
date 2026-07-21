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
    ///
    /// 설정은 Resources/BackendConfig.asset 에 둔다 (BackendConfig.cs).
    /// </summary>
    public static class SupabaseClient
    {
        public static string AccessToken { get; private set; }
        public static bool IsSignedIn => !string.IsNullOrEmpty(AccessToken);

        static BackendConfig Config => BackendConfig.Instance;

        // ---------- 인증 ----------

        [Serializable] class SignInBody { public string email; public string password; }
        [Serializable] class SignInResp { public string access_token; public string refresh_token; }

        public static async Task<bool> SignIn(string email, string password)
        {
            var body = JsonUtility.ToJson(new SignInBody { email = email, password = password });
            var resp = await Send<SignInResp>(
                $"{Config.Url}/auth/v1/token?grant_type=password", "POST", body, useAuth: false);
            AccessToken = resp?.access_token;
            return IsSignedIn;
        }

        public static void SignOut() => AccessToken = null;

        // ---------- Edge Function ----------

        public static Task<T> Invoke<T>(string function, string jsonBody = null)
            => Send<T>($"{Config.Url}/functions/v1/{function}", "POST", jsonBody ?? "{}");

        // ---------- REST (설문 저장 등) ----------

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

        // ---------- 내부 ----------

        static UnityWebRequest Build(string url, string method, string body, bool useAuth)
        {
            var req = new UnityWebRequest(url, method)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body ?? "{}")),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 40,   // AI 분석은 5~7초 걸린다. 넉넉히 둔다 (A-08은 30초 예산).
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", Config.AnonKey);
            // 로그인 전에는 anon 키로, 로그인 후에는 사용자 토큰으로 호출한다.
            req.SetRequestHeader("Authorization",
                $"Bearer {(useAuth && IsSignedIn ? AccessToken : Config.AnonKey)}");
            return req;
        }

        static async Task<T> Send<T>(string url, string method, string body, bool useAuth = true)
        {
            var req = Build(url, method, body, useAuth);
            try
            {
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Supabase] {method} {url} 실패 ({req.responseCode}): {req.downloadHandler.text}");
                    return default;
                }
                return JsonUtility.FromJson<T>(req.downloadHandler.text);
            }
            finally { req.Dispose(); }
        }
    }
}
