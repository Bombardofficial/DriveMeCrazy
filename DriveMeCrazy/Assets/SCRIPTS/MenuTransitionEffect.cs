using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MenuTransitionEffect : MonoBehaviour
{
    public Volume globalVolume; // Reference to the Global Volume with Color Adjustments override
    public float transitionDuration = 1f;  // Total duration of the transition
    public float minPostExposure = -4f;    // Minimum exposure value
    public float initialPostExposure = 0.44f; // Initial exposure value

    private ColorAdjustments colorAdjustments;
    private bool isTransitioning = false;

    void Start()
    {
        if (globalVolume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = initialPostExposure;
        }
        else
        {
            Debug.LogError("Color Adjustments override not found in the Global Volume!");
        }
    }

    public void TriggerTransition(System.Action onMinExposureReached, System.Action onTransitionComplete = null)
    {
        if (!isTransitioning)
        {
            StartCoroutine(TransitionPostExposure(onMinExposureReached, onTransitionComplete));
        }
    }

    IEnumerator TransitionPostExposure(System.Action onMinExposureReached, System.Action onTransitionComplete)
    {
        isTransitioning = true;

        float elapsedTime = 0f;
        float halfDuration = transitionDuration / 2f;

        // Phase 1: Decrease exposure
        while (elapsedTime < halfDuration)
        {
            float progress = elapsedTime / halfDuration;
            colorAdjustments.postExposure.value = Mathf.Lerp(initialPostExposure, minPostExposure, progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        colorAdjustments.postExposure.value = minPostExposure;
        onMinExposureReached?.Invoke();

        // Phase 2: Restore exposure
        elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            float progress = elapsedTime / halfDuration;
            colorAdjustments.postExposure.value = Mathf.Lerp(minPostExposure, initialPostExposure, progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        colorAdjustments.postExposure.value = initialPostExposure;
        isTransitioning = false;
        onTransitionComplete?.Invoke();
    }
}
