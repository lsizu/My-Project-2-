using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemDetailPanel : MonoBehaviour
{
    public TMP_Text itemText;
    public Image iconImage;
    private ItemData currentItem;

    public static void HideOtherPanels(MonoBehaviour activePanel)
    {
        if (activePanel == null) return;
        foreach (CharacterDetailPanel panel in Object.FindObjectsByType<CharacterDetailPanel>(FindObjectsInactive.Include))
            if (ShouldHide(panel, activePanel)) panel.gameObject.SetActive(false);
        foreach (EnemyDetailPanel panel in Object.FindObjectsByType<EnemyDetailPanel>(FindObjectsInactive.Include))
            if (ShouldHide(panel, activePanel)) panel.gameObject.SetActive(false);
        foreach (ItemDetailPanel panel in Object.FindObjectsByType<ItemDetailPanel>(FindObjectsInactive.Include))
            if (ShouldHide(panel, activePanel)) panel.Hide();
    }

    private static bool ShouldHide(MonoBehaviour panel, MonoBehaviour activePanel)
    {
        if (panel == null || panel.gameObject == activePanel.gameObject || panel.GetComponent<Canvas>() != null)
            return false;
        if (panel is CharacterDetailPanel && panel.gameObject.name != "CharacterDetailPanel")
            return false;
        if (panel is EnemyDetailPanel && panel.gameObject.name != "EnemyDetailPanel")
            return false;
        if (panel is ItemDetailPanel && panel.gameObject.name != "ItemDetailPanel")
            return false;
        return true;
    }

    public void ShowItem(ItemData item)
    {
        currentItem = item;
        if (item == null) { Hide(); return; }
        HideOtherPanels(this);
        if (iconImage == null)
        {
            Transform iconTransform = FindChildRecursive(transform, "Icon");
            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();
        }
        if (iconImage != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = item.icon != null;
        }

        if (itemText == null)
        {
            foreach (TMP_Text textComponent in GetComponentsInChildren<TMP_Text>(true))
            {
                if (textComponent.gameObject.name == "ItemText") { itemText = textComponent; break; }
            }
        }
        if (itemText == null)
        {
            Debug.LogError("ItemText was not found in ItemDetailPanel.", this);
            return;
        }
        itemText.text = item.description ?? string.Empty;
        GameObject visualRoot = FindVisualRoot();
        if (visualRoot != null && visualRoot != gameObject)
            visualRoot.SetActive(true);
        gameObject.SetActive(true);
    }

    public void RefreshDisplayedContent()
    {
        if (gameObject.activeInHierarchy && currentItem != null)
            ShowItem(currentItem);
    }

    public void Hide()
    {
        currentItem = null;
        GameObject visualRoot = FindVisualRoot();
        if (visualRoot != null && visualRoot != gameObject)
            visualRoot.SetActive(false);
        gameObject.SetActive(false);
    }

    private GameObject FindVisualRoot()
    {
        Transform start = iconImage != null ? iconImage.transform : (itemText != null ? itemText.transform : transform);
        for (Transform current = start; current != null; current = current.parent)
            if (current.name == "ItemDetailPanel") return current.gameObject;
        return null;
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName) return child;
            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null) return nested;
        }
        return null;
    }
}
