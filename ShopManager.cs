using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UI;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [System.Serializable]
    public class StageCostAppearanceRate
    {
        [Range(0, 100)] public int cost1;
        [Range(0, 100)] public int cost2;
        [Range(0, 100)] public int cost3;
        [Range(0, 100)] public int cost4;
        [Range(0, 100)] public int cost5;

        public int GetWeight(int cost)
        {
            switch (cost)
            {
                case 1: return cost1;
                case 2: return cost2;
                case 3: return cost3;
                case 4: return cost4;
                case 5: return cost5;
                default: return 0;
            }
        }
    }

    [Header("キャラクターデータ")]
    public CharacterData[] characters;

    [Header("全キャラクター名簿")]
    public CharacterData[] fullCharacterRoster;

    [Header("ショップ")]
    public GameObject shopSlotPrefab;
    public Transform shopArea;
    [Tooltip("SelectBで並べるItemDataを設定します。")]
    public ItemData[] itemDataPool;

    [Header("プレイヤー")]
    public PlayerData player;

    [Header("ステージ別ショップスロット数")]
    [Tooltip("要素0〜4がステージ1〜5のショップ枠数です。")]
    [Min(0)] public int[] shopSlotsByStage = { 4, 4, 4, 4, 4 };

    [Header("リロール")]
    [Min(0)] public int rerollCost = 2;

    [Header("ステージ報酬・利子")]
    [Min(0)] public int stageGoldReward = 4;
    [Min(1)] public int goldPerInterest = 10;
    [Min(0)] public int interestGoldPerThreshold = 1;
    [Min(0)] public int maxInterestGold = 30;
    [Header("HandSlot空き枠ボーナス")]
    [Min(1)] public int emptyHandSlotsPerBonusGold = 1;
    [Min(0)] public int bonusGoldPerEmptyHandSlots = 1;

    [Header("コスト別ショップ在庫数")]
    [Min(0)] public int[] initialPoolCountsByCost =
    {
        30, 25, 18, 10, 9
    };

    [Header("コスト別キャラクター配分")]
    [Tooltip("要素0〜4がコスト1〜5のキャラクター人数です。")]
    [Min(0)] public int[] charactersPerCost =
    {
        12, 8, 6, 4, 0
    };

    [Header("ステージ別コスト排出率")]
    [Tooltip("配列の要素0〜4がステージ1〜5です。割合の合計が100になるよう設定してください。")]
    public StageCostAppearanceRate[] stageCostAppearanceRates =
    {
        new StageCostAppearanceRate { cost1 = 85, cost2 = 15, cost3 = 0, cost4 = 0, cost5 = 0 },
        new StageCostAppearanceRate { cost1 = 50, cost2 = 40, cost3 = 10, cost4 = 0, cost5 = 0 },
        new StageCostAppearanceRate { cost1 = 10, cost2 = 45, cost3 = 45, cost4 = 0, cost5 = 0 },
        new StageCostAppearanceRate { cost1 = 0, cost2 = 25, cost3 = 60, cost4 = 15, cost5 = 0 },
        new StageCostAppearanceRate { cost1 = 0, cost2 = 10, cost3 = 45, cost4 = 45, cost5 = 0 }
    };

    [Header("コスト出現率表示")]
    [Tooltip("Cost1〜Cost5の確率バーです。未設定の場合はProbabilityCost1〜ProbabilityCost5という名前から検索します。")]
    public Image[] probabilityCostImages = new Image[5];

    [Header("ステージ進行")]
    public TMP_Text stageCountText;
    public StageManager stageManager;
    public GameObject rerollButtonObject;
    public GameObject xpBuyButtonObject;

    [Header("ステージ別必要スコア")]
    [Tooltip("要素0からステージ1-1〜5-5の順に必要スコアを設定します。")]
    [Min(0)] public int[] requiredScoresByStage =
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
        11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
        21, 22, 23, 24, 25
    };

    [Header("経験値")]
    public Button xpUpButton;
    public TMP_Text xpCountText;
    public TMP_Text levelCountText;
    [Min(0)] public int xpPurchaseGoldCost = 4;
    [Min(0)] public int xpPerPurchase = 1;
    [Min(0)] public int xpSalePurchaseGoldCost = 1;
    [Min(0)] public int xpSalePerPurchase = 1;

    [Header("ステージ選択報酬")]
    [Min(0)] public int selectAExperienceReward = 1;
    [Min(0)] public int selectBGoldReward;
    [Min(0)] public int selectBExperienceReward;
    [Min(0)] public int selectCGoldReward = 6;
    [Min(0)] public int selectCExperienceReward = 2;
    [Min(0)] public int selectDGoldReward;
    [Min(0)] public int selectDExperienceReward;
    [Min(0)] public int selectDHealAmount = 5;

    [Header("Boss報酬（各ステージ4ラウンド目）")]
    [Min(0)] public int bossGoldReward = 8;
    [Min(0)] public int bossExperienceReward = 3;

    [Header("スコア")]
    public TMP_Text scoreCountText;

    private ShopSlot[] shopSlots;
    private int currentStageIndex;
    private StageManager.StageSelectionButton currentSelection = StageManager.StageSelectionButton.SelectA;
    private float scoreRefreshTimer;
    private const int TotalStages = 25;
    private const float ScoreRefreshInterval = 0.2f;
    private int currentLevel = 1;
    private int currentLevelExperience;
    public int CurrentLevel => currentLevel;

    [Header("レベルごとの必要経験値")]
    [Min(1)] public int[] experienceToNextLevel =
    {
        2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14
    };

    private int MaxLevel =>
        experienceToNextLevel != null
            ? experienceToNextLevel.Length + 1
            : 1;

    private static CharacterData[] pendingSelectedRoster;

    public static void SetPendingSelectedRoster(CharacterData[] roster)
    {
        pendingSelectedRoster = roster != null
            ? (CharacterData[])roster.Clone()
            : null;
    }

    private readonly Dictionary<CharacterData, int> characterPool =
        new Dictionary<CharacterData, int>();

    private readonly Dictionary<string, int> totalUnitMemberCounts =
        new Dictionary<string, int>();

    private readonly Dictionary<string, int> selectedUnitMemberCounts =
        new Dictionary<string, int>();


    // =========================================================
    // 初期化
    // =========================================================

    void Start()
    {
        ResolveScoreCountText();
        ResolveStageManagerReference();
        ResolveShopActionReferences();
        ResolveProbabilityCostImages();
        if (xpUpButton != null)
            xpUpButton.onClick.AddListener(PurchaseExperienceForCurrentSelection);
        if (stageManager != null)
            stageManager.SelectionCompleted += CompleteStageSelection;
        UpdateStageCountText();
        UpdateExperienceUI();

        if (pendingSelectedRoster != null)
        {
            characters = pendingSelectedRoster;
            pendingSelectedRoster = null;
        }

        if (!AssignRandomCosts())
        {
            return;
        }

        RefreshUnitCounts();
        InitializeCharacterPool();
        CreateShopSlots();

        BeginStageSelection();
        UpdateScoreCount();
    }

    void Update()
    {
        scoreRefreshTimer += Time.unscaledDeltaTime;
        if (scoreRefreshTimer < ScoreRefreshInterval)
            return;

        scoreRefreshTimer = 0f;
        UpdateScoreCount();
    }

    void OnDestroy()
    {
        if (xpUpButton != null)
            xpUpButton.onClick.RemoveListener(PurchaseExperienceForCurrentSelection);
        if (stageManager != null)
            stageManager.SelectionCompleted -= CompleteStageSelection;
    }

    private void ResolveStageManagerReference()
    {
        if (stageManager == null)
            stageManager = FindAnyObjectByType<StageManager>();

        if (stageManager == null)
            Debug.LogError("StageManagerが見つかりません。Inspectorで参照を設定してください。", this);
    }

    private void ResolveShopActionReferences()
    {
        if (rerollButtonObject == null)
        {
            Transform rerollButton = FindTransformIncludingInactive("RerollButton");
            if (rerollButton != null) rerollButtonObject = rerollButton.gameObject;
        }
        if (xpUpButton == null)
        {
            Transform xpButtonTransform = FindTransformIncludingInactive("XPbuyButton", "XPBuyButton");
            if (xpButtonTransform != null)
                xpUpButton = xpButtonTransform.GetComponent<Button>();
        }
        if (xpBuyButtonObject == null && xpUpButton != null)
            xpBuyButtonObject = xpUpButton.gameObject;

        if (rerollButtonObject == null)
            Debug.LogError("RerollButtonが見つかりません。", this);
        if (xpUpButton == null)
            Debug.LogError("XPBuyButtonが見つかりません。", this);

        if (xpUpButton != null)
        {
            Transform buyLabel = xpUpButton.transform.Find("Buy");
            Transform saleLabel = xpUpButton.transform.Find("Sale");
            if (buyLabel == null || saleLabel == null)
            {
                Debug.LogError("XPbuyButtonにはBuyとSaleのTextオブジェクトが必要です。", xpUpButton);
                return;
            }

            TMP_Text buyText = buyLabel.GetComponent<TMP_Text>();
            TMP_Text saleText = saleLabel.GetComponent<TMP_Text>();
            if (buyText == null || saleText == null)
            {
                Debug.LogError("XPbuyButtonのBuyとSaleにはTextMeshProコンポーネントが必要です。", xpUpButton);
                return;
            }

            buyText.text =
                "" +
                Mathf.Max(0, xpPurchaseGoldCost);
            saleText.text =
                "" +
                Mathf.Max(0, xpSalePurchaseGoldCost);
            buyLabel.gameObject.SetActive(true);
            saleLabel.gameObject.SetActive(false);
            xpUpButton.gameObject.SetActive(false);
        }
    }

    private Transform FindTransformIncludingInactive(params string[] objectNames)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate.gameObject.scene != gameObject.scene)
                continue;
            foreach (string objectName in objectNames)
            {
                if (candidate.name == objectName)
                    return candidate;
            }
        }

        return null;
    }

    private void BeginStageSelection()
    {
        ResetStageSelectionButtons();
        ClearShopOffers();

        if (stageManager == null)
        {
            Debug.LogError("StageManagerが設定されていないため、ステージを開始できません。", this);
            return;
        }

        stageManager.BeginStageSelection(currentStageIndex);
    }

    private void ResetStageSelectionButtons()
    {
        if (rerollButtonObject != null)
            rerollButtonObject.SetActive(true);
        if (xpBuyButtonObject != null)
            xpBuyButtonObject.SetActive(false);
    }

    private void CompleteStageSelection(StageManager.StageSelectionButton selectedButton)
    {
        currentSelection = selectedButton;
        if (selectedButton == StageManager.StageSelectionButton.SelectB)
        {
            SetExperienceButtonMode(true);
        }
        else
        {
            SetExperienceButtonMode(false);
        }

        if (selectedButton == StageManager.StageSelectionButton.SelectD &&
            player != null)
        {
            player.hp = Mathf.Min(
                player.InitialHp,
                player.hp + Mathf.Max(0, selectDHealAmount));
            player.UpdateHPUI();
        }

        GenerateShop();
    }

    private void ClearShopOffers()
    {
        if (shopSlots == null) return;

        foreach (ShopSlot slot in shopSlots)
        {
            if (slot == null || slot.IsEmpty()) continue;
            ReturnCharacterToPool(slot.characterData, 1);
            slot.ClearCharacter();
        }
    }

    public int AdvanceStageAfterBattle(bool beginStageSelection = true)
    {
        if (currentStageIndex >= TotalStages - 1)
            return 0;

        bool completedBossRound = currentStageIndex % 5 == 3;
        currentStageIndex++;
        UpdateStageCountText();
        int experienceReward = GetExperienceReward(completedBossRound);
        GainExperience(experienceReward);

        int goldReward = 0;
        if (player != null)
        {
            int baseGoldReward = GetGoldReward(completedBossRound);
            int interest = baseGoldReward > 0 ? CalculateInterest(player.gold) : 0;
            int emptyHandSlotBonus = baseGoldReward > 0
                ? CalculateEmptyHandSlotBonus()
                : 0;
            goldReward = baseGoldReward + interest + emptyHandSlotBonus;
            player.gold += goldReward;
            player.UpdateGoldUI();
        }
        else
        {
            Debug.LogWarning("PlayerDataが設定されていないため、ゴールドを付与できません。", this);
        }

        if (beginStageSelection)
            BeginStageSelection();

        return goldReward;
    }

    public void BeginNextStageSelectionAfterBattle()
    {
        if (currentStageIndex >= TotalStages - 1)
            return;

        BeginStageSelection();
    }

    int CalculateInterest(int currentGold)
    {
        if (currentGold <= 0)
            return 0;

        int threshold = Mathf.Max(1, goldPerInterest);
        int interestPerThreshold = Mathf.Max(0, interestGoldPerThreshold);
        int interestCap = Mathf.Max(0, maxInterestGold);
        int guaranteedInterest = Mathf.Min(
            currentGold / threshold * interestPerThreshold,
            interestCap);
        if (guaranteedInterest >= interestCap ||
            interestPerThreshold == 0)
            return guaranteedInterest;

        int remainingGold = currentGold % threshold;
        if (remainingGold > 0 &&
            Random.value < (float)remainingGold / threshold)
        {
            guaranteedInterest = Mathf.Min(
                guaranteedInterest + interestPerThreshold,
                interestCap);
        }

        return guaranteedInterest;
    }

    private int CalculateEmptyHandSlotBonus()
    {
        if (player.handManager == null)
        {
            Debug.LogError(
                "HandManager is required to calculate the empty HandSlot bonus.",
                this);
            return 0;
        }

        int slotsPerBonus = Mathf.Max(1, emptyHandSlotsPerBonusGold);
        int goldPerBonus = Mathf.Max(0, bonusGoldPerEmptyHandSlots);
        return player.handManager.CountEmptySlots() / slotsPerBonus * goldPerBonus;
    }

    void UpdateStageCountText()
    {
        UpdateCostProbabilityWidths();

        if (stageCountText == null)
            return;

        int stage = currentStageIndex / 5 + 1;
        int round = currentStageIndex % 5 + 1;
        stageCountText.text = stage + "-" + round;
    }

    private void ResolveProbabilityCostImages()
    {
        if (probabilityCostImages == null || probabilityCostImages.Length != 5)
            probabilityCostImages = new Image[5];

        for (int i = 0; i < probabilityCostImages.Length; i++)
        {
            if (probabilityCostImages[i] != null)
                continue;

            Transform imageTransform = FindTransformIncludingInactive("ProbabilityCost" + (i + 1));
            if (imageTransform != null)
                probabilityCostImages[i] = imageTransform.GetComponent<Image>();

            if (probabilityCostImages[i] == null)
                Debug.LogWarning("ProbabilityCost" + (i + 1) + " Image is not assigned or found.", this);
        }
    }

    private void UpdateCostProbabilityWidths()
    {
        if (probabilityCostImages == null)
            return;

        int stageIndex = Mathf.Clamp(currentStageIndex / 5, 0, 4);
        for (int i = 0; i < probabilityCostImages.Length; i++)
        {
            Image probabilityImage = probabilityCostImages[i];
            if (probabilityImage == null)
                continue;

            RectTransform rectTransform = probabilityImage.rectTransform;
            if (rectTransform == null)
                continue;

            int probabilityPercent = GetStageCostWeight(stageIndex, i + 1);
            rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                probabilityPercent * 5f);
        }
    }

    int GetRequiredScoreForCurrentStage()
    {
        if (requiredScoresByStage == null ||
            currentStageIndex < 0 ||
            currentStageIndex >= requiredScoresByStage.Length)
        {
            return 0;
        }

        return Mathf.Max(0, requiredScoresByStage[currentStageIndex]);
    }

    private void PurchaseExperienceAtRegularPrice()
    {
        PurchaseExperience(xpPurchaseGoldCost, xpPerPurchase);
    }

    private void PurchaseExperienceAtSalePrice()
    {
        PurchaseExperience(xpSalePurchaseGoldCost, xpSalePerPurchase);
    }

    private void PurchaseExperienceForCurrentSelection()
    {
        if (currentSelection == StageManager.StageSelectionButton.SelectB)
            PurchaseExperienceAtSalePrice();
        else
            PurchaseExperienceAtRegularPrice();
    }

    private void SetExperienceButtonMode(bool saleMode)
    {
        if (xpBuyButtonObject != null)
            xpBuyButtonObject.SetActive(true);
        if (xpUpButton == null)
            return;

        Transform buyLabel = xpUpButton.transform.Find("Buy");
        Transform saleLabel = xpUpButton.transform.Find("Sale");
        if (buyLabel == null || saleLabel == null)
        {
            Debug.LogError("XPbuyButtonにはBuyとSaleのTextオブジェクトが必要です。", xpUpButton);
            return;
        }

        buyLabel.gameObject.SetActive(!saleMode);
        saleLabel.gameObject.SetActive(saleMode);
    }

    private void PurchaseExperience(int cost, int experience)
    {
        int purchaseCost = Mathf.Max(0, cost);
        int experienceAmount = Mathf.Max(0, experience);
        if (currentLevel >= MaxLevel || player == null ||
            !player.CanBuy(purchaseCost))
        {
            return;
        }

        player.gold -= purchaseCost;
        player.UpdateGoldUI();
        GainExperience(experienceAmount);
    }

    private int GetExperienceReward(bool bossRound)
    {
        if (bossRound)
            return Mathf.Max(0, bossExperienceReward);

        switch (currentSelection)
        {
            case StageManager.StageSelectionButton.SelectA:
                return Mathf.Max(0, selectAExperienceReward);
            case StageManager.StageSelectionButton.SelectB:
                return Mathf.Max(0, selectBExperienceReward);
            case StageManager.StageSelectionButton.SelectC:
                return Mathf.Max(0, selectCExperienceReward);
            case StageManager.StageSelectionButton.SelectD:
                return Mathf.Max(0, selectDExperienceReward);
            default:
                return 0;
        }
    }

    private int GetGoldReward(bool bossRound)
    {
        if (bossRound)
            return Mathf.Max(0, bossGoldReward);

        switch (currentSelection)
        {
            case StageManager.StageSelectionButton.SelectA:
                return Mathf.Max(0, stageGoldReward);
            case StageManager.StageSelectionButton.SelectB:
                return Mathf.Max(0, selectBGoldReward);
            case StageManager.StageSelectionButton.SelectC:
                return Mathf.Max(0, selectCGoldReward);
            case StageManager.StageSelectionButton.SelectD:
                return Mathf.Max(0, selectDGoldReward);
            default:
                return 0;
        }
    }

    void GainExperience(int amount)
    {
        if (amount <= 0 || currentLevel >= MaxLevel)
            return;

        currentLevelExperience += amount;
        while (currentLevel < MaxLevel)
        {
            int requiredExperience = GetExperienceRequiredForCurrentLevel();
            if (currentLevelExperience < requiredExperience)
                break;

            currentLevelExperience -= requiredExperience;
            currentLevel++;
        }

        if (currentLevel >= MaxLevel)
            currentLevelExperience = 0;

        UpdateExperienceUI();
    }

    void UpdateExperienceUI()
    {
        if (levelCountText != null)
            levelCountText.text = "Lv." + currentLevel;

        if (xpCountText != null)
        {
            xpCountText.text = currentLevel >= MaxLevel
                ? "MAX"
                : currentLevelExperience + "/" + GetExperienceRequiredForCurrentLevel();
        }

        if (xpUpButton != null)
            xpUpButton.interactable = currentLevel < MaxLevel;
    }

    int GetExperienceRequiredForCurrentLevel()
    {
        if (experienceToNextLevel == null ||
            currentLevel < 1 ||
            currentLevel > experienceToNextLevel.Length)
        {
            return int.MaxValue;
        }

        return Mathf.Max(1, experienceToNextLevel[currentLevel - 1]);
    }

    void ResolveScoreCountText()
    {
        if (scoreCountText != null)
            return;

        GameObject scoreObject = GameObject.Find("ScoreCount");
        if (scoreObject != null)
            scoreCountText = scoreObject.GetComponent<TMP_Text>();
    }

    void UpdateScoreCount()
    {
        if (scoreCountText == null)
            ResolveScoreCountText();
        if (scoreCountText == null)
            return;

        float finalScore = CalculateBoardScore();
        string currentScore = finalScore.ToString("0.##", CultureInfo.InvariantCulture);
        scoreCountText.text =
            currentScore + "pt / " + GetRequiredScoreForCurrentStage() + "pt";
    }

    float CalculateBoardScore()
    {
        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        if (boardManager == null || boardManager.boardArea == null)
            return 0f;

        float baseScore = 0f;
        Dictionary<string, int> attributeCounts =
            new Dictionary<string, int>();
        BoardSlot[] boardSlots =
            boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true);

        foreach (BoardSlot slot in boardSlots)
        {
            if (slot == null || slot.IsEmpty())
                continue;

            OwnedCharacter character = slot.placedCharacter;
            baseScore += GetCharacterBaseScore(character.data.cost, character.star);

            if (character.data.attributes == null)
                continue;

            HashSet<string> uniqueAttributes = new HashSet<string>();
            foreach (string attribute in character.data.attributes)
            {
                if (string.IsNullOrWhiteSpace(attribute) ||
                    !uniqueAttributes.Add(attribute))
                {
                    continue;
                }

                if (!attributeCounts.ContainsKey(attribute))
                    attributeCounts[attribute] = 0;
                attributeCounts[attribute]++;
            }
        }

        int activeAttributeSynergies = 0;
        foreach (int count in attributeCounts.Values)
        {
            if (count >= 2)
                activeAttributeSynergies++;
        }

        BoardUnitListDisplay unitListDisplay =
            FindAnyObjectByType<BoardUnitListDisplay>();
        if (unitListDisplay != null)
        {
            unitListDisplay.RefreshBoardUnits();
            baseScore += unitListDisplay.ActiveUnitSynergyBonus;
        }

        return baseScore * (1f + activeAttributeSynergies * 0.1f);
    }

    float GetCharacterBaseScore(int cost, int star)
    {
        switch (cost)
        {
            case 1:
                switch (star)
                {
                    case 1: return 1f;
                    case 2: return 3f;
                    case 3: return 7f;
                }
                break;
            case 2:
                switch (star)
                {
                    case 1: return 1.5f;
                    case 2: return 3.5f;
                    case 3: return 7f;
                }
                break;
            case 3:
                switch (star)
                {
                    case 1: return 2f;
                    case 2: return 4f;
                    case 3: return 7f;
                }
                break;
            case 4:
                switch (star)
                {
                    case 1: return 3f;
                    case 2: return 4.5f;
                    case 3: return 7.5f;
                }
                break;
        }

        return 0f;
    }


    bool AssignRandomCosts()
    {
        if (characters == null ||
            characters.Length == 0)
        {
            Debug.LogError(
                "キャラクターデータが設定されていません。"
            );

            return false;
        }

        List<CharacterData> shuffledCharacters =
            new List<CharacterData>(characters);

        for (int i = 0; i < shuffledCharacters.Count; i++)
        {
            if (shuffledCharacters[i] == null ||
                shuffledCharacters.IndexOf(shuffledCharacters[i]) != i)
            {
                Debug.LogError(
                    "キャラクターデータに空欄または重複があります。"
                );

                return false;
            }
        }

        for (int i = 0; i < shuffledCharacters.Count; i++)
        {
            int randomIndex =
                Random.Range(i, shuffledCharacters.Count);

            CharacterData temporary =
                shuffledCharacters[i];

            shuffledCharacters[i] =
                shuffledCharacters[randomIndex];

            shuffledCharacters[randomIndex] =
                temporary;
        }

        if (charactersPerCost == null || charactersPerCost.Length != 5)
        {
            Debug.LogError("コスト別キャラクター配分は5要素（コスト1〜5）で設定してください。");
            return false;
        }

        int totalCharacters = 0;
        foreach (int count in charactersPerCost)
        {
            if (count < 0)
            {
                Debug.LogError("コスト別キャラクター配分に負数は設定できません。");
                return false;
            }

            totalCharacters += count;
        }

        if (totalCharacters <= 0)
        {
            Debug.LogError(
                "コスト別キャラクター配分の合計は1人以上にしてください。"
            );

            return false;
        }

        if (totalCharacters > shuffledCharacters.Count)
        {
            Debug.LogError(
                "コスト別キャラクター配分の合計が、設定されたキャラクター数を超えています。"
            );

            return false;
        }

        if (totalCharacters < shuffledCharacters.Count)
        {
            int excludedCharacterCount =
                shuffledCharacters.Count - totalCharacters;
            shuffledCharacters.RemoveRange(
                totalCharacters,
                excludedCharacterCount
            );

            Debug.Log(
                excludedCharacterCount +
                "体のキャラクターをランダムにゲーム対象から除外しました。"
            );
        }

        characters = shuffledCharacters.ToArray();

        int characterIndex = 0;
        for (int costIndex = 0;
             costIndex < charactersPerCost.Length;
             costIndex++)
        {
            for (int i = 0; i < charactersPerCost[costIndex]; i++)
            {
                shuffledCharacters[characterIndex].cost =
                    costIndex + 1;

                characterIndex++;
            }
        }

        Debug.Log("Inspectorで設定したコスト別人数で割り当てました。");

        return true;
    }


    // =========================================================
    // ショップスロット生成
    // =========================================================

    void CreateShopSlots()
    {
        if (shopArea == null || shopSlotPrefab == null)
        {
            Debug.LogError("Shop AreaとShop Slot Prefabを設定してください。", this);
            shopSlots = null;
            return;
        }

        if (shopSlots != null)
        {
            foreach (ShopSlot existingSlot in shopSlots)
            {
                if (existingSlot == null || existingSlot.IsEmpty())
                    continue;

                ReturnCharacterToPool(existingSlot.characterData, 1);
                existingSlot.ClearCharacter();
            }
        }

        foreach (Transform child in shopArea)
        {
            Destroy(child.gameObject);
        }

        int shopSize = GetShopSlotCountForCurrentStage();
        shopSlots =
            new ShopSlot[shopSize];

        for (int i = 0; i < shopSize; i++)
        {
            GameObject slotObject =
                Instantiate(
                    shopSlotPrefab,
                    shopArea
                );

            ShopSlot slot =
                slotObject.GetComponent<ShopSlot>();

            shopSlots[i] = slot;
        }
    }


    // =========================================================
    // ショップ生成
    // =========================================================

    public void GenerateShop()
    {
        if (stageManager != null && stageManager.IsAwaitingStageSelection)
            return;

        if (shopSlots == null ||
            shopSlots.Length != GetShopSlotCountForCurrentStage())
        {
            CreateShopSlots();
        }

        if (shopSlots == null)
        {
            return;
        }

        if (currentSelection == StageManager.StageSelectionButton.SelectB)
        {
            GenerateItemShop();
            return;
        }

        // Return any cards still offered before drawing replacements.
        foreach (ShopSlot slot in shopSlots)
        {
            if (slot == null || slot.IsEmpty())
            {
                continue;
            }

            ReturnCharacterToPool(slot.characterData, 1);
            slot.ClearCharacter();
        }

        for (int i = 0; i < shopSlots.Length; i++)
        {
            CharacterData character = DrawCharacterFromPool();
            shopSlots[i].SetCharacter(character);
        }
    }

    private void GenerateItemShop()
    {
        foreach (ShopSlot slot in shopSlots)
        {
            if (slot == null || slot.IsEmpty()) continue;
            if (slot.characterData != null)
                ReturnCharacterToPool(slot.characterData, 1);
            slot.ClearCharacter();
        }

        List<ItemData> available = new List<ItemData>();
        if (itemDataPool != null)
        {
            foreach (ItemData item in itemDataPool)
                if (item != null && !available.Contains(item)) available.Add(item);
        }

        for (int i = available.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            ItemData temp = available[i];
            available[i] = available[j];
            available[j] = temp;
        }

        int count = Mathf.Min(shopSlots.Length, available.Count);
        for (int i = 0; i < count; i++)
            shopSlots[i].SetItem(available[i]);

        if (count == 0)
            Debug.LogWarning("SelectB用ItemDataがありません。ShopManagerのItem Data Poolを設定してください。", this);
    }
    private int GetShopSlotCountForCurrentStage()
    {
        int stageIndex = Mathf.Clamp(currentStageIndex / 5, 0, 4);
        if (shopSlotsByStage == null ||
            stageIndex >= shopSlotsByStage.Length)
        {
            return 4;
        }

        return Mathf.Max(0, shopSlotsByStage[stageIndex]);
    }

    public void RefreshUnitCounts()
    {
        totalUnitMemberCounts.Clear();
        selectedUnitMemberCounts.Clear();

        CharacterData[] roster =
            fullCharacterRoster != null && fullCharacterRoster.Length > 0
                ? fullCharacterRoster
                : characters;

        AddUnitCounts(roster, totalUnitMemberCounts);
        AddUnitCounts(characters, selectedUnitMemberCounts);
    }

    void AddUnitCounts(
        CharacterData[] source,
        Dictionary<string, int> destination)
    {
        if (source == null)
        {
            return;
        }

        foreach (CharacterData character in source)
        {
            if (character == null || character.units == null)
            {
                continue;
            }

            HashSet<string> uniqueUnits = new HashSet<string>();
            foreach (string unitName in character.units)
            {
                if (string.IsNullOrWhiteSpace(unitName) ||
                    !uniqueUnits.Add(unitName))
                {
                    continue;
                }

                if (!destination.ContainsKey(unitName))
                {
                    destination[unitName] = 0;
                }

                destination[unitName]++;
            }
        }
    }

    public int GetTotalUnitMemberCount(string unitName)
    {
        return totalUnitMemberCounts.TryGetValue(unitName, out int count)
            ? count
            : 0;
    }

    public int GetSelectedUnitMemberCount(string unitName)
    {
        return selectedUnitMemberCounts.TryGetValue(unitName, out int count)
            ? count
            : 0;
    }

    public bool IsUnitComplete(string unitName)
    {
        int totalCount = GetTotalUnitMemberCount(unitName);
        return totalCount > 0 &&
               GetSelectedUnitMemberCount(unitName) == totalCount;
    }


    void InitializeCharacterPool()
    {
        characterPool.Clear();

        foreach (CharacterData character in characters)
        {
            characterPool[character] = GetInitialPoolCount(character.cost);
        }
    }

    int GetInitialPoolCount(int cost)
    {
        if (initialPoolCountsByCost == null ||
            cost < 1 ||
            cost > initialPoolCountsByCost.Length)
            return 0;

        return Mathf.Max(0, initialPoolCountsByCost[cost - 1]);
    }

    public void ReturnCharacterToPool(
        CharacterData character,
        int copies)
    {
        if (character == null || copies <= 0 ||
            !characterPool.ContainsKey(character))
        {
            return;
        }

        int maximum = GetInitialPoolCount(character.cost);
        characterPool[character] = Mathf.Min(
            maximum,
            characterPool[character] + copies
        );
    }

    CharacterData DrawCharacterFromPool()
    {
        int currentStage = Mathf.Clamp(currentStageIndex / 5, 0, 4);
        int availableWeight = 0;

        for (int cost = 1; cost <= 5; cost++)
        {
            if (GetStageCostWeight(currentStage, cost) > 0 &&
                HasAvailableCharacterAtCost(cost))
            {
                availableWeight += GetStageCostWeight(currentStage, cost);
            }
        }

        if (availableWeight <= 0)
        {
            return null;
        }

        // Empty cost tiers are excluded, so only this slot is redrawn
        // using the original weight ratios among tiers that still have stock.
        int roll = Random.Range(0, availableWeight);
        int selectedCost = 0;

        for (int cost = 1; cost <= 5; cost++)
        {
            int weight = GetStageCostWeight(currentStage, cost);
            if (weight <= 0 || !HasAvailableCharacterAtCost(cost))
            {
                continue;
            }

            if (roll < weight)
            {
                selectedCost = cost;
                break;
            }

            roll -= weight;
        }

        if (selectedCost == 0)
        {
            return null;
        }

        int totalCopies = 0;
        foreach (CharacterData character in characters)
        {
            if (character.cost == selectedCost)
            {
                totalCopies += characterPool[character];
            }
        }

        if (totalCopies <= 0)
        {
            return null;
        }

        int copyRoll = Random.Range(0, totalCopies);
        foreach (CharacterData character in characters)
        {
            if (character.cost != selectedCost)
            {
                continue;
            }

            int copies = characterPool[character];
            if (copyRoll < copies)
            {
                characterPool[character]--;
                return character;
            }

            copyRoll -= copies;
        }

        return null;
    }

    int GetStageCostWeight(int stageIndex, int cost)
    {
        if (stageCostAppearanceRates == null ||
            stageIndex < 0 ||
            stageIndex >= stageCostAppearanceRates.Length ||
            stageCostAppearanceRates[stageIndex] == null)
        {
            return 0;
        }

        return Mathf.Clamp(stageCostAppearanceRates[stageIndex].GetWeight(cost), 0, 100);
    }

    bool HasAvailableCharacterAtCost(int cost)
    {
        foreach (CharacterData character in characters)
        {
            if (character.cost == cost &&
                characterPool.TryGetValue(character, out int copies) &&
                copies > 0)
            {
                return true;
            }
        }

        return false;
    }


    // =========================================================
    // リロール
    // =========================================================

    public void RerollShop()
    {
        if (stageManager != null && stageManager.IsAwaitingStageSelection)
            return;

        int cost = Mathf.Max(0, rerollCost);

        if (player.gold < cost)
        {
            Debug.Log(
                "ショップ更新に必要なゴールドがありません！"
            );

            return;
        }

        player.gold -= cost;

        player.UpdateGoldUI();

        GenerateShop();

        Debug.Log(
            "ショップを更新しました。残りゴールド：" +
            player.gold
        );
    }
}
