using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays Attributes shared by multiple characters currently placed on the board.
/// The denominator is the number of selected-roster characters that have the Attribute.
/// </summary>
public class BoardAttributeListDisplay : MonoBehaviour
{
    public Transform boardAttributeList;
    public Button boardAttributeButtonTemplate;
    public UnitHoverInfoPanel unitHoverInfoPanel;

    [Min(0.05f)]
    public float refreshInterval = 0.2f;

    private readonly List<Button> generatedButtons = new List<Button>();
    private readonly List<TMP_Text> generatedNameTmpTexts = new List<TMP_Text>();
    private readonly List<TMP_Text> generatedCountTmpTexts = new List<TMP_Text>();
    private readonly List<Text> generatedNameLegacyTexts = new List<Text>();
    private readonly List<Text> generatedCountLegacyTexts = new List<Text>();
    private BoardSlot[] previousSlots = new BoardSlot[0];
    private OwnedCharacter[] previousCharacters = new OwnedCharacter[0];
    private CharacterData[] previousRoster = new CharacterData[0];
    private float refreshTimer;

    private class AttributeEntry
    {
        public string name;
        public int boardCount;
        public int rosterCount;
        public int sourceOrder;
    }

    private void Awake()
    {
        if (boardAttributeList == null) boardAttributeList = transform;
        ResolveTemplate();
    }

