using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// A separate planning board for choosing characters from the complete roster.
/// It does not use BoardManager or the normal HandSlot gameplay flow.
/// </summary>
public class RosterBoardDisplay : MonoBehaviour
{
    [Header("スロット")]
    public Transform slotContainer;
    public GameObject handSlotPrefab;
    public GameObject handCardPrefab;
    public CharacterDetailPanel characterDetailPanel;

    [Header("出撃")]
    public Button launchButton;
    public Button randomFillButton;
    public TMP_Text announcementText;
    public string shopSceneName = "shop";

    [Header("配置数")]
    public int columns = 4;
    public int rows = 4;

    private CharacterData selectedCharacter;
    private List<CharacterData> selectedCharacters;
    private RosterBoardSlot selectedSourceSlot;
    private bool slotsGenerated;

    public CharacterData SelectedCharacter => selectedCharacter;

    private void Start()
    {
        ResetRosterCostsToOne();
        GenerateSlots();
        if (launchButton != null)
            launchButton.onClick.AddListener(Launch);
        if (randomFillButton != null)
            randomFillButton.onClick.AddListener(FillEmptySlotsRandomly);
    }

    private void OnDestroy()
    {
        if (launchButton != null)
            launchButton.onClick.RemoveListener(Launch);
        if (randomFillButton != null)
            randomFillButton.onClick.RemoveListener(FillEmptySlotsRandomly);
    }

    private void ResetRosterCostsToOne()
    {
        CharacterFullRosterListDisplay rosterList =
            FindAnyObjectByType<CharacterFullRosterListDisplay>();
        if (rosterList == null)
        {
            Debug.LogWarning("全キャラクター名簿が見つかりません。", this);
            return;
        }

        HashSet<CharacterData> resetCharacters = new HashSet<CharacterData>();
        foreach (CharacterData character in rosterList.GetFullRoster())
        {
            if (character != null && resetCharacters.Add(character))
                character.cost = 1;
        }
    }

