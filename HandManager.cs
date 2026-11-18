using UnityEngine;

public class HandManager : MonoBehaviour
{
    [Header("�莝���ݒ�")]
    public GameObject handSlotPrefab;
    public Transform handArea;

    [Header("�莝���g��")]
    public int slotCount = 10;

    private HandSlot[] handSlots;

    [Header("�ړ����̎莝���L�����N�^�[")]
    public HandSlot selectedHandSlot;

    void Start()
    {
        CreateHandSlots();
    }

    void CreateHandSlots()
    {
        foreach (Transform child in handArea)
        {
            Destroy(child.gameObject);
        }

        handSlots = new HandSlot[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObject =
                Instantiate(
                    handSlotPrefab,
                    handArea
                );

            HandSlot slot =
    slotObject.GetComponent<HandSlot>();

            // �������͕K����ɂ���
            slot.ownedCharacter = null;

            handSlots[i] = slot;
        }
    }

    // �󂫃X���b�g�����邩
    public HandSlot FindEmptySlot()
    {
        if (handSlots == null) return null;
        foreach (HandSlot slot in handSlots)
            if (slot != null && slot.IsEmpty()) return slot;
        return null;
    }

    public bool HasEmptySlot()
    {
        if (handSlots == null)
        {
            return false;
        }

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i].IsEmpty())
            {
                return true;
            }
        }

        return false;
    }

    public int CountEmptySlots()
    {
        if (handSlots == null)
            return 0;

        int emptySlotCount = 0;
        foreach (HandSlot slot in handSlots)
            if (slot != null && slot.IsEmpty())
                emptySlotCount++;

        return emptySlotCount;
    }

    // ��ԍ��̋󂫃X���b�g�ɃL�����N�^�[������
    public bool AddCharacter(OwnedCharacter character)
    {
        if (character == null)
        {
            return false;
        }

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i].IsEmpty())
            {
                handSlots[i].SetCharacter(character);

                Debug.Log(
                    character.data.characterName +
                    " ���莝���� " +
                    (i + 1) +
                    " �Ԗڂ̃X���b�g�ɔz�u���܂����B"
                );

                return true;
            }
        }

        Debug.Log("�莝���ɋ󂫃X���b�g������܂���I");

        return false;
    }

    public HandSlot FindCharacterSlot(
    CharacterData characterData)
    {
        if (characterData == null ||
            handSlots == null)
        {
            return null;
        }

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i].IsEmpty() || handSlots[i].ownedCharacter == null)
            {
                continue;
            }

            if (handSlots[i].ownedCharacter.data ==
                characterData)
            {
                return handSlots[i];
            }
        }

        return null;
    }

    // �V���b�v�L�����N�^�[����ԍ��̋󂫃X���b�g�֍w��
    public bool BuyShopCharacterToLeftmost(
        CharacterData character,
        ShopCard shopCard)
    {
        if (character == null)
        {
            return false;
        }

        if (shopCard == null)
        {
            return false;
        }

        // �󂫃X���b�g��T��
        HandSlot targetSlot = null;

        for (int i = 0; i < handSlots.Length; i++)
        {
            if (handSlots[i].IsEmpty())
            {
                targetSlot = handSlots[i];
                break;
            }
        }

        // �莝�������t
        if (targetSlot == null)
        {
            Debug.Log("�莝���ɋ󂫃X���b�g������܂���I");
            return false;
        }

        PlayerData player =
            FindAnyObjectByType<PlayerData>();

        if (player == null)
        {
            Debug.LogError("PlayerData��������܂���I");
            return false;
        }

        // �S�[���h�`�F�b�N
        if (!player.CanBuy(character.cost))
        {
            Debug.Log("�R�C��������܂���I");
            return false;
        }

        CharacterStatSettings statSettings =
            CharacterStatSettings.FindForCurrentScene();
        if (statSettings == null ||
            !statSettings.TryGetStatRanges(
                character.cost,
                1,
                out _,
                out _,
                out _,
                out _))
        {
            Debug.LogError(
                "Character stats are not configured for Cost " + character.cost +
                ", Star 1.");
            return false;
        }

        // �S�[���h���x����
        player.gold -= character.cost;
        player.UpdateGoldUI();

        // �����L�����N�^�[�����
        OwnedCharacter ownedCharacter =
            new OwnedCharacter(
                character,
                statSettings);

        // �������X�g�ɒǉ�
        player.ownedCharacters.Add(ownedCharacter);

        // ���[�̋󂫃X���b�g�ɔz�u
        targetSlot.SetCharacter(ownedCharacter);

        Debug.Log(
            character.characterName +
            " �����[�̋󂫃X���b�g�ɔz�u���܂����I"
        );

        // �V���b�v�������
        ShopSlot shopSlot =
    shopCard.GetShopSlot();

        if (shopSlot != null)
        {
            shopSlot.ClearCharacter();
        }

        return true;
    }

    // �莝���L�����N�^�[��I��
    public void SelectHandCharacter(
        OwnedCharacter character,
        HandSlot handSlot)
    {
        if (character == null || handSlot == null)
        {
            return;
        }

        // ���łɕʂ̎莝���L�����N�^�[��I��
        if (selectedHandSlot != null)
        {
            // �����L�����N�^�[���N���b�N
            // �� �I������
            if (selectedHandSlot == handSlot)
            {
                selectedHandSlot = null;

                Debug.Log("�莝���L�����N�^�[�̑I�����������܂����B");

                return;
            }

            // �ʂ̎莝���L�����N�^�[
            // �� ����
            SwapHandCharacters(
                selectedHandSlot,
                handSlot
            );

            return;
        }

        // �����I�����Ă��Ȃ�
        selectedHandSlot = handSlot;

        Debug.Log(
            character.data.characterName +
            " ���莝���őI�����܂����B"
        );
    }

    // �莝���L�����N�^�[���ړ��E����
    public void MoveHandCharacter(
        HandSlot fromSlot,
        HandSlot toSlot)
    {
        if (fromSlot == null || toSlot == null)
        {
            return;
        }

        if (fromSlot.IsEmpty())
        {
            return;
        }

        // �ړ��悪�󂢂Ă���
        if (toSlot.IsEmpty())
        {
            OwnedCharacter character =
                fromSlot.RemoveCharacter();

            toSlot.SetCharacter(character);

            selectedHandSlot = null;

            Debug.Log(
                character.data.characterName +
                " ���莝�����ňړ����܂����I"
            );

            return;
        }

        // �ړ���ɂ��L�����N�^�[������
        SwapHandCharacters(
            fromSlot,
            toSlot
        );
    }

    // �莝���L�����N�^�[������
    public void SwapHandCharacters(
        HandSlot slotA,
        HandSlot slotB)
    {
        if (slotA == null || slotB == null)
        {
            return;
        }

        if (slotA.IsEmpty() || slotB.IsEmpty())
        {
            return;
        }

        OwnedCharacter characterA =
            slotA.ownedCharacter;

        OwnedCharacter characterB =
            slotB.ownedCharacter;

        slotA.SetCharacter(characterB);
        slotB.SetCharacter(characterA);

        selectedHandSlot = null;

        Debug.Log(
            characterA.data.characterName +
            " �� " +
            characterB.data.characterName +
            " ���������܂����I"
        );
    }
}