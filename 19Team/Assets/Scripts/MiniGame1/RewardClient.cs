using System;
using UnityEngine;

namespace MiniGame1
{
    // 재화 연동 인터페이스 — 정식 구현은 Supabase Edge Function 규격 (상위 PRD §5.5).
    public interface IRewardClient
    {
        int GetPaws();
        bool TrySpendPaw();
        /// <returns>실제 지급된 뼈다귀 (일일 상한 반영)</returns>
        int GrantBones(int bones);
        int GetTotalBones();
    }

    // DEMO-MOCK: 해커톤 데모용 로컬 구현 (mini-game-1-prd.md §6.3).
    // 발바닥·뼈다귀를 PlayerPrefs에 저장한다. 서버 권위 없음 — 데모 이후 교체 대상.
    public class LocalMockRewardClient : IRewardClient
    {
        const string DailyKey = "mg1_bones_daily";
        const string DailyDateKey = "mg1_bones_daily_date";

        public int GetPaws()
        {
            return GameCurrencyStore.GetPaws();
        }

        public bool TrySpendPaw()
        {
            if (GameCurrencyStore.ConsumeEntryReservation()) return true;
            return GameCurrencyStore.TrySpendPaw();
        }

        public int GrantBones(int bones)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (PlayerPrefs.GetString(DailyDateKey, "") != today)
            {
                PlayerPrefs.SetString(DailyDateKey, today);
                PlayerPrefs.SetInt(DailyKey, 0);
            }
            int daily = PlayerPrefs.GetInt(DailyKey, 0);
            int granted = Mathf.Clamp(MG1Config.DailyBoneCap - daily, 0, bones);
            PlayerPrefs.SetInt(DailyKey, daily + granted);
            GameCurrencyStore.AddBones(granted);
            PlayerPrefs.Save();
            return granted;
        }

        public int GetTotalBones() => GameCurrencyStore.GetBones();

        // DEMO-MOCK: 테스트용 즉시 충전. 정식 구현에선 시간 회복·육포 충전(Edge Function)으로 대체.
        public void RefillPaws()
        {
            GameCurrencyStore.SetPaws(MG1Config.MaxPaws);
        }
    }
}
