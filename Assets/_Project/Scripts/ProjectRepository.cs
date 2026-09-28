using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adeeb.Firebase;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Adeeb.EditorApp
{
    public interface IProjectRepository
    {
        Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken token);
        Task<ContentProject> LoadAsync(string id, CancellationToken token);
        Task SaveAsync(ContentProject project, string cover, CancellationToken token);
    }
    public sealed class FirebaseProjectRepository : IProjectRepository
    {
        readonly FirebaseAuthService auth;
        readonly FirebaseRestClient client;
        public FirebaseProjectRepository(FirebaseAuthService auth, FirebaseRestClient client) { this.auth = auth; this.client = client; }
        async Task<string> Root(CancellationToken token) { await auth.GetIdTokenAsync(token); return "users/" + auth.Uid; }
        public async Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken token)
        {
            var json = await client.GetJsonAsync(await Root(token) + "/projectSummaries", token);
            var entries = JsonConvert.DeserializeObject<Dictionary<string, ProjectSummary>>(json);
            var result = new List<ProjectSummary>();
            if (entries != null) foreach (var entry in entries) if (entry.Value != null) { entry.Value.Id = entry.Key; result.Add(entry.Value); }
            result.Sort((a, b) => b.UpdatedAt.CompareTo(a.UpdatedAt));
            return result;
        }
        public async Task<ContentProject> LoadAsync(string id, CancellationToken token)
        {
            ValidateId(id);
            var value = ContentProject.Decode(await client.GetJsonAsync(await Root(token) + "/projects/" + id, token));
            value.Id = id;
            return value;
        }
        public async Task SaveAsync(ContentProject project, string cover, CancellationToken token)
        {
            ValidateId(project.Id);
            var summary = new ProjectSummary { Id = project.Id, Title = project.Title, PageCount = project.Pages.Count, UpdatedAt = project.UpdatedAt, CoverPng = cover };
            // One atomic multi-location update: the card cannot describe a different save.
            var update = new JObject { ["projects/" + project.Id] = JObject.FromObject(project), ["projectSummaries/" + project.Id] = JObject.FromObject(summary) };
            await client.PatchJsonAsync(await Root(token), update.ToString(Formatting.None), token);
        }
        static void ValidateId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOfAny(new[] { '.', '#', '$', '[', ']', '/' }) >= 0)
                throw new ArgumentException("Invalid project ID.");
        }
    }
}