    private void OnEnable()
    {
        RefreshBoardAttributes();
        CacheBoardState();
    }

    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval) return;

        refreshTimer = 0f;
        if (HasStateChanged())
        {
            RefreshBoardAttributes();
            CacheBoardState();
        }
    }

    public void RefreshBoardAttributes()
    {
        if (boardAttributeList == null) boardAttributeList = transform;
        if (!ResolveTemplate()) return;

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        CharacterData[] roster = shopManager != null && shopManager.characters != null
            ? shopManager.characters
            : new CharacterData[0];
        List<AttributeEntry> entries = new List<AttributeEntry>();

        if (boardManager != null && boardManager.boardArea != null)
        {
            BoardSlot[] slots = boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true);
            int nextSourceOrder = 0;
            foreach (BoardSlot slot in slots)
            {
                if (slot == null || slot.placedCharacter == null || slot.placedCharacter.data == null)
                    continue;

                string[] attributes = slot.placedCharacter.data.attributes;
                if (attributes == null) continue;

                HashSet<string> attributesOnCharacter = new HashSet<string>();
                foreach (string attribute in attributes)
                {
                    if (string.IsNullOrWhiteSpace(attribute) || !attributesOnCharacter.Add(attribute))
                        continue;

                    AttributeEntry entry = entries.Find(item => item.name == attribute);
                    if (entry == null)
                    {
                        entry = new AttributeEntry
                        {
                            name = attribute,
                            rosterCount = CountRosterMembers(roster, attribute),
                            sourceOrder = nextSourceOrder++
                        };
                        entries.Add(entry);
                    }
                    entry.boardCount++;
                }
            }
        }

        // Show only Attributes held by at least two characters on the board.
        entries.RemoveAll(entry => entry.boardCount < 2);
        entries.Sort((a, b) =>
        {
            int countComparison = a.rosterCount.CompareTo(b.rosterCount);
            return countComparison != 0
                ? countComparison
                : a.sourceOrder.CompareTo(b.sourceOrder);
        });

        while (generatedButtons.Count < entries.Count)
        {
            Button button = Instantiate(boardAttributeButtonTemplate, boardAttributeList, false);
            button.name = boardAttributeButtonTemplate.name + "_Generated_" + generatedButtons.Count;

            TMP_Text nameTmp = FindTmpText(button, "BoardAttributeNameText", 0);
            TMP_Text countTmp = FindTmpText(button, "BoardAttributeCountText", 1);
            Text nameLegacy = FindLegacyText(button, "BoardAttributeNameText", 0);
            Text countLegacy = FindLegacyText(button, "BoardAttributeCountText", 1);
            if (nameTmp == null && nameLegacy == null)
            {
                Debug.LogError("BoardAttributeButton needs a TMP_Text or UI Text for the Attribute name.", button);
                Destroy(button.gameObject);
                return;
            }

            generatedButtons.Add(button);
            generatedNameTmpTexts.Add(nameTmp);
            generatedCountTmpTexts.Add(countTmp);
            generatedNameLegacyTexts.Add(nameLegacy);
            generatedCountLegacyTexts.Add(countLegacy);
        }

        for (int i = 0; i < generatedButtons.Count; i++)
        {
            bool visible = i < entries.Count;
            SetButtonVisible(generatedButtons[i], visible);

            if (visible)
            {
                UnitButtonHover hover = generatedButtons[i].GetComponent<UnitButtonHover>();
                if (hover == null)
                {
                    hover = generatedButtons[i].gameObject.AddComponent<UnitButtonHover>();
                }
                hover.Initialize(entries[i].name, unitHoverInfoPanel, false, true);
            }

            string attributeName = visible ? entries[i].name : string.Empty;
            string count = visible
                ? entries[i].boardCount + "/" + entries[i].rosterCount
                : string.Empty;
            SetText(generatedNameTmpTexts[i], generatedNameLegacyTexts[i], attributeName);
            SetText(generatedCountTmpTexts[i], generatedCountLegacyTexts[i], count);
        }
    }

    private int CountRosterMembers(CharacterData[] roster, string attributeName)
    {
        int count = 0;
        foreach (CharacterData character in roster)
        {
            if (character == null || character.attributes == null) continue;
            foreach (string attribute in character.attributes)
            {
                if (string.Equals(attribute, attributeName, System.StringComparison.Ordinal))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private bool ResolveTemplate()
    {
        if (boardAttributeList == null) boardAttributeList = transform;
        if (boardAttributeButtonTemplate == null && boardAttributeList != null)
        {
            Transform template = FindChild(boardAttributeList, "BoardAttributeButton");
            boardAttributeButtonTemplate = template != null
                ? template.GetComponent<Button>()
                : boardAttributeList.GetComponentInChildren<Button>(true);
        }

        if (boardAttributeList == null || boardAttributeButtonTemplate == null)
        {
            Debug.LogWarning("Assign Board Attribute List and Board Attribute Button Template.", this);
            return false;
        }

        SetButtonVisible(boardAttributeButtonTemplate, false);
        return true;
    }

    private bool HasStateChanged()
    {
        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        CharacterData[] roster = shopManager != null && shopManager.characters != null
            ? shopManager.characters
            : new CharacterData[0];
        if (roster.Length != previousRoster.Length) return true;
        for (int i = 0; i < roster.Length; i++)
            if (roster[i] != previousRoster[i]) return true;

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        BoardSlot[] slots = boardManager != null && boardManager.boardArea != null
            ? boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true)
            : new BoardSlot[0];
        if (slots.Length != previousSlots.Length) return true;
        for (int i = 0; i < slots.Length; i++)
        {
            OwnedCharacter character = slots[i] != null ? slots[i].placedCharacter : null;
            if (slots[i] != previousSlots[i] || character != previousCharacters[i]) return true;
        }
        return false;
    }

    private void CacheBoardState()
    {
        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        previousRoster = shopManager != null && shopManager.characters != null
            ? (CharacterData[])shopManager.characters.Clone()
            : new CharacterData[0];

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        previousSlots = boardManager != null && boardManager.boardArea != null
            ? boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true)
            : new BoardSlot[0];
        previousCharacters = new OwnedCharacter[previousSlots.Length];
        for (int i = 0; i < previousSlots.Length; i++)
            previousCharacters[i] = previousSlots[i] != null ? previousSlots[i].placedCharacter : null;
    }

    private TMP_Text FindTmpText(Button button, string childName, int fallbackIndex)
    {
        Transform child = FindChild(button.transform, childName);
        TMP_Text namedText = child != null ? child.GetComponent<TMP_Text>() : null;
        if (namedText != null) return namedText;
        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        return texts.Length > fallbackIndex ? texts[fallbackIndex] : null;
    }

    private Text FindLegacyText(Button button, string childName, int fallbackIndex)
    {
        Transform child = FindChild(button.transform, childName);
        Text namedText = child != null ? child.GetComponent<Text>() : null;
        if (namedText != null) return namedText;
        Text[] texts = button.GetComponentsInChildren<Text>(true);
        return texts.Length > fallbackIndex ? texts[fallbackIndex] : null;
    }

    private Transform FindChild(Transform parent, string childName)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform nested = FindChild(child, childName);
            if (nested != null) return nested;
        }
        return null;
    }

    private void SetText(TMP_Text tmpText, Text legacyText, string value)
    {
        if (tmpText != null) tmpText.text = value;
        if (legacyText != null) legacyText.text = value;
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button == null) return;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = !visible;
    }
}
