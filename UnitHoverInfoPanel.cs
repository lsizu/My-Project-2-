using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UnitHoverInfoPanel : MonoBehaviour
{
    [Tooltip("Optional complete-roster source for scenes without a ShopManager.")]
    public CharacterFullRosterListDisplay fullRosterSource;
    [Tooltip("When assigned, hover cards use only characters currently placed in these roster slots.")]
    public RosterBoardDisplay rosterBoardSource;

    [Tooltip("Optional. Assign the HandCard Prefab to display one card per matching character.")]
    public GameObject handCardPrefab;

    [Tooltip("Fallback used when HandCard Prefab is not assigned.")]
    public Image[] characterIconImages;

    [Min(0f)]
    public float horizontalOffset = 12f;

    [Min(0f)]
    public float boardUnitPanelGap = 20f;

    private UnitButtonHover activeButton;
    private RectTransform panelRect;
    private readonly List<Image> runtimeIconImages = new List<Image>();
    private Image iconTemplate;
    private readonly List<GameObject> runtimeHandCards = new List<GameObject>();

    private void Awake()
    {
        panelRect = transform as RectTransform;
        InitializeIconImages();
        DisableRaycasts();
    }

    private void LateUpdate()
    {
        if (activeButton == null || !gameObject.activeSelf)
            return;

        if (!IsPointerOverButton(activeButton))
            Hide(activeButton);
    }

    private void OnDisable()
    {
        activeButton = null;
    }

    private void InitializeIconImages()
    {
        runtimeIconImages.Clear();
        if (characterIconImages == null) return;

        foreach (Image image in characterIconImages)
        {
            if (image != null && !runtimeIconImages.Contains(image))
            {
                runtimeIconImages.Add(image);
            }
        }

        if (runtimeIconImages.Count > 0)
        {
            iconTemplate = runtimeIconImages[0];
        }
    }

    public void Show(UnitButtonHover source, string tagName, RectTransform buttonRect, bool isUnit, bool showOnRight)
    {
        if (source == null || buttonRect == null)
        {
            return;
        }

        List<CharacterData> matchingCharacters = GetMatchingCharacters(tagName, isUnit);
        if (handCardPrefab != null)
        {
            ShowHandCardPrefabs(matchingCharacters);
        }
        else
        {
            ShowIconImages(matchingCharacters);
        }

        if (matchingCharacters.Count == 0)
        {
            Hide(source);
            return;
        }

        activeButton = source;
        DisableRaycasts();
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        PositionBesideButton(buttonRect, showOnRight);
    }

    public void Hide(UnitButtonHover source)
    {
        if (source != null && activeButton != null && source != activeButton)
        {
            return;
        }

        activeButton = null;
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void ShowHandCardPrefabs(List<CharacterData> characters)
    {
        foreach (Image image in runtimeIconImages)
        {
            if (image != null) image.gameObject.SetActive(false);
        }

        while (runtimeHandCards.Count < characters.Count)
        {
            GameObject card = Instantiate(handCardPrefab, transform, false);
            card.name = handCardPrefab.name + "_Generated_" + (runtimeHandCards.Count + 1);
            PrepareHoverCard(card);
            runtimeHandCards.Add(card);
        }

        for (int i = 0; i < runtimeHandCards.Count; i++)
        {
            GameObject cardObject = runtimeHandCards[i];
            bool hasCharacter = i < characters.Count;
            if (!hasCharacter)
            {
                cardObject.SetActive(false);
                continue;
            }

            CharacterData character = characters[i];
            HandCard handCard = cardObject.GetComponent<HandCard>();
            Image icon = handCard != null ? handCard.iconImage : FindIconImage(cardObject.transform);
            if (icon != null) icon.sprite = character.icon;

            if (handCard != null)
            {
                if (handCard.nameText != null) handCard.nameText.text = character.characterName;
                if (handCard.costText != null) handCard.costText.text = string.Empty;
                if (handCard.starText != null) handCard.starText.text = string.Empty;
                ApplyCostFrame(handCard, character.cost);
            }

            Transform star = cardObject.transform.Find("StarImage");
            if (star != null) star.gameObject.SetActive(false);
            cardObject.SetActive(true);
        }
    }

    private void ApplyCostFrame(HandCard handCard, int cost)
    {
        if (handCard.frameImage == null) return;

        switch (cost)
        {
            case 1: handCard.frameImage.sprite = handCard.cost1Sprite; break;
            case 2: handCard.frameImage.sprite = handCard.cost2Sprite; break;
            case 3: handCard.frameImage.sprite = handCard.cost3Sprite; break;
            case 4: handCard.frameImage.sprite = handCard.cost4Sprite; break;
            case 5: handCard.frameImage.sprite = handCard.cost5Sprite; break;
        }
    }

    private void DisableRaycasts()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic != null)
                graphic.raycastTarget = false;
        }
    }

    private static bool IsPointerOverButton(UnitButtonHover button)
    {
        if (button == null || !button.isActiveAndEnabled)
            return false;

        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect == null || !buttonRect.gameObject.activeInHierarchy)
            return false;

        if (!TryGetPointerScreenPosition(out Vector2 screenPosition))
            return false;

        Canvas canvas = buttonRect.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(
            buttonRect, screenPosition, eventCamera);
    }

    private static bool TryGetPointerScreenPosition(out Vector2 position)
    {
        if (Mouse.current != null)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        if (Pointer.current != null)
        {
            position = Pointer.current.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    private void PrepareHoverCard(GameObject card)
    {
        HandCard handCard = card.GetComponent<HandCard>();
        if (handCard != null) handCard.enabled = false;

        ShopCard shopCard = card.GetComponent<ShopCard>();
        if (shopCard != null) shopCard.enabled = false;

        Button button = card.GetComponent<Button>();
        if (button != null) button.enabled = false;

        foreach (Graphic graphic in card.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    private Image FindIconImage(Transform card)
    {
        Transform iconTransform = card.Find("Icon");
        return iconTransform != null ? iconTransform.GetComponent<Image>() : card.GetComponentInChildren<Image>(true);
    }

    private void ShowIconImages(List<CharacterData> characters)
    {
        if (runtimeIconImages.Count == 0 || iconTemplate == null)
        {
            Debug.LogWarning("Assign a HandCard Prefab or at least one icon Image to the shared tag hover panel.", this);
            return;
        }

        while (runtimeIconImages.Count < characters.Count)
        {
            Image clone = Instantiate(iconTemplate, iconTemplate.transform.parent, false);
            clone.name = iconTemplate.name + "_Generated_" + (runtimeIconImages.Count + 1);
            clone.sprite = null;
            clone.enabled = false;
            clone.gameObject.SetActive(false);
            runtimeIconImages.Add(clone);
        }

        for (int i = 0; i < runtimeIconImages.Count; i++)
        {
            Image image = runtimeIconImages[i];
            if (image == null) continue;

            bool hasCharacter = i < characters.Count;
            image.sprite = hasCharacter ? characters[i].icon : null;
            image.enabled = hasCharacter;
            image.gameObject.SetActive(hasCharacter);
        }
    }

    private List<CharacterData> GetMatchingCharacters(string tagName, bool isUnit)
    {
        List<CharacterData> characters = new List<CharacterData>();
        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        CharacterData[] roster = GetHoverRoster(shopManager, isUnit);
        if (roster == null) return characters;

        HashSet<CharacterData> addedCharacters = new HashSet<CharacterData>();
        foreach (CharacterData character in roster)
        {
            string[] tags = isUnit ? character?.units : character?.attributes;
            if (character == null || character.icon == null || tags == null ||
                !addedCharacters.Add(character))
            {
                continue;
            }

            foreach (string characterTag in tags)
            {
                if (string.Equals(characterTag, tagName, System.StringComparison.Ordinal))
                {
                    characters.Add(character);
                    break;
                }
            }
        }

        // Stable insertion sort: costs ascend while characters with equal cost
        // retain their original roster order.
        for (int i = 1; i < characters.Count; i++)
        {
            CharacterData current = characters[i];
            int j = i - 1;
            while (j >= 0 && characters[j].cost > current.cost)
            {
                characters[j + 1] = characters[j];
                j--;
            }

            characters[j + 1] = current;
        }

        return characters;
    }

    private CharacterData[] GetHoverRoster(ShopManager shopManager, bool isUnit)
    {
        if (rosterBoardSource != null && rosterBoardSource.slotContainer != null)
        {
            List<CharacterData> placedCharacters = new List<CharacterData>();
            HashSet<CharacterData> uniqueCharacters = new HashSet<CharacterData>();
            RosterBoardSlot[] slots = rosterBoardSource.slotContainer.GetComponentsInChildren<RosterBoardSlot>(true);
            foreach (RosterBoardSlot slot in slots)
            {
                if (slot != null && slot.characterData != null && uniqueCharacters.Add(slot.characterData))
                    placedCharacters.Add(slot.characterData);
            }
            return placedCharacters.ToArray();
        }

        if (fullRosterSource != null)
        {
            if (fullRosterSource.allCharacters != null && fullRosterSource.allCharacters.Length > 0)
                return fullRosterSource.allCharacters;
            if (fullRosterSource.characterSource != null &&
                fullRosterSource.characterSource.fullCharacterRoster != null)
                return fullRosterSource.characterSource.fullCharacterRoster;
        }

        if (shopManager == null) return null;

        // Attribute hover is limited to the current game's selected roster.
        // Unit hover still uses the full roster to show all members of a Unit.
        return !isUnit
            ? shopManager.characters
            : shopManager.fullCharacterRoster != null && shopManager.fullCharacterRoster.Length > 0
                ? shopManager.fullCharacterRoster
                : shopManager.characters;
    }

    private void PositionBesideButton(RectTransform buttonRect, bool showOnRight)
    {
        if (panelRect == null)
        {
            panelRect = transform as RectTransform;
        }

        if (panelRect == null || panelRect.parent == null) return;

        Vector3[] buttonCorners = new Vector3[4];
        buttonRect.GetWorldCorners(buttonCorners);
        Transform panelParent = panelRect.parent;

        if (showOnRight)
        {
            // The panel height changes with the number of matching characters, so
            // rebuild the layout first; otherwise the bottom edge is computed from a
            // stale size and the panel ends up misaligned.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            // World corners: 0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right.
            // Align the panel's bottom-left with the button's bottom-right, plus a gap.
            Vector3[] panelCorners = new Vector3[4];
            panelRect.GetWorldCorners(panelCorners);
            Vector3 gapOffset = panelParent.TransformVector(new Vector3(boardUnitPanelGap, 0f, 0f));
            Vector3 targetPanelBottomLeft = buttonCorners[3] + gapOffset;
            panelRect.position += targetPanelBottomLeft - panelCorners[0];
            return;
        }

        Vector3 buttonLeftCenter = (buttonCorners[0] + buttonCorners[1]) * 0.5f;
        Vector3 leftOffset = panelParent.TransformVector(
            new Vector3(-(panelRect.rect.width * 0.5f + horizontalOffset), 0f, 0f));
        panelRect.position = buttonLeftCenter + leftOffset;
    }
}
