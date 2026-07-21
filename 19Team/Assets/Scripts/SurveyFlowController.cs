using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SurveyFlowController : MonoBehaviour
{
    [Serializable]
    public class Page
    {
        public string id;
        public GameObject root;
        public Button back;
        public Button next;
        public Button[] answers;
        public int selectedAnswer = -1;
        public TMP_InputField[] inputFields;
        public bool requireAnswer;
        public TMP_Text validationMessage;
        [NonSerialized] public Color[] answerColors;
        [NonSerialized] public Color[] answerTextColors;
    }

    [Serializable]
    public class SurveyResult
    {
        public string pageId;
        public int selectedAnswer;
        public string[] selectedAnswers;
        public string[] inputs;
    }

    [Header("Start, Question01, Question02, Question03, Question04, Question05")]
    [SerializeField] private Page[] _pages = new Page[6];
    [SerializeField] private int _firstPage;
    [Header("Progress")]
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private Image _progressFill;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private GameObject _topUi;
    [SerializeField] private GameObject _sliderUi;
    [Header("Selection Style")]
    [SerializeField] private Color _selectedColor = new Color32(231, 151, 66, 255);
    [SerializeField] private Color _selectedTextColor = Color.white;
    [Header("Events")]
    [SerializeField] private UnityEvent _onSurveyCompleted;

    private int _currentPage;
    private readonly Dictionary<Button, Transform> _answerGroups = new Dictionary<Button, Transform>();
    private readonly Dictionary<Transform, int> _selectedByGroup = new Dictionary<Transform, int>();
    private readonly Dictionary<Button, GameObject> _answerIndicators = new Dictionary<Button, GameObject>();
    public int CurrentPage => _currentPage;
    public event Action SurveyCompleted;

    private void Awake()
    {
        if (_pages == null || _pages.Length == 0) return;
        ResolveMissingUiComponents();
        for (int pageIndex = 0; pageIndex < _pages.Length; pageIndex++)
        {
            int capturedPage = pageIndex;
            Page page = _pages[pageIndex];
            if (page == null) continue;
            if (page.back != null) { page.back.interactable = true; page.back.onClick.AddListener(() => GoBackFrom(capturedPage)); }
            if (page.next != null) { page.next.interactable = true; page.next.onClick.AddListener(() => GoNextFrom(capturedPage)); }

            int answerCount = page.answers != null ? page.answers.Length : 0;
            page.answerColors = new Color[answerCount];
            page.answerTextColors = new Color[answerCount];
            for (int answerIndex = 0; answerIndex < answerCount; answerIndex++)
            {
                Button answer = page.answers[answerIndex];
                if (answer == null) continue;
                int capturedAnswer = answerIndex;
                answer.interactable = true;
                page.answerColors[answerIndex] = answer.targetGraphic != null ? answer.targetGraphic.color : Color.white;
                TMP_Text label = answer.GetComponentInChildren<TMP_Text>(true);
                page.answerTextColors[answerIndex] = label != null ? label.color : Color.white;
                Transform group = answer.transform.parent;
                _answerGroups[answer] = group;
                if (!_selectedByGroup.ContainsKey(group)) _selectedByGroup[group] = -1;
                _answerIndicators[answer] = FindCircleIndicator(answer);
                answer.onClick.AddListener(() => SelectAnswer(capturedPage, capturedAnswer));
            }
            if (page.inputFields != null)
                foreach (TMP_InputField input in page.inputFields)
                    if (input != null) { input.interactable = true; input.readOnly = false; input.onValueChanged.AddListener(_ => HideValidation(page)); }
            HideValidation(page);
            RefreshAnswerStyle(page);
        }
        ShowPage(Mathf.Clamp(_firstPage, 0, _pages.Length - 1));
    }

    private void ResolveMissingUiComponents()
    {
        foreach (Page page in _pages)
        {
            if (page == null || page.root == null) continue;
            Transform root = page.root.transform;
            if (page.back == null) page.back = EnsureButton(FindExact(root, "Back"));
            if (page.next == null) page.next = EnsureButton(FindExact(root, "Next"));
            if (page.answers == null || page.answers.Length == 0)
            {
                var answers = new List<Button>();
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name.Equals("Answer", StringComparison.OrdinalIgnoreCase))
                    {
                        Button button = EnsureButton(child);
                        if (button != null) answers.Add(button);
                    }
                page.answers = answers.ToArray();
            }
            if (page.inputFields == null || page.inputFields.Length == 0)
                page.inputFields = root.GetComponentsInChildren<TMP_InputField>(true);
        }

        if (_progressFill == null)
        {
            Transform slider = FindExact(transform, "Slider");
            Transform fill = slider != null ? FindExact(slider, "Fill") : null;
            if (fill != null) _progressFill = fill.GetComponent<Image>();
        }
        if (_topUi == null)
        {
            Transform top = FindExact(transform, "Top");
            if (top != null) _topUi = top.gameObject;
        }
        if (_sliderUi == null)
        {
            Transform slider = FindExact(transform, "Slider");
            if (slider != null) _sliderUi = slider.gameObject;
        }
    }

    private static Button EnsureButton(Transform target)
    {
        if (target == null) return null;
        Button button = target.GetComponent<Button>();
        if (button == null) button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = target.GetComponent<Graphic>();
        return button;
    }

    private static Transform FindExact(Transform root, string wanted)
    {
        if (root.name.Equals(wanted, StringComparison.OrdinalIgnoreCase)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindExact(root.GetChild(i), wanted);
            if (found != null) return found;
        }
        return null;
    }

    public void ShowPage(int index)
    {
        if (_pages == null || index < 0 || index >= _pages.Length) return;
        for (int i = 0; i < _pages.Length; i++)
            if (_pages[i] != null && _pages[i].root != null) _pages[i].root.SetActive(i == index);
        _currentPage = index;
        int question = Mathf.Clamp(index, 0, 5);
        if (_progressSlider != null)
        {
            _progressSlider.minValue = 0f;
            _progressSlider.maxValue = 5f;
            _progressSlider.wholeNumbers = true;
            _progressSlider.SetValueWithoutNotify(question);
        }
        if (_progressFill != null)
        {
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = 0;
            _progressFill.fillAmount = question / 5f;
        }
        if (_progressText != null) _progressText.text = question + "/5";
        bool showQuestionChrome = index > 0;
        if (_topUi != null) _topUi.SetActive(showQuestionChrome);
        if (_sliderUi != null) _sliderUi.SetActive(showQuestionChrome);
    }

    private void GoBackFrom(int index) { if (index > 0) ShowPage(index - 1); }
    private void GoNextFrom(int index)
    {
        if (!IsPageValid(_pages[index])) return;
        if (index < _pages.Length - 1) ShowPage(index + 1);
        else
        {
            _onSurveyCompleted?.Invoke();
            SurveyCompleted?.Invoke();
        }
    }

    private void SelectAnswer(int pageIndex, int answerIndex)
    {
        Page page = _pages[pageIndex];
        page.selectedAnswer = answerIndex;
        Button selected = page.answers[answerIndex];
        if (selected != null && _answerGroups.TryGetValue(selected, out Transform group))
            _selectedByGroup[group] = answerIndex;
        HideValidation(page);
        RefreshAnswerStyle(page);
    }

    private bool IsPageValid(Page page)
    {
        if (page == null || !page.requireAnswer) return true;
        bool hasGroups = false;
        bool valid = true;
        if (page.answers != null)
        {
            var checkedGroups = new HashSet<Transform>();
            foreach (Button answer in page.answers)
            {
                if (answer == null || !_answerGroups.TryGetValue(answer, out Transform group) || !checkedGroups.Add(group)) continue;
                hasGroups = true;
                if (!_selectedByGroup.TryGetValue(group, out int selected) || selected < 0) valid = false;
            }
        }
        if (!hasGroups)
        {
            valid = false;
        }
        if (!valid && !hasGroups && page.inputFields != null)
            foreach (TMP_InputField input in page.inputFields)
                if (input != null && !string.IsNullOrWhiteSpace(input.text)) { valid = true; break; }
        if (!valid && page.validationMessage != null)
        {
            page.validationMessage.text = "답변을 선택하거나 입력해 주세요.";
            page.validationMessage.gameObject.SetActive(true);
        }
        return valid;
    }

    private void RefreshAnswerStyle(Page page)
    {
        if (page.answers == null) return;
        for (int i = 0; i < page.answers.Length; i++)
        {
            Button answer = page.answers[i]; if (answer == null) continue;
            bool selected = i == page.selectedAnswer;
            if (_answerGroups.TryGetValue(answer, out Transform group))
                selected = _selectedByGroup.TryGetValue(group, out int selectedIndex) && selectedIndex == i;
            if (answer.targetGraphic != null) answer.targetGraphic.color = selected ? _selectedColor : page.answerColors[i];
            TMP_Text label = answer.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = selected ? _selectedTextColor : page.answerTextColors[i];
            if (_answerIndicators.TryGetValue(answer, out GameObject indicator) && indicator != null)
                indicator.SetActive(selected);
        }
    }

    private static GameObject FindCircleIndicator(Button answer)
    {
        Image[] images = answer.GetComponentsInChildren<Image>(true);
        Image best = null;
        int bestDepth = -1;
        foreach (Image image in images)
        {
            if (image.transform == answer.transform) continue;
            int depth = 0;
            for (Transform current = image.transform; current != answer.transform && current != null; current = current.parent) depth++;
            if (depth > bestDepth) { best = image; bestDepth = depth; }
        }
        return best != null ? best.gameObject : null;
    }

    private static void HideValidation(Page page)
    {
        if (page != null && page.validationMessage != null) page.validationMessage.gameObject.SetActive(false);
    }

    public SurveyResult[] GetResults()
    {
        var results = new List<SurveyResult>();
        if (_pages == null) return results.ToArray();
        foreach (Page page in _pages)
        {
            if (page == null) continue;
            var selectedLabels = new List<string>();
            if (page.answers != null)
            {
                for (int answerIndex = 0; answerIndex < page.answers.Length; answerIndex++)
                {
                    Button answer = page.answers[answerIndex];
                    if (answer == null || !_answerGroups.TryGetValue(answer, out Transform group)) continue;
                    if (!_selectedByGroup.TryGetValue(group, out int selectedIndex) || selectedIndex != answerIndex) continue;
                    TMP_Text label = answer.GetComponentInChildren<TMP_Text>(true);
                    selectedLabels.Add(label != null && !string.IsNullOrWhiteSpace(label.text) ? label.text.Trim() : "선택지 " + (answerIndex + 1));
                }
            }
            string[] inputs = new string[page.inputFields != null ? page.inputFields.Length : 0];
            for (int i = 0; i < inputs.Length; i++) inputs[i] = page.inputFields[i] != null ? page.inputFields[i].text : string.Empty;
            results.Add(new SurveyResult { pageId = page.id, selectedAnswer = page.selectedAnswer, selectedAnswers = selectedLabels.ToArray(), inputs = inputs });
        }
        return results.ToArray();
    }
}
