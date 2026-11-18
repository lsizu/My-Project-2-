using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DeckSaveLoad : MonoBehaviour
{
    private const int DeckSize = 30;
    private const int BuildingSlotCount = 5;
    private const string LegacySaveKey = "CompositionScene.SavedDeck.v1";
    private const string SaveKeyPrefix = "CompositionScene.SavedDeck.v2.";

    [Header("編成")]
    public RosterBoardDisplay rosterBoardDisplay;
    public CharacterFullRosterListDisplay rosterList;

    [Header("編成保存枠")]
    public GameObject buildPanel;
    public GameObject buildingPrefab;

    [Header("ボタン")]
    public Button saveButton;
    public Button loadButton;
    public TMP_Text statusText;
    public TMP_InputField deckNameTextbox;

    private readonly List<TMP_Text> formationNameLabels = new List<TMP_Text>();
    private readonly List<TMP_Text> formationNumberLabels = new List<TMP_Text>();
    private readonly List<GameObject> contentPanels = new List<GameObject>();
    private int selectedBuildingIndex = -1;
    private int copySourceBuildingIndex = -1;

    [Serializable]
    private class SavedDeck
    {
        public List<SavedSlot> slots = new List<SavedSlot>();
        public string formationName;
    }

    [Serializable]
    private class SavedSlot
    {
        public int slotIndex;
        public string characterAssetName;
    }

    private void Start()
    {
        if (rosterBoardDisplay == null)
            rosterBoardDisplay = FindAnyObjectByType<RosterBoardDisplay>();
        if (rosterList == null)
            rosterList = FindAnyObjectByType<CharacterFullRosterListDisplay>();

        MigrateLegacyDeck();
        if (!InitializeBuildingSlots())
            return;

        SetDeckControlsActive(false);

        if (saveButton != null)
            saveButton.onClick.AddListener(SaveDeck);
        if (loadButton != null)
            loadButton.onClick.AddListener(OpenBuildPanel);

        UpdateBuildingSlotLabels();
    }

    private void OnDestroy()
    {
        if (saveButton != null)
            saveButton.onClick.RemoveListener(SaveDeck);
        if (loadButton != null)
            loadButton.onClick.RemoveListener(OpenBuildPanel);
    }

    public void SaveDeck()
    {
        if (!TrySaveSelectedDeckForLaunch(out _))
            return;

        selectedBuildingIndex = -1;
        SetBuildPanelActive(true);
    }

    public bool TrySaveSelectedDeckForLaunch(out string error)
    {
        SetDeckControlsActive(false);
        error = null;

        if (selectedBuildingIndex < 0 || selectedBuildingIndex >= BuildingSlotCount)
            error = "保存先の編成枠を選択してから出撃してください。";
        else if (rosterBoardDisplay == null || rosterBoardDisplay.slotContainer == null)
            error = "編成盤面が見つかりません。";

        if (error != null)
        {
            ShowStatus(error);
            return false;
        }

        List<RosterBoardSlot> boardSlots = GetBoardSlots();
        if (boardSlots.Count != DeckSize)
            error = "編成枠が30枠ではありません。";

        SavedDeck deck = new SavedDeck();
        HashSet<CharacterData> uniqueCharacters = new HashSet<CharacterData>();
        HashSet<string> uniqueNames = new HashSet<string>(StringComparer.Ordinal);
        if (error == null)
        {
            foreach (RosterBoardSlot slot in boardSlots)
            {
                CharacterData character = slot.characterData;
                if (character != null &&
                    (!uniqueCharacters.Add(character) ||
                     !uniqueNames.Add(character.name)))
                {
                    error = "同じキャラクターを複数の枠に保存できません。";
                    break;
                }

                deck.slots.Add(new SavedSlot
                {
                    slotIndex = slot.slotIndex,
                    characterAssetName = character != null ? character.name : string.Empty
                });
            }
        }

        if (error != null)
        {
            ShowStatus(error);
            return false;
        }

        string enteredName = deckNameTextbox != null ? deckNameTextbox.text.Trim() : string.Empty;
        deck.formationName = !string.IsNullOrWhiteSpace(enteredName)
            ? enteredName
            : GetSavedFormationName(selectedBuildingIndex);
        PlayerPrefs.SetString(GetSaveKey(selectedBuildingIndex), JsonUtility.ToJson(deck));
        PlayerPrefs.Save();
        UpdateBuildingSlotLabels();
        ShowStatus("編成" + (selectedBuildingIndex + 1) + "を出撃前に保存しました。");
        return true;
    }

    public void LoadDeck()
    {
        OpenBuildPanel();
    }

    public void OpenBuildPanel()
    {
        SetDeckControlsActive(false);
        selectedBuildingIndex = -1;
        UpdateBuildingSlotLabels();
        SetBuildPanelActive(true);
    }

    private void OnBuildingClicked(int buildingIndex)
    {
        if (copySourceBuildingIndex >= 0)
        {
            CopyBuildingTo(buildingIndex);
            return;
        }

        if (!PlayerPrefs.HasKey(GetSaveKey(buildingIndex)))
        {
            if (rosterBoardDisplay == null ||
                !rosterBoardDisplay.ApplySavedDeck(new CharacterData[DeckSize]))
            {
                ShowStatus("空の編成を盤面に反映できませんでした。");
                return;
            }

            ShowStatus("編成" + (buildingIndex + 1) + "は未保存です。");
            return;
        }

        LoadDeckFromSlot(buildingIndex, false, true);
    }

    private void OnDeleteBuildingClicked(int buildingIndex)
    {
        string saveKey = GetSaveKey(buildingIndex);
        if (!PlayerPrefs.HasKey(saveKey))
        {
            ShowStatus("編成" + (buildingIndex + 1) + "はすでに空です。");
            return;
        }

        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();

        if (selectedBuildingIndex == buildingIndex)
            selectedBuildingIndex = -1;
        if (copySourceBuildingIndex == buildingIndex)
            copySourceBuildingIndex = -1;

        UpdateBuildingSlotLabels();
        ShowStatus("編成" + (buildingIndex + 1) + "を削除しました。");
    }

    private void OnCopyBuildingClicked(int buildingIndex)
    {
        if (!PlayerPrefs.HasKey(GetSaveKey(buildingIndex)))
        {
            ShowStatus("コピー元の編成" + (buildingIndex + 1) + "は未保存です。");
            return;
        }

        copySourceBuildingIndex = buildingIndex;
        ShowStatus("編成" + (buildingIndex + 1) + "をコピー元に選択しました。コピー先のBuildingを押してください。");
    }

    private void CopyBuildingTo(int destinationIndex)
    {
        int sourceIndex = copySourceBuildingIndex;
        copySourceBuildingIndex = -1;

        if (sourceIndex == destinationIndex)
        {
            ShowStatus("コピー元とは別のBuildingを選択してください。");
            return;
        }

        string sourceKey = GetSaveKey(sourceIndex);
        if (!PlayerPrefs.HasKey(sourceKey))
        {
            ShowStatus("コピー元の編成" + (sourceIndex + 1) + "は見つかりません。");
            return;
        }

        PlayerPrefs.SetString(GetSaveKey(destinationIndex), PlayerPrefs.GetString(sourceKey));
        PlayerPrefs.Save();
        UpdateBuildingSlotLabels();
        ShowStatus("編成" + (sourceIndex + 1) + "を編成" + (destinationIndex + 1) + "へコピーしました。");
    }

    private void OnFormationChangeClicked(int buildingIndex)
    {
        selectedBuildingIndex = buildingIndex;
        ShowStatus("編成" + (buildingIndex + 1) + "を保存先に選択しました。Deck1Saveを押してください。");
        SetBuildPanelActive(false);
        SetDeckControlsActive(true);
    }

    private void LoadDeckFromSlot(
        int buildingIndex,
        bool closeBuildPanel,
        bool keepBuildingSelected = false)
    {
        SetDeckControlsActive(false);

        string saveKey = GetSaveKey(buildingIndex);
        if (!PlayerPrefs.HasKey(saveKey))
        {
            ShowStatus("編成" + (buildingIndex + 1) + "は未保存です。");
            return;
        }

        if (rosterBoardDisplay == null || rosterList == null)
        {
            ShowStatus("編成盤面または全キャラクター名簿が見つかりません。");
            return;
        }

        SavedDeck deck;
        try
        {
            deck = JsonUtility.FromJson<SavedDeck>(PlayerPrefs.GetString(saveKey));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("保存データを読み込めませんでした: " + exception.Message, this);
            ShowStatus("保存データを読み込めませんでした。");
            return;
        }

        List<RosterBoardSlot> boardSlots = GetBoardSlots();
        if (deck == null || deck.slots == null ||
            deck.slots.Count != DeckSize || boardSlots.Count != DeckSize)
        {
            ShowStatus("保存データまたは編成枠が30体分ではありません。");
            return;
        }

        Dictionary<string, CharacterData> rosterByAssetName =
            new Dictionary<string, CharacterData>(StringComparer.Ordinal);
        HashSet<string> ambiguousNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterData character in rosterList.GetFullRoster())
        {
            if (character == null)
                continue;

            if (rosterByAssetName.ContainsKey(character.name))
            {
                rosterByAssetName.Remove(character.name);
                ambiguousNames.Add(character.name);
            }
            else if (!ambiguousNames.Contains(character.name))
            {
                rosterByAssetName.Add(character.name, character);
            }
        }

        CharacterData[] charactersBySlot = new CharacterData[DeckSize];
        HashSet<int> usedSlotIndices = new HashSet<int>();
        HashSet<CharacterData> usedCharacters = new HashSet<CharacterData>();
        foreach (SavedSlot savedSlot in deck.slots)
        {
            if (savedSlot == null ||
                savedSlot.slotIndex < 0 ||
                savedSlot.slotIndex >= DeckSize ||
                !usedSlotIndices.Add(savedSlot.slotIndex))
            {
                ShowStatus("保存データに無効または重複したスロットがあります。");
                return;
            }

            if (string.IsNullOrEmpty(savedSlot.characterAssetName))
                continue;

            if (ambiguousNames.Contains(savedSlot.characterAssetName) ||
                !rosterByAssetName.TryGetValue(savedSlot.characterAssetName, out CharacterData character) ||
                !usedCharacters.Add(character))
            {
                ShowStatus("保存データに無効または重複したキャラクターがあります。");
                return;
            }

            charactersBySlot[savedSlot.slotIndex] = character;
        }

        if (!rosterBoardDisplay.ApplySavedDeck(charactersBySlot))
        {
            ShowStatus("保存デッキを編成盤面に適用できませんでした。");
            return;
        }

        selectedBuildingIndex = keepBuildingSelected ? buildingIndex : -1;
        ShowStatus("編成" + (buildingIndex + 1) + "を読み込みました。");
        if (closeBuildPanel)
            SetBuildPanelActive(false);
    }

    private bool InitializeBuildingSlots()
    {
        if (buildPanel == null)
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name == "Build")
                {
                    buildPanel = candidate.gameObject;
                    break;
                }
            }
        }

        if (buildPanel == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("Canvas is required to create the Build panel.", this);
                return false;
            }

            buildPanel = new GameObject("Build", typeof(RectTransform), typeof(Image));
            buildPanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = buildPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image background = buildPanel.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.82f);
            background.raycastTarget = true;
        }

        contentPanels.Clear();
        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name == "Content" && !candidate.IsChildOf(buildPanel.transform))
                contentPanels.Add(candidate.gameObject);
        }

        if (contentPanels.Count == 0)
        {
            Debug.LogError("No GameObject named Content was found in the scene.", this);
            return false;
        }

        if (buildingPrefab == null)
        {
#if UNITY_EDITOR
            buildingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Building.prefab");
#endif
        }

        if (buildingPrefab == null)
        {
            Debug.LogError("Assign the Building prefab to DeckSaveLoad.", this);
            return false;
        }

        SetBuildPanelActive(true);
        List<Transform> buildingObjects = new List<Transform>();
        foreach (Transform child in buildPanel.transform)
        {
            if (child.name == "Building" || child.name.StartsWith("BuildingSlot_"))
                buildingObjects.Add(child);
        }

        for (int i = buildingObjects.Count; i < BuildingSlotCount; i++)
        {
            GameObject building = Instantiate(buildingPrefab, buildPanel.transform, false);
            buildingObjects.Add(building.transform);
        }

        buildingObjects.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        formationNameLabels.Clear();
        formationNumberLabels.Clear();
        for (int i = 0; i < BuildingSlotCount; i++)
        {
            Transform building = buildingObjects[i];
            building.name = "BuildingSlot_" + (i + 1);
            RectTransform rect = building as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(205f, -28f - i * 108f);
                rect.localScale = Vector3.one * 0.72f;
            }

            foreach (Button childButton in building.GetComponentsInChildren<Button>(true))
            {
                if (childButton.gameObject == building.gameObject)
                    continue;

                string buttonName = childButton.gameObject.name;
                bool isSlotActionButton = buttonName == "FormationChange" ||
                                          buttonName == "Detele" ||
                                          buttonName == "Delete" ||
                                          buttonName == "Copy";
                childButton.enabled = isSlotActionButton;
                Graphic childGraphic = childButton.GetComponent<Graphic>();
                if (childGraphic != null)
                    childGraphic.raycastTarget = isSlotActionButton;
            }
            foreach (Graphic graphic in building.GetComponentsInChildren<Graphic>(true))
                if (graphic.gameObject != building.gameObject &&
                    graphic.transform != building.Find("FormationChange") &&
                    graphic.transform != building.Find("Detele") &&
                    graphic.transform != building.Find("Delete") &&
                    graphic.transform != building.Find("Copy"))
                    graphic.raycastTarget = false;

            GameObject clickArea = new GameObject("BuildingClickArea", typeof(RectTransform), typeof(Image), typeof(Button));
            clickArea.transform.SetParent(building, false);
            RectTransform clickAreaRect = clickArea.GetComponent<RectTransform>();
            clickAreaRect.anchorMin = new Vector2(0f, 1f);
            clickAreaRect.anchorMax = new Vector2(0f, 1f);
            clickAreaRect.pivot = new Vector2(0.5f, 0.5f);
            clickAreaRect.anchoredPosition = new Vector2(250f, -45f);
            clickAreaRect.sizeDelta = new Vector2(1100f, 180f);
            clickArea.transform.SetAsFirstSibling();
            Image clickAreaImage = clickArea.GetComponent<Image>();
            clickAreaImage.color = new Color(1f, 1f, 1f, 0f);
            clickAreaImage.raycastTarget = true;
            Button slotButton = clickArea.GetComponent<Button>();
            slotButton.targetGraphic = clickAreaImage;
            slotButton.transition = Selectable.Transition.None;
            int slotIndex = i;
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => OnBuildingClicked(slotIndex));

            Transform formationChange = building.Find("FormationChange");
            Button formationChangeButton = formationChange != null
                ? formationChange.GetComponent<Button>()
                : null;
            if (formationChangeButton == null)
            {
                Debug.LogError("Building prefab requires a FormationChange Button.", building);
                return false;
            }

            formationChangeButton.onClick.RemoveAllListeners();
            formationChangeButton.onClick.AddListener(
                () => OnFormationChangeClicked(slotIndex));

            Transform deleteTransform = building.Find("Detele");
            if (deleteTransform == null)
                deleteTransform = building.Find("Delete");
            Button deleteButton = deleteTransform != null
                ? deleteTransform.GetComponent<Button>()
                : null;
            if (deleteButton == null)
            {
                Debug.LogError("Building prefab requires a Detele Button.", building);
                return false;
            }

            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => OnDeleteBuildingClicked(slotIndex));

            Transform copyTransform = building.Find("Copy");
            Button copyButton = copyTransform != null
                ? copyTransform.GetComponent<Button>()
                : null;
            if (copyButton == null)
            {
                Debug.LogError("Building prefab requires a Copy Button.", building);
                return false;
            }

            copyButton.onClick.RemoveAllListeners();
            copyButton.onClick.AddListener(() => OnCopyBuildingClicked(slotIndex));

            TMP_Text label = building.Find("FormationName")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                Debug.LogError("Building prefab requires a FormationName TMP_Text.", building);
                return false;
            }
            formationNameLabels.Add(label);

            TMP_Text numberLabel = building.Find("FormationNumber")?.GetComponent<TMP_Text>();
            if (numberLabel == null)
                numberLabel = CreateFormationNumberLabel(building, label);
            formationNumberLabels.Add(numberLabel);
        }

        return true;
    }

    private void SetBuildPanelActive(bool isActive)
    {
        if (buildPanel != null)
            buildPanel.SetActive(isActive);

        foreach (GameObject contentPanel in contentPanels)
            if (contentPanel != null)
                contentPanel.SetActive(!isActive);
    }

    private void SetDeckControlsActive(bool isActive)
    {
        if (saveButton != null)
            saveButton.gameObject.SetActive(isActive);
        if (loadButton != null)
            loadButton.gameObject.SetActive(isActive);
        if (deckNameTextbox != null)
            deckNameTextbox.gameObject.SetActive(isActive);
    }

    private void UpdateBuildingSlotLabels()
    {
        for (int i = 0; i < formationNameLabels.Count; i++)
        {
            string defaultName = "編成" + (i + 1);
            string formationName = defaultName;
            string formationNumber = "0/" + DeckSize;

            string saveKey = GetSaveKey(i);
            if (PlayerPrefs.HasKey(saveKey))
            {
                try
                {
                    SavedDeck deck = JsonUtility.FromJson<SavedDeck>(PlayerPrefs.GetString(saveKey));
                    if (deck != null)
                    {
                        if (!string.IsNullOrWhiteSpace(deck.formationName))
                            formationName = deck.formationName;

                        int count = 0;
                        if (deck.slots != null)
                            foreach (SavedSlot slot in deck.slots)
                                if (slot != null && !string.IsNullOrEmpty(slot.characterAssetName))
                                    count++;
                        formationNumber = count + "/" + DeckSize;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "編成" + (i + 1) + "の保存データを確認できませんでした: " +
                        exception.Message,
                        this);
                    formationName = defaultName;
                    formationNumber = "データエラー";
                }
            }

            if (formationNameLabels[i] != null)
                formationNameLabels[i].text = formationName;
            if (formationNumberLabels[i] != null)
                formationNumberLabels[i].text = formationNumber;
        }
    }

    private TMP_Text CreateFormationNumberLabel(Transform building, TMP_Text nameLabel)
    {
        GameObject labelObject = new GameObject(
            "FormationNumber",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(building, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(120f, -18f);
        rect.sizeDelta = new Vector2(180f, 100f);

        TMP_Text numberLabel = labelObject.GetComponent<TMP_Text>();
        numberLabel.font = nameLabel.font;
        numberLabel.fontSize = nameLabel.fontSize;
        numberLabel.color = nameLabel.color;
        numberLabel.alignment = TextAlignmentOptions.Center;
        numberLabel.raycastTarget = false;
        return numberLabel;
    }

    private string GetSavedFormationName(int buildingIndex)
    {
        string key = GetSaveKey(buildingIndex);
        if (PlayerPrefs.HasKey(key))
        {
            try
            {
                SavedDeck savedDeck = JsonUtility.FromJson<SavedDeck>(PlayerPrefs.GetString(key));
                if (savedDeck != null && !string.IsNullOrWhiteSpace(savedDeck.formationName))
                    return savedDeck.formationName;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "編成" + (buildingIndex + 1) + "の名前を読み込めませんでした: " +
                    exception.Message,
                    this);
            }
        }

        return "編成" + (buildingIndex + 1);
    }

    private void MigrateLegacyDeck()
    {
        string firstSlotKey = GetSaveKey(0);
        if (PlayerPrefs.HasKey(firstSlotKey) || !PlayerPrefs.HasKey(LegacySaveKey))
            return;

        try
        {
            SavedDeck legacyDeck = JsonUtility.FromJson<SavedDeck>(
                PlayerPrefs.GetString(LegacySaveKey));
            if (legacyDeck == null || legacyDeck.slots == null ||
                legacyDeck.slots.Count != DeckSize)
            {
                Debug.LogWarning("Legacy saved deck could not be migrated because its data is incomplete.", this);
                return;
            }

            PlayerPrefs.SetString(firstSlotKey, JsonUtility.ToJson(legacyDeck));
            PlayerPrefs.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Legacy saved deck migration failed: " + exception.Message, this);
        }
    }

    private string GetSaveKey(int buildingIndex)
    {
        return SaveKeyPrefix + buildingIndex;
    }

    private List<RosterBoardSlot> GetBoardSlots()
    {
        List<RosterBoardSlot> slots = new List<RosterBoardSlot>();
        if (rosterBoardDisplay == null || rosterBoardDisplay.slotContainer == null)
            return slots;

        slots.AddRange(rosterBoardDisplay.slotContainer.GetComponentsInChildren<RosterBoardSlot>(true));
        slots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        return slots;
    }

    private void ShowStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.gameObject.SetActive(true);
        }
        else
        {
            Debug.Log(message, this);
        }
    }
}