using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Backend
{
    /// <summary>
    /// MG2(2048) 재화 연동.
    ///
    /// MG1과 같은 규칙이다 — 판을 시작할 때 발바닥을 쓰고, 끝날 때 뼈다귀를 받는다.
    /// 다만 MG1은 IRewardClient 인터페이스를 구현해야 했고, 2048은 그런 인터페이스가
    /// 없어 이 정적 브릿지가 GameApi를 직접 부른다.
    ///
    /// 지급은 서버 세션 상한 방식이다 (D-023) — 클라 점수를 서버가 재현할 수 없으므로
    /// 조작해도 이득이 세션당 상한으로 캡된다.
    ///
    /// 오프라인이면 조용히 로컬로 동작한다. 데모가 네트워크에 인질 잡히지 않게.
    /// </summary>
    public static class Game2048Bridge
    {
        /// <summary>2048 점수 → 뼈다귀. 512 타일 하나가 대략 1개.</summary>
        const int ScorePerBone = 512;
        const int LocalPawGuess = 5;

        static string _sessionId;
        static Task _startTask;
        static int _paws = -1;      // -1 = 서버값 미수신
        static bool _submitted;

        public static int Paws => _paws >= 0 ? _paws : LocalPawGuess;

        /// <summary>판을 시작한다. 발바닥이 없으면 false — 화면에서 안내해야 한다.</summary>
        public static bool BeginRound()
        {
            if (Paws <= 0) return false;

            _paws = Paws - 1;      // 낙관적 차감. 서버 응답이 오면 교정된다
            _sessionId = null;
            _submitted = false;
            _startTask = StartAsync();
            return true;
        }

        static async Task StartAsync()
        {
            try
            {
                var r = await GameApi.Start("mg2");
                if (r.code == "NO_PAW") { _paws = 0; return; }
                if (!string.IsNullOrEmpty(r.error)) return;   // 오프라인 — 로컬 추정 유지
                _sessionId = r.sessionId;
                _paws = r.paw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MG2] game-start 실패 (오프라인 진행): {e.Message}");
            }
        }

        /// <summary>
        /// 판이 끝났을 때 호출한다. 한 판에 한 번만 지급된다
        /// (클리어 후 계속 두다가 게임오버가 나도 중복 지급되지 않게).
        /// </summary>
        public static void EndRound(int score, Action<int> onGranted = null)
        {
            if (_submitted) return;
            _submitted = true;

            int bones = Mathf.Max(0, score / ScorePerBone);
            if (bones <= 0) { onGranted?.Invoke(0); return; }

            SubmitAsync(bones, onGranted);
        }

        static async void SubmitAsync(int bones, Action<int> onGranted)
        {
            try
            {
                if (_startTask != null) await _startTask;
                if (string.IsNullOrEmpty(_sessionId))
                {
                    Debug.LogWarning("[MG2] 세션 없음 — 이번 판은 서버에 기록되지 않음 (오프라인)");
                    onGranted?.Invoke(0);
                    return;
                }

                var r = await GameApi.SubmitBones(_sessionId, bones);
                int granted = (r != null && string.IsNullOrEmpty(r.error)) ? r.pointsAwarded : 0;
                Debug.Log($"[MG2] 서버 지급 {granted} 뼈다귀 (요청 {bones})");
                onGranted?.Invoke(granted);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MG2] game-submit 실패: {e.Message}");
                onGranted?.Invoke(0);
            }
        }

        /// <summary>발바닥 잔량을 서버에서 다시 읽는다 (시간 회복 반영).</summary>
        public static async void Refresh(Action onDone = null)
        {
            try
            {
                await AppSession.EnsureSignedIn();
                var raw = await SupabaseClient.RpcRaw("my_paw_status");
                if (raw != null) _paws = JsonUtility.FromJson<PawRow>(raw).count;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MG2] 발바닥 조회 실패: {e.Message}");
            }
            onDone?.Invoke();
        }

        [Serializable] class PawRow { public int count; }
    }
}
