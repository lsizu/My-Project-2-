using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SellButton :
    MonoBehaviour,
    IDropHandler
{
    private Button button;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (button == null)
        {
            Debug.LogError(
                "SellButton��Button�R���|�[�l���g������܂���I"
            );
        }

        SetInteractable(false);
    }

    void Update()
    {
        UpdateButtonState();
    }

    void UpdateButtonState()
    {
        if (button == null)
        {
            return;
        }

        BoardManager boardManager =
            FindAnyObjectByType<BoardManager>();

        if (boardManager == null)
        {
            SetInteractable(false);
            return;
        }

        OwnedCharacter character =
            boardManager.GetSelectedCharacter();

        // �莝���E�Ֆʂ̃L�����N�^�[��
        // �I�����Ă���ꍇ�̂ݔ��p�\
        bool canSellItem = boardManager.selectionType == BoardManager.SelectionType.Hand &&
                           boardManager.selectedHandSlot != null &&
                           boardManager.selectedHandSlot.itemData != null;
        bool canSell = canSellItem || (character != null &&
            (boardManager.selectionType == BoardManager.SelectionType.Hand ||
             boardManager.selectionType == BoardManager.SelectionType.Board));

        SetInteractable(canSell);
        SetVisible(!boardManager.IsShowingCharacterDetailOnly &&
                   !boardManager.IsSellButtonSuppressedAfterDrag);
    }

    void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
    }

    public void Sell()
    {
        if (button == null ||
            !button.interactable)
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

        if (boardManager.selectionType == BoardManager.SelectionType.Hand &&
            boardManager.selectedHandSlot != null && boardManager.selectedHandSlot.itemData != null)
        {
            ItemData soldItem = boardManager.selectedHandSlot.RemoveItem();
            boardManager.ClearSelection();
            SetInteractable(false);
            Debug.Log("Item " + soldItem.itemName + " ��0G�Ŕ��p���܂����B");
            return;
        }

        OwnedCharacter character = boardManager.GetSelectedCharacter();
        if (character == null) return;

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            Debug.LogError(
                "PlayerData��������܂���I"
            );

            return;
        }

        // ���p����
        if (!player.SellCharacter(character))
        {
            return;
        }

        // �莝���𔄋p�����ꍇ
        if (boardManager.selectionType ==
            BoardManager.SelectionType.Hand)
        {
            if (boardManager.selectedHandSlot != null)
            {
                boardManager.selectedHandSlot.RemoveCharacter();
            }
        }

        // �Ֆʂ𔄋p�����ꍇ
        if (boardManager.selectionType ==
            BoardManager.SelectionType.Board)
        {
            if (boardManager.selectedBoardSlot != null)
            {
                boardManager.selectedBoardSlot.ClearCharacterVisual();
            }
        }

        // �I������
        boardManager.ClearSelection();

        // ���p����͊m���ɖ�����
        SetInteractable(false);
    }

    public void OnDrop(
    PointerEventData eventData)
    {
        GameObject draggedObject =
            eventData.pointerDrag;

        if (draggedObject == null)
        {
            return;
        }

        UIDragCardPreview.EndForSource(draggedObject);

        BoardManager boardManager =
            FindAnyObjectByType<BoardManager>();

        if (boardManager == null)
        {
            Debug.LogError(
                "BoardManager��������܂���I"
            );

            return;
        }

        // -----------------------------------------
        // �莝���L�����N�^�[
        // -----------------------------------------

        HandSlot handSlot =
            draggedObject.GetComponentInParent<HandSlot>();

        if (handSlot != null)
        {
            if (handSlot.IsEmpty())
            {
                return;
            }

            boardManager.ClearSelection();

            boardManager.selectionType =
                BoardManager.SelectionType.Hand;

            boardManager.selectedHandSlot =
                handSlot;

            Debug.Log(
                "�莝���L�����N�^�[�𔄋p�Ώۂɂ��܂����B"
            );

            SellDraggedCharacter();

            return;
        }

        // -----------------------------------------
        // �ՖʃL�����N�^�[
        // -----------------------------------------

        BoardSlot boardSlot =
            draggedObject.GetComponentInParent<BoardSlot>();

        if (boardSlot != null)
        {
            if (boardSlot.IsEmpty())
            {
                return;
            }

            boardManager.ClearSelection();

            boardManager.selectionType =
                BoardManager.SelectionType.Board;

            boardManager.selectedBoardSlot =
                boardSlot;

            Debug.Log(
                "�ՖʃL�����N�^�[�𔄋p�Ώۂɂ��܂����B"
            );

            SellDraggedCharacter();

            return;
        }
    }

    private void SellDraggedCharacter()
    {
        BoardManager boardManager =
            FindAnyObjectByType<BoardManager>();

        if (boardManager == null)
        {
            return;
        }

        if (boardManager.selectionType == BoardManager.SelectionType.Hand &&
            boardManager.selectedHandSlot != null && boardManager.selectedHandSlot.itemData != null)
        {
            ItemData soldItem = boardManager.selectedHandSlot.RemoveItem();
            boardManager.ClearSelection();
            SetInteractable(false);
            Debug.Log("Item " + soldItem.itemName + " ��0G�Ŕ��p���܂����B");
            return;
        }

        OwnedCharacter character = boardManager.GetSelectedCharacter();
        if (character == null) return;

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            Debug.LogError(
                "PlayerData��������܂���I"
            );

            return;
        }

        // ���p����
        if (!player.SellCharacter(character))
        {
            return;
        }

        // �莝���𔄋p�����ꍇ
        if (boardManager.selectionType ==
            BoardManager.SelectionType.Hand)
        {
            if (boardManager.selectedHandSlot != null)
            {
                boardManager.selectedHandSlot.RemoveCharacter();
            }
        }

        // �Ֆʂ𔄋p�����ꍇ
        if (boardManager.selectionType ==
            BoardManager.SelectionType.Board)
        {
            if (boardManager.selectedBoardSlot != null)
            {
                boardManager.selectedBoardSlot.ClearCharacterVisual();
            }
        }

        Debug.Log(
            character.data.characterName +
            " ���h���b�O���h���b�v�Ŕ��p���܂����I"
        );

        boardManager.ClearSelection();

        SetInteractable(false);
    }
}