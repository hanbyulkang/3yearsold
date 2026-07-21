using System.Collections.Generic;
using Recommend;
using TMPro;
using UnityEngine;

namespace Donation
{
    // E-01 ~ E-04 화면 구성. 문구·수치는 Desktop/donation.dc.html 시안 기준이고,
    // 재화 명칭만 D-019 에 맞춰 "P(포인트)" → "뼈다귀" 로 바꿨다.
    // 화면 하나당 메서드 하나 — 시안 프레임과 1:1로 대조하기 쉽게 둔다.
    //
    // 배치는 전부 RecCol 커서로 직접 계산한다 (LayoutGroup·ContentSizeFitter 안 씀 — RecUI 주석 참고).
    public static class DonScreens
    {
        // 내 기여 박스 — 금색 반투명 + 점선 (시안 rgba(242,193,78,.1) / rgba(226,164,54,.45))
        static readonly Color MineFill = new Color(242 / 255f, 193 / 255f, 78 / 255f, 0.10f);
        static readonly Color MineStroke = new Color(226 / 255f, 164 / 255f, 54 / 255f, 0.45f);

        public static void BuildAll(RecNav nav, Transform canvas)
        {
            E01Home(nav, canvas);
            E02Designate(nav, canvas);
            E03Report(nav, canvas);
            E04Certificate(nav, canvas);
        }

        // ==================== E-01 후원 홈 ====================
        static void E01Home(RecNav nav, Transform canvas)
        {
            // 시안 상단바 타일은 💝 지만 NotoSansKR 에 컬러 이모지 글리프가 없다.
            // 폰트가 가진 기호로 대체 — 아이콘 스프라이트가 나오면 교체할 자리.
            var f = nav.CreateFrame(canvas, "e01", "후원", false, "♥");
            DonUI.BonePill(f.AppBar, DonData.Bones);
            var col = f.Col;

            // ---- 이번 달 공동 창고 ----
            RecUI.Card(col, "Warehouse", inner =>
            {
                DonUI.HeadRow(inner, "이번 달 공동 창고", RecTheme.FsCardTitle,
                    (p, y) => RecUI.Pill(p, "Tag", "참여량 집계", RecTheme.PillFill, RecTheme.PillStroke,
                        RecTheme.Body, 0f, y + 5f));

                DonUI.ProgressBar(inner, DonData.WarehousePercent);

                // 게이지 라벨은 "모금액"이 아니라 참여량이다 (§6.1·§6.5) — 문구를 바꾸지 말 것
                DonUI.BetweenRow(inner,
                    $"전체 참여 {DonData.WarehousePercent}%", RecTheme.Fs(17f), RecTheme.GoldInk,
                    DonData.WarehouseGoal, RecTheme.FsSmall, RecTheme.Sub);

                RecUI.DashedBox(inner, "Mine", w =>
                {
                    RecUI.Para(w, "Text",
                        $"내 기여: <color={DonTheme.GoldInkHex}>{DonData.Num(DonData.MyContribution)} 뼈다귀</color>",
                        RecTheme.FsBody, RecTheme.Body);
                }, 12f, MineFill, MineStroke, 1.5f, 7f, 6f, 0f, 16f, 11f);
            }, 14f);

            // ---- 사료 기부하기 ----
            RecUI.Card(col, "Donate", inner =>
            {
                RecUI.Para(inner, "Title", "사료 기부하기", RecTheme.FsCardTitle, RecTheme.Ink, true);

                int picked = DonData.DefaultAmountIndex;
                TextMeshProUGUI ctaLabel = null;
                string Label(int i) => $"{DonData.Num(DonData.DonateAmounts[i])} 뼈다귀 기부";

                var chipRow = RecUI.Node("Amounts", inner.Parent);
                RecUI.SetRect(chipRow, 0f, inner.Y, inner.Width, 46f);

                // 칩 폭은 글자 길이에 맡기지 않고 3등분한다.
                // "3,000 뼈다귀"처럼 단위까지 붙으면 글자 폭 합이 카드 밖으로 나간다.
                const float chipGap = 10f;
                float chipW = (inner.Width - chipGap * 2f) / 3f;

                var chips = new List<RecChip>();
                for (int i = 0; i < DonData.DonateAmounts.Length; i++)
                {
                    var chip = RecUI.Chip(chipRow, "A" + i, $"{DonData.Num(DonData.DonateAmounts[i])} 뼈다귀",
                        i == picked, 0f, 0f, c =>
                        {
                            int idx = chips.IndexOf(c);
                            if (idx < 0) return;
                            picked = idx;
                            foreach (var other in chips) other.SetOn(other == c);
                            if (ctaLabel != null) ctaLabel.text = Label(idx);
                        }, 46f);
                    chips.Add(chip);
                    RecUI.SetRect((RectTransform)chip.transform, i * (chipW + chipGap), 0f, chipW, 46f);
                }
                inner.Advance(46f);

                // TODO(백엔드): 차감·적립은 Edge Function + 원장에서만 처리한다 (§5.5).
                // 잔액을 클라이언트에서 깎지 말 것 — 여기서는 요청만 보내고 갱신된 잔액을 받아 그린다.
                var cta = RecUI.GoldButton(inner, "Cta", Label(picked), null,
                    62f, RecTheme.FsBtnGoldCta, 16f, 3f, 6f);
                ctaLabel = DonUI.FitLabel(cta, RecTheme.Fs(16f));
            });

            // §6.5 정직성 규칙 — 이 라벨은 mock 구간에서 항상 보여야 한다
            if (DonData.MockMode) DonUI.HonestBanner(col, DonData.MockNotice);

            // ---- 하단 이동 버튼 2개 ----
            const float gap = 14f, h = 56f;
            float bw = (col.Width - gap) * 0.5f;
            RecUI.BrownButton(col.Parent, "Designate", "보호소 지정 후원", () => nav.Show("e02"),
                0f, col.Y, bw, h, RecTheme.Fs(19f), 0f, 16f, 4f);
            RecUI.BrownButton(col.Parent, "Report", "집행 내역", () => nav.Show("e03"),
                bw + gap, col.Y, bw, h, RecTheme.Fs(19f), 0f, 16f, 4f);
            col.Advance(h + 4f);

            RecNav.FinishFrame(f);
        }