    public void FillEmptySlotsRandomly()
    {
        if (slotContainer == null)
        {
            Debug.LogWarning("編成盤面が設定されていません。", this);
            return;
        }

        CharacterFullRosterListDisplay rosterList =
            FindAnyObjectByType<CharacterFullRosterListDisplay>();
        if (rosterList == null)
        {
            Debug.LogWarning("全キャラクター名簿が見つかりません。", this);
            return;
        }

        List<RosterBoardSlot> emptySlots = new List<RosterBoardSlot>();
        HashSet<CharacterData> placedCharacters = new HashSet<CharacterData>();
        foreach (RosterBoardSlot slot in slotContainer.GetComponentsInChildren<RosterBoardSlot>(true))
        {
            if (slot == null)
                continue;

            if (slot.characterData == null)
                emptySlots.Add(slot);
            else
                placedCharacters.Add(slot.characterData);
        }
        emptySlots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));

        List<CharacterData> candidates = new List<CharacterData>();
        HashSet<CharacterData> uniqueCandidates = new HashSet<CharacterData>();
        foreach (CharacterData character in rosterList.GetFullRoster())
        {
            if (character != null &&
                !placedCharacters.Contains(character) &&
                uniqueCandidates.Add(character))
            {
                candidates.Add(character);
            }
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            CharacterData temporary = candidates[i];
            candidates[i] = candidates[randomIndex];
            candidates[randomIndex] = temporary;
        }

        int count = Mathf.Min(emptySlots.Count, candidates.Count);
        for (int i = 0; i < count; i++)
            emptySlots[i].SetCharacter(candidates[i], handCardPrefab);

        Debug.Log("ランダム編成で " + count + "体を追加しました。", this);
    }

    public void Launch()
    {
        if (slotContainer == null)
        {
            ShowLaunchAnnouncement("編成盤面が設定されていません。");
            return;
        }

        List<RosterBoardSlot> slots = new List<RosterBoardSlot>(
            slotContainer.GetComponentsInChildren<RosterBoardSlot>(true));
        slots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));

        List<CharacterData> selectedRoster = new List<CharacterData>();
        HashSet<CharacterData> uniqueCharacters = new HashSet<CharacterData>();
        foreach (RosterBoardSlot slot in slots)
        {
            if (slot == null || slot.characterData == null)
                continue;

            if (uniqueCharacters.Add(slot.characterData))
                selectedRoster.Add(slot.characterData);
        }

        if (selectedRoster.Count != 30)
        {
            ShowLaunchAnnouncement("出撃にはキャラクターが30体必要です。現在の編成は " + selectedRoster.Count + "体です。");
            return;
        }

        if (string.IsNullOrWhiteSpace(shopSceneName))
        {
            ShowLaunchAnnouncement("Shopシーン名が設定されていません。");
            return;
        }

        DeckSaveLoad deckSaveLoad = GetComponent<DeckSaveLoad>();
        if (deckSaveLoad == null)
        {
            ShowLaunchAnnouncement("編成保存機能が見つかりません。");
            return;
        }

        if (!deckSaveLoad.TrySaveSelectedDeckForLaunch(out string saveError))
        {
            ShowLaunchAnnouncement(saveError);
            return;
        }

        ShopManager.SetPendingSelectedRoster(selectedRoster.ToArray());
        ShowLaunchAnnouncement(string.Empty);
        SceneManager.LoadScene(shopSceneName);
    }

    public bool ApplySavedDeck(CharacterData[] charactersBySlot)
    {
        if (slotContainer == null || charactersBySlot == null)
            return false;

        GenerateSlots();

        List<RosterBoardSlot> slots = new List<RosterBoardSlot>(
            slotContainer.GetComponentsInChildren<RosterBoardSlot>(true));
        slots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));

        if (slots.Count != charactersBySlot.Length)
            return false;

        HashSet<CharacterData> uniqueCharacters = new HashSet<CharacterData>();
        foreach (CharacterData character in charactersBySlot)
        {
            if (character != null && !uniqueCharacters.Add(character))
                return false;
        }

        selectedCharacter = null;
        selectedCharacters = null;
        selectedSourceSlot = null;

        for (int i = 0; i < slots.Count; i++)
        {
            if (charactersBySlot[i] == null)
                slots[i].ClearCharacter();
            else
                slots[i].SetCharacter(charactersBySlot[i], handCardPrefab);
        }

        return true;
    }

    private void ShowLaunchAnnouncement(string message)
    {
        if (announcementText != null)
        {
            announcementText.text = message;
            if (!string.IsNullOrEmpty(message))
                announcementText.gameObject.SetActive(true);
        }

        if (!string.IsNullOrEmpty(message))
            Debug.LogWarning(message, this);
    }

    public void SelectCharacter(CharacterData character)
    {
        if (character == null) return;

        selectedCharacter = character;
        selectedCharacters = null;
        selectedSourceSlot = null;
        Debug.Log("Roster board selected: " + character.characterName, this);
    }

    public bool SelectCharactersByTag(string tagName, bool isUnit)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return false;

        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        if (shopManager == null || shopManager.characters == null) return false;

        List<CharacterData> matchingCharacters = new List<CharacterData>();
        foreach (CharacterData character in shopManager.characters)
        {
            string[] tags = character == null
                ? null
                : isUnit ? character.units : character.attributes;
            if (tags == null) continue;
            foreach (string tag in tags)
            {
                if (string.Equals(tag, tagName, StringComparison.Ordinal))
                {
                    matchingCharacters.Add(character);
                    break;
                }
            }
        }

        if (matchingCharacters.Count == 0) return false;

        selectedCharacter = null;
        selectedCharacters = matchingCharacters;
        selectedSourceSlot = null;
        return true;
    }

    public void NotifyRosterChanged()
    {
        if (characterDetailPanel == null)
        {
            CharacterFullRosterListDisplay rosterList = FindAnyObjectByType<CharacterFullRosterListDisplay>();
            if (rosterList != null)
                characterDetailPanel = rosterList.characterDetailPanel;
        }

        if (characterDetailPanel != null)
            characterDetailPanel.RefreshRosterBoardTagLists();
    }

    public void ShowCharacterDetail(CharacterData character)
    {
        if (character == null) return;

        if (characterDetailPanel == null)
        {
            CharacterFullRosterListDisplay rosterList = FindAnyObjectByType<CharacterFullRosterListDisplay>();
            if (rosterList != null)
                characterDetailPanel = rosterList.characterDetailPanel;
        }

        if (characterDetailPanel == null)
        {
            BoardManager boardManager = FindAnyObjectByType<BoardManager>();
            if (boardManager != null)
                characterDetailPanel = boardManager.characterDetailPanel;
        }

        if (characterDetailPanel != null)
        {
            if (characterDetailPanel.TryToggleUnitAttributeFor(character))
                return;

            characterDetailPanel.ShowShopCharacter(character);
        }
        else
            Debug.LogWarning("CharacterDetailPanel is not assigned to RosterBoardDisplay or CharacterFullRosterListDisplay.", this);
    }

    public void SelectCharacterFromSlot(RosterBoardSlot sourceSlot)
    {
        if (sourceSlot == null || sourceSlot.characterData == null) return;

        selectedCharacter = sourceSlot.characterData;
        selectedCharacters = null;
        selectedSourceSlot = sourceSlot;
    }

    public void PlaceSelectedCharacter(RosterBoardSlot slot)
    {
        if (selectedCharacters != null)
        {
            PlaceSelectedCharacters();
            return;
        }

        if (selectedCharacter == null || slot == null) return;

        if (slot.characterData != null && slot != selectedSourceSlot)
        {
            if (selectedSourceSlot == null || selectedSourceSlot.characterData == null)
            {
                Debug.Log("Cannot swap into an occupied roster slot without a board source slot.", this);
                return;
            }

            CharacterData targetCharacter = slot.characterData;
            slot.SetCharacter(selectedCharacter, handCardPrefab);
            selectedSourceSlot.SetCharacter(targetCharacter, handCardPrefab);
            selectedSourceSlot = null;
            selectedCharacter = null;
            return;
        }

        if (IsCharacterPlacedInOtherSlot(selectedCharacter, slot, selectedSourceSlot))
        {
            Debug.Log("Roster board already contains: " + selectedCharacter.characterName, this);
            return;
        }

        slot.SetCharacter(selectedCharacter, handCardPrefab);

        if (selectedSourceSlot != null && selectedSourceSlot != slot)
            selectedSourceSlot.ClearCharacter();

        selectedSourceSlot = null;
    }

    public void PlaceSelectedCharacters()
    {
        if (selectedCharacters == null || slotContainer == null) return;

        List<RosterBoardSlot> emptySlots = new List<RosterBoardSlot>();
        foreach (RosterBoardSlot slot in slotContainer.GetComponentsInChildren<RosterBoardSlot>(true))
        {
            if (slot != null && slot.characterData == null)
                emptySlots.Add(slot);
        }
        emptySlots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));

        HashSet<CharacterData> alreadyPlaced = new HashSet<CharacterData>();
        foreach (RosterBoardSlot slot in slotContainer.GetComponentsInChildren<RosterBoardSlot>(true))
        {
            if (slot != null && slot.characterData != null)
                alreadyPlaced.Add(slot.characterData);
        }

        int nextEmptySlot = 0;
        foreach (CharacterData character in selectedCharacters)
        {
            if (character == null || !alreadyPlaced.Add(character)) continue;
            if (nextEmptySlot >= emptySlots.Count) break;

            emptySlots[nextEmptySlot].SetCharacter(character, handCardPrefab);
            nextEmptySlot++;
        }

        selectedCharacters = null;
    }

    private bool IsCharacterPlacedInOtherSlot(
        CharacterData character,
        RosterBoardSlot targetSlot,
        RosterBoardSlot sourceSlot)
    {
        if (character == null || slotContainer == null) return false;

        foreach (RosterBoardSlot slot in slotContainer.GetComponentsInChildren<RosterBoardSlot>(true))
        {
            if (slot == null || slot == targetSlot || slot == sourceSlot) continue;
            if (slot.characterData == character) return true;
        }

        return false;
    }

    public void GenerateSlots()
    {
        if (slotsGenerated) return;
        if (slotContainer == null) slotContainer = transform;

        if (handSlotPrefab == null)
        {
            Debug.LogError("Assign the HandSlot Prefab for the roster board.", this);
            return;
        }

        if (handCardPrefab == null)
        {
            Debug.LogError("Assign the HandCard Prefab for roster character previews.", this);
            return;
        }

        int slotCount = Mathf.Max(1, columns) * Mathf.Max(1, rows);
        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObject = Instantiate(handSlotPrefab, slotContainer, false);
            slotObject.name = "RosterBoardSlot_" + (i + 1);

            // Disable the gameplay HandSlot behavior so this board stays independent.
            HandSlot gameplaySlot = slotObject.GetComponent<HandSlot>();
            if (gameplaySlot != null) gameplaySlot.enabled = false;

            RosterBoardSlot rosterSlot = slotObject.GetComponent<RosterBoardSlot>();
            if (rosterSlot == null) rosterSlot = slotObject.AddComponent<RosterBoardSlot>();
            rosterSlot.Initialize(this, i);
        }

        GridLayoutGroup grid = slotContainer.GetComponent<GridLayoutGroup>();
        RectTransform prefabRect = handSlotPrefab.transform as RectTransform;
        if (grid == null)
        {
            grid = slotContainer.gameObject.AddComponent<GridLayoutGroup>();
            if (prefabRect != null) grid.cellSize = prefabRect.sizeDelta;
        }
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);

        slotsGenerated = true;
    }
}
