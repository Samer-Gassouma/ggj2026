using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the CH tutorial scene flow.  100% self-configuring:
///   - Auto-creates NarrativeUI + KeyPromptUI (full Canvas hierarchy)
///   - Auto-finds the Portal in the scene (by Portal component)
///
/// Just drop this script on an empty GameObject — nothing to wire in the Inspector.
///
///  Flow:
///  1. Narrative: "Don't worry, everything will be explained soon..."
///  2. WASD + Space key prompts appear (keyboard layout)
///  3. Narrative: "Use WASD to move and SPACE to jump - give it a shot!"
///  4. Player presses every key (they light up green)
///  5. Narrative: "Nice! Head through the portal ahead."
///  6. Portal activates -> player walks through -> loads next scene
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float initialDelay   = 1.5f;
    [SerializeField] private float narrativePause = 2.0f;

    [Header("Narrative Lines")]
    [SerializeField] private string introLine    = "Don't worry... everything will be explained soon.";
    [SerializeField] private string controlsLine = "Use  W A S D  to move and  SPACE  to jump - give it a shot!";
    [SerializeField] private string maskLine     = "Press  TAB  to open your mask inventory, then select the Dash Mask with your mouse.";
    [SerializeField] private string doubleJumpLine = "The Dash Mask grants you double jump! Try jumping to the platform in front, then press  SPACE  mid-air to jump again.";
    [SerializeField] private string creeperWarningLine = "Careful! On that platform is a Creeper - it will chase you and explode if you get too close. Keep your distance!";
    [SerializeField] private string goodJobLine  = "Nice! Now head through the portal ahead.";

    // Auto-created / auto-found
    private NarrativeUI  narrativeUI;
    private KeyPromptUI  keyPromptUI;
    private WakeUpCamera wakeUpCamera;
    private GameObject   portalObject;

    private void Start()
    {
        // ── Auto-find the Portal in the scene ──
        Portal portalComp = FindAnyObjectByType<Portal>(FindObjectsInactive.Include);
        if (portalComp != null)
        {
            portalObject = portalComp.gameObject;

            // Auto-add the swirling visual if not already present
            if (portalComp.GetComponent<PortalVisual>() == null)
                portalComp.gameObject.AddComponent<PortalVisual>();
        }
        else
        {
            Debug.LogWarning("[TutorialManager] No Portal found in scene. " +
                             "Tutorial will run but there will be no portal to activate.");
        }

        // ── Auto-create WakeUpCamera ──
        wakeUpCamera = FindAnyObjectByType<WakeUpCamera>();
        if (wakeUpCamera == null)
        {
            GameObject go = new GameObject("WakeUpCamera");
            go.transform.SetParent(transform, false);
            wakeUpCamera = go.AddComponent<WakeUpCamera>();
        }

        // ── Auto-create NarrativeUI ──
        narrativeUI = FindAnyObjectByType<NarrativeUI>();
        if (narrativeUI == null)
        {
            GameObject go = new GameObject("NarrativeUI");
            go.transform.SetParent(transform, false);
            narrativeUI = go.AddComponent<NarrativeUI>();
        }

        // ── Auto-create KeyPromptUI ──
        keyPromptUI = FindAnyObjectByType<KeyPromptUI>();
        if (keyPromptUI == null)
        {
            GameObject go = new GameObject("KeyPromptUI");
            go.transform.SetParent(transform, false);
            keyPromptUI = go.AddComponent<KeyPromptUI>();
        }

        StartCoroutine(TutorialSequence());
    }

    private IEnumerator TutorialSequence()
    {
        // ── Wait for the wake-up to finish first ──
        yield return new WaitUntil(() => !wakeUpCamera.IsPlaying);
        yield return new WaitForSeconds(0.3f); // tiny breath after waking

        // ── Step 1: Intro narrative ──
        yield return new WaitForSeconds(initialDelay);
        narrativeUI.ShowNarrative(introLine);

        // Wait for typewriter + a beat
        yield return new WaitForSeconds(introLine.Length * 0.03f + narrativePause);

        // ── Step 2: Show controls narrative + key prompts ──
        narrativeUI.ShowNarrative(controlsLine);
        yield return new WaitForSeconds(0.5f);
        keyPromptUI.Show();

        // ── Step 3: Wait until every key has been pressed ──
        yield return new WaitUntil(() => keyPromptUI.AllKeysPressed);
        yield return new WaitForSeconds(0.5f);
        keyPromptUI.Hide();

        // ── Step 4: Mask tutorial ──
        narrativeUI.ShowNarrative(maskLine);
        yield return new WaitForSeconds(maskLine.Length * 0.03f + narrativePause);

        // ── Step 5: Double jump explanation ──
        narrativeUI.ShowNarrative(doubleJumpLine);
        yield return new WaitForSeconds(doubleJumpLine.Length * 0.03f + narrativePause);

        // ── Step 6: Creeper warning ──
        narrativeUI.ShowNarrative(creeperWarningLine);
        yield return new WaitForSeconds(creeperWarningLine.Length * 0.03f + narrativePause);

        // ── Step 7: Good job -> activate portal ──
        narrativeUI.ShowNarrative(goodJobLine);

        if (portalObject != null)
            portalObject.SetActive(true);

        yield return new WaitForSeconds(goodJobLine.Length * 0.03f + narrativePause + 1f);
        narrativeUI.HideNarrative();
    }
}
