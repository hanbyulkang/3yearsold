using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Backend
{
    /// <summary>
    /// A 온보딩 연동 (와이어프레임 A-01 ~ A-11).
    ///
    /// 흐름
    ///   로그인 → 문항마다 SaveAnswer → (선택) Probe → Analyze → 견종 3개 표시
    ///
    /// 설계 제약
    ///  · 설문은 문항 단위로 즉시 저장한다. 이탈 후 이어하기 때문이다 (A-03).
    ///  · 되묻기는 부가 기능이다. 실패해도 진행을 막지 않는다 (A-06).
    ///  · 분석은 온보딩에서 1회만 호출한다. 결과를 견종·보호견·참여 추천이
    ///    공유한다 (PRD §4.3 단일 엔진). 화면마다 다시 부르지 않는다.
    /// </summary>
    public static class OnboardingApi
    {
        [Serializable]
        public class Personality { public int activity; public int timid; public int affection; }

        [Serializable]
        public class Attribution
        {
            public string author;    // null이 아니면 화면에 반드시 노출해야 한다
            public string license;   // CC BY 계열은 출처 표기가 의무다
        }

        [Serializable]
        public class BreedPick
        {
            public string name;
            public string reason;        // 사용자가 쓴 문장을 인용한 추천 이유
            public string imageUrl;      // Supabase Storage 공개 URL
            public Personality personality;  // A-10 성격 프리필 값
            public Attribution attribution;  // null이면 CC0·퍼블릭 도메인 (표기 불필요)
        }

        [Serializable]
        public class Participation { public string recommended; public string readiness; public string reason; }

        [Serializable]
        public class AnalysisResult
        {
            public string analysisId;
            public string summary;
            public BreedPick[] breeds;      // 정확히 3개
            public Participation participation;
            public string error;
            public bool retryable;
        }

        [Serializable] class ProbeBody { public string questionId; public string questionTitle; public string answer; }
        [Serializable] public class ProbeResult { public string probe; public string reason; }

        /// <summary>문항 하나를 저장한다. valueJson은 JSON 값(문자열이면 따옴표 포함).</summary>
        public static Task<bool> SaveAnswer(string userId, string questionId, string valueJson)
            => SupabaseClient.UpsertSurveyAnswer(userId, questionId, valueJson);

        /// <summary>
        /// 자유 서술 답변에 되물을지 서버가 판단한다.
        /// probe가 null이면 되묻지 않고 넘어간다. 실패해도 null이 오므로 진행을 막지 않는다.
        /// </summary>
        public static async Task<string> Probe(string questionId, string questionTitle, string answer)
        {
            var body = JsonUtility.ToJson(new ProbeBody
            {
                questionId = questionId, questionTitle = questionTitle, answer = answer,
            });
            var r = await SupabaseClient.Invoke<ProbeResult>("survey-probe", body);
            return string.IsNullOrEmpty(r?.probe) ? null : r.probe;
        }

        /// <summary>
        /// 설문 전체를 분석한다. 5~7초 걸리므로 로딩 화면(A-08)이 필요하다.
        /// 실패해도 설문 응답은 서버에 남아 있으므로 재시도만 하면 된다.
        /// </summary>
        public static async Task<AnalysisResult> Analyze()
        {
            var r = await SupabaseClient.Invoke<AnalysisResult>("survey-analyze");
            if (r == null) return new AnalysisResult { error = "네트워크 오류", retryable = true };
            return r;
        }
    }
}
