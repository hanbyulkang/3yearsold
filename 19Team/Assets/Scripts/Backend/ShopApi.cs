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
    }
}
