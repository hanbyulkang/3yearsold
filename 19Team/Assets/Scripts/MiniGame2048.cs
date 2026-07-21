using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// A complete, self-contained 2048 mini-game UI for the minigame02 scene.
/// Built at runtime so the scene remains easy to edit and the layout is responsive.
/// </summary>
public sealed class MiniGame2048 : MonoBehaviour
{
    private const int Size = 4;
    private const float BoardPadding = 18f;
    private readonly int[,] board = new int[Size, Size];
    private readonly int[,] previousBoard = new int[Size, Size];
    private readonly Image[,] tileImages = new Image[Size, Size];
    private readonly Text[,] tileLabels = new Text[Size, Size];
    private readonly Image[,,] tileIconImages = new Image[Size, Size, 4];
    private readonly Color[] tileColors =
    {
        new Color32(238, 231, 218, 255), new Color32(239, 225, 196, 255),
        new Color32(245, 180, 100, 255), new Color32(241, 139, 82, 255),
        new Color32(236, 105, 75, 255), new Color32(211, 78, 103, 255),
        new Color32(157, 92, 181, 255), new Color32(90, 109, 190, 255),
        new Color32(58, 83, 160, 255), new Color32(42, 57, 117, 255),
        new Color32(34, 42, 90, 255), new Color32(25, 31, 70, 255)
    };

    public Sprite pawSprite;
    public Sprite boneSprite;
    public Sprite jerkySprite;
    public Sprite dogSprite;

    private Transform boardRoot;
    private Text scoreText;
    private Text bestText;
    private Text statusText;
    private GameObject gameOverOverlay;
    private int score;
    private int best;
    private bool hasUndo;
    private bool busy;
    private Vector2 touchStart;
    private Vector2 mouseStart;
    private bool mouseDragging;
    private bool cleared;

    private static readonly Color Ink = new Color32(42, 48, 77, 255);
    private static readonly Color Muted = new Color32(112, 119, 147, 255);
    private static readonly Color Cream = new Color32(255, 248, 237, 255);
    private static readonly Color BoardColor = new Color32(49, 57, 98, 255);
    private static readonly Color Orange = new Color32(244, 126, 66, 255);
    private static readonly Color Purple = new Color32(99, 89, 171, 255);

    private void Start()
    {
        Application.targetFrameRate = 60;
        best = PlayerPrefs.GetInt("MiniGame2048Best", 0);
        BuildInterface();
        // 서버에서 발바닥 잔량을 받아온 뒤 첫 판을 연다 (오프라인이면 추정값으로 진행)
        Backend.Game2048Bridge.Refresh(NewGame);
    }

