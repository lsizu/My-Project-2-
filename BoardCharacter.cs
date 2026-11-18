using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class BoardCharacter :
    MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text starText;
    public TMP_Text atkText;
    public TMP_Text defText;
    public TMP_Text hpText;
    public GameObject hpImage;
    public TMP_Text gapText;
    public GameObject gapImage;
    public CharacterStatSettings characterStatSettings;

    [Header("攻撃演出")]
    public GameObject lighting;
    public GameObject damageEffectPrefab;
    public Animator attackAnimator;
    public AnimationClip attackAnimation;

    private PlayableGraph attackAnimationGraph;
    private Coroutine attackAnimationCleanupCoroutine;
    private Coroutine defeatFadeCoroutine;

    [Header("コスト画像")]
    public Sprite cost1Sprite;
    public Sprite cost2Sprite;
    public Sprite cost3Sprite;
    public Sprite cost4Sprite;
    public Sprite cost5Sprite;

    [Header("Frame")]
    public Image frameImage;

    private OwnedCharacter ownedCharacter;
    private ItemData itemData;
    private BoardSlot boardSlot;
    private HandSlot handSlot;
    private EnemyData enemyData;
    private EnemyDetailPanel enemyDetailPanel;
    private bool isEnemyCharacter;
    private int enemyAttack;
    private int enemyDefense;
    private int enemyHealth;

    public void SetCharacter(
        OwnedCharacter character)
    {
        if (character == null || character.data == null)
        {
            Debug.LogError("Cannot display a character without CharacterData.", this);
            return;
        }

        isEnemyCharacter = false;
        itemData = null;
        enemyData = null;
        enemyDetailPanel = null;
        SetEnemyHpVisible(false);
        SetEnemyGapVisible(false);
        ownedCharacter = character;

        CharacterData data =
            character.data;

        if (characterStatSettings == null)
        {
            Debug.LogError("BoardCharacter requires CharacterStatSettings.", this);
        }
        else
        {
            SetCombatStats(
                character.GetAttack(characterStatSettings) + character.battleAttackBonus,
                character.GetDefense(characterStatSettings) + character.battleDefenseBonus);
        }

        nameText.text =
            data.characterName;

        if (starText != null)
        {
            starText.text = string.Empty;
        }

        SetStarImages(character.star);

        switch (data.cost)
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

        if (data.icon != null)
        {
            iconImage.sprite =
                data.icon;
        }
    }

    public void SetItem(ItemData item)
    {
        if (item == null) return;
        isEnemyCharacter = false;
        itemData = item;
        ownedCharacter = null;
        enemyData = null;
        enemyDetailPanel = null;

        if (nameText != null) nameText.text = item.itemName;
        if (starText != null) starText.text = string.Empty;
        if (iconImage != null) iconImage.sprite = item.icon;
        if (hpText != null) hpText.text = string.Empty;
        if (atkText != null) atkText.text = string.Empty;
        if (defText != null) defText.text = string.Empty;
        if (gapText != null) gapText.text = string.Empty;
        if (frameImage != null && cost1Sprite != null) frameImage.sprite = cost1Sprite;

        SetEnemyHpVisible(false);
        SetEnemyGapVisible(false);
        SetNamedChildActive("StarImage", false);
        SetNamedChildActive("AtkImage", false);
        SetNamedChildActive("AttackImage", false);
        SetNamedChildActive("DefImage", false);
        SetNamedChildActive("HPImage", false);
        SetNamedChildActive("GapImage", false);
        SetNamedChildActive("HPImageGapImage", false);
    }

    private void SetNamedChildActive(string childName, bool active)
    {
        Transform child = FindChildRecursive(transform, childName);
        if (child != null) child.gameObject.SetActive(active);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform nested = FindChildRecursive(child, childName);
            if (nested != null) return nested;
        }
        return null;
    }
    public void SetEnemyCharacter(
        EnemyData enemy,
        Sprite enemyFrameSprite,
        EnemyDetailPanel detailPanel,
        int gap = 0,
        int attackBonus = 0,
        int defenseBonus = 0,
        int healthBonus = 0)
    {
        if (enemy == null)
        {
            Debug.LogError("Cannot display a null EnemyData.", this);
            return;
        }

        isEnemyCharacter = true;
        enemyData = enemy;
        enemyDetailPanel = detailPanel;
        enemyAttack = enemy.attack + attackBonus;
        enemyDefense = enemy.defense + defenseBonus;
        enemyHealth = Mathf.Max(0, enemy.health + healthBonus);
        SetCombatStats(enemyAttack, enemyDefense);
        SetEnemyHpVisible(true);
        if (hpText != null)
            hpText.text = enemyHealth.ToString();
        SetEnemyGapVisible(gap != 0);
        if (gapText != null)
            gapText.text = gap.ToString();

        if (nameText != null)
            nameText.text = enemy.enemyName;

        if (starText != null)
            starText.text = string.Empty;

        Transform starImage = transform.Find("StarImage");
        if (starImage != null)
            starImage.gameObject.SetActive(false);

        Transform starParent = starImage != null ? starImage.parent : transform;
        for (int i = 0; i < starParent.childCount; i++)
        {
            Transform child = starParent.GetChild(i);
            if (child.name.StartsWith("StarImage_Dynamic_"))
                child.gameObject.SetActive(false);
        }

        if (iconImage != null)
            iconImage.sprite = enemy.icon;

        if (frameImage != null && enemyFrameSprite != null)
            frameImage.sprite = enemyFrameSprite;
    }

    public void RefreshEnemyBattleState(
        int currentHealth,
        int remainingGap,
        int currentDefense,
        int currentAttack = -1)
    {
        if (!isEnemyCharacter)
            return;

        enemyHealth = Mathf.Max(0, currentHealth);
        enemyDefense = Mathf.Max(0, currentDefense);
        if (currentAttack >= 0)
            enemyAttack = currentAttack;

        SetEnemyHpVisible(true);
        if (hpText != null)
            hpText.text = enemyHealth.ToString();

        SetEnemyGapVisible(remainingGap > 0);
        if (gapText != null)
            gapText.text = Mathf.Max(0, remainingGap).ToString();

        SetCombatStats(enemyAttack, enemyDefense);

        BoardManager.RefreshVisibleDetailPanels();
    }

    public bool TryGetEnemyBattleStats(
        out int currentAttack,
        out int currentDefense,
        out int currentHealth)
    {
        currentAttack = enemyAttack;
        currentDefense = enemyDefense;
        currentHealth = enemyHealth;
        return isEnemyCharacter;
    }

    public bool IsDisplayingEnemy(EnemyData enemy)
    {
        return isEnemyCharacter && enemyData == enemy;
    }

    private void Awake()
    {
        SetAttackLighting(false);
    }

    public void SetAttackLighting(bool active)
    {
        if (lighting == null)
        {
            Transform lightingTransform = FindChildRecursive(transform, "Lighting");
            if (lightingTransform != null)
                lighting = lightingTransform.gameObject;
        }

        if (lighting != null)
            lighting.SetActive(active);
        else if (active)
            Debug.LogWarning("Lighting child was not found on BoardCharacter.", this);
    }

    public void PlayAttackAnimation()
    {
        if (attackAnimation == null)
            return;

        if (attackAnimator == null)
            attackAnimator = GetComponentInChildren<Animator>(true);
        if (attackAnimator == null)
        {
            Debug.LogWarning("Assign an Animator to play the attack AnimationClip.", this);
            return;
        }

        StopAttackAnimationPlayback();
        attackAnimationGraph = PlayableGraph.Create("Attack Animation - " + name);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            attackAnimationGraph, "Attack Animation", attackAnimator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(
            attackAnimationGraph, attackAnimation);
        clipPlayable.SetApplyFootIK(false);
        output.SetSourcePlayable(clipPlayable);
        attackAnimationGraph.Play();
        attackAnimationCleanupCoroutine = StartCoroutine(
            StopAttackAnimationAfter(attackAnimation.length));
    }

    private void PlayDamageEffectPrefab()
    {
        if (damageEffectPrefab == null)
            return;

        RectTransform sourceRect = transform as RectTransform;
        if (sourceRect == null || GetComponentInParent<Canvas>() == null)
        {
            Debug.LogWarning("Damage effect needs a BoardCharacter RectTransform under a Canvas.", this);
            return;
        }

        RectTransform effectAnchor = iconImage != null ? iconImage.rectTransform : sourceRect;
        GameObject effect = Instantiate(damageEffectPrefab, effectAnchor, false);
        RectTransform effectRect = effect.transform as RectTransform;
        if (effectRect == null)
        {
            Debug.LogWarning("Damage effect prefab root must use a RectTransform.", this);
            Destroy(effect);
            return;
        }

        effect.SetActive(true);
        effectRect.anchorMin = new Vector2(0.5f, 0.5f);
        effectRect.anchorMax = new Vector2(0.5f, 0.5f);
        effectRect.pivot = new Vector2(0.5f, 0.5f);

        Image effectImage = effect.GetComponent<Image>();
        if (effectImage != null)
        {
            effectImage.raycastTarget = false;
            effectImage.SetNativeSize();
        }
        effectRect.localScale = Vector3.one * 0.6f;

        effectRect.anchoredPosition = Vector2.zero;

        effect.transform.SetAsLastSibling();

        Animator effectAnimator = effect.GetComponent<Animator>();
        float duration = 1f / 60f;
        if (effectAnimator != null)
        {
            effectAnimator.Rebind();
            effectAnimator.Update(0f);
            AnimationClip[] clips = effectAnimator.runtimeAnimatorController != null
                ? effectAnimator.runtimeAnimatorController.animationClips
                : System.Array.Empty<AnimationClip>();
            foreach (AnimationClip clip in clips)
                if (clip != null)
                    duration = Mathf.Max(duration, clip.length);
        }
        else
        {
            Debug.LogWarning("Damage effect prefab has no Animator component.", effect);
        }

        Destroy(effect, duration + 0.05f);
    }

    private IEnumerator StopAttackAnimationAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));
        if (attackAnimationGraph.IsValid())
            attackAnimationGraph.Destroy();
        attackAnimationCleanupCoroutine = null;
    }

    private void StopAttackAnimationPlayback()
    {
        if (attackAnimationCleanupCoroutine != null)
        {
            StopCoroutine(attackAnimationCleanupCoroutine);
            attackAnimationCleanupCoroutine = null;
        }
        if (attackAnimationGraph.IsValid())
            attackAnimationGraph.Destroy();
    }

    private void OnDisable()
    {
        SetAttackLighting(false);
        StopAttackAnimationPlayback();
    }
    public void PlayDamageShake()
    {
        PlayDamageEffectPrefab();
        DamageShakeAnimation animation = GetComponent<DamageShakeAnimation>();
        if (animation == null)
            animation = gameObject.AddComponent<DamageShakeAnimation>();
        animation.Play();
    }

    public void PlayDefeatFadeAfterPopup(
        DamagePopupAnimation damagePopupAnimation,
        System.Action onPopupComplete = null)
    {
        if (defeatFadeCoroutine != null)
            return;

        defeatFadeCoroutine = StartCoroutine(
            FadeAfterDamagePopup(damagePopupAnimation, onPopupComplete));
    }

    private IEnumerator FadeAfterDamagePopup(
        DamagePopupAnimation damagePopupAnimation,
        System.Action onPopupComplete)
    {
        while (damagePopupAnimation != null && !damagePopupAnimation.IsComplete)
            yield return null;

        onPopupComplete?.Invoke();

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        yield return FadeOutDefeatedEnemy(canvasGroup);
    }

    private IEnumerator FadeOutDefeatedEnemy(CanvasGroup canvasGroup)
    {
        const int fadeFrames = 60;
        float startingAlpha = canvasGroup.alpha;

        for (int frame = 1; frame <= fadeFrames; frame++)
        {
            canvasGroup.alpha = startingAlpha * (1f - frame / (float)fadeFrames);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        defeatFadeCoroutine = null;
    }

    public DamagePopupAnimation ShowDamagePopup(GameObject popupPrefab, int damage)
    {
        if (popupPrefab == null)
        {
            Debug.LogError("DamagePopupPrefab is not assigned on BattleManager.", this);
            return null;
        }

        RectTransform targetRect = transform as RectTransform;
        Canvas targetCanvas = GetComponentInParent<Canvas>();
        if (targetRect == null || targetCanvas == null)
        {
            Debug.LogError("Cannot show damage popup: BoardCharacter RectTransform or parent Canvas is missing.", this);
            return null;
        }

        Canvas canvas = targetCanvas.rootCanvas;
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
        {
            Debug.LogError("Cannot show damage popup: root Canvas has no RectTransform.", canvas);
            return null;
        }

        GameObject popup = Instantiate(popupPrefab, canvas.transform, false);
        popup.name = "DamagePopup_Runtime";
        popup.SetActive(true);

        TMP_Text popupText = popup.GetComponent<TMP_Text>();
        if (popupText == null)
            popupText = popup.GetComponentInChildren<TMP_Text>(true);
        if (popupText == null)
            Debug.LogError("DamagePopupPrefab has no TMP_Text component.", popup);
        else
        {
            popupText.text = damage.ToString();
            popupText.raycastTarget = false;
            popupText.enabled = true;
            popupText.gameObject.SetActive(true);
        }

        RectTransform popupRect = popup.transform as RectTransform;
        if (popupRect == null)
        {
            Debug.LogError("DamagePopupPrefab root must have a RectTransform.", popup);
            Destroy(popup);
            return null;
        }

        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);

        Vector3[] targetCorners = new Vector3[4];
        targetRect.GetWorldCorners(targetCorners);
        Vector3 targetTopCenter = (targetCorners[1] + targetCorners[2]) * 0.5f;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, targetTopCenter);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, uiCamera, out Vector2 canvasPoint))
        {
            Debug.LogError("Cannot convert BoardCharacter position into Canvas coordinates.", this);
            Destroy(popup);
            return null;
        }

        popupRect.anchoredPosition = canvasPoint + Vector2.up * (popupRect.rect.height * 0.5f);
        popup.transform.SetAsLastSibling();

        Canvas popupCanvas = popup.GetComponent<Canvas>();
        if (popupCanvas == null)
            popupCanvas = popup.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingLayerID = canvas.sortingLayerID;
        popupCanvas.sortingOrder = Mathf.Max(canvas.sortingOrder + 1, 1000);
        popupCanvas.worldCamera = canvas.worldCamera;

        CanvasGroup canvasGroup = popup.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = popup.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.ignoreParentGroups = true;

        DamagePopupAnimation animation = popup.GetComponent<DamagePopupAnimation>();
        if (animation == null)
            animation = popup.AddComponent<DamagePopupAnimation>();
        animation.Play();
        Debug.Log("Damage popup " + damage + " created above " + name + ".", popup);
        return animation;
    }

    public void RefreshOwnedStats()
    {
        if (ownedCharacter == null)
            return;

        if (characterStatSettings != null)
            SetCombatStats(ownedCharacter.GetAttack(characterStatSettings) + ownedCharacter.battleAttackBonus, ownedCharacter.GetDefense(characterStatSettings) + ownedCharacter.battleDefenseBonus);
        SetStarImages(ownedCharacter.star);
    }

    private void SetCombatStats(int attack, int defense)
    {
        if (atkText != null)
            atkText.text = attack.ToString();
        if (defText != null)
            defText.text = defense.ToString();
    }

    private void SetEnemyHpVisible(bool visible)
    {
        if (hpImage == null)
        {
            Transform hpImageTransform = transform.Find("HPImage");
            if (hpImageTransform != null)
                hpImage = hpImageTransform.gameObject;
        }

        if (hpText == null)
        {
            TMP_Text[] textComponents = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text textComponent in textComponents)
            {
                if (textComponent.gameObject.name == "HPText")
                {
                    hpText = textComponent;
                    break;
                }
            }
        }

        if (hpImage != null)
            hpImage.SetActive(visible);
        if (hpText != null)
            hpText.gameObject.SetActive(visible);
        else
            Debug.LogError("BoardCharacter requires an HPText child.", this);
    }

    private void SetEnemyGapVisible(bool visible)
    {
        if (gapImage == null)
        {
            Transform gapImageTransform = transform.Find("GapImage");
            if (gapImageTransform != null)
                gapImage = gapImageTransform.gameObject;
        }

        if (gapText == null)
        {
            TMP_Text[] textComponents = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text textComponent in textComponents)
            {
                if (textComponent.gameObject.name == "GapText")
                {
                    gapText = textComponent;
                    break;
                }
            }
        }

        if (gapImage != null)
            gapImage.SetActive(visible);
        if (gapText != null)
            gapText.gameObject.SetActive(visible);
        else
            Debug.LogError("BoardCharacter requires a GapText child.", this);
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

    public void SetBoardSlot(
        BoardSlot slot)
    {
        boardSlot = slot;
        handSlot = null;
    }

    public void SetHandSlot(HandSlot slot)
    {
        handSlot = slot;
        boardSlot = null;
    }

    public void OnPointerClick(
    PointerEventData eventData)
    {
        if (isEnemyCharacter)
        {
            if (eventData.button == PointerEventData.InputButton.Right && enemyDetailPanel != null)
            {
                BoardManager manager = FindAnyObjectByType<BoardManager>();
                if (manager != null) manager.HideCharacterAndItemDetails();
                enemyDetailPanel.ShowEnemy(enemyData, boardSlot);
            }

            return;
        }

        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        BoardManager boardManager = FindAnyObjectByType<BoardManager>();
        if (handSlot != null)
        {
            if (boardManager == null)
            {
                Debug.LogError("BoardManager was not found.", this);
                return;
            }
            if (handSlot.itemData != null)
                boardManager.ShowItemDetail(handSlot.itemData);
            else if (handSlot.ownedCharacter != null)
                boardManager.ShowHandCharacterDetail(handSlot.ownedCharacter, handSlot);
            return;
        }

        if (boardSlot == null)
        {
            Debug.LogError("BoardSlot is not assigned.", this);
            return;
        }

        if (boardManager == null)
        {
            Debug.LogError("BoardManager was not found.", this);
            return;
        }

        boardManager.ShowBoardCharacterDetail(boardSlot);
    }

    private bool isDragging = false;
    private UIDragCardPreview.Session dragPreviewSession;

    public void OnBeginDrag(
    PointerEventData eventData)
    {
        if (isEnemyCharacter ||
            (boardSlot == null && handSlot == null) ||
            (boardSlot != null && boardSlot.IsEmpty()) ||
            (handSlot != null && handSlot.IsEmpty()))
        {
            return;
        }

        BoardManager boardManager =
            FindAnyObjectByType<BoardManager>();

        if (boardManager == null)
        {
            return;
        }

        boardManager.NotifyCharacterDragStarted();

        // ドラッグ中のキャラクターを選択状態にする
        boardManager.ClearSelection(clearCharacterDetail: false);

        if (handSlot != null)
        {
            boardManager.selectionType = BoardManager.SelectionType.Hand;
            boardManager.selectedHandSlot = handSlot;
        }
        else
        {
            boardManager.selectionType = BoardManager.SelectionType.Board;
            boardManager.selectedBoardSlot = boardSlot;
        }

        isDragging = true;
        dragPreviewSession = UIDragCardPreview.Begin(gameObject, eventData);

        string draggedName = handSlot != null
            ? (handSlot.itemData != null ? handSlot.itemData.itemName : handSlot.ownedCharacter.data.characterName)
            : boardSlot.placedCharacter.data.characterName;
        Debug.Log(draggedName + " のドラッグを開始しました。");
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

        if (handSlot != null)
        {
            HandSlot handDropTarget = targetObject.GetComponentInParent<HandSlot>();
            if (handDropTarget != null)
            {
                boardManager.MoveHandCharacter(handSlot, handDropTarget, clearCharacterDetail: false);
                return;
            }

            BoardSlot boardDropTarget = targetObject.GetComponentInParent<BoardSlot>();
            if (handSlot.itemData != null)
            {
                if (boardDropTarget != null)
                    boardManager.UseHandItemOnBoard(handSlot, boardDropTarget, clearCharacterDetail: false);
                return;
            }
            if (boardDropTarget != null)
            {
                boardManager.ClearSelection(clearCharacterDetail: false);
                boardManager.selectionType = BoardManager.SelectionType.Hand;
                boardManager.selectedHandSlot = handSlot;
                boardManager.PlaceSelectedHandCharacter(boardDropTarget, clearCharacterDetail: false);
            }
            return;
        }

        // -----------------------------------------
        // 盤面 → 手持ち
        // -----------------------------------------

        HandSlot targetHandSlot =
            targetObject.GetComponentInParent<HandSlot>();

        if (targetHandSlot != null)
        {
            boardManager.ClearSelection(clearCharacterDetail: false);

            boardManager.selectionType =
                BoardManager.SelectionType.Board;

            boardManager.selectedBoardSlot =
                boardSlot;

            boardManager.ReturnSelectedBoardCharacterToHand(
                targetHandSlot,
                clearCharacterDetail: false
            );

            return;
        }

        // -----------------------------------------
        // 盤面 → 盤面
        // -----------------------------------------

        BoardSlot targetBoardSlot =
            targetObject.GetComponentInParent<BoardSlot>();

        if (targetBoardSlot != null)
        {
            boardManager.MoveCharacter(
                boardSlot,
                targetBoardSlot,
                clearCharacterDetail: false
            );

            return;
        }
    }
}
