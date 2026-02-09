using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Masks/Vision Mask", fileName = "VisionMask")]
public class VisionMaskAbility : MaskAbility
{
    [Header("Primary — Truth Sight")]
    [Tooltip("Tag used by hidden objects (bridges, platforms, etc.)")]
    public string hiddenTag = "HiddenObject";
    [Tooltip("Layer name for invisible world objects")]
    public string hiddenLayerName = "Hidden";
    public float revealRadius = 30f;
    public float pulseInterval = 1f;

    [Header("Superpower — Deep Perception")]
    public float interactRange = 5f;
    public KeyCode interactKey = KeyCode.E;

    public override void Activate(PlayerAbilityContext context)
    {
        // Primary: reveal hidden objects
        context.visionEnabled = true;

        // Screen overlay — mystic purple vignette
        MaskScreenOverlay.Show(MaskOverlayType.Vision);

        var handler = context.GetComponent<VisionMaskHandler>();
        if (handler == null)
            handler = context.gameObject.AddComponent<VisionMaskHandler>();
        handler.Setup(this, context);
        handler.enabled = true;

        if (superpowerUnlocked)
            ActivateSuperpower(context);
    }

    public override void Deactivate(PlayerAbilityContext context)
    {
        context.visionEnabled = false;
        context.canInteractHidden = false;
        context.canGrabUnseen = false;
        context.altPuzzleSolutions = false;

        // Remove screen overlay
        MaskScreenOverlay.Hide();

        var handler = context.GetComponent<VisionMaskHandler>();
        if (handler != null)
        {
            handler.HideAllRevealed();
            handler.enabled = false;
        }
    }

    protected override void ActivateSuperpower(PlayerAbilityContext context)
    {
        context.canInteractHidden = true;
        context.canGrabUnseen = true;
        context.altPuzzleSolutions = true;
    }
}

/// <summary>
/// Runtime handler for the Vision Mask. 
/// Reveals hidden objects within range when the mask is active.
/// Superpower allows interacting with / grabbing hidden objects.
/// </summary>
public class VisionMaskHandler : MonoBehaviour
{
    private VisionMaskAbility data;
    private PlayerAbilityContext context;
    private float pulseTimer;
    private readonly List<RevealedObject> revealedObjects = new List<RevealedObject>();

    // Telekinesis system
    private GameObject selectedObject;
    private Renderer[] selectedRenderers;
    private Material[][] selectedOriginalMats;
    private Rigidbody selectedRb;
    private LineRenderer teleBeam;
    private bool isHoldingObject = false;
    private float holdTimer = 0f;
    private SwordController cachedSword;
    private static Material selectionMaterial;

    // Selection shader config
    private const float THROW_FORCE = 40f;
    private const float SELECT_RANGE = 50f;
    private const float HOLD_LEVITATE_HEIGHT = 1.5f;
    private const float BEAM_ORIGIN_OFFSET = 2f;

    // Track whether we've done the initial hide pass
    private static bool initialHideDone = false;

    private struct RevealedObject
    {
        public GameObject go;
        public Renderer[] renderers;
        public Material[][] originalMaterials; // stored per-renderer
    }

    // Cached hologram material (shared across all revealed objects)
    private static Material hologramMaterial;

    private static Material GetHologramMaterial()
    {
        if (hologramMaterial != null) return hologramMaterial;

        Shader shader = Shader.Find("Custom/HologramHack");
        if (shader == null)
        {
            Debug.LogWarning("HologramHack shader not found, falling back to transparent.");
            shader = Shader.Find("Sprites/Default");
        }

        hologramMaterial = new Material(shader);
        hologramMaterial.SetColor("_MainColor", new Color(0.25f, 0.08f, 0.7f, 0.5f));       // deep purple base
        hologramMaterial.SetColor("_EdgeColor", new Color(0.5f, 0.15f, 1.0f, 1.0f));        // bright purple edge
        hologramMaterial.SetColor("_ScanlineColor", new Color(0.1f, 0.85f, 1.0f, 0.3f));    // cyan scanlines
        hologramMaterial.SetColor("_GlitchColor", new Color(1.0f, 0.15f, 0.5f, 0.8f));      // pink glitch
        hologramMaterial.SetFloat("_ScanlineSpeed", 3.0f);
        hologramMaterial.SetFloat("_ScanlineDensity", 40.0f);
        hologramMaterial.SetFloat("_ScanlineWidth", 0.4f);
        hologramMaterial.SetFloat("_GlitchIntensity", 0.06f);
        hologramMaterial.SetFloat("_GlitchSpeed", 5.0f);
        hologramMaterial.SetFloat("_FresnelPower", 2.0f);
        hologramMaterial.SetFloat("_FlickerSpeed", 12.0f);
        hologramMaterial.SetFloat("_Alpha", 0.45f);
        hologramMaterial.SetFloat("_DataStreamDensity", 15.0f);
        hologramMaterial.SetFloat("_DataStreamSpeed", 6.0f);

        return hologramMaterial;
    }

