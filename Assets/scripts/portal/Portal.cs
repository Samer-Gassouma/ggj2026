using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
/// 
/// /// <summary>
/// /// Place this on a trigger collider at the end of the tunnel.
/// /// /// When the player enters the trigger the portal plays a quick effect,
/// /// /// auto-creates a full-screen fade-to-black overlay, and loads the target scene.
/// /// No Inspector wiring needed for UI — only the optional VFX/SFX fields.
/// /// /// </summary>
/// [RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Name of the scene to load (must be added to Build Settings).")]
    [SerializeField] private string targetSceneName = "CH1";

    [Tooltip("Or use build index instead (-1 to ignore).")]
    [SerializeField] private int targetSceneIndex = -1;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem portalVFX;
    [SerializeField] private AudioClip portalSFX;
    [SerializeField] private float transitionDelay = 0.5f;

    [Header("Screen Fade")]
    [SerializeField] private float fadeSpeed = 2f;

    private bool triggered = false;
    private CanvasGroup screenFade;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        // ── Auto-create a full-screen black fade overlay ──
        GameObject canvasGO = new GameObject("PortalFadeCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // above everything

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Black image that fills the screen
        GameObject imgGO = new GameObject("FadeImage", typeof(RectTransform));
        imgGO.transform.SetParent(canvasGO.transform, false);

        Image img = imgGO.AddComponent<Image>();
        img.color = Color.black;

        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        screenFade = imgGO.AddComponent<CanvasGroup>();
        screenFade.alpha          = 0f;
        screenFade.blocksRaycasts = false;

        canvasGO.SetActive(false); // hidden until triggered
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag(playerTag)) return;

        triggered = true;
        StartCoroutine(TransitionRoutine());
    }

    private System.Collections.IEnumerator TransitionRoutine()
    {
        if (portalVFX != null) portalVFX.Play();
        if (portalSFX != null) AudioSource.PlayClipAtPoint(portalSFX, transform.position);

        // Fade to black
        screenFade.transform.parent.gameObject.SetActive(true);
        while (screenFade.alpha < 1f)
        {
            screenFade.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(transitionDelay);

        if (targetSceneIndex >= 0)
            SceneManager.LoadScene(targetSceneIndex);
        else
            SceneManager.LoadScene(targetSceneName);
    }
}