        // ==================== E-02 지정 후원 ====================
        static void E02Designate(RecNav nav, Transform canvas)
        {
            var f = nav.CreateFrame(canvas, "e02", "지정 후원", true, null, 66f);
            DonUI.BonePill(f.AppBar, DonData.Bones);
            var col = f.Col;

            int selected = 0;
            string amount = "";
            var faces = new List<RecShape>();
            TextMeshProUGUI ctaLabel = null;

            // 배분할 뼈다귀를 아직 안 적었으면 금액을 말하지 않는다 — 없는 수치를 지어내지 않기 위해서다
            string CtaText()
            {
                string name = DonData.Targets[selected].Name;
                if (!int.TryParse(amount, out int v) || v <= 0) return $"{name}에게 배분하기";
                return $"{name}에게 {DonData.Num(v)} 뼈다귀 배분";
            }

            void Refresh()
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    bool on = i == selected;
                    if (on)
                    {
                        faces[i].SetFill(RecTheme.HlFill);
                        faces[i].SetBorder(3f, RecTheme.HlStroke);
                    }
                    else
                    {
                        faces[i].SetFill(RecTheme.White);
                        faces[i].SetBorder(2f, RecTheme.CardBorder);
                    }
                }
                if (ctaLabel != null) ctaLabel.text = CtaText();
            }

            for (int i = 0; i < DonData.Targets.Length; i++)
            {
                var t = DonData.Targets[i];
                int index = i;

                var card = RecUI.Card(col, "Target_" + t.Name, inner =>
                {
                    const float photo = 100f, tagH = 30f;

                    var tag = RecUI.Pill(inner.Parent, "Tag", t.Tag, RecTheme.PillFill, RecTheme.PillStroke,
                        RecTheme.Body, 0f, inner.Y);
                    float tagW = tag.sizeDelta.x;

                    float metaX = photo + 16f;
                    // 태그가 길어도 이름 칸이 사라지지 않게 최소 폭을 남긴다 (서버가 채우는 값이다)
                    float metaW = Mathf.Max(140f, inner.Width - metaX - tagW - 16f);

                    // "도봉구 보호소 전체"처럼 이름이 길면 두 줄이 된다.
                    // 행 높이를 사진 크기로 고정하면 넘친 줄이 아래 요소를 침범하므로 큰 쪽에 맞춘다.
                    var name = RecUI.Text("Name", inner.Parent, t.Name, RecTheme.FsDogName, RecTheme.Ink, true);
                    float nh = RecUI.MeasureH(name, metaW);
                    var sub = RecUI.Text("Sub", inner.Parent, t.Sub, RecTheme.FsBody, RecTheme.Sub);
                    float sh = RecUI.MeasureH(sub, metaW);

                    float metaH = nh + 6f + sh;
                    float rowH = Mathf.Max(photo, metaH);

                    var slot = RecUI.Slot(inner.Parent, "Photo", 0f, inner.Y + (rowH - photo) * 0.5f,
                        photo, photo, 16f, "사진", RecTheme.Fs(15f));
                    Backend.RemoteImage.Load(t.Photo, slot);   // 실패 시 "사진" 라벨 유지

                    tag.anchoredPosition = new Vector2(inner.Width - tagW, -(inner.Y + (rowH - tagH) * 0.5f));

                    float metaY = inner.Y + (rowH - metaH) * 0.5f;
                    RecUI.SetRect(name.rectTransform, metaX, metaY, metaW, nh);
                    RecUI.SetRect(sub.rectTransform, metaX, metaY + nh + 6f, metaW, sh);

                    inner.Advance(rowH);
                }, 16f, 22f, 20f);

                DonUI.Touch(card, () => { selected = index; Refresh(); });

                var face = card.Find("Face");
                if (face != null) faces.Add(face.GetComponent<RecShape>());
            }

