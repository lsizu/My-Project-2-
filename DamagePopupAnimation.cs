using System.Collections;
using TMPro;
using UnityEngine;

public class DamagePopupAnimation : MonoBehaviour
{
    private const float FramesPerSecond = 60f;
    private const float RiseFrames = 30f;
    private const float FadeFrames = 5f;
    private const float RiseDistance = 50f;

    private RectTransform rectTransform;
    private TMP_Text popupText;
    private CanvasGroup canvasGroup;
    private Vector2 startPosition;
    private float elapsedFrames;

    public bool IsComplete { get; private set; }

    public void Play()
    {
        IsComplete = false;
        rectTransform = transform as RectTransform;
        popupText = GetComponent<TMP_Text>();
        if (popupText == null)
            popupText = GetComponentInChildren<TMP_Text>(true);
        canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null || popupText == null || canvasGroup == null)
        {
            IsComplete = true;
            Destroy(gameObject);
            return;
        }

        startPosition = rectTransform.anchoredPosition;
        elapsedFrames = 0f;
        canvasGroup.alpha = 1f;
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float totalFrames = RiseFrames + FadeFrames;
        while (elapsedFrames < totalFrames)
        {
            elapsedFrames += Time.unscaledDeltaTime * FramesPerSecond;
            float riseProgress = Mathf.Clamp01(elapsedFrames / RiseFrames);
            float easedRise = 1f - Mathf.Pow(1f - riseProgress, 2f);
            rectTransform.anchoredPosition = startPosition + Vector2.up * (RiseDistance * easedRise);

            float fadeProgress = Mathf.Clamp01((elapsedFrames - RiseFrames) / FadeFrames);
            canvasGroup.alpha = 1f - fadeProgress;
            yield return null;
        }

        rectTransform.anchoredPosition = startPosition + Vector2.up * RiseDistance;
        canvasGroup.alpha = 0f;
        IsComplete = true;
        Destroy(gameObject);
    }
}
