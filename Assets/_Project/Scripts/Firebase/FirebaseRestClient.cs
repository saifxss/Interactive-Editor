using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Adeeb.Firebase
{
    /// <summary>
    /// The one transport used for every Realtime Database call, in the Editor and in WebGL.
    ///
    /// Authentication note: Realtime Database REST authenticates an end user through the `auth`
    /// query parameter. An `Authorization: Bearer` header does NOT work for Firebase ID tokens --
    /// it is only accepted for OAuth2 service-account tokens, and a bearer-authenticated request
    /// comes back 401. That matters beyond a failed call: a client using the wrong scheme silently
    /// behaves as an anonymous caller, so "write denied" tests pass for the wrong reason while
    /// proving nothing about the rules. Always confirm an authenticated read returns 200 first.
    ///
    /// The token therefore travels in the URL. It is short-lived (one hour), the request is HTTPS,
    /// and this is the mechanism Firebase documents -- but it is the reason request URLs are never
    /// logged here.
    /// </summary>
    public sealed class FirebaseRestClient
    {
        private readonly FirebaseConfig _config;
        private readonly FirebaseAuthService _auth;

        public FirebaseRestClient(FirebaseConfig config, FirebaseAuthService auth)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        /// <summary>
        /// Reads a database path and returns the raw JSON body. A literal "null" is a valid answer
        /// meaning the node does not exist, and is left for the caller to interpret.
        /// </summary>
        /// <param name="path">Path without a leading slash or .json suffix, e.g. "StoryLibary".</param>
        public Task<string> GetJsonAsync(string path, CancellationToken token = default) =>
            SendWithAuthAsync(path, UnityWebRequest.kHttpVerbGET, null, token);

        /// <summary>Writes a node, replacing whatever was there.</summary>
        public Task<string> PutJsonAsync(string path, string json, CancellationToken token = default) =>
            SendWithAuthAsync(path, UnityWebRequest.kHttpVerbPUT, json, token);

        public Task<string> PatchJsonAsync(string path, string json, CancellationToken token = default) =>
            SendWithAuthAsync(path, "PATCH", json, token);

        private async Task<string> SendWithAuthAsync(
            string path, string verb, string body, CancellationToken token)
        {
            string problem = _config.DescribeProblem();
            if (problem != null)
            {
                throw new FirebaseException(FirebaseErrorKind.Configuration, problem);
            }

            string idToken = await _auth.GetIdTokenAsync(token);

            try
            {
                return await SendOnceAsync(path, verb, body, idToken, token);
            }
            catch (FirebaseException error) when (error.Kind == FirebaseErrorKind.SessionExpired)
            {
                // Exactly one bounded retry. The token was rejected, so refresh and try again
                // once; a second failure is reported rather than retried, so a permanently
                // denied call cannot become a loop.
                _auth.InvalidateIdToken();
                string refreshed = await _auth.GetIdTokenAsync(token);

                return await SendOnceAsync(path, verb, body, refreshed, token);
            }
        }

        private async Task<string> SendOnceAsync(
            string path, string verb, string body, string idToken, CancellationToken token)
        {
            using UnityWebRequest request = new UnityWebRequest(BuildUrl(path, idToken), verb)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _config.RequestTimeoutSeconds
            };

            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body))
                {
                    contentType = "application/json"
                };
            }

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

            throw Describe(request);
        }

        private string BuildUrl(string path, string idToken)
        {
            string trimmed = (path ?? string.Empty).Trim('/');
            string root = _config.DatabaseUrl;
            string auth = "?auth=" + UnityWebRequest.EscapeURL(idToken);

            return string.IsNullOrEmpty(trimmed)
                ? root + "/.json" + auth
                : root + "/" + trimmed + ".json" + auth;
        }

        /// <summary>Turns a failed request into a typed error. Never includes the URL or token.</summary>
        private static FirebaseException Describe(UnityWebRequest request)
        {
            long status = request.responseCode;

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                // In WebGL this also covers a CORS rejection, which in practice usually means a
                // wrong database URL rather than a real network problem.
                return new FirebaseException(
                    FirebaseErrorKind.Network,
                    "Could not reach the database. In a browser build this can also mean the database URL is wrong.");
            }

            if (status == 401)
            {
                if ((request.downloadHandler?.text ?? "").IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new FirebaseException(FirebaseErrorKind.PermissionDenied, "Database rules denied this operation.", status);
                // Reported as an expired session so the caller refreshes once; a second 401 surfaces.
                return new FirebaseException(FirebaseErrorKind.SessionExpired, "The database rejected the session token.", status);
            }

            if (status == 403)
            {
                return new FirebaseException(FirebaseErrorKind.PermissionDenied, "Database rules denied this operation.", status);
            }

            if (status == 404)
            {
                return new FirebaseException(FirebaseErrorKind.Configuration, "The database URL does not resolve to a database.", status);
            }

            if (status >= 500)
            {
                return new FirebaseException(FirebaseErrorKind.Unknown, "The database reported a server error (" + status + ").", status);
            }

            return new FirebaseException(FirebaseErrorKind.Unknown, "The request failed (" + status + ").", status);
        }
    }
}
