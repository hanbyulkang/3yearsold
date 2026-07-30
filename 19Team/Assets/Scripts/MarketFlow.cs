using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Runtime commerce flow based on the supplied market.dc.html and svg-shop assets.
/// The four F-commerce screens live in one scene and are switched without scene loads.
/// </summary>
public sealed class MarketFlow : MonoBehaviour
{
    public TMP_FontAsset marketFont;
    public Sprite productCardSprite;
    public Sprite productCardDonateSprite;
    public Sprite previewFrameSprite;
    public Sprite gaugeCardSprite;
    public Sprite donateBannerSprite;
    public Sprite qrSlotSprite;
    public Sprite statusPillGoldSprite;
    public Sprite statusPillWaitSprite;
    public Sprite stepCardActiveSprite;
    public Sprite stepCardDoneSprite;
    public Sprite stepCardWaitingSprite;
    public Sprite boneGoldSprite;
    public Sprite jerkySprite;

    [System.Serializable]
    public class ItemArt
    {
        public string sku;
        public Sprite sprite;
    }

    [Header("상품 이미지 (sku ↔ 아트)")]
    [Tooltip("서버 카탈로그의 sku와 짝지어 카드·미리보기에 쓴다. 없으면 카드가 비어 보인다.")]
    public ItemArt[] itemArt = System.Array.Empty<ItemArt>();

    [Header("F-05 육포 충전")]
    public Sprite packCardSprite;
    public Sprite packCardBestSprite;
    public Sprite priceButtonSprite;
    public Sprite limitCardSprite;
    public Sprite limitGaugeTrackSprite;
    public Sprite limitGaugeFillSprite;
    public Sprite jerkyTileSprite;

    // ---------- 서버 연동 상태 ----------
    // 가격·잔액을 클라가 계산하지 않는다. 전부 서버 값이다 (PRD §5.5).
    private Backend.ShopApi.Skin[] _catalog = System.Array.Empty<Backend.ShopApi.Skin>();
    private string[] _owned = System.Array.Empty<string>();
    private Backend.ShopApi.Skin _selected;
    private int _jerky = -1;      // -1 = 아직 조회 전 → "—" 로 표시
    private int _bones = -1;
    private TMP_Text _jerkyLabel;
    private TMP_Text _boneLabel;
    private string _notice;       // 구매 결과 안내 (실패 사유 포함)

    /// <summary>서버에서 카탈로그·잔액·보유목록을 읽고 화면을 다시 그린다.</summary>
    private async System.Threading.Tasks.Task LoadFromServer()
    {
        if (!await Backend.AppSession.EnsureSignedIn())
        {
            Debug.LogWarning("[Market] 로그인 실패 — 목업 카탈로그로 표시합니다");
            return;
        }
        _catalog = await Backend.ShopApi.GetCatalog();
        _owned   = await Backend.ShopApi.GetOwned();
        _jerky   = await Backend.ShopApi.GetJerky();
        _bones   = await BonesAsync();
        if (_jerky >= 0) GameCurrencyStore.SetJerky(_jerky);
        if (_bones >= 0) GameCurrencyStore.SetBones(_bones);
    }

    private static async System.Threading.Tasks.Task<int> BonesAsync()
    {
        var raw = await Backend.SupabaseClient.RpcRaw("my_bones");
        return int.TryParse((raw ?? "").Trim(), out var v) ? v : -1;
    }

    /// <summary>육포로 구매. 가격은 보내지 않는다 — sku만 보내고 서버가 정한다.</summary>
    private async void Purchase()
    {
        if (_selected == null) { ShowSkinPurchased(); return; }   // 오프라인 목업 경로

        var r = await Backend.ShopApi.BuyWithJerky(_selected.sku);
        if (!r.Ok)
        {
            _notice = r.message;
            Debug.LogWarning($"[Market] 구매 실패: {r.message}");
            Refresh();
            return;
        }
        if (r.alreadyOwned) { _notice = "이미 보유한 스킨이에요"; Refresh(); return; }

        _jerky = r.jerkyLeft;
        GameCurrencyStore.SetJerky(_jerky);
        _owned = await Backend.ShopApi.GetOwned();
        _notice = null;
        ShowSkinPurchased();
    }

    /// <summary>sku에 짝지어진 상품 아트. 없으면 null — 카드는 그대로 그려진다.</summary>
    private Sprite ArtFor(string sku)
    {
        if (string.IsNullOrEmpty(sku) || itemArt == null) return null;
        foreach (ItemArt entry in itemArt)
            if (entry != null && entry.sku == sku) return entry.sprite;
        return null;
    }

    private void ShowSkinFor(Backend.ShopApi.Skin item)
    {
        _selected = item;
        ShowSkin();
    }

