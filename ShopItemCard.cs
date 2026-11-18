using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Displays and transfers an item shop offer to a hand slot.</summary>
public class ShopItemCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Image iconImage;
    private Image frameImage;
    private TMP_Text nameText;
    private TMP_Text priceText;
    private TMP_Text descriptionText;
    private ItemData itemData;
    private bool isDragging;
    private UIDragCardPreview.Session dragPreviewSession;

    public void SetItem(ItemData item)
    {
        if (item == null) return;
        itemData = item;
        ShopCard source = GetComponent<ShopCard>();
        if (source != null)
        {
            iconImage = source.iconImage;
            nameText = source.nameText;
            priceText = source.costText;
            frameImage = GetComponent<Image>();
            if (source.starText != null) source.starText.text = string.Empty;
        }
        if (iconImage == null) iconImage = FindImage("Icon", "ItemIcon");
        if (nameText == null) nameText = FindText("NameText", "ItemNameText");
        if (priceText == null) priceText = FindText("CostText", "PriceText", "ItemPriceText");
        descriptionText = FindText("DescriptionText", "ItemDescriptionText");

        Transform panel = FindDeep(transform, "Panel");
        if (panel != null)
            panel.gameObject.SetActive(false);
        else
            Debug.LogWarning("Panel was not found on the item shop card.", this);

        if (iconImage != null) iconImage.sprite = item.icon;
        if (nameText != null) nameText.text = item.itemName;
        if (priceText != null) priceText.text = item.purchasePrice.ToString();
        if (descriptionText != null) descriptionText.text = item.description;
        if (frameImage != null && source != null && source.cost1Sprite != null)
            frameImage.sprite = source.cost1Sprite;
        gameObject.SetActive(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (itemData == null) return;
        BoardManager manager = FindAnyObjectByType<BoardManager>();
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (manager != null) manager.ShowItemDetail(itemData);
            return;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemData == null) return;
        ShopSlot slot = GetComponentInParent<ShopSlot>();
        BoardManager manager = FindAnyObjectByType<BoardManager>();
        if (slot == null || manager == null) return;

        manager.SelectShopItem(itemData, slot, clearCharacterDetail: false);
        isDragging = true;
        dragPreviewSession = UIDragCardPreview.Begin(gameObject, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging) UIDragCardPreview.Update(dragPreviewSession, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;
        UIDragCardPreview.End(ref dragPreviewSession);

        GameObject target = eventData.pointerCurrentRaycast.gameObject;
        BoardManager manager = FindAnyObjectByType<BoardManager>();
        if (manager == null) return;
        HandSlot handSlot = target != null ? target.GetComponentInParent<HandSlot>() : null;
        if (handSlot != null)
        {
            manager.BuySelectedShopItemToHand(handSlot, clearCharacterDetail: false);
            return;
        }
        BoardSlot boardSlot = target != null ? target.GetComponentInParent<BoardSlot>() : null;
        if (boardSlot != null)
            manager.BuySelectedShopItemToFirstEmptyHandSlot(clearCharacterDetail: false);
    }

    private Image FindImage(params string[] names)
    {
        foreach (string name in names)
        {
            Transform child = FindDeep(transform, name);
            if (child != null)
            {
                Image image = child.GetComponent<Image>();
                if (image != null) return image;
            }
        }
        return null;
    }

    private TMP_Text FindText(params string[] names)
    {
        foreach (string name in names)
        {
            Transform child = FindDeep(transform, name);
            if (child != null)
            {
                TMP_Text text = child.GetComponent<TMP_Text>();
                if (text != null) return text;
            }
        }
        return null;
    }

    private Transform FindDeep(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName) return child;
            Transform nested = FindDeep(child, targetName);
            if (nested != null) return nested;
        }
        return null;
    }
}