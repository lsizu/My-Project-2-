using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Creates a non-interactive visual copy of a dragged UI card and follows the pointer.
/// </summary>
public static class UIDragCardPreview
{
    private static readonly Dictionary<GameObject, Session> activeSessions = new Dictionary<GameObject, Session>();

    public sealed class Session
    {
        internal GameObject previewObject;
        internal RectTransform previewRect;
        internal Canvas canvas;
        internal GameObject source;
    }

    public static Session Begin(GameObject source, PointerEventData eventData)
    {
        if (source == null) return null;

        Canvas canvas = source.GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null) canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        GameObject preview = Object.Instantiate(source, canvas.transform, false);
        preview.name = source.name + "_DragPreview";
        DisableInteractions(preview);

        CanvasGroup group = preview.GetComponent<CanvasGroup>();
        if (group == null) group = preview.AddComponent<CanvasGroup>();
        group.alpha = 0.8f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Session session = new Session
        {
            previewObject = preview,
            previewRect = preview.transform as RectTransform,
            canvas = canvas,
            source = source
        };
        activeSessions[source] = session;
        Update(session, eventData);
        return session;
    }

    public static void Update(Session session, PointerEventData eventData)
    {
        if (session == null || session.previewRect == null || session.canvas == null)
            return;

        if (session.canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            session.previewRect.position = eventData.position;
            return;
        }

        RectTransform canvasRect = session.canvas.transform as RectTransform;
        if (canvasRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                eventData.position,
                session.canvas.worldCamera,
                out Vector3 worldPosition))
        {
            session.previewRect.position = worldPosition;
        }
    }

    public static void End(ref Session session)
    {
        if (session != null)
        {
            if (session.source != null)
                activeSessions.Remove(session.source);
            if (session.previewObject != null)
                Object.Destroy(session.previewObject);
            session.source = null;
        }
        session = null;
    }

    public static void EndForSource(GameObject source)
    {
        if (source == null || !activeSessions.TryGetValue(source, out Session session))
            return;

        End(ref session);
    }

    public static bool IsRosterBoardTarget(GameObject target)
    {
        return target != null && target.GetComponentInParent<RosterBoardSlot>() != null;
    }

    private static void DisableInteractions(GameObject preview)
    {
        foreach (HandCard component in preview.GetComponentsInChildren<HandCard>(true)) component.enabled = false;
        foreach (ShopCard component in preview.GetComponentsInChildren<ShopCard>(true)) component.enabled = false;
        foreach (BoardCharacter component in preview.GetComponentsInChildren<BoardCharacter>(true)) component.enabled = false;
        foreach (HandSlot component in preview.GetComponentsInChildren<HandSlot>(true)) component.enabled = false;
        foreach (BoardSlot component in preview.GetComponentsInChildren<BoardSlot>(true)) component.enabled = false;
        foreach (RosterBoardSlot component in preview.GetComponentsInChildren<RosterBoardSlot>(true)) component.enabled = false;
        foreach (Button component in preview.GetComponentsInChildren<Button>(true)) component.enabled = false;
        foreach (Graphic graphic in preview.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }
}
