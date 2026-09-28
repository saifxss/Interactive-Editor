using System;
using System.Threading;
using Adeeb.Firebase;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Adeeb.EditorApp
{
    public sealed class EditorBootstrap : MonoBehaviour
    {
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        RemoteAssetService assets;
        EditorController controller;
        EditorView view;
        bool loadingAssets;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() { if (FindFirstObjectByType<EditorBootstrap>() == null) DontDestroyOnLoad(new GameObject("EditorBootstrap", typeof(EditorBootstrap))); }
        void Start()
        {
            if (EventSystem.current == null) { var input = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); input.transform.SetParent(transform); }
            var config = new FirebaseConfig(); var auth = new FirebaseAuthService(config, new PlayerPrefsSessionStore());
            controller = new EditorController(new FirebaseProjectRepository(auth, new FirebaseRestClient(config, auth)));
            assets = new RemoteAssetService(); view = gameObject.AddComponent<EditorView>(); view.Build(assets);
            controller.Changed += () => view.Render(controller);
            controller.Status += view.ShowStatus;
            controller.MenuLoaded += view.ShowMenu;
            view.NewRequested += () => { if (controller.Busy) return; view.ShowEditor(); controller.NewProject(); view.ShowStatus("Choose a background, add objects, then drag to arrange them."); };
            view.OpenRequested += async (id, play) => { await controller.OpenAsync(id, play); if (controller.Project != null && controller.Project.Id == id) { view.ShowEditor(); view.Render(controller); view.ShowStatus(play ? "Playback: use Previous and Next." : "Project loaded."); } };
            view.MenuRequested += () => { if (controller.Dirty) view.ConfirmDiscard(async () => await controller.MenuAsync()); else _ = controller.MenuAsync(); };
            view.SaveRequested += async () => { if (controller.CanEdit) await controller.SaveAsync(assets.CreateCover(controller.Project.Pages[0])); };
            view.PreviewRequested += controller.Preview;
            view.AddPageRequested += controller.AddPage;
            view.PageRequested += controller.Navigate;
            view.ScaleRequested += controller.Scale;
            view.TitleChanged += controller.SetTitle;
            view.BackgroundRequested += controller.Background;
            view.ObjectRequested += controller.AddObject;
            view.Selected += controller.Select;
            view.Moved += controller.Move;
            view.DeleteRequested += controller.DeleteSelected;
            view.RetryRequested += Retry;
            Retry();
        }
        async void Retry()
        {
            if (loadingAssets || controller.Busy) return;
            loadingAssets = true;
            try { if (assets.Catalog == null) await assets.LoadAsync(lifetime.Token); await controller.MenuAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception) { view.ShowStatus("Could not load the asset catalog. Check the connection and press Retry."); }
            finally { loadingAssets = false; }
        }
        void OnDestroy() { lifetime.Cancel(); lifetime.Dispose(); controller?.Dispose(); assets?.Dispose(); }
    }
}
