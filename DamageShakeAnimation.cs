using System.Collections;
using UnityEngine;

public class DamageShakeAnimation : MonoBehaviour
{
    private const float FramesPerSecond = 60f;
    private const float DurationFrames = 30f;
    private const float MaxOffset = 10f;

    private RectTransform rectTransform;
    private Vector3 restLocalPosition;
    private Coroutine shakeCoroutine;
    private bool hasRestPosition;

    public void Play()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            rectTransform.localPosition = restLocalPosition;
        }
        else if (!hasRestPosition)
        {
            restLocalPosition = rectTransform.localPosition;
            hasRestPosition = true;
        }

        shakeCoroutine = StartCoroutine(Shake());
    }

    private IEnumerator Shake()
    {
        float elapsedFrames = 0f;
        while (elapsedFrames < DurationFrames)
        {
            elapsedFrames += Time.unscaledDeltaTime * FramesPerSecond;
            Vector2 randomOffset = new Vector2(
                Random.Range(-MaxOffset, MaxOffset),
                Random.Range(-MaxOffset, MaxOffset));
            rectTransform.localPosition = restLocalPosition +
                new Vector3(randomOffset.x, randomOffset.y, 0f);
            yield return null;
        }

        rectTransform.localPosition = restLocalPosition;
        shakeCoroutine = null;
    }

    private void OnDisable()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        if (hasRestPosition && rectTransform != null)
            rectTransform.localPosition = restLocalPosition;
    }
}