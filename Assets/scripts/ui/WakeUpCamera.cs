using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simulates the player waking up on the ground:
///   1. Screen starts black
///   2. Camera starts looking at the floor (tilted down)
///   3. Fades in from black
///   4. Camera slowly rotates up to normal eye level
///   5. Optional subtle blur-to-sharp via a second overlay
///
/// Auto-creates the fade canvas from code — no Inspector wiring.
/// Auto-finds the main camera if none is assigned.
/// Invoke <see cref="Play"/> to start, or it auto-plays on Start().
/// </summary>
public class WakeUpCamera : MonoBehaviour
{
    [Header("Camera (auto-finds if null)")]
    [SerializeField] private Camera targetCamera;

    [Header("Wake-Up Settings")]
    [Tooltip("How far down the camera looks at the start (degrees).")]
    [SerializeField] private float startPitch = 70f;   // looking almost at the ground

    [Tooltip("Final resting pitch (0 = straight ahead).")]
    [SerializeField] private float endPitch = 0f;

    [Tooltip("Total time for the head-lift animation.")]
    [SerializeField] private float liftDuration = 3f;

    [Tooltip("Ease curve for the lift (default: smooth ease-in-out).")]
    [SerializeField] private AnimationCurve liftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fade-In")]
    [SerializeField] private float fadeDuration  = 2f;
    [SerializeField] private float fadeHoldBlack = 0.5f;  // seconds to stay fully black before fading

    [Header("Blink Effect (optional)")]
    [SerializeField] private bool  enableBlink   = true;
    [SerializeField] private int   blinkCount    = 2;
    [SerializeField] private float blinkDuration = 0.25f;

    [Header("Auto-Play")]
    [SerializeField] private bool autoPlay = true;

    // Runtime
    private CanvasGroup fadeGroup;
    private Image       fadeImage;
    private bool        isPlaying;

    /// <summary>True while the wake-up sequence is running.</summary>
    public bool IsPlaying => isPlaying;

    /// <summary>Fired when the sequence finishes.</summary>
    public event System.Action OnWakeUpComplete;

    private void Awake()
    {
        BuildFadeOverlay();
    }

    private void Start()
    {
        // Auto-find camera
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                targetCamera = FindAnyObjectByType<Camera>();
        }

        if (autoPlay)
            Play();
    }

    /// <summary>Start the wake-up sequence.</summary>
    public void Play()
    {
        if (isPlaying) return;
        StartCoroutine(WakeUpSequence());
    }

    // ───────────────────── Build fade overlay from code ───────────────────
    private void BuildFadeOverlay()
    {
        GameObject canvasGO = new GameObject("WakeUpFadeCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250; // above everything

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Full-screen black image
        GameObject imgGO = new GameObject("BlackOverlay", typeof(RectTransform));
        imgGO.transform.SetParent(canvasGO.transform, false);

        fadeImage = imgGO.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fadeGroup = imgGO.AddComponent<CanvasGroup>();
        fadeGroup.alpha          = 1f; // start fully black
        fadeGroup.blocksRaycasts = false;
    }

    // ───────────────────── The wake-up coroutine ──────────────────────────
    private IEnumerator WakeUpSequence()
    {
        isPlaying = true;

        // ── Set camera to "on the ground" orientation ──
        float originalY = 0f;
        float originalZ = 0f;
        if (targetCamera != null)
        {
            originalY = targetCamera.transform.localEulerAngles.y;
            originalZ = targetCamera.transform.localEulerAngles.z;
            targetCamera.transform.localEulerAngles = new Vector3(startPitch, originalY, originalZ);
        }

        // ── Hold black for a moment ──
        fadeGroup.alpha = 1f;
        yield return new WaitForSeconds(fadeHoldBlack);

        // ── Blink effect: quick flickers while camera simultaneously lifts ──
        // The camera rises from ground to forward DURING the blinks,
        // so by the time the eyes fully open the head is already up.
        float liftElapsed = 0f;

        if (enableBlink)
        {
            for (int i = 0; i < blinkCount; i++)
            {
                // Open eyes briefly — camera keeps lifting
                float blinkOpenTime = blinkDuration * 0.4f;
                float blinkOpenElapsed = 0f;
                float startAlpha = fadeGroup.alpha;
                while (blinkOpenElapsed < blinkOpenTime)
                {
                    blinkOpenElapsed += Time.deltaTime;
                    liftElapsed += Time.deltaTime;
                    fadeGroup.alpha = Mathf.Lerp(startAlpha, 0.3f, blinkOpenElapsed / blinkOpenTime);
                    ApplyLift(liftElapsed, originalY, originalZ);
                    yield return null;
                }
                fadeGroup.alpha = 0.3f;

                // Close again — camera keeps lifting
                float blinkCloseTime = blinkDuration * 0.6f;
                float blinkCloseElapsed = 0f;
                while (blinkCloseElapsed < blinkCloseTime)
                {
                    blinkCloseElapsed += Time.deltaTime;
                    liftElapsed += Time.deltaTime;
                    fadeGroup.alpha = Mathf.Lerp(0.3f, 1f, blinkCloseElapsed / blinkCloseTime);
                    ApplyLift(liftElapsed, originalY, originalZ);
                    yield return null;
                }
                fadeGroup.alpha = 1f;

                // Tiny pause between blinks — camera keeps lifting
                float pauseElapsed = 0f;
                while (pauseElapsed < 0.15f)
                {
                    pauseElapsed += Time.deltaTime;
                    liftElapsed += Time.deltaTime;
                    ApplyLift(liftElapsed, originalY, originalZ);
                    yield return null;
                }
            }
        }

        // ── Final fade from black + continue lifting ──
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            liftElapsed += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDuration);
            ApplyLift(liftElapsed, originalY, originalZ);
            yield return null;
        }
        fadeGroup.alpha = 0f;

        // ── Finish any remaining lift if needed ──
        while (liftElapsed < liftDuration)
        {
            liftElapsed += Time.deltaTime;
            ApplyLift(liftElapsed, originalY, originalZ);
            yield return null;
        }

        // Ensure final rotation is exact
        if (targetCamera != null)
            targetCamera.transform.localEulerAngles = new Vector3(endPitch, originalY, originalZ);

        // ── Clean up: disable the overlay ──
        fadeGroup.transform.parent.gameObject.SetActive(false);

        isPlaying = false;
        OnWakeUpComplete?.Invoke();
    }

    // ───────────────────── Helpers ────────────────────────────────────────
    private void ApplyLift(float elapsed, float yaw, float roll)
    {
        if (targetCamera == null) return;
        float t = Mathf.Clamp01(elapsed / liftDuration);
        float curved = liftCurve.Evaluate(t);
        float pitch = Mathf.Lerp(startPitch, endPitch, curved);
        targetCamera.transform.localEulerAngles = new Vector3(pitch, yaw, roll);
    }

    // ───────────────────── Utility ────────────────────────────────────────
    private IEnumerator FadeTo(float target, float duration)
    {
        float start = fadeGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        fadeGroup.alpha = target;
    }
}
