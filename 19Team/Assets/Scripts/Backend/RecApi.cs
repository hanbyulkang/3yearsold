using System;
using System.Threading.Tasks;
using UnityEngine;
using Recommend;

namespace Backend
{
    /// <summary>
    /// D 추천 씬 데이터 로더 — RecData의 목업을 서버 실데이터로 덮어쓴다.
    ///
    /// 데이터 흐름 (PRD §4.3 단일 엔진)
    ///   recommend Edge Function  → 추천 3마리 (seq·이유. 캐시가 있으면 LLM 호출 없음)
    ///   shelter_animals REST     → 이름·품종·성별·체중 등 공고 원본
    ///   analyses REST            → 참여 추천 문구 (D-01 "다음 한 걸음")
    ///
    /// 실패하면 예외를 던지고, RecBootstrap이 목업 그대로 연다 —
    /// 데모가 네트워크에 인질 잡히지 않게 한다.
    /// </summary>
    public static class RecApi
    {
        // ---------- 응답 형태 ----------
        [Serializable] class Pick { public int seq; public string reason; }
        [Serializable] class RecResp { public Pick[] picks; public string error; }

        [Serializable]
        class Animal
        {
            public int seq;
            public string name;
            public string breed;
            public string sex;        // male / female / unknown (0001 생성 컬럼)
            public float weight_kg;
            public string birth_ymd;  // yyyy-MM-dd
            public string one_liner;  // traits->>one_liner
        }

        [Serializable] class Participation { public string reason; }
        [Serializable] class AnalysisResult { public Participation participation; }
        [Serializable] class AnalysisRow { public AnalysisResult result; }

        // JsonUtility는 최상위 배열을 못 읽는다 — 감싸서 파싱한다.
        [Serializable] class AnimalList { public Animal[] items; }
        [Serializable] class AnalysisList { public AnalysisRow[] items; }

        static Animal[] ParseAnimals(string json)
            => JsonUtility.FromJson<AnimalList>("{\"items\":" + json + "}").items;
        static AnalysisRow[] ParseAnalyses(string json)
            => JsonUtility.FromJson<AnalysisList>("{\"items\":" + json + "}").items;

        // ---------- 로드 ----------

        public static async Task LoadIntoRecData()
        {
            bool signedIn = await AppSession.EnsureSignedIn();
            if (!signedIn) throw new Exception("로그인 실패");

            var rec = await SupabaseClient.Invoke<RecResp>("recommend");
            if (rec == null || rec.picks == null || rec.picks.Length == 0)
                throw new Exception(rec?.error ?? "추천 응답 없음");

            // 공고 원본 조회 — AI 소개는 pick.reason, 사실 정보는 여기서 (D-03 원칙: 사실과 AI 문구 구분)
            var seqs = string.Join(",", Array.ConvertAll(rec.picks, p => p.seq.ToString()));
            var raw = await SupabaseClient.GetRaw(
                $"shelter_animals?seq=in.({seqs})" +
                "&select=seq,name,breed,sex,weight_kg,birth_ymd,one_liner:traits->>one_liner");
            if (string.IsNullOrEmpty(raw)) throw new Exception("보호견 조회 실패");
            var animals = ParseAnimals(raw);

            // D-01 "다음 한 걸음" — 온보딩 분석의 참여 추천 문구
            string nextStep = null;
            var aRaw = await SupabaseClient.GetRaw(
                "analyses?select=result&superseded_by=is.null&order=created_at.desc&limit=1");
            if (!string.IsNullOrEmpty(aRaw) && aRaw != "[]")
            {
                var rows = ParseAnalyses(aRaw);
                if (rows.Length > 0) nextStep = rows[0].result?.participation?.reason;
            }

            Apply(rec.picks, animals, nextStep);
        }

        // ---------- RecData 덮어쓰기 ----------

        static Animal Find(Animal[] all, int seq)
        {
            foreach (var a in all) if (a.seq == seq) return a;
            return null;
        }

        static string Sex(string s) => s == "female" ? "여아" : s == "male" ? "남아" : "성별 미상";

        static string AgeText(string birthYmd)
        {
            if (string.IsNullOrEmpty(birthYmd) || birthYmd.Length < 4) return "나이 미상";
            if (!int.TryParse(birthYmd.Substring(0, 4), out var y)) return "나이 미상";
            int age = Math.Max(0, DateTime.Now.Year - y);
            return age == 0 ? "1살 미만" : $"추정 {age}세";
        }

        static void Apply(Pick[] picks, Animal[] animals, string nextStep)
        {
            int n = picks.Length;
            var home = new RecData.HomeDog[Math.Min(3, n)];
            var list = new RecData.ListDog[n];

            for (int i = 0; i < n; i++)
            {
                var a = Find(animals, picks[i].seq);
                string name = a?.name ?? $"#{picks[i].seq}";
                string desc = a == null
                    ? "공고 정보를 불러오지 못했어요"
                    : $"{(string.IsNullOrEmpty(a.breed) ? "믹스" : a.breed)} · {AgeText(a.birth_ymd)} · {a.weight_kg:0.#}kg · {Sex(a.sex)}";

                list[i] = new RecData.ListDog
                {
                    Name = name,
                    Desc = desc,
                    Region = "서울",   // vPetInfo는 서울동물복지지원센터 소관 (D-019)
                    Reason = picks[i].reason,
                };
                if (i < home.Length) home[i] = new RecData.HomeDog { Caption = $"{name} · 서울" };
            }

            RecData.HomeDogs = home;
            RecData.ListDogs = list;

            // D-03 상세 — 첫 번째 추천견 기준
            var first = Find(animals, picks[0].seq);
            if (first != null)
            {
                RecData.DetailTags = new[]
                {
                    string.IsNullOrEmpty(first.breed) ? "믹스" : first.breed,
                    AgeText(first.birth_ymd),
                    $"{first.weight_kg:0.#}kg",
                    Sex(first.sex),
                };
                // AI 문구(개인화 이유)와 사실(공고 한 줄)을 함께, 구분되게 (D-03)
                RecData.DetailIntro = string.IsNullOrEmpty(first.one_liner)
                    ? picks[0].reason
                    : $"{picks[0].reason}\n\n보호소 기록 한 줄 — \"{first.one_liner}\"";
                RecData.ShelterRows = new[]
                {
                    new RecData.Kv { K = "보호소", V = "서울동물복지지원센터" },
                    new RecData.Kv { K = "지역", V = "서울" },
                    new RecData.Kv { K = "공고번호", V = $"vPetInfo #{first.seq}" },
                };
            }

            if (!string.IsNullOrEmpty(nextStep)) RecData.NextStepText = nextStep;

            Debug.Log($"[RecApi] 서버 추천 적용 — {n}마리 ({list[0].Name} 외)");
        }
    }
}
