using System;

namespace MiniGame1
{
    // 순수 C# 점수·콤보·피버 로직 (mini-game-1-prd.md §2.4) — UnityEngine 비의존.
    public class ScoreSystem
    {
        public int Score { get; private set; }
        public float FeverGauge { get; private set; }
        public bool FeverActive { get; private set; }
        public float FeverTimeLeft { get; private set; }

        public event Action FeverStarted;
        public event Action FeverEnded;

        // cascadeIndex: 0 = 첫 매치, 1부터 연쇄. 배수 ×(1 + 0.5n), 피버 중 ×2.
        public int AddCascadeStep(int matchedBlocks, int specialBlocks, int brandBlocks, int brandBonus, int cascadeIndex)
        {
            float mult = 1f + MG1Config.ComboStepMultiplier * cascadeIndex;
            if (FeverActive) mult *= 2f;
            int pts = (int)Math.Round((matchedBlocks * MG1Config.ScorePerMatchedBlock
                                     + specialBlocks * MG1Config.ScorePerSpecialCleared) * mult)
                      + brandBlocks * brandBonus;
            Score += pts;

            if (!FeverActive)
            {
                FeverGauge = Math.Min(MG1Config.FeverMax,
                    FeverGauge + (matchedBlocks + specialBlocks) * MG1Config.FeverGainPerBlock);
                if (FeverGauge >= MG1Config.FeverMax)
                {
                    FeverActive = true;
                    FeverTimeLeft = MG1Config.FeverDuration;
                    FeverStarted?.Invoke();
                }
            }
            return pts;
        }

        public void Tick(float dt)
        {
            if (FeverActive)
            {
                FeverTimeLeft -= dt;
                FeverGauge = MG1Config.FeverMax * Math.Max(0f, FeverTimeLeft) / MG1Config.FeverDuration;
                if (FeverTimeLeft <= 0f)
                {
                    FeverActive = false;
                    FeverGauge = 0f;
                    FeverEnded?.Invoke();
                }
            }
            else if (FeverGauge > 0f)
            {
                FeverGauge = Math.Max(0f, FeverGauge - MG1Config.FeverDecayPerSec * dt);
            }
        }

        public void Reset()
        {
            Score = 0; FeverGauge = 0f; FeverActive = false; FeverTimeLeft = 0f;
        }
    }
}
