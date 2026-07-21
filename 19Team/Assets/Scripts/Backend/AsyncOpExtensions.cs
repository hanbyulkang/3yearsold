using System;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace Backend
{
    /// <summary>
    /// UnityWebRequest를 await 가능하게 만든다.
    ///
    /// AsyncOperation 전체가 아니라 UnityWebRequestAsyncOperation에만 건다 —
    /// Unity가 나중에 AsyncOperation용 awaiter를 추가해도 더 구체적인 타입이
    /// 우선이라 모호성 오류가 나지 않는다.
    /// </summary>
    public static class AsyncOpExtensions
    {
        public struct WebRequestAwaiter : INotifyCompletion
        {
            readonly UnityWebRequestAsyncOperation _op;
            public WebRequestAwaiter(UnityWebRequestAsyncOperation op) => _op = op;

            public bool IsCompleted => _op.isDone;
            public UnityWebRequest GetResult() => _op.webRequest;

            public void OnCompleted(Action continuation)
            {
                if (_op.isDone) { continuation(); return; }
                _op.completed += _ => continuation();
            }
        }

        public static WebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation op)
            => new WebRequestAwaiter(op);
    }
}
