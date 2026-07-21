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
    public Font marketFont;
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
        Checkout
    }

    private void Start()
    {
        BuildInterface();
        ShowShop();
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
            marketFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        else BuildCheckout();
    }

    private void BuildShop()
    {
        CreateHeader(screenRoot, "상점", false);
        CreateTabs(screenRoot, 132f, "스킨");
        CreateText(screenRoot, "확정 구매 · 단추의 새 옷과 보호소 연동", 16f, Muted, TextAnchor.MiddleCenter, 24f, 188f, 638f, 28f);

        CreateProductCard(screenRoot, 24f, 216f, "스킨", "노란 우비", "육포 8", false, ShowSkin);
        CreateProductCard(screenRoot, 355f, 216f, "세트", "겨울 패딩 세트", "39,000원", true, ShowSet);
        CreateProductCard(screenRoot, 24f, 526f, "스킨", "체크 목도리", "육포 5", false, null);
        CreateProductCard(screenRoot, 355f, 526f, "스킨", "기본 반다나", "P 1,500", false, null);

        CreateText(screenRoot, "모든 상품은 확정 구매예요 — 뽑기·랜덤박스는 없어요", 14f, Muted, TextAnchor.MiddleCenter, 24f, 846f, 638f, 34f);
    }


    private void BuildSkin()
    {
        CreateHeader(screenRoot, "노란 우비", true, ShowShop);
        CreatePreview(screenRoot, 24f, 156f, 638f, 370f, "단추 착용 미리보기");
        CreatePanelWithText(screenRoot, "노란 우비", "육포 8", "비 오는 날 마당 연출이 바뀌어요", 24f, 552f, 638f, 132f, White, Border);
        CreateInfoBox(screenRoot, "이 구매액의 10%는 공동 창고에 적립되어 보호소 기부에 쓰여요", 24f, 708f, 638f, 94f, donateBannerSprite);
        CreatePrimaryButton(screenRoot, "육포 8로 구매", 24f, 924f, 638f, 82f, delegate { ShowSkinPurchased(); });
        CreateText(screenRoot, "디지털 상품은 사용(착용) 후 청약철회가 제한돼요", 13f, Muted, TextAnchor.MiddleCenter, 24f, 1018f, 638f, 34f);
    }

    private void BuildSet()
    {
        CreateHeader(screenRoot, "겨울 패딩 세트", true, ShowShop);
        CreatePreview(screenRoot, 24f, 156f, 638f, 300f, "실물 옷 + 착용 스킨");
        CreatePanelWithText(screenRoot, "겨울 패딩 세트", "39,000원", "실물 옷 배송 + 같은 디자인 스킨 + 3,900 P 적립", 24f, 482f, 638f, 132f, White, Border);

        GameObject gaugeCard = CreateSpritePanel("Donation Gauge", screenRoot, 24f, 640f, 638f, 190f, gaugeCardSprite, new Color32(255, 248, 218, 255));
        CreateText(gaugeCard.transform, "500벌 팔리면, 보호소에 방한용품 100세트", 20f, Ink, TextAnchor.MiddleLeft, 22f, 18f, 594f, 42f);
        CreatePanel("Gauge Track", gaugeCard.transform, 22f, 82f, 594f, 24f, new Color32(90, 58, 32, 38));
        CreatePanel("Gauge Fill", gaugeCard.transform, 22f, 82f, 406f, 24f, new Color32(240, 168, 50, 255));
        CreateText(gaugeCard.transform, "342/500벌", 17f, GoldDark, TextAnchor.MiddleLeft, 22f, 118f, 220f, 32f);
        CreateText(gaugeCard.transform, "구매 시 내 기여 +0.2%", 15f, Muted, TextAnchor.MiddleRight, 330f, 118f, 280f, 32f);
        CreateText(gaugeCard.transform, "단추가 입는 이 옷과 같은 옷이 실제 보호견에게 갑니다", 14f, Muted, TextAnchor.MiddleLeft, 22f, 150f, 594f, 28f);

        CreatePrimaryButton(screenRoot, "자사몰에서 구매 (새 탭)", 24f, 946f, 638f, 82f, ShowCheckout);
        CreateText(screenRoot, "실물 옷은 일반 반품 규정, 스킨은 지급 후 철회 제한 — 각각 다른 규정이 적용돼요", 13f, Muted, TextAnchor.MiddleCenter, 24f, 1044f, 638f, 50f);
    }

    private void BuildCheckout()
    {
        CreateHeader(screenRoot, "구매 진행", false);
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
        GameObject header = CreatePanel("Header", parent, 24f, 24f, 638f, 96f, Brown);
        if (back)
        {
            CreateButton(header.transform, "←", 16f, 18f, 18f, 50f, 58f, SoftGold, BrownDark, backAction);
            CreateText(header.transform, title, 26f, Cream, TextAnchor.MiddleLeft, 82f, 20f, 300f, 56f);
        }
        else
        {
            CreateText(header.transform, title, 28f, Cream, TextAnchor.MiddleLeft, 22f, 20f, 190f, 56f);
            CreateSpriteIcon(header.transform, boneGoldSprite, 340f, 24f, 28f, 28f);
            CreateText(header.transform, "12,680", 17f, Gold, TextAnchor.MiddleLeft, 372f, 24f, 86f, 48f);
            CreateSpriteIcon(header.transform, jerkySprite, 468f, 24f, 28f, 28f);
            CreateText(header.transform, "육포 12", 17f, Cream, TextAnchor.MiddleLeft, 500f, 24f, 120f, 48f);
        }
    }

    private void CreateTabs(Transform parent, float top, string active)
    {
        string[] labels = { "스킨", "실물 옷+스킨", "쿠폰 교환" };
        float[] widths = { 116f, 190f, 150f };
        float left = 24f;
        for (int i = 0; i < labels.Length; i++)
        {
            bool on = labels[i] == active;
            CreateButton(parent, labels[i], 16f, left, top, widths[i], 54f, on ? Gold : White, on ? BrownDark : Muted, null);
            left += widths[i] + 10f;
        }
    }

