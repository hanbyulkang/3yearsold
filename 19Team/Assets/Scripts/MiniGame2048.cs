using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MG2(2048) — minigame02 씬. mg2-2048-mockup.html 시안 그대로 구현한다:
/// 393×852 · 콘텐츠 폭 362(좌우 15.5) · 보드 362, 셀 80, 간격 8, 내측 패딩 9.
/// 아트는 Assets/UI/MiniGame2 (svg-mg2 렌더본). UI는 전부 코드로 만든다.
/// 화면은 시안의 MG2-B(플레이) / MG2-C(결과) 두 장이다.
/// </summary>
public sealed class MiniGame2048 : MonoBehaviour
{
    private const int Size = 4;
    private readonly int[,] board = new int[Size, Size];
    private readonly Image[,] tileImages = new Image[Size, Size];

    [Header("MG2 아트 (Assets/UI/MiniGame2)")]
    [SerializeField] private TMP_FontAsset koreanFont;
    [SerializeField] private Sprite boardBg;
    [SerializeField] private Sprite[] tileSprites = new Sprite[10]; // lv1 x1/x2/x4, lv2 x1/x2/x4, lv3 x1/x2/x4, lv4(강아지)
    [SerializeField] private Sprite headerBar;
    [SerializeField] private Sprite chipPill;
    [SerializeField] private Sprite closeBtn;
    [SerializeField] private Sprite medalSprite;
    [SerializeField] private Sprite btnGold;
    [SerializeField] private Sprite btnDark;
    [SerializeField] private Sprite rewardCard;
    [SerializeField] private Sprite coachCard;
    [SerializeField] private Sprite iconBone;
    [SerializeField] private Sprite iconDogface;

    // ---- 시안 토큰 ----
    private static readonly Color Cream = FromHex(0xFAF3E6);
    private static readonly Color Ink = FromHex(0x4A3327);
    private static readonly Color SubInk = FromHex(0x8A7A62);
    private static readonly Color GoldInk = FromHex(0xB8762A);

    // 보드 격자: 내측 패딩 9 + 셀 80·4 + 간격 8·3 = 362
    private const float CellStep = 88f;   // 80 + 8
    private const float CellHalfSpan = 132f; // (362 - 18 - 80) / 2

    private GameObject playPanel, resultPanel;
    private TextMeshProUGUI scoreChipText, bestChipText, hintText;
    private TextMeshProUGUI resultTitle, resultBones, resultSubLine, coachText;
    private Image resultBoneIcon;
    private int score;
    private int best;
    private int moves;
    private bool busy;
    private bool playing;
    private Vector2 touchStart;
    private Vector2 mouseStart;
    private bool mouseDragging;

    private const string DefaultHint = "같은 간식을 밀어서 합치면 다음 간식이 돼요";

    private void Start()
    {
        Application.targetFrameRate = 60;
        best = PlayerPrefs.GetInt("MiniGame2048Best", 0);
        if (koreanFont == null) koreanFont = TMP_Settings.defaultFontAsset;
        BuildUI();
        // 서버에서 발바닥 잔량을 동기화한 뒤 첫 판을 연다 (오프라인이면 추정값으로 진행)
        Backend.Game2048Bridge.Refresh(StartRound);
    }

