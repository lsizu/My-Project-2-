using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailTabs : MonoBehaviour
{
    [Header("タブ内容")]
    public GameObject basicPanel;
    public GameObject unitList;
    public GameObject attributePanel;

    [Header("Tab Button Colors")]
    public Button basicTabButton;
    public Button unitTabButton;
    public Button attributeTabButton;
    public Color selectedTabColor = Color.white;
    public Color unselectedTabColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private enum TabKind { Basic, Unit, Attribute }

    private void Awake()
    {
        if (basicTabButton == null) basicTabButton = FindButton("TabBasicButton");
        if (unitTabButton == null) unitTabButton = FindButton("TabUnitButton");
        if (attributeTabButton == null) attributeTabButton = FindButton("TabAttributeButton");
    }

    private void OnEnable()
    {
        RefreshTabButtonColorsFromActivePanel();
    }

    public void ShowBasic()
    {
        basicPanel.SetActive(true);
        unitList.SetActive(false);
        attributePanel.SetActive(false);
        SetSelectedTab(TabKind.Basic);
    }

    public void ShowUnit()
    {
        basicPanel.SetActive(false);
        unitList.SetActive(true);
        attributePanel.SetActive(false);
        SetSelectedTab(TabKind.Unit);
        RefreshUnitLayout();
    }

    public void RefreshUnitLayout()
    {
        if (unitList == null) return;

        Canvas.ForceUpdateCanvases();
        RectTransform listRect = unitList.transform as RectTransform;
        if (listRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);

        ScrollRect scrollRect = unitList.GetComponentInParent<ScrollRect>();
        if (scrollRect != null && scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.Rebuild(CanvasUpdate.PostLayout);
            scrollRect.verticalNormalizedPosition = 1f;
        }

        Canvas.ForceUpdateCanvases();
    }

    private void RefreshTabButtonColorsFromActivePanel()
    {
        if (attributePanel != null && attributePanel.activeSelf)
            SetSelectedTab(TabKind.Attribute);
        else if (unitList != null && unitList.activeSelf)
            SetSelectedTab(TabKind.Unit);
        else
            SetSelectedTab(TabKind.Basic);
    }

    private void SetSelectedTab(TabKind selected)
    {
        SetTabButtonColor(basicTabButton, selected == TabKind.Basic ? selectedTabColor : unselectedTabColor);
        SetTabButtonColor(unitTabButton, selected == TabKind.Unit ? selectedTabColor : unselectedTabColor);
        SetTabButtonColor(attributeTabButton, selected == TabKind.Attribute ? selectedTabColor : unselectedTabColor);
    }

    private void SetTabButtonColor(Button button, Color color)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = color;
        colors.pressedColor = color;
        colors.selectedColor = color;
        colors.disabledColor = color;
        button.colors = colors;
    }

    private Button FindButton(string buttonName)
    {
        Transform tabButtons = FindChildByName(transform, "TabButtons");
        Transform searchRoot = tabButtons != null ? tabButtons : transform;
        Transform found = FindChildByName(searchRoot, buttonName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
            Transform nested = FindChildByName(child, childName);
            if (nested != null) return nested;
        }
        return null;
    }

    public void ShowAttribute()
    {
        basicPanel.SetActive(false);
        unitList.SetActive(false);
        attributePanel.SetActive(true);
        SetSelectedTab(TabKind.Attribute);
    }

    public bool ToggleUnitAttribute()
    {
        if (unitList != null && unitList.activeSelf)
        {
            ShowAttribute();
            return true;
        }

        if (attributePanel != null && attributePanel.activeSelf)
        {
            ShowUnit();
            return true;
        }

        return false;
    }
}