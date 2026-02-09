using UnityEngine;

public class ObjectGrabber : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private SwordController sword;

    [Header("Grabbable")]
    [SerializeField] private LayerMask grabbableMask;
    [SerializeField] private float maxGrabDistance = 8f;

    [Header("Hold / Drag")]
    [SerializeField] private float holdDistance = 3.0f;
    [SerializeField] private float followStrength = 35f;
    [SerializeField] private float damping = 8f;
    [SerializeField] private bool keepGravityWhileHolding = false;

    [Header("Release / Throw")]
    [SerializeField] private float throwImpulse = 10f;

    [Header("Beam Anchor")]
    [SerializeField] private float beamStartForwardOffset = 5f;

    [Header("ruRun Beam Look")]
    [SerializeField, Range(6, 64)] private int beamSegments = 20;
    // simplified: straight-line beam (previous curve/wobble removed)

    // ruRun thickness (readable)
    [SerializeField] private float beamWidthMin = 0.015f;
    [SerializeField] private float beamWidthMax = 0.028f;
    [SerializeField] private float beamPulseSpeed = 6f;

    [SerializeField] private float beamUVScrollSpeed = 2.2f;

    [SerializeField] private Color beamCoreColor = Color.white;
    [SerializeField] private Color beamEdgeColor = new Color(0.25f, 0.85f, 1f, 1f); // cyan

    private Rigidbody heldRb;

    private float heldOriginalDrag;
    private float heldOriginalAngDrag;
    private bool heldOriginalGravity;

    private LineRenderer beam;
    private Material beamMat;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        CreateBeam();
        SetBeamActive(false);
    }

    private void Update()
    {
        bool handFree = (sword == null) || !sword.IsHeld;

        RaycastHit hit = default;
        bool canGrab = false;
        if (handFree)
            canGrab = TryGetTarget(out hit);

        if (sword != null)
        {
            if (heldRb != null || canGrab) sword.SetCrosshairGrab();
            else sword.SetCrosshairNormal();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!handFree) return;
            if (canGrab && hit.rigidbody != null)
                Grab(hit.rigidbody);
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (heldRb != null)
                Release(throwIt: true);
        }

        if (heldRb != null)
        {
            SetBeamActive(true);
            UpdateBeamVisual();
        }
        else
        {
            SetBeamActive(false);
        }
    }

    private void FixedUpdate()
    {
        if (heldRb == null || cam == null) return;

        Vector3 targetPos = cam.transform.position + cam.transform.forward * holdDistance;

        Vector3 toTarget = targetPos - heldRb.position;
        Vector3 desiredVel = toTarget * followStrength;

#if UNITY_6000_0_OR_NEWER
        Vector3 accel = (desiredVel - heldRb.linearVelocity) * damping;
#else
        Vector3 accel = (desiredVel - heldRb.velocity) * damping;
#endif
        heldRb.AddForce(accel, ForceMode.Acceleration);
    }

    private bool TryGetTarget(out RaycastHit hit)
    {
        if (cam == null) { hit = default; return false; }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool ok = Physics.Raycast(ray, out hit, maxGrabDistance, grabbableMask, QueryTriggerInteraction.Ignore);
        return ok && hit.rigidbody != null;
    }

    private void Grab(Rigidbody rb)
    {
        heldRb = rb;

#if UNITY_6000_0_OR_NEWER
        heldOriginalDrag = heldRb.linearDamping;
        heldOriginalAngDrag = heldRb.angularDamping;
#else
        heldOriginalDrag = heldRb.drag;
        heldOriginalAngDrag = heldRb.angularDrag;
#endif
        heldOriginalGravity = heldRb.useGravity;

        heldRb.useGravity = keepGravityWhileHolding ? heldOriginalGravity : false;

#if UNITY_6000_0_OR_NEWER
        heldRb.linearDamping = 6f;
        heldRb.angularDamping = 6f;
#else
        heldRb.drag = 6f;
        heldRb.angularDrag = 6f;
#endif

        heldRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Release(bool throwIt)
    {
        if (heldRb == null || cam == null) return;

        heldRb.useGravity = heldOriginalGravity;

#if UNITY_6000_0_OR_NEWER
        heldRb.linearDamping = heldOriginalDrag;
        heldRb.angularDamping = heldOriginalAngDrag;
#else
        heldRb.drag = heldOriginalDrag;
        heldRb.angularDrag = heldOriginalAngDrag;
#endif

        if (throwIt)
            heldRb.AddForce(cam.transform.forward * throwImpulse, ForceMode.Impulse);

        heldRb = null;

        if (sword != null) sword.SetCrosshairNormal();
        SetBeamActive(false);
    }

    // =========================
    // ruRun Beam
    // =========================
    private void CreateBeam()
    {
        GameObject beamObj = new GameObject("TelekinesisBeam");
        beamObj.transform.SetParent(transform, false);

        beam = beamObj.AddComponent<LineRenderer>();
        beam.useWorldSpace = true;
        beam.alignment = LineAlignment.View; // always faces camera
        beam.numCapVertices = 8;
        beam.numCornerVertices = 8;
        beam.textureMode = LineTextureMode.Tile;

        // URP unlit
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        beamMat = new Material(sh);
        beam.material = beamMat;

        // Gradient: bright white core with cyan tint on edges
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(beamCoreColor, 0.0f),
                new GradientColorKey(beamEdgeColor, 0.5f),
                new GradientColorKey(beamCoreColor, 1.0f),
            },
            new[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.12f),
                new GradientAlphaKey(1.0f, 0.88f),
                new GradientAlphaKey(0.0f, 1.0f),
            }
        );
        beam.colorGradient = g;

        // Width curve: thicker in the middle (ruRun style)
        AnimationCurve wc = new AnimationCurve();
        wc.AddKey(0f, 0.75f);
        wc.AddKey(0.5f, 1.0f);
        wc.AddKey(1f, 0.75f);
        beam.widthCurve = wc;

        beam.positionCount = Mathf.Max(beamSegments, 6);
        beam.enabled = false;
    }

    private void SetBeamActive(bool active)
    {
        if (beam != null) beam.enabled = active;
    }

    private void UpdateBeamVisual()
    {
        if (beam == null || cam == null || heldRb == null) return;
        Vector3 start = cam.transform.position + cam.transform.forward * beamStartForwardOffset;
        Vector3 end = heldRb.worldCenterOfMass;

        // pulse width
        float pulse01 = (Mathf.Sin(Time.time * beamPulseSpeed) + 1f) * 0.5f;
        float w = Mathf.Lerp(beamWidthMin, beamWidthMax, pulse01);
        beam.startWidth = w;
        beam.endWidth = w * 0.9f;

        // UV scroll (energy moving) and tint
        if (beamMat != null)
        {
            beamMat.mainTextureOffset = new Vector2(Time.time * beamUVScrollSpeed, 0f);
            beamMat.color = Color.Lerp(beamEdgeColor, beamCoreColor, 0.55f + 0.45f * pulse01);
        }

        int n = Mathf.Max(beamSegments, 6);
        if (beam.positionCount != n) beam.positionCount = n;

        // Straight line: sample positions linearly between start and end
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            beam.SetPosition(i, p);
        }
    }

    // (CubicBezier removed — beam now uses a straight linear interpolation)
}
