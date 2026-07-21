using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Backend;

/// <summary>
/// 설문 → AI 분석 (와이어프레임 A-07 · A-08).
///
/// [변경] OpenAI를 클라이언트에서 직접 부르던 것을 Supabase Edge Function 경유로 바꿨다.
///
/// 왜: WebGL 빌드는 문자열이 그대로 노출된다. 클라에 API 키를 두면 빌드를 받은
/// 누구나 우리 키로 OpenAI를 쓸 수 있다. 와이어프레임 A-08 주석이 정확히 이걸
/// 금지한다 — "LLM 호출은 Edge Function 경유, API 키를 WebGL 클라에 노출 금지".
///
/// 서버를 거치면서 함께 얻는 것:
///  · 견종이 사전 정의 목록에서만 나온다 (A-09 화이트리스트 검증)
///  · "유기견" 금칙어가 차단된다 (부록 A)
///  · 설문·분석이 DB에 남아 D 추천이 같은 분석을 공유한다 (PRD §4.3 단일 엔진)
///  · 3D 에셋이 있는 견종(보더콜리)이 항상 후보에 포함된다 (D-021)
///
/// 씬 연결은 그대로다 — _surveyFlow·_first·_find·_dog·이벤트 필드명을 유지했다.
/// 인스펙터의 API 키·엔드포인트·모델 필드는 제거됐다(더 이상 쓰지 않는다).
/// </summary>
public class SurveyOpenAIAnalysis : MonoBehaviour
{
    [Serializable] public class StringEvent : UnityEvent<string> { }

    [Header("Scene Flow")]
    [SerializeField] private SurveyFlowController _surveyFlow;
    [SerializeField] private GameObject _first;
    [SerializeField] private GameObject _find;
    [SerializeField] private GameObject _dog;

    [Header("Result Events")]
    [SerializeField] private StringEvent _onAnalysisCompleted;
    [SerializeField] private StringEvent _onAnalysisFailed;

    public bool IsRequesting { get; private set; }
    public string LatestResponse { get; private set; }

    /// <summary>
    /// 마지막 분석 결과. 견종 3개 화면(A-09)이 이걸 읽어
    /// 이름·추천 이유·사진·성격 프리필을 그린다.
    /// </summary>
    public static OnboardingApi.AnalysisResult Latest { get; private set; }

    /// <summary>분석이 끝나 Latest가 채워졌을 때. 견종 화면이 구독한다.</summary>
    public static event Action<OnboardingApi.AnalysisResult> AnalysisReady;

    // 씬의 페이지 id → 서버 문항 키. 순서·이름이 바뀌면 여기만 고친다.
    static readonly Dictionary<string, string> PageToQuestion = new Dictionary<string, string>
    {
        { "Question01", "q1" },   // 기본 여건 (주거·동거인)
        { "Question02", "q2" },   // 함께할 시간
        { "Question03", "q3" },   // 월 지출
        { "Question04", "q4" },   // 행동 문제 (필수 서술)
        { "Question05", "q5" },   // 원하는 하루 (필수 서술)
    };

    private void Awake()
    {
        if (_surveyFlow != null) _surveyFlow.SurveyCompleted += BeginAnalysis;
    }

    private void OnDestroy()
    {
        if (_surveyFlow != null) _surveyFlow.SurveyCompleted -= BeginAnalysis;
    }

