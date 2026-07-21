using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Backend
{
    /// <summary>
    /// 견종 3개 추천 화면 (A-09).
    ///
    /// 씬의 Dog 패널 구조를 그대로 쓴다:
    ///   Dog / Scroll View / Viewport / Content / Frame   ← 카드 1개가 템플릿
    ///     Frame / Image / Image   사진
    ///     Frame / Name            견종명
    ///     Frame / Description     견종 설명
    ///     Frame / AI / Description  추천 이유 (사용자 문장 인용)
    ///
    /// 템플릿을 3개로 복제해 서버 분석 결과를 채운다. 사진은 RemoteImage가
    /// 기존 Image 위에 얹으므로 실패해도 원래 자리 그림이 남는다.
    ///
    /// 선택 → "선택하기"(Bottom (4)/Next) 버튼으로 캐릭터견 생성.
    /// 견종은 서버가 화이트리스트로 다시 검증한다.
    /// </summary>
    public class BreedResultBinder : MonoBehaviour
    {
        [Tooltip("비워두면 이 오브젝트 아래에서 Content/Next를 이름으로 찾는다")]
        [SerializeField] private Transform _content;
        [SerializeField] private Button _selectButton;

        [Tooltip("캐릭터견 이름 입력칸 (없으면 견종명을 그대로 이름으로 쓴다)")]
        [SerializeField] private TMP_InputField _nameInput;

        readonly List<GameObject> _cards = new List<GameObject>();
        GameObject _template;
        int _selected = -1;
        OnboardingApi.AnalysisResult _result;
        bool _creating;

        void OnEnable()
        {
            SurveyOpenAIAnalysis.AnalysisReady += Bind;
            // 이미 분석이 끝난 뒤 화면이 켜지는 경우(대부분이 이 경로다)
            if (SurveyOpenAIAnalysis.Latest != null) Bind(SurveyOpenAIAnalysis.Latest);
        }

        void OnDisable() => SurveyOpenAIAnalysis.AnalysisReady -= Bind;

        void Bind(OnboardingApi.AnalysisResult result)
        {
            if (result?.breeds == null || result.breeds.Length == 0) return;
            _result = result;

            var content = _content != null ? _content : FindDeep(transform, "Content");
            if (content == null) { Debug.LogWarning("[A-09] Content를 찾지 못했습니다", this); return; }

            if (_template == null)
            {
                _template = FindDeep(content, "Frame")?.gameObject;
                if (_template == null) { Debug.LogWarning("[A-09] Frame 템플릿이 없습니다", this); return; }
                _template.SetActive(false);   // 원본은 숨기고 복제만 보여준다
            }

            foreach (var c in _cards) if (c != null) Destroy(c);
            _cards.Clear();
            _selected = -1;

            for (int i = 0; i < result.breeds.Length; i++)
            {
                var b = result.breeds[i];
                var card = Instantiate(_template, content);
                card.name = $"Frame_{b.name}";
                card.SetActive(true);

                SetText(card.transform, "Name", b.name);
                SetText(card.transform, "Description", DescribeBreed(b));
                var ai = FindDeep(card.transform, "AI");
                if (ai != null) SetText(ai, "Description", b.reason);

                // 사진 — Frame/Image/Image 안쪽 슬롯에 얹는다
                var imgSlot = FindDeep(card.transform, "Image");
                if (imgSlot != null && !string.IsNullOrEmpty(b.imageUrl))
                    RemoteImage.Load(b.imageUrl, imgSlot as RectTransform);

                int index = i;
                var btn = card.GetComponent<Button>() ?? card.AddComponent<Button>();
                btn.onClick.AddListener(() => Select(index));

                _cards.Add(card);
            }

            Select(0);   // 첫 카드를 기본 선택 — 아무것도 안 고르고 넘어가는 상태를 없앤다

            var next = _selectButton != null ? _selectButton : FindDeep(transform, "Next")?.GetComponent<Button>();
            if (next != null)
            {
                next.onClick.RemoveListener(Confirm);
                next.onClick.AddListener(Confirm);
            }
        }

        /// <summary>성격 프리필 값을 사람이 읽는 한 줄로 (A-10 값이 뭘 뜻하는지 보여준다).</summary>
        static string DescribeBreed(OnboardingApi.BreedPick b)
        {
            var p = b.personality;
            if (p == null) return string.Empty;
            return $"활동량 {Bar(p.activity)} · 겁 {Bar(p.timid)} · 애정표현 {Bar(p.affection)}";
        }

        static string Bar(int v) => new string('●', Mathf.Clamp(v, 1, 5)) + new string('○', 5 - Mathf.Clamp(v, 1, 5));

        void Select(int index)
        {
            _selected = index;
            for (int i = 0; i < _cards.Count; i++)
            {
                var outline = _cards[i].GetComponent<Outline>();
                if (outline != null) outline.enabled = (i == index);
                var img = _cards[i].GetComponent<Image>();
                if (img != null) img.color = (i == index) ? Color.white : new Color(1f, 1f, 1f, 0.72f);
            }
        }

        async void Confirm()
        {
            if (_creating || _result?.breeds == null) return;
            if (_selected < 0 || _selected >= _result.breeds.Length) return;

            _creating = true;
            string breed = _result.breeds[_selected].name;
            string dogName = _nameInput != null && !string.IsNullOrWhiteSpace(_nameInput.text)
                ? _nameInput.text.Trim()
                : breed;   // 이름 화면이 아직 없으면 견종명을 임시로 쓴다

            var ch = await OnboardingApi.CreateCharacter(breed, dogName);
            _creating = false;

            if (ch == null) { Debug.LogWarning($"[A-09] 캐릭터 생성 실패 ({breed})", this); return; }
            Debug.Log($"[A-09] 캐릭터견 생성 — {ch.name} ({ch.breed}) LV.{ch.level}", this);
        }

        // ---------- 이름으로 자식 찾기 ----------
        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        static void SetText(Transform root, string childName, string value)
        {
            var t = FindDeep(root, childName);
            if (t == null) return;
            var tmp = t.GetComponent<TMP_Text>() ?? t.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = value;
        }
    }
}
