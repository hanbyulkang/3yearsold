using UnityEngine;

namespace Recommend
{
    // D. 추천 루프 디자인 토큰.
    // 원본: Desktop/recomend.html (design-doc canvas, 프레임 686×1220).
    // 캔버스 referenceResolution 을 686×1220 으로 두므로 아래 수치는 시안의 px 값 그대로다.
    public static class RecTheme
    {
        // ---- 프레임 ----
        public const float FrameW = 686f;
        public const float FrameH = 1220f;
        public const float Pad = 24f;      // screen padding
        public const float Gap = 16f;      // 기본 세로 간격
        public const float GapWide = 18f;  // D-01·D-02

        public const float Radius = 22f;

        // ---- 바탕 ----
        public static readonly Color Cream      = Hex("FFF9EC");
        public static readonly Color White      = Hex("FFFFFF");
        public static readonly Color CardBorder = Hex("EDE4D2");
        public static readonly Color Divider    = Hex("F2EBDC");

        // ---- 글자 ----
        public static readonly Color Ink     = Hex("5A4632"); // 제목
        public static readonly Color Body    = Hex("6A583F"); // 본문
        public static readonly Color Sub     = Hex("8A7A62"); // 보조
        public static readonly Color Caption = Hex("A9946F"); // 캡션
        public static readonly Color GoldInk = Hex("B8762A"); // 금색 글자
        public static readonly Color OnDark  = Hex("FFEFD2"); // 어두운 바 위
        public static readonly Color Placeholder = Hex("B9A88E");

        // ---- 금색 (주요 액션) ----
        public static readonly Color GoldTop    = Hex("FFD769");
        public static readonly Color GoldBottom = Hex("F0A832");
        public static readonly Color GoldBorder = Hex("A4690F");
        public static readonly Color GoldDeep   = Hex("7D4F0A"); // 눌림 그림자
        public static readonly Color GoldText   = Hex("4A3113");

        // ---- 갈색 (보조 액션·상단바) ----
        public static readonly Color BrownTop    = Hex("6B543D");
        public static readonly Color BrownBottom = Hex("54402C");
        public static readonly Color BrownBorder = Hex("2B1F13");
        public static readonly Color BrownDeep   = Hex("241A11");

        // 상단바는 살짝 더 어두운 갈색 그라데이션
        public static readonly Color BarTop    = Hex("4A3A2C");
        public static readonly Color BarBottom = Hex("3B2D21");
        // 상단바 안 아이콘 타일
        public static readonly Color TileTop    = Hex("5C4936");
        public static readonly Color TileBottom = Hex("4A3826");

        // ---- AI 영역 (금색 점선) ----
        public static readonly Color AiFill    = Rgba(242, 193, 78, 0.10f);
        public static readonly Color AiStroke  = Rgba(226, 164, 54, 0.55f);
        // D-04 '지금 추천' 강조 카드
        public static readonly Color HlFill    = Rgba(242, 193, 78, 0.12f);
        public static readonly Color HlStroke  = Rgba(226, 164, 54, 0.60f);

        // ---- 사진 자리·점선 안내 박스 ----
        public static readonly Color SlotFill   = Rgba(176, 123, 79, 0.10f);
        public static readonly Color SlotStroke = Rgba(176, 123, 79, 0.40f);
        public static readonly Color NoteFill   = Rgba(176, 123, 79, 0.08f);
        public static readonly Color NoteStroke = Rgba(176, 123, 79, 0.30f);
        // 정보 pill
        public static readonly Color PillFill   = Rgba(176, 123, 79, 0.12f);
        public static readonly Color PillStroke = Rgba(176, 123, 79, 0.35f);

        // 카드 그림자 (0 4px 12px rgba(138,90,52,.12))
        public static readonly Color CardShadow = Rgba(138, 90, 52, 0.12f);
        // 상단바 안쪽 하이라이트
        public static readonly Color BarInner   = Rgba(255, 236, 200, 0.18f);

        // ---- 글자 크기 ----
        //
        // 시안(웹 686px 캔버스)의 px 값을 그대로 쓰면 실기기에서 너무 작다.
        // 메인(MG1)은 referenceResolution 393 기준 19~28px 이라, 686 기준으로 환산하면
        // 33~49px 이다. 시안 값(16~17px)은 그 절반 수준이라 눈에 띄게 작아 보인다.
        // 전역 배율 하나로 맞춘다 — 비율은 시안 그대로 두고 크기만 올린다.
        public const float TypeScale = 1.5f;

        /// <summary>시안 px 을 실제 글자 크기로 환산. 코드에 박힌 크기도 전부 이걸 거친다.</summary>
        public static float Fs(float designPx) => designPx * TypeScale;

        public const float FsAppBar     = 26f * TypeScale;
        public const float FsAppBarLg   = 28f * TypeScale;
        public const float FsCardTitle  = 22f * TypeScale;
        public const float FsCardTitleLg= 24f * TypeScale;
        public const float FsDogName    = 24f * TypeScale;
        public const float FsAiCap      = 18f * TypeScale;
        public const float FsAiText     = 17f * TypeScale;
        public const float FsBody       = 16f * TypeScale;
        public const float FsSub        = 16f * TypeScale;
        public const float FsSmall      = 15f * TypeScale;
        public const float FsTiny       = 14f * TypeScale;
        public const float FsCaption    = 13.5f * TypeScale;
        public const float FsBtnGold    = 21f * TypeScale;
        public const float FsBtnGoldCta = 23f * TypeScale;
        public const float FsBtnBrown   = 16f * TypeScale;

        // 시안의 line-height 는 1.6~1.75 — TMP 는 배수를 % 로 받는다.
        public const float LineTight  = 1.0f;
        public const float LineNormal = 1.65f;
        public const float LineLoose  = 1.75f;

        static Color Hex(string h)
        {
            return new Color(
                int.Parse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                1f);
        }

        static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }
    }
}