    public void BeginAnalysis()
    {
        if (IsRequesting)
        {
            Debug.LogWarning("[Survey] 이미 분석 요청이 진행 중입니다.", this);
            return;
        }
        SetState(first: false, find: true, dog: false);
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        IsRequesting = true;
        try
        {
            if (!await AppSession.EnsureSignedIn())
            {
                Fail("로그인에 실패했습니다. 네트워크를 확인해 주세요.");
                return;
            }

            // 1) 문항 단위로 저장한다 — 이탈 후 이어하기, 그리고 D 추천이 이 응답을 다시 쓴다
            var answers = Collect();
            if (!answers.TryGetValue("q4", out var q4) || string.IsNullOrWhiteSpace(q4) ||
                !answers.TryGetValue("q5", out var q5) || string.IsNullOrWhiteSpace(q5))
            {
                Fail("필수 문항(Q4·Q5) 답변이 비어 있습니다.");
                return;
            }

            string userId = SupabaseClient.UserId;
            foreach (var kv in answers)
            {
                bool ok = await OnboardingApi.SaveAnswer(userId, kv.Key, ToJsonString(kv.Value));
                if (!ok) Debug.LogWarning($"[Survey] {kv.Key} 저장 실패 — 분석은 계속 진행합니다.", this);
            }

            // 2) 서버에서 분석. 키는 서버에만 있다.
            var result = await OnboardingApi.Analyze();
            if (result == null || !string.IsNullOrEmpty(result.error))
            {
                Fail(result?.error ?? "AI 분석에 실패했습니다.");
                return;
            }

            Latest = result;
            LatestResponse = Summarize(result);
            IsRequesting = false;
            SetState(first: false, find: false, dog: true);

            Debug.Log($"[Survey] 분석 완료 — 견종 {string.Join(" / ", BreedNames(result))} · " +
                      $"참여 {result.participation?.recommended}", this);

            AnalysisReady?.Invoke(result);
            _onAnalysisCompleted?.Invoke(LatestResponse);
        }
        catch (Exception e)
        {
            Fail($"분석 중 오류가 발생했습니다: {e.Message}");
        }
    }

    /// <summary>설문 페이지 결과를 서버 문항 키로 모은다.</summary>
    private Dictionary<string, string> Collect()
    {
        var map = new Dictionary<string, string>();
        var results = _surveyFlow != null
            ? _surveyFlow.GetResults()
            : Array.Empty<SurveyFlowController.SurveyResult>();

        foreach (var r in results)
        {
            if (r == null || string.IsNullOrEmpty(r.pageId)) continue;
            if (!PageToQuestion.TryGetValue(r.pageId, out var key)) continue;   // Start 등은 건너뛴다

            var parts = new List<string>();
            if (r.selectedAnswers != null)
                foreach (var s in r.selectedAnswers)
                    if (!string.IsNullOrWhiteSpace(s)) parts.Add(s.Trim());
            if (r.inputs != null)
                foreach (var s in r.inputs)
                    if (!string.IsNullOrWhiteSpace(s)) parts.Add(s.Trim());

            if (parts.Count > 0) map[key] = string.Join(" · ", parts);
        }
        return map;
    }

    /// <summary>분석 결과를 기존 이벤트가 받던 형태(사람이 읽는 한 덩어리 텍스트)로 만든다.</summary>
    private static string Summarize(OnboardingApi.AnalysisResult r)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(r.summary)) sb.AppendLine(r.summary).AppendLine();

        if (r.breeds != null)
        {
            sb.AppendLine("추천 견종");
            foreach (var b in r.breeds) sb.AppendLine($"· {b.name} — {b.reason}");
            sb.AppendLine();
        }
        if (r.participation != null && !string.IsNullOrWhiteSpace(r.participation.reason))
            sb.Append(r.participation.reason);

        return sb.ToString().TrimEnd();
    }

    private static string[] BreedNames(OnboardingApi.AnalysisResult r)
    {
        if (r.breeds == null) return Array.Empty<string>();
        var names = new string[r.breeds.Length];
        for (int i = 0; i < r.breeds.Length; i++) names[i] = r.breeds[i].name;
        return names;
    }

    /// <summary>JSON 문자열 리터럴로 감싼다 (survey_responses.value는 jsonb다).</summary>
    private static string ToJsonString(string raw)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in raw ?? string.Empty)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    private void Fail(string message)
    {
        IsRequesting = false;
        SetState(first: true, find: false, dog: false);
        Debug.LogError("[Survey] " + message, this);
        _onAnalysisFailed?.Invoke(message);
    }

    private void SetState(bool first, bool find, bool dog)
    {
        if (_first != null) _first.SetActive(first);
        if (_find != null) _find.SetActive(find);
        if (_dog != null) _dog.SetActive(dog);
    }
}
