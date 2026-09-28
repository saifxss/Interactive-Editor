using System;
using System.IO;
using Adeeb.EditorApp;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class SceneComposer
{
    [MenuItem("Adeeb/Rebuild Story Studio Scene UI")]
    public static void Compose()
    {
        const string path = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var old = GameObject.Find("EditorBootstrap");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        old = GameObject.Find("EventSystem");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        var catalogPath = Path.GetFullPath("../RemoteAssets/catalog.json");
        if (!File.Exists(catalogPath)) throw new FileNotFoundException("Local asset manifest is missing.", catalogPath);
        var catalog = JsonConvert.DeserializeObject<AssetCatalog>(File.ReadAllText(catalogPath));
        if (catalog?.Backgrounds == null || catalog.Objects == null) throw new InvalidOperationException("Invalid asset manifest.");

        var host = new GameObject("EditorBootstrap", typeof(EditorBootstrap), typeof(EditorView));
        host.GetComponent<EditorView>().BuildScene(catalog);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Saved editable Story Studio Canvas and EventSystem to " + path);
    }
}
