using System;
using UnityEngine;
using UnityEngine.UI;

public class StageManager : MonoBehaviour
{
    public enum StageSelectionButton
    {
        SelectA,
        SelectB,
        SelectC,
        SelectD
    }

    [Header("Stage Selection UI")]
    public GameObject popoutSelectStagePanel;
    public Button selectAButton;
    public Button selectBButton;
    public Button selectCButton;
    public Button selectDButton;

    private const int TotalStages = 25;
    private int currentStageIndex;
    private bool awaitingStageSelection;

    public event Action<StageSelectionButton> SelectionCompleted;
    public event Action StageStarting;
    public event Action<StageSelectionButton> StageChoiceSelected;
    public bool IsAwaitingStageSelection => awaitingStageSelection;
    public int CurrentStageNumber => currentStageIndex / 5 + 1;
    public int CurrentRoundNumber => currentStageIndex % 5 + 1;

    private void Start()
    {
        if (selectAButton != null)
            selectAButton.onClick.AddListener(SelectStageA);
        if (selectBButton != null)
            selectBButton.onClick.AddListener(SelectStageB);
        if (selectCButton != null)
            selectCButton.onClick.AddListener(SelectStageC);
        if (selectDButton != null)
            selectDButton.onClick.AddListener(SelectStageD);
    }

    private void OnDestroy()
    {
        if (selectAButton != null)
            selectAButton.onClick.RemoveListener(SelectStageA);
        if (selectBButton != null)
            selectBButton.onClick.RemoveListener(SelectStageB);
        if (selectCButton != null)
            selectCButton.onClick.RemoveListener(SelectStageC);
        if (selectDButton != null)
            selectDButton.onClick.RemoveListener(SelectStageD);
    }

    public void BeginStageSelection(int stageIndex)
    {
        currentStageIndex = Mathf.Clamp(stageIndex, 0, TotalStages - 1);
        StageStarting?.Invoke();
        SetSelectionButtonsVisible(false);

        if (CurrentRoundNumber != 5)
        {
            awaitingStageSelection = false;
            if (popoutSelectStagePanel != null)
                popoutSelectStagePanel.SetActive(false);
            SelectionCompleted?.Invoke(StageSelectionButton.SelectA);
            return;
        }

        if (selectAButton == null ||
            selectBButton == null ||
            selectCButton == null ||
            selectDButton == null)
        {
            Debug.LogError("〇-5の選択にはSelectA〜Dのボタン参照がすべて必要です。", this);
            awaitingStageSelection = true;
            if (popoutSelectStagePanel != null)
                popoutSelectStagePanel.SetActive(true);
            return;
        }

        SetSelectionButtonsVisible(true);
        awaitingStageSelection = true;
        if (popoutSelectStagePanel != null)
            popoutSelectStagePanel.SetActive(true);
    }

    private Button GetButton(StageSelectionButton button)
    {
        switch (button)
        {
            case StageSelectionButton.SelectA: return selectAButton;
            case StageSelectionButton.SelectB: return selectBButton;
            case StageSelectionButton.SelectC: return selectCButton;
            case StageSelectionButton.SelectD: return selectDButton;
            default: return null;
        }
    }

    private void SetSelectionButtonsVisible(bool showButtons)
    {
        SetButtonActive(StageSelectionButton.SelectA, showButtons);
        SetButtonActive(StageSelectionButton.SelectB, showButtons);
        SetButtonActive(StageSelectionButton.SelectC, showButtons);
        SetButtonActive(StageSelectionButton.SelectD, showButtons);
    }

    private void SetButtonActive(StageSelectionButton button, bool active)
    {
        Button stageButton = GetButton(button);
        if (stageButton != null)
            stageButton.gameObject.SetActive(active);
    }

    public void SelectStageA()
    {
        CompleteStageSelection(StageSelectionButton.SelectA);
    }

    public void SelectStageB()
    {
        CompleteStageSelection(StageSelectionButton.SelectB);
    }

    public void SelectStageC()
    {
        CompleteStageSelection(StageSelectionButton.SelectC);
    }

    public void SelectStageD()
    {
        CompleteStageSelection(StageSelectionButton.SelectD);
    }

    private void CompleteStageSelection(StageSelectionButton selectedButton)
    {
        if (!awaitingStageSelection)
            return;

        if (CurrentRoundNumber != 5 || GetButton(selectedButton) == null)
            return;

        awaitingStageSelection = false;
        if (popoutSelectStagePanel != null)
            popoutSelectStagePanel.SetActive(false);

        StageChoiceSelected?.Invoke(selectedButton);
        SelectionCompleted?.Invoke(selectedButton);
    }
}
