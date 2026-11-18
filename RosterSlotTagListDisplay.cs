using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared display logic for tags represented by characters on the roster board.
/// </summary>
public abstract class RosterSlotTagListDisplay : MonoBehaviour
{
    public Transform listContainer;
    public Button buttonTemplate;
    public UnitHoverInfoPanel unitHoverInfoPanel;

    [Min(0.05f)]
    public float refreshInterval = 0.2f;

    private readonly List<Button> generatedButtons = new List<Button>();
    private readonly List<TMP_Text> generatedNameTmpTexts = new List<TMP_Text>();
    private readonly List<TMP_Text> generatedCountTmpTexts = new List<TMP_Text>();
    private readonly List<Text> generatedNameTexts = new List<Text>();
    private readonly List<Text> generatedCountTexts = new List<Text>();
    private float refreshTimer;

    protected abstract bool IsUnitList { get; }
    protected abstract string TemplateObjectName { get; }
    protected abstract string NameTextObjectName { get; }
    protected abstract string CountTextObjectName { get; }

    private sealed class TagEntry
    {
        public string name;
        public int placedCount;
        public int rosterCount;
        public int sourceOrder;
        public bool connected;
        public bool dimmed;
        public readonly HashSet<CharacterData> members = new HashSet<CharacterData>();
        public readonly List<RosterBoardSlot> placedSlots = new List<RosterBoardSlot>();
    }

    protected virtual void Awake()
    {
        ResolveReferences();
    }

    protected virtual void OnEnable()
    {
        Refresh();
    }

