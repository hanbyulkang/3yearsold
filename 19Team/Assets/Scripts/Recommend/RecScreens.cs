using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Recommend
{
    // D-01 ~ D-06 화면 구성. 문구·수치는 Desktop/recomend.html 시안 그대로.
    // 화면 하나당 메서드 하나 — 시안 프레임과 1:1로 대조하기 쉽게 둔다.
    //
    // 배치는 전부 RecCol 커서로 직접 계산한다 (LayoutGroup·ContentSizeFitter 안 씀 — RecUI 주석 참고).
    public static class RecScreens
    {
        public static void BuildAll(RecNav nav, Transform canvas)
        {
            D01Home(nav, canvas);
            D02DogList(nav, canvas);
            D03DogDetail(nav, canvas);
            D04Ways(nav, canvas);
            D05Apply(nav, canvas);
            D06Survey(nav, canvas);
        }

        // ==================== D-01 추천 홈 ====================
        static void D01Home(RecNav nav, Transform canvas)
        {
            // 시안은 상단바 타일에 💛 를 쓰지만 NotoSansKR 에 컬러 이모지 글리프가 없다.
            // 폰트가 가진 기호로 대체 — 아이콘 스프라이트가 나오면 교체할 자리.
            var f = nav.CreateFrame(canvas, "d01", "추천", false, "♥", 0f, RecTheme.GapWide);
            var col = f.Col;

            // §4.4 — 다음 한 걸음은 항상 1개만
            RecUI.DashedBox(col, "NextStep", inner =>
            {
                RecUI.Para(inner, "Cap", "AI · 다음 한 걸음", RecTheme.FsAiCap, RecTheme.GoldInk, true);
                RecUI.Para(inner, "Text", RecData.NextStepText, RecTheme.FsAiText, RecTheme.Body, false, RecTheme.LineNormal);
                RecUI.GoldButton(inner, "Cta", "봉사 신청 보기", () => nav.Show("d05"));
            }, RecTheme.Radius, RecTheme.AiFill, RecTheme.AiStroke, 2.5f, 9f, 7f);

            // 나에게 맞는 보호견
            RectTransform grid = null;
            var photoSlots = new System.Collections.Generic.List<RectTransform>();
            var dogsCard = RecUI.Card(col, "DogsCard", inner =>
            {
                Head(inner, "나에게 맞는 보호견", RecTheme.FsCardTitle, "전체", () => nav.Show("d02"));

                // 3열 사진 그리드
                const float gap = 14f;
                const float photoH = 120f, capH = 34f;
                float cellW = (inner.Width - gap * 2f) / 3f;
                grid = RecUI.Node("Grid", inner.Parent);
                RecUI.SetRect(grid, 0f, inner.Y, inner.Width, photoH + 8f + capH);
                for (int i = 0; i < RecData.HomeDogs.Length; i++)
                {
                    float x = i * (cellW + gap);
                    var slot = RecUI.Slot(grid, "Photo", x, 0f, cellW, photoH, 14f, "사진", RecTheme.Fs(15f));
                    Touch(slot, () => nav.Show("d03"));
                    photoSlots.Add(slot);
                    var cap = RecUI.Text("Cap", grid, RecData.HomeDogs[i].Caption, RecTheme.FsTiny,
                        RecTheme.Sub, false, TextAlignmentOptions.Top);
                    RecUI.SetRect(cap.rectTransform, x, photoH + 8f, cellW, capH);
                }
                inner.Advance(photoH + 8f + capH);
            });

            // 참여 방법 알아보기
            RecUI.Card(col, "WaysCard", inner =>
            {
                // 버튼을 먼저 만들어 실제 폭을 재고 본문 폭을 정한다.
                // 폭을 미리 가정하면 글자 크기가 바뀔 때 본문이 버튼 밑으로 파고든다.
                var btn = RecUI.BrownButton(inner.Parent, "See", "보기", () => nav.Show("d04"), 0f, 0f);
                float btnW = btn.sizeDelta.x;
                float textW = inner.Width - btnW - 16f;

                var title = RecUI.Text("Title", inner.Parent, "참여 방법 알아보기",
                    RecTheme.FsCardTitle, RecTheme.Ink, true);
                float th = RecUI.MeasureH(title, textW);
                RecUI.SetRect(title.rectTransform, 0f, inner.Y, textW, th);

                var sub = RecUI.Text("Sub", inner.Parent, "후원 · 봉사 · 임시보호 · 입양 — 어느 단계에 있어도 괜찮아요",
                    RecTheme.FsSmall, RecTheme.Sub, false, TextAlignmentOptions.TopLeft, RecTheme.LineNormal);
                float sh = RecUI.MeasureH(sub, textW);
                RecUI.SetRect(sub.rectTransform, 0f, inner.Y + th + 8f, textW, sh);

                float blockH = th + 8f + sh;
                RecUI.SetRect(btn, inner.Width - btnW, inner.Y + (blockH - btn.sizeDelta.y) * 0.5f,
                    btnW, btn.sizeDelta.y);
                inner.Advance(blockH);
            }, 0f);

            // 남는 세로 공간은 보호견 사진이 흡수한다 (카드 → 그리드 → 사진 3개).
            // 사진이 세로로 길쭉해지지 않게 폭의 1.2배까지만 늘린다.
            var chain = new System.Collections.Generic.List<RectTransform> { dogsCard, grid };
            chain.AddRange(photoSlots);
            float cell = (col.Width - 48f - 28f) / 3f;
            RecNav.GrowToFill(f, cell * 1.2f - 120f, chain.ToArray());
        }

        // ==================== D-02 보호견 추천 목록 ====================
        static void D02DogList(RecNav nav, Transform canvas)
        {
            var f = nav.CreateFrame(canvas, "d02", "나에게 맞는 보호견", true, null, 0f, RecTheme.GapWide);
            var col = f.Col;

            // 필터 칩 — 하나만 켜진다
            var chiprow = RecUI.Node("Chips", col.Parent);
            RecUI.SetRect(chiprow, 0f, col.Y, col.Width, 42f);
            var chips = new System.Collections.Generic.List<RecChip>();
            void Pick(RecChip picked) { foreach (var c in chips) c.SetOn(c == picked); }
            string[] labels = { "지역 선택", "서울 노원구", "전체" };
            float cx = 0f;
            for (int i = 0; i < labels.Length; i++)
            {
                var chip = RecUI.Chip(chiprow, "C" + i, labels[i], i == 1, cx, 0f, Pick);
                chips.Add(chip);
                cx += ((RectTransform)chip.transform).sizeDelta.x + 10f;
            }
            col.Advance(42f);

            foreach (var d in RecData.ListDogs)
            {
                var card = RecUI.Card(col, "Dog_" + d.Name, inner =>
                {
                    const float photo = 110f;
                    var slot = RecUI.Slot(inner.Parent, "Photo", 0f, inner.Y, photo, photo, 16f, "사진", RecTheme.Fs(15f));

                    float metaX = photo + 16f;
                    var pill = RecUI.Pill(inner.Parent, "Region", d.Region, RecTheme.PillFill, RecTheme.PillStroke,
                        RecTheme.Body, 0f, inner.Y + (photo - 30f) * 0.5f);
                    float pillW = pill.sizeDelta.x;
                    pill.anchoredPosition = new Vector2(inner.Width - pillW, pill.anchoredPosition.y);

                    float metaW = inner.Width - metaX - pillW - 16f;
                    var name = RecUI.Text("Name", inner.Parent, d.Name, RecTheme.FsDogName, RecTheme.Ink, true);
                    float nh = RecUI.MeasureH(name, metaW);
                    var desc = RecUI.Text("Desc", inner.Parent, d.Desc, RecTheme.FsBody, RecTheme.Sub);
                    float dh = RecUI.MeasureH(desc, metaW);
                    float metaH = nh + 6f + dh;
                    float metaY = inner.Y + (photo - metaH) * 0.5f;
                    RecUI.SetRect(name.rectTransform, metaX, metaY, metaW, nh);
                    RecUI.SetRect(desc.rectTransform, metaX, metaY + nh + 6f, metaW, dh);

                    inner.Advance(photo);

                    // 왜 추천하나요 — 카드 안 작은 AI 박스
                    RecUI.DashedBox(inner, "Why", w =>
                    {
                        RecUI.Para(w, "Cap", "왜 추천하나요", RecTheme.Fs(16f), RecTheme.GoldInk, true);
                        RecUI.Para(w, "Text", d.Reason, RecTheme.FsBody, RecTheme.Body, false, RecTheme.LineNormal);
                    }, 16f, RecTheme.AiFill, RecTheme.AiStroke, 2f, 9f, 7f, 6f, 18f, 14f);
                }, 16f, 22f, 20f);

                Touch(card, () => nav.Show("d03"));
            }

            // 빈 상태
            RecUI.DashedBox(col, "Empty", inner =>
            {
                RecUI.Para(inner, "Text", "선택한 지역에 더 없어요 — 필터를 풀면 12마리를 더 볼 수 있어요",
                    RecTheme.FsBody, RecTheme.Sub, false, RecTheme.LineNormal, TextAlignmentOptions.Top);
                RecUI.BrownButton(inner.Parent, "All", "전체 보기", null, 0f, inner.Y, inner.Width, 52f, RecTheme.Fs(19f), 0f, 14f, 4f);
                inner.Advance(50f);
            }, RecTheme.Radius, RecTheme.NoteFill, RecTheme.NoteStroke, 2f, 9f, 7f, 14f, 24f, 20f);

            RecNav.FinishFrame(f);
        }

        // ==================== D-03 보호견 상세 ====================
        static void D03DogDetail(RecNav nav, Transform canvas)
        {
            var f = nav.CreateFrame(canvas, "d03", "보리", true);
            var col = f.Col;

            var photo = RecUI.Slot(col.Parent, "Photo", 0f, col.Y, col.Width, 330f, RecTheme.Radius, null, 0f);
            // 시안: 사진 자리 가운데 크림색 라벨
            var label = RecUI.Node("Label", photo);
            var ls = RecUI.AddShape(label.gameObject);
            ls.raycastTarget = false;
            ls.Radius = 12f;
            ls.SetFill(RecTheme.Cream);
            ls.SetBorder(2f, new Color(226 / 255f, 164 / 255f, 54 / 255f, 0.4f));
            var lt = RecUI.Text("Text", label, "보호소 보유 사진", RecTheme.Fs(16f), RecTheme.GoldInk, true, TextAlignmentOptions.Center);
            RecUI.Stretch(lt.rectTransform);
            float lw = RecUI.MeasureW(lt) + 32f;
            // 사진 영역은 남는 공간만큼 늘어난다 — 라벨은 가운데 앵커로 두어 따라오게 한다
            label.anchorMin = label.anchorMax = label.pivot = new Vector2(0.5f, 0.5f);
            label.sizeDelta = new Vector2(lw, 44f);
            label.anchoredPosition = Vector2.zero;
            col.Advance(330f);

            TagFlow(col, RecData.DetailTags, 8f);

            RecUI.DashedBox(col, "Intro", inner =>
            {
                RecUI.Para(inner, "Cap", "AI가 소개해요", RecTheme.FsAiCap, RecTheme.GoldInk, true);
                RecUI.Para(inner, "Text", RecData.DetailIntro, RecTheme.FsBody, RecTheme.Body, false, RecTheme.LineLoose);
                RecUI.Para(inner, "Src", "보호소 공고 데이터를 바탕으로 당신의 설문에 맞춰 작성했어요",
                    RecTheme.FsCaption, RecTheme.Caption);
            }, RecTheme.Radius, RecTheme.AiFill, RecTheme.AiStroke, 2.5f, 9f, 7f, 10f, 24f, 20f);

            RecUI.Card(col, "Shelter", inner =>
            {
                for (int i = 0; i < RecData.ShelterRows.Length; i++)
                    RecUI.KvRow(inner, RecData.ShelterRows[i].K, RecData.ShelterRows[i].V,
                        i < RecData.ShelterRows.Length - 1);
            }, 0f, 24f, 8f);

            // 남는 세로 공간은 대표 사진이 흡수한다 (4:3 정도까지만)
            RecNav.GrowToFill(f, col.Width * 0.75f - 330f, photo);
        }

        // ==================== D-04 참여 방식 추천 ====================
        static void D04Ways(RecNav nav, Transform canvas)
        {
            var f = nav.CreateFrame(canvas, "d04", "참여 방법", true);
            var col = f.Col;

            // 지금 추천 — 강조 카드
            RecUI.SolidBox(col, "Volunteer", inner =>
            {
                var title = RecUI.Text("Title", inner.Parent, "봉사", RecTheme.FsCardTitleLg, RecTheme.Ink,
                    true, TextAlignmentOptions.MidlineLeft);
                RecUI.SetRect(title.rectTransform, 0f, inner.Y, inner.Width * 0.5f, 34f);
                var pill = RecUI.Pill(inner.Parent, "Now", "지금 추천", RecTheme.GoldTop, RecTheme.GoldBorder,
                    RecTheme.GoldText, 0f, inner.Y + 2f, RecTheme.FsTiny, 14f, 30f, true);
                pill.anchoredPosition = new Vector2(inner.Width - pill.sizeDelta.x, pill.anchoredPosition.y);
                inner.Advance(34f);

                RecUI.Para(inner, "Text", "주 1회, 보호소에서 산책·청소를 도와요. 매일 산책을 거르지 않는 당신에게 잘 맞아요.",
                    RecTheme.FsBody, RecTheme.Body, false, RecTheme.LineNormal);
                RecUI.GoldButton(inner, "Apply", "신청하기", () => nav.Show("d05"));
            }, RecTheme.Radius, RecTheme.HlFill, RecTheme.HlStroke, 3f);

            SimpleWay(col, "후원", "뼈다귀나 물품으로 지금 바로 참여할 수 있어요");
            SimpleWay(col, "임시보호", "입양 전, 정해진 기간 동안 집에서 돌봐요. 사료·병원비는 보호소가 부담해요");

            // 입양 — 준비 확인이 필요한 단계
            RecUI.Card(col, "Adopt", inner =>
            {
                var title = RecUI.Text("Title", inner.Parent, "입양", RecTheme.FsCardTitle, RecTheme.Ink,
                    true, TextAlignmentOptions.MidlineLeft);
                RecUI.SetRect(title.rectTransform, 0f, inner.Y, inner.Width * 0.5f, 32f);
                var pill = RecUI.Pill(inner.Parent, "Need", "준비 확인 필요", RecTheme.PillFill, RecTheme.PillStroke,
                    RecTheme.Sub, 0f, inner.Y + 1f);
                pill.anchoredPosition = new Vector2(inner.Width - pill.sizeDelta.x, pill.anchoredPosition.y);
                inner.Advance(32f);

                RecUI.Para(inner, "Text", "평생을 함께하는 결정이라 절차를 쉽게 만들지 않아요. 준비 상태를 먼저 같이 확인해요.",
                    RecTheme.FsBody, RecTheme.Sub, false, RecTheme.LineNormal);

                RecUI.BrownButton(inner.Parent, "Check", "준비 상태 확인하기", null, 0f, inner.Y, 0f, 50f, RecTheme.Fs(17f), 22f);
                inner.Advance(47f);
            }, 12f, 24f, 20f);

            RecNav.FinishFrame(f);
        }

        static void SimpleWay(RecCol col, string title, string sub)
        {
            RecUI.Card(col, "Way_" + title, inner =>
            {
                RecUI.Para(inner, "Title", title, RecTheme.FsCardTitle, RecTheme.Ink, true);
                RecUI.Para(inner, "Sub", sub, RecTheme.FsBody, RecTheme.Sub, false, 1.6f);
            }, 8f, 24f, 20f);
        }

        // ==================== D-05 봉사 신청 ====================
        static void D05Apply(RecNav nav, Transform canvas)
        {
            var f = nav.CreateFrame(canvas, "d05", "봉사 신청", true, null, 66f);
            var col = f.Col;

            RecUI.Card(col, "Summary", inner =>
            {
                for (int i = 0; i < RecData.ApplyRows.Length; i++)
                    RecUI.KvRow(inner, RecData.ApplyRows[i].K, RecData.ApplyRows[i].V,
                        i < RecData.ApplyRows.Length - 1);
            }, 0f, 24f, 8f);

            foreach (var fld in RecData.ApplyFields)
                InputField(col, fld.K, fld.V);

            RecUI.DashedBox(col, "Note", inner =>
            {
                RecUI.Para(inner, "Text",
                    "신청하면 보호소에서 1~2일 내에 연락드려요. 준비물은 편한 옷과 운동화면 충분해요.",
                    RecTheme.FsSmall, RecTheme.Sub, false, RecTheme.LineNormal);
            }, 14f, RecTheme.NoteFill, RecTheme.NoteStroke, 1.5f, 7f, 6f, 0f, 16f, 12f);

            // 하단 고정 CTA
            RecUI.GoldButton(f.Footer, "Submit", "신청하기", null,
                0f, 0f, RecTheme.FrameW - RecTheme.Pad * 2f, 60f, RecTheme.FsBtnGoldCta, 18f, 3f, 6f);

            RecNav.FinishFrame(f);
        }

        static void InputField(RecCol col, string label, string placeholder)
        {
            var lt = RecUI.Text("Label_" + label, col.Parent, label, RecTheme.FsAiCap, RecTheme.Ink, true);
            float lh = RecUI.MeasureH(lt, col.Width);
            RecUI.SetRect(lt.rectTransform, 0f, col.Y, col.Width, lh);

            var box = RecUI.Node("Field_" + label, col.Parent);
            var bs = RecUI.AddShape(box.gameObject);
            bs.raycastTarget = true;
            bs.Radius = 16f;
            bs.SetFill(RecTheme.White);
            bs.SetBorder(2f, RecTheme.CardBorder);
            RecUI.SetRect(box, 0f, col.Y + lh + 8f, col.Width, 58f);

            var area = RecUI.Node("TextArea", box);
            RecUI.Stretch(area, 18f, 18f, 0f, 0f);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = RecUI.Text("Placeholder", area, placeholder, RecTheme.Fs(17f), RecTheme.Placeholder,
                false, TextAlignmentOptions.MidlineLeft);
            RecUI.Stretch(ph.rectTransform);

            var txt = RecUI.Text("Text", area, "", RecTheme.Fs(17f), RecTheme.Ink, false, TextAlignmentOptions.MidlineLeft);
            RecUI.Stretch(txt.rectTransform);

            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.targetGraphic = bs;
            input.transition = Selectable.Transition.None;

            col.Advance(lh + 8f + 58f);
        }

        // ==================== D-06 내 설문 ====================
        static void D06Survey(RecNav nav, Transform canvas)
        {
            var f = nav.CreateFrame(canvas, "d06", "내 설문", true);
            var col = f.Col;

            RecUI.Para(col, "Hint", "여기 답변이 견종·보호견·참여 추천에 그대로 쓰여요. 여건이 바뀌면 고쳐주세요.",
                RecTheme.FsBody, RecTheme.Sub, false, RecTheme.LineNormal);

            RecUI.Card(col, "Survey", inner =>
            {
                const float btnW = 66f;
                for (int i = 0; i < RecData.SurveyRows.Length; i++)
                {
                    var q = RecData.SurveyRows[i];
                    float metaW = inner.Width - btnW - 16f;

                    var lab = RecUI.Text("L" + i, inner.Parent, q.Label, RecTheme.FsTiny, RecTheme.Caption);
                    float lh = RecUI.MeasureH(lab, metaW);
                    var ans = RecUI.Text("A" + i, inner.Parent, q.Answer, RecTheme.FsAiCap, RecTheme.Ink, true);
                    float ah = RecUI.MeasureH(ans, metaW);

                    float rowH = lh + 5f + ah + 32f;   // 위아래 패딩 16씩
                    RecUI.SetRect(lab.rectTransform, 0f, inner.Y + 16f, metaW, lh);
                    RecUI.SetRect(ans.rectTransform, 0f, inner.Y + 16f + lh + 5f, metaW, ah);

                    RecUI.BrownButton(inner.Parent, "Edit" + i, "수정", null,
                        inner.Width - btnW, inner.Y + (rowH - 41f) * 0.5f, btnW, 38f, 15f, 16f, 11f);

                    if (i < RecData.SurveyRows.Length - 1)
                        RecUI.Divider(inner.Parent, inner.Width, inner.Y + rowH);

                    inner.Advance(rowH);
                }
            }, 0f, 24f, 8f);

            RecUI.Para(col, "Recalc", "수정하면 추천이 새로 계산돼요", RecTheme.Fs(14.5f), RecTheme.Caption);

            RecNav.FinishFrame(f);
        }

        // ---- 공통 ----

        /// <summary>카드 헤더: 왼쪽 제목 + 오른쪽 갈색 버튼.</summary>
        static void Head(RecCol col, string title, float size, string btnLabel, System.Action onClick)
        {
            const float h = 43f;
            var t = RecUI.Text("Title", col.Parent, title, size, RecTheme.Ink, true, TextAlignmentOptions.MidlineLeft);
            RecUI.SetRect(t.rectTransform, 0f, col.Y, col.Width * 0.65f, h);

            var b = RecUI.BrownButton(col.Parent, "Action", btnLabel, onClick, 0f, col.Y, 0f);
            b.anchoredPosition = new Vector2(col.Width - b.sizeDelta.x, b.anchoredPosition.y);
            col.Advance(h);
        }

        /// <summary>시안의 flex-wrap 태그 줄 — 폭을 재서 줄바꿈한다.</summary>
        static void TagFlow(RecCol col, string[] labels, float gap)
        {
            const float rowH = 38f;
            var wrap = RecUI.Node("Tags", col.Parent);
            float x = 0f, y = 0f;

            foreach (var label in labels)
            {
                var tag = RecUI.Pill(wrap, "Tag", label, RecTheme.White, RecTheme.CardBorder,
                    RecTheme.Body, x, y, 15f, 16f, rowH);
                float w = tag.sizeDelta.x;
                if (x > 0f && x + w > col.Width)
                {
                    y += rowH + gap;
                    x = 0f;
                    tag.anchoredPosition = new Vector2(x, -y);
                }
                x += w + gap;
            }

            float total = y + rowH;
            RecUI.SetRect(wrap, 0f, col.Y, col.Width, total);
            col.Advance(total);
        }

        // 상자 전체를 누를 수 있게. Card() 는 root 에 Graphic 이 없으므로 Face 가 판정을 받는다.
        static void Touch(RectTransform box, System.Action onClick)
        {
            var face = box.Find("Face");
            var shape = face != null ? face.GetComponent<RecShape>() : box.GetComponent<RecShape>();
            if (shape == null) return;
            shape.raycastTarget = true;
            var btn = shape.gameObject.AddComponent<Button>();
            btn.targetGraphic = shape;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick());
        }
    }
}
