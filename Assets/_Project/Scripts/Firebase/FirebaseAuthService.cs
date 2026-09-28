using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Adeeb.Firebase
{
    /// <summary>
    /// Anonymous authentication over the Identity Toolkit REST API.
    ///
    /// REST rather than the native Firebase Unity SDK, because that SDK does not list WebGL as a
    /// supported platform. The same small C# transport then works in the Editor and the browser.
    ///
    /// Restores the saved session on start, refreshes an expiring ID token, and only signs up a
    /// new anonymous account when there is genuinely nothing to restore -- creating a fresh
    /// account on every page load would silently orphan the user's saved work.
    /// </summary>
    public sealed class FirebaseAuthService
    {
        private const string SignUpUrl = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";
        private const string RefreshUrl = "https://securetoken.googleapis.com/v1/token?key=";

        /// <summary>Refresh slightly early so a token cannot expire mid-request.</summary>
        private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(2);

        private readonly FirebaseConfig _config;
        private readonly ISessionStore _store;

        /// <summary>Serialises token work so concurrent callers cannot each trigger a refresh.</summary>
        private readonly SemaphoreSlim _tokenGate = new SemaphoreSlim(1, 1);

        private string _uid;
        private string _idToken;
        private string _refreshToken;
        private DateTime _expiresAtUtc;

        public FirebaseAuthService(FirebaseConfig config, ISessionStore store)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>The signed-in user ID, or null before the first successful sign-in.</summary>
        public string Uid => _uid;

        /// <summary>Raised when the identity changes, so views can reload user-scoped data.</summary>
        public event Action<string> SignedIn;

        private bool HasUsableSession =>
            !string.IsNullOrEmpty(_idToken) && !string.IsNullOrEmpty(_refreshToken);

        private bool NeedsRefresh => DateTime.UtcNow + ExpiryMargin >= _expiresAtUtc;

        /// <summary>
        /// Returns an ID token valid right now: reusing the live session, refreshing the stored
        /// one, or signing up anonymously -- in that order of preference.
        /// </summary>
        public async Task<string> GetIdTokenAsync(CancellationToken token = default)
        {
            string problem = _config.DescribeProblem();
            if (problem != null)
            {
                throw new FirebaseException(FirebaseErrorKind.Configuration, problem);
            }

            await _tokenGate.WaitAsync(token);
            try
            {
                if (HasUsableSession && !NeedsRefresh)
                {
                    return _idToken;
                }

                // A refresh token can outlive this run, sitting in browser storage from a
                // previous page load.
                string storedRefreshToken = _refreshToken ?? _store.ReadRefreshToken();

                if (!string.IsNullOrEmpty(storedRefreshToken))
                {
                    try
                    {
                        await RefreshAsync(storedRefreshToken, token);
                        return _idToken;
                    }
                    catch (FirebaseException error) when (error.Kind == FirebaseErrorKind.SessionExpired)
                    {
                        // The stored identity is genuinely gone. Drop it and start a new one. The
                        // uid changes, so anything saved under the old one will not reappear --
                        // visible behaviour, rather than pretending the saves never existed.
                        // Keep the identity until the user explicitly chooses to sign out.
                        // Automatically replacing it would hide previously saved projects.
                        throw;
                    }
                }

                await SignUpAnonymouslyAsync(token);
                return _idToken;
            }
            finally
            {
                _tokenGate.Release();
            }
        }

        /// <summary>
        /// Marks the current token stale so the next call refreshes it. Used after a 401, to tell
        /// a token that expired early apart from a rule that denies access.
        /// </summary>
        public void InvalidateIdToken() => _expiresAtUtc = DateTime.UtcNow;

        /// <summary>Forgets the identity, including the stored refresh token.</summary>
        public void SignOut()
        {
            _uid = null;
            _idToken = null;
            _refreshToken = null;
            _store.Clear();
        }

        private async Task SignUpAnonymouslyAsync(CancellationToken token)
        {
            using UnityWebRequest request = BuildPost(
                SignUpUrl + _config.WebApiKey, "{\"returnSecureToken\":true}", "application/json");

            string payload = await SendAsync(request, token);
            SignUpResponse parsed = ParseOrThrow<SignUpResponse>(payload);

            if (parsed == null || string.IsNullOrEmpty(parsed.idToken) || string.IsNullOrEmpty(parsed.localId) || string.IsNullOrEmpty(parsed.refreshToken))
            {
                throw new FirebaseException(FirebaseErrorKind.MalformedResponse, "Anonymous sign-in returned no token.");
            }

            Adopt(parsed.localId, parsed.idToken, parsed.refreshToken, parsed.expiresIn);
        }

        private async Task RefreshAsync(string refreshToken, CancellationToken token)
        {
            string body = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(refreshToken);

            using UnityWebRequest request = BuildPost(
                RefreshUrl + _config.WebApiKey, body, "application/x-www-form-urlencoded");

            string payload = await SendAsync(request, token);
            RefreshResponse parsed = ParseOrThrow<RefreshResponse>(payload);

            if (parsed == null || string.IsNullOrEmpty(parsed.id_token) || string.IsNullOrEmpty(parsed.user_id))
            {
                throw new FirebaseException(FirebaseErrorKind.MalformedResponse, "Token refresh returned no token.");
            }

            Adopt(
                parsed.user_id,
                parsed.id_token,
                string.IsNullOrEmpty(parsed.refresh_token) ? refreshToken : parsed.refresh_token,
                parsed.expires_in);
        }

        private void Adopt(string uid, string idToken, string refreshToken, string expiresInSeconds)
        {
            bool identityChanged = _uid != uid;

            _uid = uid;
            _idToken = idToken;
            _refreshToken = refreshToken;

            // An unreadable lifetime is treated as already stale rather than assumed to be long.
            _expiresAtUtc = int.TryParse(expiresInSeconds, out int seconds) && seconds > 0
                ? DateTime.UtcNow.AddSeconds(seconds)
                : DateTime.UtcNow;

            _store.WriteRefreshToken(refreshToken);

            if (identityChanged)
            {
                SignedIn?.Invoke(uid);
            }
        }

        private UnityWebRequest BuildPost(string url, string body, string contentType)
        {
            return new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)) { contentType = contentType },
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _config.RequestTimeoutSeconds
            };
        }

        private static async Task<string> SendAsync(UnityWebRequest request, CancellationToken token)
        {
            try
            {
                await WebRequestAwaiter.SendAsync(request, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler.text;
            }

            long status = request.responseCode;

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                throw new FirebaseException(FirebaseErrorKind.Network, "Could not reach the authentication service.");
            }

            string reason = request.downloadHandler != null
                ? request.downloadHandler.text ?? string.Empty
                : string.Empty;

            if (status == 400 || status == 401 || status == 403)
            {
                // Identity Toolkit reports a dead refresh token as 400 with one of these codes.
                // They mean "start over", not "retry".
                bool sessionGone =
                    reason.Contains("TOKEN_EXPIRED") ||
                    reason.Contains("INVALID_REFRESH_TOKEN") ||
                    reason.Contains("USER_NOT_FOUND") ||
                    reason.Contains("USER_DISABLED");

                if (sessionGone)
                {
                    throw new FirebaseException(
                        FirebaseErrorKind.SessionExpired, "The stored anonymous session is no longer valid.", status);
                }

                if (reason.Contains("ADMIN_ONLY_OPERATION") ||
                    reason.Contains("OPERATION_NOT_ALLOWED") ||
                    reason.Contains("CONFIGURATION_NOT_FOUND"))
                {
                    throw new FirebaseException(
                        FirebaseErrorKind.Configuration,
                        "Anonymous sign-in is not enabled for this Firebase project.",
                        status);
                }

                throw new FirebaseException(FirebaseErrorKind.Unknown, "Authentication was rejected (" + status + ").", status);
            }

            throw new FirebaseException(FirebaseErrorKind.Unknown, "Authentication failed (" + status + ").", status);
        }

        private static T ParseOrThrow<T>(string payload)
        {
            try
            {
                return JsonUtility.FromJson<T>(payload);
            }
            catch (Exception error)
            {
                throw new FirebaseException(
                    FirebaseErrorKind.MalformedResponse, "Could not read the authentication response.", 0, error);
            }
        }

        // Fixed-shape responses, so Unity's own JsonUtility is enough here. The story data needs
        // dynamic keys, which is the only reason the project takes a JSON dependency at all.

        [Serializable]
        private sealed class SignUpResponse
        {
            public string idToken;
            public string refreshToken;
            public string expiresIn;
            public string localId;
        }

        [Serializable]
        private sealed class RefreshResponse
        {
            public string id_token;
            public string refresh_token;
            public string expires_in;
            public string user_id;
        }
    }
}
