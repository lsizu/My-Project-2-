using UnityEngine;

public class HandManager : MonoBehaviour
{
    [Header("手持ち設定")]
    public GameObject handSlotPrefab;
    public Transform handArea;

    [Header("手持ち枠数")]
    public int slotCount = 10;

    private HandSlot[] handSlots;

    [Header("移動中の手持ちキャラクター")]
    public HandSlot selectedHandSlot;

    void Start()
    {
        CreateHandSlots();
    }

    void CreateHandSlots()
    {
        foreach (Transform child in handArea)
        {
            Destroy(child.gameObject);
        }

        handSlots = new HandSlot[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObject =
                Instantiate(
                    handSlotPrefab,
                    handArea
                );

            HandSlot slot =
    slotObject.GetComponent<HandSlot>();

            // 初期化は必ず空にする
            slot.ownedCharacter = null;

            handSlots[i] = slot;
        }
    }

    // 空きスロットがあるか
    public HandSlot FindEmptySlot()
    {
        if (handSlots == null) return null;
        foreach (HandSlot slot in handSlots)
            if (slot != null && slot.IsEmpty()) return slot;
        return null;
    }

    public bool HasEmptySlot()
    {
        if (handSlots == null)
        {
            return false;
        }

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i].IsEmpty())
            {
                return true;
            }
        }

        return false;
    }

    public int CountEmptySlots()
    {
        if (handSlots == null)
            return 0;

        int emptySlotCount = 0;
        foreach (HandSlot slot in handSlots)
            if (slot != null && slot.IsEmpty())
                emptySlotCount++;

        return emptySlotCount;
    }

    // 一番左の空きスロットにキャラクターを入れる
    public bool AddCharacter(OwnedCharacter character)
    {
        if (character == null)
        {
            return false;
        }

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i].IsEmpty())
            {
                handSlots[i].SetCharacter(character);

                Debug.Log(
                    character.data.characterName +
                    " を手持ちの " +
                    (i + 1) +
                    " 番目のスロットに配置しました。"
                );

                return true;
            }
        }

        Debug.Log("手持ちに空きスロットがありません！");

        return false;
    }

    public HandSlot FindCharacterSlot(
    CharacterData characterData)
    {
        if (characterData == null ||
            handSlots == null)
        {
            return null;
        }

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i].IsEmpty() || handSlots[i].ownedCharacter == null)
            {
                continue;
            }

            if (handSlots[i].ownedCharacter.data ==
                characterData)
            {
                return handSlots[i];
            }
        }

        return null;
    }

    // ショップキャラクターを一番左の空きスロットへ購入
    public bool BuyShopCharacterToLeftmost(
        CharacterData character,
        ShopCard shopCard)
    {
        if (character == null)
        {
            return false;
        }

        if (shopCard == null)
        {
            return false;
        }

        // 空きスロットを探す
        HandSlot targetSlot = null;

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i].IsEmpty())
            {
                targetSlot = handSlots[i];
                break;
            }
        }

        // 手持ちが満杯
        if (targetSlot == null)
        {
            Debug.Log("手持ちに空きスロットがありません！");
            return false;
        }

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            Debug.LogError("PlayerDataが見つかりません！");
            return false;
        }

        // ゴールドチェック
        if (!player.CanBuy(character.cost))
        {
            Debug.Log("コインが足りません！");
            return false;
        }

        CharacterStatSettings statSettings =
            CharacterStatSettings.FindForCurrentScene();
        if (statSettings == null ||
            !statSettings.TryGetStatRanges(
                character.cost,
                1,
                out _,
                out _,
                out _,
                out _))
        {
            Debug.LogError(
                "Character stats are not configured for Cost " + character.cost +
                ", Star 1.");
            return false;
        }

        // ゴールドを支払う
        player.gold -= character.cost;
        player.UpdateGoldUI();

        // 所持キャラクターを作る
        OwnedCharacter ownedCharacter =
            new OwnedCharacter(
                character,
                statSettings);

        // 所持リストに追加
        player.ownedCharacters.Add(ownedCharacter);

        // 左端の空きスロットに配置
        targetSlot.SetCharacter(ownedCharacter);

        Debug.Log(
            character.characterName +
            " を左端の空きスロットに配置しました！"
        );

        // ショップから消す
        ShopSlot shopSlot =
    shopCard.GetShopSlot();

        if (shopSlot != null)
        {
            shopSlot.ClearCharacter();
        }

        return true;
    }

    // 手持ちキャラクターを選択
    public void SelectHandCharacter(
        OwnedCharacter character,
        HandSlot handSlot)
    {
        if (character == null || handSlot == null)
        {
            return;
        }

        // すでに別の手持ちキャラクターを選択中
        if (selectedHandSlot != null)
        {
            // 同じキャラクターをクリック
            // → 選択解除
            if (selectedHandSlot == handSlot)
            {
                selectedHandSlot = null;

                Debug.Log("手持ちキャラクターの選択を解除しました。");

                return;
            }

            // 別の手持ちキャラクター
            // → 交換
            SwapHandCharacters(
                selectedHandSlot,
                handSlot
            );

            return;
        }

        // 何も選択していない
        selectedHandSlot = handSlot;

        Debug.Log(
            character.data.characterName +
            " を手持ちで選択しました。"
        );
    }

    // 手持ちキャラクターを移動・交換
    public void MoveHandCharacter(
        HandSlot fromSlot,
        HandSlot toSlot)
    {
        if (fromSlot == null || toSlot == null)
        {
            return;
        }

        if (fromSlot.IsEmpty())
        {
            return;
        }

        // 移動先が空いている
        if (toSlot.IsEmpty())
        {
            OwnedCharacter character =
                fromSlot.RemoveCharacter();

            toSlot.SetCharacter(character);

            selectedHandSlot = null;

            Debug.Log(
                character.data.characterName +
                " を手持ち内で移動しました！"
            );

            return;
        }

        // 移動先にもキャラクターがいる
        SwapHandCharacters(
            fromSlot,
            toSlot
        );
    }

    // 手持ちキャラクターを交換
    public void SwapHandCharacters(
        HandSlot slotA,
        HandSlot slotB)
    {
        if (slotA == null || slotB == null)
        {
            return;
        }

        if (slotA.IsEmpty() || slotB.IsEmpty())
        {
            return;
        }

        OwnedCharacter characterA =
            slotA.ownedCharacter;

        OwnedCharacter characterB =
            slotB.ownedCharacter;

        slotA.SetCharacter(characterB);
        slotB.SetCharacter(characterA);

        selectedHandSlot = null;

        Debug.Log(
            characterA.data.characterName +
            " と " +
            characterB.data.characterName +
            " を交換しました！"
        );
    }
}