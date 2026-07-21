using System;
using System.Threading.Tasks;
using UnityEngine;
using MiniGame1;

namespace Backend
{
    /// <summary>
    /// IRewardClient의 서버 구현 (와이어프레임 C-01~C-03, PRD §5.5).
    ///
    /// 인터페이스가 동기(int GetPaws())라 네트워크를 직접 태울 수 없다.
    /// 캐시를 두고 낙관적으로 응답한 뒤, 서버 결과로 캐시를 맞추는 방식을 쓴다:
    ///
    ///   GetPaws()      → 캐시 (AppSession이 시작 시 예열)
    ///   TrySpendPaw()  → 캐시>0이면 즉시 true + 서버 game-start 비동기 (세션 확보)
    ///   GrantBones(n)  → 세션 상한으로 잘라 즉시 반환 + 서버 game-submit 비동기
    ///
    /// 서버가 거부하면(발바닥 없음·조작 판정) 캐시가 서버값으로 교정된다.
    /// 오프라인이면 조용히 로컬 추정값으로 동작한다 — 데모가 네트워크에 인질 잡히지 않게.
    ///
    /// 주의: 지급은 서버 상한(config mg1_session_bone_cap, D-023)이 최종이다.
    ///       여기 SessionCapGuess는 표시용 추정일 뿐이며 서버와 어긋나면 서버가 맞다.
    /// </summary>
    public class ServerRewardClient : IRewardClient
    {
        const int SessionCapGuess = 30;   // 서버 config와 같은 가안 — 표시용

        static int _paws = -1;            // -1 = 아직 서버값을 못 받음
        static int _bones = -1;
        static string _sessionId;
        static Task _startTask;

        [Serializable] class PawRow { public int count; }

        /// <summary>로그인 직후 호출 — 발바닥·뼈다귀 캐시 예열 (AppSession이 부른다).</summary>
        public static async void Prewarm()
        {
            try
            {
                var paw = await SupabaseClient.RpcRaw("my_paw_status");
                if (paw != null) _paws = JsonUtility.FromJson<PawRow>(paw).count;

                var bones = await SupabaseClient.RpcRaw("my_bones");
                if (bones != null && int.TryParse(bones.Trim(), out var b)) _bones = b;

                Debug.Log($"[Reward] 서버 예열 — 발바닥 {_paws} · 뼈다귀 {_bones}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Reward] 예열 실패 (오프라인 추정으로 동작): {e.Message}");
            }
        }

        public int GetPaws() => _paws >= 0 ? _paws : MG1Config.MaxPaws;

        public bool TrySpendPaw()
        {
            int paws = GetPaws();
            if (paws <= 0) return false;

            _paws = paws - 1;          // 낙관적 차감 — 서버 응답이 오면 교정된다
            _sessionId = null;
            _startTask = StartAsync();
            return true;
        }

        async Task StartAsync()
        {
            try
            {
                var r = await GameApi.Start("mg1");
                if (r.code == "NO_PAW") { _paws = 0; return; }
                if (!string.IsNullOrEmpty(r.error)) return;   // 오프라인 — 로컬 추정 유지
                _sessionId = r.sessionId;
                _paws = r.paw;                                 // 서버값으로 교정
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Reward] game-start 실패 (오프라인 진행): {e.Message}");
            }
        }

        public int GrantBones(int bones)
        {
            // 표시용 추정 — 최종 지급량은 서버(세션 상한 + 일일 상한)가 정한다 (D-023)
            int estimate = Mathf.Clamp(bones, 0, SessionCapGuess);
            if (_bones >= 0) _bones += estimate;
            SubmitAsync(bones);
            return estimate;
        }

        async void SubmitAsync(int bones)
        {
            try
            {
                if (_startTask != null) await _startTask;
                if (string.IsNullOrEmpty(_sessionId))
                {
                    Debug.LogWarning("[Reward] 세션 없음 — 이번 판 지급은 서버에 기록되지 않음 (오프라인)");
                    return;
                }

                var r = await GameApi.SubmitBones(_sessionId, bones);
                if (r != null && string.IsNullOrEmpty(r.error))
                {
                    // 서버 확정값으로 잔액 재조회 (추정치 누적 오차 제거)
                    var raw = await SupabaseClient.RpcRaw("my_bones");
                    if (raw != null && int.TryParse(raw.Trim(), out var b)) _bones = b;
                    Debug.Log($"[Reward] 서버 지급 {r.pointsAwarded} 뼈다귀 (잔액 {_bones})");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Reward] game-submit 실패: {e.Message}");
            }
        }

        public int GetTotalBones() => _bones >= 0 ? _bones : 0;
    }
}
