using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        [SerializeField] Sprite[] blockArts = new Sprite[6]; // bowl, bone, ball, chew, leash, squeaky
        [SerializeField] Sprite rocketHArt;
        [SerializeField] Sprite rocketVArt;
        [SerializeField] Sprite bombArt;
        [SerializeField] Sprite magicArt;
        [SerializeField] Sprite brandFrameArt;

        [Header("결과 화면 아트 (Assets/UI/MiniGame1/Ending — minigame-ending 디자인)")]
        [SerializeField] Sprite endMedal;
        [SerializeField] Sprite endSunburst;
        [SerializeField] Sprite endGlow;
        [SerializeField] Sprite endCard;
        [SerializeField] Sprite endBubble;
        [SerializeField] Sprite endAvatar;
        [SerializeField] Sprite endBtnGold;
        [SerializeField] Sprite endBtnGoldPressed;
        [SerializeField] Sprite endBtnDark;
        [SerializeField] Sprite endBtnGhost;
        [SerializeField] Sprite endBanner;

        [Header("사운드 — 팝 3종은 연쇄 단계별 (Audio/pop_line1~3.wav)")]
        [SerializeField] AudioClip swapClip;
        [SerializeField] AudioClip popClip1;   // 1연쇄 (한 줄)
        [SerializeField] AudioClip popClip2;   // 2연쇄 (두 줄)
        [SerializeField] AudioClip popClip3;   // 3연쇄 이상
        [SerializeField] AudioClip specialClip;
        [SerializeField] AudioClip feverClip;
        [SerializeField] AudioClip resultClip;
        [SerializeField] AudioClip bgmClip; // match-3.wav 도착 시 여기 연결

        [Header("미션 연동")]
        [SerializeField] MissionDataSet missionDataSet;
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
        float _timeLeft;
        float _playDuration = MG1Config.PlayTime;
        int _goalCollected;
        bool _goalDone;

        float _brandTimer;
        BrandConfig[] _brands = System.Array.Empty<BrandConfig>();
        int _brandRotation;
        BrandConfig _activeBrand;  // 지금 보드에 나와 있는(나올) 브랜드
        BrandConfig _seenBrand;    // 이번 판에서 노출된 마지막 브랜드
        int _brandBlocksPopped;    // 브랜드 블록 보너스 집계

        // UI refs
        GameObject _entryPanel, _playPanel, _resultPanel;
        TextMeshProUGUI _pawText, _entryNotice;
        Button _startButton;
        TextMeshProUGUI _movesText, _scoreText, _goalText, _comboText, _coachText;
        Slider _timeSlider;
        Image _timeFillImage;
        GameObject _coachRow;
        Image _feverFill;
        TextMeshProUGUI _feverLabel;
        CanvasGroup _boardGroup;
        RectTransform _boardRoot;
        TextMeshProUGUI _resultTitle, _resultSub, _boneBig, _boneTotal, _resultCoach;
        RectTransform _resultSunburst, _resultMedal;

        // 홈 씬 연결 지점 — 미니게임은 재화 루프(§4.2)까지만 책임진다
        public static event System.Action<int> GameFinished;   // 지급된 뼈다귀
        public static event System.Action GoHomeRequested;
        Button _retryButton;
        Sprite _brandLogoSprite;

        // minigame-ending 디자인 팔레트
        static readonly Color TitleInk = new Color(0.353f, 0.275f, 0.196f);   // #5A4632
        static readonly Color SubInk = new Color(0.541f, 0.478f, 0.384f);     // #8A7A62
        static readonly Color GoldInk = new Color(0.722f, 0.463f, 0.165f);    // #B8762A
        static readonly Color DarkInk = new Color(0.416f, 0.345f, 0.247f);    // #6A583F
        static readonly Color GoldBtnInk = new Color(0.29f, 0.192f, 0.075f);  // #4A3113
        static readonly Color DarkBtnInk = new Color(1f, 0.937f, 0.824f);     // #FFEFD2

        static readonly Color Cream = new Color(0.98f, 0.953f, 0.902f);
        static readonly Color Ink = new Color(0.29f, 0.2f, 0.153f);        // #4A3327 (목표 디자인 브라운)
        static readonly Color Gold = new Color(0.949f, 0.702f, 0.298f);    // #F2B34C
        static readonly Color Accent = new Color(0.894f, 0.357f, 0.31f);
        static readonly Color CardBg = Color.white;

        void Awake()
        {
            // 서버 연동 (PRD §5.5) — 발바닥 차감·지급이 Supabase에 기록된다.
            // 오프라인이면 ServerRewardClient가 조용히 로컬 추정값으로 동작하므로
            // 데모가 네트워크에 인질 잡히지 않는다. 순수 로컬 개발은 아래 목업으로 교체:
            //   _reward = new LocalMockRewardClient();
            _reward = new Backend.ServerRewardClient();
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
            // 입장 화면 없이 바로 한 판 시작한다. 발바닥은 이미 허브의
            // 플레이 버튼에서 차감·예약됐다 — 여기서 한 번 더 묻는 건 같은 확인의 반복이다.
            StartGame();
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
            _timeLeft = Mathf.Max(0f, _timeLeft - Time.deltaTime);
            if (_timeLeft <= 0f)
            {
                EndBecauseTimeExpired();
                return;
            }

            _brandTimer += Time.deltaTime;
            float interval = demoMode ? 5f : 8f;
            if (_brandTimer >= interval && !_model.HasBrandOnBoard() && _brands.Length > 0)
            {
                // 브랜드 3종 중 랜덤으로 드롭 — 로고 블록이 아이템 사이에 수시로 등장
                _activeBrand = _brands[Random.Range(0, _brands.Length)];
                _seenBrand = _activeBrand; // 노출만으로 홍보 성립 — 수집 개념 없음
                _board.SetBrandLogo(_activeBrand.logo);
                _model.QueueBrandDrop(); // §3.1: 동시 최대 1개
                _brandTimer = 0f;
            }

            RefreshPlayHud();
        }

        // ---- 상태 전환 ----

        /// <summary>
        /// 발바닥이 없어 판을 열지 못했을 때만 보이는 화면.
        ///
        /// 정상 경로에서는 허브가 이미 발바닥을 차감하므로 여기까지 오지 않는다.
        /// 씬을 직접 실행했거나 예약이 유실된 경우의 막다른 길을 막는 역할이다.
        /// </summary>
        void ShowNoPaw()
        {
            _state = State.Entry;
            _entryPanel.SetActive(true);
            _playPanel.SetActive(false);
            _resultPanel.SetActive(false);
            _pawText.text = $"발바닥 {GameCurrencyStore.GetPaws()} / {MG1Config.MaxPaws}";
            _entryNotice.text = "발바닥이 부족해요 — 시간이 지나면 회복돼요";
        }

        void StartGame()
        {
            // 발바닥은 두 곳에서 센다 — 허브가 미리 차감한 로컬 예약, 그리고 서버 paw_state.
            bool alreadyPaidAtEntry = GameCurrencyStore.ConsumeEntryReservation();
            if (!alreadyPaidAtEntry && !GameCurrencyStore.TrySpendPaw()) { ShowNoPaw(); return; }

            // 서버 세션을 여는 유일한 지점이다. 이걸 건너뛰면 game-submit이
            // 붙을 세션이 없어 이번 판 뼈다귀가 원장에 기록되지 않는다.
            // 실패해도 판은 진행한다 — 오프라인이 데모를 막지 않게 (§5.5 주석과 같은 태도).
            if (!_reward.TrySpendPaw())
                Debug.LogWarning("[MG1] 서버 발바닥 부족 — 판은 진행하되 지급은 서버가 거부합니다");

            // 실제 한 판이 시작된 시점에 미니게임 미션 진행도를 저장한다.
            // 마을 씬으로 돌아오면 MissionUIController가 같은 데이터셋의
            // PlayerPrefs 진행도를 읽어 완료 UI와 Count를 즉시 갱신한다.
            if (missionDataSet != null)
            {
                missionDataSet.LoadSavedState();
                missionDataSet.AddProgress(MissionAction.MiniGame);
            }

            _model = new BoardModel(MG1Config.BoardSize, MG1Config.NormalTypes, new System.Random());
            _score.Reset();
            _brandTimer = (demoMode ? 5f : 8f) - 3f; // 시작 후 ~3초면 첫 브랜드 블록 등장
            _activeBrand = null;
            _seenBrand = null;
            _brandBlocksPopped = 0;
            _movesLeft = demoMode ? DemoMoves : MovesPerGame;
            _playDuration = demoMode ? MG1Config.DemoPlayTime : MG1Config.PlayTime;
            _timeLeft = _playDuration;
            _goalCollected = 0;
            _goalDone = false;

            _entryPanel.SetActive(false);
            _resultPanel.SetActive(false);
            _playPanel.SetActive(true);
            _boardGroup.interactable = true;
            _boardGroup.blocksRaycasts = true;
            _comboText.text = "";
            SetCoach(""); // 시작 멘트 없음 — 진행 상황이 생기면 코치가 나타난다
            if (_feverLabel != null) _feverLabel.gameObject.SetActive(false);

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
            if (step.BrandBlocks > 0 && _activeBrand != null) _seenBrand = _activeBrand;
            _brandBlocksPopped += step.BrandBlocks;
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

        void EndBecauseTimeExpired()
        {
            if (_state != State.Playing) return;
            _timeLeft = 0f;
            _state = State.Ending;
            _boardGroup.interactable = false;
            _boardGroup.blocksRaycasts = false;
            RefreshPlayHud();
            if (_board == null || !_board.IsResolving) ShowResult();
        }

        void ShowResult()
        {
            if (_state == State.Result) return;
            _state = State.Result;
            // 모은 뼈다귀 블록이 그대로 재화가 된다 (목표 달성 시 ×2, 브랜드 블록 보너스)
            int earned = _goalCollected * MG1Config.BonePerBlock;
            if (_goalDone) earned *= MG1Config.ClearMultiplier;
            earned += _brandBlocksPopped * MG1Config.BrandBoneBonus;
            int granted = _reward.GrantBones(earned);

            _playPanel.SetActive(false);
            _resultPanel.SetActive(true);
            PlaySfx(resultClip, 0.9f);
            StartCoroutine(SpinSunburst());

            // 클리어!/결과 집계 분기 (실패 연출 없음, §1.2 원칙 2)
            _resultTitle.text = _goalDone ? "클리어!" : "결과 집계";
            _resultSub.text = $"점수 {_score.Score:N0} · 이동 {Mathf.Max(0, _movesLeft)}회 남김 · 뼈다귀 {Mathf.Min(_goalCollected, GoalTargetCount)}/{GoalTargetCount}";

            string capNote = granted < earned ? " · 오늘 상한 도달" : "";
            _boneTotal.text = $"보유 뼈다귀 {_reward.GetTotalBones():N0}{capNote}";

            // 캐릭터견(단추) 코멘트 — 기부 연결은 CTA가 아니라 이 한 줄이 담당한다
            _resultCoach.text = _goalDone
                ? "(신나서 폴짝폴짝) 이 뼈다귀로 보호소 친구들 사료를 채워줄 수 있대요!"
                : "오늘도 수고했어요! 모은 뼈다귀는 보호소 친구들에게 보탬이 돼요.";

            int paws = GameCurrencyStore.GetPaws();
            _retryButton.interactable = paws > 0;
            _retryButton.GetComponentInChildren<TextMeshProUGUI>().text =
                paws > 0 ? $"한 번 더 (발바닥 {paws})" : "발바닥이 부족해요";

            StartCoroutine(ResultSequence(granted));
            PlayerLevelStore.AddExperience(30);
            GameFinished?.Invoke(granted); // 홈 씬이 재화 HUD를 갱신하는 지점
        }

        // 보상이 주인공인 연출: 메달 팝인 → 뼈다귀 카운트업 → 축하 불꽃
        IEnumerator ResultSequence(int granted)
        {
            if (_resultMedal != null)
            {
                for (float t = 0; t < 0.32f; t += Time.deltaTime)
                {
                    float k = Mathf.Sin((t / 0.32f) * Mathf.PI * 0.5f);
                    _resultMedal.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.06f, k);
                    yield return null;
                }
                _resultMedal.localScale = Vector3.one;
            }

            _boneBig.text = "+0";
            yield return new WaitForSeconds(0.12f);

            const float dur = 0.7f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                _boneBig.text = $"+{Mathf.RoundToInt(Mathf.Lerp(0, granted, t / dur)):N0}";
                yield return null;
            }
            _boneBig.text = $"+{granted:N0}";

            // 숫자가 다 오른 뒤에 터뜨려야 "얻었다"는 순간이 살아난다
            if (fireworksFxPrefab != null)
            {
                Vector3 pos = _resultPanel.transform.position;
                var cam = Camera.main;
                if (cam != null) pos += (cam.transform.position - pos).normalized * 2f;
                var fx = Instantiate(fireworksFxPrefab, pos, Quaternion.identity);
                fx.transform.localScale = Vector3.one * 1.2f;
                var effect = fx.GetComponent<CartoonFX.CFXR_Effect>();
                if (effect?.cameraShake != null) effect.cameraShake.shakeStrength *= 0.25f;
                Destroy(fx, 4f);
            }
        }

        void RefreshPlayHud()
        {
            int displayedSeconds = Mathf.CeilToInt(_timeLeft);
            _movesText.text = displayedSeconds.ToString();
            if (_timeSlider != null)
            {
                _timeSlider.maxValue = Mathf.Max(1f, _playDuration);
                _timeSlider.SetValueWithoutNotify(displayedSeconds);
            }
            if (_timeFillImage != null)
                _timeFillImage.fillAmount = displayedSeconds / Mathf.Max(1f, _playDuration);
            _scoreText.text = $"{_score.Score:N0}";
            _goalText.text = $"{Mathf.Min(_goalCollected, GoalTargetCount)}<color=#4A3327>/{GoalTargetCount}</color>";
            _feverFill.fillAmount = _score.FeverGauge / MG1Config.FeverMax;
            // 피버 중에는 게이지가 노랑 → 빨강으로 변하고 "피버!" 라벨이 뜬다
            _feverFill.color = _score.FeverActive ? new Color(1f, 0.32f, 0.26f) : Color.white;
            if (_feverLabel != null && _feverLabel.gameObject.activeSelf != _score.FeverActive)
                _feverLabel.gameObject.SetActive(_score.FeverActive);
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
            // 시작 화면의 구성과 간격은 유지한 채 전체 묶음을 아래로 이동한다.
            ((RectTransform)_entryPanel.transform).anchoredPosition = new Vector2(0f, -70f);

            MakeIcon(_entryPanel.transform, "DogFace", dogFace, new Vector2(0, 290), 130f);
            MakeText(_entryPanel.transform, "Title", "3매치", 36, FontStyles.Bold, Ink,
                new Vector2(0, 200), new Vector2(340, 60));
            MakeText(_entryPanel.transform, "Subtitle", "지금은 한 판을 열 수 없어요", 16, FontStyles.Normal, Ink,
                new Vector2(0, 155), new Vector2(360, 30));

            var pawCard = MakeCard(_entryPanel.transform, "PawCard", CardBg, new Vector2(0, 80), new Vector2(240, 70));
            MakeIcon(pawCard.transform, "PawIcon", pawIcon, new Vector2(-80, 0), 44f);
            _pawText = MakeText(pawCard.transform, "PawText", "", 22, FontStyles.Bold, Ink,
                new Vector2(20, 0), new Vector2(180, 40));

            _entryNotice = MakeText(_entryPanel.transform, "Notice", "", 15, FontStyles.Normal, Ink,
                new Vector2(0, 20), new Vector2(340, 30));

            // 발바닥 부족 화면 전용이므로 나갈 길만 둔다. 여기서 "시작하기"를
            // 눌러도 발바닥이 없어 다시 이 화면으로 돌아올 뿐이다.
            _startButton = MakeArtButton(_entryPanel.transform, "StartButton", "마을로 돌아가기", Color.white,
                new Vector2(0, -70), new Vector2(260, 64), btnPrimary, btnPrimaryPressed, ReturnToVillage);

        }

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
            closeBtn.onClick.AddListener(ReturnToVillage);

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
            MakeText(movesChip.transform, "Label", "시간", 13, FontStyles.Normal, Ink, new Vector2(-42 + 10 + 15, 0), new Vector2(30, 30)).alignment = TextAlignmentOptions.Left;
            _movesText = MakeText(movesChip.transform, "Value", "60", 17, FontStyles.Bold, Gold, new Vector2(42 - 10 - 15, 0), new Vector2(30, 30));
            _movesText.alignment = TextAlignmentOptions.Right;
            var timeBar = MakeImage(movesChip.transform, "TimeSlider", new Color(0.18f, 0.13f, 0.1f, 0.28f), null);
            MG1Skin.ApplyRounded(timeBar, 8f);
            SetRect(timeBar.rectTransform, new Vector2(0, -14), new Vector2(64, 5));
            var timeFill = MakeImage(timeBar.transform, "Fill", Gold, null);
            MG1Skin.ApplyRounded(timeFill, 8f);
            Stretch(timeFill.rectTransform);
            timeFill.type = Image.Type.Filled;
            timeFill.fillMethod = Image.FillMethod.Horizontal;
            timeFill.fillOrigin = 0;
            timeFill.fillAmount = 1f;
            _timeFillImage = timeFill;
            timeFill.raycastTarget = false;
            _timeSlider = timeBar.gameObject.AddComponent<Slider>();
            _timeSlider.transition = Selectable.Transition.None;
            _timeSlider.interactable = false;
            _timeSlider.direction = Slider.Direction.LeftToRight;
            _timeSlider.fillRect = null;
            _timeSlider.targetGraphic = timeBar;
            _timeSlider.minValue = 0f;
            _timeSlider.maxValue = MG1Config.PlayTime;
            _timeSlider.SetValueWithoutNotify(MG1Config.PlayTime);

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
            // 피버 라벨은 피버 중에만 보인다 (평소엔 게이지만)
            _feverLabel = MakeText(_playPanel.transform, "FeverLabel", "피버!", 13, FontStyles.Bold, Accent,
                new Vector2(Half - 26, 262), new Vector2(52, 26));
            _feverLabel.alignment = TextAlignmentOptions.Right;
            _feverLabel.gameObject.SetActive(false);

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


            // 피버 타임 대각선 리본 배너 (우측 상단, 빨간 배경)
            // 화면 안에 완전히 들어오는 크기·각도 (392px 폭 기준 잘림 없음)
            // 피버 표시는 별도 뱃지 없이 게이지 바 색 전환으로 (노랑 → 빨강)
        }

        void BuildResultPanel(Transform parent)
        {
            _resultPanel = MakePanel(parent, "ResultPanel");

            // ── minigame-ending 디자인. 보상이 주인공이므로 CTA는 2개만 둔다.
            var glow = MakeImage(_resultPanel.transform, "Glow", Color.white, endGlow);
            SetRect(glow.rectTransform, new Vector2(0, 252), new Vector2(440, 440));

            var sun = MakeImage(_resultPanel.transform, "Sunburst", Color.white, endSunburst);
            SetRect(sun.rectTransform, new Vector2(0, 250), new Vector2(172, 172));
            _resultSunburst = sun.rectTransform;

            var medal = MakeImage(_resultPanel.transform, "Medal", Color.white, endMedal);
            medal.preserveAspect = true;
            SetRect(medal.rectTransform, new Vector2(0, 250), new Vector2(112, 118));
            _resultMedal = medal.rectTransform;

            _resultTitle = MakeText(_resultPanel.transform, "Title", "클리어!", 38, FontStyles.Bold, TitleInk,
                new Vector2(0, 160), new Vector2(340, 50));
            _resultSub = MakeText(_resultPanel.transform, "Sub", "", 13, FontStyles.Normal, SubInk,
                new Vector2(0, 124), new Vector2(360, 24));

            // 보상 카드 — 화면의 주인공
            var card = MakeImage(_resultPanel.transform, "BoneCard", Color.white, endCard);
            SetRect(card.rectTransform, new Vector2(0, 44), new Vector2(320, 116));
            MakeIcon(card.transform, "BoneIcon",
                blockArts != null && blockArts.Length > 1 && blockArts[1] != null ? blockArts[1] : boneSprite,
                new Vector2(-66, 18), 40f);
            _boneBig = MakeText(card.transform, "BoneBig", "+0", 36, FontStyles.Bold, GoldInk,
                new Vector2(22, 18), new Vector2(220, 50));
            _boneTotal = MakeText(card.transform, "BoneTotal", "", 12, FontStyles.Normal, SubInk,
                new Vector2(0, -28), new Vector2(300, 22));

            // 캐릭터견(단추) 말풍선 — 기부 연결은 CTA 대신 이 한 줄로
            var avatar = MakeImage(_resultPanel.transform, "CoachAvatar", Color.white, endAvatar);
            SetRect(avatar.rectTransform, new Vector2(-160, -52), new Vector2(52, 52));
            MakeIcon(avatar.transform, "Face", dogFace, Vector2.zero, 38f);

            var bubble = MakeImage(_resultPanel.transform, "CoachBubble", Color.white, endBubble);
            SetRect(bubble.rectTransform, new Vector2(24, -70), new Vector2(252, 84));
            var cap = MakeText(bubble.transform, "Cap", "단추", 12, FontStyles.Bold, GoldInk,
                new Vector2(-90, 24), new Vector2(60, 18));
            cap.alignment = TextAlignmentOptions.Left;
            _resultCoach = MakeText(bubble.transform, "Msg", "", 12f, FontStyles.Normal, DarkInk,
                new Vector2(8, -10), new Vector2(196, 46));
            _resultCoach.alignment = TextAlignmentOptions.TopLeft;

            // CTA 2개 — 한 번 더(주) / 홈으로(보조)
            _retryButton = MakeEndingButton(_resultPanel.transform, "RetryBtn", "한 번 더", GoldBtnInk,
                new Vector2(0, -206), new Vector2(320, 58), endBtnGold, endBtnGoldPressed, StartGame);
            _retryButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 19;

            var homeBtn = MakeEndingButton(_resultPanel.transform, "HomeBtn", "홈으로", SubInk,
                new Vector2(0, -278), new Vector2(320, 52), endBtnGhost, null, null);
            homeBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 17;
            homeBtn.onClick.AddListener(() =>
            {
                GoHomeRequested?.Invoke(); // 홈 씬이 구독. 미구독이면 아래 폴백
                ReturnToVillage();
            });
        }

        void ReturnToVillage()
        {
            SceneManager.LoadScene("Suntail Village");
        }

        // 결과 화면 전용 버튼 — 아트를 원본 비율 그대로(Simple) 쓴다
        Button MakeEndingButton(Transform parent, string name, string label, Color fg,
            Vector2 pos, Vector2 size, Sprite art, Sprite pressed, UnityEngine.Events.UnityAction onClick)
        {
            Image img;
            if (art != null)
            {
                img = MakeImage(parent, name, Color.white, art);
            }
            else
            {
                MakeShadow(parent, pos, size);
                img = MakeImage(parent, name, Accent, null);
                MG1Skin.ApplyRounded(img, 5f);
            }
            img.raycastTarget = true;
            SetRect(img.rectTransform, pos, size);
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
            // 3D 버튼이라 라벨을 살짝 위로 (아래쪽 그림자 두께 보정)
            MakeText(img.transform, "Label", label, 19, FontStyles.Bold, fg, new Vector2(0, 3f), size);
            return btn;
        }

        IEnumerator SpinSunburst()
        {
            while (_resultPanel != null && _resultPanel.activeSelf)
            {
                if (_resultSunburst != null)
                    _resultSunburst.localRotation = Quaternion.Euler(0, 0, -Time.time * 15f);
                yield return null;
            }
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
