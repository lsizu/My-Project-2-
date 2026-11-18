using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BattleManager : MonoBehaviour
{
    private enum BattlePlaybackMode { Manual, FastForward, Auto }

    public Button battleButton;
    public Button battleStepButton;
    public Button autoBattleButton;
    public Button fastBattleButton;
    private Coroutine battleCoroutine;
    private bool battleStepRequested;
    private BattlePlaybackMode playbackMode = BattlePlaybackMode.Manual;
    public PlayerData player;
    public ShopManager shopManager;
    public CharacterStatSettings characterStatSettings;
    public GameObject damagePopupPrefab;
    public TMP_Text logText;
    public TMP_Text blockCountText;
    public GameObject blockCountPanel;

    public bool LastBattleWasWin { get; private set; }
    public int Block { get; private set; }
    private int pendingPlayerHpDelta;
    private int pendingPlayerBlockBonus;
    private int permanentPlayerBlockBonus;
    private float blockPreviewRefreshTimer;
    private const float BlockPreviewRefreshInterval = 0.2f;

    private class TargetedAttack
    {
        public TargetData data;
        public List<BattleUnit> targets = new List<BattleUnit>();
    }

    private enum TargetSelectionMode { All, Uniform, Weighted }

    private sealed class TargetWeightRule
    {
        public string position;
        public int weight;
    }

    private class BattleUnit
    {
        public string name;
        public int attack;
        public int defense;
        public int temporaryDefenseReduction;
        public int maxHealth;
        public int health;
        public int gap;
        public int cooldown;
        public int nextActionRound;
        public int stunRounds;
        public int star;
        public BoardSlot boardSlot;
        public EnemyData enemyData;
        public int enemySlotIndex;
        public int nextTargetIndex;
        public int boardSlotIndex;
        public readonly List<string> unitNames = new List<string>();
    }

    private class UnitMove
    {
        public string name;
        public HashSet<CharacterData> rosterMembers = new HashSet<CharacterData>();
        public List<BattleUnit> participants = new List<BattleUnit>();
        public List<BoardSlot> boardSlots = new List<BoardSlot>();
    }

    private class UnitMoveRoundState
    {
        public HashSet<UnitMove> usedMoves = new HashSet<UnitMove>();
        public Dictionary<BattleUnit, int> participationCounts =
            new Dictionary<BattleUnit, int>();
    }

    private void OnEnable()
    {
        if (battleButton != null)
            battleButton.onClick.AddListener(ResolveBattle);
        if (battleStepButton != null)
            battleStepButton.onClick.AddListener(StepBattle);
        if (autoBattleButton != null)
            autoBattleButton.onClick.AddListener(StartAutoBattle);
        if (fastBattleButton != null)
            fastBattleButton.onClick.AddListener(StartFastBattle);
    }

    private void OnDisable()
    {
        if (battleButton != null)
            battleButton.onClick.RemoveListener(ResolveBattle);
        if (battleStepButton != null)
            battleStepButton.onClick.RemoveListener(StepBattle);
        if (autoBattleButton != null)
            autoBattleButton.onClick.RemoveListener(StartAutoBattle);
        if (fastBattleButton != null)
            fastBattleButton.onClick.RemoveListener(StartFastBattle);
        if (battleCoroutine != null)
            StopCoroutine(battleCoroutine);
        battleCoroutine = null;
        battleStepRequested = false;
        playbackMode = BattlePlaybackMode.Manual;
    }

    private void Awake()
    {
        EnsureDamagePopupPrefab();
    }

    private void EnsureDamagePopupPrefab()
    {
        if (damagePopupPrefab != null)
            return;
#if UNITY_EDITOR
        damagePopupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DamagePopup.prefab");
        if (damagePopupPrefab != null)
            Debug.LogWarning("BattleManager loaded DamagePopupPrefab automatically because its Inspector reference was empty.", this);
#endif
    }
    private void Start()
    {
        EnsureDamagePopupPrefab();
        ResolveBlockCountUI();
        RefreshPreBattleBlockCount();
        if (player == null)
            player = FindAnyObjectByType<PlayerData>();
        if (shopManager == null)
            shopManager = FindAnyObjectByType<ShopManager>();
        if (characterStatSettings == null)
            Debug.LogError("BattleManager requires Character Stat Settings.", this);
        if (battleButton == null)
            Debug.LogError("BattleManager requires a Battle Button.", this);
        if (battleStepButton == null)
        {
            GameObject stepObject = GameObject.Find("BattleStepButton");
            if (stepObject == null) stepObject = GameObject.Find("ButtleStepButton");
            if (stepObject == null) stepObject = GameObject.Find("ButtleStep");
            if (stepObject != null) battleStepButton = stepObject.GetComponent<Button>();
        }
        if (autoBattleButton == null)
        {
            GameObject autoObject = GameObject.Find("AutoBattleButton");
            if (autoObject != null) autoBattleButton = autoObject.GetComponent<Button>();
        }
        if (fastBattleButton == null)
        {
            GameObject fastObject = GameObject.Find("FastBattleButton");
            if (fastObject != null) fastBattleButton = fastObject.GetComponent<Button>();
        }
        if (battleStepButton == null && autoBattleButton == null && fastBattleButton == null)
            Debug.LogError("BattleManager requires a Battle Step, Auto Battle, or Fast Battle Button reference.", this);
        else
        {
            if (battleStepButton != null)
            {
                battleStepButton.onClick.RemoveListener(StepBattle);
                battleStepButton.onClick.AddListener(StepBattle);
                battleStepButton.interactable = false;
            }
        }
        if (autoBattleButton != null)
        {
            autoBattleButton.onClick.RemoveListener(StartAutoBattle);
            autoBattleButton.onClick.AddListener(StartAutoBattle);
            autoBattleButton.interactable = true;
        }
        if (fastBattleButton != null)
        {
            fastBattleButton.onClick.RemoveListener(StartFastBattle);
            fastBattleButton.onClick.AddListener(StartFastBattle);
            fastBattleButton.interactable = true;
        }
    }

    private void Update()
    {
        if (battleCoroutine != null)
            return;

        blockPreviewRefreshTimer += Time.unscaledDeltaTime;
        if (blockPreviewRefreshTimer < BlockPreviewRefreshInterval)
            return;

        blockPreviewRefreshTimer = 0f;
        RefreshPreBattleBlockCount();
    }

    private void ResolveBlockCountUI()
    {
        if (blockCountText == null)
        {
            foreach (TMP_Text candidate in FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.name == "BlockCount")
                {
                    blockCountText = candidate;
                    break;
                }
            }
        }

        if (blockCountPanel == null)
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name == "BlockCountPanel")
                {
                    blockCountPanel = candidate.gameObject;
                    break;
                }
            }
        }

        if (blockCountPanel == null && blockCountText != null)
        {
            Transform parent = blockCountText.transform;
            while (parent != null && parent.name != "BlockCountPanel")
                parent = parent.parent;
            if (parent != null)
                blockCountPanel = parent.gameObject;
        }

        if (blockCountText == null)
            Debug.LogWarning("BlockCount TMP_Text was not found. Assign it to BattleManager or name it 'BlockCount'.", this);
        if (blockCountPanel == null)
            Debug.LogWarning("BlockCountPanel was not found. Assign it to BattleManager or name it 'BlockCountPanel'.", this);
    }

    private void RefreshPreBattleBlockCount()
    {
        if (battleCoroutine != null)
            return;

        List<CharacterData> boardCharacters = new List<CharacterData>();
        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        if (boardManager != null && boardManager.boardArea != null)
        {
            BoardSlot[] slots = boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true);
            foreach (BoardSlot slot in slots)
            {
                if (slot != null && slot.placedCharacter != null &&
                    slot.placedCharacter.data != null)
                    boardCharacters.Add(slot.placedCharacter.data);
            }
        }

        Block = Mathf.Max(
            0,
            CountActiveAttributeSynergies(boardCharacters) + pendingPlayerBlockBonus + permanentPlayerBlockBonus);
        UpdateBlockCountUI();
    }

    private void UpdateBlockCountUI()
    {
        if (blockCountText != null)
            blockCountText.text = Block.ToString();
        if (blockCountPanel != null)
            blockCountPanel.SetActive(Block > 0);
    }

    public void ResolveBattle()
    {
        if (battleCoroutine != null)
            return;
        if (battleStepButton == null && playbackMode == BattlePlaybackMode.Manual)
        {
            Debug.LogError("Assign the Battle Step Button in the BattleManager Inspector.", this);
            return;
        }

        if (battleStepButton != null)
            battleStepButton.interactable = playbackMode == BattlePlaybackMode.Manual;
        battleCoroutine = StartCoroutine(ResolveBattleSequence());
    }

    public void StartAutoBattle()
    {
        StartAutomaticBattle(BattlePlaybackMode.Auto);
    }

    public void StartFastBattle()
    {
        StartAutomaticBattle(BattlePlaybackMode.FastForward);
    }

    private void StartAutomaticBattle(BattlePlaybackMode mode)
    {
        playbackMode = mode;
        battleStepRequested = true;
        if (autoBattleButton != null)
            autoBattleButton.interactable = false;
        if (fastBattleButton != null)
            fastBattleButton.interactable = false;
        if (battleStepButton != null)
            battleStepButton.interactable = false;

        if (battleCoroutine == null)
            ResolveBattle();
    }

    public void StepBattle()
    {
        if (battleCoroutine != null)
            battleStepRequested = true;
    }

    private IEnumerator ResolveBattleSequence()
    {
        yield return RunBattleSteps();
        battleCoroutine = null;
        battleStepRequested = false;
        playbackMode = BattlePlaybackMode.Manual;
        if (battleStepButton != null)
            battleStepButton.interactable = false;
        if (autoBattleButton != null)
            autoBattleButton.interactable = true;
        if (fastBattleButton != null)
            fastBattleButton.interactable = true;
    }

    private IEnumerator WaitForBattleStep()
    {
        battleStepRequested = false;
        while (!battleStepRequested && playbackMode == BattlePlaybackMode.Manual)
            yield return null;
        battleStepRequested = false;
    }

    private IEnumerator WaitBattleFrames(int frames)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0, frames) / 60f);
    }

    private IEnumerator BeginAttackPresentation(
        BoardCharacter attackerDisplay,
        List<BoardCharacter> lightingDisplays = null,
        string attackName = null)
    {
        SetAttackLighting(attackerDisplay, lightingDisplays, true);
        SetBattleLog((string.IsNullOrEmpty(attackName) && attackerDisplay != null
            ? attackerDisplay.name
            : attackName) + "の攻撃！");

        int leadFrames = playbackMode == BattlePlaybackMode.Auto ? 45 : 10;
        yield return WaitBattleFrames(leadFrames);

        if (attackerDisplay != null)
            attackerDisplay.PlayAttackAnimation();

        yield return WaitBattleFrames(25);
    }

    private IEnumerator FinishAttackPresentation(
        BoardCharacter attackerDisplay,
        List<BoardCharacter> lightingDisplays = null)
    {
        int recoveryFrames = playbackMode == BattlePlaybackMode.Auto ? 50 : 20;
        yield return WaitBattleFrames(recoveryFrames);
        SetAttackLighting(attackerDisplay, lightingDisplays, false);
    }

    private static void SetAttackLighting(
        BoardCharacter attackerDisplay,
        List<BoardCharacter> lightingDisplays,
        bool active)
    {
        if (lightingDisplays != null && lightingDisplays.Count > 0)
        {
            foreach (BoardCharacter display in lightingDisplays)
                if (display != null)
                    display.SetAttackLighting(active);
            return;
        }

        if (attackerDisplay != null)
            attackerDisplay.SetAttackLighting(active);
    }

    private void SetBattleLog(string message)
    {
        if (logText == null)
        {
            GameObject logObject = GameObject.Find("LogText");
            if (logObject != null)
                logText = logObject.GetComponent<TMP_Text>() ??
                    logObject.GetComponentInChildren<TMP_Text>(true);
        }

        if (logText != null)
            logText.text = message;
        else
            Debug.LogWarning("LogText was not found. Assign it to BattleManager or name the TMP object 'LogText'.", this);
    }
    private IEnumerator RunBattleSteps()
    {
        if (player == null)
        {
            Debug.LogError("PlayerData is required to resolve the battle.", this);
            yield break;
        }

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        if (boardManager == null || boardManager.boardArea == null)
        {
            Debug.LogError("BoardManager and its Board Area are required to resolve the battle.", this);
            yield break;
        }

        EnemyManager enemyManager = FindAnyObjectByType<EnemyManager>();
        if (enemyManager == null || enemyManager.enemyDataBySlot == null)
        {
            Debug.LogError("EnemyManager and its EnemyDataBySlot are required to resolve the battle.", this);
            yield break;
        }

        List<BattleUnit> playerUnits = CreatePlayerBattleUnits(boardManager);
        List<UnitMove> activeUnitMoves = CreateActiveUnitMoves(playerUnits, boardManager);
        List<BattleUnit> enemies = CreateEnemyBattleUnits(enemyManager);
        Dictionary<string, TargetData> targetDataById = LoadTargetData();
        Dictionary<string, EffectData> effectDataById = LoadEffectData();
        if (enemies.Count > 0 && !ValidateEnemyTargets(enemies, targetDataById))
            yield break;
        if (enemies.Count > 0 && !ValidateEnemyEffects(enemies, effectDataById))
            yield break;
        yield return WaitForBattleStep();
        Block = Mathf.Max(0, CountActiveAttributeSynergies(playerUnits) + pendingPlayerBlockBonus + permanentPlayerBlockBonus);
        UpdateBlockCountUI();
        Debug.Log(
            "Battle started. Player HP: " + player.hp +
            ", Block gained from attribute synergies: " + Block + ".",
            this);

        if (player.hp <= 0)
        {
            yield return WaitForBattleStep();
            yield return FinishBattle(false);
            yield break;
        }

        if (enemies.Count == 0)
        {
            yield return WaitForBattleStep();
            yield return FinishBattle(true);
            yield break;
        }

        foreach (BattleUnit enemy in enemies)
            ApplyEnemyEffects(
                enemy.enemyData.cip, enemy, null, null, effectDataById, enemyManager);

        int round = 1;
        while (true)
        {
            yield return WaitForBattleStep();
            Debug.Log("Battle round " + round + " - Player turn.", this);
            bool healthChanged = false;
            UnitMoveRoundState unitMoveRoundState = new UnitMoveRoundState();

            foreach (BattleUnit attacker in playerUnits)
            {
                if (enemies.Count == 0)
                    break;

                if (attacker.stunRounds > 0)
                {
                    attacker.stunRounds--;
                    yield return WaitForBattleStep();
                    Debug.Log(attacker.name + " skips their action while stunned.", this);
                    continue;
                }

                int participationLimit = Mathf.Clamp(attacker.star, 1, 3);
                int participations = GetParticipationCount(unitMoveRoundState, attacker);
                if (participations >= participationLimit)
                {
                    yield return WaitForBattleStep();
                    Debug.Log(attacker.name + " skips their action after participating in a UnitMove.", this);
                    continue;
                }

                while (GetParticipationCount(unitMoveRoundState, attacker) < participationLimit &&
                       enemies.Count > 0)
                {
                    List<UnitMove> availableMoves = FindAvailableUnitMoves(
                        attacker,
                        activeUnitMoves,
                        unitMoveRoundState);
                    if (availableMoves.Count == 0)
                        break;

                    yield return WaitForBattleStep();
                    UnitMove selectedMove = availableMoves[0];
                    unitMoveRoundState.usedMoves.Add(selectedMove);
                    int combinedAttack = 0;
                    List<string> participantNames = new List<string>();
                    List<BoardCharacter> unitMoveParticipantDisplays = new List<BoardCharacter>();
                    foreach (BattleUnit participant in selectedMove.participants)
                    {
                        combinedAttack += participant.attack;
                        participantNames.Add(participant.name);
                        IncrementParticipationCount(unitMoveRoundState, participant);

                        BoardCharacter participantDisplay = participant.boardSlot != null
                            ? participant.boardSlot.GetComponentInChildren<BoardCharacter>(true)
                            : null;
                        if (participantDisplay != null && !unitMoveParticipantDisplays.Contains(participantDisplay))
                            unitMoveParticipantDisplays.Add(participantDisplay);
                    }

                    BattleUnit target = enemies[Random.Range(0, enemies.Count)];
                    int damage = CalculateBattleDamage(combinedAttack, target.defense);
                    BoardCharacter unitMoveAttackerDisplay = attacker.boardSlot != null
                        ? attacker.boardSlot.GetComponentInChildren<BoardCharacter>(true)
                        : null;
                    yield return BeginAttackPresentation(
                        unitMoveAttackerDisplay, unitMoveParticipantDisplays, selectedMove.name);

                    int previousHealth = target.health;
                    target.health = Mathf.Max(0, target.health - damage);
                    int actualDamage = previousHealth - target.health;
                    float defeatLogShownAt = -1f;
                    healthChanged |= actualDamage > 0;
                    if (damage == 0)
                        ReduceEnemyDefenseUntilEnemyTurn(
                            target,
                            selectedMove.participants.Count);
                    enemyManager.RefreshEnemyBattleState(
                        target.enemySlotIndex,
                        target.health,
                        target.gap,
                        target.defense);
                    BoardCharacter unitMoveEnemyDisplay = enemyManager.PlayEnemyDamageShake(target.enemySlotIndex);
                    DamagePopupAnimation damagePopup = null;
                    if (unitMoveEnemyDisplay != null)
                        damagePopup = unitMoveEnemyDisplay.ShowDamagePopup(damagePopupPrefab, damage);
                    SetBattleLog(damage + "のダメージを与えた！");
                    if (target.health == 0 && unitMoveEnemyDisplay != null)
                        unitMoveEnemyDisplay.PlayDefeatFadeAfterPopup(
                            damagePopup,
                            () =>
                            {
                                SetBattleLog(target.name + "を倒した！");
                                defeatLogShownAt = Time.unscaledTime;
                            });
                    yield return FinishAttackPresentation(unitMoveAttackerDisplay, unitMoveParticipantDisplays);
                    if (target.health == 0 && unitMoveEnemyDisplay != null)
                        yield return WaitForAutoDefeatLog(() => defeatLogShownAt);
                    Debug.Log(
                        "UnitMove [" + selectedMove.name + "] by " +
                        string.Join(", ", participantNames.ToArray()) +
                        " (combined ATK " + combinedAttack + ") attacks " + target.name +
                        " (DEF " + target.defense + "): damage " + actualDamage +
                        ", enemy HP " + target.health + "/" + target.maxHealth + ".",
                        this);

                    if (target.health == 0)
                    {
                        yield return WaitForBattleStep();
                        Debug.Log(target.name + " was defeated.", this);
                        ApplyEnemyEffects(
                            target.enemyData.pig,
                            target,
                            attacker,
                            new List<BattleUnit> { target },
                            effectDataById,
                            enemyManager);
                        enemies.Remove(target);
                    }
                }

                if (GetParticipationCount(unitMoveRoundState, attacker) > 0 ||
                    enemies.Count == 0)
                    continue;

                yield return WaitForBattleStep();
                BattleUnit normalAttackTarget = enemies[Random.Range(0, enemies.Count)];
                int normalDamage = CalculateBattleDamage(attacker.attack, normalAttackTarget.defense);
                BoardCharacter normalAttackerDisplay = attacker.boardSlot != null
                    ? attacker.boardSlot.GetComponentInChildren<BoardCharacter>(true)
                    : null;
                yield return BeginAttackPresentation(normalAttackerDisplay, null, attacker.name);

                int previousNormalHealth = normalAttackTarget.health;
                normalAttackTarget.health = Mathf.Max(0, normalAttackTarget.health - normalDamage);
                int actualNormalDamage = previousNormalHealth - normalAttackTarget.health;
                float normalDefeatLogShownAt = -1f;
                healthChanged |= actualNormalDamage > 0;
                enemyManager.RefreshEnemyBattleState(normalAttackTarget.enemySlotIndex, normalAttackTarget.health, normalAttackTarget.gap,normalAttackTarget.defense);
                BoardCharacter normalEnemyDisplay = enemyManager.PlayEnemyDamageShake(normalAttackTarget.enemySlotIndex);
                DamagePopupAnimation normalDamagePopup = null;
                if (normalEnemyDisplay != null)
                    normalDamagePopup = normalEnemyDisplay.ShowDamagePopup(damagePopupPrefab, normalDamage);
                SetBattleLog(normalDamage + "のダメージを与えた！");
                if (normalAttackTarget.health == 0 && normalEnemyDisplay != null)
                    normalEnemyDisplay.PlayDefeatFadeAfterPopup(
                        normalDamagePopup,
                        () =>
                        {
                            SetBattleLog(normalAttackTarget.name + "を倒した！");
                            normalDefeatLogShownAt = Time.unscaledTime;
                        });
                yield return FinishAttackPresentation(normalAttackerDisplay);
                if (normalAttackTarget.health == 0 && normalEnemyDisplay != null)
                    yield return WaitForAutoDefeatLog(() => normalDefeatLogShownAt);

                Debug.Log(
                    attacker.name + " performs a normal attack (ATK " + attacker.attack +
                    ") against " + normalAttackTarget.name + " (DEF " +
                    normalAttackTarget.defense + "): damage " + actualNormalDamage +
                    ", enemy HP " + normalAttackTarget.health + "/" +
                    normalAttackTarget.maxHealth + ".",
                    this);

                if (normalAttackTarget.health == 0)
                {
                    yield return WaitForBattleStep();
                    Debug.Log(normalAttackTarget.name + " was defeated.", this);
                    ApplyEnemyEffects(
                        normalAttackTarget.enemyData.pig,
                        normalAttackTarget,
                        attacker,
                        new List<BattleUnit> { normalAttackTarget },
                        effectDataById,
                        enemyManager);
                    enemies.Remove(normalAttackTarget);
                }
            }

            if (enemies.Count == 0)
            {
                yield return WaitForBattleStep();
                yield return FinishBattle(true);
                yield break;
            }

            yield return WaitForBattleStep();

            Debug.Log("Battle round " + round + " - Enemy turn.", this);
            RestoreTemporaryEnemyDefenseReductions(enemies, enemyManager);
            bool enemyWaitingForFutureAction = false;
            foreach (BattleUnit enemy in enemies)
            {
                if (round < enemy.nextActionRound)
                {
                    enemyWaitingForFutureAction = true;
                    yield return WaitForBattleStep();
                    enemy.gap = Mathf.Max(0, enemy.gap - 1);
                    enemyManager.RefreshEnemyBattleState(
                        enemy.enemySlotIndex,
                        enemy.health,
                        enemy.gap,
                        enemy.defense);
                    Debug.Log(
                        enemy.name + " waits until round " + enemy.nextActionRound +
                        " (Gap " + enemy.gap + ", CD " + enemy.cooldown + ").",
                        this);
                    continue;
                }

                yield return WaitForBattleStep();
                enemy.gap = 0;
                enemyManager.RefreshEnemyBattleState(
                        enemy.enemySlotIndex,
                        enemy.health,
                        enemy.gap,
                        enemy.defense);
                TargetedAttack enemyAttack = SelectEnemyTargets(
                    enemy,
                    playerUnits,
                    boardManager,
                    enemyManager,
                    targetDataById);
                if (enemyAttack == null)
                    yield break;
                BoardCharacter enemyAttackerDisplay = enemyManager.GetEnemyBoardCharacter(enemy.enemySlotIndex);
                yield return BeginAttackPresentation(enemyAttackerDisplay, null, enemy.name);
                int totalDefense = 0;
                List<string> targetDescriptions = new List<string>();
                foreach (BattleUnit target in enemyAttack.targets)
                {
                    totalDefense += target.defense;
                    if (target.boardSlot != null)
                    {
                        BoardCharacter targetDisplay = target.boardSlot.GetComponentInChildren<BoardCharacter>(true);
                        if (targetDisplay != null)
                            targetDisplay.PlayDamageShake();
                    }
                    targetDescriptions.Add(
                        target.name + " (BoardSlot " + (target.boardSlotIndex + 1) +
                        ", DEF " + target.defense + ")");
                }

                int totalDamage = CalculateBattleDamage(
                    enemy.attack,
                    enemyAttack.targets.Count > 0 ? totalDefense : 0);
                SetBattleLog(totalDamage + "のダメージを与えた！");

                int blockDamage = Mathf.Min(Block, totalDamage);
                Block -= blockDamage;
                UpdateBlockCountUI();
                int hpDamage = Mathf.Max(0, totalDamage - blockDamage);
                int previousPlayerHealth = player.hp;
                player.hp = Mathf.Max(0, player.hp - hpDamage);
                int actualHpDamage = previousPlayerHealth - player.hp;
                healthChanged |= blockDamage > 0 || actualHpDamage > 0;
                player.UpdateHPUI();
                yield return FinishAttackPresentation(enemyAttackerDisplay);

                string targetDescription = enemyAttack.targets.Count > 0
                    ? string.Join(", ", targetDescriptions.ToArray())
                    : "no character (direct attack on player)";
                Debug.Log(
                    enemy.name + " uses " + enemyAttack.data.targetId + " (" +
                    enemyAttack.data.targetText + ") against " + targetDescription +
                    ": ATK " + enemy.attack + ", total target DEF " + totalDefense +
                    ", damage " + totalDamage + " (Block absorbed " +
                    blockDamage + ", HP damage " + actualHpDamage +
                    "), Player HP " + player.hp + ", Block " + Block + ".",
                    this);

                ApplyEnemyEffects(
                    enemy.enemyData.ae,
                    enemy,
                    enemy,
                    enemyAttack.targets,
                    effectDataById,
                    enemyManager);

                if (player.hp == 0)
                {
                    yield return WaitForBattleStep();
                    yield return FinishBattle(false);
                    yield break;
                }

                enemy.nextActionRound = round + enemy.cooldown + 1;
                enemy.gap = Mathf.Max(0, enemy.cooldown);
                enemyManager.RefreshEnemyBattleState(
                    enemy.enemySlotIndex,
                    enemy.health,
                    enemy.gap,
                    enemy.defense);
            }

           
            // 敵ターン終了時、全敵のDEFを1減少
            foreach (BattleUnit enemy in enemies)
            {
                enemy.defense = Mathf.Max(0, enemy.defense - 1);

                enemyManager.RefreshEnemyBattleState(
                    enemy.enemySlotIndex,
                    enemy.health,
                    enemy.gap,
                    enemy.defense);
            }
            round++;
        }
    }

    private int CountActiveAttributeSynergies(List<BattleUnit> playerUnits)
    {
        List<CharacterData> characters = new List<CharacterData>();
        foreach (BattleUnit battleUnit in playerUnits)
        {
            if (battleUnit.boardSlot != null &&
                battleUnit.boardSlot.placedCharacter != null &&
                battleUnit.boardSlot.placedCharacter.data != null)
                characters.Add(battleUnit.boardSlot.placedCharacter.data);
        }

        return CountActiveAttributeSynergies(characters);
    }

    private int CountActiveAttributeSynergies(IEnumerable<CharacterData> characters)
    {
        Dictionary<string, int> attributeCounts = new Dictionary<string, int>();
        foreach (CharacterData character in characters)
        {
            if (character == null || character.attributes == null)
                continue;

            HashSet<string> attributesOnCharacter = new HashSet<string>();
            foreach (string attribute in character.attributes)
            {
                if (string.IsNullOrWhiteSpace(attribute) ||
                    !attributesOnCharacter.Add(attribute))
                    continue;

                if (attributeCounts.TryGetValue(attribute, out int count))
                    attributeCounts[attribute] = count + 1;
                else
                    attributeCounts.Add(attribute, 1);
            }
        }

        int synergyCount = 0;
        foreach (int count in attributeCounts.Values)
        {
            if (count >= 2)
                synergyCount++;
        }

        return synergyCount;
    }

    private List<BattleUnit> CreatePlayerBattleUnits(BoardManager boardManager)
    {
        List<BattleUnit> units = new List<BattleUnit>();
        BoardSlot[] slots = boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            BoardSlot slot = slots[i];
            if (slot == null || slot.placedCharacter == null || slot.placedCharacter.data == null)
                continue;

            CharacterData character = slot.placedCharacter.data;
            int star = slot.placedCharacter.star;
            BattleUnit battleUnit = new BattleUnit
            {
                name = string.IsNullOrEmpty(character.characterName)
                    ? character.name
                    : character.characterName,
                attack = slot.placedCharacter.GetAttack(characterStatSettings) + slot.placedCharacter.battleAttackBonus,
                defense = slot.placedCharacter.GetDefense(characterStatSettings) + slot.placedCharacter.battleDefenseBonus,
                star = star,
                boardSlot = slot,
                boardSlotIndex = i
            };

            if (character.units != null)
            {
                HashSet<string> uniqueUnits = new HashSet<string>();
                foreach (string unitName in character.units)
                {
                    if (!string.IsNullOrWhiteSpace(unitName) && uniqueUnits.Add(unitName))
                        battleUnit.unitNames.Add(unitName);
                }
            }

            units.Add(battleUnit);
        }

        return units;
    }

    private List<UnitMove> CreateActiveUnitMoves(
        List<BattleUnit> playerUnits,
        BoardManager boardManager)
    {
        List<UnitMove> unitMoves = new List<UnitMove>();
        ShopManager currentShopManager = shopManager != null
            ? shopManager
            : FindAnyObjectByType<ShopManager>();
        if (currentShopManager == null || currentShopManager.characters == null)
            return unitMoves;

        foreach (BattleUnit battleUnit in playerUnits)
        {
            foreach (string unitName in battleUnit.unitNames)
            {
                UnitMove unitMove = unitMoves.Find(move => move.name == unitName);
                if (unitMove == null)
                {
                    unitMove = new UnitMove { name = unitName };
                    foreach (CharacterData rosterCharacter in currentShopManager.characters)
                    {
                        if (CharacterHasUnit(rosterCharacter, unitName))
                            unitMove.rosterMembers.Add(rosterCharacter);
                    }
                    unitMoves.Add(unitMove);
                }

                unitMove.participants.Add(battleUnit);
                unitMove.boardSlots.Add(battleUnit.boardSlot);
            }
        }

        unitMoves.RemoveAll(move =>
            move.participants.Count < 2 ||
            move.participants.Count != move.rosterMembers.Count ||
            !currentShopManager.IsUnitComplete(move.name) ||
            !AreUnitSlotsConnected(move.boardSlots, boardManager.columns));

        unitMoves.Sort(CompareUnitMovePriority);

        return unitMoves;
    }

    private int CompareUnitMovePriority(UnitMove left, UnitMove right)
    {
        int participantCountComparison =
            right.participants.Count.CompareTo(left.participants.Count);
        if (participantCountComparison != 0)
            return participantCountComparison;

        for (int i = 0; i < left.participants.Count; i++)
        {
            int slotComparison =
                left.participants[i].boardSlotIndex.CompareTo(
                    right.participants[i].boardSlotIndex);
            if (slotComparison != 0)
                return slotComparison;
        }

        return string.CompareOrdinal(left.name, right.name);
    }

    private bool CharacterHasUnit(CharacterData character, string unitName)
    {
        if (character == null || character.units == null)
            return false;

        foreach (string characterUnit in character.units)
        {
            if (string.Equals(characterUnit, unitName, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool AreUnitSlotsConnected(List<BoardSlot> unitSlots, int columns)
    {
        if (unitSlots.Count < 2 || columns <= 0)
            return false;

        HashSet<BoardSlot> remaining = new HashSet<BoardSlot>(unitSlots);
        Queue<BoardSlot> queue = new Queue<BoardSlot>();
        BoardSlot first = unitSlots[0];
        remaining.Remove(first);
        queue.Enqueue(first);

        while (queue.Count > 0)
        {
            BoardSlot current = queue.Dequeue();
            int currentIndex = current.transform.GetSiblingIndex();
            int currentRow = currentIndex / columns;
            int currentColumn = currentIndex % columns;
            List<BoardSlot> connectedNeighbors = new List<BoardSlot>();

            foreach (BoardSlot candidate in remaining)
            {
                int candidateIndex = candidate.transform.GetSiblingIndex();
                int candidateRow = candidateIndex / columns;
                int candidateColumn = candidateIndex % columns;
                if (Mathf.Abs(currentRow - candidateRow) +
                    Mathf.Abs(currentColumn - candidateColumn) == 1)
                    connectedNeighbors.Add(candidate);
            }

            foreach (BoardSlot neighbor in connectedNeighbors)
            {
                remaining.Remove(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return remaining.Count == 0;
    }

    private List<UnitMove> FindAvailableUnitMoves(
        BattleUnit actingCharacter,
        List<UnitMove> activeUnitMoves,
        UnitMoveRoundState roundState)
    {
        List<UnitMove> availableMoves = new List<UnitMove>();
        foreach (UnitMove unitMove in activeUnitMoves)
        {
            if (roundState.usedMoves.Contains(unitMove) ||
                !unitMove.participants.Contains(actingCharacter))
                continue;

            bool allParticipantsAvailable = true;
            foreach (BattleUnit participant in unitMove.participants)
            {
                if (GetParticipationCount(roundState, participant) >=
                    Mathf.Clamp(participant.star, 1, 3))
                {
                    allParticipantsAvailable = false;
                    break;
                }
            }

            if (allParticipantsAvailable)
                availableMoves.Add(unitMove);
        }

        return availableMoves;
    }

    private int GetParticipationCount(UnitMoveRoundState roundState, BattleUnit character)
    {
        return roundState.participationCounts.TryGetValue(character, out int count)
            ? count
            : 0;
    }

    private void IncrementParticipationCount(UnitMoveRoundState roundState, BattleUnit character)
    {
        roundState.participationCounts[character] =
            GetParticipationCount(roundState, character) + 1;
    }

    private List<BattleUnit> CreateEnemyBattleUnits(EnemyManager enemyManager)
    {
        List<BattleUnit> units = new List<BattleUnit>();
        EnemyFormationData formation = enemyManager.currentFormation;
        for (int i = 0; i < enemyManager.enemyDataBySlot.Length; i++)
        {
            EnemyData enemy = enemyManager.enemyDataBySlot[i];
            if (enemy == null)
                continue;
            int effectiveHealth = Mathf.Max(0, enemy.health + enemyManager.GetBattleHealthBonus(i));
            if (effectiveHealth <= 0)
                continue;

            int gap = 0;
            int cooldown = 0;
            if (formation != null && formation.slots != null && i < formation.slots.Length)
            {
                EnemyFormationData.FormationSlot slot = formation.slots[i];
                if (slot != null)
                {
                    gap = Mathf.Max(0, slot.gap + enemyManager.GetBattleGapBonus(i));
                    cooldown = Mathf.Max(0, slot.cooldown);
                }
            }

            units.Add(new BattleUnit
            {
                name = string.IsNullOrEmpty(enemy.enemyName) ? enemy.name : enemy.enemyName,
                attack = enemy.attack + enemyManager.GetBattleAttackBonus(i),
                defense = enemy.defense + enemyManager.GetBattleDefenseBonus(i),
                maxHealth = effectiveHealth,
                health = effectiveHealth,
                gap = gap,
                cooldown = cooldown,
                nextActionRound = gap + 1,
                enemyData = enemy,
                enemySlotIndex = i
            });
        }

        return units;
    }

    private Dictionary<string, TargetData> LoadTargetData()
    {
        Dictionary<string, TargetData> targetDataById =
            new Dictionary<string, TargetData>(System.StringComparer.Ordinal);
        TargetData[] targetDataAssets = Resources.LoadAll<TargetData>("TargetData");
        foreach (TargetData targetData in targetDataAssets)
        {
            if (targetData == null || string.IsNullOrWhiteSpace(targetData.targetId))
                continue;

            if (targetDataById.ContainsKey(targetData.targetId))
            {
                Debug.LogError("Duplicate TargetID found: " + targetData.targetId + ".", targetData);
                continue;
            }

            targetDataById.Add(targetData.targetId, targetData);
        }

        return targetDataById;
    }

    private Dictionary<string, EffectData> LoadEffectData()
    {
        Dictionary<string, EffectData> effectDataById =
            new Dictionary<string, EffectData>(System.StringComparer.Ordinal);
        EffectData[] effectDataAssets = Resources.LoadAll<EffectData>("EffectData");
        foreach (EffectData effectData in effectDataAssets)
        {
            if (effectData == null || string.IsNullOrWhiteSpace(effectData.effectId))
                continue;

            if (effectDataById.ContainsKey(effectData.effectId))
            {
                Debug.LogError(
                    "Duplicate EffectID found: " + effectData.effectId + ".",
                    effectData);
                continue;
            }

            effectDataById.Add(effectData.effectId, effectData);
        }

        return effectDataById;
    }

    private bool ValidateEnemyEffects(
        List<BattleUnit> enemies,
        Dictionary<string, EffectData> effectDataById)
    {
        foreach (BattleUnit enemy in enemies)
        {
            string[] effectLists =
            {
                enemy.enemyData.cip,
                enemy.enemyData.ae,
                enemy.enemyData.pig
            };
            for (int triggerIndex = 0; triggerIndex < effectLists.Length; triggerIndex++)
            {
                foreach (string effectId in GetEnemyEffectIds(effectLists[triggerIndex]))
                {
                    if (!effectDataById.TryGetValue(effectId, out EffectData effectData))
                    {
                        Debug.LogError(
                            enemy.name + " references EffectID " + effectId +
                            ", but no matching EffectData exists under Resources/EffectData.",
                            enemy.enemyData);
                        return false;
                    }

                    if (!IsValidEnemyEffect(effectData))
                        return false;
                    if (triggerIndex == 0 && effectData.target == "攻撃対象")
                    {
                        Debug.LogError(
                            "CIP cannot target 攻撃対象 because no attack has occurred yet: " +
                            effectData.effectId + ".",
                            effectData);
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private bool IsValidEnemyEffect(EffectData effectData)
    {
        bool validDuration = effectData.duration == "このバトル中" ||
                             effectData.duration == "即時効果";
        bool targetsPlayer = effectData.target == "プレイヤー";
        bool validEffectTarget =
            (effectData.effect == "Attack" || effectData.effect == "Def") &&
            (effectData.target == "攻撃者" || effectData.target == "攻撃対象") ||
            effectData.effect == "Gap" &&
            (effectData.target == "攻撃者" || effectData.target == "攻撃対象") ||
            (effectData.effect == "Gold" || effectData.effect == "Damage" ||
             effectData.effect == "Block") && targetsPlayer;

        if (!validDuration || !validEffectTarget ||
            (effectData.effect == "Gold" && effectData.duration != "即時効果") ||
            (effectData.effect == "Gap" && effectData.target == "攻撃対象" &&
             effectData.duration == "即時効果"))
        {
            Debug.LogError(
                "Invalid EffectData configuration for EffectID " + effectData.effectId +
                " (Effect: " + effectData.effect + ", Target: " + effectData.target +
                ", Duration: " + effectData.duration + ").",
                effectData);
            return false;
        }

        return true;
    }

    private List<string> GetEnemyEffectIds(string effectValue)
    {
        List<string> effectIds = new List<string>();
        if (string.IsNullOrWhiteSpace(effectValue))
            return effectIds;

        string[] values = effectValue.Split(
            new[] { ',', ';', '|', '/', '、', ' ', '\t', '\r', '\n' },
            System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string value in values)
        {
            string effectId = value.Trim();
            if (!string.IsNullOrEmpty(effectId))
                effectIds.Add(effectId);
        }

        return effectIds;
    }

    private void ApplyEnemyEffects(
        string effectList,
        BattleUnit effectOwner,
        BattleUnit triggerActor,
        List<BattleUnit> attackTargets,
        Dictionary<string, EffectData> effectDataById,
        EnemyManager enemyManager)
    {
        foreach (string effectId in GetEnemyEffectIds(effectList))
        {
            if (!effectDataById.TryGetValue(effectId, out EffectData effectData))
            {
                Debug.LogError("EffectID " + effectId + " was not loaded.", this);
                continue;
            }

            ApplyEnemyEffect(
                effectData, effectOwner, triggerActor, attackTargets, enemyManager);
        }
    }

    private void ApplyEnemyEffect(
        EffectData effectData,
        BattleUnit effectOwner,
        BattleUnit triggerActor,
        List<BattleUnit> attackTargets,
        EnemyManager enemyManager)
    {
        if (effectData == null)
            return;

        bool immediateEffect;
        if (effectData.duration == "このバトル中")
            immediateEffect = false;
        else if (effectData.duration == "即時効果")
            immediateEffect = true;
        else
        {
            Debug.LogError(
                "Unsupported duration '" + effectData.duration +
                "' on EffectID " + effectData.effectId + ".",
                effectData);
            return;
        }

        Debug.Log(
            "Enemy effect " + effectData.effectId + " activated: " +
            effectData.description,
            effectData);

        string target = (effectData.target ?? string.Empty).Trim();
        if (target == "プレイヤー")
        {
            ApplyEnemyEffectToPlayer(effectData, immediateEffect);
            return;
        }

        List<BattleUnit> targets = target == "攻撃者"
            ? new List<BattleUnit> { triggerActor ?? effectOwner }
            : target == "攻撃対象" && attackTargets != null
                ? attackTargets
                : null;
        if (targets == null)
        {
            Debug.LogError(
                "Unsupported or unavailable target '" + effectData.target +
                "' on EffectID " + effectData.effectId + ".",
                effectData);
            return;
        }

        foreach (BattleUnit targetUnit in targets)
        {
            if (targetUnit == null)
                continue;

            switch (effectData.effect)
            {
                case "Attack":
                case "Def":
                    ApplyEnemyEffectToUnit(
                        targetUnit, effectData.effect, effectData.effectValue,
                        immediateEffect, enemyManager, effectData);
                    break;
                case "Gap":
                    if (targetUnit.enemyData != null)
                    {
                        targetUnit.gap = Mathf.Max(0, targetUnit.gap + effectData.effectValue);
                        targetUnit.nextActionRound += Mathf.Max(0, effectData.effectValue);
                        if (immediateEffect)
                        {
                            BoardSlot enemySlot = GetEnemyBoardSlot(
                                enemyManager, targetUnit.enemySlotIndex);
                            if (enemySlot == null ||
                                !enemyManager.TryApplyItemEffect(
                                    enemySlot, "Gap", effectData.effectValue, true))
                                Debug.LogError(
                                    "Could not apply permanent Gap effect to " + targetUnit.name + ".",
                                    effectData);
                        }
                        enemyManager.RefreshEnemyBattleState(
                            targetUnit.enemySlotIndex,
                            targetUnit.health,
                            targetUnit.gap,
                            targetUnit.defense);
                    }
                    else
                    {
                        if (immediateEffect)
                        {
                            Debug.LogError(
                                "Player-character Gap effects must use duration このバトル中: " +
                                effectData.effectId + ".",
                                effectData);
                        }
                        else
                        {
                            targetUnit.stunRounds += Mathf.Max(0, effectData.effectValue);
                        }
                    }
                    break;
                default:
                    Debug.LogError(
                        "Unsupported effect '" + effectData.effect +
                        "' on EffectID " + effectData.effectId + ".",
                        effectData);
                    break;
            }
        }
    }

    private void ApplyEnemyEffectToPlayer(EffectData effectData, bool immediateEffect)
    {
        if (effectData.effect == "Gold")
        {
            if (!immediateEffect)
                Debug.LogWarning(
                    "Gold effects are persistent; set the duration to 即時効果 on " +
                    effectData.effectId + ".",
                    effectData);
            player.gold = Mathf.Max(0, player.gold + effectData.effectValue);
            player.UpdateGoldUI();
            return;
        }

        if (effectData.effect == "Damage")
        {
            if (!ApplyPlayerItemEffect(effectData.effect, effectData.effectValue, immediateEffect))
                Debug.LogError(
                    "Could not apply " + effectData.effect +
                    " to the player for EffectID " + effectData.effectId + ".",
                    effectData);
            return;
        }

        if (effectData.effect == "Block")
        {
            if (immediateEffect)
                permanentPlayerBlockBonus += effectData.effectValue;
            Block = Mathf.Max(0, Block + effectData.effectValue);
            UpdateBlockCountUI();
            return;
        }

        Debug.LogError(
            "Effect '" + effectData.effect + "' cannot target the player for EffectID " +
            effectData.effectId + ".",
            effectData);
    }

    private void ApplyEnemyEffectToUnit(
        BattleUnit target,
        string effect,
        int value,
        bool immediateEffect,
        EnemyManager enemyManager,
        EffectData effectData)
    {
        if (target.enemyData != null)
        {
            if (immediateEffect)
            {
                BoardSlot enemySlot = GetEnemyBoardSlot(enemyManager, target.enemySlotIndex);
                if (enemySlot == null ||
                    !enemyManager.TryApplyItemEffect(enemySlot, effect, value, true))
                {
                    Debug.LogError(
                        "Could not apply permanent " + effect + " effect to " + target.name + ".",
                        effectData);
                    return;
                }
            }

            if (effect == "Attack")
                target.attack += value;
            else
                target.defense += value;
            enemyManager.RefreshEnemyBattleState(
                target.enemySlotIndex,
                target.health,
                target.gap,
                target.defense,
                target.attack);
            return;
        }

        if (target.boardSlot == null || target.boardSlot.placedCharacter == null)
        {
            Debug.LogError(
                "Effect target has no player character for EffectID " + effectData.effectId + ".",
                effectData);
            return;
        }

        OwnedCharacter character = target.boardSlot.placedCharacter;
        if (immediateEffect)
        {
            if (effect == "Attack")
                character.attack += value;
            else
                character.defense += value;
        }
        else if (effect == "Attack")
        {
            character.battleAttackBonus += value;
        }
        else
        {
            character.battleDefenseBonus += value;
        }

        if (effect == "Attack")
            target.attack += value;
        else
            target.defense += value;

        BoardCharacter display =
            target.boardSlot.GetComponentInChildren<BoardCharacter>(true);
        if (display != null)
            display.RefreshOwnedStats();

        BoardManager.RefreshVisibleDetailPanels();
    }

    private BoardSlot GetEnemyBoardSlot(EnemyManager enemyManager, int slotIndex)
    {
        if (enemyManager == null || enemyManager.boardArea == null)
            return null;

        BoardSlot[] slots = enemyManager.boardArea.GetComponentsInChildren<BoardSlot>(true);
        return slotIndex >= 0 && slotIndex < slots.Length ? slots[slotIndex] : null;
    }

    private bool ValidateEnemyTargets(
        List<BattleUnit> enemies,
        Dictionary<string, TargetData> targetDataById)
    {
        foreach (BattleUnit enemy in enemies)
        {
            foreach (string targetId in GetEnemyTargetIds(enemy.enemyData.target))
            {
                if (targetDataById.ContainsKey(targetId))
                    continue;

                Debug.LogError(
                    enemy.name + " references TargetID " + targetId +
                    ", but no matching TargetData exists under Resources/TargetData.",
                    enemy.enemyData);
                return false;
            }
        }

        return true;
    }

    private List<string> GetEnemyTargetIds(string targetValue)
    {
        List<string> targetIds = new List<string>();
        if (string.IsNullOrWhiteSpace(targetValue))
        {
            targetIds.Add("T001");
            return targetIds;
        }

        string[] values = targetValue.Split(
            new[] { ',', ';', '|', '/', '、', ' ', '\t', '\r', '\n' },
            System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string value in values)
        {
            string targetId = value.Trim();
            if (!string.IsNullOrEmpty(targetId))
                targetIds.Add(targetId);
        }

        if (targetIds.Count == 0)
            targetIds.Add("T001");
        return targetIds;
    }

    private TargetedAttack SelectEnemyTargets(
        BattleUnit enemy,
        List<BattleUnit> playerUnits,
        BoardManager boardManager,
        EnemyManager enemyManager,
        Dictionary<string, TargetData> targetDataById)
    {
        List<string> targetIds = GetEnemyTargetIds(enemy.enemyData.target);
        string targetId = targetIds[enemy.nextTargetIndex];
        enemy.nextTargetIndex = (enemy.nextTargetIndex + 1) % targetIds.Count;
        TargetData targetData = targetDataById[targetId];
        TargetedAttack attack = new TargetedAttack { data = targetData };

        List<BattleUnit> candidates = GetCandidatesForFilter(
            targetData.candidateFilter, enemy, playerUnits, boardManager, enemyManager);
        if (candidates.Count == 0)
            return attack;

        if (!TryParseSelectionMethod(targetData, out TargetSelectionMode mode, out int count, out List<TargetWeightRule> rules))
        {
            Debug.LogError("Invalid selection method for TargetID " + targetId + ": " + targetData.selectionMethod, targetData);
            return attack;
        }

        if (mode == TargetSelectionMode.All)
        {
            attack.targets.AddRange(candidates);
            return attack;
        }

        int draws = Mathf.Min(count, candidates.Count);
        for (int draw = 0; draw < draws; draw++)
        {
            if (mode == TargetSelectionMode.Uniform)
            {
                int index = Random.Range(0, candidates.Count);
                attack.targets.Add(candidates[index]);
                candidates.RemoveAt(index);
                continue;
            }

            List<long> candidateWeights = new List<long>(candidates.Count);
            long totalWeight = 0;
            foreach (BattleUnit candidate in candidates)
            {
                long weight = GetCandidateWeight(
                    candidate, enemy, playerUnits, boardManager, enemyManager, rules);
                candidateWeights.Add(weight);
                totalWeight += weight;
            }
            if (totalWeight <= 0)
                break;

            double roll = Random.value * totalWeight;
            int selectedIndex = candidateWeights.Count - 1;
            for (int i = 0; i < candidateWeights.Count; i++)
            {
                roll -= candidateWeights[i];
                if (roll < 0)
                {
                    selectedIndex = i;
                    break;
                }
            }
            attack.targets.Add(candidates[selectedIndex]);
            candidates.RemoveAt(selectedIndex);
        }
        return attack;
    }

    private List<BattleUnit> GetCandidatesForFilter(
        string filter,
        BattleUnit enemy,
        List<BattleUnit> playerUnits,
        BoardManager boardManager,
        EnemyManager enemyManager)
    {
        List<BattleUnit> candidates = new List<BattleUnit>();
        int columns = boardManager.columns;
        if (columns <= 0) return candidates;

        if (TryParseCellSelection(filter, out TargetSelectionMode cellMode, out int cellCount, out List<TargetWeightRule> cellRules))
        {
            BoardSlot[] slots = boardManager.boardArea != null
                ? boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true)
                : new BoardSlot[0];
            List<int> eligibleCells = new List<int>();
            for (int cellIndex = 0; cellIndex < slots.Length; cellIndex++)
                eligibleCells.Add(cellIndex); // 空マスも抽選候補に含める

            int draws = Mathf.Min(cellCount, eligibleCells.Count);
            HashSet<int> selectedCells = new HashSet<int>();
            for (int draw = 0; draw < draws; draw++)
            {
                int selectedIndex;
                if (cellMode == TargetSelectionMode.Uniform)
                {
                    selectedIndex = Random.Range(0, eligibleCells.Count);
                }
                else
                {
                    List<long> weights = new List<long>(eligibleCells.Count);
                    long totalWeight = 0;
                    foreach (int cellIndex in eligibleCells)
                    {
                        long weight = GetCandidateWeight(
                            cellIndex, enemy, boardManager, enemyManager, cellRules);
                        weights.Add(weight);
                        totalWeight += weight;
                    }
                    if (totalWeight <= 0)
                        break;
                    selectedIndex = ChooseWeightedIndex(weights, totalWeight);
                }

                selectedCells.Add(eligibleCells[selectedIndex]);
                eligibleCells.RemoveAt(selectedIndex);
            }

            foreach (BattleUnit unit in playerUnits)
            {
                if (selectedCells.Contains(unit.boardSlotIndex))
                    candidates.Add(unit);
            }
            return candidates;
        }

        int enemyLine = enemyManager.columns > 0
            ? enemy.enemySlotIndex / enemyManager.columns
            : -1;
        foreach (BattleUnit unit in playerUnits)
        {
            int column = unit.boardSlotIndex % columns;
            int row = unit.boardSlotIndex / columns;
            bool include = filter == "全マス" ||
                           (filter == "後列" && column == 0) ||
                           (filter == "中列" && column == columns / 2) ||
                           (filter == "前列" && column == columns - 1) ||
                           (filter == "直線" && row == enemyLine);
            if (include) candidates.Add(unit);
        }
        return candidates;
    }

    private bool TryParseCellSelection(
        string value,
        out TargetSelectionMode mode,
        out int count,
        out List<TargetWeightRule> rules)
    {
        mode = TargetSelectionMode.Uniform;
        count = 0;
        rules = new List<TargetWeightRule>();
        if (string.IsNullOrWhiteSpace(value)) return false;

        string[] parts = value.Split(',');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        if (parts[0] != "均等抽選" && parts[0] != "重み付き抽選")
            return false;
        if (parts.Length < 2 || !int.TryParse(parts[1], out count) || count <= 0)
            return false;
        if (parts[0] == "均等抽選" && parts.Length == 2)
        {
            mode = TargetSelectionMode.Uniform;
            return true;
        }
        if (parts[0] != "重み付き抽選" || parts.Length < 4 || (parts.Length - 2) % 2 != 0)
            return false;

        mode = TargetSelectionMode.Weighted;
        for (int i = 2; i < parts.Length; i += 2)
        {
            bool validPosition = parts[i] == "後列" || parts[i] == "中列" || parts[i] == "前列" ||
                                 parts[i] == "直線" || parts[i] == "else" ||
                                 (int.TryParse(parts[i], out int cell) && cell >= 1 && cell <= 9);
            if (!validPosition || !int.TryParse(parts[i + 1], out int weight) || weight < 0)
                return false;
            rules.Add(new TargetWeightRule { position = parts[i], weight = weight });
        }
        return true;
    }

    private int ChooseWeightedIndex(List<long> weights, long totalWeight)
    {
        double roll = Random.value * totalWeight;
        int lastPositiveIndex = -1;
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0) continue;
            lastPositiveIndex = i;
            roll -= weights[i];
            if (roll < 0) return i;
        }
        return lastPositiveIndex;
    }
    private bool TryParseSelectionMethod(
        TargetData targetData,
        out TargetSelectionMode mode,
        out int count,
        out List<TargetWeightRule> rules)
    {
        mode = TargetSelectionMode.Uniform;
        count = 1;
        rules = new List<TargetWeightRule>();
        string method = targetData.selectionMethod;
        if (string.IsNullOrWhiteSpace(method)) return false;

        string[] parts = method.Split(',');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        if (parts.Length == 1 && parts[0] == "全員選択")
        {
            mode = TargetSelectionMode.All;
            return true;
        }
        if (parts.Length < 2 || !int.TryParse(parts[1], out count) || count <= 0)
            return false;
        if (parts[0] == "均等抽選" && parts.Length == 2)
        {
            mode = TargetSelectionMode.Uniform;
            return true;
        }
        if (parts[0] != "重み付き抽選" || parts.Length < 4 || (parts.Length - 2) % 2 != 0)
            return false;

        mode = TargetSelectionMode.Weighted;
        for (int i = 2; i < parts.Length; i += 2)
        {
            bool validPosition = parts[i] == "後列" || parts[i] == "中列" || parts[i] == "前列" ||
                                 parts[i] == "直線" || parts[i] == "前方人数0" ||
                                 parts[i] == "前方人数1" || parts[i] == "前方人数2" ||
                                 parts[i] == "else" ||
                                 (int.TryParse(parts[i], out int cell) && cell >= 1 && cell <= 9);
            if (!validPosition || !int.TryParse(parts[i + 1], out int weight) || weight < 0)
                return false;
            rules.Add(new TargetWeightRule { position = parts[i], weight = weight });
        }
        return true;
    }

    private long GetCandidateWeight(
        BattleUnit candidate,
        BattleUnit enemy,
        List<BattleUnit> playerUnits,
        BoardManager boardManager,
        EnemyManager enemyManager,
        List<TargetWeightRule> rules)
    {
        long? defaultWeight = null;
        foreach (TargetWeightRule rule in rules)
        {
            if (rule.position == "else")
            {
                defaultWeight = rule.weight;
                continue;
            }
            if (IsCandidateAtPosition(
                    rule.position, candidate, playerUnits, enemy, boardManager, enemyManager))
                return rule.weight;
        }
        return defaultWeight ?? 1;
    }

    private long GetCandidateWeight(
        int slotIndex,
        BattleUnit enemy,
        BoardManager boardManager,
        EnemyManager enemyManager,
        List<TargetWeightRule> rules)
    {
        long? defaultWeight = null;
        foreach (TargetWeightRule rule in rules)
        {
            if (rule.position == "else")
            {
                defaultWeight = rule.weight;
                continue;
            }
            if (IsCandidateAtPosition(rule.position, slotIndex, enemy, boardManager, enemyManager))
                return rule.weight;
        }
        return defaultWeight ?? 1;
    }

    private bool IsCandidateAtPosition(
        string position,
        BattleUnit candidate,
        List<BattleUnit> playerUnits,
        BattleUnit enemy,
        BoardManager boardManager,
        EnemyManager enemyManager)
    {
        if (position == "前方人数0" || position == "前方人数1" || position == "前方人数2")
        {
            int columns = boardManager.columns;
            if (columns <= 0)
                return false;

            int candidateColumn = candidate.boardSlotIndex % columns;
            int candidateRow = candidate.boardSlotIndex / columns;
            int frontCharacterCount = 0;
            foreach (BattleUnit ally in playerUnits)
            {
                int allyColumn = ally.boardSlotIndex % columns;
                int allyRow = ally.boardSlotIndex / columns;
                if (allyRow == candidateRow && allyColumn > candidateColumn)
                    frontCharacterCount++;
            }
            return position == "前方人数" + frontCharacterCount;
        }

        return IsCandidateAtPosition(
            position, candidate.boardSlotIndex, enemy, boardManager, enemyManager);
    }

    private bool IsCandidateAtPosition(
        string position,
        BattleUnit candidate,
        BattleUnit enemy,
        BoardManager boardManager,
        EnemyManager enemyManager)
    {
        return IsCandidateAtPosition(position, candidate.boardSlotIndex, enemy, boardManager, enemyManager);
    }

    private bool IsCandidateAtPosition(
        string position,
        int slotIndex,
        BattleUnit enemy,
        BoardManager boardManager,
        EnemyManager enemyManager)
    {
        int columns = boardManager.columns;
        if (columns <= 0) return false;
        int column = slotIndex % columns;
        int row = slotIndex / columns;
        if (position == "後列") return column == 0;
        if (position == "中列") return column == columns / 2;
        if (position == "前列") return column == columns - 1;
        if (position == "直線")
            return enemyManager.columns > 0 && row == enemy.enemySlotIndex / enemyManager.columns;
        if (int.TryParse(position, out int cellNumber))
            return slotIndex == cellNumber - 1;
        return false;
    }
    private int CalculateBattleDamage(int attack, int defense)
    {
        return Mathf.Max(0, attack - defense);
    }

    private void ReduceEnemyDefenseUntilEnemyTurn(
        BattleUnit enemy,
        int reductionAmount = 1)
    {
        if (enemy == null ||
            enemy.enemyData == null ||
            reductionAmount <= 0 ||
            enemy.defense <= 0)
            return;

        int actualReduction = Mathf.Min(enemy.defense, reductionAmount);
        enemy.defense -= actualReduction;
        enemy.temporaryDefenseReduction += actualReduction;
    }

    private void RestoreTemporaryEnemyDefenseReductions(
        List<BattleUnit> enemies,
        EnemyManager enemyManager)
    {
        foreach (BattleUnit enemy in enemies)
        {
            if (enemy == null || enemy.enemyData == null ||
                enemy.temporaryDefenseReduction <= 0)
                continue;

            enemy.defense += enemy.temporaryDefenseReduction;
            enemy.temporaryDefenseReduction = 0;
            enemyManager.RefreshEnemyBattleState(
                enemy.enemySlotIndex,
                enemy.health,
                enemy.gap,
                enemy.defense,
                enemy.attack);
        }
    }

    private IEnumerator WaitForAutoDefeatLog(System.Func<float> getShownAt)
    {
        if (playbackMode != BattlePlaybackMode.Auto)
            yield break;

        while (getShownAt() < 0f)
            yield return null;

        float remainingTime = 1f - (Time.unscaledTime - getShownAt());
        if (remainingTime > 0f)
            yield return new WaitForSecondsRealtime(remainingTime);
    }

    public bool ApplyPlayerItemEffect(string effect, int value, bool immediateEffect = false)
    {
        if (effect == "Damage")
        {
            int previousHp = player.hp;
            player.hp = Mathf.Clamp(player.hp - value, 0, player.InitialHp);
            if (!immediateEffect)
                pendingPlayerHpDelta += player.hp - previousHp;
            player.UpdateHPUI();
            return true;
        }
        if (effect == "Block")
        {
            if (immediateEffect)
                permanentPlayerBlockBonus += value;
            else
                pendingPlayerBlockBonus += value;
            if (battleCoroutine == null)
                RefreshPreBattleBlockCount();
            return true;
        }
        return false;
    }

    private IEnumerator FinishBattle(bool won)
    {
        LastBattleWasWin = won;
        if (player != null && pendingPlayerHpDelta != 0)
        {
            player.hp = Mathf.Max(0, player.hp - pendingPlayerHpDelta);
            pendingPlayerHpDelta = 0;
        }
        pendingPlayerBlockBonus = 0;
        BoardSlot[] playerSlots = FindAnyObjectByType<BoardManager>()?.boardArea?.GetComponentsInChildren<BoardSlot>(true);
        if (playerSlots != null)
        {
            foreach (BoardSlot slot in playerSlots)
            {
                if (slot == null || slot.placedCharacter == null) continue;
                slot.placedCharacter.battleAttackBonus = 0;
                slot.placedCharacter.battleDefenseBonus = 0;
                slot.placedCharacter.RevertTemporaryStar();
                BoardCharacter display = slot.GetComponentInChildren<BoardCharacter>(true);
                if (display != null) display.RefreshOwnedStats();
            }
        }
        EnemyManager enemyManager = FindAnyObjectByType<EnemyManager>();
        if (enemyManager != null)
            enemyManager.ResetBattleItemBonuses(refreshSlots: !won);
        player?.UpdateHPUI();
        Block = 0;
        UpdateBlockCountUI();

        if (won)
        {
            Debug.Log("Battle won. Player HP remaining: " + player.hp + ".", this);
            if (shopManager == null)
            {
                Debug.LogError("ShopManager is required to advance to the next stage.", this);
                yield break;
            }

            int goldReward = shopManager.AdvanceStageAfterBattle(false);
            SetBattleLog(goldReward + "ゴールドを手に入れた！");
            yield return new WaitForSecondsRealtime(3f);
            shopManager.BeginNextStageSelectionAfterBattle();
            yield break;
        }

        Debug.Log("Battle lost. Player HP reached 0.", this);
        if (!Application.CanStreamedLevelBeLoaded("CompositionScene"))
        {
            Debug.LogError("CompositionScene is not available in the Build Settings.", this);
            yield break;
        }

        SceneManager.LoadScene("CompositionScene");
    }
}
