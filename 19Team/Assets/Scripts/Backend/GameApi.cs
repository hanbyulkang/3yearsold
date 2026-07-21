using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Backend
{
    /// <summary>
    /// 미니게임 서버 연동 (와이어프레임 C-01 ~ C-03).
    ///
    /// MiniGame1.IRewardClient 를 직접 구현하지 않았다. 이유가 두 가지다.
    ///  1. 그 인터페이스는 동기(int GetPaws())인데 HTTP는 비동기다.
    ///     동기 시그니처를 네트워크로 채우려면 메인 스레드를 막아야 한다.
    ///  2. RewardClient.cs 가 아직 main에 없어, 참조하면 빌드가 깨진다.
    ///
    /// 연결 방법은 11-backend/docs/unity-integration.md 참고.
    /// 요약: IRewardClient 를 async 로 바꾸고 이 클래스로 위임하면 된다.
    ///
    /// 서버 권위 원칙 (PRD §5.5)
    ///  · 보드 시드는 서버가 발급한다. 클라가 고른 시드는 받지 않는다.
    ///  · 점수는 서버가 같은 시드로 다시 계산한다. 클라 주장과 다르면 지급 0.
    ///  · 따라서 클라는 이동 목록을 정직하게 보내는 것 외에 할 일이 없다.
    /// </summary>
    public static class GameApi
    {
        [Serializable]
        public class BoardSpec { public int cols; public int rows; public int colors; public int maxMoves; }

        [Serializable]
        public class StartResult
        {
            public string sessionId;
            public long seed;          // 이 시드로 보드를 만든다. 서버도 같은 시드로 재계산한다.
            public int paw;            // 차감 후 남은 발바닥
            public string nextRefillAt;
            public BoardSpec board;    // 보드 규격도 서버가 준다 — 클라에 상수를 복제하지 않는다
            public string error;
            public string code;        // NO_PAW 면 충전 시트(C-04)를 연다
        }

        [Serializable]
        public class SubmitDetail { public int moves; public int invalid; public int maxCascade; }

        [Serializable]
        public class SubmitResult
        {
            public bool accepted;      // false면 클라 점수와 서버 재계산이 달랐다는 뜻
            public int verifiedScore;  // 서버가 계산한 점수 — 화면에는 이 값을 쓴다
            public int claimedScore;
            public int pointsAwarded;  // 실제 지급된 뼈다귀 (일일 상한 반영)
            public bool duplicate;     // 이미 제출된 세션
            public SubmitDetail detail;
            public string error;
        }

        [Serializable] class SubmitBody { public string sessionId; public Move[] moves; public int score; }

        [Serializable]
        public struct Move
        {
            public int r;
            public int c;
            public string dir;   // "right" 또는 "down"
            public Move(int r, int c, bool down) { this.r = r; this.c = c; dir = down ? "down" : "right"; }
        }

        /// <summary>발바닥을 차감하고 세션을 연다. 발바닥이 없으면 code = "NO_PAW".</summary>
        public static async Task<StartResult> Start(string game = "mg1")
        {
            var r = await SupabaseClient.Invoke<StartResult>("game-start", $"{{\"game\":\"{game}\"}}");
            if (r == null) return new StartResult { error = "네트워크 오류" };
            return r;
        }

        [Serializable] class BonesBody { public string sessionId; public int bones; }

        /// <summary>
        /// MG1 데모 지급 경로 (D-023) — 획득 뼈다귀를 제출하면 서버가
        /// 세션 상한·일일 상한 안에서 지급한다. 세션당 1회 멱등.
        /// MG1은 피버·특수블록 등 클라 전용 로직이라 서버 리플레이가 불가능해서
        /// 상한 지급으로 방어한다. 랭킹을 켜기 전에 리플레이 검증으로 교체할 것.
        /// </summary>
        public static async Task<SubmitResult> SubmitBones(string sessionId, int bones)
        {
            var body = JsonUtility.ToJson(new BonesBody { sessionId = sessionId, bones = bones });
            var r = await SupabaseClient.Invoke<SubmitResult>("game-submit", body);
            if (r == null) return new SubmitResult { error = "네트워크 오류" };
            return r;
        }

        /// <summary>
        /// 플레이 결과를 제출한다. 서버가 시드로 재계산해 점수를 확정한다.
        /// 같은 세션을 다시 제출해도 중복 지급되지 않는다(멱등).
        /// </summary>
        public static async Task<SubmitResult> Submit(string sessionId, Move[] moves, int score)
        {
            var body = JsonUtility.ToJson(new SubmitBody { sessionId = sessionId, moves = moves, score = score });
            var r = await SupabaseClient.Invoke<SubmitResult>("game-submit", body);
            if (r == null) return new SubmitResult { error = "네트워크 오류" };

            if (!r.accepted && string.IsNullOrEmpty(r.error))
            {
                // 클라 점수 계산이 서버와 어긋났다는 신호다. 치팅이 아니라
                // 보드 로직이 서버(_shared/game.ts)와 달라졌을 때도 발생한다.
                Debug.LogWarning($"[GameApi] 점수 불일치 — 클라 {r.claimedScore} / 서버 {r.verifiedScore}. " +
                                 "보드 로직이 서버와 같은지 확인하세요.");
            }
            return r;
        }
    }
}
