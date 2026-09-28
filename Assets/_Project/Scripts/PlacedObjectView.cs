using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Adeeb.EditorApp
{
    public sealed class PlacedObjectView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string Id;
        public bool Editable;
        public RectTransform Stage;
        public Action<string> Selected;
        public Action<string, float, float> Moved;
        Vector2 offset;
        bool dragged;
        public void OnPointerClick(PointerEventData e) { if (Editable && !dragged) Selected?.Invoke(Id); }
        public void OnBeginDrag(PointerEventData e)
        {
            dragged = true;
            if (!Editable) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Stage, e.position, e.pressEventCamera, out var point);
            offset = ((RectTransform)transform).anchoredPosition - point;
        }
        public void OnDrag(PointerEventData e)
        {
            if (!Editable) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Stage, e.position, e.pressEventCamera, out var point);
            point += offset;
            var rect = Stage.rect;
            float x = Mathf.Clamp((point.x - rect.xMin) / rect.width, .05f, .95f);
            float y = Mathf.Clamp((point.y - rect.yMin) / rect.height, .05f, .95f);
            ((RectTransform)transform).anchoredPosition = new Vector2((x - .5f) * rect.width, (y - .5f) * rect.height);
            Moved?.Invoke(Id, x, y);
        }
        public void OnEndDrag(PointerEventData e) { if (Editable) Selected?.Invoke(Id); }
    }
}
