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
                "SellButtonにButtonコンポーネントがありません！"
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

        // 手持ち・盤面のキャラクターが
        // 選択している場合のみ売却可能
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
                "BoardManagerが見つかりません！"
            );

            return;
        }

        if (boardManager.selectionType == BoardManager.SelectionType.Hand &&
            boardManager.selectedHandSlot != null && boardManager.selectedHandSlot.itemData != null)
        {
            ItemData soldItem = boardManager.selectedHandSlot.RemoveItem();
            boardManager.ClearSelection();
            SetInteractable(false);
            Debug.Log("Item " + soldItem.itemName + " を0Gで売却しました。");
            return;
        }

        OwnedCharacter character = boardManager.GetSelectedCharacter();
        if (character == null) return;

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            Debug.LogError(
                "PlayerDataが見つかりません！"
            );

            return;
        }

        // 売却処理
        if (!player.SellCharacter(character))
        {
            return;
        }

        // 手持ちを売却した場合
        if (boardManager.selectionType ==
            BoardManager.SelectionType.Hand)
        {
            if (boardManager.selectedHandSlot != null)
            {
                boardManager.selectedHandSlot.RemoveCharacter();
            }
        }

        // 盤面を売却した場合
        if (boardManager.selectionType ==
            BoardManager.SelectionType.Board)
        {
            if (boardManager.selectedBoardSlot != null)
            {
                boardManager.selectedBoardSlot.ClearCharacterVisual();
            }
        }

        // 選択解除
        boardManager.ClearSelection();

        // 売却直後は確実に無効化
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
                "BoardManagerが見つかりません！"
            );

            return;
        }

        // -----------------------------------------
        // 手持ちキャラクター
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
                "手持ちキャラクターを売却対象にしました。"
            );

            SellDraggedCharacter();

            return;
        }

        // -----------------------------------------
        // 盤面キャラクター
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
                "盤面キャラクターを売却対象にしました。"
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
            Debug.Log("Item " + soldItem.itemName + " を0Gで売却しました。");
            return;
        }

        OwnedCharacter character = boardManager.GetSelectedCharacter();
        if (character == null) return;

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            Debug.LogError(
                "PlayerDataが見つかりません！"
            );

            return;
        }

        // 売却処理
        if (!player.SellCharacter(character))
        {
            return;
        }

        // 手持ちを売却した場合
        if (boardManager.selectionType ==
            BoardManager.SelectionType.Hand)
        {
            if (boardManager.selectedHandSlot != null)
            {
                boardManager.selectedHandSlot.RemoveCharacter();
            }
        }

        // 盤面を売却した場合
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
            " をドラッグ＆ドロップで売却しました！"
        );

        boardManager.ClearSelection();

        SetInteractable(false);
    }
}