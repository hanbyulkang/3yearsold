using System.Collections.Generic;
using UnityEngine;

namespace MiniGame1
{
    // 프로젝트 공용 에셋에서 가져온 스킨 스프라이트 — MG1GameManager가 Awake에서 채운다.
    // 비어 있으면 각 뷰는 MG1Sprites 절차 생성으로 폴백한다.
    public static class MG1Skin
    {
        public static Sprite Round;      // 9-slice 라운드 (MUIP 사본)
        public static Sprite Shadow;     // 원형 그림자 (MUIP 사본)
        public static Sprite Bone;       // 뼈다귀 블록 아트 (Assets/UI/22.png)
        public static Sprite Paw;        // 발바닥 아이콘 (Assets/UI/제목 없는 디자인-4.png)

        // 전용 블록 아트 세트 (Assets/UI/MiniGame1/Art) — 전부 채워지면 BlockView가 이 세트만 쓴다
        public static Sprite[] BlockArts; // [0]사료그릇 [1]뼈다귀 [2]테니스공 [3]개껌 [4]리드줄 [5]삑삑이
        public static Sprite RocketHArt, RocketVArt, BombArt, MagicArt, BrandFrameArt;

        public static bool HasBlockArts =>
            BlockArts != null && BlockArts.Length >= 6 && BlockArts[0] != null && BrandFrameArt != null;

        public static void ApplyRounded(UnityEngine.UI.Image img, float cornerScale)
        {
            if (Round != null)
            {
                img.sprite = Round;
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = cornerScale;
            }
            else
            {
                img.sprite = MG1Sprites.RoundedRect();
            }
        }
    }

    // 절차 생성 스프라이트 — 외부 아트 없이 플랫 2D 블록을 그린다 (mini-game-1-prd.md §9-2).
    // 레퍼런스 아트 도착 시 BlockView의 스프라이트 참조만 교체하면 된다.
    public static class MG1Sprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite RoundedRect(int size = 64, int radius = 14)
        {
            string key = $"rr{size}_{radius}";
            if (Cache.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, RoundedRectAlpha(x, y, size, radius)));
            return Bake(key, tex, size);
        }

        public static Sprite Circle(int size = 64)
        {
            string key = $"ci{size}";
            if (Cache.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f, r = size / 2f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 1f)));
                }
            return Bake(key, tex, size);
        }

        public static Sprite Ring(int size = 64, int thickness = 10)
        {
            string key = $"rg{size}_{thickness}";
            if (Cache.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) / 2f, rOut = size / 2f - 1f, rIn = rOut - thickness;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(rOut - d + 1f) * Mathf.Clamp01(d - rIn + 1f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            return Bake(key, tex, size);
        }

        static float RoundedRectAlpha(int x, int y, int size, int radius)
        {
            float cx = Mathf.Clamp(x, radius, size - 1 - radius);
            float cy = Mathf.Clamp(y, radius, size - 1 - radius);
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(radius - d + 1f);
        }

        static Sprite Bake(string key, Texture2D tex, int size)
        {
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = sprite;
            return sprite;
        }
    }
}
