using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Adeeb.EditorApp
{
    [Serializable]
    public sealed class ContentProject
    {
        public int SchemaVersion = 1;
        public string Id = Guid.NewGuid().ToString("N");
        public string Title = "My story";
        public long UpdatedAt;
        public List<ContentPage> Pages = new List<ContentPage> { new ContentPage() };
        public static ContentProject Decode(string json)
        {
            var value = JsonConvert.DeserializeObject<ContentProject>(json,
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            if (value == null || value.SchemaVersion != 1 || value.Pages == null || value.Pages.Count == 0)
                throw new InvalidOperationException("This project has an unsupported format.");
            foreach (var page in value.Pages)
            {
                if (page == null) throw new InvalidOperationException("A project page is missing.");
                page.Objects ??= new List<PlacedObject>();
                page.Objects.RemoveAll(item => item == null);
                foreach (var item in page.Objects)
                {
                    item.X = Clamp(item.X, 0.05f, 0.95f);
                    item.Y = Clamp(item.Y, 0.05f, 0.95f);
                    item.Scale = Clamp(item.Scale, 0.4f, 3f);
                }
            }
            return value;
        }
        public static float Clamp(float value, float min, float max) =>
            float.IsNaN(value) || float.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
    }
    [Serializable] public sealed class ContentPage
    {
        public string BackgroundId = "sky";
        public List<PlacedObject> Objects = new List<PlacedObject>();
    }
    [Serializable] public sealed class PlacedObject
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string AssetId;
        public float X = 0.5f, Y = 0.5f, Scale = 1f;
    }
    [Serializable] public sealed class ProjectSummary
    {
        public string Id, Title, CoverPng;
        public int PageCount;
        public long UpdatedAt;
    }
    [Serializable] public sealed class AssetCatalog
    {
        public List<CatalogAsset> Backgrounds = new List<CatalogAsset>();
        public List<CatalogAsset> Objects = new List<CatalogAsset>();
        public IEnumerable<CatalogAsset> All { get { foreach (var a in Backgrounds) yield return a; foreach (var a in Objects) yield return a; } }
        public CatalogAsset Find(string id) { foreach (var a in All) if (a.Id == id) return a; return null; }
    }
    [Serializable] public sealed class CatalogAsset { public string Id, Name, Url; }
}
