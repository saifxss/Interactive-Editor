using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Adeeb.EditorApp
{
    // Application state and decisions. Views have no database dependency.
    public sealed class EditorController : IDisposable
    {
        readonly IProjectRepository repository;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        public ContentProject Project { get; private set; }
        public int PageIndex { get; private set; }
        public bool Playback { get; private set; }
        public bool Dirty { get; private set; }
        public bool Busy { get; private set; }
        public string SelectedId { get; private set; }
        public ContentPage Page => Project?.Pages[PageIndex];
        public event Action Changed;
        public event Action<string> Status;
        public event Action<IReadOnlyList<ProjectSummary>> MenuLoaded;
        public EditorController(IProjectRepository repository) { this.repository = repository; }
        public void NewProject() { if (Busy) return; Project = new ContentProject(); PageIndex = 0; Playback = false; SelectedId = null; Dirty = true; Changed?.Invoke(); }
        public async Task MenuAsync()
        {
            if (Busy) return;
            await Run(async () => MenuLoaded?.Invoke(await repository.ListAsync(lifetime.Token)), "Loading saved projects...");
        }
        public async Task OpenAsync(string id, bool playback)
        {
            if (Busy) return;
            await Run(async () => { Project = await repository.LoadAsync(id, lifetime.Token); PageIndex = 0; Playback = playback; Dirty = false; SelectedId = null; Changed?.Invoke(); }, "Opening project...");
        }
        public void SetTitle(string title) { if (!CanEdit) return; Project.Title = string.IsNullOrWhiteSpace(title) ? "Untitled story" : title.Trim(); Change(); }
        public bool CanEdit => Project != null && !Playback && !Busy;
        public void AddPage() { if (!CanEdit || Project.Pages.Count >= 30) return; Project.Pages.Add(new ContentPage()); PageIndex = Project.Pages.Count - 1; SelectedId = null; Change(); }
        public void Navigate(int direction) { if (Busy || Project == null) return; PageIndex = Math.Max(0, Math.Min(Project.Pages.Count - 1, PageIndex + direction)); SelectedId = null; Changed?.Invoke(); }
        public void Background(string id) { if (!CanEdit) return; Page.BackgroundId = id; Change(); }
        public void AddObject(string id) { if (!CanEdit || Page.Objects.Count >= 50) return; var item = new PlacedObject { AssetId = id }; Page.Objects.Add(item); SelectedId = item.Id; Change(); }
        public void Select(string id) { if (!CanEdit) return; SelectedId = id; Changed?.Invoke(); }
        public void Move(string id, float x, float y)
        {
            if (!CanEdit) return;
            var item = Page.Objects.Find(o => o.Id == id); if (item == null) return;
            item.X = ContentProject.Clamp(x, 0.05f, 0.95f); item.Y = ContentProject.Clamp(y, 0.05f, 0.95f);
            Dirty = true; // The drag view moves immediately; no full redraw during a gesture.
        }
        public void Scale(float multiplier) { if (!CanEdit) return; var item = Page.Objects.Find(o => o.Id == SelectedId); if (item == null) return; item.Scale = ContentProject.Clamp(item.Scale * multiplier, 0.4f, 3f); Change(); }
        public void DeleteSelected() { if (!CanEdit) return; Page.Objects.RemoveAll(o => o.Id == SelectedId); SelectedId = null; Change(); }
        public void Preview() { if (Busy || Project == null) return; Playback = !Playback; PageIndex = 0; SelectedId = null; Changed?.Invoke(); }
        public async Task SaveAsync(string cover)
        {
            if (!CanEdit) return;
            Project.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await Run(async () => { await repository.SaveAsync(Project, cover, lifetime.Token); Dirty = false; Status?.Invoke("Saved. Your project is available in this browser profile."); }, "Saving...");
        }
        void Change() { Dirty = true; Changed?.Invoke(); }
        async Task Run(Func<Task> action, string message)
        {
            Busy = true; Status?.Invoke(message);
            try { await action(); }
            catch (OperationCanceledException) { }
            catch (Adeeb.Firebase.FirebaseException e) { Status?.Invoke(e.UserMessage); }
            catch (Exception) { Status?.Invoke("The operation failed. Please retry; your current work is kept."); }
            finally { Busy = false; Changed?.Invoke(); }
        }
        public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); Changed = null; Status = null; MenuLoaded = null; }
    }
}
