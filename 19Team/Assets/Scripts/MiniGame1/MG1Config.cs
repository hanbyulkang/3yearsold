namespace MiniGame1
{
    // mini-game-1-prd.md §2 코어 룰 · §9 확정 수치. 밸런스 변경은 PRD §9 표와 함께 갱신한다.
    public static class MG1Config
    {
        public const int BoardSize = 7;
        public const int NormalTypes = 6;

        public const float PlayTime = 60f;
        public const float DemoPlayTime = 30f;      // 데모 모드 (PRD §7-6)

        public const int ScorePerMatchedBlock = 10;
        public const int ScorePerSpecialCleared = 15;
        public const float ComboStepMultiplier = 0.5f;

        public const float FeverMax = 100f;
        public const float FeverGainPerBlock = 8f;
        public const float FeverDecayPerSec = 5f;
        public const float FeverDuration = 10f;

        public const float HintDelaySec = 4f;
        public const int PointsDivisor = 100;       // 포인트 = floor(점수 / 100)
        public const int DailyPointCap = 500;       // MG1 일일 상한 (가안)
        public const int MaxPaws = 5;
    }
}
