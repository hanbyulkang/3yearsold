using UnityEngine;

namespace Donation
{
    // E. 후원 화면에만 나오는 디자인 토큰.
    // 원본: Desktop/donation.dc.html + svg-donate/ (design-doc canvas, 프레임 686×1220).
    //
    // 바탕·글자·금색·갈색처럼 D 추천 루프와 같은 값은 RecTheme 을 그대로 쓴다.
    // 두 시안이 같은 디자인 시스템에서 나왔기 때문에 색을 복제하면 한쪽만 바뀌었을 때 어긋난다.
    // 여기에는 후원에만 있는 것 — 진행 막대·상태 pill·정직성 배너·기부 증서 — 만 둔다.
    public static class DonTheme
    {
        // ---- 진행 막대 (svg-donate/progress-track.svg) ----
        public static readonly Color TrackFill   = Rgba(90, 58, 32, 0.15f);
        public static readonly Color TrackStroke = Rgba(90, 58, 32, 0.25f);
        public const float TrackH = 26f;

        // ---- 상태 pill ----
        // 집행 완료 (status-pill-green.svg)
        public static readonly Color OkTop    = Hex("8FCE5A");
        public static readonly Color OkBottom = Hex("61A532");
        public static readonly Color OkBorder = Hex("3C6E1A");
        // 목표 미달 종료 (status-pill-red.svg)
        public static readonly Color WarnTop    = Hex("E4796C");
        public static readonly Color WarnBottom = Hex("C05A4E");
        public static readonly Color WarnBorder = Hex("93433A");
        // pill 위쪽 광택 띠
        public static readonly Color GlossLight = Rgba(255, 255, 255, 0.35f);

        // ---- 정직성 배너 (notice-banner-red.svg) ----
        // §6.5 "모의 기부" 라벨 — DONATION_MODE=mock 구간에서 숨기지 않는다.
        public static readonly Color HonestFill   = Rgba(228, 91, 79, 0.08f);
        public static readonly Color HonestStroke = Rgba(228, 91, 79, 0.40f);
        public static readonly Color HonestInk    = Hex("C05A4E");

        // ---- 재화 HUD pill (point-pill-dark.svg) ----
        public static readonly Color HudFill   = Rgba(0, 0, 0, 0.32f);
        public static readonly Color HudStroke = Rgba(242, 193, 78, 0.35f);
        public static readonly Color HudInk    = Hex("F2C14E");

        // ---- 기부 증서 (certificate-frame.svg · certificate-seal.svg) ----
        public static readonly Color CertTop     = Hex("FFFDF6");
        public static readonly Color CertBottom  = Hex("FBF3DF");
        public static readonly Color CertBorder  = Hex("D9B96A");
        public static readonly Color CertInner   = Rgba(217, 185, 106, 0.40f);
        public static readonly Color CertShadow  = Rgba(138, 90, 52, 0.25f);
        public static readonly Color SealRing    = Rgba(164, 105, 15, 0.40f);

        // ---- 강조 글자색 ----
        // 굵기로 강조하지 않는다 (폰트가 Black 하나뿐 — RecUI 주석 참고). 색으로만 준다.
        public const string GoldInkHex = "#B8762A";

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
