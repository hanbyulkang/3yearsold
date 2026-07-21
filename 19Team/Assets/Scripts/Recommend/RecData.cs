namespace Recommend
{
    // 시안(Desktop/recomend.html)의 DCLogic.renderVals() 목업 데이터.
    // Backend.RecApi.LoadIntoRecData() 가 서버 응답으로 아래 필드를 덮어쓴다 —
    // 그래서 서버가 덮는 필드는 readonly가 아니다. 서버 실패 시 이 목업이 그대로 보인다.
    public static class RecData
    {
        public struct HomeDog { public string Caption; }

        public struct ListDog
        {
            public string Name, Desc, Region, Reason;
        }

        public struct Kv { public string K, V; }

        public struct SurveyRow { public string Label, Answer; }

        public static HomeDog[] HomeDogs =
        {
            new HomeDog { Caption = "보리 · 노원구" },
            new HomeDog { Caption = "콩이 · 도봉구" },
            new HomeDog { Caption = "누리 · 성북구" },
        };

        public static ListDog[] ListDogs =
        {
            new ListDog
            {
                Name = "보리", Desc = "믹스 · 추정 3세 · 12kg · 여아", Region = "노원구",
                Reason = "혼자 있는 시간을 잘 견디는 아이예요. 평일에 집을 비우신다고 하셨죠.",
            },
            new ListDog
            {
                Name = "콩이", Desc = "시바 믹스 · 추정 2세 · 9kg · 남아", Region = "도봉구",
                Reason = "단추와 성격이 비슷해요. 낯을 가리지만 산책을 아주 좋아하는 아이예요.",
            },
            new ListDog
            {
                Name = "누리", Desc = "진도 믹스 · 4세 · 15kg · 여아", Region = "성북구",
                Reason = "짖음이 적어 원룸에도 무리가 없어요. 저녁 산책만 꾸준히 나가면 되는 아이예요.",
            },
        };

        public static string[] DetailTags =
        {
            "믹스", "추정 3세", "12kg", "여아", "보호 시작 2026-03-02",
        };

        public static Kv[] ShelterRows =
        {
            new Kv { K = "보호소", V = "노원구 동물보호센터" },
            new Kv { K = "지역", V = "서울 노원구" },
            new Kv { K = "공고번호", V = "서울-노원-2026-00127" },
        };

        public static readonly Kv[] ApplyRows =
        {
            new Kv { K = "보호소", V = "노원구 동물보호센터" },
            new Kv { K = "활동", V = "주말 산책 봉사 (2시간)" },
            new Kv { K = "모집", V = "이번 주 토 · 4명 중 2자리 남음" },
        };

        // V 는 placeholder 문구다. 시안은 "김지민" / "010-1234-5678" 처럼 예시값을 넣어
        // 채워진 상태를 보여주지만, 실제 입력칸에 그대로 쓰면 이미 입력된 것처럼 읽힌다.
        // 무엇을 넣어야 하는지 알려주는 안내 문구로 바꾼다.
        public static readonly Kv[] ApplyFields =
        {
            new Kv { K = "이름", V = "이름을 입력해주세요" },
            new Kv { K = "연락처", V = "010-0000-0000" },
            new Kv { K = "희망일", V = "희망하는 날짜와 시간을 골라주세요" },
        };

        public static readonly SurveyRow[] SurveyRows =
        {
            new SurveyRow { Label = "Q1 기본 여건",        Answer = "28세 · 원룸 · 혼자" },
            new SurveyRow { Label = "Q2 함께할 시간",      Answer = "2~4시간" },
            new SurveyRow { Label = "Q3 월 지출",          Answer = "5~10만원" },
            new SurveyRow { Label = "Q4 행동 문제가 생기면", Answer = "\"왜 짖는지 먼저 찾아볼 것 같아요...\"" },
            new SurveyRow { Label = "Q5 원하는 하루",      Answer = "\"퇴근하고 같이 산책하는 하루\"" },
        };

        // D-01 다음 한 걸음 (§4.4 — 항상 1개만)
        public static string NextStepText =
            "매일 산책을 거르지 않으시네요. 지금은 <b>주말 봉사 한 번</b>이 잘 맞아 보여요. " +
            "서울 노원구 보호소가 주말 산책 봉사자를 찾고 있어요.";

        // D-03 AI 소개문
        public static string DetailIntro =
            "공고에는 \"겁 많음, 검정, 믹스\" 세 줄뿐이지만 — 보리는 처음 보는 사람 앞에서 몸을 낮추다가도, " +
            "간식을 내밀면 조심스럽게 다가오는 아이예요. 혼자 있는 시간을 잘 견뎌서, " +
            "평일 낮에 집을 비우는 당신의 생활에도 무리가 없어요.";
    }
}
