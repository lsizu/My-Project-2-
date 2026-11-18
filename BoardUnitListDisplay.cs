using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoardUnitListDisplay : MonoBehaviour
{
    [Header("表示先")]
    public Transform boardUnitList;

    [Header("Prefab")]
    public Button boardUnitButtonTemplate;
    public Button boardUnitButtonStillTemplate;
    public Button boardUnitButtonCharacterTemplate;

    [Header("既存機能")]
    public UnitHoverInfoPanel unitHoverInfoPanel;

    [Min(0.05f)]
    public float refreshInterval = 0.2f;

    private readonly List<GeneratedItem> generatedItems = new List<GeneratedItem>();
    private readonly Dictionary<Button, Image> inactiveOverlays =
        new Dictionary<Button, Image>();

    private BoardSlot[] previousSlots = new BoardSlot[0];
    private OwnedCharacter[] previousCharacters = new OwnedCharacter[0];
    private int[] previousStars = new int[0];
    private int[] previousAttacks = new int[0];
    private int[] previousAttackBonuses = new int[0];
    private CharacterData[] previousRoster = new CharacterData[0];

    private float refreshTimer;
    private int activeUnitSynergyBonus;
    public int ActiveUnitSynergyBonus => activeUnitSynergyBonus;

    private class UnitEntry
    {
        public string name;
        public int boardCount;
        public int selectedCount;
        public int totalCount;
        public readonly HashSet<CharacterData> selectedMembers = new HashSet<CharacterData>();
        public readonly HashSet<CharacterData> boardMembers = new HashSet<CharacterData>();
        public readonly List<BoardSlot> boardSlots = new List<BoardSlot>();
        public readonly List<int> boardSlotIndexes = new List<int>();
        public bool connected;
        public bool synergyInactive;
    }

    private enum DisplayItemType { UnitMove, NormalAttack, IncompleteUnit }

    private class DisplayItem
    {
        public DisplayItemType type;
        public UnitEntry unit;
        public string characterName;
        public int attack;
    }

    private class GeneratedItem
    {
        public Button button;
        public Button sourceTemplate;
        public TMP_Text nameText;
        public TMP_Text fractionText;
        public TMP_Text attackText;
    }

    private class BoardActor
    {
        public BoardSlot slot;
        public OwnedCharacter character;
        public CharacterData data;
        public int slotIndex;
        public int attack;
        public int star;
    }

    private void Awake()
    {
        if (boardUnitList == null) boardUnitList = transform;
        ResolveTemplates();
    }

    private void OnEnable()
    {
        RefreshBoardUnits();
        CacheBoardState();
    }

    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval) return;

        refreshTimer = 0f;
        if (HasStateChanged())
        {
            RefreshBoardUnits();
            CacheBoardState();
        }
    }

    public void RefreshBoardUnits()
    {
        if (boardUnitList == null) boardUnitList = transform;
        if (!ResolveTemplates()) return;

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        BattleManager battleManager = FindAnyObjectByType<BattleManager>();

        CharacterData[] roster =
            shopManager != null && shopManager.characters != null
                ? shopManager.characters
                : new CharacterData[0];
        BoardSlot[] slots =
            boardManager != null && boardManager.boardArea != null
                ? boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true)
                : new BoardSlot[0];
        CharacterStatSettings settings =
            battleManager != null ? battleManager.characterStatSettings : null;

        List<BoardActor> actors = BuildBoardActors(slots, settings);
        List<UnitEntry> entries = BuildUnitEntries(actors, roster, shopManager, boardManager);

        List<UnitEntry> activeMoves = entries.FindAll(entry =>
            !entry.synergyInactive && entry.boardCount >= 2);

                activeMoves.Sort(CompareUnitMovePriority);

        activeUnitSynergyBonus = 0;
        foreach (UnitEntry entry in entries)
            if (!entry.synergyInactive)
                activeUnitSynergyBonus += GetUnitSynergyBonus(entry.selectedCount);

        List<DisplayItem> displayItems = BuildBattleOrder(actors, activeMoves);

        // 実際に攻撃順に採用されたUnitを取得
        HashSet<UnitEntry> displayedMoves = new HashSet<UnitEntry>();

        foreach (DisplayItem item in displayItems)
        {
            if (item.type == DisplayItemType.UnitMove && item.unit != null)
            {
                displayedMoves.Add(item.unit);
            }
        }

        // Unitとして成立していても、攻撃順に採用されなかったものはStillで表示
        List<UnitEntry> incomplete = entries.FindAll(entry =>
            !displayedMoves.Contains(entry));

        incomplete.Sort(CompareIncompleteUnitPriority);

        foreach (UnitEntry entry in incomplete)
        {
            displayItems.Add(new DisplayItem
            {
                type = DisplayItemType.IncompleteUnit,
                unit = entry
            });
        }

        RenderItems(displayItems);
    }

    private List<BoardActor> BuildBoardActors(
        BoardSlot[] slots, CharacterStatSettings settings)
    {
        List<BoardActor> actors = new List<BoardActor>();
        for (int i = 0; i < slots.Length; i++)
        {
            BoardSlot slot = slots[i];
            if (slot == null || slot.placedCharacter == null ||
                slot.placedCharacter.data == null) continue;

            OwnedCharacter owned = slot.placedCharacter;
            int attack = settings != null
                ? owned.GetAttack(settings) + owned.battleAttackBonus
                : owned.attack + owned.battleAttackBonus;

            actors.Add(new BoardActor
            {
                slot = slot,
                character = owned,
                data = owned.data,
                slotIndex = i,
                attack = attack,
                star = Mathf.Clamp(owned.star, 1, 3)
            });
        }
        return actors;
    }

    private List<UnitEntry> BuildUnitEntries(
        List<BoardActor> actors,
        CharacterData[] roster,
        ShopManager shopManager,
        BoardManager boardManager)
    {
        List<UnitEntry> entries = new List<UnitEntry>();

        foreach (BoardActor actor in actors)
        {
            if (actor.data.units == null) continue;
            HashSet<string> uniqueUnits = new HashSet<string>();

            foreach (string unitName in actor.data.units)
            {
                if (string.IsNullOrWhiteSpace(unitName) ||
                    !uniqueUnits.Add(unitName)) continue;

                UnitEntry entry = entries.Find(x => x.name == unitName);
                if (entry == null)
                {
                    entry = new UnitEntry { name = unitName };

                    // 今回のゲームで選ばれたメンバーを取得
                    CharacterData[] selectedRoster =
                        shopManager != null && shopManager.characters != null
                            ? shopManager.characters
                            : new CharacterData[0];

                    foreach (CharacterData character in selectedRoster)
                    {
                        if (CharacterHasUnit(character, unitName))
                            entry.selectedMembers.Add(character);
                    }

                    // 今回のゲームで選ばれた人数
                    entry.selectedCount = entry.selectedMembers.Count;

                    // 全名簿におけるUnitの総人数
                    entry.totalCount = shopManager != null
                        ? shopManager.GetTotalUnitMemberCount(unitName)
                        : entry.selectedCount;

                    entries.Add(entry);
                }

                entry.boardCount++;
                entry.boardMembers.Add(actor.data);
                entry.boardSlots.Add(actor.slot);
                entry.boardSlotIndexes.Add(actor.slotIndex);
            }
        }

            // 盤面に2人以上いて、今回のゲームのRosterに全メンバーが揃っているUnitだけ表示
            entries.RemoveAll(entry =>
                entry.boardCount < 2 ||
                entry.totalCount <= 0 ||
                entry.selectedCount != entry.totalCount);

        foreach (UnitEntry entry in entries)
        {
            entry.connected = boardManager != null &&
                AreUnitSlotsConnected(entry.boardSlots, boardManager.columns);

            bool rosterComplete = shopManager != null &&
                shopManager.IsUnitComplete(entry.name);

            entry.synergyInactive =
                !rosterComplete ||
                entry.boardCount != entry.selectedCount ||
                !entry.boardMembers.SetEquals(entry.selectedMembers) ||
                !entry.connected;
        }

        return entries;
    }

    private void ApplySubsetSuppression(
        List<UnitEntry> entries, List<UnitEntry> activeMoves)
    {
        foreach (UnitEntry smaller in entries)
        {
            foreach (UnitEntry larger in activeMoves)
            {
                if (smaller != larger &&
                    IsStrictSubset(smaller.selectedMembers, larger.selectedMembers))
                {
                    smaller.synergyInactive = true;
                    break;
                }
            }
        }
    }

    private List<DisplayItem> BuildBattleOrder(
        List<BoardActor> actors, List<UnitEntry> activeMoves)
    {
        List<DisplayItem> result = new List<DisplayItem>();
        Dictionary<BoardSlot, int> participationCounts =
            new Dictionary<BoardSlot, int>();
        HashSet<UnitEntry> usedMoves = new HashSet<UnitEntry>();

        foreach (BoardActor actingActor in actors)
        {
            if (GetParticipationCount(participationCounts, actingActor.slot) >=
                actingActor.star)
                continue;

            while (GetParticipationCount(participationCounts, actingActor.slot) <
                   actingActor.star)
            {
                UnitEntry selectedMove = null;

                foreach (UnitEntry move in activeMoves)
                {
                    if (usedMoves.Contains(move) ||
                        !move.boardSlots.Contains(actingActor.slot))
                        continue;

                    bool available = true;
                    foreach (BoardSlot participantSlot in move.boardSlots)
                    {
                        OwnedCharacter participant = participantSlot != null
                            ? participantSlot.placedCharacter : null;
                        if (participant == null ||
                            GetParticipationCount(participationCounts, participantSlot) >=
                            Mathf.Clamp(participant.star, 1, 3))
                        {
                            available = false;
                            break;
                        }
                    }

                    if (available)
                    {
                        selectedMove = move;
                        break;
                    }
                }

                if (selectedMove == null) break;

                usedMoves.Add(selectedMove);
                int combinedAttack = 0;
                foreach (BoardSlot participantSlot in selectedMove.boardSlots)
                {
                    BoardActor participantActor =
                        actors.Find(x => x.slot == participantSlot);
                    if (participantActor != null)
                        combinedAttack += participantActor.attack;

                    participationCounts[participantSlot] =
                        GetParticipationCount(participationCounts, participantSlot) + 1;
                }

                result.Add(new DisplayItem
                {
                    type = DisplayItemType.UnitMove,
                    unit = selectedMove,
                    attack = combinedAttack
                });
            }

            // BattleManager skips normal attacks for anyone who participated
            // in at least one UnitMove during this turn.
            if (GetParticipationCount(participationCounts, actingActor.slot) == 0)
            {
                string name = string.IsNullOrEmpty(actingActor.data.characterName)
                    ? actingActor.data.name
                    : actingActor.data.characterName;

                result.Add(new DisplayItem
                {
                    type = DisplayItemType.NormalAttack,
                    characterName = name,
                    attack = actingActor.attack
                });
            }
        }

        return result;
    }

    private int CompareUnitMovePriority(UnitEntry left, UnitEntry right)
    {
        int countComparison = right.boardSlots.Count.CompareTo(left.boardSlots.Count);
        if (countComparison != 0) return countComparison;

        for (int i = 0; i < left.boardSlotIndexes.Count; i++)
        {
            int slotComparison =
                left.boardSlotIndexes[i].CompareTo(right.boardSlotIndexes[i]);
            if (slotComparison != 0) return slotComparison;
        }

        return string.CompareOrdinal(left.name, right.name);
    }

    private int CompareIncompleteUnitPriority(UnitEntry left, UnitEntry right)
    {
        // 総人数と盤面人数が一致しているUnitを優先
        bool leftComplete = left.boardCount == left.totalCount;
        bool rightComplete = right.boardCount == right.totalCount;

        int completeComparison =
            rightComplete.CompareTo(leftComplete);

        if (completeComparison != 0)
            return completeComparison;

        // それ以外は盤面人数（分子）が多い順
        int boardComparison =
            right.boardCount.CompareTo(left.boardCount);

        if (boardComparison != 0)
            return boardComparison;

        // 盤面人数が同じなら総人数（分母）が少ない順
        int totalComparison =
            left.totalCount.CompareTo(right.totalCount);

        if (totalComparison != 0)
            return totalComparison;

        return CompareUnitMovePriority(left, right);
    }

    private int GetParticipationCount(
        Dictionary<BoardSlot, int> counts, BoardSlot slot)
    {
        return counts.TryGetValue(slot, out int count) ? count : 0;
    }

    private bool CharacterHasUnit(CharacterData character, string unitName)
    {
        if (character == null || character.units == null) return false;
        foreach (string item in character.units)
            if (string.Equals(item, unitName, StringComparison.Ordinal)) return true;
        return false;
    }

    private bool AreUnitSlotsConnected(List<BoardSlot> unitSlots, int columns)
    {
        if (unitSlots.Count < 2 || columns <= 0) return false;

        HashSet<BoardSlot> remaining = new HashSet<BoardSlot>(unitSlots);
        Queue<BoardSlot> queue = new Queue<BoardSlot>();
        BoardSlot first = unitSlots[0];
        remaining.Remove(first);
        queue.Enqueue(first);

        while (queue.Count > 0)
        {
            BoardSlot current = queue.Dequeue();
            int index = current.transform.GetSiblingIndex();
            int row = index / columns;
            int column = index % columns;

            List<BoardSlot> neighbors = new List<BoardSlot>();
            foreach (BoardSlot candidate in remaining)
            {
                int candidateIndex = candidate.transform.GetSiblingIndex();
                int candidateRow = candidateIndex / columns;
                int candidateColumn = candidateIndex % columns;

                if (Mathf.Abs(row - candidateRow) +
                    Mathf.Abs(column - candidateColumn) == 1)
                    neighbors.Add(candidate);
            }

            foreach (BoardSlot neighbor in neighbors)
            {
                remaining.Remove(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return remaining.Count == 0;
    }

    private int GetUnitSynergyBonus(int count)
    {
        switch (count)
        {
            case 2: return 2;
            case 3: return 5;
            case 4: return 9;
            case 5: return 14;
            case 6: return 20;
            case 7: return 27;
            default: return count >= 8 ? 35 : 0;
        }
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

    private void RenderItems(List<DisplayItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            DisplayItem item = items[i];
            Button template = GetTemplate(item.type);
            if (template == null) continue;

            GeneratedItem generated = GetOrCreateGeneratedItem(i, template, item.type);
            if (generated == null) continue;

            generated.button.transform.SetAsLastSibling();

            bool dimmed = item.type == DisplayItemType.IncompleteUnit ||
                (item.type == DisplayItemType.UnitMove &&
                 item.unit != null && item.unit.synergyInactive);
            SetButtonVisible(generated.button, true, dimmed);

            if (item.type == DisplayItemType.NormalAttack)
            {
                SetText(generated.nameText, item.characterName);
                SetText(generated.fractionText, string.Empty);
                SetText(generated.attackText, item.attack.ToString());
            }
            else
            {
                UnitEntry unit = item.unit;
                SetText(generated.nameText, unit.name);
                SetText(generated.fractionText,
                    unit.boardCount + "/" + unit.totalCount);
                SetText(generated.attackText,
                    item.type == DisplayItemType.UnitMove
                        ? item.attack.ToString()
                        : string.Empty);

                UnitButtonHover hover =
                    generated.button.GetComponent<UnitButtonHover>();
                if (hover == null)
                    hover = generated.button.gameObject.AddComponent<UnitButtonHover>();
                hover.Initialize(unit.name, unitHoverInfoPanel, true, true);
            }
        }

        for (int i = items.Count; i < generatedItems.Count; i++)
            if (generatedItems[i] != null)
                SetButtonVisible(generatedItems[i].button, false);
    }

    private Button GetTemplate(DisplayItemType type)
    {
        switch (type)
        {
            case DisplayItemType.UnitMove: return boardUnitButtonTemplate;
            case DisplayItemType.IncompleteUnit: return boardUnitButtonStillTemplate;
            case DisplayItemType.NormalAttack: return boardUnitButtonCharacterTemplate;
            default: return null;
        }
    }

    private GeneratedItem GetOrCreateGeneratedItem(
        int index, Button template, DisplayItemType type)
    {
        while (generatedItems.Count <= index)
            generatedItems.Add(null);

        GeneratedItem existing = generatedItems[index];
        if (existing != null && existing.button != null &&
            existing.sourceTemplate == template)
            return existing;

        if (existing != null && existing.button != null)
            Destroy(existing.button.gameObject);

        Button button = Instantiate(template, boardUnitList, false);
        button.name = template.name + "_Generated_" + index;

        GeneratedItem generated = new GeneratedItem
        {
            button = button,
            sourceTemplate = template
        };

        switch (type)
        {
            case DisplayItemType.UnitMove:
                generated.nameText = FindTmpText(button, "UnitName");
                generated.fractionText = FindTmpText(button, "UnitFraction");
                generated.attackText = FindTmpText(button, "UnitAtk");
                break;

            case DisplayItemType.IncompleteUnit:
                generated.nameText = FindTmpText(button, "UnitName");
                generated.fractionText = FindTmpText(button, "UnitFraction");
                break;

            case DisplayItemType.NormalAttack:
                generated.nameText = FindTmpText(button, "CharacterName");
                generated.attackText = FindTmpText(button, "CharacterAtk");
                break;
        }

        if (generated.nameText == null)
        {
            Debug.LogError(
                template.name + " に必要な名前用 TMP_Text がありません。",
                template);
            Destroy(button.gameObject);
            generatedItems[index] = null;
            return null;
        }

        Transform panel = FindChild(button.transform, "Panel");
        if (panel != null) panel.gameObject.SetActive(false);

        generatedItems[index] = generated;
        return generated;
    }

    private bool ResolveTemplates()
    {
        if (boardUnitList == null) boardUnitList = transform;

        if (boardUnitButtonTemplate == null)
            boardUnitButtonTemplate = FindButtonTemplate("BoardUnitButton");
        if (boardUnitButtonStillTemplate == null)
            boardUnitButtonStillTemplate = FindButtonTemplate("BoardUnitButtonStill");
        if (boardUnitButtonCharacterTemplate == null)
            boardUnitButtonCharacterTemplate =
                FindButtonTemplate("BoardUnitButtonCharacter");

        if (boardUnitList == null ||
            boardUnitButtonTemplate == null ||
            boardUnitButtonStillTemplate == null ||
            boardUnitButtonCharacterTemplate == null)
        {
            Debug.LogWarning(
                "BoardUnitButton / BoardUnitButtonStill / BoardUnitButtonCharacter の3つを設定してください。",
                this);
            return false;
        }

        SetButtonVisible(boardUnitButtonTemplate, false);
        SetButtonVisible(boardUnitButtonStillTemplate, false);
        SetButtonVisible(boardUnitButtonCharacterTemplate, false);
        return true;
    }

    private Button FindButtonTemplate(string objectName)
    {
        Transform found = FindChild(boardUnitList, objectName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private TMP_Text FindTmpText(Button button, string childName)
    {
        Transform child = FindChild(button.transform, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
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

    private void SetText(TMP_Text textComponent, string value)
    {
        if (textComponent != null)
            textComponent.text = value;
    }

    private void SetButtonVisible(Button button, bool visible, bool dimmed = false)
    {
        if (button == null) return;

        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        Image overlay = GetInactiveOverlay(button);
        if (overlay != null)
            overlay.gameObject.SetActive(visible && dimmed);

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = !visible;
    }

    private Image GetInactiveOverlay(Button button)
    {
        if (inactiveOverlays.TryGetValue(button, out Image cached) && cached != null)
            return cached;

        Transform existing = FindChild(button.transform, "UnitInactiveOverlay");
        Image overlay;

        if (existing != null)
        {
            overlay = existing.GetComponent<Image>();
        }
        else
        {
            GameObject overlayObject = new GameObject(
                "UnitInactiveOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.layer = button.gameObject.layer;
            overlayObject.transform.SetParent(button.transform, false);
            overlayObject.transform.SetAsFirstSibling();

            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            overlay = overlayObject.GetComponent<Image>();
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                overlay.sprite = buttonImage.sprite;
                overlay.type = buttonImage.type == Image.Type.Sliced
                    ? Image.Type.Sliced : Image.Type.Simple;
            }

            overlay.color = Color.gray;
            overlay.raycastTarget = false;
        }

        if (overlay == null)
        {
            Debug.LogError("UnitInactiveOverlay に Image がありません。", button);
            return null;
        }

        overlay.transform.SetAsFirstSibling();
        overlay.raycastTarget = false;
        inactiveOverlays[button] = overlay;
        return overlay;
    }

    private bool HasStateChanged()
    {
        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        CharacterData[] roster =
            shopManager != null && shopManager.characters != null
                ? shopManager.characters : new CharacterData[0];

        if (roster.Length != previousRoster.Length) return true;
        for (int i = 0; i < roster.Length; i++)
            if (roster[i] != previousRoster[i]) return true;

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        BoardSlot[] slots =
            boardManager != null && boardManager.boardArea != null
                ? boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true)
                : new BoardSlot[0];

        if (slots.Length != previousSlots.Length) return true;

        BattleManager battleManager = FindAnyObjectByType<BattleManager>();
        CharacterStatSettings settings =
            battleManager != null ? battleManager.characterStatSettings : null;

        for (int i = 0; i < slots.Length; i++)
        {
            OwnedCharacter character =
                slots[i] != null ? slots[i].placedCharacter : null;

            if (slots[i] != previousSlots[i] ||
                character != previousCharacters[i])
                return true;

            int star = character != null ? character.star : 0;
            int attack = character != null && character.data != null
                ? (settings != null
                    ? character.GetAttack(settings) + character.battleAttackBonus
                    : character.attack + character.battleAttackBonus)
                : 0;
            int bonus = character != null ? character.battleAttackBonus : 0;

            if (star != previousStars[i] ||
                attack != previousAttacks[i] ||
                bonus != previousAttackBonuses[i])
                return true;
        }

        return false;
    }

    private void CacheBoardState()
    {
        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        previousRoster =
            shopManager != null && shopManager.characters != null
                ? (CharacterData[])shopManager.characters.Clone()
                : new CharacterData[0];

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        previousSlots =
            boardManager != null && boardManager.boardArea != null
                ? boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true)
                : new BoardSlot[0];

        BattleManager battleManager = FindAnyObjectByType<BattleManager>();
        CharacterStatSettings settings =
            battleManager != null ? battleManager.characterStatSettings : null;

        previousCharacters = new OwnedCharacter[previousSlots.Length];
        previousStars = new int[previousSlots.Length];
        previousAttacks = new int[previousSlots.Length];
        previousAttackBonuses = new int[previousSlots.Length];

        for (int i = 0; i < previousSlots.Length; i++)
        {
            OwnedCharacter character =
                previousSlots[i] != null ? previousSlots[i].placedCharacter : null;

            previousCharacters[i] = character;
            previousStars[i] = character != null ? character.star : 0;
            previousAttacks[i] = character != null && character.data != null
                ? (settings != null
                    ? character.GetAttack(settings) + character.battleAttackBonus
                    : character.attack + character.battleAttackBonus)
                : 0;
            previousAttackBonuses[i] =
                character != null ? character.battleAttackBonus : 0;
        }
    }
}
