using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniGame1
{
    // MG1 상태 머신 + UI 구성. 목표 디자인(Desktop 레퍼런스) 기준:
    // 이동(스왑) 제한 + 목표 블록 수집 방식, 상단 헤더 칩(이동·점수), 하단 강아지 코치.
    // 씬에는 이 컴포넌트 하나만 두고 UI는 전부 코드로 만든다.
    public class MG1GameManager : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] BrandConfig brandConfig;      // 단일 폴백 (구버전 호환)
        [SerializeField] BrandConfig[] brandConfigs;   // 시즌 로테이션 3종 — 드롭마다 순환
        [SerializeField] bool demoMode; // 데모: 이동 수 축소 + 브랜드 블록 조기 드롭

        [Header("공용 에셋 참조")]
        [SerializeField] Sprite roundSprite;     // UIKit/rounded_512 (9-slice)
        [SerializeField] Sprite shadowSprite;    // UIKit/shadow_radial
        [SerializeField] Sprite clockIcon;       // UIKit/icon_clock (미사용 대기)
        [SerializeField] Sprite starIcon;        // UIKit/icon_star
        [SerializeField] Sprite pawIcon;         // Assets/UI/제목 없는 디자인-4
        [SerializeField] Sprite dogFace;         // Assets/UI/5
        [SerializeField] Sprite trophyIcon;      // Assets/UI/12
        [SerializeField] Sprite homeIcon;        // Assets/UI/15
        [SerializeField] Sprite boneSprite;      // Assets/UI/22 (폴백용)
        [SerializeField] GameObject popFxPrefab;       // CFXR3 Hit Magical Stars (Rainbow)
        [SerializeField] GameObject fireworksFxPrefab; // CFXR3 Hit Light Fireworks

        [Header("전용 아트 (Assets/UI/MiniGame1/Art)")]
        [SerializeField] Sprite bgMain;
        [SerializeField] Sprite boardBgArt;
        [SerializeField] Sprite btnPrimary;
        [SerializeField] Sprite btnPrimaryPressed;
        [SerializeField] Sprite btnSecondary;
        [SerializeField] Sprite feverBarBg;
        [SerializeField] Sprite feverBarFill;
        [SerializeField] Sprite pawArt;
        [SerializeField] Sprite couponArt;
        [SerializeField] Sprite[] blockArts = new Sprite[6]; // bowl, bone, ball, chew, leash, squeaky
        [SerializeField] Sprite rocketHArt;
        [SerializeField] Sprite rocketVArt;
        [SerializeField] Sprite bombArt;
        [SerializeField] Sprite magicArt;
        [SerializeField] Sprite brandFrameArt;

        [Header("사운드 — 팝 3종은 연쇄 단계별 (Audio/pop_line1~3.wav)")]
        [SerializeField] AudioClip swapClip;
        [SerializeField] AudioClip popClip1;   // 1연쇄 (한 줄)
        [SerializeField] AudioClip popClip2;   // 2연쇄 (두 줄)
        [SerializeField] AudioClip popClip3;   // 3연쇄 이상
        [SerializeField] AudioClip specialClip;
        [SerializeField] AudioClip feverClip;
        [SerializeField] AudioClip resultClip;
        [SerializeField] AudioClip bgmClip; // match-3.wav 도착 시 여기 연결
        AudioSource _audio;
        AudioSource _bgm;

        enum State { Entry, Playing, Ending, Result }
        State _state;

        IRewardClient _reward;
        BoardModel _model;
        BoardView _board;
        ScoreSystem _score;

        // 이동·수집 목표 (목표 디자인 기준)
        const int GoalBlockType = 1;          // 뼈다귀
        const int GoalTargetCount = 20;
        const int MovesPerGame = 20;
        const int DemoMoves = 10;
        int _movesLeft;
        int _goalCollected;
        bool _goalDone;

        float _brandTimer;
        int _brandPopped;
        BrandConfig[] _brands = System.Array.Empty<BrandConfig>();
        int _brandRotation;
        BrandConfig _activeBrand;  // 지금 보드에 나와 있는(나올) 브랜드
        BrandConfig _poppedBrand;  // 이번 판에서 마지막으로 터뜨린 브랜드 (쿠폰 대상)

        // UI refs
        GameObject _entryPanel, _playPanel, _resultPanel;
        TextMeshProUGUI _pawText, _entryNotice;
        Button _startButton;
        TextMeshProUGUI _movesText, _scoreText, _goalText, _comboText, _coachText;
        GameObject _coachRow;
        RectTransform _brandChipsRow;
        int _brandChipCount;
        const float ContentHalf = 181f;
        Image _feverFill;
        CanvasGroup _boardGroup;
        RectTransform _boardRoot;
        TextMeshProUGUI _resultTitle, _resultScore, _resultPoints, _resultGoal, _couponTitle, _donationText;
        GameObject _couponCard;
        Button _couponButton, _retryButton;
        Image _couponLogo;
        TextMeshProUGUI _couponLabel;
        Sprite _brandLogoSprite;

        static readonly Color Cream = new Color(0.98f, 0.953f, 0.902f);
        static readonly Color Ink = new Color(0.29f, 0.2f, 0.153f);        // #4A3327 (목표 디자인 브라운)
        static readonly Color Gold = new Color(0.949f, 0.702f, 0.298f);    // #F2B34C
        static readonly Color Accent = new Color(0.894f, 0.357f, 0.31f);
        static readonly Color CardBg = Color.white;

        void Awake()
        {
            _reward = new LocalMockRewardClient(); // DEMO-MOCK (§6.3)
            _score = new ScoreSystem();
            _brands = brandConfigs != null && brandConfigs.Length > 0
                ? brandConfigs
                : (brandConfig != null ? new[] { brandConfig } : System.Array.Empty<BrandConfig>());
            if (_brands.Length > 0) _brandLogoSprite = _brands[0].logo;

            MG1Skin.Round = roundSprite;
            MG1Skin.Shadow = shadowSprite;
            MG1Skin.Bone = boneSprite;
            MG1Skin.Paw = pawIcon;
            MG1Skin.BlockArts = blockArts;
            MG1Skin.RocketHArt = rocketHArt;
            MG1Skin.RocketVArt = rocketVArt;
            MG1Skin.BombArt = bombArt;
            MG1Skin.MagicArt = magicArt;
            MG1Skin.BrandFrameArt = brandFrameArt;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            // 비어 있는 클립만 합성 플레이스홀더로 채운다
            if (swapClip == null) swapClip = MG1AudioSynth.Swap();
            if (popClip1 == null) popClip1 = MG1AudioSynth.Pop();
            if (popClip2 == null) popClip2 = popClip1;
            if (popClip3 == null) popClip3 = popClip2;
            if (specialClip == null) specialClip = MG1AudioSynth.Special();
            if (feverClip == null) feverClip = MG1AudioSynth.Fever();
            if (resultClip == null) resultClip = MG1AudioSynth.Result();
            _score.FeverStarted += () => PlaySfx(feverClip, 0.9f);

            // BGM: 루프 재생 (게임 전체 상태 공통)
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.clip = bgmClip != null ? bgmClip : MG1AudioSynth.Bgm();
            _bgm.loop = true;
            _bgm.volume = 0.55f;
            _bgm.playOnAwake = false;
            _bgm.Play();

            BuildUI();
            ShowEntry();
        }

        void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, volume);
        }

        void Update()
        {
            // BGM 워치독 — 어떤 이유로든 멈춰 있으면 다시 재생
            if (_bgm != null && _bgm.clip != null && !_bgm.isPlaying) _bgm.Play();

            if (_state != State.Playing) return;
            if (_score == null || _model == null) return; // 에디터 도메인 리로드 등으로 상태가 날아간 경우 방어

            _score.Tick(Time.deltaTime);

            _brandTimer += Time.deltaTime;
            float interval = demoMode ? 5f : 8f;
            if (_brandTimer >= interval && !_model.HasBrandOnBoard() && _brands.Length > 0)
            {
                // 브랜드 3종 중 랜덤으로 드롭 — 로고 블록이 아이템 사이에 수시로 등장
                _activeBrand = _brands[Random.Range(0, _brands.Length)];
                _board.SetBrandLogo(_activeBrand.logo);
                _model.QueueBrandDrop(); // §3.1: 동시 최대 1개
                _brandTimer = 0f;
            }

            RefreshPlayHud();
        }

        // ---- 상태 전환 ----

        void ShowEntry()
        {
            _state = State.Entry;
            _entryPanel.SetActive(true);
            _playPanel.SetActive(false);
            _resultPanel.SetActive(false);
            int paws = _reward.GetPaws();
            _pawText.text = $"발바닥 {paws} / {MG1Config.MaxPaws}";
            bool canPlay = paws > 0;
            _startButton.interactable = canPlay;
            _entryNotice.text = canPlay
                ? "입장하면 발바닥 1개를 사용해요"
                : "발바닥이 부족해요 — 시간이 지나면 회복돼요"; // DEMO-MOCK: 회복 미구현
        }

        void StartGame()
        {
            if (!_reward.TrySpendPaw()) { ShowEntry(); return; }

            _model = new BoardModel(MG1Config.BoardSize, MG1Config.NormalTypes, new System.Random());
            _score.Reset();
            _brandTimer = (demoMode ? 5f : 8f) - 3f; // 시작 후 ~3초면 첫 브랜드 블록 등장
            _brandPopped = 0;
            _activeBrand = null;
            _poppedBrand = null;
            _movesLeft = demoMode ? DemoMoves : MovesPerGame;
            _goalCollected = 0;
            _goalDone = false;

            _entryPanel.SetActive(false);
            _resultPanel.SetActive(false);
            _playPanel.SetActive(true);
            _boardGroup.interactable = true;
            _boardGroup.blocksRaycasts = true;
            _comboText.text = "";
            SetCoach(""); // 시작 멘트 없음 — 진행 상황이 생기면 코치가 나타난다
            if (_brandChipsRow != null)
            {
                for (int i = _brandChipsRow.childCount - 1; i >= 0; i--)
                    Destroy(_brandChipsRow.GetChild(i).gameObject);
                _brandChipCount = 0;
            }

            float cell = _boardRoot.rect.width / MG1Config.BoardSize;
            _board.PopFxPrefab = popFxPrefab;
            _board.Build(_model, _boardRoot, cell, _brandLogoSprite);
            _state = State.Playing;
            RefreshPlayHud();
        }

        void OnMoveCommitted()
        {
            if (_state != State.Playing) return;
            _movesLeft--;
            PlaySfx(swapClip, 0.7f);
            RefreshPlayHud();
        }

        void OnCascadeStep(CascadeStep step, int cascadeIndex)
        {
            if (_state != State.Playing && _state != State.Ending) return;
            int bonus = _activeBrand != null ? _activeBrand.bonusScore : 300;
            _score.AddCascadeStep(step.MatchedBlocks, step.SpecialBlocks, step.BrandBlocks, bonus, cascadeIndex);
            _brandPopped += step.BrandBlocks;
            if (step.BrandBlocks > 0 && _activeBrand != null) _poppedBrand = _activeBrand;
            // 폭발음: 1·2연쇄는 같은 소리(2번 wav), 3연쇄 이상만 3번
            PlaySfx(cascadeIndex <= 1 ? popClip2 : popClip3, 0.85f);
            if (step.SpecialBlocks > 0 || step.BrandBlocks > 0) PlaySfx(specialClip, 0.6f);

            foreach (int code in step.ClearedCodes)
                if (code == GoalBlockType) _goalCollected++;

            if (cascadeIndex >= 1)
            {
                _comboText.text = $"{cascadeIndex + 1} 연쇄!";
                StopCoroutine(nameof(FadeCombo));
                StartCoroutine(nameof(FadeCombo));
            }
            // 브랜드 블록 획득은 토스트 대신 하단 누적 칩으로
            for (int i = 0; i < step.BrandBlocks; i++) AddBrandChip(_poppedBrand);

            if (!_goalDone && _goalCollected >= GoalTargetCount)
            {
                _goalDone = true;
                SetCoach("해냈어! 최고야!");
            }
            else
            {
                int remain = Mathf.Max(0, GoalTargetCount - _goalCollected);
                if (remain <= 6 && remain > 0) SetCoach($"좋아! 뼈다귀 {remain}개만 더 모으면 돼!");
            }
            RefreshPlayHud();
        }

        IEnumerator FadeCombo()
        {
            yield return new WaitForSeconds(1.1f);
            _comboText.text = "";
        }

        // 하단 브랜드 획득 누적 칩 (로고 미니 카드)
        void AddBrandChip(BrandConfig b)
        {
            if (_brandChipsRow == null || b == null) return;
            var chip = MakeImage(_brandChipsRow, "Chip" + _brandChipCount, Color.white, null);
            MG1Skin.ApplyRounded(chip, 8f);
            SetRect(chip.rectTransform, new Vector2(-ContentHalf + 32 + _brandChipCount * 46, 0), new Vector2(42, 42));
            var logo = MakeImage(chip.transform, "Logo", Color.white, b.logo);
            logo.preserveAspect = true;
            SetRect(logo.rectTransform, Vector2.zero, new Vector2(36, 22));
            StartCoroutine(PopChip(chip.rectTransform));
            _brandChipCount++;
        }

        IEnumerator PopChip(RectTransform rt)
        {
            for (float t = 0; t < 0.2f; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                rt.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, t / 0.2f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        void OnResolveFinished()
        {
            if (_state == State.Ending) { ShowResult(); return; }
            if (_state != State.Playing) return;

            // 목표 달성 또는 이동 소진 → 종료 (연쇄가 끝난 시점에만 판정)
            if (_goalDone || _movesLeft <= 0)
            {
                _state = State.Ending;
                _boardGroup.interactable = false;
                _boardGroup.blocksRaycasts = false;
                if (!_board.IsResolving) ShowResult();
            }
        }

        void ShowResult()
        {
            _state = State.Result;
            int granted = _reward.GrantPointsForScore(_score.Score);
            int raw = _score.Score / MG1Config.PointsDivisor;

            _playPanel.SetActive(false);
            _resultPanel.SetActive(true);
            PlaySfx(resultClip, 0.9f);

            if (fireworksFxPrefab != null)
            {
                // 결과 축하 1회 — 점수와 무관하게 항상 (§1.2 원칙 2: 실패 연출 없음)
                Vector3 pos = _resultPanel.transform.position;
                var cam = Camera.main;
                if (cam != null) pos += (cam.transform.position - pos).normalized * 2f;
                var fx = Instantiate(fireworksFxPrefab, pos, Quaternion.identity);
                fx.transform.localScale = Vector3.one * 1.2f;
                Destroy(fx, 4f);
            }

            _resultTitle.text = _goalDone ? "목표 달성!" : "결과 집계";
            _resultGoal.text = $"뼈다귀 {Mathf.Min(_goalCollected, GoalTargetCount)}/{GoalTargetCount} 수집";
            _resultScore.text = $"{_score.Score:N0}점";
            _resultPoints.text = granted < raw
                ? $"+{granted} 포인트 (오늘 상한 {MG1Config.DailyPointCap} 도달)"
                : $"+{granted} 포인트";

            bool coupon = _brandPopped > 0 && _poppedBrand != null;
            _couponCard.SetActive(coupon);
            if (coupon)
            {
                _couponTitle.text = $"{_poppedBrand.brandName} {_poppedBrand.productName} 쿠폰";
                if (_couponLabel != null) _couponLabel.text = $"({_poppedBrand.partnershipLabel})";
                if (_couponLogo != null && _poppedBrand.logo != null) _couponLogo.sprite = _poppedBrand.logo;
                _couponButton.interactable = true;
                _couponButton.GetComponentInChildren<TextMeshProUGUI>().text = "쿠폰 받기";
            }

            var donationBrand = _poppedBrand != null ? _poppedBrand : (_brands.Length > 0 ? _brands[0] : null);
            string brand = donationBrand != null ? donationBrand.brandName : "브랜드";
            string label = donationBrand != null ? donationBrand.partnershipLabel : "모의 협업";
            // §3.3: 판매 연동 기부 게이지 — 정보 노출 1줄, 수치는 DEMO-MOCK
            _donationText.text = $"[{label}] {brand} 장난감 342/500개\n목표 달성 시 보호소에 장난감이 전달돼요";

            int paws = _reward.GetPaws();
            _retryButton.interactable = paws > 0;
            _retryButton.GetComponentInChildren<TextMeshProUGUI>().text =
                paws > 0 ? $"다시 하기 (발바닥 {paws})" : "발바닥이 부족해요";
        }

        void RefreshPlayHud()
        {
            _movesText.text = _movesLeft.ToString();
            _scoreText.text = $"{_score.Score:N0}";
            _goalText.text = $"{Mathf.Min(_goalCollected, GoalTargetCount)}<color=#4A3327>/{GoalTargetCount}</color>";
            _feverFill.fillAmount = _score.FeverGauge / MG1Config.FeverMax;
            // 피버 중에는 게이지가 노랑 → 빨강으로 변한다 (별도 뱃지 없음)
            _feverFill.color = _score.FeverActive ? new Color(1f, 0.32f, 0.26f) : Color.white;
        }

        void SetCoach(string msg)
        {
            if (_coachText == null) return;
            _coachText.text = msg;
            if (_coachRow != null) _coachRow.SetActive(!string.IsNullOrEmpty(msg));
        }

        // ---- UI 구성 ----

        void BuildUI()
        {
            if (koreanFont == null) koreanFont = TMP_Settings.defaultFontAsset;

            var canvasGo = new GameObject("MG1Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            // 파티클(CFX)이 UI 위에 보이도록 카메라 방식 사용
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.transform.position = new Vector3(0, 0, -10f);
                cam.transform.rotation = Quaternion.identity;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
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

            var bg = MakeImage(canvasGo.transform, "Background", bgMain != null ? Color.white : Cream, bgMain);
            Stretch(bg.rectTransform);

            BuildEntryPanel(canvasGo.transform);
            BuildPlayPanel(canvasGo.transform);
            BuildResultPanel(canvasGo.transform);
        }

        void BuildEntryPanel(Transform parent)
        {
            _entryPanel = MakePanel(parent, "EntryPanel");

            MakeIcon(_entryPanel.transform, "DogFace", dogFace, new Vector2(0, 330), 130f);
            MakeText(_entryPanel.transform, "Title", "3매치", 36, FontStyles.Bold, Ink,
                new Vector2(0, 240), new Vector2(340, 60));
            MakeText(_entryPanel.transform, "Subtitle", $"이동 {MovesPerGame}번 안에 뼈다귀 {GoalTargetCount}개를 모아보세요!", 16, FontStyles.Normal, Ink,
                new Vector2(0, 190), new Vector2(360, 30));

            var pawCard = MakeCard(_entryPanel.transform, "PawCard", CardBg, new Vector2(0, 80), new Vector2(240, 70));
            MakeIcon(pawCard.transform, "PawIcon", pawIcon, new Vector2(-80, 0), 44f);
            _pawText = MakeText(pawCard.transform, "PawText", "", 22, FontStyles.Bold, Ink,
                new Vector2(20, 0), new Vector2(180, 40));

            _entryNotice = MakeText(_entryPanel.transform, "Notice", "", 15, FontStyles.Normal, Ink,
                new Vector2(0, 20), new Vector2(340, 30));

            _startButton = MakeArtButton(_entryPanel.transform, "StartButton", "시작하기", Color.white,
                new Vector2(0, -70), new Vector2(260, 64), btnPrimary, btnPrimaryPressed, StartGame);

            var demoBtn = MakeArtButton(_entryPanel.transform, "DemoToggle", DemoLabel(), Ink,
                new Vector2(0, -150), new Vector2(220, 44), btnSecondary, null, null);
            demoBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14;
            demoBtn.onClick.AddListener(() =>
            {
                demoMode = !demoMode;
                demoBtn.GetComponentInChildren<TextMeshProUGUI>().text = DemoLabel();
            });

            // DEMO-MOCK: 테스트·무대 시연용 즉시 충전 (정식: 시간 회복·육포 충전)
            var refillBtn = MakeArtButton(_entryPanel.transform, "RefillBtn", "발바닥 충전 (테스트용)", Ink,
                new Vector2(0, -210), new Vector2(220, 44), btnSecondary, null, null);
            refillBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14;
            refillBtn.onClick.AddListener(() =>
            {
                if (_reward is LocalMockRewardClient mock) mock.RefillPaws();
                ShowEntry();
            });
        }

        string DemoLabel() => demoMode ? $"데모 모드 켜짐 (이동 {DemoMoves})" : $"데모 모드 꺼짐 (이동 {MovesPerGame})";

        void BuildPlayPanel(Transform parent)
        {
            _playPanel = MakePanel(parent, "PlayPanel");

            // 공통 그리드: 콘텐츠 폭 362 (좌우 여백 15.5), 카드 좌우 내부 패딩 14
            const float W = 362f;
            const float Half = W / 2f;   // 181
            const float Pad = 14f;

            // 종료 버튼: 헤더 밖 독립 배치. 흰 라운드 + 브라운 X (일반적인 매치3 닫기 버튼 스타일)
            MakeShadow(_playPanel.transform, new Vector2(-Half + 22, 322), new Vector2(44, 44));
            var closeImg = MakeImage(_playPanel.transform, "CloseBtn", Color.white, null);
            MG1Skin.ApplyRounded(closeImg, 5f);
            closeImg.raycastTarget = true;
            SetRect(closeImg.rectTransform, new Vector2(-Half + 22, 322), new Vector2(44, 44));
            var closeBtn = closeImg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            MakeText(closeImg.transform, "Label", "X", 20, FontStyles.Bold, new Color(0.29f, 0.2f, 0.153f, 0.75f),
                Vector2.zero, new Vector2(44, 44));
            closeBtn.onClick.AddListener(ShowEntry);

            // 상단 헤더 (다크 브라운 바): 타이틀 · 이동/점수 칩 — X 제외한 나머지 폭
            float headerW = W - 44f - 8f;           // 310
            float headerHalf = headerW / 2f;        // 155
            var header = MakeCard(_playPanel.transform, "Header", Ink,
                new Vector2(-Half + 44f + 8f + headerHalf, 322), new Vector2(headerW, 62), 4f);
            var headerTitle = MakeText(header.transform, "Title", "3매치", 21, FontStyles.Bold, Color.white,
                new Vector2(-headerHalf + Pad + 45, 0), new Vector2(90, 40));
            headerTitle.alignment = TextAlignmentOptions.Left;

            // 칩 2개: 오른쪽 끝에서부터 역산 배치, 칩 내부는 좌라벨·우값
            var scoreChip = MakeChip(header.transform, "ScoreChip", new Vector2(headerHalf - Pad - 50, 0), new Vector2(100, 36));
            MakeText(scoreChip.transform, "Label", "점수", 13, FontStyles.Normal, Ink, new Vector2(-50 + 10 + 15, 0), new Vector2(30, 30)).alignment = TextAlignmentOptions.Left;
            _scoreText = MakeText(scoreChip.transform, "Value", "0", 15, FontStyles.Bold, Gold, new Vector2(50 - 10 - 27, 0), new Vector2(54, 30));
            _scoreText.alignment = TextAlignmentOptions.Right;

            var movesChip = MakeChip(header.transform, "MovesChip", new Vector2(headerHalf - Pad - 100 - 8 - 42, 0), new Vector2(84, 36));
            MakeText(movesChip.transform, "Label", "이동", 13, FontStyles.Normal, Ink, new Vector2(-42 + 10 + 15, 0), new Vector2(30, 30)).alignment = TextAlignmentOptions.Left;
            _movesText = MakeText(movesChip.transform, "Value", "20", 17, FontStyles.Bold, Gold, new Vector2(42 - 10 - 15, 0), new Vector2(30, 30));
            _movesText.alignment = TextAlignmentOptions.Right;

            // 목표 바: 보드 아래 하단 배치 (라벨 일반 굵기, 카운트만 강조)
            var goalCard = MakeCard(_playPanel.transform, "GoalCard", CardBg, new Vector2(0, -230), new Vector2(W, 50), 4f);
            MakeIcon(goalCard.transform, "GoalIcon", blockArts != null && blockArts.Length > 1 ? blockArts[1] : boneSprite,
                new Vector2(-Half + Pad + 17, 0), 34f);
            var goalLabel = MakeText(goalCard.transform, "GoalLabel", "뼈다귀 모으기", 15, FontStyles.Normal, Ink,
                new Vector2(-Half + Pad + 34 + 8 + 105, 0), new Vector2(210, 36));
            goalLabel.alignment = TextAlignmentOptions.Left;
            _goalText = MakeText(goalCard.transform, "GoalCount", "0/20", 17, FontStyles.Bold, Accent,
                new Vector2(Half - Pad - 35, 0), new Vector2(70, 36));
            _goalText.alignment = TextAlignmentOptions.Right;

            // 피버 게이지: 발바닥(좌) · 바(중) · FEVER(우), 겹침 없이
            MakeIcon(_playPanel.transform, "FeverPaw", pawArt != null ? pawArt : pawIcon, new Vector2(-Half + 15, 262), 30f);
            var feverBg = MakeImage(_playPanel.transform, "FeverBg", Color.white, feverBarBg);
            if (feverBarBg == null) { MG1Skin.ApplyRounded(feverBg, 10f); feverBg.color = new Color(0, 0, 0, 0.1f); }
            SetRect(feverBg.rectTransform, new Vector2(-8, 262), new Vector2(266, 26));
            _feverFill = MakeImage(feverBg.transform, "FeverFill", Color.white, feverBarFill);
            if (feverBarFill == null) { _feverFill.sprite = MG1Sprites.RoundedRect(64, 20); _feverFill.color = Gold; }
            Stretch(_feverFill.rectTransform);
            _feverFill.type = Image.Type.Filled;
            _feverFill.fillMethod = Image.FillMethod.Horizontal;
            _feverFill.fillAmount = 0f;
            var feverLabel = MakeText(_playPanel.transform, "FeverLabel", "FEVER", 12, FontStyles.Bold, Ink,
                new Vector2(Half - 28, 262), new Vector2(52, 26));
            feverLabel.alignment = TextAlignmentOptions.Right;

            // 보드
            var boardBg = MakeImage(_playPanel.transform, "BoardBg", Color.white, boardBgArt);
            if (boardBgArt == null) { MG1Skin.ApplyRounded(boardBg, 3f); boardBg.color = new Color(0.42f, 0.36f, 0.29f, 0.2f); }
            SetRect(boardBg.rectTransform, new Vector2(0, 10), new Vector2(378, 378));
            _boardGroup = boardBg.gameObject.AddComponent<CanvasGroup>();
            boardBg.raycastTarget = true;

            var boardRootGo = new GameObject("BoardRoot", typeof(RectTransform));
            boardRootGo.transform.SetParent(boardBg.transform, false);
            _boardRoot = (RectTransform)boardRootGo.transform;
            SetRect(_boardRoot, Vector2.zero, new Vector2(356, 356));

            _board = boardRootGo.AddComponent<BoardView>();
            _board.CascadeStepResolved += OnCascadeStep;
            _board.ResolveFinished += OnResolveFinished;
            _board.MoveCommitted += OnMoveCommitted;

            // 연쇄 텍스트는 보드 위에 떠서 나온다
            _comboText = MakeText(_playPanel.transform, "Combo", "", 26, FontStyles.Bold, Accent,
                new Vector2(0, 10), new Vector2(340, 44));

            // 하단 강아지 코치: 아바타(좌) + 말풍선(우). 멘트가 없으면 통째로 숨긴다
            _coachRow = new GameObject("CoachRow", typeof(RectTransform));
            _coachRow.transform.SetParent(_playPanel.transform, false);
            Stretch((RectTransform)_coachRow.transform);
            var avatarCard = MakeCard(_coachRow.transform, "CoachAvatar", CardBg, new Vector2(-Half + 33, -320), new Vector2(66, 66), 2.5f);
            MakeIcon(avatarCard.transform, "Face", dogFace, Vector2.zero, 52f);
            var bubble = MakeCard(_coachRow.transform, "CoachBubble", CardBg, new Vector2(-Half + 66 + 10 + 143, -320), new Vector2(286, 58), 5f);
            _coachText = MakeText(bubble.transform, "Msg", "", 14, FontStyles.Bold, Ink,
                Vector2.zero, new Vector2(262, 48));
            _coachRow.SetActive(false);
            // 브랜드 설명 캡션은 제거 — "모의 협업" 라벨은 쿠폰 카드에서 표기 (§3.4)

            // 하단 브랜드 획득 누적 칩 줄 (목표 바 아래)
            var chipsGo = new GameObject("BrandChips", typeof(RectTransform));
            chipsGo.transform.SetParent(_playPanel.transform, false);
            _brandChipsRow = (RectTransform)chipsGo.transform;
            SetRect(_brandChipsRow, new Vector2(0, -283), new Vector2(W, 44));

            // 피버 타임 대각선 리본 배너 (우측 상단, 빨간 배경)
            // 화면 안에 완전히 들어오는 크기·각도 (392px 폭 기준 잘림 없음)
            // 피버 표시는 별도 뱃지 없이 게이지 바 색 전환으로 (노랑 → 빨강)
        }

        void BuildResultPanel(Transform parent)
        {
            _resultPanel = MakePanel(parent, "ResultPanel");

            var card = MakeCard(_resultPanel.transform, "Card", CardBg, new Vector2(0, 110), new Vector2(340, 320));

            MakeIcon(card.transform, "Trophy", trophyIcon, new Vector2(0, 178), 84f);
            _resultTitle = MakeText(card.transform, "Title", "결과 집계", 22, FontStyles.Bold, Ink,
                new Vector2(0, 112), new Vector2(300, 36));
            _resultGoal = MakeText(card.transform, "Goal", "", 16, FontStyles.Normal, Ink,
                new Vector2(0, 76), new Vector2(300, 30));
            _resultScore = MakeText(card.transform, "Score", "0점", 40, FontStyles.Bold, Ink,
                new Vector2(0, 24), new Vector2(300, 60));
            _resultPoints = MakeText(card.transform, "Points", "", 20, FontStyles.Bold, Accent,
                new Vector2(0, -28), new Vector2(300, 34));
            // §1.2 원칙 2: 낮은 점수여도 실패 연출 없음 — 항상 획득만 보여준다
            _donationText = MakeText(card.transform, "Donation", "", 13, FontStyles.Normal,
                new Color(0.29f, 0.2f, 0.153f, 0.7f), new Vector2(0, -105), new Vector2(300, 60));

            // 조건부 토글 요소라 그림자까지 홀더로 묶는다
            var couponHolder = new GameObject("CouponCard", typeof(RectTransform));
            couponHolder.transform.SetParent(_resultPanel.transform, false);
            SetRect((RectTransform)couponHolder.transform, new Vector2(0, -125), new Vector2(340, 130));
            var couponBg = MakeImage(couponHolder.transform, "Bg", Color.white, couponArt);
            if (couponArt == null)
            {
                MakeShadow(couponHolder.transform, Vector2.zero, new Vector2(340, 130));
                couponBg.color = new Color(1f, 0.96f, 0.88f);
                MG1Skin.ApplyRounded(couponBg, 5f);
            }
            Stretch(couponBg.rectTransform);
            _couponCard = couponHolder;

            // 쿠폰 내부: 좌열(로고+라벨) · 우열(제목+버튼), 열 경계 -20
            if (_brandLogoSprite != null)
            {
                _couponLogo = MakeImage(_couponCard.transform, "Logo", Color.white, _brandLogoSprite);
                _couponLogo.preserveAspect = true;
                SetRect(_couponLogo.rectTransform, new Vector2(-95, 22), new Vector2(120, 34));
            }
            _couponLabel = MakeText(_couponCard.transform, "CouponLabel", "(모의 협업)", 11, FontStyles.Normal,
                new Color(0.29f, 0.2f, 0.153f, 0.55f), new Vector2(-95, -30), new Vector2(120, 22));
            _couponTitle = MakeText(_couponCard.transform, "CouponTitle", "", 15, FontStyles.Bold, Ink,
                new Vector2(70, 22), new Vector2(175, 50));
            _couponTitle.alignment = TextAlignmentOptions.Left;
            _couponButton = MakeArtButton(_couponCard.transform, "CouponBtn", "쿠폰 받기", Color.white,
                new Vector2(90, -30), new Vector2(130, 40), btnPrimary, btnPrimaryPressed, null);
            _couponButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15;
            _couponButton.onClick.AddListener(() =>
            {
                _reward.SaveCoupon(brandConfig != null ? brandConfig.brandName : "브랜드");
                _couponButton.interactable = false;
                _couponButton.GetComponentInChildren<TextMeshProUGUI>().text = "받았어요!";
            });

            _retryButton = MakeArtButton(_resultPanel.transform, "RetryBtn", "다시 하기", Color.white,
                new Vector2(0, -245), new Vector2(260, 58), btnPrimary, btnPrimaryPressed, StartGame);
            var homeBtn = MakeArtButton(_resultPanel.transform, "HomeBtn", "마당으로", Ink,
                new Vector2(0, -315), new Vector2(260, 50), btnSecondary, null, null);
            homeBtn.onClick.AddListener(ShowEntry); // 홈 씬 연결은 인터페이스만 노출 (§5)
        }

        // ---- UI 헬퍼 ----

        GameObject MakePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            return go;
        }

        Image MakeImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, FontStyles style,
            Color color, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = koreanFont;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            SetRect(tmp.rectTransform, pos, sizeDelta);
            return tmp;
        }

        // 전용 아트 버튼 (아트 없으면 라운드 폴백). pressed 스프라이트가 있으면 SpriteSwap.
        Button MakeArtButton(Transform parent, string name, string label, Color fg,
            Vector2 pos, Vector2 sizeDelta, Sprite art, Sprite pressed, UnityEngine.Events.UnityAction onClick)
        {
            Image img;
            if (art != null)
            {
                img = MakeImage(parent, name, Color.white, art);
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2f;
            }
            else
            {
                MakeShadow(parent, pos, sizeDelta);
                img = MakeImage(parent, name, Accent, null);
                MG1Skin.ApplyRounded(img, 5f);
            }
            img.raycastTarget = true;
            SetRect(img.rectTransform, pos, sizeDelta);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            if (pressed != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var ss = btn.spriteState;
                ss.pressedSprite = pressed;
                ss.disabledSprite = art;
                btn.spriteState = ss;
            }
            if (onClick != null) btn.onClick.AddListener(onClick);
            MakeText(img.transform, "Label", label, 19, FontStyles.Bold, fg, Vector2.zero, sizeDelta);
            return btn;
        }

        Image MakeChip(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var img = MakeImage(parent, name, Cream, null);
            MG1Skin.ApplyRounded(img, 8f);
            SetRect(img.rectTransform, pos, size);
            return img;
        }

        // 그림자 + 9-slice 라운드 카드
        Image MakeCard(Transform parent, string name, Color color, Vector2 pos, Vector2 size, float corner = 5f)
        {
            MakeShadow(parent, pos, size);
            var img = MakeImage(parent, name, color, null);
            MG1Skin.ApplyRounded(img, corner);
            SetRect(img.rectTransform, pos, size);
            return img;
        }

        void MakeShadow(Transform parent, Vector2 pos, Vector2 size)
        {
            if (MG1Skin.Shadow == null) return;
            var sh = MakeImage(parent, "Shadow", new Color(0f, 0f, 0f, 0.35f), MG1Skin.Shadow);
            SetRect(sh.rectTransform, pos + new Vector2(0, -6f), size + new Vector2(28f, 28f));
        }

        Image MakeIcon(Transform parent, string name, Sprite sprite, Vector2 pos, float size)
        {
            if (sprite == null) return null;
            var img = MakeImage(parent, name, Color.white, sprite);
            img.preserveAspect = true;
            SetRect(img.rectTransform, pos, new Vector2(size, size));
            return img;
        }

        static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
