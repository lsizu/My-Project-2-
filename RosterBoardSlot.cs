using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One independent planning-board slot that accepts dragged roster cards.
/// </summary>
public class RosterBoardSlot : MonoBehaviour
{
    private RosterBoardDisplay owner;
    private GameObject displayedCard;

    public int slotIndex { get; private set; }
    public CharacterData characterData { get; private set; }

    public void Initialize(RosterBoardDisplay display, int index)
    {
        owner = display;
        slotIndex = index;

        Image background = GetComponent<Image>();
        if (background != null) background.raycastTarget = true;
    }

    public void ClickSlot()
    {
        if (owner != null)
            owner.PlaceSelectedCharacter(this);
    }

    public void SelectCharacter()
    {
        if (owner != null)
            owner.SelectCharacterFromSlot(this);
    }

    public void ShowCharacterDetail()
    {
        if (owner != null && characterData != null)
            owner.ShowCharacterDetail(characterData);
    }

    public void ClearCharacter()
    {
        if (displayedCard != null)
            Destroy(displayedCard);

        bool hadCharacter = characterData != null;
        displayedCard = null;
        characterData = null;
        if (hadCharacter && owner != null)
            owner.NotifyRosterChanged();
    }

    public void SetCharacter(CharacterData character, GameObject handCardPrefab)
    {
        if (character == null || handCardPrefab == null) return;

        if (displayedCard != null)
            Destroy(displayedCard);

        characterData = character;
        displayedCard = Instantiate(handCardPrefab, transform, false);
        displayedCard.name = "RosterBoardCharacter_" + character.characterName;

        RectTransform cardRect = displayedCard.transform as RectTransform;
        RectTransform slotRect = transform as RectTransform;
        if (cardRect != null && slotRect != null)
        {
            // Keep the HandCard prefab's authored size and scale. Its 0.75
            // scale makes the 160x160 frame fit the HandSlot's 120x120 area.
            // Stretching the root or resetting its scale to 1 makes the fixed-
            // size child graphics overflow the slot.
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
        }

        HandCard card = displayedCard.GetComponent<HandCard>();
        if (card != null)
        {
            card.enabled = true;
            card.SetReadOnlyCharacter(character);
            card.SetRosterBoardSlot(this);
            card.SetCharacterDetailPanel(owner != null ? owner.characterDetailPanel : null);
            if (card.nameText != null) card.nameText.text = character.characterName;
            if (card.costText != null) card.costText.text = character.cost.ToString();
            if (card.starText != null) card.starText.text = string.Empty;
            if (card.iconImage != null) card.iconImage.sprite = character.icon;
            ApplyCostFrame(card, character.cost);
        }

        ShopCard shopCard = displayedCard.GetComponent<ShopCard>();
        if (shopCard != null) shopCard.enabled = false;

        Button button = displayedCard.GetComponent<Button>();
        if (button != null) button.enabled = false;

        Transform star = displayedCard.transform.Find("StarImage");
        if (star != null) star.gameObject.SetActive(false);

        if (owner != null)
            owner.NotifyRosterChanged();
    }

    private void ApplyCostFrame(HandCard card, int cost)
    {
        if (card.frameImage == null) return;

        switch (cost)
        {
            case 1: card.frameImage.sprite = card.cost1Sprite; break;
            case 2: card.frameImage.sprite = card.cost2Sprite; break;
            case 3: card.frameImage.sprite = card.cost3Sprite; break;
            case 4: card.frameImage.sprite = card.cost4Sprite; break;
            case 5: card.frameImage.sprite = card.cost5Sprite; break;
        }
    }
}
