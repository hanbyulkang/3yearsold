using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class SurveyOpenAIAnalysis : MonoBehaviour
{
    [Serializable] public class StringEvent : UnityEvent<string> { }

    [Serializable]
    private class ResponseRequest
    {
        public string model;
        public string instructions;
        public string input;
        public int max_output_tokens;
    }

    [Serializable] private class ApiResponse { public OutputItem[] output; }
    [Serializable] private class OutputItem { public ContentItem[] content; }
    [Serializable] private class ContentItem { public string type; public string text; }
    [Serializable] private class ApiErrorEnvelope { public ApiError error; }
    [Serializable] private class ApiError { public string message; }

    [Header("Scene Flow")]
    [SerializeField] private SurveyFlowController _surveyFlow;
    [SerializeField] private GameObject _first;
    [SerializeField] private GameObject _find;
    [SerializeField] private GameObject _dog;

    [Header("OpenAI")]
    [Tooltip("테스트용입니다. 빌드에 API 키를 포함하지 말고 실제 서비스에서는 서버 프록시를 사용하세요.")]
    [SerializeField] private string _apiKey;
    [SerializeField] private string _endpoint = "https://api.openai.com/v1/responses";
    [SerializeField] private string _model = "gpt-4.1-mini";
    [TextArea(4, 12)]
    [SerializeField] private string _instructions = "사용자의 반려견 설문 응답을 분석해 어울리는 반려견 성향과 추천을 친절한 한국어로 요약하세요.";
    [SerializeField, Min(1)] private int _maxOutputTokens = 800;
    [SerializeField, Min(5)] private int _timeoutSeconds = 60;

    [Header("Result Events")]
    [SerializeField] private StringEvent _onAnalysisCompleted;
    [SerializeField] private StringEvent _onAnalysisFailed;

    public bool IsRequesting { get; private set; }
    public string LatestResponse { get; private set; }

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
            Debug.LogWarning("[Survey OpenAI] 이미 분석 요청이 진행 중입니다.", this);
            return;
        }

        SetState(first: false, find: true, dog: false);
        string key = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            Fail("OpenAI API 키가 없습니다. SurveyOpenAIAnalysis 인스펙터의 Api Key를 입력하거나 OPENAI_API_KEY 환경 변수를 설정해 주세요.");
            return;
        }

        StartCoroutine(RequestAnalysis(key));
    }

    private IEnumerator RequestAnalysis(string key)
    {
        IsRequesting = true;
        string prompt = BuildSurveyPrompt();
        var payload = new ResponseRequest
        {
            model = _model,
            instructions = _instructions,
            input = prompt,
            max_output_tokens = _maxOutputTokens
        };
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        Debug.Log($"[Survey OpenAI] 분석 요청 시작 / model={_model}\n[전송 설문]\n{prompt}", this);

        using (var request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = _timeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + key);

            yield return request.SendWebRequest();
            string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            Debug.Log($"[Survey OpenAI] HTTP 응답 수신 / status={request.responseCode}", this);

            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = ReadApiError(responseBody);
                if (string.IsNullOrWhiteSpace(message)) message = !string.IsNullOrWhiteSpace(responseBody) ? responseBody : request.error;
                Fail($"OpenAI 요청 실패 ({request.responseCode}): {message}");
                yield break;
            }

            string answer = ReadOutputText(responseBody);
            if (string.IsNullOrWhiteSpace(answer))
            {
                Fail("OpenAI 응답에서 분석 문장을 찾지 못했습니다. 원본 응답: " + responseBody);
                yield break;
            }

            IsRequesting = false;
            LatestResponse = answer;
            SetState(first: false, find: false, dog: true);
            Debug.Log("[Survey OpenAI] 분석 완료\n[OpenAI 답변]\n" + answer, this);
            _onAnalysisCompleted?.Invoke(answer);
        }
    }

    private string BuildSurveyPrompt()
    {
        var builder = new StringBuilder("다음 설문 응답을 분석해 주세요.\n");
        SurveyFlowController.SurveyResult[] results = _surveyFlow != null ? _surveyFlow.GetResults() : Array.Empty<SurveyFlowController.SurveyResult>();
        foreach (SurveyFlowController.SurveyResult result in results)
        {
            builder.Append("- ").Append(result.pageId).Append(":");
            if (result.selectedAnswers != null)
                foreach (string selected in result.selectedAnswers)
                    if (!string.IsNullOrWhiteSpace(selected)) builder.Append(" 선택: ").Append(selected);
            if (result.inputs != null)
                foreach (string input in result.inputs)
                    if (!string.IsNullOrWhiteSpace(input)) builder.Append(", 입력: ").Append(input.Trim());
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_apiKey)) return _apiKey.Trim();
        return Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    private static string ReadOutputText(string json)
    {
        ApiResponse response = JsonUtility.FromJson<ApiResponse>(json);
        if (response?.output == null) return null;
        foreach (OutputItem item in response.output)
            if (item?.content != null)
                foreach (ContentItem content in item.content)
                    if (content != null && content.type == "output_text" && !string.IsNullOrWhiteSpace(content.text)) return content.text;
        return null;
    }

    private static string ReadApiError(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonUtility.FromJson<ApiErrorEnvelope>(json)?.error?.message; }
        catch { return null; }
    }

    private void Fail(string message)
    {
        IsRequesting = false;
        SetState(first: true, find: false, dog: false);
        Debug.LogError("[Survey OpenAI] " + message, this);
        _onAnalysisFailed?.Invoke(message);
    }

    private void SetState(bool first, bool find, bool dog)
    {
        if (_first != null) _first.SetActive(first);
        if (_find != null) _find.SetActive(find);
        if (_dog != null) _dog.SetActive(dog);
    }
}
