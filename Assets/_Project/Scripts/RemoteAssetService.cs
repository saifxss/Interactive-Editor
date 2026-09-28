using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adeeb.Firebase;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Adeeb.EditorApp
{
    public sealed class RemoteAssetService : IDisposable
    {
        public const string CatalogUrl = "https://adeeb-technical-test.web.app/catalog.json";
        readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public AssetCatalog Catalog { get; private set; }
        public Sprite Find(string id) => id != null && cache.TryGetValue(id, out var sprite) ? sprite : null;
        public async Task LoadAsync(CancellationToken token)
        {
            using var request = UnityWebRequest.Get(CatalogUrl);
            request.timeout = 20;
            await WebRequestAwaiter.SendAsync(request, token);
            if (request.result != UnityWebRequest.Result.Success) throw new InvalidOperationException("Catalog unavailable.");
            var catalog = JsonConvert.DeserializeObject<AssetCatalog>(request.downloadHandler.text);
            if (catalog?.Backgrounds == null || catalog.Objects == null || catalog.Backgrounds.Count == 0 || catalog.Backgrounds.Count + catalog.Objects.Count > 32)
                throw new InvalidOperationException("Invalid catalog.");
            var ids = new HashSet<string>();
            foreach (var asset in catalog.All)
            {
                if (asset == null || string.IsNullOrEmpty(asset.Id) || !ids.Add(asset.Id) || !Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                    throw new InvalidOperationException("Invalid catalog asset.");
                if (cache.ContainsKey(asset.Id)) continue;
                // Sequential loads keep peak decode memory predictable in WebGL.
                using var image = UnityWebRequestTexture.GetTexture(asset.Url, false);
                image.timeout = 20;
                await WebRequestAwaiter.SendAsync(image, token);
                if (image.result != UnityWebRequest.Result.Success) throw new InvalidOperationException("An asset could not be downloaded.");
                var texture = DownloadHandlerTexture.GetContent(image);
                if (texture.width > 2048 || texture.height > 2048) { UnityEngine.Object.Destroy(texture); throw new InvalidOperationException("Asset exceeds size limit."); }
                cache.Add(asset.Id, Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f)));
            }
            Catalog = catalog;
        }
        public string CreateCover(ContentPage page)
        {
            const int width = 320, height = 180;
            var pixels = new Color[width * height];
            var background = Find(page.BackgroundId);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                pixels[y * width + x] = background == null ? Color.white : background.texture.GetPixelBilinear((float)x / width, (float)y / height);
            foreach (var item in page.Objects)
            {
                var sprite = Find(item.AssetId); if (sprite == null) continue;
                float size = 0.17f * width * item.Scale;
                float left = item.X * width - size / 2, bottom = item.Y * height - size / 2;
                for (int y = Math.Max(0, (int)bottom); y < Math.Min(height, bottom + size); y++)
                    for (int x = Math.Max(0, (int)left); x < Math.Min(width, left + size); x++)
                    {
                        var color = sprite.texture.GetPixelBilinear((x - left) / size, (y - bottom) / size);
                        pixels[y * width + x] = Color.Lerp(pixels[y * width + x], color, color.a);
                    }
            }
            var cover = new Texture2D(width, height, TextureFormat.RGB24, false);
            cover.SetPixels(pixels); cover.Apply();
            var encoded = Convert.ToBase64String(cover.EncodeToPNG());
            UnityEngine.Object.Destroy(cover);
            return encoded;
        }
        public void Dispose()
        {
            foreach (var sprite in cache.Values) { UnityEngine.Object.Destroy(sprite.texture); UnityEngine.Object.Destroy(sprite); }
            cache.Clear();
        }
    }
}
