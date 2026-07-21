using System;
using Recommend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Donation
{
    // 후원 화면에만 나오는 요소를 만드는 빌더.
    //
    // 상자·버튼·pill·글자처럼 두 시안에 똑같이 나오는 것은 RecUI 를 그대로 쓴다.
    // (Recommend 폴더는 읽기만 한다 — 고치지 않는다.)
    // 여기 있는 것은 후원 시안에만 있는 것: 재화 HUD·진행 막대·상태 pill·정직성 배너·증서 도장.
    //
    // 배치 규칙도 RecUI 와 같다 — LayoutGroup·ContentSizeFitter 를 쓰지 않고 RecCol 커서로
    // 좌표를 직접 계산한다. 부모 폭↔자식 높이가 서로 참조하면 ScrollRect 가 매 프레임
    // 리빌드를 강제해 에디터가 멈춘다.
    public static class DonUI
    {
        /// <summary>뼈다귀 아이콘. 없으면 글자로 폴백한다 (DonBootstrap 이 채운다).</summary>
        public static Sprite BoneIcon;

        // ---- 재화 HUD ----

        /// <summary>상단바 오른쪽 뼈다귀 잔액 (svg-donate/point-pill-dark.svg).</summary>
        public static RectTransform BonePill(RectTransform appbar, int bones)
        {
            const float h = 42f, padL = 14f, padR = 18f, icon = 26f, iconGap = 8f;

            var root = RecUI.Node("Bones", appbar);
            var s = RecUI.AddShape(root.gameObject);
            s.raycastTarget = false;
            s.Radius = h * 0.5f;
            s.SetFill(DonTheme.HudFill);
            s.SetBorder(1.5f, DonTheme.HudStroke);

            // 아이콘이 없으면 단위를 글자로 적는다 — 숫자만 남아 무엇의 수량인지 사라지면 안 된다.
            bool hasIcon = BoneIcon != null;
            string label = hasIcon ? DonData.Num(bones) : DonData.Num(bones) + " 뼈다귀";

            var t = RecUI.Text("Count", root, label, RecTheme.Fs(19f), DonTheme.HudInk,
                true, TextAlignmentOptions.MidlineLeft);
            float tw = RecUI.MeasureW(t);

            float textX = hasIcon ? padL + icon + iconGap : padL;
            RecUI.SetRect(t.rectTransform, textX, 0f, tw, h);

            if (hasIcon)
            {
                var img = RecUI.Node("Icon", root);
                var i = img.gameObject.AddComponent<Image>();
                i.sprite = BoneIcon;
                i.preserveAspect = true;
                i.raycastTarget = false;
                RecUI.SetRect(img, padL, (h - icon) * 0.5f, icon, icon);
            }

            float w = textX + tw + padR;
            // 상단바 오른쪽 끝에서 20px 안쪽 — 시안의 padding 과 같다
            RecUI.SetRect(root, appbar.sizeDelta.x - w - 20f, (RecNav.BarH - h) * 0.5f, w, h);
            return root;
        }

        // ---- 상태 pill ----

        /// <summary>집행 상태 pill. ok=true 초록(집행 완료) / false 빨강(목표 미달 종료).</summary>
        public static RectTransform StatusPill(Transform parent, string label, bool ok,
            float x, float y, float height = 32f)
        {
            var root = RecUI.Node("Status", parent);
            var s = RecUI.AddShape(root.gameObject);
            s.raycastTarget = false;
            s.Radius = height * 0.5f;
            s.SetGradient(ok ? DonTheme.OkTop : DonTheme.WarnTop, ok ? DonTheme.OkBottom : DonTheme.WarnBottom);
            s.SetBorder(1.5f, ok ? DonTheme.OkBorder : DonTheme.WarnBorder);

            var t = RecUI.Text("Label", root, label, RecTheme.FsTiny, Color.white, true, TextAlignmentOptions.Center);
            RecUI.Stretch(t.rectTransform);

            float w = RecUI.MeasureW(t) + 32f;
            RecUI.SetRect(root, x, y, w, height);

            // 위쪽 광택 띠 — 글자보다 먼저 그려야 하므로 형제 순서를 앞으로 보낸다
            var gloss = RecUI.Shape("Gloss", root);
            gloss.Radius = 6f;
            gloss.SetFill(DonTheme.GlossLight);
            RecUI.SetRect(gloss.rectTransform, 5f, 3.5f, w - 10f, 12f);
            gloss.transform.SetSiblingIndex(0);

            return root;
        }

        // ---- 진행 막대 ----

        /// <summary>
        /// 공동 창고 진행 막대 (svg-donate/progress-track.svg).
        /// 채워진 길이는 참여량 진행률이다 — 모금액이 아니다 (§6.1).
        /// </summary>
        public static void ProgressBar(RecCol col, int percent)
        {
            float h = DonTheme.TrackH;

            var track = RecUI.Node("Track", col.Parent);
            var ts = RecUI.AddShape(track.gameObject);
            ts.raycastTarget = false;
            ts.Radius = h * 0.5f;
            ts.SetFill(DonTheme.TrackFill);
            ts.SetBorder(2f, DonTheme.TrackStroke);
            RecUI.SetRect(track, 0f, col.Y, col.Width, h);

            const float inset = 3f;
            float innerH = h - inset * 2f;
            // 0% 여도 둥근 끝이 보이도록 최소 폭을 준다 — 폭 0 이면 메시가 아예 안 나온다
            float fillW = Mathf.Max(innerH, (col.Width - inset * 2f) * Mathf.Clamp01(percent / 100f));

            var fill = RecUI.Shape("Fill", track);
            fill.Radius = innerH * 0.5f;
            fill.SetGradient(RecTheme.GoldTop, RecTheme.GoldBottom);
            RecUI.SetRect(fill.rectTransform, inset, inset, fillW, innerH);

            var gloss = RecUI.Shape("Gloss", fill.rectTransform);
            gloss.Radius = 4f;
            gloss.SetFill(new Color(1f, 1f, 1f, 0.45f));
            RecUI.SetRect(gloss.rectTransform, 4f, 2f, Mathf.Max(0f, fillW - 8f), 8f);

            col.Advance(h);
        }

        // ---- 정직성 배너 ----

        /// <summary>
        /// 빨간 점선 고지 (svg-donate/notice-banner-red.svg).
        /// §6.5 — 실제 집행이 없는 구간에서는 이 라벨을 숨기지 않는다. 조건부 분기를 만들지 말 것.
        /// </summary>
        public static void HonestBanner(RecCol col, string text)
        {
            RecUI.DashedBox(col, "Honest", inner =>
            {
                RecUI.Para(inner, "Text", text, RecTheme.FsSmall, DonTheme.HonestInk, false, RecTheme.LineNormal);
            }, 14f, DonTheme.HonestFill, DonTheme.HonestStroke, 2f, 8f, 6f, 0f, 18f, 13f);
        }

        // ---- 입력칸 ----

        /// <summary>
        /// 숫자 입력칸. 오른쪽에 단위를 붙여 무엇의 수량인지 남긴다.
        /// 시안은 "2,000 P" 가 채워진 상태를 보여주지만, 실제 입력칸에 값을 미리 넣으면
        /// 이미 입력한 것처럼 읽힌다 — 안내 문구(placeholder)로 두고 값은 비워서 시작한다.
        /// </summary>
        public static TMP_InputField NumberField(RecCol col, string label, string placeholder, string unit,
            Action<string> onChange)
        {
            const float boxH = 62f;

            var lt = RecUI.Text("Label_" + label, col.Parent, label, RecTheme.FsAiCap, RecTheme.Ink, true);
            float lh = RecUI.MeasureH(lt, col.Width);
            RecUI.SetRect(lt.rectTransform, 0f, col.Y, col.Width, lh);

            var box = RecUI.Node("Field_" + label, col.Parent);
            var bs = RecUI.AddShape(box.gameObject);
            bs.raycastTarget = true;
            bs.Radius = 16f;
            bs.SetFill(RecTheme.White);
            bs.SetBorder(2f, RecTheme.CardBorder);
            RecUI.SetRect(box, 0f, col.Y + lh + 8f, col.Width, boxH);

            var ut = RecUI.Text("Unit", box, unit, RecTheme.Fs(17f), RecTheme.Sub, false, TextAlignmentOptions.MidlineRight);
            float uw = RecUI.MeasureW(ut) + 18f;
            RecUI.SetRect(ut.rectTransform, col.Width - uw, 0f, uw - 18f, boxH);

            var area = RecUI.Node("TextArea", box);
            RecUI.Stretch(area, 18f, uw + 8f, 0f, 0f);
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
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 9;
            input.targetGraphic = bs;
            input.transition = Selectable.Transition.None;
            if (onChange != null) input.onValueChanged.AddListener(v => onChange(v));

            col.Advance(lh + 8f + boxH);
            return input;
        }

        // ---- 기부 증서 ----

        /// <summary>증서 도장 (svg-donate/certificate-seal.svg). 금색 원 + 안쪽 점선 링.</summary>
        public static void Seal(Transform parent, float x, float y, float size, string glyph)
        {
            const float drop = 4f;

            var deep = RecUI.Shape("SealDeep", parent);
            deep.Radius = size * 0.5f;
            deep.SetFill(RecTheme.GoldDeep);
            RecUI.SetRect(deep.rectTransform, x, y + drop, size, size);

            var face = RecUI.Shape("Seal", parent);
            face.Radius = size * 0.5f;
            face.SetGradient(RecTheme.GoldTop, RecTheme.GoldBottom);
            face.SetBorder(3f, RecTheme.GoldBorder);
            RecUI.SetRect(face.rectTransform, x, y, size, size);

            var ring = RecUI.Shape("Ring", face.rectTransform);
            ring.Filled = false;
            ring.Radius = (size - 20f) * 0.5f;
            ring.SetDashedBorder(2f, DonTheme.SealRing, 4f, 4f);
            RecUI.Stretch(ring.rectTransform, 10f, 10f, 10f, 10f);

            if (string.IsNullOrEmpty(glyph)) return;
            var t = RecUI.Text("Glyph", face.rectTransform, glyph, RecTheme.Fs(17f), RecTheme.GoldText,
                true, TextAlignmentOptions.Center);
            RecUI.Stretch(t.rectTransform);
        }

        /// <summary>증서 종이 (svg-donate/certificate-frame.svg). 안쪽 내용은 fill 이 채운다.</summary>
        public static RectTransform Certificate(Transform parent, float x, float y, float width,
            Action<RecCol> fill, float padH = 40f, float padV = 40f, float innerGap = 14f)
        {
            var root = RecUI.Node("Certificate", parent);

            var shadow = RecUI.Shape("Shadow", root);
            shadow.Radius = 24f;
            shadow.SetFill(DonTheme.CertShadow);
            RecUI.Stretch(shadow.rectTransform, -2f, -2f, 6f, -10f);

            var face = RecUI.Shape("Face", root);
            face.Radius = 24f;
            face.SetGradient(DonTheme.CertTop, DonTheme.CertBottom);
            face.SetBorder(3f, DonTheme.CertBorder);
            RecUI.Stretch(face.rectTransform);

            // 테두리 안쪽 얇은 금선 — 채우지 않고 선만 그린다
            var inner = RecUI.Shape("InnerLine", root);
            inner.Filled = false;
            inner.Radius = 17f;
            inner.SetBorder(2f, DonTheme.CertInner);
            RecUI.Stretch(inner.rectTransform, 8f, 8f, 8f, 8f);

            var col = new RecCol(root, width - padH * 2f, innerGap, padV);
            fill(col);
            float h = col.Height + padV;

            RecUI.SetRect(root, x, y, width, h);
            // 배경 3개(그림자·종이·금선)는 stretch 라 건드리지 않고, 그 뒤 자식만 좌우 패딩만큼 민다
            for (int i = 3; i < root.childCount; i++)
                ((RectTransform)root.GetChild(i)).anchoredPosition += new Vector2(padH, 0f);

            return root;
        }

        // ---- 공통 ----

        /// <summary>카드 헤더: 왼쪽 제목 + 오른쪽에 붙는 요소(pill 등). 오른쪽 요소는 make 가 만든다.</summary>
        public static void HeadRow(RecCol col, string title, float titleSize, Func<Transform, float, RectTransform> make,
            float height = 40f)
        {
            var t = RecUI.Text("Title", col.Parent, title, titleSize, RecTheme.Ink, true, TextAlignmentOptions.MidlineLeft);

            if (make != null)
            {
                var right = make(col.Parent, col.Y);
                float rw = right.sizeDelta.x;
                right.anchoredPosition = new Vector2(col.Width - rw, right.anchoredPosition.y);
                RecUI.SetRect(t.rectTransform, 0f, col.Y, col.Width - rw - 12f, height);
            }
            else
            {
                RecUI.SetRect(t.rectTransform, 0f, col.Y, col.Width, height);
            }

            col.Advance(height);
        }

        /// <summary>좌우로 마주 보는 한 줄 (시안의 justify-content:space-between).</summary>
        public static void BetweenRow(RecCol col, string left, float leftSize, Color leftColor,
            string right, float rightSize, Color rightColor, float height = 30f)
        {
            var l = RecUI.Text("L", col.Parent, left, leftSize, leftColor, true, TextAlignmentOptions.MidlineLeft);
            var r = RecUI.Text("R", col.Parent, right, rightSize, rightColor, false, TextAlignmentOptions.MidlineRight);
            RecUI.SetRect(l.rectTransform, 0f, col.Y, col.Width * 0.55f, height);
            RecUI.SetRect(r.rectTransform, col.Width * 0.55f, col.Y, col.Width * 0.45f, height);
            col.Advance(height);
        }

        /// <summary>
        /// 버튼 글자를 폭에 맞춰 줄인다.
        /// 라벨에 이름·수량이 들어가는 버튼("도봉구 보호소 전체에게 2,000 뼈다귀 배분")은
        /// 데이터에 따라 길이가 달라져 그대로 두면 버튼 밖으로 흘러넘친다.
        /// </summary>
        public static TextMeshProUGUI FitLabel(RectTransform button, float minSize)
        {
            var t = button.Find("Face/Label");
            if (t == null) return null;
            var label = t.GetComponent<TextMeshProUGUI>();
            if (label == null) return null;

            // 줄바꿈을 켠 채로 자동 크기를 쓰면 글자를 줄이는 대신 두 줄로 접는다.
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = minSize;
            label.enableAutoSizing = true;
            return label;
        }

        /// <summary>상자 전체를 누를 수 있게. Card() 는 root 에 Graphic 이 없으므로 Face 가 판정을 받는다.</summary>
        public static void Touch(RectTransform box, Action onClick)
        {
            var face = box.Find("Face");
            var shape = face != null ? face.GetComponent<RecShape>() : box.GetComponent<RecShape>();
            if (shape == null) return;
            shape.raycastTarget = true;
            var btn = shape.gameObject.AddComponent<Button>();
            btn.targetGraphic = shape;
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
        }
    }
}
