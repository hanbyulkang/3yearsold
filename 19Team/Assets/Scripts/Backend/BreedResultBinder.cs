using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Backend
{
    /// <summary>API 추천 3종 표시와 Dog → Mind → Meet 온보딩 흐름.</summary>
    public sealed class BreedResultBinder : MonoBehaviour
    {
        [SerializeField] private Transform _content;
        [SerializeField] private Button _selectButton;

        readonly List<GameObject> _cards = new List<GameObject>();
        GameObject _template;
        GameObject _dogRoot;
        GameObject _mindRoot;
        GameObject _meetRoot;
        Button _mindNext;
        TMP_InputField _nameInput;
        TMP_InputField _meetName;
        TMP_Text _meetSummary;
        GameObject _meetDog;
        SliderBinding _timid;
        SliderBinding _activity;
        SliderBinding _affection;
        int _selected = -1;
        OnboardingApi.AnalysisResult _result;
        string _boundAnalysisId;
        bool _creating;

        sealed class SliderBinding
        {
            public SurveyValueSlider slider;
            public int Value => slider != null ? slider.Value : 0;

            public void Set(int value)
            {
                if (slider == null) return;
                slider.SetValue(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= AttachToSurvey;
            SceneManager.sceneLoaded += AttachToSurvey;
        }

        static void AttachToSurvey(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.Equals("Survey", StringComparison.OrdinalIgnoreCase)) return;
            Transform dog = FindInScene(scene, "Dog");
            if (dog == null || dog.GetComponent<BreedResultBinder>() != null) return;
            dog.gameObject.AddComponent<BreedResultBinder>();
        }

        void Awake()
        {
            _dogRoot = gameObject;
            Transform canvas = GetComponentInParent<Canvas>(true)?.transform;
            _mindRoot = FindDeep(canvas, "Mind")?.gameObject;
            _meetRoot = FindDeep(canvas, "Meet")?.gameObject;
            _meetDog = FindWorldDog(gameObject.scene, _dogRoot);
            _content = _content != null ? _content : FindDeep(transform, "Content");
            _selectButton = _selectButton != null ? _selectButton : EnsureButton(FindDeep(transform, "Next"));

            if (_mindRoot != null)
            {
                _mindNext = EnsureButton(FindDeep(_mindRoot.transform, "Next"));
                _nameInput = _mindRoot.GetComponentInChildren<TMP_InputField>(true);
                _timid = BuildSlider(_mindRoot.transform, "겁 많음");
                _activity = BuildSlider(_mindRoot.transform, "활동성");
                _affection = BuildSlider(_mindRoot.transform, "사람 좋아함");
            }
            if (_meetRoot != null)
            {
                _meetName = _meetRoot.GetComponentInChildren<TMP_InputField>(true);
                foreach (TMP_Text text in _meetRoot.GetComponentsInChildren<TMP_Text>(true))
                    if (text.text.Contains("{0}")) { _meetSummary = text; break; }
            }

            if (_selectButton != null) _selectButton.onClick.AddListener(ShowMind);
            if (_mindNext != null) _mindNext.onClick.AddListener(CreateAndShowMeet);
        }

        void OnEnable()
        {
            SurveyOpenAIAnalysis.AnalysisReady += Bind;
            if (SurveyOpenAIAnalysis.Latest != null) Bind(SurveyOpenAIAnalysis.Latest);
        }

        void OnDisable() => SurveyOpenAIAnalysis.AnalysisReady -= Bind;

        void OnDestroy()
        {
            if (_selectButton != null) _selectButton.onClick.RemoveListener(ShowMind);
            if (_mindNext != null) _mindNext.onClick.RemoveListener(CreateAndShowMeet);
        }

        public void BindResult(OnboardingApi.AnalysisResult result) => Bind(result);

        void Bind(OnboardingApi.AnalysisResult result)
        {
            if (result?.breeds == null || result.breeds.Length == 0) return;
            if (_boundAnalysisId == result.analysisId && _cards.Count > 0) return;
            _boundAnalysisId = result.analysisId;
            _result = result;
            if (_content == null) { Debug.LogWarning("[Survey Dog] Content를 찾지 못했습니다.", this); return; }

            if (_template == null)
            {
                _template = FindDeep(_content, "Frame")?.gameObject;
                if (_template == null) { Debug.LogWarning("[Survey Dog] Frame 템플릿을 찾지 못했습니다.", this); return; }
                _template.SetActive(false);
            }

            foreach (GameObject card in _cards) if (card != null) Destroy(card);
            _cards.Clear();
            _selected = -1;

            int recommendationCount = Mathf.Min(3, result.breeds.Length);
            for (int i = 0; i < recommendationCount; i++)
            {
                OnboardingApi.BreedPick breed = result.breeds[i];
                GameObject card = Instantiate(_template, _content);
                card.name = $"Frame_{i + 1}_{breed.name}";
                card.SetActive(true);

                SetText(card.transform, "Name", breed.name);
                SetText(card.transform, "Description", BreedDescription(breed));
                Transform ai = FindDeep(card.transform, "AI");
                if (ai != null) SetText(ai, "Description", RecommendationReason(breed));

                Transform imageSlot = FindDeepestExact(card.transform, "Image");
                if (imageSlot != null && !string.IsNullOrWhiteSpace(breed.imageUrl))
                    RemoteImage.Load(breed.imageUrl, imageSlot as RectTransform);

                int index = i;
                Button button = card.GetComponent<Button>() ?? card.AddComponent<Button>();
                if (button.targetGraphic == null) button.targetGraphic = card.GetComponent<Graphic>();
                button.onClick.AddListener(() => Select(index));
                Outline outline = card.GetComponent<Outline>() ?? card.AddComponent<Outline>();
                outline.effectColor = new Color32(231, 151, 66, 255);
                outline.effectDistance = new Vector2(4f, -4f);
                outline.enabled = false;
                _cards.Add(card);
            }

            Select(0);
            Debug.Log($"[Survey Dog] API 추천 견종 {_cards.Count}개를 표시했습니다.", this);
        }

        static string RecommendationReason(OnboardingApi.BreedPick breed)
        {
            return string.IsNullOrWhiteSpace(breed.reason)
                ? "설문 답변과 잘 맞는 친구예요."
                : FirstCompleteSentence(breed.reason);
        }

        static string BreedDescription(OnboardingApi.BreedPick breed)
        {
            OnboardingApi.Personality p = breed.personality;
            if (p == null) return "편안하게 교감하며 지낼 수 있는 아이예요.";
            if (p.affection <= 2) return "독립적이고 혼자 있는 시간을 잘 견뎌요.";
            if (p.activity >= 4 && p.affection >= 4) return "활발하고 사람과 함께 노는 걸 좋아해요.";
            if (p.activity <= 2) return "차분하고 편안하게 쉬는 시간을 좋아해요.";
            if (p.timid >= 4) return "조심스럽지만 익숙해지면 깊이 마음을 열어요.";
            if (p.affection >= 4) return "사람을 좋아하고 애정 표현이 풍부해요.";
            return "차분함과 활동성이 고르게 어우러진 아이예요.";
        }

        static string FirstCompleteSentence(string value)
        {
            string text = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            int sentenceEnd = text.IndexOfAny(new[] { '.', '!', '?', '。' });
            if (sentenceEnd >= 0) return text.Substring(0, sentenceEnd + 1);
            return text + ".";
        }

        void Select(int index)
        {
            if (index < 0 || index >= _cards.Count) return;
            _selected = index;
            for (int i = 0; i < _cards.Count; i++)
            {
                Outline outline = _cards[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = i == index;
                Image image = _cards[i].GetComponent<Image>();
                if (image != null) image.color = i == index ? Color.white : new Color(1f, 1f, 1f, 0.72f);
                _cards[i].transform.localScale = i == index ? Vector3.one * 1.025f : Vector3.one;
            }
        }

        void ShowMind()
        {
            if (_result?.breeds == null || _selected < 0 || _selected >= Mathf.Min(3, _result.breeds.Length)) return;
            OnboardingApi.Personality p = _result.breeds[_selected].personality;
            _timid?.Set(p != null ? p.timid : 3);
            _activity?.Set(p != null ? p.activity : 3);
            _affection?.Set(p != null ? p.affection : 3);
            _dogRoot.SetActive(false);
            if (_meetRoot != null) _meetRoot.SetActive(false);
            if (_mindRoot != null) _mindRoot.SetActive(true);
        }

        async void CreateAndShowMeet()
        {
            if (_creating || _result?.breeds == null || _selected < 0) return;
            string dogName = _nameInput != null ? _nameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(dogName))
            {
                if (_nameInput != null) _nameInput.ActivateInputField();
                Debug.LogWarning("[Survey Mind] 강아지 이름을 입력해 주세요.", this);
                return;
            }

            OnboardingApi.BreedPick breed = _result.breeds[_selected];
            var personality = new OnboardingApi.Personality
            {
                timid = _timid?.Value ?? 0,
                activity = _activity?.Value ?? 0,
                affection = _affection?.Value ?? 0,
            };

            _creating = true;
            if (_mindNext != null) _mindNext.interactable = false;
            OnboardingApi.Character character = await OnboardingApi.CreateCharacter(breed.name, dogName, personality);
            _creating = false;
            if (_mindNext != null) _mindNext.interactable = true;
            if (character == null)
            {
                Debug.LogWarning($"[Survey Mind] 캐릭터 생성 실패: {dogName} ({breed.name})", this);
                return;
            }

            PlayerPrefs.SetString("selected_dog_name", dogName);
            PlayerPrefs.SetString("selected_dog_breed", breed.name);
            PlayerPrefs.SetInt("selected_dog_timid", personality.timid);
            PlayerPrefs.SetInt("selected_dog_activity", personality.activity);
            PlayerPrefs.SetInt("selected_dog_affection", personality.affection);
            PlayerPrefs.Save();

            if (_meetName != null)
            {
                _meetName.text = dogName;
                _meetName.readOnly = true;
            }
            if (_meetSummary != null)
                _meetSummary.text = $"<color=#00FF00>{dogName}</color>를 만났어요\n" +
                                    $"{breed.name} · 겁 많음 {personality.timid} · 활동성 {personality.activity} · 사람 좋아함 {personality.affection}";
            if (_mindRoot != null) _mindRoot.SetActive(false);
            if (_meetRoot != null) _meetRoot.SetActive(true);
            if (_meetDog != null)
            {
                Transform borderCollie = FindByPartialName(_meetDog.transform, "Border Collie");
                SurveyDogReveal reveal = _meetDog.GetComponent<SurveyDogReveal>() ?? _meetDog.AddComponent<SurveyDogReveal>();
                _meetDog.SetActive(true);
                reveal.Play(borderCollie != null ? borderCollie : _meetDog.transform);
            }
            Debug.Log($"[Survey Meet] {dogName} ({breed.name}) 생성 완료 — 성격 {personality.timid}/{personality.activity}/{personality.affection}", this);
        }

        static SliderBinding BuildSlider(Transform mind, string labelText)
        {
            Transform group = null;
            foreach (TMP_Text label in mind.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text.Replace(" ", string.Empty).IndexOf(labelText.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase) < 0) continue;
                group = label.transform.parent;
                break;
            }
            if (group == null) return null;

            Transform sliderRoot = FindDeep(group, "Slider");
            if (sliderRoot == null) return null;
            Transform fill = FindDeep(sliderRoot, "Fill");
            Image fillImage = fill != null ? fill.GetComponent<Image>() : null;
            TMP_Text count = FindDeep(group, "Count")?.GetComponent<TMP_Text>();
            SurveyValueSlider slider = sliderRoot.GetComponent<SurveyValueSlider>() ?? sliderRoot.gameObject.AddComponent<SurveyValueSlider>();
            slider.Configure(fillImage, count);

            SliderBinding binding = new SliderBinding
            {
                slider = slider,
            };
            binding.Set(3);
            return binding;
        }

        static GameObject FindWorldDog(Scene scene, GameObject uiDog)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != uiDog && root.name.Equals("Dog", StringComparison.OrdinalIgnoreCase) && root.layer != 5)
                    return root;
            return null;
        }

        static Transform FindByPartialName(Transform root, string partialName)
        {
            if (root == null) return null;
            if (root.name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindByPartialName(root.GetChild(i), partialName);
                if (found != null) return found;
            }
            return null;
        }

        static Button EnsureButton(Transform target)
        {
            if (target == null) return null;
            Button button = target.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
            if (button.targetGraphic == null) button.targetGraphic = target.GetComponent<Graphic>();
            return button;
        }

        static Transform FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        static Transform FindDirect(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent.GetChild(i);
            return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        static Transform FindDeepestExact(Transform root, string name)
        {
            Transform deepest = null;
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeepestExact(root.GetChild(i), name);
                if (hit != null) deepest = hit;
            }
            return deepest != null ? deepest : (root.name.Equals(name, StringComparison.OrdinalIgnoreCase) ? root : null);
        }

        static TMP_Text SetText(Transform root, string childName, string value)
        {
            Transform child = FindDeep(root, childName);
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() ?? child.GetComponentInChildren<TMP_Text>(true) : null;
            if (text != null) text.text = value;
            return text;
        }
    }
}
