using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Backend
{
    /// <summary>
    /// 앱 세션 — 데모 자동 로그인.
    ///
    /// A-01 로그인 화면이 아직 없으므로, 앱이 뜨면 데모 계정으로 조용히 로그인한다.
    /// 데모 계정에는 설문·AI 분석·보호견 추천이 미리 시드돼 있어
    /// D 추천 씬이 로그인 직후 바로 실데이터를 받는다.
    ///
    /// A 온보딩 씬이 생기면: BackendConfig의 DemoEmail을 비우고
    /// A-01에서 SupabaseClient.SignIn(...)을 직접 호출하면 된다. 이 클래스는 그대로 둔다.
    /// </summary>
    public static class AppSession
    {
        static Task<bool> _signIn;

        /// <summary>여러 곳에서 불러도 로그인은 한 번만 일어난다.</summary>
        public static Task<bool> EnsureSignedIn()
        {
            if (_signIn == null) _signIn = SignInInternal();
            return _signIn;
        }

        static async Task<bool> SignInInternal()
        {
            if (SupabaseClient.IsSignedIn) return true;

            var c = BackendConfig.Instance;
            if (string.IsNullOrEmpty(c.DemoEmail))
            {
                Debug.Log("[AppSession] 데모 계정 미설정 — 로그인 화면(A-01)에서 처리");
                return false;
            }

            try
            {
                bool ok = await SupabaseClient.SignIn(c.DemoEmail, c.DemoPassword);
                Debug.Log(ok
                    ? $"[AppSession] 데모 로그인 완료 ({c.DemoEmail})"
                    : "[AppSession] 데모 로그인 실패 — 오프라인 목업으로 동작");
                if (ok) ServerRewardClient.Prewarm();   // 발바닥·뼈다귀 캐시 예열
                return ok;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AppSession] 로그인 예외 — 오프라인 목업으로 동작: {e.Message}");
                return false;
            }
        }
    }
}
