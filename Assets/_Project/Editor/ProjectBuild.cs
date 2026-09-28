using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class ProjectBuild
{
    public static void Prepare()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            AssetDatabase.ImportPackage(Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage"), false);
        }
        const string target = "Assets/_Project/Resources/Fonts/SearchFont.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target) == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Resources/Fonts/NotoNaskhArabic-Regular.ttf");
            if (source == null) throw new InvalidOperationException("Arabic font source missing.");
            var font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "SearchFont";
            AssetDatabase.CreateAsset(font, target);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
        }
        var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target);
        var latin = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        saved.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
        if (latin != null && !saved.fallbackFontAssetTable.Contains(latin)) saved.fallbackFontAssetTable.Add(latin);
        EditorUtility.SetDirty(saved);
        PlayerSettings.productName = "Adeeb Interactive Editor";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        AssetDatabase.SaveAssets();
    }

    public static void Web()
    {
        Prepare();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = Path.GetFullPath("../Builds/Interactive-Editor"),
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed.");
    }
}
