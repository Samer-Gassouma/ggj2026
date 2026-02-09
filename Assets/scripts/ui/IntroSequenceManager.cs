using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Intro Sequence Manager — ZERO CONFIGURATION NEEDED.
///
/// Just add this script to an empty GameObject in your Intro scene.
/// It auto-loads the video and image from Resources/Intro/:
///   • Resources/Intro/animationafteredit.mp4  (loaded as VideoClip)
///   • Resources/Intro/cutscene.png            (loaded as Texture2D)
///
/// Flow:
///   1. Video plays fullscreen (animationafteredit.mp4)
///   2. Cross-fade to still image (cutscene.png)
///   3. "Press any key to start" pulses at the bottom
///   4. Any key → black screen + typing: "Hello, [USERNAME]..."
///   5. Load scene index 1
///
/// SETUP:
///   1. Create a new scene, put it at BUILD INDEX 0
///   2. Create empty GameObject → add this script
///   3. Done. No Inspector assignment needed.
/// </summary>
public class IntroSequenceManager : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float videoToImageFadeDuration = 1.5f;
    [SerializeField] private float pressKeyDelay            = 1f;
    [SerializeField] private float fadeToBlackDuration       = 0.8f;
    [SerializeField] private float typingCharDelay           = 0.06f;
    [SerializeField] private float postTypingDelay           = 2.5f;

    [Header("Target Scene")]
    [SerializeField] private int targetSceneIndex = 1;

    // Resource paths (relative to Resources/, no extension)
    private const string VIDEO_RESOURCE = "Intro/animationafteredit";
    private const string IMAGE_RESOURCE = "Intro/cutscene";

    // ─── Auto-loaded ───
    private VideoClip introVideoClip;

    // ─── Auto-created ───
    private Camera          mainCam;
    private Canvas          canvas;
    private RawImage        videoDisplay;
    private RawImage        stillDisplay;   // RawImage so it works with any Texture2D
    private Image           blackOverlay;
    private TextMeshProUGUI pressKeyText;
    private TextMeshProUGUI typingText;
    private VideoPlayer     videoPlayer;
    private RenderTexture   videoRT;
    private AudioSource     audioSource;
    private Texture2D       stillTexture;

    private bool videoFinished;
    private bool waitingForInput;

    // ═══════════════════════════════════════════════════════════
    private void Awake()
    {
        // Auto-load video clip from Resources (Unity imports .mp4 as VideoClip)
        introVideoClip = Resources.Load<VideoClip>(VIDEO_RESOURCE);
        if (introVideoClip == null)
            Debug.LogWarning($"IntroSequenceManager: VideoClip not found at Resources/{VIDEO_RESOURCE}");

        // Auto-load image from Resources
        var tex = Resources.Load<Texture2D>(IMAGE_RESOURCE);
        if (tex != null)
            stillTexture = tex;
        else
            Debug.LogWarning($"IntroSequenceManager: Texture not found at Resources/{IMAGE_RESOURCE}");

        BuildUI();
        SetupVideoPlayer();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        StartCoroutine(IntroSequence());
    }

    // ═══════════════════════════════════════════════════════════
    //  MAIN SEQUENCE
    // ═══════════════════════════════════════════════════════════
    private IEnumerator IntroSequence()
    {
        // ── 1. PLAY VIDEO using VideoClip (no file paths, no URL issues) ──
        if (introVideoClip != null)
        {
            videoPlayer.clip = introVideoClip;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.Prepare();

            while (!videoPlayer.isPrepared)
                yield return null;

            videoPlayer.Play();
            videoDisplay.gameObject.SetActive(true);

            // Wait for video to finish
            videoFinished = false;
            videoPlayer.loopPointReached += OnVideoEnd;

            while (!videoFinished)
                yield return null;

            // ── 2. CROSS-FADE TO STILL IMAGE ──
            if (stillTexture != null)
            {
                stillDisplay.texture = stillTexture;
                stillDisplay.gameObject.SetActive(true);
                stillDisplay.color = new Color(1, 1, 1, 0);

                yield return StartCoroutine(CrossFade(stillDisplay, videoDisplay, videoToImageFadeDuration));
            }

            videoPlayer.Stop();
            videoDisplay.gameObject.SetActive(false);
        }
        else
        {
            // No video clip — show still image immediately
            if (stillTexture != null)
            {
                stillDisplay.texture = stillTexture;
                stillDisplay.gameObject.SetActive(true);
                stillDisplay.color = Color.white;
            }
        }

        // ── 3. "PRESS ANY KEY TO START" ──
        yield return new WaitForSeconds(pressKeyDelay);

        pressKeyText.gameObject.SetActive(true);
        waitingForInput = true;

        StartCoroutine(PulseText(pressKeyText));

        while (waitingForInput)
        {
            if (Input.anyKeyDown)
                waitingForInput = false;
            yield return null;
        }

        pressKeyText.gameObject.SetActive(false);

        // ── 4. FADE TO BLACK ──
        yield return StartCoroutine(FadeOverlay(blackOverlay, 0f, 1f, fadeToBlackDuration));

        stillDisplay.gameObject.SetActive(false);

        // ── 5. TYPING ANIMATION — "Hello, [USERNAME]..." ──
        yield return new WaitForSeconds(0.5f);

        string userName = GetComputerUserName();
        string fullMessage = $"Hello, {userName}...";

        typingText.gameObject.SetActive(true);
        typingText.text = "";

        yield return StartCoroutine(TypeText(typingText, fullMessage, typingCharDelay));

        yield return new WaitForSeconds(postTypingDelay);

        // ── 6. LOAD NEXT SCENE ──
        SceneManager.LoadScene(targetSceneIndex);
    }

    private void OnVideoEnd(VideoPlayer vp) => videoFinished = true;

    // ═══════════════════════════════════════════════════════════
    //  ANIMATIONS
    // ═══════════════════════════════════════════════════════════

    private IEnumerator CrossFade(RawImage fadeIn, RawImage fadeOut, float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            fadeIn.color  = new Color(1, 1, 1, p);
            fadeOut.color = new Color(1, 1, 1, 1f - p);
            yield return null;
        }
        fadeIn.color  = Color.white;
        fadeOut.color = new Color(1, 1, 1, 0);
    }

    private IEnumerator FadeOverlay(Image overlay, float from, float to, float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            overlay.color = new Color(0, 0, 0, a);
            yield return null;
        }
        overlay.color = new Color(0, 0, 0, to);
    }

    private IEnumerator TypeText(TextMeshProUGUI textComp, string message, float charDelay)
    {
        textComp.text = "";
        for (int i = 0; i < message.Length; i++)
        {
            textComp.text += message[i];
            yield return new WaitForSeconds(charDelay);
        }
    }

    private IEnumerator PulseText(TextMeshProUGUI textComp)
    {
        while (waitingForInput)
        {
            float alpha = Mathf.PingPong(Time.time * 1.5f, 1f) * 0.6f + 0.4f;
            textComp.color = new Color(1, 1, 1, alpha);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  GET USERNAME
    // ═══════════════════════════════════════════════════════════
    private string GetComputerUserName()
    {
        string name = System.Environment.UserName;

        if (string.IsNullOrEmpty(name))
            name = System.Environment.GetEnvironmentVariable("USERNAME");
        if (string.IsNullOrEmpty(name))
            name = System.Environment.GetEnvironmentVariable("USER");
        if (string.IsNullOrEmpty(name))
            name = "Stranger";

        if (name.Length > 1)
            name = char.ToUpper(name[0]) + name.Substring(1);

        return name;
    }

    // ═══════════════════════════════════════════════════════════
    //  BUILD UI — everything auto-created
    // ═══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        // Camera
        mainCam = FindAnyObjectByType<Camera>();
        if (mainCam == null)
        {
            var camGo = new GameObject("IntroCam");
            mainCam = camGo.AddComponent<Camera>();
        }
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = Color.black;
        mainCam.orthographic = true;

        // Canvas
        var canvasGo = new GameObject("IntroCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Video display (fullscreen RawImage)
        videoDisplay = CreateFullscreenRawImage(canvasGo.transform, "VideoDisplay");
        videoDisplay.gameObject.SetActive(false);

        // Still image display (fullscreen RawImage — works with any Texture2D, no import settings)
        stillDisplay = CreateFullscreenRawImage(canvasGo.transform, "StillDisplay");
        stillDisplay.gameObject.SetActive(false);

        // Black overlay
        blackOverlay = CreateFullscreenImage(canvasGo.transform, "BlackOverlay");
        blackOverlay.color = new Color(0, 0, 0, 0);

        // "Press any key" text
        var pressGo = new GameObject("PressKeyText");
        pressGo.transform.SetParent(canvasGo.transform, false);
        pressKeyText = pressGo.AddComponent<TextMeshProUGUI>();
        pressKeyText.text = "Press any key to start";
        pressKeyText.fontSize = 36;
        pressKeyText.alignment = TextAlignmentOptions.Center;
        pressKeyText.color = Color.white;
        pressKeyText.font = TMP_Settings.defaultFontAsset;
        var pressRect = pressGo.GetComponent<RectTransform>();
        pressRect.anchorMin = new Vector2(0.2f, 0.08f);
        pressRect.anchorMax = new Vector2(0.8f, 0.18f);
        pressRect.offsetMin = Vector2.zero;
        pressRect.offsetMax = Vector2.zero;
        pressGo.SetActive(false);

        // Typing text
        var typingGo = new GameObject("TypingText");
        typingGo.transform.SetParent(canvasGo.transform, false);
        typingText = typingGo.AddComponent<TextMeshProUGUI>();
        typingText.text = "";
        typingText.fontSize = 52;
        typingText.alignment = TextAlignmentOptions.Center;
        typingText.color = Color.white;
        typingText.font = TMP_Settings.defaultFontAsset;
        var typingRect = typingGo.GetComponent<RectTransform>();
        typingRect.anchorMin = new Vector2(0.1f, 0.35f);
        typingRect.anchorMax = new Vector2(0.9f, 0.65f);
        typingRect.offsetMin = Vector2.zero;
        typingRect.offsetMax = Vector2.zero;
        typingGo.SetActive(false);
    }

    private void SetupVideoPlayer()
    {
        videoRT = new RenderTexture(1920, 1080, 0);
        videoRT.Create();

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRT;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;

        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        audioSource = gameObject.AddComponent<AudioSource>();
        videoPlayer.SetTargetAudioSource(0, audioSource);

        videoDisplay.texture = videoRT;
    }

    // ═══════════════════════════════════════════════════════════
    //  UI Helpers
    // ═══════════════════════════════════════════════════════════
    private RawImage CreateFullscreenRawImage(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var raw = go.AddComponent<RawImage>();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return raw;
    }

    private Image CreateFullscreenImage(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return img;
    }

    private void OnDestroy()
    {
        if (videoRT != null) { videoRT.Release(); Destroy(videoRT); }
    }
}
