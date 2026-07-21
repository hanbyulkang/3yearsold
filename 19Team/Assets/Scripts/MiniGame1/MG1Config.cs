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
        public const int MaxPaws = 5;

        // ── 재화 규칙 (MG1)
        // 모은 뼈다귀 블록이 그대로 재화가 된다. 점수는 재화가 아니라 주간 랭킹용.
        public const int BonePerBlock = 1;      // 뼈다귀 블록 1개 = 뼈다귀 1
        public const int ClearMultiplier = 2;   // 목표 달성 시 ×2
        public const int BrandBoneBonus = 5;    // 브랜드 블록 1개당 +5
        public const int DailyBoneCap = 200;    // 발바닥 5회 × 40 = 하루 상한
    }
}
