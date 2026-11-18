using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyDetailPanel : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text attackText;
    public TMP_Text defenseText;
    public TMP_Text healthText;
    public TMP_Text targetText;
    private EnemyData currentEnemy;
    private BoardSlot currentEnemySlot;

    public void ShowEnemy(EnemyData enemy, BoardSlot sourceSlot = null)
    {
        if (enemy == null)
        {
            Debug.LogError("Cannot display a null EnemyData.", this);
            return;
        }

        ItemDetailPanel.HideOtherPanels(this);
        currentEnemy = enemy;
        currentEnemySlot = sourceSlot;

        if (iconImage != null)
            iconImage.sprite = enemy.icon;

        if (nameText != null)
            nameText.text = enemy.enemyName;

        RefreshCombatStats();

        UpdateTargetText(enemy.target);
        gameObject.SetActive(true);
    }

    public void RefreshDisplayedContent()
    {
        if (!gameObject.activeInHierarchy || currentEnemy == null)
            return;

        if (currentEnemySlot != null)
        {
            if (currentEnemySlot.enemyData == null)
            {
                Hide();
                return;
            }

            currentEnemy = currentEnemySlot.enemyData;
        }

        if (nameText != null)
            nameText.text = currentEnemy.enemyName;
        if (iconImage != null)
            iconImage.sprite = currentEnemy.icon;

        RefreshCombatStats();
        UpdateTargetText(currentEnemy.target);
    }

    private void RefreshCombatStats()
    {
        int attack = currentEnemy.attack;
        int defense = currentEnemy.defense;
        int health = currentEnemy.health;

        if (currentEnemySlot != null)
        {
            BoardCharacter[] displays =
                currentEnemySlot.GetComponentsInChildren<BoardCharacter>(true);
            BoardCharacter display = System.Array.Find(
                displays,
                candidate => candidate != null && candidate.IsDisplayingEnemy(currentEnemy));
            if (display != null &&
                display.TryGetEnemyBattleStats(out int currentAttack, out int currentDefense, out int currentHealth))
            {
                attack = currentAttack;
                defense = currentDefense;
                health = currentHealth;
            }
        }

        if (attackText != null)
            attackText.text = "ATK " + attack;
        if (defenseText != null)
            defenseText.text = "DEF " + defense;
        if (healthText != null)
            healthText.text = "HP " + health;
    }

    private void UpdateTargetText(string targetValue)
    {
        if (targetText == null)
        {
            TMP_Text[] textComponents = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text textComponent in textComponents)
            {
                if (textComponent.gameObject.name == "TargetText")
                {
                    targetText = textComponent;
                    break;
                }
            }
        }

        if (targetText == null)
        {
            Debug.LogError("TargetText was not found in EnemyDetailPanel.", this);
            return;
        }

        List<string> targetIds = GetTargetIds(targetValue);
        List<string> descriptions = new List<string>();
        foreach (string targetId in targetIds)
        {
            TargetData targetData = Resources.Load<TargetData>("TargetData/" + targetId);
            if (targetData == null)
            {
                Debug.LogError(
                    "TargetData was not found for TargetID " + targetId + ".",
                    this);
                descriptions.Add(targetId);
                continue;
            }

            descriptions.Add(targetData.targetText);
        }

        string descriptionText = string.Join("\n", descriptions.ToArray());
        targetText.text = targetIds.Count > 1
            ? "以下の行動をターン毎に繰り返す。\n" + descriptionText
            : descriptionText;
    }

    private List<string> GetTargetIds(string targetValue)
    {
        List<string> targetIds = new List<string>();
        if (string.IsNullOrWhiteSpace(targetValue))
        {
            targetIds.Add("T001");
            return targetIds;
        }

        string[] values = targetValue.Split(
            new[] { ',', ';', '|', '/', '、', ' ', '\t', '\r', '\n' },
            System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string value in values)
        {
            string targetId = value.Trim();
            if (!string.IsNullOrEmpty(targetId))
                targetIds.Add(targetId);
        }

        if (targetIds.Count == 0)
            targetIds.Add("T001");
        return targetIds;
    }

    public void Hide()
    {
        currentEnemy = null;
        currentEnemySlot = null;
        gameObject.SetActive(false);
    }
}