    private const float ReferenceWidth = 686f;
    private const float ReferenceHeight = 1220f;

    private static readonly Color Ink = new Color32(90, 70, 50, 255);
    private static readonly Color Muted = new Color32(138, 122, 98, 255);
    private static readonly Color Cream = new Color32(255, 249, 236, 255);
    private static readonly Color White = new Color32(255, 255, 255, 255);
    private static readonly Color Brown = new Color32(63, 47, 34, 255);
    private static readonly Color BrownDark = new Color32(36, 26, 17, 255);
    private static readonly Color Gold = new Color32(242, 193, 78, 255);
    private static readonly Color GoldDark = new Color32(164, 105, 15, 255);
    private static readonly Color Green = new Color32(97, 165, 50, 255);
    private static readonly Color Border = new Color32(237, 228, 210, 255);
    private static readonly Color SoftGold = new Color32(255, 244, 202, 255);

    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform screenRoot;
    private ScreenId currentScreen;
    private bool initialized;

    private enum ScreenId
    {
        Shop,
        Skin,
        Set,
        Checkout,
        Topup     // F-05 육포 충전
    }

    private async void Start()
    {
        BuildInterface();
        canvas.enabled = false;
        await LoadFromServer();
        ShowShop();
        canvas.enabled = true;
    }

    /// <summary>현재 화면을 서버 값으로 다시 그린다. 잔액 라벨도 갱신된다.</summary>
    private void Refresh()
    {
        if (!initialized) return;
        ShowScreen(currentScreen);
        if (!string.IsNullOrEmpty(_notice))
        {
            Debug.Log($"[Market] {_notice}");
            _notice = null;
        }
    }

