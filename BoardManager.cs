using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BoardManager : MonoBehaviour
{
    [Header("盤面設定")]
    public GameObject boardSlotPrefab;
    public Transform boardArea;

    public int columns = 8;
    public int rows = 3;

    [Header("プレイヤーデータ")]
    public PlayerData playerData;

    [Header("キャラクター詳細")]
    public CharacterDetailPanel characterDetailPanel;
    public ItemDetailPanel itemDetailPanel;


    // =========================================================
    // 選択状態
    // =========================================================

    public enum SelectionType
    {
        None,
        Hand,
        Board,
        Shop
    }

    [Header("現在の選択")]
    public SelectionType selectionType =
        SelectionType.None;
    public bool IsShowingCharacterDetailOnly { get; private set; }
    public bool IsSellButtonSuppressedAfterDrag { get; private set; }

    public void NotifyCharacterDragStarted()
    {
        IsSellButtonSuppressedAfterDrag = false;
    }

    public void NotifyCharacterDragEnded()
    {
        IsSellButtonSuppressedAfterDrag = true;
    }

    // 手持ちから選択中
    public HandSlot selectedHandSlot;

    // 盤面から選択中
    public BoardSlot selectedBoardSlot;

    // ショップから選択中
    public CharacterData selectedShopCharacter;
    public ShopCard selectedShopCard;
    public ItemData selectedShopItem;
    public ShopSlot selectedShopItemSlot;


    // =========================================================
    // 盤面生成
    // =========================================================

    void Start()
    {
        CreateBoard();
    }

    void CreateBoard()
    {
        foreach (Transform child in boardArea)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < columns * rows; i++)
        {
            GameObject slotObject =
                Instantiate(
                    boardSlotPrefab,
                    boardArea
                );

            BoardSlot slot =
                slotObject.GetComponent<BoardSlot>();
        }
    }


    // =========================================================
    // 手持ちキャラクター
    // =========================================================

    // 手持ちキャラクターを選択
    public void SelectHandCharacter(
        OwnedCharacter character,
        HandSlot handSlot)
    {
        if (character == null ||
            handSlot == null)
        {
            return;
        }

        IsSellButtonSuppressedAfterDrag = false;

        // すでに手持ちキャラクターを選択中
        if (selectionType ==
            SelectionType.Hand)
        {
            // 同じスロットをクリック
            // → 選択解除
            if (selectedHandSlot == handSlot)
            {
                ClearSelection();

                Debug.Log(
                    "手持ちキャラクターの選択を解除しました。"
                );

                return;
            }

            // 別の手持ちスロット
            // → 移動または交換
            MoveHandCharacter(
                selectedHandSlot,
                handSlot
            );

            return;
        }

        // 新しく手持ちキャラクターを選択
        ClearSelection();

        selectionType =
            SelectionType.Hand;

        selectedHandSlot =
            handSlot;
        
        Debug.Log(
            character.data.characterName +
            " を手持ちから選択しました。"
        );
    }

    public void ShowHandCharacterDetail(
    OwnedCharacter character,
    HandSlot handSlot)
    {
        if (character == null ||
            handSlot == null)
        {
            return;
        }

        if (characterDetailPanel != null &&
            characterDetailPanel.TryToggleUnitAttributeFor(character))
            return;

        HideItemDetailPanel();
        ClearSelection();

        selectionType =
            SelectionType.Hand;
        IsShowingCharacterDetailOnly = true;

        selectedHandSlot =
            handSlot;

        UpdateCharacterDetail();

        Debug.Log(
            character.data.characterName +
            " を右クリックで選択しました。"
        );
    }


    // 手持ちキャラクターを移動・交換
    public void MoveHandCharacter(HandSlot fromSlot, HandSlot toSlot, bool clearCharacterDetail = true)
    {
        if (fromSlot == null || toSlot == null || fromSlot == toSlot || fromSlot.IsEmpty())
            return;

        OwnedCharacter fromCharacter = fromSlot.ownedCharacter;
        if (fromCharacter != null && fromCharacter.data == null) fromCharacter = null;
        ItemData fromItem = fromSlot.itemData;
        OwnedCharacter toCharacter = toSlot.ownedCharacter;
        if (toCharacter != null && toCharacter.data == null) toCharacter = null;
        ItemData toItem = toSlot.itemData;

        fromSlot.ClearContents();
        toSlot.ClearContents();

        if (toCharacter != null) fromSlot.SetCharacter(toCharacter);
        else if (toItem != null) fromSlot.SetItem(toItem);
        if (fromCharacter != null) toSlot.SetCharacter(fromCharacter);
        else if (fromItem != null) toSlot.SetItem(fromItem);

        ClearSelection(clearCharacterDetail);
    }


    // 手持ち → 盤面
    // 空きなら移動、キャラクターがいれば交換
    public void PlaceSelectedHandCharacter(
        BoardSlot slot,
        bool clearCharacterDetail = true)
    {
        if (selectedHandSlot == null)
        {
            return;
        }

        if (slot == null)
        {
            return;
        }

        if (!slot.acceptsPlayerCharacters)
            return;

        if (selectedHandSlot.IsEmpty() || selectedHandSlot.itemData != null)
        {
            return;
        }

        // 手持ちのキャラクター
        OwnedCharacter handCharacter =
            selectedHandSlot.ownedCharacter;

        if (HasSameNameOnBoard(
            handCharacter.data,
            slot))
        {
            Debug.Log(
                "盤面には同じ名前のキャラクターを複数配置できません。"
            );

            return;
        }

        // -----------------------------------------
        // 盤面が空いている場合
        // -----------------------------------------

        if (slot.IsEmpty())
        {
            if (!CanAddCharacterToBoard())
                return;

            OwnedCharacter character =
                selectedHandSlot.RemoveCharacter();

            slot.PlaceCharacter(
                character
            );

            Debug.Log(
                character.data.characterName +
                " を手持ちから盤面へ配置しました！"
            );

            ClearSelection(clearCharacterDetail);

            return;
        }

        // -----------------------------------------
        // 盤面にもキャラクターがいる場合
        // → 交換
        // -----------------------------------------

        OwnedCharacter boardCharacter =
            slot.placedCharacter;

        // 盤面 ← 手持ち
        slot.SetCharacterWithoutDataChange(
            handCharacter
        );

        // 手持ち ← 盤面
        selectedHandSlot.SetCharacter(
            boardCharacter
        );

        Debug.Log(
            handCharacter.data.characterName +
            " と " +
            boardCharacter.data.characterName +
            " を手持ちと盤面で交換しました！"
        );

        ClearSelection(clearCharacterDetail);
    }


    // =========================================================
    // 盤面キャラクター
    // =========================================================

    // 盤面キャラクターを選択
    public void SelectBoardCharacter(
        BoardSlot slot)
    {
        if (slot == null ||
            slot.IsEmpty())
        {
            return;
        }

        IsSellButtonSuppressedAfterDrag = false;

        // すでに盤面キャラクターを選択中
        if (selectionType ==
            SelectionType.Board)
        {
            // 同じキャラクター
            // → 選択解除
            if (selectedBoardSlot == slot)
            {
                ClearSelection();

                Debug.Log(
                    "盤面キャラクターの選択を解除しました。"
                );

                return;
            }

            // 別の盤面キャラクター
            // → 移動または交換
            MoveCharacter(
                selectedBoardSlot,
                slot
            );

            return;
        }

        // 新しく盤面キャラクターを選択
        ClearSelection();

        selectionType =
            SelectionType.Board;

        selectedBoardSlot =
            slot;
       
        Debug.Log(
            slot.placedCharacter.data.characterName +
            " を選択しました。"
        );
    }

    public void ShowBoardCharacterDetail(
    BoardSlot slot)
    {
        if (slot == null ||
            slot.IsEmpty())
        {
            return;
        }

        if (characterDetailPanel != null &&
            characterDetailPanel.TryToggleUnitAttributeFor(slot.placedCharacter))
            return;

        HideItemDetailPanel();
        ClearSelection();

        selectionType =
            SelectionType.Board;
        IsShowingCharacterDetailOnly = true;

        selectedBoardSlot =
            slot;

        UpdateCharacterDetail();

        Debug.Log(
            slot.placedCharacter.data.characterName +
            " を右クリックで選択しました。"
        );
    }


    // 盤面キャラクターを移動
    // または交換
    public void MoveCharacter(BoardSlot fromSlot, BoardSlot toSlot, bool clearCharacterDetail = true)
    {
        if (fromSlot == null || toSlot == null ||
            !fromSlot.acceptsPlayerCharacters || !toSlot.acceptsPlayerCharacters ||
            fromSlot == toSlot || fromSlot.IsEmpty())
            return;

        if (toSlot.IsEmpty())
        {
            OwnedCharacter character = fromSlot.placedCharacter;
            fromSlot.ClearCharacterVisual();
            toSlot.PlaceCharacter(character);
            ClearSelection(clearCharacterDetail);
            return;
        }
        SwapCharacters(fromSlot, toSlot, clearCharacterDetail);
    }


    public bool CanAddCharacterToBoard(bool showWarning = true)
    {
        ShopManager shopManager =
            FindAnyObjectByType<ShopManager>();

        if (shopManager == null || boardArea == null)
            return true;

        int boardCharacterCount = 0;
        foreach (BoardSlot slot in boardArea.GetComponentsInChildren<BoardSlot>(true))
        {
            if (slot != null && !slot.IsEmpty())
                boardCharacterCount++;
        }

        if (boardCharacterCount < shopManager.CurrentLevel)
            return true;

        if (showWarning)
        {
            Debug.Log(
                "レベル" + shopManager.CurrentLevel +
                "の盤面配置上限に達しています。"
            );
        }

        return false;
    }

    bool HasSameNameOnBoard(
        CharacterData characterData,
        BoardSlot excludedSlot = null)
    {
        if (characterData == null ||
            boardArea == null)
        {
            return false;
        }

        BoardSlot[] boardSlots =
            boardArea.GetComponentsInChildren<BoardSlot>();

        foreach (BoardSlot slot in boardSlots)
        {
            if (slot == null ||
                slot == excludedSlot ||
                slot.IsEmpty() ||
                slot.placedCharacter.data == null)
            {
                continue;
            }

            if (slot.placedCharacter.data.characterName ==
                characterData.characterName)
            {
                return true;
            }
        }

        return false;
    }


    // 盤面キャラクターを交換
    public void SwapCharacters(
        BoardSlot slotA,
        BoardSlot slotB,
        bool clearCharacterDetail = true)
    {
        if (slotA == null ||
            slotB == null)
        {
            return;
        }

        if (slotA.IsEmpty() ||
            slotB.IsEmpty())
        {
            return;
        }

        OwnedCharacter characterA =
            slotA.placedCharacter;

        OwnedCharacter characterB =
            slotB.placedCharacter;

        slotA.SetCharacterWithoutDataChange(
            characterB
        );

        slotB.SetCharacterWithoutDataChange(
            characterA
        );

        Debug.Log(
            characterA.data.characterName +
            " と " +
            characterB.data.characterName +
            " を交換しました！"
        );

        ClearSelection(clearCharacterDetail);
    }


    // 盤面 → 手持ち
    // 空きなら移動、キャラクターがいれば交換
    public void ReturnSelectedBoardCharacterToHand(
        HandSlot handSlot,
        bool clearCharacterDetail = true)
    {
        if (selectedBoardSlot == null)
        {
            return;
        }

        if (handSlot == null || handSlot.itemData != null)
        {
            return;
        }

        if (selectedBoardSlot.IsEmpty())
        {
            return;
        }

        // 盤面のキャラクター
        OwnedCharacter boardCharacter =
            selectedBoardSlot.placedCharacter;

        // -----------------------------------------
        // 手持ちが空いている場合
        // -----------------------------------------

        if (handSlot.IsEmpty())
        {
            handSlot.SetCharacter(
                boardCharacter
            );

            selectedBoardSlot.ClearCharacterVisual();

            Debug.Log(
                boardCharacter.data.characterName +
                " を盤面から手持ちへ移動しました！"
            );

            ClearSelection(clearCharacterDetail);

            return;
        }

        // -----------------------------------------
        // 手持ちにもキャラクターがいる場合
        // → 交換
        // -----------------------------------------

        OwnedCharacter handCharacter =
            handSlot.ownedCharacter;

        if (HasSameNameOnBoard(
            handCharacter.data,
            selectedBoardSlot))
        {
            Debug.Log(
                "盤面には同じ名前のキャラクターを複数配置できません。"
            );

            return;
        }

        // 手持ち ← 盤面
        handSlot.SetCharacter(
            boardCharacter
        );

        // 盤面 ← 手持ち
        selectedBoardSlot.SetCharacterWithoutDataChange(
            handCharacter
        );

        Debug.Log(
            boardCharacter.data.characterName +
            " と " +
            handCharacter.data.characterName +
            " を盤面と手持ちで交換しました！"
        );

        ClearSelection(clearCharacterDetail);
    }


    // =========================================================
    // ショップキャラクター
    // =========================================================

    // ショップキャラクターを選択
    public void SelectShopCharacter(
        CharacterData character,
        ShopCard shopCard)
    {
        if (character == null ||
            shopCard == null)
        {
            return;
        }

        HideItemDetailPanel();
        ClearSelection();

        selectionType =
            SelectionType.Shop;

        selectedShopCharacter =
            character;

        selectedShopCard =
            shopCard;
        
        Debug.Log(
            character.characterName +
            " をショップから選択しました。"
        );
    }

    public void SelectShopItem(ItemData item, ShopSlot sourceSlot, bool clearCharacterDetail = true)
    {
        if (item == null || sourceSlot == null) return;
        ClearSelection(clearCharacterDetail);
        selectionType = SelectionType.Shop;
        selectedShopItem = item;
        selectedShopItemSlot = sourceSlot;
    }

    public void BuySelectedShopItemToHand(HandSlot handSlot, bool clearCharacterDetail = true)
    {
        if (selectedShopItem == null || selectedShopItemSlot == null ||
            handSlot == null || !handSlot.IsEmpty())
            return;

        PlayerData player = FindAnyObjectByType<PlayerData>();
        if (player == null)
        {
            Debug.LogError("PlayerDataが見つかりません。", this);
            return;
        }
        if (!player.CanBuy(selectedShopItem.purchasePrice))
        {
            Debug.Log("コインが足りません。", this);
            return;
        }

        ItemData item = selectedShopItem;
        ShopSlot sourceSlot = selectedShopItemSlot;
        player.gold -= item.purchasePrice;
        player.UpdateGoldUI();
        handSlot.SetItem(item);
        sourceSlot.ClearCharacter();
        ClearSelection(clearCharacterDetail);
    }

    public HandSlot FindEmptyHandSlot()
    {
        HandManager manager = FindAnyObjectByType<HandManager>();
        return manager != null ? manager.FindEmptySlot() : null;
    }

    public bool BuySelectedShopItemToFirstEmptyHandSlot(bool clearCharacterDetail = true)
    {
        HandSlot slot = FindEmptyHandSlot();
        if (slot == null || selectedShopItem == null || selectedShopItemSlot == null) return false;
        BuySelectedShopItemToHand(slot, clearCharacterDetail);
        return slot.itemData != null;
    }

    public bool UseHandItemOnBoard(HandSlot handSlot, BoardSlot dropSlot, bool clearCharacterDetail = true)
    {
        if (handSlot == null || handSlot.itemData == null || dropSlot == null) return false;
        ItemData item = handSlot.itemData;
        if (!TryResolveItemTarget(item.target, out string targetType)) return false;
        if (!TryResolveItemDuration(item.duration, out bool immediateEffect)) return false;
        bool playerBoard = dropSlot.acceptsPlayerCharacters && boardArea != null && dropSlot.transform.IsChildOf(boardArea);
        EnemyManager enemyManager = FindAnyObjectByType<EnemyManager>();
        bool enemyBoard = enemyManager != null && enemyManager.boardArea != null && dropSlot.transform.IsChildOf(enemyManager.boardArea);
        if (targetType == "Ally" && (!playerBoard || dropSlot.IsEmpty() || dropSlot.placedCharacter == null)) return false;
        if (targetType == "Allies" && !playerBoard) return false;
        if ((targetType == "Enemy" || targetType == "Enemies") && !enemyBoard) return false;
        if (targetType == "Enemy" && dropSlot.enemyData == null) return false;
        if (targetType == "Player" && !playerBoard) return false;
        if (item.effects == null || item.effects.Length == 0 || item.effectValues == null || item.effectValues.Length != item.effects.Length)
        {
            Debug.LogWarning("ItemDataのEffectsとEffectValuesの数が一致しません: " + item.itemName, item);
            return false;
        }
        foreach (string effect in item.effects)
            if (!IsItemEffectSupported(targetType, effect)) return false;
        if (targetType == "Allies" && !boardArea.GetComponentsInChildren<BoardSlot>(true).Any(s => s != null && !s.IsEmpty())) return false;
        if (targetType == "Enemies" && (enemyManager == null || enemyManager.enemyDataBySlot == null || !enemyManager.enemyDataBySlot.Any(e => e != null))) return false;
        if (targetType == "Player" && FindAnyObjectByType<BattleManager>() == null) return false;

        bool applied = false;
        for (int i = 0; i < item.effects.Length; i++)
            applied |= ApplyItemEffectToTarget(targetType, dropSlot, item.effects[i], item.effectValues[i], enemyManager, immediateEffect);
        if (!applied) return false;
        RefreshVisibleDetailPanels();
        handSlot.RemoveItem();
        ClearSelection(clearCharacterDetail);
        return true;
    }

    private bool TryResolveItemDuration(string duration, out bool immediateEffect)
    {
        string value = (duration ?? string.Empty).Trim();
        if (value == "このバトル中")
        {
            immediateEffect = false;
            return true;
        }
        if (value == "即時効果")
        {
            immediateEffect = true;
            return true;
        }

        immediateEffect = false;
        Debug.LogWarning("未対応のItem持続期間です: " + duration, this);
        return false;
    }

    private bool TryResolveItemTarget(string target, out string resolved)
    {
        string value = (target ?? string.Empty).Trim();
        if (value == "味方" || value == "味方1体" || value == "Ally" || value == "OneAlly") resolved = "Ally";
        else if (value == "全ての味方" || value == "すべての味方" || value == "Allies" || value == "AllAllies") resolved = "Allies";
        else if (value == "敵" || value == "敵1体" || value == "Enemy" || value == "OneEnemy") resolved = "Enemy";
        else if (value == "全ての敵" || value == "すべての敵" || value == "Enemies" || value == "AllEnemies") resolved = "Enemies";
        else if (value == "プレイヤー" || value == "Player") resolved = "Player";
        else { resolved = null; Debug.LogWarning("未対応のItem対象です: " + target, this); }
        return resolved != null;
    }

    private bool IsItemEffectSupported(string target, string effect)
    {
        if (target == "Ally") return effect == "Attack" || effect == "Def" || effect == "Star";
        if (target == "Allies") return effect == "Attack" || effect == "Def";
        if (target == "Enemy") return effect == "Attack" || effect == "Def" || effect == "Damage" || effect == "Gap";
        if (target == "Enemies") return effect == "Attack" || effect == "Def" || effect == "Damage";
        if (target == "Player") return effect == "Damage" || effect == "Block";
        return false;
    }

    private bool ApplyItemEffectToTarget(string target, BoardSlot dropSlot, string effect, int value, EnemyManager enemies, bool immediateEffect)
    {
        if (target == "Ally") return ApplyItemEffectToCharacter(dropSlot.placedCharacter, effect, value, dropSlot, immediateEffect);
        if (target == "Allies")
        {
            bool any = false;
            foreach (BoardSlot slot in boardArea.GetComponentsInChildren<BoardSlot>(true))
                if (slot != null && !slot.IsEmpty()) any |= ApplyItemEffectToCharacter(slot.placedCharacter, effect, value, slot, immediateEffect);
            return any;
        }
        if (target == "Enemy") return enemies != null && enemies.TryApplyItemEffect(dropSlot, effect, value, immediateEffect);
        if (target == "Enemies") return enemies != null && enemies.ApplyItemEffectToAllEnemies(effect, value, immediateEffect);
        if (target == "Player")
        {
            BattleManager battle = FindAnyObjectByType<BattleManager>();
            return battle != null && battle.ApplyPlayerItemEffect(effect, value, immediateEffect);
        }
        return false;
    }

    private bool ApplyItemEffectToCharacter(OwnedCharacter character, string effect, int value, BoardSlot slot, bool immediateEffect)
    {
        if (character == null || character.data == null) return false;
        if (effect == "Attack")
        {
            if (immediateEffect) character.attack += value;
            else character.battleAttackBonus += value;
        }
        else if (effect == "Def")
        {
            if (immediateEffect) character.defense += value;
            else character.battleDefenseBonus += value;
        }
        else if (effect == "Star")
        {
            if (value < 1 || value > 3)
            {
                Debug.LogWarning(
                    "ItemのStar効果値は1から3で指定してください: " + value,
                    character.data);
                return false;
            }

            if (value != character.star &&
                !(immediateEffect
                    ? character.TrySetStar(value, CharacterStatSettings.FindForCurrentScene())
                    : character.TrySetTemporaryStar(value, CharacterStatSettings.FindForCurrentScene())))
            {
                return false;
            }

            slot.SetCharacterWithoutDataChange(character);
            return true;
        }
        else return false;
        BoardCharacter display = slot.GetComponentInChildren<BoardCharacter>(true);
        if (display != null) display.RefreshOwnedStats();
        RefreshVisibleDetailPanels();
        return true;
    }

    public void SelectHandItem(HandSlot handSlot)
    {
        if (handSlot == null || handSlot.itemData == null) return;
        IsSellButtonSuppressedAfterDrag = false;
        ClearSelection();
        selectionType = SelectionType.Hand;
        selectedHandSlot = handSlot;
    }
    public void ShowShopCharacterDetail(
    CharacterData character,
    ShopCard shopCard)
    {
        if (character == null ||
            shopCard == null)
        {
            return;
        }

        if (characterDetailPanel != null &&
            characterDetailPanel.TryToggleUnitAttributeFor(character))
            return;

        HideItemDetailPanel();
        ClearSelection();

        selectionType =
            SelectionType.Shop;
        IsShowingCharacterDetailOnly = true;

        selectedShopCharacter =
            character;

        selectedShopCard =
            shopCard;

        UpdateCharacterDetail();

        Debug.Log(
            character.characterName +
            " を右クリックで選択しました。"
        );
    }

    // ショップ → 手持ち
    public void BuySelectedShopCharacterToHand(
    HandSlot handSlot,
    bool clearCharacterDetail = true)
    {
        if (selectedShopCharacter == null ||
            handSlot == null)
        {
            return;
        }

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            Debug.LogError(
                "PlayerDataが見つかりません！"
            );

            return;
        }

        if (!player.CanBuy(
            selectedShopCharacter.cost))
        {
            Debug.Log(
                "コインが足りません！"
            );

            return;
        }

        CharacterStatSettings statSettings =
            CharacterStatSettings.FindForCurrentScene();
        if (statSettings == null ||
            !statSettings.TryGetStatRanges(
                selectedShopCharacter.cost,
                1,
                out _,
                out _,
                out _,
                out _))
        {
            Debug.LogError(
                "Character stats are not configured for Cost " +
                selectedShopCharacter.cost + ", Star 1.",
                this);
            return;
        }

        // =========================================
        // まず盤面・手持ちに同じキャラがいるか確認
        // =========================================

        if (TryCombineShopCharacter(
            selectedShopCharacter))
        {
            player.gold -=
                selectedShopCharacter.cost;

            player.UpdateGoldUI();

            ShopSlot combinedShopSlot =
                selectedShopCard.GetShopSlot();

            if (combinedShopSlot != null)
            {
                combinedShopSlot.ClearCharacter();
            }

            Debug.Log(
                selectedShopCharacter.characterName +
                " を購入して合成しました！"
            );

            ClearSelection(clearCharacterDetail);

            return;
        }

        // =========================================
        // 同じキャラがいない
        // → 指定された手持ちへ通常購入
        // =========================================

        if (!handSlot.IsEmpty())
        {
            return;
        }

        player.gold -=
            selectedShopCharacter.cost;

        player.UpdateGoldUI();

        OwnedCharacter ownedCharacter =
            new OwnedCharacter(
                selectedShopCharacter,
                statSettings
            );

        player.ownedCharacters.Add(
            ownedCharacter
        );

        handSlot.SetCharacter(
            ownedCharacter
        );

        Debug.Log(
            selectedShopCharacter.characterName +
            " を購入しました！"
        );

        ShopSlot shopSlot =
            selectedShopCard.GetShopSlot();

        if (shopSlot != null)
        {
            shopSlot.ClearCharacter();
        }

        ClearSelection(clearCharacterDetail);
    }


    // ショップ → 直接盤面
    public void BuySelectedShopCharacterToBoard(
     BoardSlot slot,
     bool clearCharacterDetail = true)
    {
        if (selectedShopCharacter == null ||
            slot == null)
        {
            return;
        }

        if (!slot.acceptsPlayerCharacters)
            return;

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            Debug.LogError(
                "PlayerDataが見つかりません！"
            );

            return;
        }

        if (!player.CanBuy(
            selectedShopCharacter.cost))
        {
            Debug.Log(
                "コインが足りません！"
            );

            return;
        }

        CharacterStatSettings statSettings =
            CharacterStatSettings.FindForCurrentScene();
        if (statSettings == null ||
            !statSettings.TryGetStatRanges(
                selectedShopCharacter.cost,
                1,
                out _,
                out _,
                out _,
                out _))
        {
            Debug.LogError(
                "Character stats are not configured for Cost " +
                selectedShopCharacter.cost + ", Star 1.",
                this);
            return;
        }

        // =========================================
        // まず盤面・手持ちに同じキャラがいるか確認
        // =========================================

        if (TryCombineShopCharacter(
            selectedShopCharacter,
            slot))
        {
            player.gold -=
                selectedShopCharacter.cost;

            player.UpdateGoldUI();

            ShopSlot combinedShopSlot =
                selectedShopCard.GetShopSlot();

            if (combinedShopSlot != null)
            {
                combinedShopSlot.ClearCharacter();
            }

            Debug.Log(
                selectedShopCharacter.characterName +
                " を購入して合成しました！"
            );

            ClearSelection(clearCharacterDetail);

            return;
        }

        if (HasSameNameOnBoard(
            selectedShopCharacter,
            slot))
        {
            Debug.Log(
                "盤面には同じ名前のキャラクターを複数配置できません。"
            );

            return;
        }

        // =========================================
        // 同じキャラがいない
        // → 指定された盤面へ通常購入
        // =========================================

        if (!slot.IsEmpty())
        {
            Debug.Log(
                "その盤面スロットにはキャラクターがいます。"
            );

            return;
        }

        if (!CanAddCharacterToBoard())
            return;

        player.gold -=
            selectedShopCharacter.cost;

        player.UpdateGoldUI();

        OwnedCharacter ownedCharacter =
            new OwnedCharacter(
                selectedShopCharacter,
                statSettings
            );

        player.ownedCharacters.Add(
            ownedCharacter
        );

        slot.PlaceCharacter(
            ownedCharacter
        );

        Debug.Log(
            selectedShopCharacter.characterName +
            " を購入して盤面に配置しました！"
        );

        ShopSlot shopSlot =
            selectedShopCard.GetShopSlot();

        if (shopSlot != null)
        {
            shopSlot.ClearCharacter();
        }

        ClearSelection(clearCharacterDetail);
    }


    // =========================================================
    // 選択解除
    // =========================================================

    public void ClearSelection(bool clearCharacterDetail = true)
    {
        IsShowingCharacterDetailOnly = false;
        selectionType =
            SelectionType.None;

        selectedHandSlot = null;

        selectedBoardSlot = null;

        selectedShopCharacter = null;

        selectedShopCard = null;
        selectedShopItem = null;
        selectedShopItemSlot = null;

        if (clearCharacterDetail && characterDetailPanel != null)
        {
            characterDetailPanel.Clear();
        }
    }

    public OwnedCharacter GetSelectedCharacter()
    {
        // 手持ちを選択中
        if (selectionType ==
            SelectionType.Hand)
        {
            if (selectedHandSlot == null ||
                selectedHandSlot.IsEmpty())
            {
                return null;
            }

            return selectedHandSlot.ownedCharacter;
        }

        // 盤面を選択中
        if (selectionType ==
            SelectionType.Board)
        {
            if (selectedBoardSlot == null ||
                selectedBoardSlot.IsEmpty())
            {
                return null;
            }

            return selectedBoardSlot.placedCharacter;
        }

        // ショップを選択中
        // ※ショップのキャラクターはまだ所持していないので、
        // 今回の売却対象にはしない
        return null;
    }

    public void ShowItemDetail(ItemData item)
    {
        if (item == null) return;
        if (characterDetailPanel == null)
            characterDetailPanel = FindAnyObjectByType<CharacterDetailPanel>(FindObjectsInactive.Include);
        if (characterDetailPanel != null) characterDetailPanel.Clear();
        EnemyDetailPanel enemyPanel = characterDetailPanel != null && characterDetailPanel.enemyDetailPanel != null
            ? characterDetailPanel.enemyDetailPanel
            : FindAnyObjectByType<EnemyDetailPanel>(FindObjectsInactive.Include);
        if (enemyPanel != null) enemyPanel.Hide();
        itemDetailPanel = ResolveItemDetailPanel();
        if (itemDetailPanel == null)
        {
            Debug.LogError("ItemDetailPanel was not found. Assign it in BoardManager or add ItemDetailPanel to the panel object.", this);
            return;
        }
        itemDetailPanel.ShowItem(item);
    }

    private ItemDetailPanel ResolveItemDetailPanel()
    {
        ItemDetailPanel[] panels = FindObjectsByType<ItemDetailPanel>(FindObjectsInactive.Include);
        foreach (ItemDetailPanel panel in panels)
        {
            if (panel != null && panel.gameObject.name == "ItemDetailPanel" &&
                (panel.itemText != null || panel.iconImage != null))
                return panel;
        }
        if (itemDetailPanel != null && itemDetailPanel.GetComponent<Canvas>() == null)
            return itemDetailPanel;
        return FindAnyObjectByType<ItemDetailPanel>(FindObjectsInactive.Include);
    }

    public void HideItemDetailPanel()
    {
        itemDetailPanel = ResolveItemDetailPanel();
        if (itemDetailPanel != null) itemDetailPanel.Hide();
    }

    public void HideCharacterAndItemDetails()
    {
        if (characterDetailPanel == null)
            characterDetailPanel = FindAnyObjectByType<CharacterDetailPanel>(FindObjectsInactive.Include);
        if (characterDetailPanel != null) characterDetailPanel.Clear();
        EnemyDetailPanel enemyPanel = characterDetailPanel != null && characterDetailPanel.enemyDetailPanel != null
            ? characterDetailPanel.enemyDetailPanel
            : FindAnyObjectByType<EnemyDetailPanel>(FindObjectsInactive.Include);
        if (enemyPanel != null) enemyPanel.Hide();
        HideItemDetailPanel();
    }

    public static void RefreshVisibleDetailPanels()
    {
        foreach (CharacterDetailPanel panel in FindObjectsByType<CharacterDetailPanel>(
                     FindObjectsInactive.Include))
        {
            if (panel != null && panel.gameObject.activeInHierarchy)
                panel.RefreshDisplayedContent();
        }

        foreach (ItemDetailPanel panel in FindObjectsByType<ItemDetailPanel>(
                     FindObjectsInactive.Include))
        {
            if (panel != null && panel.gameObject.activeInHierarchy)
                panel.RefreshDisplayedContent();
        }

        foreach (EnemyDetailPanel panel in FindObjectsByType<EnemyDetailPanel>(
                     FindObjectsInactive.Include))
        {
            if (panel != null && panel.gameObject.activeInHierarchy)
                panel.RefreshDisplayedContent();
        }
    }

    public void UpdateCharacterDetail()
    {
        if (characterDetailPanel == null)
        {
            return;
        }

        OwnedCharacter character =
            GetSelectedCharacter();

        if (character == null)
        {
            // ショップ選択中はCharacterDataから表示
            if (selectionType ==
                SelectionType.Shop)
            {
                if (selectedShopCharacter != null)
                {
                    characterDetailPanel.ShowShopCharacter(
                        selectedShopCharacter
                    );

                    return;
                }
            }

            characterDetailPanel.Clear();

            return;
        }

        characterDetailPanel.ShowCharacter(
            character
        );
    }

    // =========================================================
    // 盤面重複チェック
    // =========================================================

    public bool IsCharacterAlreadyOnBoard(
        CharacterData characterData,
        BoardSlot excludeSlot = null)
    {
        if (characterData == null)
        {
            return false;
        }

        BoardSlot[] boardSlots =
            boardArea.GetComponentsInChildren<BoardSlot>();

        foreach (BoardSlot slot in boardSlots)
        {
            if (slot == null)
            {
                continue;
            }

            // 移動元の盤面はチェックから除外
            if (slot == excludeSlot)
            {
                continue;
            }

            if (slot.IsEmpty())
            {
                continue;
            }

            if (slot.placedCharacter.data ==
                characterData)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryCombineShopCharacter(
    CharacterData characterData,
    BoardSlot preferredBoardSlot = null)
    {
        if (characterData == null)
        {
            return false;
        }

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            return false;
        }

        HandManager handManager =
            FindAnyObjectByType<HandManager>();

        if (handManager == null)
        {
            return false;
        }

        // =========================================
        // ① まず盤面に同じキャラがいるか探す
        // =========================================

        BoardSlot[] boardSlots =
            boardArea.GetComponentsInChildren<BoardSlot>();

        int[] starCounts = new int[4];

        foreach (BoardSlot slot in boardSlots)
        {
            if (slot == null || slot.IsEmpty())
            {
                continue;
            }

            OwnedCharacter character = slot.placedCharacter;
            if (character.data == characterData &&
                character.star >= 1 && character.star <= 3)
            {
                starCounts[character.star]++;
            }
        }

        HandSlot[] allHandSlots =
            handManager.handArea.GetComponentsInChildren<HandSlot>();

        foreach (HandSlot slot in allHandSlots)
        {
            if (slot == null || slot.IsEmpty() || slot.ownedCharacter == null)
            {
                continue;
            }

            OwnedCharacter character = slot.ownedCharacter;
            if (character.data == characterData &&
                character.star >= 1 && character.star <= 3)
            {
                starCounts[character.star]++;
            }
        }

        int targetStar = 0;
        for (int star = 1; star < 3; star++)
        {
            if (starCounts[star] >= 2)
            {
                targetStar = star;
                break;
            }
        }

        if (targetStar == 0)
        {
            return false;
        }

        // Include both board and hand copies, preferring the lowest mergeable star tier.

        List<OwnedCharacter> candidates =
            new List<OwnedCharacter>();

        List<BoardSlot> candidateBoardSlots =
            new List<BoardSlot>();

        List<HandSlot> candidateHandSlots =
            new List<HandSlot>();

        // -----------------------------------------
        // 盤面から探す
        // -----------------------------------------

        foreach (BoardSlot slot in boardSlots)
        {
            if (slot == null ||
                slot.IsEmpty())
            {
                continue;
            }

            OwnedCharacter character =
                slot.placedCharacter;

            if (character.data != characterData)
            {
                continue;
            }

            if (character.star != targetStar)
            {
                continue;
            }

            candidates.Add(character);
            candidateBoardSlots.Add(slot);
        }

        // -----------------------------------------
        // 手持ちから探す
        // -----------------------------------------

        foreach (HandSlot slot in allHandSlots)
        {
            if (slot == null || slot.IsEmpty() || slot.ownedCharacter == null)
            {
                continue;
            }

            OwnedCharacter character =
                slot.ownedCharacter;

            if (character.data != characterData)
            {
                continue;
            }

            if (character.star != targetStar)
            {
                continue;
            }

            candidates.Add(character);
            candidateHandSlots.Add(slot);
        }

        // 3体揃っていない
        // The shop character is the third copy, so two owned copies are enough.
        if (candidates.Count < 2)
        {
            return false;
        }

        // =========================================
        // ④ 合成後に残す場所を決める
        // =========================================

        OwnedCharacter resultCharacter = null;

        BoardSlot resultBoardSlot = null;
        BoardSlot resultSourceBoardSlot = null;
        HandSlot resultHandSlot = null;

        // 空き盤面を指定している場合は、そのスロットを優先する。
        if (preferredBoardSlot != null &&
            preferredBoardSlot.IsEmpty())
        {
            resultBoardSlot = preferredBoardSlot;

            if (candidateBoardSlots.Count > 0)
            {
                resultSourceBoardSlot =
                    candidateBoardSlots[0];

                resultCharacter =
                    resultSourceBoardSlot.placedCharacter;
            }
            else
            {
                resultHandSlot =
                    candidateHandSlots[0];

                resultCharacter =
                    resultHandSlot.ownedCharacter;
            }
        }
        else if (candidateBoardSlots.Count > 0)
        {
            // 空き盤面の指定がなければ、盤面にいる同キャラの位置を優先する。
            resultBoardSlot = candidateBoardSlots[0];
            resultSourceBoardSlot = resultBoardSlot;
            resultCharacter = resultBoardSlot.placedCharacter;
        }
        else
        {
            resultHandSlot =
                candidateHandSlots[0];
            resultCharacter =
                resultHandSlot.ownedCharacter;
        }

        if (resultBoardSlot != null &&
            resultSourceBoardSlot == null &&
            !CanAddCharacterToBoard(false))
        {
            return false;
        }

        if (!resultCharacter.TrySetStar(
            targetStar + 1,
            CharacterStatSettings.FindForCurrentScene()))
        {
            return false;
        }

        // =========================================
        // ⑤ 合成対象の残り2体を削除
        // =========================================

        int removedCount = 0;
        int requiredRemovalCount =
            Mathf.Min(2, candidates.Count - 1);

        foreach (BoardSlot slot in candidateBoardSlots)
        {
            if (removedCount >= requiredRemovalCount)
            {
                break;
            }

            if (slot == resultSourceBoardSlot)
            {
                if (resultBoardSlot != resultSourceBoardSlot)
                {
                    slot.ClearCharacterVisual();
                }

                continue;
            }

            OwnedCharacter character =
                slot.placedCharacter;

            player.ownedCharacters.Remove(
                character
            );

            slot.ClearCharacterVisual();

            removedCount++;
        }

        foreach (HandSlot slot in candidateHandSlots)
        {
            if (removedCount >= requiredRemovalCount)
            {
                break;
            }

            if (slot.ownedCharacter == resultCharacter)
            {
                continue;
            }

            OwnedCharacter character =
                slot.ownedCharacter;

            player.ownedCharacters.Remove(
                character
            );

            slot.RemoveCharacter();

            removedCount++;
        }

        // =========================================
        // ⑦ 結果を配置
        // =========================================

        if (resultBoardSlot != null)
        {
            if (resultHandSlot != null)
            {
                resultHandSlot.RemoveCharacter();
            }

            resultBoardSlot.SetCharacterWithoutDataChange(
                resultCharacter
            );
        }
        else
        {
            // 盤面にいなかった場合
            // → 一番左の空き手持ちへ

            // 元の手持ちスロットから消す
            foreach (HandSlot slot in candidateHandSlots)
            {
                if (slot.ownedCharacter ==
                    resultCharacter)
                {
                    slot.RemoveCharacter();
                    break;
                }
            }

            handManager.AddCharacter(
                resultCharacter
            );
        }

        Debug.Log(
            characterData.characterName +
            " を3体合成して★" +
            resultCharacter.star +
            "になりました！"
        );

        return true;
    }

    public bool TryCombineShopCharacterWithHand(
    HandSlot targetHandSlot)
    {
        if (selectedShopCharacter == null ||
            targetHandSlot == null ||
            targetHandSlot.IsEmpty())
        {
            return false;
        }

        OwnedCharacter targetCharacter =
            targetHandSlot.ownedCharacter;

        // 同じキャラクターでなければ合成しない
        if (targetCharacter.data !=
            selectedShopCharacter)
        {
            return false;
        }

        // ★3なら合成不可
        if (targetCharacter.star >= 3)
        {
            Debug.Log(
                selectedShopCharacter.characterName +
                " はすでに★3です。"
            );

            return false;
        }

        HandManager handManager =
            FindAnyObjectByType<HandManager>();
        if (handManager == null)
        {
            Debug.LogError("HandManager was not found.", this);
            return false;
        }

        targetHandSlot.RemoveCharacter();
        if (!targetCharacter.TrySetStar(
            targetCharacter.star + 1,
            CharacterStatSettings.FindForCurrentScene()))
        {
            targetHandSlot.SetCharacter(targetCharacter);
            return false;
        }

        // 一番左の空きスロットへ配置
        bool added =
            handManager.AddCharacter(
                targetCharacter
            );

        if (!added)
        {
            targetHandSlot.SetCharacter(
                targetCharacter
            );

            return false;
        }

        Debug.Log(
            selectedShopCharacter.characterName +
            " を合成して★" +
            targetCharacter.star +
            "になりました！"
        );

        return true;
    }

    public bool TryCombineShopCharacterWithBoard(
    BoardSlot targetBoardSlot)
    {
        if (selectedShopCharacter == null ||
            targetBoardSlot == null ||
            !targetBoardSlot.acceptsPlayerCharacters ||
            targetBoardSlot.IsEmpty())
        {
            return false;
        }

        OwnedCharacter targetCharacter =
            targetBoardSlot.placedCharacter;

        // 同じキャラクターでなければ合成しない
        if (targetCharacter.data !=
            selectedShopCharacter)
        {
            return false;
        }

        // ★3なら合成不可
        if (targetCharacter.star >= 3)
        {
            Debug.Log(
                selectedShopCharacter.characterName +
                " はすでに★3です。"
            );

            return false;
        }

        if (!targetCharacter.TrySetStar(
            targetCharacter.star + 1,
            CharacterStatSettings.FindForCurrentScene()))
        {
            return false;
        }

        targetBoardSlot.SetCharacterWithoutDataChange(
            targetCharacter
        );

        Debug.Log(
            selectedShopCharacter.characterName +
            " を合成して★" +
            targetCharacter.star +
            "になりました！"
        );

        return true;
    }
}
