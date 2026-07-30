using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Backend
{
    /// <summary>
    /// URL 이미지를 기존 플레이스홀더 슬롯 위에 얹는다.
    ///
    /// RecUI.Slot("사진" 라벨 박스)의 디자인을 건드리지 않고, 로드에 성공했을 때만
    /// RawImage를 위에 덮는다 — 오프라인이면 팀원이 만든 플레이스홀더가 그대로 보인다.
    /// 텍스처는 캐시한다 (견종 대표 사진은 같은 URL이 반복된다).
    /// </summary>
    public static class RemoteImage
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Task<Texture2D>> Pending = new Dictionary<string, Task<Texture2D>>();

        /// <param name="onLoaded">로드 성공 직후 호출 — 라벨 교체 등 후처리용</param>
        public static async void Load(string url, RectTransform slot, Action onLoaded = null)
        {
            if (string.IsNullOrEmpty(url) || slot == null) return;

            Texture2D tex;
            try { tex = await Fetch(url); }
            catch (Exception e) { Debug.LogWarning($"[RemoteImage] {url} 실패: {e.Message}"); return; }
            if (tex == null || slot == null) return;   // slot은 씬 전환으로 파괴됐을 수 있다

            var go = new GameObject("RemoteImage", typeof(RawImage));
            go.transform.SetParent(slot, false);
            var ri = go.GetComponent<RawImage>();
            ri.texture = tex;
            ri.raycastTarget = false;

            var rt = ri.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);

            CoverCrop(ri, tex, slot);
            onLoaded?.Invoke();
        }

        /// <summary>슬롯 비율에 맞춰 중앙을 잘라 채운다 (늘어나 보이지 않게).</summary>
        static void CoverCrop(RawImage ri, Texture2D tex, RectTransform slot)
        {
            float sw = Mathf.Max(1f, slot.rect.width), sh = Mathf.Max(1f, slot.rect.height);
            float slotAspect = sw / sh;
            float texAspect = (float)tex.width / tex.height;

            if (texAspect > slotAspect)
            {
                float u = slotAspect / texAspect;             // 가로가 남는다 → 좌우를 자른다
                ri.uvRect = new Rect((1f - u) * 0.5f, 0f, u, 1f);
            }
            else
            {
                float v = texAspect / slotAspect;             // 세로가 남는다 → 위아래를 자른다
                ri.uvRect = new Rect(0f, (1f - v) * 0.5f, 1f, v);
            }
        }

        static async Task<Texture2D> Fetch(string url)
        {
            if (Cache.TryGetValue(url, out var hit)) return hit;
            if (Pending.TryGetValue(url, out var running)) return await running;

            var task = FetchInternal(url);
            Pending[url] = task;
            try
            {
                var tex = await task;
                if (tex != null) Cache[url] = tex;
                return tex;
            }
            finally { Pending.Remove(url); }
        }

        static async Task<Texture2D> FetchInternal(string url)
        {
            string requestUrl = WebGLSafeUrl(url);
            using (var req = UnityWebRequestTexture.GetTexture(requestUrl))
            {
                req.timeout = 20;
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[RemoteImage] HTTP {req.responseCode} ({req.result}): {req.error} — {requestUrl}");
                    return null;
                }
                Debug.Log($"[RemoteImage] 이미지 완료 ({req.responseCode}) — {requestUrl}");
                return DownloadHandlerTexture.GetContent(req);
            }
        }

        static string WebGLSafeUrl(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Uri.TryCreate(url, UriKind.Absolute, out var imageUri) &&
                imageUri.Host.Equals("func.seoul.go.kr", StringComparison.OrdinalIgnoreCase) &&
                imageUri.AbsolutePath.StartsWith("/upload/animal/", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var appUri))
            {
                string relative = imageUri.AbsolutePath.Substring("/upload/animal/".Length);
                return appUri.GetLeftPart(UriPartial.Authority) + "/seoul-animal/" + relative;
            }
#endif
            return url;
        }
    }
}
