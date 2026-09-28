namespace Adeeb.Firebase
{
    /// <summary>
    /// Where the anonymous refresh token is kept between page loads. Behind an interface so tests
    /// can substitute memory storage and so the storage choice stays one swappable decision.
    /// </summary>
    public interface ISessionStore
    {
        /// <summary>The stored refresh token, or null when there is nothing to restore.</summary>
        string ReadRefreshToken();

        void WriteRefreshToken(string refreshToken);

        void Clear();
    }

    /// <summary>
    /// Persists the refresh token in PlayerPrefs, which under WebGL is browser storage.
    ///
    /// This exists so the app keeps ONE anonymous identity across reloads instead of creating a
    /// new account on every page load. It is deliberately not a secure vault: the value is
    /// readable by anyone with access to the browser profile, and clearing site data loses the
    /// identity and therefore any saved work. That trade-off is acceptable for a demo and is
    /// stated in the handover rather than hidden.
    /// </summary>
    public sealed class PlayerPrefsSessionStore : ISessionStore
    {
        private const string Key = "adeeb.firebase.refreshToken";

        public string ReadRefreshToken()
        {
            string stored = UnityEngine.PlayerPrefs.GetString(Key, string.Empty);
            return string.IsNullOrEmpty(stored) ? null : stored;
        }

        public void WriteRefreshToken(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                Clear();
                return;
            }

            UnityEngine.PlayerPrefs.SetString(Key, refreshToken);
            UnityEngine.PlayerPrefs.Save();
        }

        public void Clear()
        {
            UnityEngine.PlayerPrefs.DeleteKey(Key);
            UnityEngine.PlayerPrefs.Save();
        }
    }

    /// <summary>Non-persistent store, for tests and for opting out of browser storage.</summary>
    public sealed class MemorySessionStore : ISessionStore
    {
        private string _refreshToken;

        public string ReadRefreshToken() => _refreshToken;

        public void WriteRefreshToken(string refreshToken) => _refreshToken = refreshToken;

        public void Clear() => _refreshToken = null;
    }
}