    private void Update()
    {
        if (!playing || busy) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame) Move(Vector2Int.left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) Move(Vector2Int.right);
            else if (keyboard.upArrowKey.wasPressedThisFrame) Move(Vector2Int.up);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Move(Vector2Int.down);
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var touch = touchscreen.primaryTouch;
            Vector2 position = touch.position.ReadValue();
            if (touch.press.wasPressedThisFrame) touchStart = position;
            if (touch.press.wasReleasedThisFrame) TrySwipe(position - touchStart);
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                mouseStart = position;
                mouseDragging = true;
            }
            if (mouseDragging && mouse.leftButton.wasReleasedThisFrame)
            {
                mouseDragging = false;
                TrySwipe(position - mouseStart);
            }
        }
    }

    private void TrySwipe(Vector2 delta)
    {
        if (delta.magnitude <= 55f) return;
        Move(Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
            ? (delta.x > 0 ? Vector2Int.right : Vector2Int.left)
            : (delta.y > 0 ? Vector2Int.up : Vector2Int.down));
    }

    // ---- UI 구성 (시안 좌표를 중앙 원점으로 환산: x-196.5, y 426-top) ----

    private void BuildUI()
    {
        var canvasGo = new GameObject("MG2Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(393, 852);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        BuildPlayPanel(canvasGo.transform);
        BuildResultPanel(canvasGo.transform);
        resultPanel.SetActive(false);
    }

    private void BuildPlayPanel(Transform parent)
    {
        playPanel = MakePanel(parent, "PlayPanel");
        var bg = MakeImage(playPanel.transform, "Bg", Cream, null);
        Stretch(bg.rectTransform);

        // X 버튼: x15.5 y16 44×44 (+아래 그림자 3)
        var close = MakeImage(playPanel.transform, "CloseBtn", Color.white, closeBtn);
        close.raycastTarget = true;
        SetRect(close.rectTransform, new Vector2(-159f, 386.5f), new Vector2(44f, 47f));
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close;
        closeButton.onClick.AddListener(ReturnToVillage);

        // 헤더 바: y72 362×64 — "멍멍 2048" + 점수/베스트 칩
        var header = MakeImage(playPanel.transform, "Header", Color.white, headerBar);
        SetRect(header.rectTransform, new Vector2(0f, 322f), new Vector2(362f, 64f));
        var title = MakeText(header.transform, "Title", "멍멍 2048", 20, FontStyles.Bold, Cream,
            new Vector2(-97f, 0f), new Vector2(140f, 40f));
        title.alignment = TextAlignmentOptions.Left;
        title.characterSpacing = 2f;

        scoreChipText = MakeChip(header.transform, "ScoreChip", new Vector2(-13f, 0f), new Vector2(104f, 32f));
        bestChipText = MakeChip(header.transform, "BestChip", new Vector2(107f, 0f), new Vector2(120f, 32f));

        // 보드: y172 362×362 — 빈 슬롯은 보드 아트에 새겨져 있다
        var boardImg = MakeImage(playPanel.transform, "Board", Color.white, boardBg);
        boardImg.raycastTarget = true;
        SetRect(boardImg.rectTransform, new Vector2(0f, 73f), new Vector2(362f, 362f));
        for (int row = 0; row < Size; row++)
            for (int col = 0; col < Size; col++)
            {
                var tile = MakeImage(boardImg.transform, $"Tile {row}-{col}", Color.white, null);
                SetRect(tile.rectTransform, CellCenter(row, col), new Vector2(80f, 80f));
                tile.gameObject.SetActive(false);
                tileImages[row, col] = tile;
            }

        // 안내: y560
        hintText = MakeText(playPanel.transform, "Hint", DefaultHint, 14, FontStyles.Normal, SubInk,
            new Vector2(0f, -145f), new Vector2(362f, 22f));
    }

    private void BuildResultPanel(Transform parent)
    {
        resultPanel = MakePanel(parent, "ResultPanel");
        var bg = MakeImage(resultPanel.transform, "Bg", Cream, null);
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;   // 뒤 보드 입력 차단

        // 메달: y120 112×118
        var medal = MakeImage(resultPanel.transform, "Medal", Color.white, medalSprite);
        SetRect(medal.rectTransform, new Vector2(0f, 247f), new Vector2(112f, 118f));

        resultTitle = MakeText(resultPanel.transform, "Title", "잘 놀았어요!", 32, FontStyles.Bold, Ink,
            new Vector2(0f, 152f), new Vector2(362f, 46f));

        // 보상 카드: y320 362×96 — "+N 🦴" + 요약 한 줄
        var reward = MakeImage(resultPanel.transform, "RewardCard", Color.white, rewardCard);
        SetRect(reward.rectTransform, new Vector2(0f, 58f), new Vector2(362f, 96f));
        resultBones = MakeText(reward.transform, "Bones", "+0", 30, FontStyles.Bold, GoldInk,
            new Vector2(-19f, 13f), new Vector2(240f, 40f));
        resultBoneIcon = MakeImage(reward.transform, "BoneIcon", Color.white, iconBone);
        resultBoneIcon.preserveAspect = true;
        SetRect(resultBoneIcon.rectTransform, new Vector2(30f, 13f), new Vector2(30f, 30f));
        resultSubLine = MakeText(reward.transform, "Sub", "", 14, FontStyles.Normal, SubInk,
            new Vector2(0f, -26f), new Vector2(334f, 22f));

        // 보호견 코멘트: y448 362×76 (점선 카드)
        var coach = MakeImage(resultPanel.transform, "CoachCard", Color.white, coachCard);
        SetRect(coach.rectTransform, new Vector2(0f, -60f), new Vector2(362f, 76f));
        var face = MakeImage(coach.transform, "Face", Color.white, iconDogface);
        face.preserveAspect = true;
        SetRect(face.rectTransform, new Vector2(-156f, 14f), new Vector2(26f, 26f));
        coachText = MakeText(coach.transform, "Msg", "", 15, FontStyles.Normal, Ink,
            new Vector2(15f, -2f), new Vector2(304f, 56f));
        coachText.alignment = TextAlignmentOptions.TopLeft;
        coachText.lineSpacing = 8f;

        // 버튼: y736 — 한 번 더(골드 216) / 홈으로(다크 136)
        MakeArtButton(resultPanel.transform, "RetryBtn", "한 번 더 (발바닥 1)", Ink, btnGold,
            new Vector2(-73f, -342f), new Vector2(216f, 64f), 18, RetryRound);
        MakeArtButton(resultPanel.transform, "HomeBtn", "홈으로", Cream, btnDark,
            new Vector2(113f, -342f), new Vector2(136f, 64f), 19, ReturnToVillage);
    }

    private static Vector2 CellCenter(int row, int col)
    { return new Vector2(col * CellStep - CellHalfSpan, CellHalfSpan - row * CellStep); }

    // ---- 흐름 ----

    private void StartRound()
    {
        // 판 시작에 발바닥 1개를 쓴다 (PRD §5.1 — 미니게임 입장 전용)
        if (!Backend.Game2048Bridge.BeginRound())
        {
            hintText.text = "발바닥이 다 떨어졌어요. 잠시 뒤 다시 채워집니다";
            return;
        }
        resultPanel.SetActive(false);
        playPanel.SetActive(true);
        playing = true;
        score = 0;
        moves = 0;
        Array.Clear(board, 0, board.Length);
        AddRandomTile();
        AddRandomTile();
        UpdateView();
        hintText.text = DefaultHint;
    }

    private void RetryRound()
    {
        if (!Backend.Game2048Bridge.BeginRound())
        {
            coachText.text = "단추: 발바닥이 다 떨어졌어요. 잠시 뒤 다시 채워져요!";
            return;
        }
        resultPanel.SetActive(false);
        playing = true;
        score = 0;
        moves = 0;
        Array.Clear(board, 0, board.Length);
        AddRandomTile();
        AddRandomTile();
        UpdateView();
        hintText.text = DefaultHint;
    }

    private void ReturnToVillage()
    {
        SceneManager.LoadScene("Suntail Village");
    }

    private void Move(Vector2Int direction)
    {
        if (busy) return;
        if (!CanMove(direction)) { hintText.text = "그 방향으로는 움직일 수 없어요"; return; }
        bool[,] merged = new bool[Size, Size];
        Vector2Int gridDirection = new Vector2Int(direction.x, -direction.y);
        bool horizontal = gridDirection.x != 0;
        int outerStart = gridDirection.y > 0 || gridDirection.x > 0 ? Size - 1 : 0;
        int outerEnd = gridDirection.y > 0 || gridDirection.x > 0 ? -1 : Size;
        int outerStep = gridDirection.y > 0 || gridDirection.x > 0 ? -1 : 1;
        for (int outer = outerStart; outer != outerEnd; outer += outerStep)
        {
            int innerStart = gridDirection.y > 0 || gridDirection.x > 0 ? Size - 1 : 0;
            int innerEnd = gridDirection.y > 0 || gridDirection.x > 0 ? -1 : Size;
            int innerStep = gridDirection.y > 0 || gridDirection.x > 0 ? -1 : 1;
            for (int inner = innerStart; inner != innerEnd; inner += innerStep)
            {
                int row = horizontal ? outer : inner, col = horizontal ? inner : outer; int value = board[row, col]; if (value == 0) continue;
                int r = row, c = col;
                while (true)
                {
                    int nr = r + gridDirection.y, nc = c + gridDirection.x; if (nr < 0 || nr >= Size || nc < 0 || nc >= Size || (board[nr, nc] != 0 && board[nr, nc] != value)) break;
                    if (board[nr, nc] == value && !merged[nr, nc]) { board[nr, nc] *= 2; score += board[nr, nc]; merged[nr, nc] = true; board[r, c] = 0; break; }
                    if (board[nr, nc] == 0) { board[nr, nc] = board[r, c]; board[r, c] = 0; r = nr; c = nc; } else break;
                }
            }
        }
        moves++;
        if (score > best) { best = score; PlayerPrefs.SetInt("MiniGame2048Best", best); }
        AddRandomTile(); UpdateView(); StartCoroutine(AnimateSlide(direction));
        hintText.text = DefaultHint;
        if (HasDog()) ShowResult(true);
        else if (!HasMoves()) ShowResult(false);
    }

    private IEnumerator AnimateSlide(Vector2Int direction)
    {
        busy = true;
        Vector2 offset = new Vector2(-direction.x * 24f, -direction.y * 24f);
        List<RectTransform> movedRects = new List<RectTransform>();
        List<Vector2> homes = new List<Vector2>();
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
        {
            if (board[r, c] == 0) continue;
            RectTransform rect = tileImages[r, c].rectTransform;
            movedRects.Add(rect);
            homes.Add(CellCenter(r, c));
            rect.anchoredPosition = CellCenter(r, c) + offset;
            rect.localScale = Vector3.one * 0.94f;
        }
        float elapsed = 0f;
        while (elapsed < 0.16f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.16f));
            for (int i = 0; i < movedRects.Count; i++)
            {
                movedRects[i].anchoredPosition = Vector2.Lerp(homes[i] + offset, homes[i], t);
                movedRects[i].localScale = Vector3.Lerp(Vector3.one * 0.94f, Vector3.one, t);
            }
            yield return null;
        }
        for (int i = 0; i < movedRects.Count; i++) { movedRects[i].anchoredPosition = homes[i]; movedRects[i].localScale = Vector3.one; }
        busy = false;
    }

    private bool CanMove(Vector2Int direction)
    {
        Vector2Int gridDirection = new Vector2Int(direction.x, -direction.y);
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
            if (board[r, c] != 0)
            {
                int nr = r + gridDirection.y, nc = c + gridDirection.x;
                if (nr >= 0 && nr < Size && nc >= 0 && nc < Size && (board[nr, nc] == 0 || board[nr, nc] == board[r, c])) return true;
            }
        return false;
    }

    private bool HasMoves() { if (FindEmpty() >= 0) return true; return CanMove(Vector2Int.left) || CanMove(Vector2Int.right) || CanMove(Vector2Int.up) || CanMove(Vector2Int.down); }
    private bool HasDog()
    { for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++) if (board[r, c] >= 512) return true; return false; }

    private int FindEmpty() { for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++) if (board[r, c] == 0) return r * Size + c; return -1; }
    private void AddRandomTile() { List<int> empty = new List<int>(); for (int i = 0; i < Size * Size; i++) if (board[i / Size, i % Size] == 0) empty.Add(i); if (empty.Count == 0) return; int at = empty[UnityEngine.Random.Range(0, empty.Count)]; board[at / Size, at % Size] = UnityEngine.Random.value < 0.9f ? 1 : 2; }

    /// <summary>값 → 타일 스프라이트. lv1 발바닥(1·2·4) lv2 뼈다귀(8·16·32) lv3 육포(64·128·256) lv4 강아지(512+).</summary>
    private Sprite TileSpriteFor(int value)
    {
        if (value >= 512) return tileSprites[9];
        int level = value <= 4 ? 0 : value <= 32 ? 1 : 2;
        int count = value <= 4 ? value : value <= 32 ? value / 8 : value / 64; // 1, 2, 4
        int countIndex = count == 1 ? 0 : count == 2 ? 1 : 2;
        return tileSprites[level * 3 + countIndex];
    }

    private int MaxTile()
    { int max = 0; for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++) if (board[r, c] > max) max = board[r, c]; return max; }

    private static string TileName(int value)
    { return value <= 4 ? "발바닥" : value <= 32 ? "뼈다귀" : value <= 256 ? "육포" : "강아지"; }

    private void UpdateView()
    {
        if (scoreChipText != null) scoreChipText.text = $"점수 {score:N0}";
        if (bestChipText != null) bestChipText.text = $"베스트 {best:N0}";
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
        {
            int value = board[r, c];
            var tile = tileImages[r, c];
            tile.gameObject.SetActive(value != 0);
            if (value != 0) tile.sprite = TileSpriteFor(value);
        }
    }

    private void ShowResult(bool cleared)
    {
        playing = false;
        int maxTile = MaxTile();
        resultTitle.text = cleared ? "강아지 완성!" : "잘 놀았어요!";
        resultSubLine.text = $"최고 타일 · {TileName(maxTile)} {maxTile} · 이동 {moves}회";
        coachText.text = cleared
            ? "단추: 강아지 타일까지 왔어요! 최고예요. 뼈다귀는 제가 잘 챙겨둘게요."
            : "단추: 뼈다귀 냄새가 여기까지 나요! 다음엔 강아지 타일까지 가봐요.";
        SetBones(0);
        resultPanel.SetActive(true);

        // 판이 끝났으니 서버에 점수를 올리고 지급 결과를 반영한다
        Backend.Game2048Bridge.EndRound(score, granted =>
        {
            if (resultBones == null) return;   // 씬을 이미 떠났을 수 있다
            SetBones(granted);
        });
    }

    /// <summary>"+N" 텍스트와 뼈다귀 아이콘을 한 묶음으로 가운데 정렬한다.</summary>
    private void SetBones(int granted)
    {
        resultBones.text = $"+{granted:N0}";
        resultBones.ForceMeshUpdate();
        float textW = resultBones.preferredWidth;
        const float gap = 8f, iconW = 30f;
        resultBones.rectTransform.anchoredPosition = new Vector2(-(gap + iconW) * 0.5f, 13f);
        resultBoneIcon.rectTransform.anchoredPosition = new Vector2((textW + gap) * 0.5f, 13f);
    }

    // ---- UI 헬퍼 ----

    private static Color FromHex(int rgb)
    { return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f); }

    private GameObject MakePanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        return go;
    }

    private Image MakeImage(Transform parent, string name, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, FontStyles style,
        Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = koreanFont;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        SetRect(tmp.rectTransform, pos, sizeDelta);
        return tmp;
    }

    /// <summary>헤더 칩 (9-slice 알약) — 라벨 텍스트를 돌려준다.</summary>
    private TextMeshProUGUI MakeChip(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var img = MakeImage(parent, name, Color.white, chipPill);
        img.type = Image.Type.Sliced;
        SetRect(img.rectTransform, pos, size);
        return MakeText(img.transform, "Label", "", 14, FontStyles.Bold, Cream, Vector2.zero, size);
    }

    /// <summary>3D 아트 버튼 — 그림자 6px가 아트에 새겨져 있어 라벨을 3px 올린다.</summary>
    private Button MakeArtButton(Transform parent, string name, string label, Color fg, Sprite art,
        Vector2 pos, Vector2 sizeDelta, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var img = MakeImage(parent, name, Color.white, art);
        img.raycastTarget = true;
        SetRect(img.rectTransform, pos, sizeDelta);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);
        MakeText(img.transform, "Label", label, fontSize, FontStyles.Bold, fg, new Vector2(0f, 3f), sizeDelta);
        return btn;
    }

    private static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