    protected virtual void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval) return;
        refreshTimer = 0f;
        Refresh();
    }

    public void Refresh()
    {
        if (!ResolveReferences()) return;

        RosterBoardDisplay rosterBoard = FindAnyObjectByType<RosterBoardDisplay>();
        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        if (rosterBoard == null || shopManager == null || shopManager.characters == null)
        {
            ApplyEntries(new List<TagEntry>());
            return;
        }

        List<TagEntry> entries = new List<TagEntry>();
        RosterBoardSlot[] slots = rosterBoard.slotContainer != null
            ? rosterBoard.slotContainer.GetComponentsInChildren<RosterBoardSlot>(true)
            : new RosterBoardSlot[0];
        int sourceOrder = 0;

        foreach (RosterBoardSlot slot in slots)
        {
            CharacterData character = slot != null ? slot.characterData : null;
            if (character == null) continue;

            string[] tags = IsUnitList ? character.units : character.attributes;
            if (tags == null) continue;

            HashSet<string> tagsOnCharacter = new HashSet<string>();
            foreach (string tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag) || !tagsOnCharacter.Add(tag)) continue;

                TagEntry entry = entries.Find(item => item.name == tag);
                if (entry == null)
                {
                    entry = new TagEntry { name = tag, sourceOrder = sourceOrder++ };
                    foreach (CharacterData rosterCharacter in shopManager.characters)
                    {
                        string[] rosterTags = rosterCharacter == null
                            ? null
                            : IsUnitList ? rosterCharacter.units : rosterCharacter.attributes;
                        if (rosterTags == null) continue;

                        foreach (string rosterTag in rosterTags)
                        {
                            if (string.Equals(rosterTag, tag, System.StringComparison.Ordinal))
                            {
                                entry.members.Add(rosterCharacter);
                                break;
                            }
                        }
                    }
                    entry.rosterCount = entry.members.Count;
                    entries.Add(entry);
                }

                entry.placedCount++;
                entry.placedSlots.Add(slot);
            }
        }

        entries.RemoveAll(entry => entry.placedCount < 2 ||
            (IsUnitList && !shopManager.IsUnitComplete(entry.name)));

        if (IsUnitList)
        {
            foreach (TagEntry entry in entries)
            {
                entry.connected = AreUnitSlotsConnected(entry.placedSlots, rosterBoard.columns);
                entry.dimmed = entry.placedCount != entry.rosterCount || !entry.connected;
            }

            foreach (TagEntry smallerEntry in entries)
            {
                foreach (TagEntry largerEntry in entries)
                {
                    if (largerEntry.placedCount != largerEntry.rosterCount ||
                        !largerEntry.connected ||
                        !IsStrictSubset(smallerEntry.members, largerEntry.members))
                        continue;

                    smallerEntry.dimmed = true;
                    break;
                }
            }
        }

        entries.Sort((a, b) =>
        {
            if (IsUnitList)
            {
                int dimmedComparison = a.dimmed.CompareTo(b.dimmed);
                if (dimmedComparison != 0) return dimmedComparison;
            }

            int countComparison = a.rosterCount.CompareTo(b.rosterCount);
            return countComparison != 0 ? countComparison : a.sourceOrder.CompareTo(b.sourceOrder);
        });

        ApplyEntries(entries);
    }

    private void ApplyEntries(List<TagEntry> entries)
    {
        while (generatedButtons.Count < entries.Count)
        {
            Button button = Instantiate(buttonTemplate, listContainer, false);
            button.name = buttonTemplate.name + "_Generated_" + generatedButtons.Count;

            TMP_Text nameTmp = FindTmpText(button, NameTextObjectName, 0);
            TMP_Text countTmp = FindTmpText(button, CountTextObjectName, 1);
            Text nameText = FindLegacyText(button, NameTextObjectName, 0);
            Text countText = FindLegacyText(button, CountTextObjectName, 1);
            if (nameTmp == null && nameText == null)
            {
                Debug.LogError(buttonTemplate.name + " needs a text component for its tag name.", button);
                Destroy(button.gameObject);
                return;
            }

            generatedButtons.Add(button);
            generatedNameTmpTexts.Add(nameTmp);
            generatedCountTmpTexts.Add(countTmp);
            generatedNameTexts.Add(nameText);
            generatedCountTexts.Add(countText);
            Transform panel = FindChild(button.transform, "Panel");
            if (panel != null && IsUnitList) panel.gameObject.SetActive(false);
        }

        for (int i = 0; i < generatedButtons.Count; i++)
        {
            bool visible = i < entries.Count;
            TagEntry entry = visible ? entries[i] : null;
            SetButtonVisible(generatedButtons[i], visible,
                visible && IsUnitList && entry.dimmed);
            SetText(generatedNameTmpTexts[i], generatedNameTexts[i], entry != null ? entry.name : string.Empty);
            SetText(generatedCountTmpTexts[i], generatedCountTexts[i], entry != null
                ? entry.placedCount + "/" + entry.rosterCount
                : string.Empty);

            if (visible)
            {
                UnitButtonHover hover = generatedButtons[i].GetComponent<UnitButtonHover>();
                if (hover == null) hover = generatedButtons[i].gameObject.AddComponent<UnitButtonHover>();
                hover.Initialize(entry.name, unitHoverInfoPanel, IsUnitList, true);
            }
        }
    }

    private bool AreUnitSlotsConnected(List<RosterBoardSlot> unitSlots, int columns)
    {
        if (unitSlots.Count < 2 || columns <= 0) return false;

        HashSet<RosterBoardSlot> remaining = new HashSet<RosterBoardSlot>(unitSlots);
        Queue<RosterBoardSlot> queue = new Queue<RosterBoardSlot>();
        RosterBoardSlot first = unitSlots[0];
        remaining.Remove(first);
        queue.Enqueue(first);

        while (queue.Count > 0)
        {
            RosterBoardSlot current = queue.Dequeue();
            int currentRow = current.slotIndex / columns;
            int currentColumn = current.slotIndex % columns;
            List<RosterBoardSlot> connectedNeighbors = new List<RosterBoardSlot>();

            foreach (RosterBoardSlot candidate in remaining)
            {
                int candidateRow = candidate.slotIndex / columns;
                int candidateColumn = candidate.slotIndex % columns;
                if (Mathf.Abs(currentRow - candidateRow) +
                    Mathf.Abs(currentColumn - candidateColumn) == 1)
                {
                    connectedNeighbors.Add(candidate);
                }
            }

            foreach (RosterBoardSlot neighbor in connectedNeighbors)
            {
                remaining.Remove(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return remaining.Count == 0;
    }

    private bool IsStrictSubset(
        HashSet<CharacterData> candidate,
        HashSet<CharacterData> container)
    {
        if (candidate.Count >= container.Count) return false;
        foreach (CharacterData character in candidate)
            if (!container.Contains(character)) return false;
        return true;
    }

    private bool ResolveReferences()
    {
        if (listContainer == null) listContainer = transform;
        if (listContainer != null && buttonTemplate == null)
        {
            Transform templateTransform = FindChild(listContainer, TemplateObjectName);
            if (templateTransform == null)
                templateTransform = listContainer.GetComponentInChildren<Button>(true)?.transform;
            if (templateTransform != null) buttonTemplate = templateTransform.GetComponent<Button>();
        }

        if (buttonTemplate == null || listContainer == null)
        {
            Debug.LogWarning("Assign the roster tag list and its button template.", this);
            return false;
        }

        SetButtonVisible(buttonTemplate, false);
        return true;
    }

    private TMP_Text FindTmpText(Button button, string childName, int fallbackIndex)
    {
        Transform child = FindChild(button.transform, childName);
        TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
        if (text != null) return text;
        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        return texts.Length > fallbackIndex ? texts[fallbackIndex] : null;
    }

    private Text FindLegacyText(Button button, string childName, int fallbackIndex)
    {
        Transform child = FindChild(button.transform, childName);
        Text text = child != null ? child.GetComponent<Text>() : null;
        if (text != null) return text;
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

    private void SetButtonVisible(Button button, bool visible, bool dimmed = false)
    {
        if (button == null) return;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? (dimmed ? 0.8f : 1f) : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = !visible;
    }
}
