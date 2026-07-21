using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiniGame1
{
    // 블록 1개의 표시와 스와이프 입력. 로직은 전부 BoardModel — 여기는 그리기만 한다.
    public class BlockView : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        // 애견용품 6종 팔레트 (mini-game-1-prd.md §2.2 가안)
        static readonly Color[] BgColors =
        {
            Hex("#E45B4F"), // 0 사료 그릇 (빨강)
            Hex("#EFE3C8"), // 1 뼈다귀 (아이보리)
            Hex("#B9D64B"), // 2 테니스공 (연두)
            Hex("#B07B4F"), // 3 개껌 (갈색)
            Hex("#4F86D6"), // 4 리드줄 (파랑)
            Hex("#E87BAB"), // 5 삑삑이 (분홍)
        };

        public Cell Cell;
        public int Code { get; private set; }

        BoardView _board;
        Image _bg;
        Image _icon;
        Image _icon2;
        Vector2 _pressPos;
        bool _swiped;
        float _cellSize;

        public static BlockView Create(Transform parent, BoardView board, Cell cell, int code, float cellSize, Sprite brandLogo)
        {
            var go = new GameObject($"Block_{cell.X}_{cell.Y}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(cellSize - 4f, cellSize - 4f);

            var v = go.AddComponent<BlockView>();
            v._board = board;
            v._cellSize = cellSize;
            v.Cell = cell;

            v._bg = go.AddComponent<Image>();
            MG1Skin.ApplyRounded(v._bg, 8f);
            v._bg.raycastTarget = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            v._icon = iconGo.AddComponent<Image>();
            v._icon.raycastTarget = false;

            var icon2Go = new GameObject("Icon2", typeof(RectTransform));
            icon2Go.transform.SetParent(go.transform, false);
            v._icon2 = icon2Go.AddComponent<Image>();
            v._icon2.raycastTarget = false;

            v.SetCode(code, brandLogo);
            return v;
        }

        public void SetCode(int code, Sprite brandLogo)
        {
            Code = code;
            var iconRt = (RectTransform)_icon.transform;
            var icon2Rt = (RectTransform)_icon2.transform;
            _icon.enabled = true;
            _icon2.enabled = false;
            iconRt.localRotation = Quaternion.identity;
            iconRt.anchoredPosition = Vector2.zero;
            float s = _cellSize - 4f;

            // 전용 아트 세트가 있으면 그것만 쓴다 (목표 디자인 반영)
            if (MG1Skin.HasBlockArts)
            {
                _bg.type = Image.Type.Simple;
                _bg.color = Color.white;
                _bg.preserveAspect = true;
                _icon.enabled = false;
                if (BoardModel.IsNormal(code))
                    _bg.sprite = MG1Skin.BlockArts[code % 6];
                else if (code == BoardModel.RocketH) _bg.sprite = MG1Skin.RocketHArt;
                else if (code == BoardModel.RocketV) _bg.sprite = MG1Skin.RocketVArt;
                else if (code == BoardModel.Bomb) _bg.sprite = MG1Skin.BombArt;
                else if (code == BoardModel.Magic) _bg.sprite = MG1Skin.MagicArt;
                else if (code == BoardModel.Brand)
                {
                    _bg.sprite = MG1Skin.BrandFrameArt;
                    if (brandLogo != null)
                    {
                        _icon.enabled = true;
                        _icon.sprite = brandLogo;
                        _icon.color = Color.white;
                        _icon.preserveAspect = true;
                        iconRt.sizeDelta = new Vector2(s * 0.78f, s * 0.44f);
                    }
                }
                return;
            }
            MG1Skin.ApplyRounded(_bg, 8f);

            if (BoardModel.IsNormal(code))
            {
                _bg.color = BgColors[code % BgColors.Length];
                Color iconColor = Color.Lerp(_bg.color, Color.white, 0.55f);
                Color darkColor = Color.Lerp(_bg.color, Color.black, 0.35f);
                switch (code)
                {
                    case 0: // 사료 그릇 — 흰 원
                        _icon.sprite = MG1Sprites.Circle();
                        _icon.color = iconColor;
                        iconRt.sizeDelta = new Vector2(s * 0.5f, s * 0.5f);
                        break;
                    case 1: // 뼈다귀 — 전용 아트 (Assets/UI/22.png), 없으면 바 폴백
                        if (MG1Skin.Bone != null)
                        {
                            _bg.color = Hex("#F7EEDC");
                            _icon.sprite = MG1Skin.Bone;
                            _icon.color = Color.white;
                            _icon.preserveAspect = true;
                            iconRt.sizeDelta = new Vector2(s * 0.8f, s * 0.8f);
                        }
                        else
                        {
                            _icon.sprite = MG1Sprites.RoundedRect(64, 24);
                            _icon.color = darkColor;
                            iconRt.sizeDelta = new Vector2(s * 0.62f, s * 0.26f);
                        }
                        break;
                    case 2: // 테니스공 — 링
                        _icon.sprite = MG1Sprites.Ring();
                        _icon.color = iconColor;
                        iconRt.sizeDelta = new Vector2(s * 0.55f, s * 0.55f);
                        break;
                    case 3: // 개껌 — 다이아몬드
                        _icon.sprite = MG1Sprites.RoundedRect(64, 10);
                        _icon.color = iconColor;
                        iconRt.sizeDelta = new Vector2(s * 0.42f, s * 0.42f);
                        iconRt.localRotation = Quaternion.Euler(0, 0, 45f);
                        break;
                    case 4: // 리드줄 — 대각선 바
                        _icon.sprite = MG1Sprites.RoundedRect(64, 24);
                        _icon.color = iconColor;
                        iconRt.sizeDelta = new Vector2(s * 0.7f, s * 0.2f);
                        iconRt.localRotation = Quaternion.Euler(0, 0, -40f);
                        break;
                    default: // 삑삑이 — 작은 원 2개
                        _icon.sprite = MG1Sprites.Circle();
                        _icon.color = iconColor;
                        iconRt.sizeDelta = new Vector2(s * 0.34f, s * 0.34f);
                        iconRt.anchoredPosition = new Vector2(-s * 0.14f, s * 0.1f);
                        _icon2.enabled = true;
                        _icon2.sprite = MG1Sprites.Circle();
                        _icon2.color = iconColor;
                        icon2Rt.sizeDelta = new Vector2(s * 0.26f, s * 0.26f);
                        icon2Rt.anchoredPosition = new Vector2(s * 0.14f, -s * 0.12f);
                        break;
                }
                return;
            }

            switch (code)
            {
                case BoardModel.RocketH:
                case BoardModel.RocketV:
                    _bg.color = Hex("#37474F");
                    _icon.sprite = MG1Sprites.RoundedRect(64, 24);
                    _icon.color = Color.white;
                    iconRt.sizeDelta = new Vector2(s * 0.72f, s * 0.2f);
                    if (code == BoardModel.RocketV) iconRt.localRotation = Quaternion.Euler(0, 0, 90f);
                    break;
                case BoardModel.Bomb:
                    _bg.color = Hex("#263238");
                    _icon.sprite = MG1Sprites.Circle();
                    _icon.color = Hex("#FF7043");
                    iconRt.sizeDelta = new Vector2(s * 0.55f, s * 0.55f);
                    break;
                case BoardModel.Magic:
                    _bg.color = Hex("#1A1A2E");
                    _icon.sprite = MG1Sprites.Ring();
                    _icon.color = Hex("#FFD54F");
                    iconRt.sizeDelta = new Vector2(s * 0.62f, s * 0.62f);
                    _icon2.enabled = true;
                    _icon2.sprite = MG1Sprites.Circle();
                    _icon2.color = Hex("#FFD54F");
                    icon2Rt.sizeDelta = new Vector2(s * 0.24f, s * 0.24f);
                    icon2Rt.anchoredPosition = Vector2.zero;
                    break;
                case BoardModel.Brand:
                    _bg.color = Color.white;
                    if (brandLogo != null)
                    {
                        _icon.sprite = brandLogo;
                        _icon.color = Color.white;
                        _icon.preserveAspect = true;
                        iconRt.sizeDelta = new Vector2(s * 0.86f, s * 0.5f);
                    }
                    else
                    {
                        _icon.sprite = MG1Sprites.Circle();
                        _icon.color = Hex("#FF8A65");
                        iconRt.sizeDelta = new Vector2(s * 0.5f, s * 0.5f);
                    }
                    break;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressPos = eventData.position;
            _swiped = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_swiped) return;
            Vector2 delta = eventData.position - _pressPos;
            if (delta.magnitude < 24f) return;
            _swiped = true;
            Cell target = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Cell(Cell.X + (delta.x > 0 ? 1 : -1), Cell.Y)
                : new Cell(Cell.X, Cell.Y + (delta.y > 0 ? 1 : -1));
            _board.RequestSwap(Cell, target);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
