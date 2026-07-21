using System;
using UnityEngine;

namespace MiniGame1
{
    // 재화 연동 인터페이스 — 정식 구현은 Supabase Edge Function 규격 (상위 PRD §5.5).
    public interface IRewardClient
    {
        int GetPaws();
        bool TrySpendPaw();
        /// <returns>실제 지급된 포인트 (일일 상한 반영)</returns>
        int GrantPointsForScore(int score);
        void SaveCoupon(string brandName);
        int GetTotalPoints();
    }

    // DEMO-MOCK: 해커톤 데모용 로컬 구현 (mini-game-1-prd.md §6.3).
    // 발바닥·포인트를 PlayerPrefs에 저장한다. 서버 권위 없음 — 데모 이후 교체 대상.
    public class LocalMockRewardClient : IRewardClient
    {
        const string PawKey = "mg1_paws";
        const string PointKey = "mg1_points_total";
        const string DailyKey = "mg1_points_daily";
        const string DailyDateKey = "mg1_points_daily_date";
        const string CouponKey = "mg1_coupons";

        public int GetPaws()
        {
            if (!PlayerPrefs.HasKey(PawKey)) PlayerPrefs.SetInt(PawKey, MG1Config.MaxPaws);
            return PlayerPrefs.GetInt(PawKey);
        }

        public bool TrySpendPaw()
        {
            int paws = GetPaws();
            if (paws <= 0) return false;
            PlayerPrefs.SetInt(PawKey, paws - 1); // DEMO-MOCK: 시간 회복 없음
            PlayerPrefs.Save();
            return true;
        }

        public int GrantPointsForScore(int score)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (PlayerPrefs.GetString(DailyDateKey, "") != today)
            {
                PlayerPrefs.SetString(DailyDateKey, today);
                PlayerPrefs.SetInt(DailyKey, 0);
            }
            int daily = PlayerPrefs.GetInt(DailyKey, 0);
            int raw = score / MG1Config.PointsDivisor;
            int granted = Mathf.Clamp(MG1Config.DailyPointCap - daily, 0, raw);
            PlayerPrefs.SetInt(DailyKey, daily + granted);
            PlayerPrefs.SetInt(PointKey, PlayerPrefs.GetInt(PointKey, 0) + granted); // DEMO-MOCK: origin=play 태깅은 서버 몫
            PlayerPrefs.Save();
            return granted;
        }

        public void SaveCoupon(string brandName)
        {
            PlayerPrefs.SetInt(CouponKey, PlayerPrefs.GetInt(CouponKey, 0) + 1); // DEMO-MOCK
            PlayerPrefs.Save();
        }

        public int GetTotalPoints() => PlayerPrefs.GetInt(PointKey, 0);

        // DEMO-MOCK: 테스트용 즉시 충전. 정식 구현에선 시간 회복·육포 충전(Edge Function)으로 대체.
        public void RefillPaws()
        {
            PlayerPrefs.SetInt(PawKey, MG1Config.MaxPaws);
            PlayerPrefs.Save();
        }
    }
}
