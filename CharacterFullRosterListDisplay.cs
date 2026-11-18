using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays every character for a pre-match roster selection scene.
/// </summary>
public class CharacterFullRosterListDisplay : MonoBehaviour
{
    [Header("表示先")]
    public Transform listContent;

    [Header("カード")]
    public GameObject handCardPrefab;

    [Tooltip("Assign the complete character roster for scenes that do not contain a ShopManager.")]
    public CharacterData[] allCharacters;

    [Tooltip("Optional source for ShopManager.fullCharacterRoster.")]
    public ShopManager characterSource;

    [Tooltip("Shown when a full-roster card is right-clicked.")]
    public CharacterDetailPanel characterDetailPanel;
    public RosterBoardDisplay rosterBoardDisplay;

    private readonly List<GameObject> generatedCards = new List<GameObject>();

    private void Awake()
    {
        if (listContent == null)
            listContent = transform;
    }

    private void Start()
    {
        RefreshRoster();
    }

    public void RefreshRoster()
    {
        if (listContent == null)
            listContent = transform;

        if (handCardPrefab == null || listContent == null)
        {
            Debug.LogWarning("Assign List Content and Hand Card Prefab.", this);
            return;
        }

        if (rosterBoardDisplay == null)
            rosterBoardDisplay = FindAnyObjectByType<RosterBoardDisplay>();
        if (characterDetailPanel != null)
        {
            characterDetailPanel.fullRosterSource = this;
            characterDetailPanel.rosterBoardDisplay = rosterBoardDisplay;
            if (characterDetailPanel.unitHoverInfoPanel != null)
            {
                characterDetailPanel.unitHoverInfoPanel.fullRosterSource = this;
                characterDetailPanel.unitHoverInfoPanel.rosterBoardSource = rosterBoardDisplay;
            }
            if (rosterBoardDisplay != null)
                rosterBoardDisplay.characterDetailPanel = characterDetailPanel;
        }

        CharacterData[] roster = GetFullRoster();
        List<CharacterData> validCharacters = new List<CharacterData>();
        HashSet<CharacterData> seenCharacters = new HashSet<CharacterData>();
        foreach (CharacterData character in roster)
        {
            if (character != null && seenCharacters.Add(character))
                validCharacters.Add(character);
        }

        while (generatedCards.Count < validCharacters.Count)
        {
            GameObject card = Instantiate(handCardPrefab, listContent, false);
            card.name = handCardPrefab.name + "_FullRoster_" + (generatedCards.Count + 1);
            PrepareDisplayCard(card);
            generatedCards.Add(card);
        }

        for (int i = 0; i < generatedCards.Count; i++)
        {
            bool hasCharacter = i < validCharacters.Count;
            GameObject cardObject = generatedCards[i];
            cardObject.SetActive(hasCharacter);
            if (!hasCharacter) continue;

            SetCardData(cardObject, validCharacters[i]);
            cardObject.transform.SetSiblingIndex(i);
        }
    }

    public CharacterData[] GetFullRoster()
    {
        if (allCharacters != null && allCharacters.Length > 0)
            return allCharacters;

        if (characterSource == null)
            characterSource = FindAnyObjectByType<ShopManager>();

        if (characterSource != null && characterSource.fullCharacterRoster != null)
            return characterSource.fullCharacterRoster;

        Debug.LogWarning("Assign allCharacters or a ShopManager with fullCharacterRoster.", this);
        return new CharacterData[0];
    }

    private void SetCardData(GameObject cardObject, CharacterData character)
    {
        HandCard card = cardObject.GetComponent<HandCard>();
        if (card == null)
        {
            Debug.LogWarning("The assigned HandCard Prefab has no HandCard component.", cardObject);
            return;
        }

        card.SetReadOnlyCharacter(character);
        card.SetCharacterDetailPanel(characterDetailPanel);
        if (card.nameText != null) card.nameText.text = character.characterName;
        if (card.costText != null) card.costText.text = string.Empty;
        if (card.starText != null) card.starText.text = string.Empty;
        if (card.iconImage != null) card.iconImage.sprite = character.icon;
        ApplyCostOneFrame(card);

        Transform star = cardObject.transform.Find("StarImage");
        if (star != null) star.gameObject.SetActive(false);
    }

    private void ApplyCostOneFrame(HandCard card)
    {
        if (card.frameImage != null && card.cost1Sprite != null)
            card.frameImage.sprite = card.cost1Sprite;
    }

    private void PrepareDisplayCard(GameObject cardObject)
    {
        ShopCard shopCard = cardObject.GetComponent<ShopCard>();
        if (shopCard != null) shopCard.enabled = false;

        Button button = cardObject.GetComponent<Button>();
        if (button != null) button.enabled = false;
    }
}
