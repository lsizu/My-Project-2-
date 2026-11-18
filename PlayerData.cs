using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerData : MonoBehaviour
{
    [Header("�����R�C��")]
    public int gold = 10;
    public TMP_Text goldText;

    [Header("HP")]
    public int hp;
    public TMP_Text hpCount;
    public Slider hpBar;
    private int initialHp;
    public int InitialHp => initialHp;

    [Header("�����L�����N�^�[")]
    public List<OwnedCharacter> ownedCharacters =
        new List<OwnedCharacter>();

    [Header("�莝��")]
    public HandManager handManager;

    private void Awake()
    {
        initialHp = Mathf.Max(0, hp);
    }

    private void Start()
    {
        ResolveHpBar();
        UpdateHPUI();
    }

    public bool CanBuy(int cost)
    {
        return gold >= cost;
    }

    public bool SellCharacter(
    OwnedCharacter character)
    {
        if (character == null)
        {
            return false;
        }

        if (!ownedCharacters.Contains(character))
        {
            Debug.LogError(
                "���p�Ώۂ̃L�����N�^�[���������X�g�ɑ��݂��܂���I"
            );

            return false;
        }

        int returnedCopies = 1;
        if (character.star == 2)
        {
            returnedCopies = 3;
        }
        else if (character.star >= 3)
        {
            returnedCopies = 9;
        }

        int sellPrice = character.data.cost * returnedCopies;

        ShopManager shopManager =
            FindAnyObjectByType<ShopManager>();

        if (shopManager != null)
        {
            shopManager.ReturnCharacterToPool(
                character.data,
                returnedCopies
            );
        }

        gold += sellPrice;

        ownedCharacters.Remove(
            character
        );

        UpdateGoldUI();

        Debug.Log(
            character.data.characterName +
            " �� " +
            sellPrice +
            "G �Ŕ��p���܂����I"
        );

        return true;
    }

    public void UpdateGoldUI()
    {
        if (goldText != null)
        {
            goldText.text = gold.ToString();
        }
    }

    public void UpdateHPUI()
    {
        if (hpCount != null)
            hpCount.text = Mathf.Max(0, hp) + "/" + initialHp;

        if (hpBar != null)
        {
            hpBar.maxValue = initialHp;
            hpBar.value = Mathf.Clamp(hp, 0, initialHp);
        }
    }

    private void ResolveHpBar()
    {
        if (hpBar != null)
            return;

        foreach (Slider candidate in FindObjectsByType<Slider>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject.name == "HPBar")
            {
                hpBar = candidate;
                return;
            }
        }

        Debug.LogWarning("HPBar Slider was not found. Assign it to PlayerData or name it 'HPBar'.", this);
    }
}