    /// <summary>
    /// Selection shader — white/cyan glitch for grabbed objects.
    /// </summary>
    private static Material GetSelectionMaterial()
    {
        if (selectionMaterial != null) return selectionMaterial;

        Shader shader = Shader.Find("Custom/HologramHack");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        selectionMaterial = new Material(shader);
        selectionMaterial.SetColor("_MainColor", new Color(0.9f, 0.95f, 1.0f, 0.55f));       // bright white
        selectionMaterial.SetColor("_EdgeColor", new Color(0.6f, 0.9f, 1.0f, 1.0f));         // cyan edge
        selectionMaterial.SetColor("_ScanlineColor", new Color(0.8f, 0.95f, 1.0f, 0.4f));    // white-cyan scanlines
        selectionMaterial.SetColor("_GlitchColor", new Color(0.3f, 0.8f, 1.0f, 0.9f));       // cyan glitch
        selectionMaterial.SetFloat("_ScanlineSpeed", 5.0f);
        selectionMaterial.SetFloat("_ScanlineDensity", 50.0f);
        selectionMaterial.SetFloat("_ScanlineWidth", 0.35f);
        selectionMaterial.SetFloat("_GlitchIntensity", 0.04f);
        selectionMaterial.SetFloat("_GlitchSpeed", 4.0f);
        selectionMaterial.SetFloat("_FresnelPower", 1.5f);
        selectionMaterial.SetFloat("_FlickerSpeed", 18.0f);
        selectionMaterial.SetFloat("_Alpha", 0.55f);
        selectionMaterial.SetFloat("_DataStreamDensity", 20.0f);
        selectionMaterial.SetFloat("_DataStreamSpeed", 10.0f);

        return selectionMaterial;
    }

    public void Setup(VisionMaskAbility ability, PlayerAbilityContext ctx)
    {
        data = ability;
        context = ctx;
        pulseTimer = 0f;

        // First time: hide all hidden objects globally
        if (!initialHideDone)
        {
            HideAllHiddenObjectsGlobally();
            initialHideDone = true;
        }
    }

    /// <summary>
    /// Called once at first setup — finds ALL "HiddenObject" tagged objects in the scene
    /// and makes them invisible + non-collidable.
    /// </summary>
    private void HideAllHiddenObjectsGlobally()
    {
        if (data == null) return;
        GameObject[] all = GameObject.FindGameObjectsWithTag(data.hiddenTag);
        foreach (var obj in all)
        {
            HideObjectCompletely(obj);
        }
        Debug.Log($"👁️ Vision system initialized: hid {all.Length} hidden objects");
    }