    private void Update()
    {
        if (busy) return;
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

    private void BuildInterface()
    {
        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("2048 Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.45f;
        }

        RectTransform root = canvas.GetComponent<RectTransform>();
        CreateImage("Background", root, new Color32(241, 244, 252, 255), Vector2.zero, Vector2.one);
        CreateRoundedPanel("TopGlow", root, new Color32(226, 231, 250, 255), new Vector2(0.5f, 0.92f), new Vector2(0.92f, 0.23f), 26f);

        Text eyebrow = CreateText("Eyebrow", root, "PAWS & PUZZLES  /  MINI GAME", 22, Muted, TextAnchor.MiddleLeft);
        SetRect(eyebrow.rectTransform, new Vector2(0.08f, 0.945f), new Vector2(0.92f, 0.98f));
        Text title = CreateText("Title", root, "2048\nPaw Edition", 64, Ink, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        SetRect(title.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.72f, 0.94f));

        Text subtitle = CreateText("Subtitle", root, "작은 한 수가 큰 꼬리 흔들기를 만들어요", 25, Muted, TextAnchor.MiddleLeft);
        SetRect(subtitle.rectTransform, new Vector2(0.08f, 0.785f), new Vector2(0.82f, 0.83f));

        CreateScoreCard(root, "SCORE", new Vector2(0.68f, 0.885f), out scoreText);
        CreateScoreCard(root, "BEST", new Vector2(0.86f, 0.885f), out bestText);

        boardRoot = new GameObject("2048 Board", typeof(RectTransform)).transform;
        boardRoot.SetParent(root, false);
        RectTransform boardRect = boardRoot as RectTransform;
        SetRect(boardRect, new Vector2(0.075f, 0.325f), new Vector2(0.925f, 0.765f));
        CreateRoundedPanel("BoardSurface", boardRoot, BoardColor, new Vector2(0.5f, 0.5f), Vector2.one, 28f);
        BuildBoardSlots(boardRoot);

        Text hint = CreateText("Hint", root, "DRAG THE BOARD TO MOVE", 20, Muted, TextAnchor.MiddleCenter);
        hint.fontStyle = FontStyle.Bold;
        SetRect(hint.rectTransform, new Vector2(0.15f, 0.275f), new Vector2(0.85f, 0.31f));
        statusText = CreateText("Status", root, "합쳐서 더 큰 숫자를 만들어 보세요!", 24, Purple, TextAnchor.MiddleCenter);
        statusText.fontStyle = FontStyle.Bold;
        SetRect(statusText.rectTransform, new Vector2(0.12f, 0.215f), new Vector2(0.88f, 0.27f));

        Button undo = CreateButton(root, "↶  UNDO", new Vector2(0.12f, 0.12f), new Vector2(0.36f, 0.19f), Purple);
        undo.onClick.AddListener(Undo);
        Button hintButton = CreateButton(root, "✦  HINT", new Vector2(0.38f, 0.12f), new Vector2(0.62f, 0.19f), new Color32(75, 91, 143, 255));
        hintButton.onClick.AddListener(ShowHint);
        Button newGame = CreateButton(root, "NEW GAME", new Vector2(0.64f, 0.12f), new Vector2(0.88f, 0.19f), Orange);
        newGame.onClick.AddListener(NewGame);

        Text footer = CreateText("Footer", root, "DRAG THE BOARD  •  MERGE THE PAWS  •  REACH 2048", 17, Muted, TextAnchor.MiddleCenter);
        SetRect(footer.rectTransform, new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.095f));
    }

    private void BuildBoardSlots(Transform parent)
    {
        for (int row = 0; row < Size; row++)
        {
            for (int col = 0; col < Size; col++)
            {
                GameObject slot = CreateRoundedPanel("Slot", parent, new Color32(66, 74, 116, 255), new Vector2(0.125f + col * 0.25f, 0.875f - row * 0.25f), new Vector2(0.205f, 0.205f), 18f);
                Image tile = slot.GetComponent<Image>();
                tileImages[row, col] = tile;
                Text label = CreateText("Tile", slot.transform, "", 58, Ink, TextAnchor.MiddleCenter);
                label.fontStyle = FontStyle.Bold;
                SetRect(label.rectTransform, Vector2.zero, Vector2.one);
                label.gameObject.SetActive(false);
                tileLabels[row, col] = label;
                for (int icon = 0; icon < 4; icon++)
                    tileIconImages[row, col, icon] = CreateTileIcon(slot.transform, icon);
            }
        }
    }

