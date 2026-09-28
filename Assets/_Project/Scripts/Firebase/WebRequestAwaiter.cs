using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Adeeb.Firebase
{
    /// <summary>
    /// Awaits a UnityWebRequest without using threads, so the same code runs in the Editor and in
    /// WebGL, where the player is single-threaded. Continuations resume on Unity's main thread,
    /// which is what lets controllers touch UI objects straight after an await.
    /// </summary>
    internal static class WebRequestAwaiter
    {
        public static Task<UnityWebRequest> SendAsync(UnityWebRequest request, CancellationToken token)
        {
            var completion = new TaskCompletionSource<UnityWebRequest>();
            CancellationTokenRegistration registration = default;

            if (token.CanBeCanceled)
            {
                if (token.IsCancellationRequested)
                {
                    completion.SetCanceled();
                    return completion.Task;
                }

                // Aborting completes the operation with a connection error; the handler below
                // reports it as a cancellation because the token is set.
                registration = token.Register(() =>
                {
                    if (!request.isDone)
                    {
                        request.Abort();
                    }
                });
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                registration.Dispose();

                if (token.IsCancellationRequested)
                {
                    completion.TrySetCanceled();
                    return;
                }

                completion.TrySetResult(request);
            };

            return completion.Task;
        }
    }
}