    /// <summary>
    /// Makes an object completely invisible: disable all renderers, disable colliders.
    /// </summary>
    private void HideObjectCompletely(GameObject obj)
    {
        if (obj == null) return;

        // Disable all renderers
        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.enabled = false;

            // Remove any leftover emission
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                }
            }
        }

        // Disable all colliders so player can't walk on invisible things
        var colliders = obj.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticState()
    {
        // Reset so it re-hides on scene reload / play again
        initialHideDone = false;
    }

    private void OnEnable()
    {
        pulseTimer = 0f;
        // Immediately reveal nearby objects
        PulseReveal();
    }

    private void OnDisable()
    {
        HideAllRevealed();
        DeselectObject();
    }

    private void Update()
    {
        if (data == null) return;

        // Periodic pulse to reveal nearby hidden objects
        pulseTimer -= Time.deltaTime;
        if (pulseTimer <= 0f)
        {
            PulseReveal();
            pulseTimer = data.pulseInterval;
        }

        // Superpower: interact with hidden objects
        if (context != null && context.canInteractHidden && Input.GetKeyDown(data.interactKey))
        {
            TryInteractHidden();
        }
        else if (context != null && !context.canInteractHidden && Input.GetKeyDown(data.interactKey))
        {
            // Player attempted to use Vision super-interact but hasn't unlocked it yet
            if (LockFeedbackUI.Instance != null)
                LockFeedbackUI.Instance.ShowLocked("Locked", "Not learned yet");
            else if (MaskPickupUI.Instance != null)
                MaskPickupUI.Instance.ShowToast(data.icon, "Not learned yet", "Locked");
        }

        // Superpower: telekinesis grab & throw
        if (context != null && context.canGrabUnseen)
        {
            HandleTelekinesis();
        }
        else if (context != null && !context.canGrabUnseen)
        {
            // If player tries to select a hidden object with LMB while telekinesis locked, show locked UI
            if (Input.GetMouseButtonDown(0))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    if (Physics.Raycast(ray, out RaycastHit hit, SELECT_RANGE))
                    {
                        // if ray hits a hidden-tag object, inform player it's locked
                        if (hit.collider != null && hit.collider.gameObject.CompareTag(data.hiddenTag))
                        {
                            if (LockFeedbackUI.Instance != null)
                                LockFeedbackUI.Instance.ShowLocked("Locked", "Not learned yet");
                            else if (MaskPickupUI.Instance != null)
                                MaskPickupUI.Instance.ShowToast(data.icon, "Not learned yet", "Locked");
                        }
                    }
                }
            }
        }
    }

    private void PulseReveal()
    {
        // Find all objects with the hidden tag in range
        GameObject[] hiddenObjects = GameObject.FindGameObjectsWithTag(data.hiddenTag);

        foreach (var obj in hiddenObjects)
        {
            float dist = Vector3.Distance(transform.position, obj.transform.position);

            if (dist <= data.revealRadius)
            {
                RevealObject(obj);
            }
            else
            {
                UnrevealObject(obj);
            }
        }
    }

    private void RevealObject(GameObject obj)
    {
        // Check if already revealed
        for (int i = 0; i < revealedObjects.Count; i++)
        {
            if (revealedObjects[i].go == obj) return;
        }

        // Gather renderers
        var renderers = obj.GetComponentsInChildren<Renderer>(true);

        // Store original materials before swapping
        Material[][] origMats = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            var shared = renderers[i].sharedMaterials;
            origMats[i] = new Material[shared.Length];
            System.Array.Copy(shared, origMats[i], shared.Length);
        }

        // Enable renderers and apply hologram shader
        Material holoMat = GetHologramMaterial();
        foreach (var r in renderers)
        {
            r.enabled = true;

            // Replace all material slots with the hologram material
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = holoMat;
            r.materials = mats;
        }

        // Enable colliders so player can walk on / interact with revealed objects
        var colliders = obj.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            col.enabled = true;
        }

        revealedObjects.Add(new RevealedObject
        {
            go = obj,
            renderers = renderers,
            originalMaterials = origMats
        });

    }

    private void UnrevealObject(GameObject obj)
    {
        for (int i = revealedObjects.Count - 1; i >= 0; i--)
        {
            if (revealedObjects[i].go == obj)
            {
                RestoreAndHide(revealedObjects[i]);
                revealedObjects.RemoveAt(i);
                break;
            }
        }
    }

    public void HideAllRevealed()
    {
        for (int i = revealedObjects.Count - 1; i >= 0; i--)
        {
            RestoreAndHide(revealedObjects[i]);
        }
        revealedObjects.Clear();
    }

    /// <summary>
    /// Restore original materials, then hide the object completely.
    /// </summary>
    private void RestoreAndHide(RevealedObject revealed)
    {
        if (revealed.go == null) return;

        // Restore original materials before disabling
        if (revealed.originalMaterials != null && revealed.renderers != null)
        {
            for (int i = 0; i < revealed.renderers.Length; i++)
            {
                if (revealed.renderers[i] == null) continue;
                if (i < revealed.originalMaterials.Length && revealed.originalMaterials[i] != null)
                {
                    revealed.renderers[i].materials = revealed.originalMaterials[i];
                }
            }
        }

        HideObjectCompletely(revealed.go);
    }

    private void TryInteractHidden()
    {
        // Raycast forward to find interactable hidden objects
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, data.interactRange))
        {
            // Check for IHiddenInteractable interface or "HiddenInteractable" tag
            var interactable = hit.collider.GetComponent<IHiddenInteractable>();
            if (interactable == null)
                interactable = hit.collider.GetComponentInParent<IHiddenInteractable>();

            if (interactable != null)
            {
                interactable.OnVisionInteract();
                Debug.Log($"Vision Mask: Interacted with {hit.collider.gameObject.name}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    //  TELEKINESIS — Ctrl+RightClick to select, release to throw
    // ─────────────────────────────────────────────────────────

    private void HandleTelekinesis()
    {
        // Set crosshair blue if holding object
        SetCrosshairBlue(selectedObject != null && isHoldingObject);

        Camera cam = Camera.main;
        if (cam == null) return;

        // Only allow telekinesis if sword is thrown (not held)
        if (cachedSword == null)
            cachedSword = FindAnyObjectByType<SwordController>();
        if (cachedSword == null || cachedSword.IsHeld)
            return;

        bool leftDown = Input.GetMouseButtonDown(0);
        bool leftHeld = Input.GetMouseButton(0);

        // --- SELECT on Left Mouse Down ---
        if (leftDown && selectedObject == null)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, SELECT_RANGE))
            {
                Rigidbody rb = hit.collider.attachedRigidbody;
                if (rb == null) rb = hit.collider.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    SelectObject(rb.gameObject, rb);
                }
            }
            return;
        }

        // --- While holding object ---
        if (selectedObject != null && isHoldingObject)
        {
            // Release when left mouse released
            if (!leftHeld)
            {
                ThrowSelectedObject(cam);
                return;
            }

            // Keep object levitating in front of camera
            holdTimer += Time.deltaTime;
            Vector3 holdPos = cam.transform.position + cam.transform.forward * (SELECT_RANGE * 0.3f);
            holdPos.y += HOLD_LEVITATE_HEIGHT;

            Vector3 dir = holdPos - selectedRb.position;
            selectedRb.linearVelocity = dir * 8f;

            // Update beam
            UpdateTeleBeam(cam);
        }
    }

    // Crosshair color utility
    private void SetCrosshairBlue(bool isBlue)
    {
        // Try to find SwordController crosshair image
        if (cachedSword != null)
        {
            var crosshair = cachedSword.GetType().GetField("aimingCrosshairImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(cachedSword) as UnityEngine.UI.Image;
            if (crosshair != null)
            {
                if (isBlue)
                    crosshair.color = new Color(0.3f, 0.7f, 1f, 0.9f); // Blue
                else
                    crosshair.color = new Color(1f, 1f, 1f, 0.9f); // White
            }
        }
    }

    private void SelectObject(GameObject obj, Rigidbody rb)
    {
        selectedObject = obj;
        selectedRb = rb;
        isHoldingObject = true;
        holdTimer = 0f;

        // Freeze gravity while held
        selectedRb.useGravity = false;
        selectedRb.linearDamping = 5f;

        // Store original materials & apply selection shader
        selectedRenderers = obj.GetComponentsInChildren<Renderer>(true);
        selectedOriginalMats = new Material[selectedRenderers.Length][];
        Material selMat = GetSelectionMaterial();

        for (int i = 0; i < selectedRenderers.Length; i++)
        {
            var r = selectedRenderers[i];
            if (r == null) continue;
            selectedOriginalMats[i] = r.sharedMaterials;

            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int j = 0; j < mats.Length; j++)
                mats[j] = selMat;
            r.materials = mats;
        }

        // Hide sword
        if (cachedSword == null)
            cachedSword = FindAnyObjectByType<SwordController>();
        if (cachedSword != null)
        {
            var swordRenderers = cachedSword.GetComponentsInChildren<Renderer>(true);
            foreach (var sr in swordRenderers) sr.enabled = false;
        }

        // Create beam
        CreateTeleBeam();

        Debug.Log($"👁️ Telekinesis: selected {obj.name}");
    }

    private void DeselectObject()
    {
        if (selectedObject == null) return;

        // Restore materials
        if (selectedRenderers != null && selectedOriginalMats != null)
        {
            for (int i = 0; i < selectedRenderers.Length; i++)
            {
                if (selectedRenderers[i] == null) continue;
                if (i < selectedOriginalMats.Length && selectedOriginalMats[i] != null)
                    selectedRenderers[i].materials = selectedOriginalMats[i];
            }
        }

        // Restore physics
        if (selectedRb != null)
        {
            selectedRb.useGravity = true;
            selectedRb.linearDamping = 0f;
        }

        // Show sword again
        if (cachedSword != null)
        {
            var swordRenderers = cachedSword.GetComponentsInChildren<Renderer>(true);
            foreach (var sr in swordRenderers) sr.enabled = true;
        }

        // Destroy beam
        if (teleBeam != null) Destroy(teleBeam.gameObject);
        teleBeam = null;

        selectedObject = null;
        selectedRenderers = null;
        selectedOriginalMats = null;
        selectedRb = null;
        isHoldingObject = false;
    }

    private void ThrowSelectedObject(Camera cam)
    {
        if (selectedRb != null)
        {
            // Restore physics before throwing
            selectedRb.useGravity = true;
            selectedRb.linearDamping = 0f;

            // Throw in camera forward direction
            selectedRb.linearVelocity = cam.transform.forward * THROW_FORCE;

            Debug.Log($"👁️ Telekinesis: threw {selectedObject.name}");
        }

        // Restore materials
        if (selectedRenderers != null && selectedOriginalMats != null)
        {
            for (int i = 0; i < selectedRenderers.Length; i++)
            {
                if (selectedRenderers[i] == null) continue;
                if (i < selectedOriginalMats.Length && selectedOriginalMats[i] != null)
                    selectedRenderers[i].materials = selectedOriginalMats[i];
            }
        }

        // Show sword again
        if (cachedSword != null)
        {
            var swordRenderers = cachedSword.GetComponentsInChildren<Renderer>(true);
            foreach (var sr in swordRenderers) sr.enabled = true;
        }

        // Destroy beam
        if (teleBeam != null) Destroy(teleBeam.gameObject);
        teleBeam = null;

        selectedObject = null;
        selectedRenderers = null;
        selectedOriginalMats = null;
        selectedRb = null;
        isHoldingObject = false;
    }

    // ── Beam ──────────────────────────────────────────────

    private void CreateTeleBeam()
    {
        var beamGO = new GameObject("TeleBeam");
        teleBeam = beamGO.AddComponent<LineRenderer>();
        teleBeam.positionCount = 2;
        teleBeam.startWidth = 0.06f;
        teleBeam.endWidth = 0.03f;
        teleBeam.useWorldSpace = true;
        teleBeam.numCapVertices = 4;
        teleBeam.numCornerVertices = 4;
        teleBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        teleBeam.receiveShadows = false;

        // White-cyan gradient
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.4f, 0.85f, 1f), 0.5f),
                new GradientColorKey(new Color(0.85f, 0.95f, 1f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(0.7f, 1f)
            }
        );
        teleBeam.colorGradient = grad;

        // Simple white unlit material
        Material beamMat = new Material(Shader.Find("Sprites/Default"));
        beamMat.SetColor("_Color", new Color(0.7f, 0.9f, 1f, 0.9f));
        teleBeam.material = beamMat;

        UpdateTeleBeam(Camera.main);
    }

    private void UpdateTeleBeam(Camera cam)
    {
        if (teleBeam == null || selectedObject == null || cam == null) return;

        Vector3 origin = cam.transform.position + cam.transform.forward * BEAM_ORIGIN_OFFSET;
        Vector3 target = selectedObject.transform.position;

        teleBeam.SetPosition(0, origin);
        teleBeam.SetPosition(1, target);

        // Subtle pulse effect
        float pulse = Mathf.Sin(Time.time * 6f) * 0.02f;
        teleBeam.startWidth = 0.06f + pulse;
        teleBeam.endWidth = 0.03f + pulse * 0.5f;
    }
}

/// <summary>
/// Interface for objects that can be interacted with via the Vision Mask superpower.
/// Place on hidden switches, puzzle elements, etc.
/// </summary>
public interface IHiddenInteractable
{
    void OnVisionInteract();
}
