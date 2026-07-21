using UnityEngine;

namespace Backend
{
    /// <summary>
    /// Supabase 접속 설정.
    ///
    /// Assets/Resources/BackendConfig.asset 으로 만들어 값을 채운다.
    /// (메뉴: Create ▸ D+ ▸ Backend Config)
    ///
    /// 여기에 넣어도 되는 것: 프로젝트 URL, anon 키.
    ///   anon 키는 공개 전제이며 RLS가 실제 방어선이다 (0003_rls.sql).
    /// 넣으면 안 되는 것: service_role 키, DB 비밀번호, OpenAI 키.
    ///   WebGL 빌드는 문자열이 그대로 노출된다.
    /// </summary>
    [CreateAssetMenu(fileName = "BackendConfig", menuName = "D+/Backend Config")]
    public class BackendConfig : ScriptableObject
    {
        [Tooltip("https://<project-ref>.supabase.co")]
        public string Url;

        [Tooltip("anon 키 (공개 가능 — 방어는 RLS가 한다)")]
        public string AnonKey;

        static BackendConfig _instance;
        public static BackendConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<BackendConfig>("BackendConfig");
                    if (_instance == null)
                        Debug.LogError("[Backend] Resources/BackendConfig.asset 이 없습니다. " +
                                       "Create ▸ D+ ▸ Backend Config 로 만들어 주세요.");
                }
                return _instance;
            }
        }
    }
}
