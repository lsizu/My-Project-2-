using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class CharacterDetailPanel : MonoBehaviour
{

    [Header("Display")]
    public Image iconImage;
    [Tooltip("Optional parent for the HandCard Prefab used in DetailIcon. If empty, iconImage's Transform is used.")]
    public Transform detailIconContainer;
    public GameObject detailHandCardPrefab;
    public TMP_Text nameText;
    public TMP_Text starText;
    public TMP_Text costText;
    [FormerlySerializedAs("hpText")]
    public TMP_Text defenseText;
    public TMP_Text attackText;
    public TMP_Text attributeText;
    public TMP_Text unitText;
    public CharacterStatSettings characterStatSettings;
    public EnemyDetailPanel enemyDetailPanel;
    public Transform unitList;
    public Button unitButtonTemplate;
    public UnitHoverInfoPanel unitHoverInfoPanel;
    public UnitHoverInfoPanel unitHoverInfoPanelPrefab;
    public Transform attributeList;
    public Button attributeButtonTemplate;

    [Header("出撃画面の集計元（設定時のみ有効）")]
    public CharacterFullRosterListDisplay fullRosterSource;
    public RosterBoardDisplay rosterBoardDisplay;

    private GameObject generatedDetailHandCard;
    private CharacterData currentCharacter;
    private OwnedCharacter currentOwnedCharacter;
    private readonly List<GameObject> generatedStarImages = new List<GameObject>();
    private readonly List<Button> generatedUnitButtons = new List<Button>();
    private readonly List<TMP_Text> generatedUnitNameTexts = new List<TMP_Text>();
    private readonly List<TMP_Text> generatedUnitCountTexts = new List<TMP_Text>();
    private readonly List<Button> generatedAttributeButtons = new List<Button>();
    private readonly List<TMP_Text> generatedAttributeNameTexts = new List<TMP_Text>();
    private readonly List<TMP_Text> generatedAttributeCountTexts = new List<TMP_Text>();

    private class TagButtonEntry
    {
        public string tagName;
        public int totalMembers;
        public int boardMembers;
        public int sourceOrder;
    }

    public UnitHoverInfoPanel GetOrCreateUnitHoverInfoPanel()
    {
        if (unitHoverInfoPanel != null)
            return unitHoverInfoPanel;
        if (unitHoverInfoPanelPrefab == null)
        {
            Debug.LogWarning("Assign the HoverPanel Prefab on CharacterDetailPanel.", this);
            return null;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("A Canvas is required to create the HoverPanel.", this);
            return null;
        }

        unitHoverInfoPanel = Instantiate(unitHoverInfoPanelPrefab, canvas.transform, false);
        unitHoverInfoPanel.name = "HoverPanel_Runtime";
        unitHoverInfoPanel.fullRosterSource = fullRosterSource;
        unitHoverInfoPanel.rosterBoardSource = rosterBoardDisplay;
        unitHoverInfoPanel.gameObject.SetActive(false);
        return unitHoverInfoPanel;
    }
    public void ShowShopCharacter(
    CharacterData data)
    {
        if (data == null)
        {
            Clear();
            return;
        }

        ItemDetailPanel.HideOtherPanels(this);
        HideEnemyDetailPanel();
        currentCharacter = data;
        currentOwnedCharacter = null;
        SetDetailIcon(data);

        if (nameText != null)
        {
            nameText.text =
                data.characterName;
        }

        SetStarImages(1);

        if (costText != null)
        {
            costText.text =
                "COST " + data.cost;
        }

        if (defenseText != null)
        {
            defenseText.text = "DEF " +
                GetStatRangeText(data.cost, 1, false);
        }

        if (attackText != null)
        {
            attackText.text = "ATK " +
                GetStatRangeText(data.cost, 1, true);
        }

        SetAttributeButtons(data);

        SetCompleteUnitButtons(data.units);

        gameObject.SetActive(true);
        CharacterDetailTabs tabs = GetComponent<CharacterDetailTabs>();
        if (tabs != null) tabs.RefreshUnitLayout();
    }

    public void ShowCharacter(
        OwnedCharacter character)
    {
        if (character == null ||
            character.data == null)
        {
            Clear();
            return;
        }

        CharacterData data =
            character.data;

        ItemDetailPanel.HideOtherPanels(this);
        HideEnemyDetailPanel();
        currentCharacter = data;
        currentOwnedCharacter = character;

        // アイコン
        SetDetailIcon(data);

        // 基本情報
        if (nameText != null)
        {
            nameText.text =
                data.characterName;
        }

        SetStarImages(character.star);

        if (costText != null)
        {
            costText.text =
                "COST " + data.cost;
        }

        // ステータス
        if (defenseText != null)
        {
            defenseText.text =
                "DEF " + character.GetDefense(characterStatSettings);
        }

        if (attackText != null)
        {
            attackText.text =
                "ATK " + character.GetAttack(characterStatSettings);
        }

        // 属性
        SetAttributeButtons(data);

        // ユニット
        SetCompleteUnitButtons(data.units);

        gameObject.SetActive(true);
        CharacterDetailTabs tabs = GetComponent<CharacterDetailTabs>();
        if (tabs != null) tabs.RefreshUnitLayout();
    }

    public bool TryToggleUnitAttributeFor(OwnedCharacter character)
    {
        if (!gameObject.activeInHierarchy ||
            character == null ||
            currentOwnedCharacter != character)
            return false;

        CharacterDetailTabs tabs = GetComponent<CharacterDetailTabs>();
        return tabs != null && tabs.ToggleUnitAttribute();
    }

    public bool TryToggleUnitAttributeFor(CharacterData character)
    {
        if (!gameObject.activeInHierarchy ||
            character == null ||
            currentOwnedCharacter != null ||
            currentCharacter != character)
            return false;

        CharacterDetailTabs tabs = GetComponent<CharacterDetailTabs>();
        return tabs != null && tabs.ToggleUnitAttribute();
    }

    private string GetStatRangeText(int cost, int star, bool attack)
    {
        if (characterStatSettings == null ||
            !characterStatSettings.TryGetStatRanges(
                cost,
                star,
                out int minimumAttack,
                out int maximumAttack,
                out int minimumDefense,
                out int maximumDefense))
        {
            Debug.LogError(
                "Character stat options are missing for Cost " + cost +
                ", Star " + star + ".",
                this);
            return "-";
        }

        int minimum = attack ? minimumAttack : minimumDefense;
        int maximum = attack ? maximumAttack : maximumDefense;
        return minimum == maximum
            ? minimum.ToString()
            : minimum + "-" + maximum;
    }

    private void HideEnemyDetailPanel()
    {
        if (enemyDetailPanel != null)
            enemyDetailPanel.Hide();
    }


    public void RefreshRosterBoardTagLists()
    {
        if (!UsesRosterBoardData() || currentCharacter == null || !gameObject.activeInHierarchy)
            return;

        SetAttributeButtons(currentCharacter);
        SetCompleteUnitButtons(currentCharacter.units);
        CharacterDetailTabs tabs = GetComponent<CharacterDetailTabs>();
        if (tabs != null) tabs.RefreshUnitLayout();
    }

    public void RefreshDisplayedContent()
    {
        if (!gameObject.activeInHierarchy || currentCharacter == null)
            return;

        if (currentOwnedCharacter != null)
            ShowCharacter(currentOwnedCharacter);
        else
            ShowShopCharacter(currentCharacter);
    }

    private void SetDetailIcon(CharacterData data)
    {
        if (data == null) return;

        if (detailHandCardPrefab == null)
        {
            if (generatedDetailHandCard != null)
            {
                generatedDetailHandCard.SetActive(false);
            }

            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = data.icon;
            }
            return;
        }

        Transform parent = detailIconContainer != null
            ? detailIconContainer
            : iconImage != null ? iconImage.transform : null;
        if (parent == null)
        {
            Debug.LogWarning("Assign Detail Icon Container or Icon Image to CharacterDetailPanel.", this);
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (generatedDetailHandCard == null)
        {
            generatedDetailHandCard = Instantiate(detailHandCardPrefab, parent, false);
            generatedDetailHandCard.name = detailHandCardPrefab.name + "_DetailIcon";
            PrepareDetailHandCard(generatedDetailHandCard);
        }
        else if (generatedDetailHandCard.transform.parent != parent)
        {
            generatedDetailHandCard.transform.SetParent(parent, false);
        }

        HandCard handCard = generatedDetailHandCard.GetComponent<HandCard>();
        if (handCard != null)
        {
            if (handCard.nameText != null) handCard.nameText.text = data.characterName;
            if (handCard.costText != null) handCard.costText.text = string.Empty;
            if (handCard.starText != null) handCard.starText.text = string.Empty;
            ApplyDetailCostFrame(handCard, data.cost);
        }

        Image cardIcon = handCard != null ? handCard.iconImage : null;
        if (cardIcon == null)
            cardIcon = FindDetailCardIcon(generatedDetailHandCard.transform);

        if (cardIcon != null)
        {
            cardIcon.sprite = data.icon;
            cardIcon.enabled = true;
        }
        else
        {
            Debug.LogWarning("Detail HandCard Prefab has no Icon Image. Assign its Icon child to HandCard.iconImage.", generatedDetailHandCard);
        }

        if (data.icon == null)
            Debug.LogWarning("CharacterData has no icon assigned: " + data.characterName, data);

        Transform star = generatedDetailHandCard.transform.Find("StarImage");
        if (star != null) star.gameObject.SetActive(false);
        generatedDetailHandCard.SetActive(true);
    }

    private void SetStarImages(int starCount)
    {
        ClearStarImages();

        if (starText == null)
            return;

        starText.text = string.Empty;
        starText.enabled = false;

        Transform template = detailHandCardPrefab != null
            ? detailHandCardPrefab.transform.Find("StarImage")
            : null;
        Image templateImage = template != null ? template.GetComponent<Image>() : null;
        RectTransform templateRect = template as RectTransform;
        if (templateImage == null || templateRect == null)
        {
            Debug.LogWarning("Detail HandCard Prefab must contain an Image named StarImage.", this);
            return;
        }

        int count = Mathf.Clamp(starCount, 1, 3);
        float width = templateRect.rect.width;
        float spacing = 5f;

        for (int i = 0; i < count; i++)
        {
            GameObject starObject = Instantiate(template.gameObject, starText.transform, false);
            starObject.name = "DetailStarImage_" + (i + 1);
            starObject.SetActive(true);

            RectTransform starRect = starObject.GetComponent<RectTransform>();
            starRect.anchorMin = new Vector2(0f, 0.5f);
            starRect.anchorMax = new Vector2(0f, 0.5f);
            starRect.pivot = new Vector2(0f, 0.5f);
            starRect.sizeDelta = templateRect.sizeDelta;
            starRect.anchoredPosition = new Vector2((width + spacing) * i, 0f);

            Image starImage = starObject.GetComponent<Image>();
            starImage.raycastTarget = false;
            generatedStarImages.Add(starObject);
        }
    }

    private void ClearStarImages()
    {
        foreach (GameObject starImage in generatedStarImages)
        {
            if (starImage != null)
            {
                starImage.SetActive(false);
                Destroy(starImage);
            }
        }
        generatedStarImages.Clear();
    }

    private Image FindDetailCardIcon(Transform parent)
    {
        if (parent == null) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == "Icon")
            {
                Image image = child.GetComponent<Image>();
                if (image != null) return image;
            }

            Image nestedImage = FindDetailCardIcon(child);
            if (nestedImage != null) return nestedImage;
        }

        return null;
    }

    private void PrepareDetailHandCard(GameObject card)
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

    private void ApplyDetailCostFrame(HandCard handCard, int cost)
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

    private void SetCompleteUnitButtons(string[] units)
    {
        if (unitHoverInfoPanel != null)
        {
            unitHoverInfoPanel.Hide(null);
        }

        if (unitText != null)
        {
            unitText.text = string.Empty;
        }

        ShopManager shopManager = UsesRosterBoardData() ? null : FindAnyObjectByType<ShopManager>();
        BoardManager boardManager = UsesRosterBoardData() ? null : FindAnyObjectByType<BoardManager>();
        CharacterData[] roster = GetDetailRoster(shopManager);
        List<TagButtonEntry> entries = new List<TagButtonEntry>();

        if (units != null)
        {
            for (int i = 0; i < units.Length; i++)
            {
                string unitName = units[i];
                if (string.IsNullOrWhiteSpace(unitName) ||
                    entries.Exists(entry => entry.tagName == unitName))
                {
                    continue;
                }

                int totalMembers = UsesRosterBoardData()
                    ? CountTagMembers(roster, unitName, true)
                    : shopManager != null ? shopManager.GetSelectedUnitMemberCount(unitName) : 0;
                int boardMembers = UsesRosterBoardData()
                    ? GetRosterBoardTagCount(unitName, true)
                    : GetBoardTagCount(boardManager, unitName, true);

                if (UsesRosterBoardData())
                {
                    if (totalMembers == 0 || boardMembers != totalMembers)
                        continue;
                }
                else if (shopManager != null && !shopManager.IsUnitComplete(unitName))
                {
                    continue;
                }

                entries.Add(new TagButtonEntry
                {
                    tagName = unitName,
                    totalMembers = totalMembers,
                    boardMembers = boardMembers,
                    sourceOrder = i
                });
            }
        }

        entries.Sort((a, b) =>
        {
            int countComparison = a.totalMembers.CompareTo(b.totalMembers);
            return countComparison != 0
                ? countComparison
                : a.sourceOrder.CompareTo(b.sourceOrder);
        });

        if (!ResolveUnitButtonTemplate())
        {
            return;
        }

        while (generatedUnitButtons.Count < entries.Count)
        {
            Button button = Instantiate(unitButtonTemplate, unitList, false);
            button.name = unitButtonTemplate.name + "_Generated_" + generatedUnitButtons.Count;

            TMP_Text nameText = FindButtonText(button, "UnitNameText", 0);
            if (nameText == null)
            {
                Debug.LogError("UnitButton template needs a TMP_Text for the Unit name.", button);
                return;
            }

            TMP_Text countText = FindButtonText(button, "UnitCountText", 1);
            generatedUnitButtons.Add(button);
            generatedUnitNameTexts.Add(nameText);
            generatedUnitCountTexts.Add(countText);
        }

        for (int i = 0; i < generatedUnitButtons.Count; i++)
        {
            bool visible = i < entries.Count;
            SetGeneratedButtonVisible(generatedUnitButtons[i], visible);
            generatedUnitNameTexts[i].text = visible
                ? entries[i].tagName
                : string.Empty;

            if (generatedUnitCountTexts[i] != null)
            {
                generatedUnitCountTexts[i].text = visible
                    ? entries[i].boardMembers + "/" + entries[i].totalMembers
                    : string.Empty;
            }

            if (visible)
            {
                UnitButtonHover hover = generatedUnitButtons[i].GetComponent<UnitButtonHover>();
                if (hover == null)
                {
                    hover = generatedUnitButtons[i].gameObject.AddComponent<UnitButtonHover>();
                }
                hover.Initialize(entries[i].tagName, unitHoverInfoPanel, true);
            }
        }
    }

    private int GetBoardTagCount(BoardManager boardManager, string tagName, bool isUnit)
    {
        if (boardManager == null || boardManager.boardArea == null)
        {
            return 0;
        }

        int count = 0;
        BoardSlot[] boardSlots =
            boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true);

        foreach (BoardSlot slot in boardSlots)
        {
            if (slot == null || slot.placedCharacter == null ||
                slot.placedCharacter.data == null)
            {
                continue;
            }

            string[] tags = isUnit
                ? slot.placedCharacter.data.units
                : slot.placedCharacter.data.attributes;
            if (tags == null) continue;

            foreach (string boardTag in tags)
            {
                if (string.Equals(boardTag, tagName, System.StringComparison.Ordinal))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private TMP_Text FindButtonText(Button button, string expectedName, int fallbackIndex)
    {
        Transform namedTransform = FindChildByName(button.transform, expectedName);
        if (namedTransform != null)
        {
            TMP_Text namedText = namedTransform.GetComponent<TMP_Text>();
            if (namedText != null)
            {
                return namedText;
            }
        }

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        return texts.Length > fallbackIndex
            ? texts[fallbackIndex]
            : null;
    }

    private void SetAttributeButtons(CharacterData character)
    {
        if (attributeText != null)
        {
            attributeText.text = string.Empty;
        }

        if (character == null || character.attributes == null)
        {
            HideGeneratedAttributeButtons();
            return;
        }

        ShopManager shopManager = UsesRosterBoardData() ? null : FindAnyObjectByType<ShopManager>();
        BoardManager boardManager = UsesRosterBoardData() ? null : FindAnyObjectByType<BoardManager>();
        CharacterData[] roster = GetDetailRoster(shopManager);
        List<TagButtonEntry> entries = new List<TagButtonEntry>();

        for (int i = 0; i < character.attributes.Length; i++)
        {
            string attributeName = character.attributes[i];
            if (string.IsNullOrWhiteSpace(attributeName) ||
                entries.Exists(entry => entry.tagName == attributeName))
            {
                continue;
            }

            int totalMembers = CountAttributeMembers(roster, attributeName);
            if (roster != null && totalMembers < 2)
            {
                continue;
            }

            int boardMembers = UsesRosterBoardData()
                ? GetRosterBoardTagCount(attributeName, false)
                : GetBoardTagCount(boardManager, attributeName, false);
            if (UsesRosterBoardData() && boardMembers <= 1)
            {
                continue;
            }

            entries.Add(new TagButtonEntry
            {
                tagName = attributeName,
                totalMembers = totalMembers,
                boardMembers = boardMembers,
                sourceOrder = i
            });
        }

        entries.Sort((a, b) =>
        {
            int countComparison = b.totalMembers.CompareTo(a.totalMembers);
            return countComparison != 0
                ? countComparison
                : a.sourceOrder.CompareTo(b.sourceOrder);
        });

        if (!ResolveAttributeButtonTemplate())
        {
            return;
        }

        while (generatedAttributeButtons.Count < entries.Count)
        {
            Button button = Instantiate(attributeButtonTemplate, attributeList, false);
            button.name = attributeButtonTemplate.name + "_Generated_" + generatedAttributeButtons.Count;

            TMP_Text nameText = FindButtonText(button, "AttributeNameText", 0);
            if (nameText == null)
            {
                Debug.LogError("AttributeButton template needs a TMP_Text for the Attribute name.", button);
                return;
            }

            TMP_Text countText = FindButtonText(button, "AttributeCountText", 1);
            generatedAttributeButtons.Add(button);
            generatedAttributeNameTexts.Add(nameText);
            generatedAttributeCountTexts.Add(countText);
        }

        for (int i = 0; i < generatedAttributeButtons.Count; i++)
        {
            bool visible = i < entries.Count;
            SetGeneratedButtonVisible(generatedAttributeButtons[i], visible);
            generatedAttributeNameTexts[i].text = visible
                ? entries[i].tagName
                : string.Empty;

            if (generatedAttributeCountTexts[i] != null)
            {
                generatedAttributeCountTexts[i].text = visible
                    ? entries[i].boardMembers + "/" + (UsesRosterBoardData()
                        ? entries[i].boardMembers
                        : entries[i].totalMembers)
                    : string.Empty;
            }

            if (visible)
            {
                UnitButtonHover hover = generatedAttributeButtons[i].GetComponent<UnitButtonHover>();
                if (hover == null)
                {
                    hover = generatedAttributeButtons[i].gameObject.AddComponent<UnitButtonHover>();
                }

                hover.Initialize(entries[i].tagName, unitHoverInfoPanel, false);
            }
        }
    }

    private bool UsesRosterBoardData()
    {
        return fullRosterSource != null && rosterBoardDisplay != null;
    }

    private CharacterData[] GetDetailRoster(ShopManager shopManager)
    {
        if (fullRosterSource != null)
        {
            if (fullRosterSource.allCharacters != null && fullRosterSource.allCharacters.Length > 0)
                return fullRosterSource.allCharacters;
            if (fullRosterSource.characterSource != null && fullRosterSource.characterSource.fullCharacterRoster != null)
                return fullRosterSource.characterSource.fullCharacterRoster;
        }
        return shopManager != null ? shopManager.characters : null;
    }

    private int CountTagMembers(CharacterData[] roster, string tagName, bool isUnit)
    {
        if (roster == null) return 0;
        int count = 0;
        foreach (CharacterData character in roster)
        {
            string[] tags = character == null ? null : isUnit ? character.units : character.attributes;
            if (tags == null) continue;
            foreach (string tag in tags)
            {
                if (string.Equals(tag, tagName, System.StringComparison.Ordinal))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private int GetRosterBoardTagCount(string tagName, bool isUnit)
    {
        if (rosterBoardDisplay == null || rosterBoardDisplay.slotContainer == null)
            return 0;

        int count = 0;
        RosterBoardSlot[] slots = rosterBoardDisplay.slotContainer.GetComponentsInChildren<RosterBoardSlot>(true);
        foreach (RosterBoardSlot slot in slots)
        {
            if (slot == null || slot.characterData == null) continue;
            string[] tags = isUnit ? slot.characterData.units : slot.characterData.attributes;
            if (tags == null) continue;
            foreach (string tag in tags)
            {
                if (string.Equals(tag, tagName, System.StringComparison.Ordinal))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private int CountAttributeMembers(CharacterData[] roster, string attributeName)
    {
        if (roster == null) return 0;

        int count = 0;
        foreach (CharacterData member in roster)
        {
            if (member == null || member.attributes == null) continue;
            foreach (string attribute in member.attributes)
            {
                if (string.Equals(attribute, attributeName, System.StringComparison.Ordinal))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private bool ResolveAttributeButtonTemplate()
    {
        if (attributeList == null && attributeButtonTemplate != null)
        {
            attributeList = attributeButtonTemplate.transform.parent;
        }
        if (attributeList == null)
        {
            attributeList = FindChildByName(transform, "AttributeList");
        }

        if (attributeButtonTemplate == null && attributeList != null)
        {
            Transform templateTransform = FindChildByName(attributeList, "AttributeButton");
            if (templateTransform != null)
            {
                attributeButtonTemplate = templateTransform.GetComponent<Button>();
            }
        }

        if (attributeList == null || attributeButtonTemplate == null)
        {
            Debug.LogError("AttributeList or its AttributeButton template could not be found. Assign them in CharacterDetailPanel.", this);
            return false;
        }

        if (FindButtonText(attributeButtonTemplate, "AttributeNameText", 0) == null)
        {
            Debug.LogError("AttributeButton template needs a TMP_Text for the Attribute name.", attributeButtonTemplate);
            return false;
        }

        SetGeneratedButtonVisible(attributeButtonTemplate, false);
        return true;
    }

    private void HideGeneratedAttributeButtons()
    {
        for (int i = 0; i < generatedAttributeButtons.Count; i++)
        {
            generatedAttributeNameTexts[i].text = string.Empty;
            if (generatedAttributeCountTexts[i] != null)
            {
                generatedAttributeCountTexts[i].text = string.Empty;
            }
            SetGeneratedButtonVisible(generatedAttributeButtons[i], false);
        }
    }

    private bool ResolveUnitButtonTemplate()
    {
        if (unitList == null && unitButtonTemplate != null)
        {
            unitList = unitButtonTemplate.transform.parent;
        }
        if (unitList == null)
        {
            unitList = FindChildByName(transform, "UnitList");
        }

        if (unitButtonTemplate == null && unitList != null)
        {
            Transform templateTransform = FindChildByName(unitList, "UnitButton");
            if (templateTransform != null)
            {
                unitButtonTemplate = templateTransform.GetComponent<Button>();
            }
        }

        if (unitList == null || unitButtonTemplate == null)
        {
            Debug.LogError("UnitList or its UnitButton template could not be found. Assign them in CharacterDetailPanel.", this);
            return false;
        }

        TMP_Text templateText = FindButtonText(unitButtonTemplate, "UnitNameText", 0);
        if (templateText == null)
        {
            Debug.LogError("UnitButton template needs a child TMP_Text component.", unitButtonTemplate);
            return false;
        }

        SetGeneratedButtonVisible(unitButtonTemplate, false);
        return true;
    }

    private Transform FindChildByName(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void SetGeneratedButtonVisible(Button button, bool visible)
    {
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = button.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = button.gameObject.AddComponent<LayoutElement>();
        }
        layout.ignoreLayout = !visible;
    }

    public void Clear()
    {
        currentCharacter = null;
        currentOwnedCharacter = null;
        if (unitHoverInfoPanel != null)
        {
            unitHoverInfoPanel.Hide(null);
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = detailHandCardPrefab == null;
        }

        if (generatedDetailHandCard != null)
        {
            generatedDetailHandCard.SetActive(false);
        }

        if (nameText != null)
        {
            nameText.text = "";
        }

        if (starText != null)
        {
            starText.text = "";
            starText.enabled = true;
        }
        ClearStarImages();

        if (costText != null)
        {
            costText.text = "";
        }

        if (defenseText != null)
        {
            defenseText.text = "";
        }

        if (attackText != null)
        {
            attackText.text = "";
        }

        if (attributeText != null)
        {
            attributeText.text = "";
        }

        if (unitText != null)
        {
            unitText.text = "";
        }

        for (int i = 0; i < generatedUnitButtons.Count; i++)
        {
            generatedUnitNameTexts[i].text = string.Empty;
            if (generatedUnitCountTexts[i] != null)
            {
                generatedUnitCountTexts[i].text = string.Empty;
            }
            SetGeneratedButtonVisible(generatedUnitButtons[i], false);
        }

        HideGeneratedAttributeButtons();
        gameObject.SetActive(false);
    }
}