            DonUI.NumberField(col, "배분할 뼈다귀", $"예: {DonData.Num(DonData.DefaultAllocation)}", "뼈다귀",
                v => { amount = v; if (ctaLabel != null) ctaLabel.text = CtaText(); });

            RecUI.Para(col, "Rotation", DonData.RotationNote, RecTheme.Fs(14.5f), RecTheme.Caption,
                false, RecTheme.LineNormal);

            // 하단 고정 CTA.
            // TODO(백엔드): 배분 트랜잭션도 Edge Function + 원장 (§5.5).
            // 뼈다귀는 재원이 아니라 분배 의사다 — 집행액은 판매·과금 재원에서 순환 배분으로 산정한다 (§6.1).
            var cta = RecUI.GoldButton(f.Footer, "Allocate", CtaText(), null,
                0f, 0f, RecTheme.FrameW - RecTheme.Pad * 2f, 60f, RecTheme.FsBtnGoldCta, 18f, 3f, 6f);
            ctaLabel = DonUI.FitLabel(cta, RecTheme.Fs(15f));

            Refresh();
            RecNav.FinishFrame(f);
        }

        // ==================== E-03 집행 내역 ====================
        static void E03Report(RecNav nav, Transform canvas)
        {
            var f = nav.CreateFrame(canvas, "e03", "기부 집행 내역", true);
            var col = f.Col;

            foreach (var r in DonData.Reports)
            {
                var report = r;
                RecUI.Card(col, "Report_" + report.Title, inner =>
                {
                    DonUI.HeadRow(inner, report.Title, RecTheme.FsCardTitle,
                        (p, y) => DonUI.StatusPill(p, report.Status, report.Ok, 0f, y + 4f));

                    if (string.IsNullOrEmpty(report.PhotoCaption))
                    {
                        RecUI.Para(inner, "Body", report.Body, RecTheme.FsBody, RecTheme.Sub,
                            false, RecTheme.LineLoose);
                    }
                    else
                    {
                        // 보호소가 올린 수령 확인 사진 (§6.4) — 아직 없으면 자리만 보여준다
                        const float pw = 130f, ph = 96f;
                        var slot = RecUI.Slot(inner.Parent, "Photo", 0f, inner.Y, pw, ph, 14f,
                            report.PhotoCaption, RecTheme.Fs(14f));
                        Backend.RemoteImage.Load(report.Photo, slot);

                        float textX = pw + 16f;
                        float textW = inner.Width - textX;
                        var body = RecUI.Text("Body", inner.Parent, report.Body, RecTheme.FsBody, RecTheme.Sub,
                            false, TextAlignmentOptions.TopLeft, RecTheme.LineLoose);
                        float bh = RecUI.MeasureH(body, textW);
                        RecUI.SetRect(body.rectTransform, textX, inner.Y + Mathf.Max(0f, (ph - bh) * 0.5f), textW, bh);

                        inner.Advance(Mathf.Max(ph, bh));
                    }

                    // 시안에는 증서로 가는 입구가 없다. E-04 는 집행이 끝난 건에서 열리는 화면이라
                    // 집행 완료 카드에 진입점을 붙였다 (§6.5 폐루프의 마지막 단계).
                    if (report.HasCertificate)
                    {
                        RecUI.BrownButton(inner.Parent, "Cert", "기부 증서 보기", () => nav.Show("e04"),
                            0f, inner.Y, 0f, 48f, RecTheme.Fs(17f), 22f);
                        inner.Advance(45f);
                    }
                }, 14f, 24f, 20f);
            }

            // 미집행 이월분 (svg-donate/carryover-card.svg) — §6.5 전건 공개에 포함된다
            RecUI.DashedBox(col, "Carryover", inner =>
            {
                DonUI.BetweenRow(inner, "미집행 이월분", RecTheme.Fs(19f), RecTheme.Body,
                    DonData.CarryoverAmount, RecTheme.Fs(21f), RecTheme.GoldInk, 32f);
                RecUI.Para(inner, "Note", DonData.CarryoverNote, RecTheme.FsSmall, RecTheme.Caption);
            }, RecTheme.Radius, RecTheme.NoteFill, RecTheme.NoteStroke, 2f, 9f, 7f, 8f, 24f, 20f);

            RecNav.FinishFrame(f);
        }

        // ==================== E-04 기부 증서 ====================
        static void E04Certificate(RecNav nav, Transform canvas)
        {
            var f = nav.CreateFrame(canvas, "e04", "기부 증서", true);
            var col = f.Col;

            // 상단바 오른쪽 공유 버튼.
            // TODO: 공유 이미지는 서버에서 렌더한다 (위변조 방지) — 클라이언트 캡처를 쓰지 말 것.
            var share = RecUI.BrownButton(f.AppBar, "Share", "공유", null, 0f, (RecNav.BarH - 41f) * 0.5f,
                0f, 38f, RecTheme.FsBtnBrown, 20f);
            share.anchoredPosition = new Vector2(f.AppBar.sizeDelta.x - share.sizeDelta.x - 20f,
                share.anchoredPosition.y);

            // 시안의 증서는 686 프레임 안에서 560 폭이다 — 좌우로 여백을 두어 종이처럼 보이게 한다
            float certW = col.Width - 78f;
            const float seal = 76f;

            var cert = DonUI.Certificate(col.Parent, (col.Width - certW) * 0.5f, col.Y, certW, inner =>
            {
                DonUI.Seal(inner.Parent, (inner.Width - seal) * 0.5f, inner.Y, seal, "증서");
                inner.Advance(seal + 4f);

                RecUI.Para(inner, "Period", DonData.CertPeriod, RecTheme.FsSmall, RecTheme.Caption,
                    false, RecTheme.LineTight, TextAlignmentOptions.Top);
                RecUI.Para(inner, "Holder", DonData.CertHolder, RecTheme.Fs(34f), RecTheme.Ink,
                    true, RecTheme.LineTight, TextAlignmentOptions.Top);
                RecUI.Para(inner, "Body", DonData.CertBody, RecTheme.Fs(17f), RecTheme.Body,
                    false, RecTheme.LineLoose, TextAlignmentOptions.Top);

                // 시안의 70% 폭 구분선
                var line = RecUI.Shape("Divider", inner.Parent);
                line.Radius = 0f;
                line.SetFill(DonTheme.CertInner);
                RecUI.SetRect(line.rectTransform, inner.Width * 0.15f, inner.Y + 8f, inner.Width * 0.7f, 2f);
                inner.Advance(18f);

                // 명의 산정 규칙을 증서에 그대로 적는다 (§5.5·§6.3 — 명예를 돈으로 살 수 없다는 사실)
                RecUI.Para(inner, "Rule", DonData.CertRule, RecTheme.FsTiny, RecTheme.Caption,
                    false, RecTheme.LineLoose, TextAlignmentOptions.Top);

                var sponsor = RecUI.Pill(inner.Parent, "Sponsor", DonData.CertSponsor,
                    RecTheme.PillFill, RecTheme.PillStroke, RecTheme.Body, 0f, inner.Y + 6f);
                sponsor.anchoredPosition = new Vector2((inner.Width - sponsor.sizeDelta.x) * 0.5f,
                    sponsor.anchoredPosition.y);
                inner.Advance(36f);
            });

            // 시안은 증서를 본문 가운데에 띄운다. 본문 높이는 기기마다 다르므로 남는 공간을 반씩 나눈다.
            float h = cert.sizeDelta.y;
            float top = Mathf.Max(0f, (f.ViewportH - h) * 0.5f);
            cert.anchoredPosition = new Vector2(cert.anchoredPosition.x, -top);
            col.Y = top + h + col.Gap;

            RecNav.FinishFrame(f);
        }
    }
}
