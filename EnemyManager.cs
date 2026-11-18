using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [Header("Board Settings")]
    public GameObject boardSlotPrefab;
    public Transform boardArea;
    public int columns = 2;
    public int rows = 3;

    [Header("Encounter Data")]
    public EncounterPoolData[] encounterPoolData;
    public EnemyData[] enemyDataBySlot = new EnemyData[6];
    public EnemyFormationData currentFormation;
    public Sprite enemyFrameSprite;
    [Tooltip("Used for enemies with an AE effect or a target other than T002.")]
    public Sprite specialEnemyFrameSprite;
    public EnemyDetailPanel enemyDetailPanel;
    public StageManager stageManager;

    private bool hasManualStageChoice;
    private int[] battleAttackBonusBySlot;
    private int[] battleDefenseBonusBySlot;
    private int[] battleHealthBonusBySlot;
    private int[] battleGapBonusBySlot;
    private int[] permanentAttackBonusBySlot;
    private int[] permanentDefenseBonusBySlot;
    private int[] permanentHealthBonusBySlot;
    private int[] permanentGapBonusBySlot;

    private void OnEnable()
    {
        if (stageManager == null)
            stageManager = FindAnyObjectByType<StageManager>();

        if (stageManager == null)
        {
            Debug.LogError("EnemyManager requires a StageManager.", this);
            return;
        }

        stageManager.StageStarting += HandleStageStarting;
        stageManager.StageChoiceSelected += HandleStageChoiceSelected;
        stageManager.SelectionCompleted += HandleSelectionCompleted;
    }

    private void OnDisable()
    {
        if (stageManager == null)
            return;

        stageManager.StageStarting -= HandleStageStarting;
        stageManager.StageChoiceSelected -= HandleStageChoiceSelected;
        stageManager.SelectionCompleted -= HandleSelectionCompleted;
    }

    private void Start()
    {
        CreateBoard();
    }

    private void CreateBoard()
    {
        if (boardArea == null)
        {
            Debug.LogError("EnemyManager requires a Board Area.", this);
            return;
        }

        if (boardSlotPrefab == null)
        {
            Debug.LogError("EnemyManager requires a Board Slot Prefab.", this);
            return;
        }

        if (columns <= 0 || rows <= 0)
        {
            Debug.LogError("EnemyManager columns and rows must be greater than zero.", this);
            return;
        }

        foreach (Transform child in boardArea)
            Destroy(child.gameObject);

        int slotCount = columns * rows;
        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObject = Instantiate(boardSlotPrefab, boardArea);
            BoardSlot slot = slotObject.GetComponent<BoardSlot>();
            if (slot == null)
            {
                Debug.LogError("Board Slot Prefab must have a BoardSlot component.", slotObject);
                continue;
            }

            slot.SetAcceptsPlayerCharacters(false);
        }

        if (enemyDataBySlot == null || enemyDataBySlot.Length != slotCount)
            enemyDataBySlot = new EnemyData[slotCount];

        RefreshEnemySlots();
    }

    private void HandleStageStarting()
    {
        hasManualStageChoice = false;
        currentFormation = null;
        enemyDataBySlot = new EnemyData[Mathf.Max(0, columns * rows)];
        ResetItemBonuses();
        RefreshEnemySlots();
    }

    private void HandleStageChoiceSelected(StageManager.StageSelectionButton selectedButton)
    {
        hasManualStageChoice = true;

        string category;
        if (!TryGetCategory(selectedButton, out category))
        {
            currentFormation = null;
            enemyDataBySlot = new EnemyData[Mathf.Max(0, columns * rows)];
            Debug.Log(
                selectedButton + " was selected. This choice has no enemies; battle will be an automatic win.",
                this);
            RefreshEnemySlots();
            return;
        }

        ApplyEncounterForCurrentRound(category);
    }

    private void HandleSelectionCompleted(StageManager.StageSelectionButton selectedButton)
    {
        if (hasManualStageChoice)
            return;

        string category = stageManager.CurrentRoundNumber == 4
            ? "Boss"
            : "Normal";
        ApplyEncounterForCurrentRound(category);
    }

    private bool TryGetCategory(StageManager.StageSelectionButton selectedButton, out string category)
    {
        switch (selectedButton)
        {
            case StageManager.StageSelectionButton.SelectA:
                category = "Normal";
                return true;
            case StageManager.StageSelectionButton.SelectC:
                category = "Hard";
                return true;
            case StageManager.StageSelectionButton.SelectB:
            case StageManager.StageSelectionButton.SelectD:
                category = null;
                return false;
            default:
                category = null;
                return false;
        }
    }

    private void ApplyEncounterForCurrentRound(string category)
    {
        if (stageManager == null)
        {
            Debug.LogError("EnemyManager requires a StageManager to select an encounter.", this);
            return;
        }

        List<EnemyFormationData> candidates = new List<EnemyFormationData>();
        if (encounterPoolData != null)
        {
            int roundIndex = (stageManager.CurrentStageNumber - 1) * 5 +
                             stageManager.CurrentRoundNumber - 1;
            foreach (EncounterPoolData pool in encounterPoolData)
            {
                if (pool == null ||
                    !string.Equals(pool.category, category, System.StringComparison.OrdinalIgnoreCase) ||
                    !pool.ContainsRound(roundIndex) ||
                    pool.formations == null)
                    continue;

                foreach (EnemyFormationData formation in pool.formations)
                {
                    if (formation != null)
                        candidates.Add(formation);
                }
            }
        }

        if (candidates.Count == 0)
        {
            currentFormation = null;
            enemyDataBySlot = new EnemyData[Mathf.Max(0, columns * rows)];
            Debug.LogWarning(
                "No EnemyFormationData is configured for round " +
                stageManager.CurrentStageNumber + "-" + stageManager.CurrentRoundNumber +
                " and category " + category + ".",
                this);
            RefreshEnemySlots();
            return;
        }

        currentFormation = candidates[Random.Range(0, candidates.Count)];
        int slotCount = Mathf.Max(0, columns * rows);
        enemyDataBySlot = new EnemyData[slotCount];
        ResetItemBonuses();
        if (currentFormation.slots != null)
        {
            int count = Mathf.Min(slotCount, currentFormation.slots.Length);
            for (int i = 0; i < count; i++)
            {
                EnemyFormationData.FormationSlot formationSlot = currentFormation.slots[i];
                if (formationSlot != null)
                    enemyDataBySlot[i] = formationSlot.enemy;
            }
        }

        Debug.Log(
            "Selected formation " + currentFormation.formationId +
            " for round " + stageManager.CurrentStageNumber + "-" +
            stageManager.CurrentRoundNumber + " (" + category + ").",
            this);
        RefreshEnemySlots();
    }

    public bool TryApplyItemEffect(BoardSlot slot, string effect, int value, bool immediateEffect = false)
    {
        if (slot == null || slot.enemyData == null || boardArea == null) return false;
        BoardSlot[] slots = boardArea.GetComponentsInChildren<BoardSlot>(true);
        int index = System.Array.IndexOf(slots, slot);
        if (index < 0 || index >= enemyDataBySlot.Length) return false;
        EnsureItemBonusArrays();
        switch (effect)
        {
            case "Attack":
                AddItemBonus(battleAttackBonusBySlot, permanentAttackBonusBySlot, index, value, immediateEffect);
                break;
            case "Def":
                AddItemBonus(battleDefenseBonusBySlot, permanentDefenseBonusBySlot, index, value, immediateEffect);
                break;
            case "Damage":
                AddItemBonus(battleHealthBonusBySlot, permanentHealthBonusBySlot, index, -value, immediateEffect);
                break;
            case "Gap":
                AddItemBonus(battleGapBonusBySlot, permanentGapBonusBySlot, index, value, immediateEffect);
                break;
            default: return false;
        }
        RefreshEnemySlots();
        return true;
    }

    public bool ApplyItemEffectToAllEnemies(string effect, int value, bool immediateEffect = false)
    {
        if (enemyDataBySlot == null) return false;
        bool applied = false;
        BoardSlot[] slots = boardArea != null ? boardArea.GetComponentsInChildren<BoardSlot>(true) : new BoardSlot[0];
        foreach (BoardSlot slot in slots)
            if (slot != null && slot.enemyData != null) applied |= TryApplyItemEffect(slot, effect, value, immediateEffect);
        return applied;
    }

    public int GetBattleAttackBonus(int index) => ReadBonus(battleAttackBonusBySlot, index) + ReadBonus(permanentAttackBonusBySlot, index);
    public int GetBattleDefenseBonus(int index) => ReadBonus(battleDefenseBonusBySlot, index) + ReadBonus(permanentDefenseBonusBySlot, index);
    public int GetBattleHealthBonus(int index) => ReadBonus(battleHealthBonusBySlot, index) + ReadBonus(permanentHealthBonusBySlot, index);
    public int GetBattleGapBonus(int index) => ReadBonus(battleGapBonusBySlot, index) + ReadBonus(permanentGapBonusBySlot, index);

    public void ResetItemBonuses(bool refreshSlots = true)
    {
        int count = enemyDataBySlot != null ? enemyDataBySlot.Length : Mathf.Max(0, columns * rows);
        battleAttackBonusBySlot = new int[count];
        battleDefenseBonusBySlot = new int[count];
        battleHealthBonusBySlot = new int[count];
        battleGapBonusBySlot = new int[count];
        permanentAttackBonusBySlot = new int[count];
        permanentDefenseBonusBySlot = new int[count];
        permanentHealthBonusBySlot = new int[count];
        permanentGapBonusBySlot = new int[count];
        if (refreshSlots)
            RefreshEnemySlots();
    }

    public void ResetBattleItemBonuses(bool refreshSlots = true)
    {
        int count = enemyDataBySlot != null ? enemyDataBySlot.Length : Mathf.Max(0, columns * rows);
        battleAttackBonusBySlot = new int[count];
        battleDefenseBonusBySlot = new int[count];
        battleHealthBonusBySlot = new int[count];
        battleGapBonusBySlot = new int[count];
        if (refreshSlots)
            RefreshEnemySlots();
    }

    private void EnsureItemBonusArrays()
    {
        int count = enemyDataBySlot != null ? enemyDataBySlot.Length : 0;
        if (battleAttackBonusBySlot == null || battleAttackBonusBySlot.Length != count) battleAttackBonusBySlot = new int[count];
        if (battleDefenseBonusBySlot == null || battleDefenseBonusBySlot.Length != count) battleDefenseBonusBySlot = new int[count];
        if (battleHealthBonusBySlot == null || battleHealthBonusBySlot.Length != count) battleHealthBonusBySlot = new int[count];
        if (battleGapBonusBySlot == null || battleGapBonusBySlot.Length != count) battleGapBonusBySlot = new int[count];
        if (permanentAttackBonusBySlot == null || permanentAttackBonusBySlot.Length != count) permanentAttackBonusBySlot = new int[count];
        if (permanentDefenseBonusBySlot == null || permanentDefenseBonusBySlot.Length != count) permanentDefenseBonusBySlot = new int[count];
        if (permanentHealthBonusBySlot == null || permanentHealthBonusBySlot.Length != count) permanentHealthBonusBySlot = new int[count];
        if (permanentGapBonusBySlot == null || permanentGapBonusBySlot.Length != count) permanentGapBonusBySlot = new int[count];
    }

    private int ReadBonus(int[] values, int index) => values != null && index >= 0 && index < values.Length ? values[index] : 0;

    private void AddItemBonus(int[] battleBonuses, int[] permanentBonuses, int index, int value, bool immediateEffect)
    {
        if (immediateEffect)
            permanentBonuses[index] += value;
        else
            battleBonuses[index] += value;
    }

    public void RefreshEnemyBattleState(
    int slotIndex,
    int currentHealth,
    int remainingGap,
    int currentDefense,
    int currentAttack = -1)
    {
        if (boardArea == null)
            return;

        BoardSlot[] slots = boardArea.GetComponentsInChildren<BoardSlot>(true);
        if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
            return;

        BoardCharacter[] displays =
            slots[slotIndex].GetComponentsInChildren<BoardCharacter>(true);
        foreach (BoardCharacter display in displays)
        {
            if (display == null || !display.IsDisplayingEnemy(slots[slotIndex].enemyData))
                continue;

            display.RefreshEnemyBattleState(
                currentHealth, remainingGap, currentDefense, currentAttack);
            break;
        }
    }

    public BoardCharacter GetEnemyBoardCharacter(int slotIndex)
    {
        if (boardArea == null)
            return null;

        BoardSlot[] slots = boardArea.GetComponentsInChildren<BoardSlot>(true);
        if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
            return null;

        return slots[slotIndex].GetComponentInChildren<BoardCharacter>(true);
    }

    public BoardCharacter PlayEnemyDamageShake(int slotIndex)
    {
        BoardCharacter display = GetEnemyBoardCharacter(slotIndex);
        if (display != null)
            display.PlayDamageShake();
        return display;
    }

    private void RefreshEnemySlots()
    {
        if (boardArea == null)
            return;

        EnsureItemBonusArrays();
        BoardSlot[] slots = boardArea.GetComponentsInChildren<BoardSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            BoardSlot slot = slots[i];
            if (slot == null)
                continue;

            slot.SetAcceptsPlayerCharacters(false);
            int gap = 0;
            if (currentFormation != null &&
                currentFormation.slots != null &&
                i < currentFormation.slots.Length &&
                currentFormation.slots[i] != null)
            {
                gap = Mathf.Max(0, currentFormation.slots[i].gap + GetBattleGapBonus(i));
            }

            EnemyData enemy = enemyDataBySlot != null && i < enemyDataBySlot.Length
                ? enemyDataBySlot[i]
                : null;
            slot.SetEnemyData(
                enemy,
                GetEnemyFrameSprite(enemy),
                enemyDetailPanel,
                gap,
                ReadBonus(battleAttackBonusBySlot, i),
                ReadBonus(battleDefenseBonusBySlot, i),
                ReadBonus(battleHealthBonusBySlot, i));
        }
    }

    private Sprite GetEnemyFrameSprite(EnemyData enemy)
    {
        if (enemy != null &&
            (!string.IsNullOrWhiteSpace(enemy.ae) ||
             !string.Equals((enemy.target ?? string.Empty).Trim(), "T002", System.StringComparison.Ordinal)))
        {
            return specialEnemyFrameSprite != null
                ? specialEnemyFrameSprite
                : enemyFrameSprite;
        }

        return enemyFrameSprite;
    }
}