private void CreateProductCard(Transform parent, float x, float y, string kind, string title, string price, bool donation, UnityEngine.Events.UnityAction action)
    {
        Sprite frame = donation ? productCardDonateSprite : productCardSprite;
        GameObject card = CreateSpritePanel(title, parent, x, y, 307f, 286f, frame, White);
        GameObject kindBadge = CreatePanel("Kind", card.transform, 108f, 42f, 91f, 32f, Cream, new Color32(226, 164, 54, 100));
        CreateText(kindBadge.transform, kind, 14f, GoldDark, TextAnchor.MiddleCenter, 2f, 1f, 87f, 30f);
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


private void CreatePreview(Transform parent, float x, float y, float width, float height, string label)
    {
        GameObject preview = CreateSpritePanel("Preview", parent, x, y, width, height, previewFrameSprite, new Color32(176, 123, 79, 24));
        float labelWidth = Mathf.Min(320f, width - 80f);
        GameObject labelPanel = CreatePanel("Preview Label", preview.transform, (width - labelWidth) * 0.5f, height * 0.5f - 24f, labelWidth, 48f, Cream, new Color32(226, 164, 54, 100));
        CreateText(labelPanel.transform, label, 16f, GoldDark, TextAnchor.MiddleCenter, 8f, 5f, labelWidth - 16f, 38f);
    }


    private void CreatePanelWithText(Transform parent, string title, string value, string subtitle, float x, float y, float width, float height, Color fill, Color outline)
    {
        GameObject card = CreatePanel(title, parent, x, y, width, height, fill, outline);
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
            statusObject = CreateSpritePanel("Status", card.transform, 486f, 18f, 128f, 34f, statusPillGoldSprite, statusColor);
        else if (status == "대기")
            statusObject = CreateSpritePanel("Status", card.transform, 486f, 18f, 128f, 34f, statusPillWaitSprite, statusColor);
        else
            statusObject = CreatePanel("Status", card.transform, 486f, 18f, 128f, 34f, statusColor, new Color32(60, 110, 26, 180));
        CreateText(statusObject.transform, status, 14f, status == "완료" ? Color.white : BrownDark, TextAnchor.MiddleCenter, 4f, 1f, 120f, 32f);
        if (!string.IsNullOrEmpty(subtitle)) CreateText(card.transform, subtitle, 14f, Muted, TextAnchor.MiddleLeft, 22f, 62f, 440f, 34f);
        if (action != null)
        {
            CreateButton(card.transform, "링크 다시 열기", 17f, 22f, 92f, 168f, 34f, Brown, Cream, action);
            GameObject qr = CreateSpritePanel("QR Slot", card.transform, 536f, 70f, 56f, 56f, qrSlotSprite, SoftGold);
            CreateText(qr.transform, "QR", 14f, BrownDark, TextAnchor.MiddleCenter, 2f, 2f, 52f, 52f);
        }
    }


private void CreateInfoBox(Transform parent, string text, float x, float y, float width, float height, Sprite backgroundSprite = null)
    {
        GameObject box = backgroundSprite != null
            ? CreateSpritePanel("Info", parent, x, y, width, height, backgroundSprite, new Color32(255, 248, 218, 255))
            : CreatePanel("Info", parent, x, y, width, height, new Color32(255, 248, 218, 255), new Color32(226, 164, 54, 140));
        CreateText(box.transform, text, 15f, Ink, TextAnchor.MiddleLeft, 18f, 14f, width - 36f, height - 28f);
    }


    private void CreateBadge(Transform parent, string text, float x, float y, float width, float height, Color color, Sprite backgroundSprite = null)
    {
        GameObject badge = CreatePanel("Badge", parent, x, y, width, height, color);
        CreateText(badge.transform, text, 14f, Color.white, TextAnchor.MiddleCenter, 2f, 1f, width - 4f, height - 2f);
    }


    private Button CreatePrimaryButton(Transform parent, string label, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
    {
        return CreateButton(parent, label, 23f, x, y, width, height, Gold, BrownDark, action);
    }

    private Button CreateButton(Transform parent, string label, float fontSize, float x, float y, float width, float height, Color fill, Color textColor, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreatePanel(label + " Button", parent, x, y, width, height, fill, fill == Gold ? GoldDark : Border);
        Button button = buttonObject.AddComponent<Button>();
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.raycastTarget = true;
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.normalColor = fill;
        colors.highlightedColor = Color.Lerp(fill, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(fill, Color.black, 0.12f);
        button.colors = colors;
        CreateText(buttonObject.transform, label, fontSize, textColor, TextAnchor.MiddleCenter, 4f, 2f, width - 8f, height - 4f);
        if (action != null) button.onClick.AddListener(action);
        return button;
    }

    private GameObject CreateSpritePanel(string name, Transform parent, float x, float y, float width, float height, Sprite sprite, Color fallback)
    {
        GameObject objectToCreate = CreatePanel(name, parent, x, y, width, height, fallback);
        Image image = objectToCreate.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = sprite == null ? fallback : Color.white;
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

    private Text CreateText(Transform parent, string content, float size, Color color, TextAnchor alignment, float x, float y, float width, float height)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        Place(rect, x, y, width, height);
        Text text = textObject.GetComponent<Text>();
        text.text = content;
        text.font = marketFont;
        text.fontSize = Mathf.RoundToInt(size);
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
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
}
