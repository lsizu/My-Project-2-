using UnityEngine;
using UnityEngine.EventSystems;

public class DetailPanelDismissArea : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        if (boardManager == null)
        {
            Debug.LogError("BoardManager was not found.", this);
            return;
        }

        boardManager.HideCharacterAndItemDetails();
    }
}
