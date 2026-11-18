using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>One shop position that can show either a character or an item.</summary>
public class ShopSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("表示用")]
    public GameObject shopCardPrefab;

    private ShopCard shopCard;
    private GameObject itemCardObject;

    public CharacterData characterData;
    public ItemData itemData;

    public bool IsEmpty() => characterData == null && itemData == null;

    public void SetCharacter(CharacterData character)
    {
        ClearCharacter();
        characterData = character;
        if (character == null) return;

        if (shopCardPrefab == null)
        {
            Debug.LogError("ShopCard Prefabが設定されていません。", this);
            return;
        }

        GameObject cardObject = Instantiate(shopCardPrefab, transform, false);
        shopCard = cardObject.GetComponent<ShopCard>();
        if (shopCard == null)
        {
            Debug.LogError("ShopCard PrefabにShopCard.csがありません。", cardObject);
            return;
        }

        shopCard.SetCharacter(character);
        shopCard.SetShopSlot(this);
    }

    public void SetItem(ItemData item)
    {
        ClearCharacter();
        itemData = item;
        if (item == null) return;

        if (shopCardPrefab == null)
        {
            Debug.LogError("ShopSlotに表示用Card Prefabが設定されていません。", this);
            return;
        }

        itemCardObject = Instantiate(shopCardPrefab, transform, false);
        ShopCard characterCard = itemCardObject.GetComponent<ShopCard>();
        if (characterCard != null) characterCard.enabled = false;

        ShopItemCard display = itemCardObject.GetComponent<ShopItemCard>();
        if (display == null) display = itemCardObject.AddComponent<ShopItemCard>();
        display.SetItem(item);
    }

    public CharacterData GetCharacter() => characterData;
    public ItemData GetItem() => itemData;
    public ShopCard GetShopCard() => shopCard;

    public void ClearCharacter()
    {
        if (shopCard != null)
        {
            Destroy(shopCard.gameObject);
            shopCard = null;
        }
        if (itemCardObject != null)
        {
            Destroy(itemCardObject);
            itemCardObject = null;
        }
        characterData = null;
        itemData = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Item buying/targeting is not part of this display-only change.
        if (characterData == null) return;

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        if (boardManager == null)
        {
            Debug.LogError("BoardManagerが見つかりません。", this);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
            boardManager.ShowShopCharacterDetail(characterData, shopCard);
    }
}