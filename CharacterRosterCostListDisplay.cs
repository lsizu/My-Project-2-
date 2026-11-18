using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the selected character roster as HandCard prefabs, ordered by cost
/// from low to high. Characters with the same cost keep their roster order.
/// </summary>
public class CharacterRosterCostListDisplay : MonoBehaviour
{
    [Header("表示先")]
    public Transform listContent;

    [Header("カード")]
    public GameObject handCardPrefab;

    [Min(0.1f)]
    public float refreshInterval = 0.5f;

    private readonly List<GameObject> generatedCards = new List<GameObject>();
    private readonly List<GameObject> generatedSpacers = new List<GameObject>();
    private readonly List<CharacterData> previousRoster = new List<CharacterData>();
    private readonly Dictionary<CharacterData, int> previousCosts = new Dictionary<CharacterData, int>();
    private float refreshTimer;
    private bool initialized;
    private int previousColumnCount = -1;

    private void Awake()
    {
        if (listContent == null)
            listContent = transform;
    }

    private void LateUpdate()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (!initialized || refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            if (!initialized || HasRosterChanged())
                RefreshRoster();
        }
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

        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        CharacterData[] roster = shopManager != null && shopManager.characters != null
            ? shopManager.characters
            : new CharacterData[0];

        List<CharacterData> sortedRoster = new List<CharacterData>();
        foreach (CharacterData character in roster)
        {
            if (character != null)
                sortedRoster.Add(character);
        }

        // Stable insertion sort: characters with equal cost retain roster order.
        for (int i = 1; i < sortedRoster.Count; i++)
        {
            CharacterData current = sortedRoster[i];
            int j = i - 1;
            while (j >= 0 && sortedRoster[j].cost > current.cost)
            {
                sortedRoster[j + 1] = sortedRoster[j];
                j--;
            }
            sortedRoster[j + 1] = current;
        }

        int columnCount = GetColumnCount();
        while (generatedCards.Count < sortedRoster.Count)
        {
            GameObject card = Instantiate(handCardPrefab, listContent, false);
            card.name = handCardPrefab.name + "_Roster_" + (generatedCards.Count + 1);
            PrepareDisplayCard(card);
            generatedCards.Add(card);
        }

        List<Transform> displayOrder = new List<Transform>();
        int spacerCount = 0;
        for (int i = 0; i < generatedCards.Count; i++)
        {
            bool hasCharacter = i < sortedRoster.Count;
            GameObject cardObject = generatedCards[i];
            cardObject.SetActive(hasCharacter);
            if (!hasCharacter)
                continue;

            if (i > 0 && sortedRoster[i].cost != sortedRoster[i - 1].cost)
            {
                int remainder = displayOrder.Count % columnCount;
                int requiredSpacers = remainder == 0 ? 0 : columnCount - remainder;
                for (int spacerIndex = 0; spacerIndex < requiredSpacers; spacerIndex++)
                {
                    while (generatedSpacers.Count <= spacerCount)
                    {
                        GameObject spacer = new GameObject(
                            "RosterCostGroupSpacer_" + (generatedSpacers.Count + 1),
                            typeof(RectTransform));
                        spacer.transform.SetParent(listContent, false);
                        generatedSpacers.Add(spacer);
                    }

                    GameObject spacerObject = generatedSpacers[spacerCount++];
                    spacerObject.SetActive(true);
                    displayOrder.Add(spacerObject.transform);
                }
            }

            SetCardData(cardObject, sortedRoster[i]);
            displayOrder.Add(cardObject.transform);
        }

        for (int i = spacerCount; i < generatedSpacers.Count; i++)
            generatedSpacers[i].SetActive(false);

        for (int i = 0; i < displayOrder.Count; i++)
            displayOrder[i].SetSiblingIndex(i);

        previousRoster.Clear();
        previousRoster.AddRange(roster);
        previousCosts.Clear();
        foreach (CharacterData character in roster)
        {
            if (character != null)
                previousCosts[character] = character.cost;
        }
        previousColumnCount = columnCount;
        initialized = true;
    }

    private int GetColumnCount()
    {
        GridLayoutGroup grid = listContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return 4;

        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            return Mathf.Max(1, grid.constraintCount);

        if (grid.constraint != GridLayoutGroup.Constraint.Flexible)
            return 4;

        RectTransform contentRect = listContent as RectTransform;
        float cellAndSpacingWidth = grid.cellSize.x + grid.spacing.x;
        if (contentRect == null || cellAndSpacingWidth <= 0f)
            return 4;

        float availableWidth = contentRect.rect.width - grid.padding.horizontal;
        return Mathf.Max(1, Mathf.FloorToInt(
            (availableWidth + grid.spacing.x) / cellAndSpacingWidth));
    }

    private void SetCardData(GameObject cardObject, CharacterData character)
    {
        HandCard card = cardObject.GetComponent<HandCard>();
        if (card != null)
        {
            card.SetReadOnlyCharacter(character);
            if (card.nameText != null) card.nameText.text = character.characterName;
            if (card.costText != null) card.costText.text = character.cost.ToString();
            if (card.starText != null) card.starText.text = string.Empty;
            if (card.iconImage != null) card.iconImage.sprite = character.icon;
            ApplyCostFrame(card, character.cost);
        }
        else
        {
            Debug.LogWarning("The assigned HandCard Prefab has no HandCard component.", cardObject);
        }

        Transform star = cardObject.transform.Find("StarImage");
        if (star != null) star.gameObject.SetActive(false);
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

    private void PrepareDisplayCard(GameObject cardObject)
    {
        // These cards are a read-only roster display and must not trigger hand actions.
        ShopCard shopCard = cardObject.GetComponent<ShopCard>();
        if (shopCard != null) shopCard.enabled = false;

        Button button = cardObject.GetComponent<Button>();
        if (button != null) button.enabled = false;

        Graphic rootGraphic = cardObject.GetComponent<Graphic>();
        if (rootGraphic != null)
            rootGraphic.raycastTarget = true;
    }

    private bool HasRosterChanged()
    {
        if (GetColumnCount() != previousColumnCount)
            return true;

        ShopManager shopManager = FindAnyObjectByType<ShopManager>();
        CharacterData[] roster = shopManager != null && shopManager.characters != null
            ? shopManager.characters
            : new CharacterData[0];

        if (roster.Length != previousRoster.Count)
            return true;

        for (int i = 0; i < roster.Length; i++)
        {
            if (roster[i] != previousRoster[i])
                return true;
            if (roster[i] != null &&
                (!previousCosts.TryGetValue(roster[i], out int previousCost) ||
                 previousCost != roster[i].cost))
                return true;
        }

        return false;
    }
}
