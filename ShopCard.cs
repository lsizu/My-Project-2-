using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShopCard :
    MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Card Display")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text costText;
    public TMP_Text starText;
    public TMP_Text atkDefText;
    public CharacterStatSettings characterStatSettings;

    [Header("Cost Sprites")]
    public Sprite cost1Sprite;
    public Sprite cost2Sprite;
    public Sprite cost3Sprite;
    public Sprite cost4Sprite;
    public Sprite cost5Sprite;

    private Image cardImage;
    private GameObject lightingPanel;

    private CharacterData characterData;

    private ShopSlot shopSlot;

    private float lightingRefreshTimer;

    private const float LightingRefreshInterval = 0.2f;

    private void Awake()
    {
        cardImage = GetComponent<Image>();
        if (atkDefText == null)
            atkDefText = FindText("AtkDefText");

        Transform lightingTransform = transform.Find("Lighting");
        lightingPanel = lightingTransform != null
            ? lightingTransform.gameObject
            : null;
    }

    private void Update()
    {
        lightingRefreshTimer += Time.unscaledDeltaTime;
        if (lightingRefreshTimer < LightingRefreshInterval)
            return;

        lightingRefreshTimer = 0f;
        UpdateLighting();
    }

    public void SetCharacter(
        CharacterData data)
    {
        characterData = data;

        nameText.text =
            data.characterName;

        costText.text =
            data.cost.ToString();

        starText.text =
            "STAR 1";

        UpdateStatTexts(data.cost);

        switch (data.cost)
        {
            case 1:
                cardImage.sprite = cost1Sprite;
                break;

            case 2:
                cardImage.sprite = cost2Sprite;
                break;

            case 3:
                cardImage.sprite = cost3Sprite;
                break;

            case 4:
                cardImage.sprite = cost4Sprite;
                break;

            case 5:
                cardImage.sprite = cost5Sprite;
                break;
        }

        if (data.icon != null)
        {
            iconImage.sprite =
                data.icon;
        }

        gameObject.SetActive(true);
        UpdateLighting();
    }

    public void SetShopSlot(
        ShopSlot slot)
    {
        shopSlot = slot;
    }

    public ShopSlot GetShopSlot()
    {
        return shopSlot;
    }

    public void ClearCharacter()
    {
        characterData = null;

        nameText.text = "";
        costText.text = "";
        starText.text = "";
        if (atkDefText != null)
            atkDefText.text = "";

        iconImage.sprite = null;
        if (lightingPanel != null)
            lightingPanel.SetActive(false);

        gameObject.SetActive(false);
    }

    private void UpdateStatTexts(int cost)
    {
        if (atkDefText == null)
        {
            Debug.LogError("AtkDefText is not assigned or found on ShopCard.", this);
            return;
        }

        if (characterStatSettings == null)
            characterStatSettings = CharacterStatSettings.FindForCurrentScene();

        if (characterStatSettings == null ||
            !characterStatSettings.TryGetStatOptions(
                cost,
                1,
                out CharacterStatSettings.StarStats[] options))
        {
            Debug.LogError(
                "Character stat options are missing for Cost " + cost + ", Star 1.",
                this);
            return;
        }

        List<string> statLines = new List<string>();
        foreach (CharacterStatSettings.StarStats option in options)
        {
            if (option != null && option.weight > 0)
                statLines.Add(option.attack + " " + option.defense);
        }

        atkDefText.text = string.Join("\n", statLines);
    }

    private TMP_Text FindText(string childName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text.gameObject.name == childName)
                return text;
        }

        return null;
    }

    private void UpdateLighting()
    {
        if (lightingPanel == null || characterData == null)
            return;

        bool hasMatchingCharacterInHandOrBoard =
            GetMatchingCharacterCountInHandAndBoard() >= 2;
        bool isInRosterBoard = IsCharacterInRosterBoard();
        lightingPanel.SetActive(hasMatchingCharacterInHandOrBoard || isInRosterBoard);
    }

    private int GetMatchingCharacterCountInHandAndBoard()
    {
        int count = 0;

        HandManager handManager = FindAnyObjectByType<HandManager>();
        if (handManager != null && handManager.handArea != null)
        {
            HandSlot[] handSlots =
                handManager.handArea.GetComponentsInChildren<HandSlot>(true);
            foreach (HandSlot slot in handSlots)
            {
                if (slot != null &&
                    !slot.IsEmpty() &&
                    slot.ownedCharacter != null &&
                    slot.ownedCharacter.data == characterData)
                {
                    count++;
                }
            }
        }

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        if (boardManager != null && boardManager.boardArea != null)
        {
            BoardSlot[] boardSlots =
                boardManager.boardArea.GetComponentsInChildren<BoardSlot>(true);
            foreach (BoardSlot slot in boardSlots)
            {
                if (slot != null &&
                    !slot.IsEmpty() &&
                    slot.placedCharacter.data == characterData)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private bool IsCharacterInRosterBoard()
    {
        RosterBoardDisplay rosterBoard =
            FindAnyObjectByType<RosterBoardDisplay>(FindObjectsInactive.Include);
        if (rosterBoard == null)
            return false;

        Transform slotContainer = rosterBoard.slotContainer != null
            ? rosterBoard.slotContainer
            : rosterBoard.transform;
        RosterBoardSlot[] rosterSlots =
            slotContainer.GetComponentsInChildren<RosterBoardSlot>(true);

        foreach (RosterBoardSlot slot in rosterSlots)
        {
            if (slot != null && slot.characterData == characterData)
                return true;
        }

        return false;
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (characterData == null)
        {
            return;
        }

        BoardManager boardManager =
            FindAnyObjectByType<BoardManager>();

        if (boardManager == null)
        {
            Debug.LogError(
                "BoardManager��������܂���I"
            );

            return;
        }

        if (eventData.button ==
            PointerEventData.InputButton.Right)
        {
            boardManager.ShowShopCharacterDetail(
               characterData,
               this
            );

            return;
        }
    }

    private bool isDragging = false;
    private UIDragCardPreview.Session dragPreviewSession;

    public void OnBeginDrag(
    PointerEventData eventData)
    {
        Debug.Log(
            "ShopCard OnBeginDrag"
        );

        if (characterData == null)
        {
            return;
        }

        isDragging = true;
        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        if (boardManager != null)
            boardManager.NotifyCharacterDragStarted();
        dragPreviewSession = UIDragCardPreview.Begin(gameObject, eventData);

        Debug.Log(
            characterData.characterName +
            " �̃h���b�O���J�n���܂����B"
        );
    }

    public void OnDrag(
        PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }
        UIDragCardPreview.Update(dragPreviewSession, eventData);
    }

    public void OnEndDrag(
        PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        UIDragCardPreview.End(ref dragPreviewSession);

        BoardManager boardManager =
            FindAnyObjectByType<BoardManager>();
        if (boardManager != null)
            boardManager.NotifyCharacterDragEnded();

        GameObject targetObject =
            eventData.pointerCurrentRaycast.gameObject;

        if (targetObject == null)
        {
            return;
        }

        if (UIDragCardPreview.IsRosterBoardTarget(targetObject))
            return;

        if (boardManager == null)
        {
            return;
        }

        // -----------------------------------------
        // �V���b�v �� �莝��
        // -----------------------------------------

        HandSlot targetHandSlot =
            targetObject.GetComponentInParent<HandSlot>();

        if (targetHandSlot != null)
        {
          

            boardManager.ClearSelection(clearCharacterDetail: false);

            boardManager.selectionType =
                BoardManager.SelectionType.Shop;

            boardManager.selectedShopCharacter =
                characterData;

            boardManager.selectedShopCard =
                this;

            boardManager.BuySelectedShopCharacterToHand(
                targetHandSlot,
                clearCharacterDetail: false
            );

            return;
        }

        // -----------------------------------------
        // �V���b�v �� �Ֆ�
        // -----------------------------------------

        BoardSlot targetBoardSlot =
            targetObject.GetComponentInParent<BoardSlot>();

        if (targetBoardSlot != null)
        {
           

            boardManager.ClearSelection(clearCharacterDetail: false);

            boardManager.selectionType =
                BoardManager.SelectionType.Shop;

            boardManager.selectedShopCharacter =
                characterData;

            boardManager.selectedShopCard =
                this;

            boardManager.BuySelectedShopCharacterToBoard(
                targetBoardSlot,
                clearCharacterDetail: false
            );

            return;
        }
    }
}
