using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adeeb.EditorApp;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Adeeb.Tests
{
    public class EditorFlowTests
    {
        sealed class MemoryProjects : IProjectRepository
        {
            public string Saved;
            public Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken token) =>
                Task.FromResult<IReadOnlyList<ProjectSummary>>(new List<ProjectSummary>());
            public Task<ContentProject> LoadAsync(string id, CancellationToken token) =>
                Task.FromResult(ContentProject.Decode(Saved));
            public Task SaveAsync(ContentProject project, string cover, CancellationToken token)
            {
                Saved = JsonConvert.SerializeObject(project);
                return Task.CompletedTask;
            }
        }

        [Test]
        public void SavedProjectOpensWithExactlyItsOriginalPages()
        {
            var source = new ContentProject();
            source.Pages[0].Objects.Add(new PlacedObject { AssetId = "house", X = .4f, Y = .7f });
            source.Pages.Add(new ContentPage { BackgroundId = "night" });
            var decoded = ContentProject.Decode(JsonConvert.SerializeObject(source));
            Assert.AreEqual(2, decoded.Pages.Count);
            Assert.AreEqual("house", decoded.Pages[0].Objects[0].AssetId);
            Assert.AreEqual("night", decoded.Pages[1].BackgroundId);
        }

        [Test]
        public async Task PlaybackBlocksEditsWhileAllowingPageNavigation()
        {
            var store = new MemoryProjects();
            using var controller = new EditorController(store);
            controller.NewProject();
            controller.AddPage();
            await controller.SaveAsync("cover");
            Assert.IsFalse(controller.Dirty);
            await controller.OpenAsync(controller.Project.Id, true);
            Assert.AreEqual(2, controller.Project.Pages.Count);
            Assert.AreEqual(0, controller.PageIndex);
            Assert.IsTrue(controller.Playback);
            controller.AddObject("tree");
            Assert.IsEmpty(controller.Page.Objects);
            controller.Navigate(1);
            Assert.AreEqual(1, controller.PageIndex);
        }

        [Test]
        public void LoadedPositionsAndScaleStayWithinTheStage()
        {
            var source = new ContentProject();
            source.Pages[0].Objects.Add(new PlacedObject { AssetId = "tree", X = -4f, Y = 400f, Scale = 100f });
            var item = ContentProject.Decode(JsonConvert.SerializeObject(source)).Pages[0].Objects[0];
            Assert.AreEqual(.05f, item.X);
            Assert.AreEqual(.95f, item.Y);
            Assert.AreEqual(3f, item.Scale);
        }
    }
}
