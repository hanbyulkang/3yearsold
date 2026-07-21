using UnityEngine;

namespace Backend
{
    /// <summary>
    /// Supabase 접속 설정.
    ///
    /// Resources/BackendConfig.asset 이 있으면 그것을 쓰고, 없으면 아래 코드 기본값으로
    /// 동작한다 — 팀원이 에셋을 만들지 않아도 씬을 열면 바로 서버에 붙는 것이 목적이다.
    ///
    /// 여기 넣어도 되는 것: 프로젝트 URL, anon 키, 데모 계정.
    ///   anon 키는 공개 전제이며 실제 방어선은 RLS다 (0003_rls.sql).
    ///   데모 계정은 RLS로 자기 데이터만 만질 수 있는 일반 계정이다.
    /// 절대 넣으면 안 되는 것: service_role 키, DB 비밀번호, OpenAI 키.
    ///   WebGL 빌드는 문자열이 그대로 노출된다.
    /// </summary>
    [CreateAssetMenu(fileName = "BackendConfig", menuName = "D+/Backend Config")]
    public class BackendConfig : ScriptableObject
    {
        [Tooltip("https://<project-ref>.supabase.co")]
        public string Url = DefaultUrl;

        [Tooltip("anon 키 (공개 가능 — 방어는 RLS가 한다)")]
        public string AnonKey = DefaultAnonKey;

        [Header("데모 자동 로그인 (A 로그인 화면이 생기면 비우기)")]
        public string DemoEmail = DefaultDemoEmail;
        public string DemoPassword = DefaultDemoPassword;

        // ---- 코드 기본값 (프로젝트 balang) ----
        const string DefaultUrl = "https://buzeurukwscushcryksn.supabase.co";
        const string DefaultAnonKey =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
            "eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImJ1emV1cnVrd3NjdXNoY3J5a3NuIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODQ2NTE1MjAsImV4cCI6MjEwMDIyNzUyMH0." +
            "BA6mQqIxzt_HPgJskvG3OAnqMa-HMhz7Erqn1LCgqUw";
        // 데모 계정: 설문·분석·추천이 미리 시드돼 있다. RLS로 자기 데이터만 접근 가능.
        const string DefaultDemoEmail = "demo@dplus-demo.app";
        const string DefaultDemoPassword = "dplus-demo-2026!";

        static BackendConfig _instance;
        public static BackendConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<BackendConfig>("BackendConfig");
                    if (_instance == null)
                    {
                        // 에셋이 없으면 기본값으로 런타임 인스턴스를 만든다.
                        _instance = CreateInstance<BackendConfig>();
                        Debug.Log("[Backend] BackendConfig.asset 없음 — 코드 기본값(balang)으로 동작");
                    }
                }
                return _instance;
            }
        }
    }
}
