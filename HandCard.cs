using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HandCard :
    MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text starText;
    public TMP_Text costText;

    [Header("コスト画像")]
    public Sprite cost1Sprite;
    public Sprite cost2Sprite;
    public Sprite cost3Sprite;
    public Sprite cost4Sprite;
    public Sprite cost5Sprite;

        [Header("Frame")]
    public Image frameImage;

    private HandSlot handSlot;
    private CharacterData readOnlyCharacterData;
    private RosterBoardSlot rosterBoardSlot;
    private CharacterDetailPanel characterDetailPanelOverride;

    public void SetReadOnlyCharacter(CharacterData characterData)
    {
        readOnlyCharacterData = characterData;
        handSlot = null;
    }

    public void SetRosterBoardSlot(RosterBoardSlot slot)
    {
        rosterBoardSlot = slot;
    }

    public void SetCharacterDetailPanel(CharacterDetailPanel panel)
    {
        characterDetailPanelOverride = panel;
    }



    public void SetCharacter(
        OwnedCharacter character)
    {
        if (character == null ||
            character.data == null)
        {
            return;
        }

        CharacterData characterData =
            character.data;

        nameText.text =
            characterData.characterName;

        if (starText != null)
        {
            starText.text = string.Empty;
        }

        SetStarImages(character.star);

        costText.text =
            characterData.cost.ToString();

        switch (characterData.cost)
        {
            case 1:
                frameImage.sprite = cost1Sprite;
                break;

            case 2:
                frameImage.sprite = cost2Sprite;
                break;

            case 3:
                frameImage.sprite = cost3Sprite;
                break;

            case 4:
                frameImage.sprite = cost4Sprite;
                break;

            case 5:
                frameImage.sprite = cost5Sprite;
                break;
        }

        if (characterData.icon != null)
        {
            iconImage.sprite =
                characterData.icon;
        }
        else
        {
            iconImage.sprite = null;
        }
    }

    private void SetStarImages(int starCount)
    {
        Transform baseStar = transform.Find("StarImage");
        if (baseStar == null)
        {
            Debug.LogWarning("StarImage child was not found.", this);
            return;
        }

        Image baseImage = baseStar.GetComponent<Image>();
        RectTransform baseRect = baseStar as RectTransform;
        if (baseImage == null || baseRect == null)
        {
            Debug.LogWarning("StarImage needs an Image and RectTransform.", this);
            return;
        }

        Transform starParent = baseStar.parent;
        for (int i = starParent.childCount - 1; i >= 0; i--)
        {
            Transform child = starParent.GetChild(i);
            if (child != baseStar && child.name.StartsWith("StarImage_Dynamic_"))
            {
                Destroy(child.gameObject);
            }
        }

        int count = Mathf.Clamp(starCount, 1, 3);
        baseStar.gameObject.SetActive(true);
        float step = baseRect.rect.width + 5f;

        for (int i = 1; i < count; i++)
        {
            GameObject extra = Instantiate(baseStar.gameObject, starParent, false);
            extra.name = "StarImage_Dynamic_" + (i + 1);
            RectTransform extraRect = extra.GetComponent<RectTransform>();
            extraRect.anchoredPosition = baseRect.anchoredPosition + Vector2.right * step * i;
            extraRect.localRotation = baseRect.localRotation;
            extraRect.localScale = baseRect.localScale;
            extra.GetComponent<Image>().raycastTarget = false;
            extra.SetActive(true);
        }
    }

    public void SetHandSlot(
        HandSlot slot)
    {
        handSlot = slot;
    }

    public void OnPointerClick(
    PointerEventData eventData)
    {
        if (handSlot == null)
        {
            if (eventData.button == PointerEventData.InputButton.Right &&
                readOnlyCharacterData != null)
            {
                if (rosterBoardSlot != null)
                {
                    rosterBoardSlot.ShowCharacterDetail();
                    return;
                }

                CharacterDetailPanel detailPanel = characterDetailPanelOverride;
                if (detailPanel == null)
                {
                    CharacterFullRosterListDisplay rosterList = FindAnyObjectByType<CharacterFullRosterListDisplay>();
                    if (rosterList != null)
                        detailPanel = rosterList.characterDetailPanel;
                }
                if (detailPanel == null)
                {
                    BoardManager detailBoardManager = FindAnyObjectByType<BoardManager>();
                    if (detailBoardManager != null)
                        detailPanel = detailBoardManager.characterDetailPanel;
                }

                if (detailPanel != null)
                    detailPanel.ShowShopCharacter(readOnlyCharacterData);
            }
            return;
        }

        BoardManager boardManager =
            FindAnyObjectByType<BoardManager>();

        if (boardManager == null)
        {
            Debug.LogError(
                "BoardManagerが見つかりません！"
            );

            return;
        }

        if (eventData.button ==
            PointerEventData.InputButton.Right)
        {
            if (handSlot.IsEmpty())
            {
                return;
            }

            boardManager.ShowHandCharacterDetail(
                handSlot.ownedCharacter,
                handSlot
            );

            return;
        }
    }

    private bool isDragging = false;
    private bool isRosterDragging = false;
    private UIDragCardPreview.Session dragPreviewSession;

    public void OnBeginDrag(
    PointerEventData eventData)
    {
        if (handSlot == null)
        {
            if (readOnlyCharacterData == null)
                return;

            RosterBoardDisplay rosterBoard = FindAnyObjectByType<RosterBoardDisplay>();
            if (rosterBoard == null)
                return;

            if (rosterBoardSlot != null)
                rosterBoard.SelectCharacterFromSlot(rosterBoardSlot);
            else
                rosterBoard.SelectCharacter(readOnlyCharacterData);
            isRosterDragging = true;
            dragPreviewSession = UIDragCardPreview.Begin(gameObject, eventData);
            return;
        }

        if (handSlot.IsEmpty()) return;

        BoardManager boardManager =
            FindAnyObjectByType<BoardManager>();

        if (boardManager == null)
        {
            return;
        }

        boardManager.NotifyCharacterDragStarted();

        // ドラッグ中のキャラクターを選択状態にする
        boardManager.ClearSelection(clearCharacterDetail: false);

        boardManager.selectionType =
            BoardManager.SelectionType.Hand;

        boardManager.selectedHandSlot =
            handSlot;

        isDragging = true;
        dragPreviewSession = UIDragCardPreview.Begin(gameObject, eventData);

        Debug.Log(
            handSlot.ownedCharacter.data.characterName +
            " のドラッグを開始しました。"
        );
    }

    public void OnDrag(
    PointerEventData eventData)
    {
        if (isRosterDragging)
        {
            UIDragCardPreview.Update(dragPreviewSession, eventData);
            return;
        }

        if (!isDragging)
        {
            return;
        }

        UIDragCardPreview.Update(dragPreviewSession, eventData);
    }

    public void OnEndDrag(
    PointerEventData eventData)
    {
        if (isRosterDragging)
        {
            isRosterDragging = false;
            UIDragCardPreview.End(ref dragPreviewSession);

            GameObject dropObject = eventData.pointerCurrentRaycast.gameObject;
            RosterBoardSlot targetRosterSlot = dropObject != null
                ? dropObject.GetComponentInParent<RosterBoardSlot>()
                : null;
            if (targetRosterSlot != null)
            {
                targetRosterSlot.ClickSlot();
            }
            else if (rosterBoardSlot != null)
            {
                rosterBoardSlot.ClearCharacter();
            }
            return;
        }

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
        // 手持ち → 手持ち
        // -----------------------------------------

        HandSlot targetHandSlot =
            targetObject.GetComponentInParent<HandSlot>();

        if (targetHandSlot != null)
        {
            boardManager.MoveHandCharacter(
                handSlot,
                targetHandSlot,
                clearCharacterDetail: false
            );

            return;
        }

        // -----------------------------------------
        // 手持ち → 盤面
        // -----------------------------------------

        BoardSlot targetBoardSlot =
            targetObject.GetComponentInParent<BoardSlot>();

        if (targetBoardSlot != null)
        {
            boardManager.ClearSelection(clearCharacterDetail: false);

            boardManager.selectionType =
                BoardManager.SelectionType.Hand;

            boardManager.selectedHandSlot =
                handSlot;

            boardManager.PlaceSelectedHandCharacter(
                targetBoardSlot,
                clearCharacterDetail: false
            );

            return;
        }
    }

}
