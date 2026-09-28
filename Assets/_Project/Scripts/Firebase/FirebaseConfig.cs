using UnityEngine;

namespace Adeeb.Firebase
{
    /// <summary>
    /// Client configuration for the Firebase project.
    ///
    /// These values are not secrets. A Firebase web API key identifies the project; it does not
    /// grant access. Access is decided by Realtime Database rules, which is why the rules deny
    /// writes to the story source and scope every user's saves to their own uid. Shipping this
    /// inside a WebGL build is the intended design.
    /// </summary>
    [System.Serializable]
    public class FirebaseConfig
    {
        [SerializeField] private string projectId = "adeeb-technical-test";
        [SerializeField] private string webApiKey = "AIzaSyC3UKkBikjLs19H9FZiiw0jL70DKYjPyPI";

        [Tooltip("Copied verbatim from the console. Regional URL formats differ, so this is never " +
                 "built from the project ID.")]
        [SerializeField] private string databaseUrl =
            "https://adeeb-technical-test-default-rtdb.europe-west1.firebasedatabase.app";

        [Tooltip("Seconds before a single request is abandoned.")]
        [SerializeField, Range(1, 60)] private int requestTimeoutSeconds = 15;

        public string ProjectId => projectId;
        public string WebApiKey => webApiKey;
        public int RequestTimeoutSeconds => requestTimeoutSeconds;

        /// <summary>Database URL without a trailing slash, so path joining stays predictable.</summary>
        public string DatabaseUrl =>
            string.IsNullOrEmpty(databaseUrl) ? string.Empty : databaseUrl.TrimEnd('/');

        /// <summary>
        /// Describes what is missing, or null when the configuration is usable. Returning a
        /// sentence rather than a bool lets the UI show a real setup error instead of a blank list
        /// that looks like "no results".
        /// </summary>
        public string DescribeProblem()
        {
            if (string.IsNullOrWhiteSpace(webApiKey))
            {
                return "Firebase web API key is not set.";
            }

            if (string.IsNullOrWhiteSpace(DatabaseUrl))
            {
                return "Firebase Realtime Database URL is not set.";
            }

            return null;
        }
    }
}
