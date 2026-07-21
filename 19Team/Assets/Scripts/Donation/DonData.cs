namespace Donation
{
    // 시안(Desktop/donation.dc.html)의 DCLogic.renderVals() 목업 데이터.
    //
    // 시안은 기본 재화를 "P(포인트)"로 적고 있지만 이 저장소는 D-019 로 명칭을 "뼈다귀"로
    // 통일했다 — 여기서는 수치만 가져오고 단위는 전부 뼈다귀로 쓴다.
    //
    // TODO(백엔드): 아래 값은 서버가 채워야 한다.
    //   - 잔액·기부·배분은 전부 Edge Function + 원장에서만 움직인다 (PRD §5.5).
    //     WebGL 클라이언트는 코드가 통째로 노출되므로 어떤 재화 계산도 여기 두지 않는다.
    //   - 공동 창고 게이지는 모금액이 아니라 참여량 집계다 (§6.1). 라벨을 "모금액"으로 바꾸지 말 것.
    //   - RecApi.LoadIntoRecData() 와 같은 방식으로 DonApi 를 만들어 이 필드를 덮으면 된다.
    //     실패하면 이 목업이 그대로 보이는 구조를 유지한다.
    public static class DonData
    {
        public struct Target
        {
            public string Name, Sub, Tag;
            public string Photo;   // 서버가 채움 — null이면 사진 자리 유지
        }

        public struct Report
        {
            public string Title, Status, Body;
            public bool Ok;             // true 집행 완료 / false 목표 미달 종료
            public bool HasCertificate; // 증서가 발급된 건 (E-04 로 이동)
            public string Photo;        // 보호소 수령 확인 사진 (§6.4) — 없으면 자리만 표시
            public string PhotoCaption;
        }

        /// <summary>보유 뼈다귀. 서버 잔액으로 덮어쓴다.</summary>
        public static int Bones = 12680;

        // ---- E-01 공동 창고 ----
        public static int WarehousePercent = 68;              // 참여량 진행률 (모금액 아님)
        public static string WarehouseGoal = "달성 시 사료 200kg 기부";
        public static int MyContribution = 3400;

        /// <summary>기부 금액 선택지. 시안의 500 / 1,000 / 3,000.</summary>
        public static readonly int[] DonateAmounts = { 500, 1000, 3000 };
        public const int DefaultAmountIndex = 1;

        // ---- E-02 지정 후원 ----
        public static Target[] Targets =
        {
            new Target { Name = "보리",              Sub = "이번 달 후원 참여 12명", Tag = "노원구 동물보호센터" },
            new Target { Name = "도봉구 보호소 전체", Sub = "이번 달 후원 참여 4명",  Tag = "봉사자 부족" },
        };

        public const int DefaultAllocation = 2000;

        // §6.5 폐루프 — 수혜처 선정 기준을 화면에 그대로 적는다
        public const string RotationNote =
            "배분처가 몰리지 않게 같은 보호소에 연속으로 배분되지 않아요 (순환 배분)";

        // ---- E-03 집행 내역 ----
        // §6.5 정직성 규칙 — 미달성 캠페인도 결과를 공개한다. 조용히 지우지 말 것.
        public static Report[] Reports =
        {
            new Report
            {
                Title = "6월 사료 200kg", Status = "집행 완료", Ok = true, HasCertificate = true,
                Body = "노원구 동물보호센터 · 7월 2일 수령\n집행액 480,000원 · 참여 1,240명",
                PhotoCaption = "수령 사진",
            },
            new Report
            {
                Title = "6월 방한용품 캠페인", Status = "목표 미달 종료", Ok = false,
                Body = "312/500벌로 종료 — 약정에 따라 브랜드가 200세트로 축소 집행했어요. 결과를 그대로 공개합니다.",
            },
        };

        public static string CarryoverAmount = "120,000원";
        public const string CarryoverNote = "다음 달 집행분에 합산돼요";

        // ---- E-04 기부 증서 ----
        // 명의는 주간 랭킹 1위. 랭킹은 과금 유래 뼈다귀를 제외하고 집계한다 (§5.5·§6.3) —
        // 증서에 그 사실을 같이 적는 것까지가 규칙이다.
        public static string CertPeriod  = "발랑 기부 증서 · 2026년 7월 3주";
        public static string CertHolder  = "멍멍이집사 님";
        public static string CertBody    = "전체 유저의 참여로 모인 <color=" + DonTheme.GoldInkHex + ">사료 200kg</color>이\n" +
                                           "노원구 동물보호센터에 전달되었습니다";
        public const string CertRule     = "명의는 주간 랭킹 1위에게 드립니다.\n" +
                                           "랭킹은 플레이·돌봄 점수만 집계합니다 (결제 전환분 제외)";
        public static string CertSponsor = "스폰서 · OO펫푸드";

        /// <summary>데모 빌드에서 실물이 나가지 않는 구간 — §6.5 에 따라 라벨을 항상 노출한다.</summary>
        public const bool MockMode = true;
        public const string MockNotice = "모의 기부 — 데모 빌드에서는 실물이 발송되지 않아요";

        /// <summary>1,000 처럼 세 자리마다 끊어서.</summary>
        public static string Num(int v) => v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
