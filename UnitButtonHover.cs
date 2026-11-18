using UnityEngine;
using UnityEngine.EventSystems;

public class UnitButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private string tagName;
    private bool isUnit;
    private bool showPanelOnRight;
    private UnitHoverInfoPanel hoverPanel;
    private bool isDraggingUnitButton;
    private UIDragCardPreview.Session dragPreviewSession;

    public void Initialize(
        string name,
        UnitHoverInfoPanel panel,
        bool representsUnit = true,
        bool showOnRight = false)
    {
        tagName = name;
        hoverPanel = panel;
        isUnit = representsUnit;
        showPanelOnRight = showOnRight;
    }

    private void ResolveHoverPanel()
    {
        if (hoverPanel != null)
            return;

        CharacterDetailPanel detailPanel =
            FindAnyObjectByType<CharacterDetailPanel>(FindObjectsInactive.Include);
        if (detailPanel != null)
            hoverPanel = detailPanel.GetOrCreateUnitHoverInfoPanel();
        if (hoverPanel == null)
            hoverPanel = FindAnyObjectByType<UnitHoverInfoPanel>(FindObjectsInactive.Include);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        ResolveHoverPanel();
        if (hoverPanel == null || string.IsNullOrWhiteSpace(tagName))
        {
            return;
        }

        RectTransform buttonRect = transform as RectTransform;
        if (buttonRect != null)
        {
            hoverPanel.Show(this, tagName, buttonRect, isUnit, showPanelOnRight);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideHoverPanel();
    }

    private void OnDisable()
    {
        HideHoverPanel();
    }

    private void HideHoverPanel()
    {
        if (hoverPanel != null)
            hoverPanel.Hide(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ResolveHoverPanel();
        RosterBoardDisplay rosterBoard = FindAnyObjectByType<RosterBoardDisplay>();
        if (rosterBoard == null || !rosterBoard.SelectCharactersByTag(tagName, isUnit)) return;

        isDraggingUnitButton = true;
        RectTransform buttonRect = transform as RectTransform;
        if (hoverPanel != null && buttonRect != null)
        {
            hoverPanel.Show(this, tagName, buttonRect, isUnit, showPanelOnRight);
            dragPreviewSession = UIDragCardPreview.Begin(hoverPanel.gameObject, eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDraggingUnitButton)
            UIDragCardPreview.Update(dragPreviewSession, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingUnitButton) return;

        isDraggingUnitButton = false;
        UIDragCardPreview.End(ref dragPreviewSession);
        if (hoverPanel != null)
            hoverPanel.Hide(this);

        GameObject dropObject = eventData.pointerCurrentRaycast.gameObject;
        RosterBoardSlot targetSlot = dropObject != null
            ? dropObject.GetComponentInParent<RosterBoardSlot>()
            : null;
        if (targetSlot != null)
            targetSlot.ClickSlot();
    }
}