    public void BuildInterface()
    {
        if (initialized) return;
        initialized = true;
        Application.targetFrameRate = 60;

        EnsureEventSystem();
        GameObject canvasObject = new GameObject("Market Flow Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvasRect = canvasObject.GetComponent<RectTransform>();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.35f;

        if (marketFont == null)
            marketFont = TMP_Settings.defaultFontAsset;
    }

    public void ShowShop()
    {
        ShowScreen(ScreenId.Shop);
    }

    public void ShowSkin()
    {
        ShowScreen(ScreenId.Skin);
    }

    public void ShowSet()
    {
        ShowScreen(ScreenId.Set);
    }

    public void ShowCheckout()
    {
        ShowScreen(ScreenId.Checkout);
    }

    public void ShowTopup()
    {
        ShowScreen(ScreenId.Topup);
        LoadTopup();
    }

    public string CurrentScreenName()
    {
        return currentScreen.ToString();
    }

    private void ShowScreen(ScreenId screen)
    {
        BuildInterface();
        currentScreen = screen;
        if (screenRoot != null) Destroy(screenRoot.gameObject);

        screenRoot = new GameObject("F-0" + ((int)screen + 1) + " Screen", typeof(RectTransform)).GetComponent<RectTransform>();
        screenRoot.SetParent(canvasRect, false);
        screenRoot.anchorMin = new Vector2(0.5f, 0.5f);
        screenRoot.anchorMax = new Vector2(0.5f, 0.5f);
        screenRoot.pivot = new Vector2(0.5f, 0.5f);
        screenRoot.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
        screenRoot.anchoredPosition = Vector2.zero;


        CreatePanel("Background", screenRoot, 0f, 0f, ReferenceWidth, ReferenceHeight, new Color32(255, 249, 236, 255));
        if (screen == ScreenId.Shop) BuildShop();
        else if (screen == ScreenId.Skin) BuildSkin();
        else if (screen == ScreenId.Set) BuildSet();
        else if (screen == ScreenId.Topup) BuildTopup();
        else BuildCheckout();
    }

    private void BuildShop()
    {
        CreateHeader(screenRoot, "상점", false);
        CreateButton(screenRoot, "홈", 16f, 42f, 43f, 58f, 58f, SoftGold, BrownDark, AppSceneFlow.GoHome);
        CreateTabs(screenRoot, 132f);
        CreateText(screenRoot, "확정 구매 · 단추의 새 옷과 보호소 연동", 16f, Muted, TextAnchor.MiddleCenter, 24f, 188f, 638f, 28f);

        var shown = new System.Collections.Generic.List<Backend.ShopApi.Skin>();
        if (_catalog != null)
            foreach (var c in _catalog)
                if (c.kind == _tab) shown.Add(c);

        if (shown.Count > 0)
        {
            // 서버 카탈로그 — 2열 그리드. 가격·제목은 전부 서버 값이다.
            for (int i = 0; i < shown.Count && i < 4; i++)
            {
                var item = shown[i];
                float x = (i % 2 == 0) ? 24f : 355f;
                float y = (i < 2) ? 216f : 526f;
                bool owned = System.Array.IndexOf(_owned, item.sku) >= 0;
                string price = owned ? "보유 중" : item.PriceLabel;

                if (item.IsJerky)
                {
                    var captured = item;
                    CreateProductCard(screenRoot, x, y, item.KindLabel, item.title, price, false,
                        owned ? (UnityEngine.Events.UnityAction)null : delegate { ShowSkinFor(captured); },
                        ArtFor(item.sku));
                }
                else
                {
                    // 실물 결제 상품은 자사몰 플로우로 (F-03) — 서버 구매 대상이 아니다
                    CreateProductCard(screenRoot, x, y, item.KindLabel, item.title, price, true, ShowSet, ArtFor(item.sku));
                }
            }
        }
        else if (_catalog != null && _catalog.Length > 0)
        {
            // 카탈로그는 읽었는데 이 탭이 비었다 — 없는 걸 있는 척하지 않는다
            CreateText(screenRoot, "이 분류에는 아직 상품이 없어요", 17f, Muted, TextAnchor.MiddleCenter,
                24f, 400f, 638f, 40f);
        }
        else
        {
            // 오프라인 폴백 — 서버를 못 읽어도 화면은 깨지지 않는다.
            // 목업이라도 전부 눌리게 둔다. 눌리지 않는 카드는 고장으로 읽힌다.
            CreateProductCard(screenRoot, 24f, 216f, "스킨", "노란 우비", "육포 8", false, ShowSkin, ArtFor("skin-raincoat"));
            CreateProductCard(screenRoot, 355f, 216f, "세트", "겨울 패딩 세트", "39,000원", true, ShowSet, ArtFor("set-winter"));
            CreateProductCard(screenRoot, 24f, 526f, "스킨", "체크 목도리", "육포 5", false, ShowSkin, ArtFor("skin-scarf"));
            CreateProductCard(screenRoot, 355f, 526f, "스킨", "노란 캡모자", "육포 6", false, ShowSkin, ArtFor("skin-cap"));
        }

        CreateText(screenRoot, "모든 상품은 확정 구매예요 — 뽑기·랜덤박스는 없어요", 14f, Muted, TextAnchor.MiddleCenter, 24f, 846f, 638f, 34f);
        CreatePrimaryButton(screenRoot, "육포 충전하기", 24f, 892f, 638f, 76f, ShowTopup);
    }


    private void BuildSkin()
    {
        string title = _selected != null ? _selected.title : "노란 우비";
        string price = _selected != null ? _selected.PriceLabel : "육포 8";
        string desc  = _selected != null && !string.IsNullOrEmpty(_selected.description)
            ? _selected.description : "비 오는 날 마당 연출이 바뀌어요";

        CreateHeader(screenRoot, title, true, ShowShop);
        CreatePreview(screenRoot, 24f, 156f, 638f, 370f, "단추 착용 미리보기",
            ArtFor(_selected != null ? _selected.sku : "skin-raincoat"));
        CreatePanelWithText(screenRoot, title, price, desc, 24f, 552f, 638f, 132f, White, Border);
        CreateInfoBox(screenRoot, "이 구매액의 10%는 공동 창고에 적립되어 보호소 기부에 쓰여요", 24f, 708f, 638f, 94f, donateBannerSprite);
        CreatePrimaryButton(screenRoot, $"{price}로 구매", 24f, 924f, 638f, 82f, delegate { Purchase(); });
        CreateText(screenRoot, "디지털 상품은 사용(착용) 후 청약철회가 제한돼요", 13f, Muted, TextAnchor.MiddleCenter, 24f, 1018f, 638f, 34f);
    }

    private void BuildSet()
    {
        CreateHeader(screenRoot, "겨울 패딩 세트", true, ShowShop);
        CreatePreview(screenRoot, 24f, 156f, 638f, 300f, "실물 옷 + 착용 스킨", ArtFor("set-winter"));
        CreatePanelWithText(screenRoot, "겨울 패딩 세트", "39,000원", "실물 옷 배송 + 같은 디자인 스킨 + 3,900 P 적립", 24f, 482f, 638f, 132f, White, Border);

        GameObject gaugeCard = CreateSpritePanel("Donation Gauge", screenRoot, 24f, 640f, 638f, 190f, gaugeCardSprite, new Color32(255, 248, 218, 255));
        CreateText(gaugeCard.transform, "500벌 팔리면, 보호소에 방한용품 100세트", 20f, Ink, TextAnchor.MiddleLeft, 22f, 18f, 594f, 42f);
        // 게이지도 전용 자산이 있다 — 각진 사각형으로 그리면 카드와 재질이 어긋난다
        CreateSpritePanel("Gauge Track", gaugeCard.transform, 22f, 82f, 594f, 22f, limitGaugeTrackSprite, new Color32(90, 58, 32, 38));
        CreateSpritePanel("Gauge Fill", gaugeCard.transform, 22f, 82f, 406f, 22f, limitGaugeFillSprite, new Color32(240, 168, 50, 255));
        CreateText(gaugeCard.transform, "342/500벌", 17f, GoldDark, TextAnchor.MiddleLeft, 22f, 118f, 220f, 32f);
        CreateText(gaugeCard.transform, "구매 시 내 기여 +0.2%", 15f, Muted, TextAnchor.MiddleRight, 330f, 118f, 280f, 32f);
        CreateText(gaugeCard.transform, "단추가 입는 이 옷과 같은 옷이 실제 보호견에게 갑니다", 14f, Muted, TextAnchor.MiddleLeft, 22f, 150f, 594f, 28f);

        CreatePrimaryButton(screenRoot, "자사몰에서 구매 (새 탭)", 24f, 946f, 638f, 82f, ShowCheckout);
        CreateText(screenRoot, "실물 옷은 일반 반품 규정, 스킨은 지급 후 철회 제한 — 각각 다른 규정이 적용돼요", 13f, Muted, TextAnchor.MiddleCenter, 24f, 1044f, 638f, 50f);
    }

    private void BuildCheckout()
    {
        CreateHeader(screenRoot, "구매 진행", true, ShowSet);
        CreateStepCard(screenRoot, 24f, 156f, "1. 자사몰로 이동", "완료", "새 탭이 안 열렸다면 아래 링크를 다시 열어 주세요", Green, delegate { ShowCheckoutLinkOpened(); });
        CreateStepCard(screenRoot, 24f, 326f, "2. 자사몰에서 결제", "진행 중", "결제를 마치면 이 화면이 자동으로 바뀌어요", GoldDark, null);
        CreateStepCard(screenRoot, 24f, 496f, "3. 스킨 지급", "대기", "", Muted, null);
        CreateInfoBox(screenRoot, "완료 상태 (지급 후)\n단추: 옷이 도착했어요! 판매 게이지도 +0.2% 올랐어요.", 24f, 682f, 638f, 138f);
        CreateText(screenRoot, "결제는 자사몰에서 진행되며 카드 정보는 게임 서버에 저장되지 않아요", 13f, Muted, TextAnchor.MiddleCenter, 24f, 862f, 638f, 46f);
    }

    private void ShowSkinPurchased()
    {
        CreateToast("스킨을 지급했어요. 단추가 새 우비를 입었어요!");
    }

    private void ShowCheckoutLinkOpened()
    {
        CreateToast("자사몰 링크를 다시 열 준비가 되었어요.");
    }

    private void CreateHeader(Transform parent, string title, bool back, UnityEngine.Events.UnityAction backAction = null)
    {
        GameObject header = CreateSpritePanel("Header", parent, 24f, 24f, 638f, 96f, productCardSprite, Brown, Brown);
        if (back)
        {
            CreateButton(header.transform, "←", 16f, 18f, 18f, 50f, 58f, SoftGold, BrownDark, backAction);
            CreateText(header.transform, title, 26f, Cream, TextAnchor.MiddleLeft, 82f, 20f, 300f, 56f);
        }
        else
        {
            CreateText(header.transform, title, 28f, Cream, TextAnchor.MiddleLeft, 90f, 20f, 150f, 56f);
            CreateSpriteIcon(header.transform, boneGoldSprite, 340f, 24f, 28f, 28f);
            // 잔액은 서버 값만 표시한다. 조회 전에는 "—" (PRD §5.5 — 클라가 계산하지 않는다)
            _boneLabel = CreateText(header.transform, _bones >= 0 ? $"{_bones:N0}" : "—",
                17f, Gold, TextAnchor.MiddleLeft, 372f, 24f, 86f, 48f);
            CreateSpriteIcon(header.transform, jerkySprite, 468f, 24f, 28f, 28f);
            _jerkyLabel = CreateText(header.transform, _jerky >= 0 ? $"육포 {_jerky}" : "육포 —",
                17f, Cream, TextAnchor.MiddleLeft, 500f, 24f, 120f, 48f);
        }
    }

    // 탭 = 서버 카탈로그의 kind 값. 라벨만 우리 말로 바꿔 쓴다.
    private static readonly string[] TabKinds  = { "skin", "set", "coupon" };
    private static readonly string[] TabLabels = { "스킨", "실물 옷+스킨", "쿠폰 교환" };
    private string _tab = "skin";

    private void CreateTabs(Transform parent, float top)
    {
        float[] widths = { 116f, 190f, 150f };
        float left = 24f;
        for (int i = 0; i < TabLabels.Length; i++)
        {
            bool on = TabKinds[i] == _tab;
            string kind = TabKinds[i];   // 클로저 캡처
            CreateButton(parent, TabLabels[i], 16f, left, top, widths[i], 54f,
                on ? Gold : White, on ? BrownDark : Muted,
                on ? (UnityEngine.Events.UnityAction)null : delegate { SelectTab(kind); });
            left += widths[i] + 10f;
        }
    }

    private void SelectTab(string kind)
    {
        _tab = kind;
        ShowShop();
    }

private void CreateProductCard(Transform parent, float x, float y, string kind, string title, string price, bool donation, UnityEngine.Events.UnityAction action, Sprite art = null)
    {
        Sprite frame = donation ? productCardDonateSprite : productCardSprite;
        GameObject card = CreateSpritePanel(title, parent, x, y, 307f, 286f, frame, White);
        // 분류 배지는 왼쪽 위 모서리로. 가운데에 두면 상품 그림과 겹친다.
        GameObject kindBadge = CreateSpritePanel("Kind", card.transform, 18f, 16f, 91f, 32f, statusPillWaitSprite, Cream);
        CreateText(kindBadge.transform, kind, 14f, GoldDark, TextAnchor.MiddleCenter, 2f, 1f, 87f, 30f);
        // 배지(끝 48)와 제목(시작 164) 사이의 칸에만 그림을 넣는다. preserveAspect가
        // 이 칸 안에 맞춰 축소하므로 카드 밖으로 넘치지 않는다.
        if (art != null) CreateSpriteIcon(card.transform, art, 48f, 54f, 211f, 104f);
        CreateText(card.transform, title, 20f, Ink, TextAnchor.MiddleCenter, 18f, 164f, 271f, 34f);
        CreateText(card.transform, price, 17f, GoldDark, TextAnchor.MiddleCenter, donation ? 18f : 18f, 216f, donation ? 150f : 271f, 30f);
        if (donation) CreateBadge(card.transform, "기부 연동", 190f, 216f, 99f, 32f, Green);
        if (action != null)
        {
            Button button = card.AddComponent<Button>();
            Image cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = true;
            button.targetGraphic = cardImage;
            button.onClick.AddListener(action);
        }
    }


private void CreatePreview(Transform parent, float x, float y, float width, float height, string label, Sprite art = null)
    {
        GameObject preview = CreateSpritePanel("Preview", parent, x, y, width, height, previewFrameSprite, new Color32(176, 123, 79, 24));

        // 그림이 있으면 그림이 미리보기다. 자리표시 라벨은 그림이 없을 때만 띄운다.
        if (art != null)
        {
            const float Pad = 32f;
            CreateSpriteIcon(preview.transform, art, Pad, Pad, width - Pad * 2f, height - Pad * 2f);
            return;
        }

        float labelWidth = Mathf.Min(320f, width - 80f);
        GameObject labelPanel = CreateSpritePanel("Preview Label", preview.transform, (width - labelWidth) * 0.5f, height * 0.5f - 24f, labelWidth, 48f, jerkyTileSprite, Cream);
        CreateText(labelPanel.transform, label, 16f, GoldDark, TextAnchor.MiddleCenter, 8f, 5f, labelWidth - 16f, 38f);
    }


    private void CreatePanelWithText(Transform parent, string title, string value, string subtitle, float x, float y, float width, float height, Color fill, Color outline)
    {
        GameObject card = CreateSpritePanel(title, parent, x, y, width, height, productCardSprite, fill);
        CreateText(card.transform, title, 23f, Ink, TextAnchor.MiddleLeft, 22f, 20f, width * 0.55f, 38f);
        CreateText(card.transform, value, 21f, GoldDark, TextAnchor.MiddleRight, width * 0.54f, 20f, width * 0.4f, 38f);
        CreateText(card.transform, subtitle, 16f, Muted, TextAnchor.MiddleLeft, 22f, 68f, width - 44f, 36f);
    }

private void CreateStepCard(Transform parent, float x, float y, string title, string status, string subtitle, Color statusColor, UnityEngine.Events.UnityAction action)
    {
        Sprite stepSprite = title.StartsWith("1.") ? stepCardDoneSprite : title.StartsWith("2.") ? stepCardActiveSprite : stepCardWaitingSprite;
        GameObject card = CreateSpritePanel(title, parent, x, y, 638f, 140f, stepSprite, White);
        CreateText(card.transform, title, 20f, Ink, TextAnchor.MiddleLeft, 22f, 18f, 380f, 38f);
        GameObject statusObject;
        if (status == "진행 중")
            statusObject = CreateSpritePanel("Status", card.transform, 490f, 19f, 124f, 32f, statusPillGoldSprite, statusColor);
        else if (status == "대기")
            statusObject = CreateSpritePanel("Status", card.transform, 490f, 19f, 124f, 32f, statusPillWaitSprite, statusColor);
        else
            statusObject = CreateSpritePanel("Status", card.transform, 490f, 19f, 124f, 32f, statusPillGoldSprite, statusColor);
        CreateText(statusObject.transform, status, 14f, BrownDark, TextAnchor.MiddleCenter, 2f, 0f, 120f, 32f);
        if (!string.IsNullOrEmpty(subtitle)) CreateText(card.transform, subtitle, 14f, Muted, TextAnchor.MiddleLeft, 22f, 62f, 440f, 34f);
        if (action != null)
        {
            CreateButton(card.transform, "링크 다시 열기", 16f, 22f, 90f, 176f, 40f, SoftGold, BrownDark, action);
            GameObject qr = CreateSpritePanel("QR Slot", card.transform, 536f, 70f, 56f, 56f, qrSlotSprite, SoftGold);
            CreateText(qr.transform, "QR", 14f, BrownDark, TextAnchor.MiddleCenter, 2f, 2f, 52f, 52f);
        }
    }


private void CreateInfoBox(Transform parent, string text, float x, float y, float width, float height, Sprite backgroundSprite = null)
    {
        GameObject box = backgroundSprite != null
            ? CreateSpritePanel("Info", parent, x, y, width, height, backgroundSprite, new Color32(255, 248, 218, 255))
            : CreateSpritePanel("Info", parent, x, y, width, height, gaugeCardSprite, new Color32(255, 248, 218, 255));
        CreateText(box.transform, text, 15f, Ink, TextAnchor.MiddleLeft, 18f, 14f, width - 36f, height - 28f);
    }


    private void CreateBadge(Transform parent, string text, float x, float y, float width, float height, Color color, Sprite backgroundSprite = null)
    {
        Sprite art = backgroundSprite != null ? backgroundSprite : statusPillGoldSprite;
        GameObject badge = art != null
            ? CreateSpritePanel("Badge", parent, x, y, width, height, art, color)
            : CreatePanel("Badge", parent, x, y, width, height, color);
        CreateText(badge.transform, text, 14f, art != null ? GoldDark : Color.white,
            TextAnchor.MiddleCenter, 2f, 1f, width - 4f, height - 2f);
    }


    private Button CreatePrimaryButton(Transform parent, string label, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
    {
        return CreateButton(parent, label, 23f, x, y, width, height, Gold, BrownDark, action);
    }

    /// <summary>
    /// 버튼. 금색 계열은 price-button 자산(그라데이션·그림자·광택)을 배경으로 깔고
    /// 그 위에 글자를 얹는다. 단색 사각형으로 그리면 같은 화면의 카드 자산과
    /// 재질이 어긋난다.
    /// </summary>
    private Button CreateButton(Transform parent, string label, float fontSize, float x, float y, float width, float height, Color fill, Color textColor, UnityEngine.Events.UnityAction action)
    {
        // 금색 계열은 price-button, 나머지는 흰 라운드 카드에 색을 곱해 쓴다.
        // 선택된 버튼만 자산을 쓰면 미선택 버튼이 각진 사각형으로 남아 튄다.
        bool goldArt = priceButtonSprite != null && (fill == Gold || fill == SoftGold);
        bool cardArt = !goldArt && productCardSprite != null;
        bool useArt = goldArt || cardArt;
        GameObject buttonObject = goldArt
            ? CreateSpritePanel(label + " Button", parent, x, y, width, height, priceButtonSprite, fill)
            : cardArt
                ? CreateSpritePanel(label + " Button", parent, x, y, width, height, productCardSprite, fill, fill)
                : CreatePanel(label + " Button", parent, x, y, width, height, fill, Border);

        Button button = buttonObject.AddComponent<Button>();
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.raycastTarget = true;
        button.targetGraphic = buttonImage;

        // 자산을 쓸 땐 색을 곱하지 않는다. 흰색이 원본 그라데이션이다.
        Color baseColor = goldArt ? Color.white : fill;
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.12f);
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        button.colors = colors;

        CreateText(buttonObject.transform, label, fontSize, textColor, TextAnchor.MiddleCenter, 4f, 2f, width - 8f, height - 4f);
        if (action != null) button.onClick.AddListener(action);
        return button;
    }

    /// <summary>
    /// svg-shop 자산 한 장을 배경으로 깐다.
    ///
    /// 자산은 전부 둥근 모서리 + 아래 그림자다. 원본 크기 그대로 늘리면
    /// 모서리와 그림자가 함께 늘어나 뭉개진다. 9-슬라이스 테두리가 있는
    /// 자산은 Sliced로 그려 모서리를 원본 픽셀 그대로 유지한다.
    /// </summary>
    private GameObject CreateSpritePanel(string name, Transform parent, float x, float y, float width, float height, Sprite sprite, Color fallback, Color? tint = null)
    {
        GameObject objectToCreate = CreatePanel(name, parent, x, y, width, height, fallback);
        Image image = objectToCreate.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = false;
        // 자산은 흰 라운드 카드 한 장뿐이다. 다른 색이 필요하면 곱해서 재사용한다.
        image.color = sprite == null ? fallback : (tint ?? Color.white);
        return objectToCreate;
    }

    private void CreateSpriteIcon(Transform parent, Sprite sprite, float x, float y, float width, float height)
    {
        if (sprite == null) return;
        GameObject icon = new GameObject("SVG Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(parent, false);
        Place(icon.GetComponent<RectTransform>(), x, y, width, height);
        Image image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

private GameObject CreatePanel(string name, Transform parent, float x, float y, float width, float height, Color fill, Color? outline = null)
    {
        GameObject objectToCreate = new GameObject(name, typeof(RectTransform), typeof(Image));
        objectToCreate.transform.SetParent(parent, false);
        RectTransform rect = objectToCreate.GetComponent<RectTransform>();
        Place(rect, x, y, width, height);
        Image image = objectToCreate.GetComponent<Image>();
        image.color = fill;
        image.raycastTarget = false;
        if (outline.HasValue)
        {
            Outline border = objectToCreate.AddComponent<Outline>();
            border.effectColor = outline.Value;
            border.effectDistance = new Vector2(2f, -2f);
            border.useGraphicAlpha = true;
        }
        return objectToCreate;
    }

    private TMP_Text CreateText(Transform parent, string content, float size, Color color, TextAnchor alignment, float x, float y, float width, float height)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        Place(rect, x, y, width, height);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = content;
        text.font = marketFont;
        text.fontSize = size;
        text.color = color;
        text.alignment = ToTmpAlignment(alignment);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
    {
        switch (alignment)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private void CreateToast(string message)
    {
        if (screenRoot == null) return;
        GameObject toast = CreatePanel("Toast", screenRoot, 54f, 1080f, 578f, 72f, BrownDark, GoldDark);
        CreateText(toast.transform, message, 16f, Cream, TextAnchor.MiddleCenter, 16f, 8f, 546f, 56f);
        Destroy(toast, 2.4f);
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        RectTransform parentRect = rect.parent as RectTransform;
        float parentWidth = parentRect == null || parentRect.rect.width <= 0f ? ReferenceWidth : parentRect.rect.width;
        float parentHeight = parentRect == null || parentRect.rect.height <= 0f ? ReferenceHeight : parentRect.rect.height;
        rect.anchoredPosition = new Vector2(x + width * 0.5f - parentWidth * 0.5f, parentHeight * 0.5f - y - height * 0.5f);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        if (Application.isPlaying) DontDestroyOnLoad(eventSystem);
    }

    // ---------- F-05 육포 충전 ----------

    private Backend.ShopApi.JerkyPack[] _packs = System.Array.Empty<Backend.ShopApi.JerkyPack>();
    private Backend.ShopApi.PaymentLimit _limit;

    /// <summary>
    /// 충전 화면. 결제 한도 게이지 + 패키지 목록.
    ///
    /// 한도는 서버가 강제한다 — 클라가 버튼을 흐리게 하는 건 안내일 뿐이고,
    /// 초과 요청은 purchase_jerky가 거부한다.
    /// </summary>
    private void BuildTopup()
    {
        CreateHeader(screenRoot, "육포 충전", true, ShowShop);

        // --- 이번 달 결제 한도 ---
        GameObject card = CreateSpritePanel("Limit Card", screenRoot, 24f, 156f, 638f, 150f, limitCardSprite, White);
        CreateText(card.transform, "이번 달 결제 한도", 18f, Ink, TextAnchor.MiddleLeft, 22f, 16f, 300f, 34f);
        CreateText(card.transform,
            _limit != null ? $"{_limit.spent:N0} / {_limit.cap:N0}원" : "불러오는 중",
            18f, GoldDark, TextAnchor.MiddleRight, 316f, 16f, 300f, 34f);

        CreateSpritePanel("Limit Track", card.transform, 22f, 66f, 594f, 22f, limitGaugeTrackSprite, new Color32(90, 58, 32, 38));
        if (_limit != null && _limit.percent > 0)
        {
            float width = Mathf.Max(22f, 594f * Mathf.Clamp01(_limit.percent / 100f));
            CreateSpritePanel("Limit Fill", card.transform, 22f, 66f, width, 22f, limitGaugeFillSprite, new Color32(240, 168, 50, 255));
        }
        CreateText(card.transform,
            _limit != null ? $"남은 한도 {_limit.remaining:N0}원" : "한도를 확인하는 중이에요",
            15f, Muted, TextAnchor.MiddleLeft, 22f, 100f, 594f, 30f);

        // --- 충전 패키지 ---
        float y = 336f;
        if (_packs != null && _packs.Length > 0)
        {
            foreach (Backend.ShopApi.JerkyPack pack in _packs)
            {
                CreatePackRow(pack, y);
                y += 116f;
            }
        }
        else
        {
            CreateText(screenRoot, "충전 상품을 불러오지 못했어요", 16f, Muted, TextAnchor.MiddleCenter, 24f, y, 638f, 44f);
            y += 76f;
        }

        // 정직성 라벨 — 데모라는 사실을 숨기지 않는다 (PRD §6.5와 같은 태도)
        CreateText(screenRoot, "데모 빌드예요 — 실제 결제는 일어나지 않고 육포만 지급돼요",
            14f, Muted, TextAnchor.MiddleCenter, 24f, y + 8f, 638f, 44f);
    }

    /// <summary>충전 패키지 한 줄. best면 강조 카드 자산을 쓴다.</summary>
    private void CreatePackRow(Backend.ShopApi.JerkyPack pack, float y)
    {
        Sprite frame = (pack.best && packCardBestSprite != null) ? packCardBestSprite : packCardSprite;
        GameObject row = CreateSpritePanel("Pack " + pack.sku, screenRoot, 24f, y, 638f, 104f, frame, White);

        CreateSpritePanel("Tile", row.transform, 20f, 22f, 60f, 60f, jerkyTileSprite, new Color32(255, 244, 202, 255));
        CreateSpriteIcon(row.transform, jerkySprite, 34f, 36f, 32f, 32f);

        CreateText(row.transform, $"육포 {pack.jerky}", 21f, Ink, TextAnchor.MiddleLeft, 100f, 20f, 300f, 34f);
        CreateText(row.transform, pack.SubLabel, 15f, pack.best ? GoldDark : Muted, TextAnchor.MiddleLeft, 100f, 56f, 300f, 30f);

        // 한도를 넘으면 미리 흐리게 — 그래도 최종 판정은 서버가 한다
        bool affordable = _limit == null || _limit.remaining >= pack.krw;
        GameObject button = CreateSpritePanel("Price", row.transform, 448f, 26f, 168f, 54f, priceButtonSprite, Gold);
        if (!affordable)
        {
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = new Color(image.color.r, image.color.g, image.color.b, 0.4f);
        }
        CreateText(button.transform, $"{pack.krw:N0}원", 18f, BrownDark, TextAnchor.MiddleCenter, 0f, 0f, 168f, 54f);

        Button click = button.AddComponent<Button>();
        Backend.ShopApi.JerkyPack captured = pack;
        click.onClick.AddListener(delegate { TopupPurchase(captured); });
    }

    /// <summary>충전 실행. 한도 초과·오류는 서버 메시지를 그대로 보여준다.</summary>
    private async void TopupPurchase(Backend.ShopApi.JerkyPack pack)
    {
        Backend.ShopApi.TopupResult result = await Backend.ShopApi.PurchaseJerky(pack.sku);
        if (!result.Ok)
        {
            _notice = result.message;
            Debug.LogWarning($"[Market] 충전 실패: {result.message}");
            Refresh();
            return;
        }

        _jerky = await Backend.ShopApi.GetJerky();
        if (_jerky >= 0) GameCurrencyStore.SetJerky(_jerky);
        _limit = await Backend.ShopApi.GetLimit();
        _notice = $"육포 {result.jerky}개를 지급했어요 (모의 결제)";
        Refresh();
    }

    /// <summary>충전 화면 진입 — 패키지·한도를 서버에서 읽고 그린다.</summary>
    private async void LoadTopup()
    {
        if (!await Backend.AppSession.EnsureSignedIn())
        {
            Debug.LogWarning("[Market] 로그인 실패 — 충전 정보를 불러오지 못했어요");
            return;
        }
        _packs = await Backend.ShopApi.GetPacks();
        _limit = await Backend.ShopApi.GetLimit();
        if (currentScreen == ScreenId.Topup) Refresh();
    }
}