    private Image CreateTileIcon(Transform parent, int index)
    {
        GameObject obj = new GameObject("Icon " + index, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(64f, 64f);
        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        obj.SetActive(false);
        return image;
    }

    private void CreateScoreCard(Transform parent, string caption, Vector2 center, out Text value)
    {
        GameObject card = CreateRoundedPanel(caption + " Card", parent, Cream, center, new Vector2(0.155f, 0.075f), 18f);
        Text cap = CreateText("Caption", card.transform, caption, 16, Muted, TextAnchor.MiddleCenter);
        cap.fontStyle = FontStyle.Bold;
        SetRect(cap.rectTransform, new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.98f));
        value = CreateText("Value", card.transform, "0", 30, Ink, TextAnchor.MiddleCenter);
        value.fontStyle = FontStyle.Bold;
        SetRect(value.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.58f));
    }

    private Button CreateButton(Transform parent, string label, Vector2 min, Vector2 max, Color color)
    {
        GameObject obj = CreateRoundedPanel(label + " Button", parent, color, (min + max) * 0.5f, max - min, 18f);
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
        button.colors = colors;
        Text text = CreateText("Label", obj.transform, label, 21, Color.white, TextAnchor.MiddleCenter);
        text.fontStyle = FontStyle.Bold;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one);
        return button;
    }

    private GameObject CreateRoundedPanel(string name, Transform parent, Color color, Vector2 anchor, Vector2 size, float radius)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero; rect.offsetMin = new Vector2(-size.x * 500f, -size.y * 500f); rect.offsetMax = new Vector2(size.x * 500f, size.y * 500f);
        Image image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = true;
        return obj;
    }

    private Image CreateImage(string name, Transform parent, Color color, Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
        RectTransform r = obj.GetComponent<RectTransform>(); r.anchorMin = min; r.anchorMax = max; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        Image image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }

    private Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>(); text.text = content; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.color = color; text.alignment = anchor; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false; return text;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; rect.pivot = new Vector2(0.5f, 0.5f); }

    private void NewGame()
    {
        if (boardRoot == null) return;
        // 판 시작에 발바닥 1개를 쓴다 (PRD §5.1 — 미니게임 입장 전용)
        if (!Backend.Game2048Bridge.BeginRound())
        {
            statusText.text = "발바닥이 다 떨어졌어요. 잠시 뒤 다시 채워집니다";
            return;
        }
        score = 0; cleared = false; hasUndo = false; Array.Clear(board, 0, board.Length); AddRandomTile(); AddRandomTile(); UpdateView(); statusText.text = $"발바닥 {Backend.Game2048Bridge.Paws}개 남음 · 합쳐서 뼈다귀를 모아보세요!"; if (gameOverOverlay != null) Destroy(gameOverOverlay);
    }

    private void SaveUndo()
    { Array.Copy(board, previousBoard, board.Length); hasUndo = true; }

    private void Undo()
    { if (!hasUndo) { statusText.text = "아직 되돌릴 수가 없어요"; return; } Array.Copy(previousBoard, board, board.Length); hasUndo = false; UpdateView(); statusText.text = "한 수 되돌렸어요"; }

    private void ShowHint()
    { Vector2Int[] dirs = { Vector2Int.left, Vector2Int.up, Vector2Int.right, Vector2Int.down }; foreach (Vector2Int d in dirs) if (CanMove(d)) { statusText.text = "힌트: " + (d == Vector2Int.left ? "왼쪽" : d == Vector2Int.right ? "오른쪽" : d == Vector2Int.up ? "위쪽" : "아래쪽") + "으로 움직여 보세요 ✦"; return; } statusText.text = "새 게임이 필요해요"; }

    private void Move(Vector2Int direction)
    {
        if (busy) return;
        if (!CanMove(direction)) { statusText.text = "그 방향으로는 움직일 수 없어요"; return; }
        SaveUndo();
        bool[,] merged = new bool[Size, Size];
        bool horizontal = direction.x != 0;
        int outerStart = direction.y > 0 || direction.x > 0 ? Size - 1 : 0;
        int outerEnd = direction.y > 0 || direction.x > 0 ? -1 : Size;
        int outerStep = direction.y > 0 || direction.x > 0 ? -1 : 1;
        for (int outer = outerStart; outer != outerEnd; outer += outerStep)
        {
            int innerStart = direction.y > 0 || direction.x > 0 ? Size - 1 : 0;
            int innerEnd = direction.y > 0 || direction.x > 0 ? -1 : Size;
            int innerStep = direction.y > 0 || direction.x > 0 ? -1 : 1;
            for (int inner = innerStart; inner != innerEnd; inner += innerStep)
            {
                int row = horizontal ? outer : inner, col = horizontal ? inner : outer; int value = board[row, col]; if (value == 0) continue;
                int r = row, c = col;
                while (true)
                {
                    int nr = r + direction.y, nc = c + direction.x; if (nr < 0 || nr >= Size || nc < 0 || nc >= Size || (board[nr, nc] != 0 && board[nr, nc] != value)) break;
                    if (board[nr, nc] == value && !merged[nr, nc]) { board[nr, nc] *= 2; score += board[nr, nc]; merged[nr, nc] = true; board[r, c] = 0; break; }
                    if (board[nr, nc] == 0) { board[nr, nc] = board[r, c]; board[r, c] = 0; r = nr; c = nc; } else break;
                }
            }
        }
        if (score > best) { best = score; PlayerPrefs.SetInt("MiniGame2048Best", best); }
        AddRandomTile(); UpdateView(); StartCoroutine(AnimateSlide(direction)); statusText.text = HasDog() ? "강아지 완성! CLEAR! 🎉" : "좋아요! 같은 펫 타일을 드래그해서 합쳐 보세요";
        if (HasDog()) { cleared = true; ShowClear(); }
        else if (!HasMoves()) ShowGameOver();
    }

    private IEnumerator AnimateSlide(Vector2Int direction)
    {
        busy = true;
        Vector2 offset = new Vector2(-direction.x * 70f, -direction.y * 70f);
        List<RectTransform> moved = new List<RectTransform>();
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
        {
            if (board[r, c] == 0) continue;
            RectTransform rect = tileImages[r, c].rectTransform;
            rect.anchoredPosition = offset;
            moved.Add(rect);
            rect.localScale = Vector3.one * 0.94f;
        }
        float elapsed = 0f;
        while (elapsed < 0.16f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.16f));
            foreach (RectTransform rect in moved)
            {
                rect.anchoredPosition = Vector2.Lerp(offset, Vector2.zero, t);
                rect.localScale = Vector3.Lerp(Vector3.one * 0.94f, Vector3.one, t);
            }
            yield return null;
        }
        foreach (RectTransform rect in moved) { rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one; }
        busy = false;
    }

    private bool CanMove(Vector2Int direction)
    {
        int[,] copy = new int[Size, Size]; Array.Copy(board, copy, board.Length); bool moved = false;
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++) if (board[r, c] != 0) { int nr = r + direction.y, nc = c + direction.x; if (nr >= 0 && nr < Size && nc >= 0 && nc < Size && (board[nr, nc] == 0 || board[nr, nc] == board[r, c])) moved = true; }
        Array.Copy(copy, board, board.Length); return moved;
    }

    private bool HasMoves() { if (FindEmpty() >= 0) return true; return CanMove(Vector2Int.left) || CanMove(Vector2Int.right) || CanMove(Vector2Int.up) || CanMove(Vector2Int.down); }
    private bool HasDog()
    { for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++) if (board[r, c] >= 512) return true; return false; }

    private int FindEmpty() { for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++) if (board[r, c] == 0) return r * Size + c; return -1; }
    private void AddRandomTile() { List<int> empty = new List<int>(); for (int i = 0; i < Size * Size; i++) if (board[i / Size, i % Size] == 0) empty.Add(i); if (empty.Count == 0) return; int at = empty[UnityEngine.Random.Range(0, empty.Count)]; board[at / Size, at % Size] = UnityEngine.Random.value < 0.9f ? 1 : 2; }

    private Sprite SpriteFor(int value)
    { return value <= 4 ? pawSprite : value <= 32 ? boneSprite : value <= 256 ? jerkySprite : dogSprite; }

    private int CountFor(int value)
    { return value <= 4 ? value : value <= 32 ? value / 8 : value <= 256 ? value / 64 : 1; }

    private void UpdateView()
    {
        if (scoreText != null) scoreText.text = score.ToString();
        if (bestText != null) bestText.text = best.ToString();
        for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
        {
            int value = board[r, c];
            int level = value <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(value, 2)) - 1, 0, tileColors.Length - 1);
            Sprite sprite = value == 0 ? null : SpriteFor(value);
            int count = value == 0 ? 0 : CountFor(value);
            tileImages[r, c].sprite = null;
            tileImages[r, c].preserveAspect = false;
            tileImages[r, c].color = value == 0 ? new Color32(66, 74, 116, 255) : new Color32(255, 255, 255, 20);
            tileLabels[r, c].text = "";
            for (int icon = 0; icon < 4; icon++)
            {
                Image image = tileIconImages[r, c, icon];
                bool visible = icon < count;
                image.gameObject.SetActive(visible);
                image.sprite = sprite;
                image.color = Color.white;
                RectTransform iconRect = image.rectTransform;
                if (count <= 1) iconRect.anchoredPosition = Vector2.zero;
                else if (count == 2) iconRect.anchoredPosition = new Vector2(icon == 0 ? -28f : 28f, 0f);
                else iconRect.anchoredPosition = icon == 0 ? new Vector2(-25f, -25f) : icon == 1 ? new Vector2(25f, -25f) : icon == 2 ? new Vector2(-25f, 25f) : new Vector2(25f, 25f);
            }
        }
    }

    /// <summary>판이 끝나면 서버에 점수를 올리고 지급 결과를 표시한다.</summary>
    private void GrantBones()
    {
        Backend.Game2048Bridge.EndRound(score, granted =>
        {
            if (statusText == null) return;   // 씬을 이미 떠났을 수 있다
            statusText.text = granted > 0
                ? $"뼈다귀 {granted}개를 받았어요!"
                : "이번 판은 뼈다귀를 모으지 못했어요";
        });
    }

    private void ShowClear()
    {
        if (gameOverOverlay != null) return;
        GrantBones();
        Transform root = boardRoot.parent;
        gameOverOverlay = new GameObject("Clear Overlay", typeof(RectTransform), typeof(Image));
        gameOverOverlay.transform.SetParent(root, false);
        RectTransform rect = gameOverOverlay.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.075f, 0.325f), new Vector2(0.925f, 0.765f));
        Image image = gameOverOverlay.GetComponent<Image>(); image.color = new Color32(49, 57, 98, 238);
        Text title = CreateText("Title", gameOverOverlay.transform, "CLEAR!", 64, Color.white, TextAnchor.MiddleCenter); title.fontStyle = FontStyle.Bold; SetRect(title.rectTransform, new Vector2(0.1f, 0.56f), new Vector2(0.9f, 0.75f));
        Text sub = CreateText("Sub", gameOverOverlay.transform, "강아지를 완성했어요! 🎉", 28, Cream, TextAnchor.MiddleCenter); SetRect(sub.rectTransform, new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.53f));
        Button retry = CreateButton(gameOverOverlay.transform, "PLAY AGAIN", new Vector2(0.27f, 0.18f), new Vector2(0.73f, 0.32f), Orange); retry.onClick.AddListener(NewGame);
    }

    private void ShowGameOver()
    {
        if (gameOverOverlay != null) return; GrantBones(); Transform root = boardRoot.parent; gameOverOverlay = new GameObject("Game Over", typeof(RectTransform), typeof(Image)); gameOverOverlay.transform.SetParent(root, false); RectTransform rect = gameOverOverlay.GetComponent<RectTransform>(); SetRect(rect, new Vector2(0.075f, 0.325f), new Vector2(0.925f, 0.765f)); Image image = gameOverOverlay.GetComponent<Image>(); image.color = new Color32(35, 42, 83, 235); Text title = CreateText("Title", gameOverOverlay.transform, "NO MORE MOVES", 46, Color.white, TextAnchor.MiddleCenter); title.fontStyle = FontStyle.Bold; SetRect(title.rectTransform, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.72f)); Text sub = CreateText("Sub", gameOverOverlay.transform, "한 번 더 도전해 볼까요?", 26, Cream, TextAnchor.MiddleCenter); SetRect(sub.rectTransform, new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.52f)); Button retry = CreateButton(gameOverOverlay.transform, "PLAY AGAIN", new Vector2(0.27f, 0.18f), new Vector2(0.73f, 0.32f), Orange); retry.onClick.AddListener(NewGame); }
}
