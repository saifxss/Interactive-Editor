using System;

namespace Adeeb.Firebase
{
    /// <summary>How a failure should be handled, and what the user should be told.</summary>
    public enum FirebaseErrorKind
    {
        /// <summary>Configuration is missing or wrong; retrying cannot help.</summary>
        Configuration,

        /// <summary>No route to the server, a dropped connection, or a browser CORS rejection.</summary>
        Network,

        /// <summary>The request exceeded its timeout, or the caller cancelled it.</summary>
        Timeout,

        /// <summary>The stored session is gone or rejected; a fresh sign-in is required.</summary>
        SessionExpired,

        /// <summary>Database rules refused the read or write.</summary>
        PermissionDenied,

        /// <summary>The server answered, but not with the shape we expect.</summary>
        MalformedResponse,

        /// <summary>Anything else, including 5xx.</summary>
        Unknown
    }

    /// <summary>
    /// A failure that is safe to show on screen. Never carries a token: error text reaches the UI
    /// and the browser console, and a session token in either is a leak.
    /// </summary>
    public sealed class FirebaseException : Exception
    {
        public FirebaseErrorKind Kind { get; }

        /// <summary>HTTP status when there was one, otherwise 0.</summary>
        public long StatusCode { get; }

        public FirebaseException(FirebaseErrorKind kind, string message, long statusCode = 0, Exception inner = null)
            : base(message, inner)
        {
            Kind = kind;
            StatusCode = statusCode;
        }

        /// <summary>True when repeating the same call has a realistic chance of succeeding.</summary>
        public bool IsRetryable =>
            Kind == FirebaseErrorKind.Network ||
            Kind == FirebaseErrorKind.Timeout ||
            (Kind == FirebaseErrorKind.Unknown && StatusCode >= 500);

        /// <summary>One sentence for the on-screen error state.</summary>
        public string UserMessage
        {
            get
            {
                switch (Kind)
                {
                    case FirebaseErrorKind.Configuration:
                        return "The app is not connected to Firebase.";
                    case FirebaseErrorKind.Network:
                        return "Could not reach the server. Check your connection and try again.";
                    case FirebaseErrorKind.Timeout:
                        return "The server took too long to respond.";
                    case FirebaseErrorKind.SessionExpired:
                        return "The saved sign-in expired. Clear this app's site data only if you accept losing access to its saved projects.";
                    case FirebaseErrorKind.PermissionDenied:
                        return "You do not have access to this data.";
                    case FirebaseErrorKind.MalformedResponse:
                        return "The server returned data the app could not read.";
                    default:
                        return "Something went wrong. Please try again.";
                }
            }
        }
    }
}
