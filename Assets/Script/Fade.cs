using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Fade : MonoBehaviour
{
    public Image targetImage;
    public float fadeDuration = 0.5f;

    public IEnumerator FadeUI(float targetAlpha)
    {
        targetImage.raycastTarget = true;
        float startAlpha = targetImage.color.a;
        float time = 0;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            targetImage.color = new Color(targetImage.color.r, targetImage.color.g, targetImage.color.b, alpha);
            yield return null;
        }

        targetImage.color = new Color(targetImage.color.r, targetImage.color.g, targetImage.color.b, targetAlpha);
        if (targetAlpha == 0) targetImage.raycastTarget = false;
    }
}
