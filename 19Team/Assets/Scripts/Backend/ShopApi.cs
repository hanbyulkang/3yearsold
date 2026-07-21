using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Backend
{
    /// <summary>
    /// 상점 (F-01 · F-02).
    ///
    /// 가격을 클라가 보내지 않는다. sku만 보내고 서버 카탈로그가 가격을 정한다 —
    /// 클라가 가격을 보내면 0원 구매를 만들 수 있다.
    ///
    /// 잔액도 서버 값만 쓴다 (PRD §5.5 — 재화 계산은 클라에서 하지 않는다).
    /// </summary>
    public static class ShopApi
    {
        [Serializable]
        public class Skin
        {
            public string sku;
            public string title;
            public string kind;          // skin | set | coupon
            public int jerky_price;      // 0이면 육포 상품이 아님
            public int krw_price;        // 0이면 실물 결제 상품이 아님
            public string description;
            public int sort_order;

            public bool IsJerky => jerky_price > 0;
            /// <summary>카드에 표시할 가격 문자열.</summary>
            public string PriceLabel => IsJerky ? $"육포 {jerky_price}" : $"{krw_price:N0}원";
            public string KindLabel => kind == "set" ? "세트" : kind == "coupon" ? "쿠폰" : "스킨";
        }

        [Serializable] class SkinList { public Skin[] items; }
        [Serializable] class BuyBody { public string p_sku; }

        [Serializable]
        public class BuyResult
        {
            public string sku;
            public int spent;
            public int jerkyLeft;
            public bool alreadyOwned;
            public string message;       // 실패 시 서버 오류 메시지
            public bool Ok => string.IsNullOrEmpty(message);
        }

        /// <summary>판매 중인 상품 목록. 실패하면 빈 배열 — 화면은 목업으로 폴백한다.</summary>
        public static async Task<Skin[]> GetCatalog()
        {
            var raw = await SupabaseClient.GetRaw(
                "skins?active=eq.true&order=sort_order&select=sku,title,kind,jerky_price,krw_price,description,sort_order");
            if (string.IsNullOrEmpty(raw)) return Array.Empty<Skin>();
            try { return JsonUtility.FromJson<SkinList>("{\"items\":" + raw + "}").items ?? Array.Empty<Skin>(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Shop] 카탈로그 파싱 실패: {e.Message}");
                return Array.Empty<Skin>();
            }
        }

        /// <summary>육포 잔액. 조회 실패 시 -1 (화면에서 "—"로 표시).</summary>
        public static async Task<int> GetJerky()
        {
            var raw = await SupabaseClient.RpcRaw("my_jerky");
            return int.TryParse((raw ?? "").Trim(), out var v) ? v : -1;
        }

        /// <summary>보유 스킨 sku 목록.</summary>
        public static async Task<string[]> GetOwned()
        {
            var raw = await SupabaseClient.GetRaw("skins_owned?select=sku");
            if (string.IsNullOrEmpty(raw)) return Array.Empty<string>();
            try
            {
                var list = JsonUtility.FromJson<SkinList>("{\"items\":" + raw + "}").items ?? Array.Empty<Skin>();
                var skus = new string[list.Length];
                for (int i = 0; i < list.Length; i++) skus[i] = list[i].sku;
                return skus;
            }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>육포로 구매. 잔액 부족·실물 상품이면 서버가 거부한다.</summary>
        public static async Task<BuyResult> BuyWithJerky(string sku)
        {
            var raw = await SupabaseClient.RpcRaw("buy_skin_with_jerky",
                JsonUtility.ToJson(new BuyBody { p_sku = sku }));
            if (string.IsNullOrEmpty(raw)) return new BuyResult { message = "네트워크 오류" };
            try { return JsonUtility.FromJson<BuyResult>(raw); }
            catch { return new BuyResult { message = raw }; }
        }

        // ---------- F-05 육포 충전 ----------

        [Serializable]
        public class JerkyPack
        {
            public string sku;
            public int jerky;
            public int krw;
            public string bonus_note;
            public bool best;
            public int sort_order;

            /// <summary>카드 두 번째 줄. 보너스가 없으면 강조 문구로 대신한다.</summary>
            public string SubLabel =>
                !string.IsNullOrEmpty(bonus_note) ? bonus_note
                : best ? "가장 많이 고르는 구성" : "기본 구성";
        }

        [Serializable] class PackList { public JerkyPack[] items; }
        [Serializable] class TopupBody { public string p_sku; }

        /// <summary>이번 달 결제 한도. 서버가 강제하는 값이며 클라는 표시만 한다.</summary>
        [Serializable]
        public class PaymentLimit
        {
            public int cap;
            public int spent;
            public int remaining;
            public int percent;
        }

        [Serializable]
        public class TopupResult
        {
            public bool ok;
            public string sku;
            public int jerky;
            public int krw;
            public bool mock;
            public string message;       // 실패 시 서버 오류 (한도 초과 등)
            public bool Ok => ok && string.IsNullOrEmpty(message);
        }

        /// <summary>충전 패키지 카탈로그. 가격은 서버 값만 쓴다.</summary>
        public static async Task<JerkyPack[]> GetPacks()
        {
            var raw = await SupabaseClient.GetRaw(
                "jerky_packs?active=eq.true&order=sort_order&select=sku,jerky,krw,bonus_note,best,sort_order");
            if (string.IsNullOrEmpty(raw)) return Array.Empty<JerkyPack>();
            try { return JsonUtility.FromJson<PackList>("{\"items\":" + raw + "}").items ?? Array.Empty<JerkyPack>(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Shop] 충전 상품 파싱 실패: {e.Message}");
                return Array.Empty<JerkyPack>();
            }
        }

        /// <summary>결제 한도 조회. 실패하면 null — 화면은 "불러오는 중"으로 둔다.</summary>
        public static async Task<PaymentLimit> GetLimit()
        {
            var raw = await SupabaseClient.RpcRaw("my_payment_limit");
            if (string.IsNullOrEmpty(raw) || raw == "null") return null;
            try { return JsonUtility.FromJson<PaymentLimit>(raw); }
            catch { return null; }
        }

        /// <summary>
        /// 충전 (모의 결제). 가격은 보내지 않는다 — sku만 보내고 서버 카탈로그가 정한다.
        /// 한도 초과는 서버가 거부하고 그 메시지가 그대로 올라온다.
        /// </summary>
        public static async Task<TopupResult> PurchaseJerky(string sku)
        {
            var raw = await SupabaseClient.RpcRaw("purchase_jerky",
                JsonUtility.ToJson(new TopupBody { p_sku = sku }));
            if (string.IsNullOrEmpty(raw)) return new TopupResult { message = "네트워크 오류" };
            try
            {
                var r = JsonUtility.FromJson<TopupResult>(raw);
                if (!r.ok && string.IsNullOrEmpty(r.message)) r.message = "충전에 실패했어요";
                return r;
            }
            catch { return new TopupResult { message = raw }; }
        }
    }
}
