using System;
using System.Threading.Tasks;
using UnityEngine;
using Donation;

namespace Backend
{
    /// <summary>
    /// E 후원 데이터 로더 — DonData의 목업을 서버 실데이터로 덮는다.
    ///
    /// RecApi와 같은 규칙:
    ///  · 잔액·진행률·기여는 전부 서버 값이다 (§5.5). 클라가 계산하지 않는다.
    ///  · 실패하면 목업이 그대로 보인다 — 데모가 네트워크에 인질 잡히지 않게.
    ///  · 미달 캠페인도 그대로 표시한다 (§6.5). 모의 라벨(MockMode)은 유지한다.
    ///
    /// 기부·배분 액션은 DonActions로 분리했다 (이건 화면 로드용).
    /// </summary>
    public static class DonApi
    {
        [Serializable] class BonesRow { public int count; }
        [Serializable] class Progress { public int units; public int goal; public int percent; public int participants; }

        [Serializable]
        class Campaign
        {
            public string id;
            public string title;
            public string goal_note;
            public int goal_units;
            public string status;         // active | fulfilled | closed_short
            public string executed_note;
            public string receipt_caption;
        }

        [Serializable]
        class Target
        {
            public string id;
            public string name;
            public string region;
            public string note;
        }

        [Serializable] class CampaignList { public Campaign[] items; }
        [Serializable] class TargetList { public Target[] items; }

        public static async Task LoadIntoDonData()
        {
            if (!await AppSession.EnsureSignedIn()) throw new Exception("로그인 실패");

            // 잔액 (뼈다귀)
            var bonesRaw = await SupabaseClient.RpcRaw("my_bones");
            if (int.TryParse((bonesRaw ?? "").Trim(), out var bones)) DonData.Bones = bones;

            // 활성 캠페인 → E-01 공동 창고
            var campRaw = await SupabaseClient.GetRaw(
                "warehouse_campaigns?order=created_at&select=id,title,goal_note,goal_units,status,executed_note,receipt_caption");
            Campaign[] camps = ParseCampaigns(campRaw);

            var active = Array.Find(camps, c => c.status == "active");
            if (active != null)
            {
                DonData.WarehouseGoal = active.goal_note;
                DonActions.ActiveCampaignId = active.id;

                var prog = await ProgressOf(active.id);
                if (prog != null)
                {
                    DonData.WarehousePercent = prog.percent;
                    DonData.MyContribution = await MyContribution();
                }
            }

            // 완료·미달 캠페인 → E-03 집행 내역 (§6.5 — 미달도 공개)
            var reports = new System.Collections.Generic.List<DonData.Report>();
            foreach (var c in camps)
            {
                if (c.status == "active") continue;
                reports.Add(new DonData.Report
                {
                    Title = c.title,
                    Status = c.status == "fulfilled" ? "집행 완료" : "목표 미달 종료",
                    Ok = c.status == "fulfilled",
                    HasCertificate = c.status == "fulfilled",
                    Body = c.executed_note ?? "",
                    PhotoCaption = c.receipt_caption,
                });
            }
            if (reports.Count > 0) DonData.Reports = reports.ToArray();

            // 지정 후원 대상 → E-02
            var tgtRaw = await SupabaseClient.GetRaw(
                "donation_targets?active=eq.true&select=id,name,region,note");
            Target[] tgts = ParseTargets(tgtRaw);
            if (tgts.Length > 0)
            {
                var arr = new DonData.Target[tgts.Length];
                var ids = new string[tgts.Length];
                for (int i = 0; i < tgts.Length; i++)
                {
                    arr[i] = new DonData.Target { Name = tgts[i].name, Sub = tgts[i].note ?? "", Tag = tgts[i].region ?? "" };
                    ids[i] = tgts[i].id;
                }
                DonData.Targets = arr;
                DonActions.TargetIds = ids;
            }

            Debug.Log($"[DonApi] 후원 데이터 적용 — 잔액 {DonData.Bones} · 진행률 {DonData.WarehousePercent}% · 대상 {DonData.Targets.Length}");
        }

        static async Task<Progress> ProgressOf(string campaignId)
        {
            var raw = await SupabaseClient.RpcRaw("campaign_progress", $"{{\"p_campaign\":\"{campaignId}\"}}");
            if (string.IsNullOrEmpty(raw) || raw == "null") return null;
            try { return JsonUtility.FromJson<Progress>(raw); } catch { return null; }
        }

        static async Task<int> MyContribution()
        {
            var raw = await SupabaseClient.RpcRaw("my_warehouse_contribution");
            return int.TryParse((raw ?? "").Trim(), out var v) ? v : 0;
        }

        static Campaign[] ParseCampaigns(string json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<Campaign>();
            try { return JsonUtility.FromJson<CampaignList>("{\"items\":" + json + "}").items ?? Array.Empty<Campaign>(); }
            catch { return Array.Empty<Campaign>(); }
        }

        static Target[] ParseTargets(string json)
        {
            if (string.IsNullOrEmpty(json)) return Array.Empty<Target>();
            try { return JsonUtility.FromJson<TargetList>("{\"items\":" + json + "}").items ?? Array.Empty<Target>(); }
            catch { return Array.Empty<Target>(); }
        }
    }

    /// <summary>후원 액션 — 기부·배분. 잔액 부족은 서버가 거부한다.</summary>
    public static class DonActions
    {
        /// <summary>DonApi가 채운다. 어느 캠페인에 기부할지.</summary>
        public static string ActiveCampaignId = "jul-food-200kg";
        /// <summary>DonData.Targets와 같은 순서의 서버 id 배열.</summary>
        public static string[] TargetIds = Array.Empty<string>();

        [Serializable] class DonateBody { public string p_campaign; public int p_amount; }
        [Serializable] class AllocBody { public string p_target; public int p_amount; }
        [Serializable] public class Result { public bool ok; public string message; public bool Ok => ok || string.IsNullOrEmpty(message); }

        /// <summary>공동 창고 기부. 성공하면 갱신된 진행률까지 서버가 돌려준다.</summary>
        public static async Task<bool> DonateToWarehouse(int amount)
        {
            var raw = await SupabaseClient.RpcRaw("donate_to_warehouse",
                JsonUtility.ToJson(new DonateBody { p_campaign = ActiveCampaignId, p_amount = amount }));
            return Succeeded(raw, amount);
        }

        /// <summary>지정 후원 배분. index는 DonData.Targets 순서.</summary>
        public static async Task<bool> AllocateToTarget(int index, int amount)
        {
            if (index < 0 || index >= TargetIds.Length) { Debug.LogWarning("[Don] 대상 인덱스 범위 밖"); return false; }
            var raw = await SupabaseClient.RpcRaw("allocate_to_target",
                JsonUtility.ToJson(new AllocBody { p_target = TargetIds[index], p_amount = amount }));
            return Succeeded(raw, amount);
        }

        static bool Succeeded(string raw, int amount)
        {
            if (string.IsNullOrEmpty(raw)) { Debug.LogWarning("[Don] 네트워크 오류"); return false; }
            if (raw.Contains("\"message\"") && raw.Contains("잔액"))
            {
                Debug.LogWarning($"[Don] 뼈다귀가 부족합니다 (요청 {amount})");
                return false;
            }
            return raw.Contains("\"ok\"");
        }
    }
}
