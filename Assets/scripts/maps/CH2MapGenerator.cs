using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CH2 Level Generator — "Rock & Vision"
/// Builds the complete level from cubes/cylinders. No random floating spheres.
/// Clean ReRun-style corridors with combat arenas, hidden paths, and platforming.
///
/// ┌──────────────────────────────────────────────────────────────────┐
/// │  LEVEL LAYOUT (top-down, player goes forward = +Z)              │
/// │                                                                  │
/// │  1. SPAWN ROOM      — safe start, sword nearby                   │
/// │  2. CORRIDOR         — narrow run with cover crates              │
/// │  3. ROCK ARENA       — open arena, 2 Rock Chargers, 5 covers    │
/// │  4. L-TURN HALLWAY   — left turn toward shrine                   │
/// │  5. MASK SHRINE      — pedestal with Vision Mask pickup          │
/// │  6. DEAD-END ROOM    — wall blocks path, hidden routes past it   │
/// │  7. VERTICAL CLIMB   — zigzag platforms going up                 │
/// │  8. PORTAL ROOM      — final platform with portal                │
/// └──────────────────────────────────────────────────────────────────┘
///
/// ═══════════════════════════════════════════════════════════════════
///  PLACEMENT GUIDE — drop prefabs onto the RED marker cubes:
///
///   👤 Player          → 1_Spawn / PLAYER_SPAWN           (0, 1.5, -3)
///   🗡️ Sword           → 1_Spawn / SWORD_SPAWN            (3.5, 1, 1)
///   🪨 Rock Enemy #1   → 3_RockArena / ROCK_ENEMY_1       world ≈ (-5, 6.8, 36)
///   🪨 Rock Enemy #2   → 3_RockArena / ROCK_ENEMY_2       world ≈ ( 6, 6.8, 41)
///   👁️ Vision Mask     → 5_MaskShrine / VISION_MASK_SPAWN world ≈ (-24, 2.5, 76)
///   🌀 Portal          → 8_PortalRoom / PORTAL_SPAWN      world ≈ (-24, 12.5, 136)
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
public class CH2MapGenerator : MonoBehaviour
{
    [Header("Colours")]
    public Color groundColor = new Color(0.78f, 0.75f, 0.70f);
    public Color wallColor   = new Color(0.50f, 0.48f, 0.45f);
    public Color accentColor = new Color(0.25f, 0.65f, 0.85f);
    public Color hiddenColor = new Color(0.45f, 0.18f, 0.75f);
    public Color dangerColor = new Color(0.85f, 0.20f, 0.12f);
    public Color portalColor = new Color(0.15f, 0.55f, 1f);
    public Color darkColor   = new Color(0.28f, 0.26f, 0.24f);

    private Material matGround, matWall, matAccent, matHidden, matDanger, matPortal, matDark;

    // ═══════════════════════════════════════════════════
    //  GENERATE
    // ═══════════════════════════════════════════════════

    [ContextMenu("Generate CH2 Map")]
    public void GenerateMap()
    {
        while (transform.childCount > 0)
            DestroyImmediate(transform.GetChild(0).gameObject);

        CreateMaterials();

        BuildSection1_Spawn();
        BuildSection2_Corridor();
        BuildSection3_RockArena();
        BuildSection4_LTurn();
        BuildSection5_MaskShrine();
        BuildSection6_DeadEnd();
        BuildSection7_Climb();
        BuildSection8_PortalRoom();

        Debug.Log(
            "═══════════════════════════════════════════════════════\n" +
            "✅  CH2 MAP GENERATED!\n\n" +
            "Place these items at the RED markers:\n\n" +
            "  👤 PLAYER        → 1_Spawn / PLAYER_SPAWN\n" +
            "  🗡️ SWORD         → 1_Spawn / SWORD_SPAWN\n" +
            "  🪨 ROCK ENEMY #1 → 3_RockArena / ROCK_ENEMY_1  (left of center)\n" +
            "  🪨 ROCK ENEMY #2 → 3_RockArena / ROCK_ENEMY_2  (right of center)\n" +
            "  👁️ VISION MASK   → 5_MaskShrine / VISION_MASK_SPAWN  (on pedestal)\n" +
            "  🌀 PORTAL        → 8_PortalRoom / PORTAL_SPAWN  (on pedestal)\n\n" +
            "Purple 'HiddenObject' blocks are invisible until Vision Mask.\n" +
            "═══════════════════════════════════════════════════════");
    }

    // ═══════════════════════════════════════════════════
    //  1. SPAWN ROOM — safe start, sword nearby
    // ═══════════════════════════════════════════════════
    private void BuildSection1_Spawn()
    {
        var s = Section("1_Spawn", V(0, 0, 0));

        Floor(s, "Floor",     V(0, 0, 0),       V(16, 1, 14));
        Wall(s,  "WallBack",  V(0, 2, -7.5f),   V(16, 4, 1));
        Wall(s,  "WallLeft",  V(-8.5f, 2, 0),   V(1, 4, 14));
        Wall(s,  "WallRight", V(8.5f, 2, 0),    V(1, 4, 14));

        // Decorative crates (visual interest, climbable)
        Box(s, "Crate1",  V(-5, 1, -4),    V(1.5f, 1.5f, 1.5f), matDark);
        Box(s, "Crate2",  V(-5, 2.2f, -4), V(1, 1, 1),          matDark);
        Box(s, "CrateR",  V(5.5f, 0.8f, -4), V(1.2f, 1.2f, 1.2f), matDark);
        // Accent trim
        Accent(s, "BackTrim", V(0, 0.52f, -6.5f), V(14, 0.08f, 0.3f));

        Marker(s, "PLAYER_SPAWN", V(0, 1.5f, -3));
        Marker(s, "SWORD_SPAWN",  V(3.5f, 1, 1));
    }

    // ═══════════════════════════════════════════════════
    //  2. CORRIDOR — funnels player into arena
    // ═══════════════════════════════════════════════════
    private void BuildSection2_Corridor()
    {
        var s = Section("2_Corridor", V(0, 0, 14));

        Floor(s, "Floor", V(0, 0, 0),     V(8, 1, 22));
        Wall(s,  "WallL", V(-4.5f, 2, 0), V(1, 4, 22));
        Wall(s,  "WallR", V(4.5f, 2, 0),  V(1, 4, 22));

        // Cover crates — useful if enemy follows you back
        Box(s, "CoverL", V(-2.5f, 1, 4),  V(2, 2, 1.5f), matDark);
        Box(s, "CoverR", V(2.5f, 1, 11),  V(2, 2, 1.5f), matDark);

        // Floor direction stripes
        Accent(s, "Stripe1", V(0, 0.52f, 3),  V(6, 0.06f, 0.2f));
        Accent(s, "Stripe2", V(0, 0.52f, 9),  V(6, 0.06f, 0.2f));
        Accent(s, "Stripe3", V(0, 0.52f, 15), V(6, 0.06f, 0.2f));

        // Red danger line at arena entrance
        Box(s, "DangerLine", V(0, 0.52f, 20), V(6, 0.06f, 0.4f), matDanger);
    }

    // ═══════════════════════════════════════════════════
    //  3. ROCK ARENA — main combat, 2 Rock Chargers
    //
    //  Enemy placement:
    //    ROCK_ENEMY_1 → left of center  (marker at local -5, 4, 0)
    //    ROCK_ENEMY_2 → right of center (marker at local  6, 4, 5)
    //    They hover, so Y=4 above local floor is good.
    //
    //  5 cover blocks let the player bait charges then dodge.
    //  4 corner pillars frame the arena.
    //  Red danger stripes mark common charge lanes.
    // ═══════════════════════════════════════════════════
    private void BuildSection3_RockArena()
    {
        var s = Section("3_RockArena", V(0, 0, 36));

        Floor(s, "ArenaFloor", V(0, 0, 0),         V(32, 1, 28));
        Wall(s,  "WallL",      V(-16.5f, 2.5f, 0), V(1, 5, 28));
        Wall(s,  "WallR",      V(16.5f, 2.5f, 0),  V(1, 5, 28));
        Wall(s,  "WallBack",   V(0, 2.5f, -14.5f), V(32, 5, 1));

        // Front wall with 4-unit exit gap
        Wall(s, "FrontWallL", V(-10.25f, 2.5f, 14.5f), V(12.5f, 5, 1));
        Wall(s, "FrontWallR", V(10.25f, 2.5f, 14.5f),  V(12.5f, 5, 1));

        // ── Cover blocks (player hides behind these during charges) ──
        Box(s, "CoverNW",     V(-8, 1.5f, -6),  V(3, 3, 2.5f), matWall);
        Box(s, "CoverNE",     V(8, 1.5f, -6),   V(3, 3, 2.5f), matWall);
        Box(s, "CoverCenter", V(0, 1.5f, 2),    V(2.5f, 3, 2.5f), matWall);
        Box(s, "CoverSW",     V(-7, 1.5f, 8),   V(3, 3, 2.5f), matWall);
        Box(s, "CoverSE",     V(7, 1.5f, 8),    V(3, 3, 2.5f), matWall);

        // ── Corner pillars ──
        Cylinder(s, "PillarNW", V(-13, 3, -11), V(1.2f, 6, 1.2f), matDark);
        Cylinder(s, "PillarNE", V(13, 3, -11),  V(1.2f, 6, 1.2f), matDark);
        Cylinder(s, "PillarSW", V(-13, 3, 11),  V(1.2f, 6, 1.2f), matDark);
        Cylinder(s, "PillarSE", V(13, 3, 11),   V(1.2f, 6, 1.2f), matDark);

        // ── Charge lane danger stripes on floor ──
        Box(s, "ChargeLane1", V(0, 0.52f, -6), V(28, 0.06f, 0.3f), matDanger);
        Box(s, "ChargeLane2", V(0, 0.52f, 8),  V(28, 0.06f, 0.3f), matDanger);

        // ── Raised observation ledge along back wall (player can jump on) ──
        Box(s, "BackLedge", V(0, 1, -12), V(10, 0.5f, 2), matGround);

        Marker(s, "ROCK_ENEMY_1", V(-5, 4, 0));
        Marker(s, "ROCK_ENEMY_2", V(6, 4, 5));
    }

    // ═══════════════════════════════════════════════════
    //  4. L-TURN — turns left, leads to mask shrine
    // ═══════════════════════════════════════════════════
    private void BuildSection4_LTurn()
    {
        var s = Section("4_LTurn", V(0, 0, 66));

        // Forward segment
        Floor(s, "FloorFwd",   V(0, 0, 0),      V(8, 1, 12));
        Wall(s,  "WallFwdL",   V(-4.5f, 2, 0),  V(1, 4, 12));
        Wall(s,  "WallFwdR",   V(4.5f, 2, 0),   V(1, 4, 12));

        // Corner piece
        Floor(s, "FloorCorner", V(-4, 0, 10),   V(12, 1, 8));

        // Left segment (going -X)
        Floor(s, "FloorLeft",    V(-14, 0, 10),    V(12, 1, 8));
        Wall(s,  "WallLeftTop",  V(-14, 2, 14.5f), V(22, 4, 1));
        Wall(s,  "WallLeftBot",  V(-14, 2, 5.5f),  V(12, 4, 1));
        Wall(s,  "WallFwdEnd",   V(0, 2, 14.5f),   V(8, 4, 1));

        // Accent floor lights guiding the turn
        Accent(s, "TurnGuide1", V(-4, 0.52f, 10),  V(0.3f, 0.06f, 6));
        Accent(s, "TurnGuide2", V(-10, 0.52f, 10), V(0.3f, 0.06f, 6));

        // Crate in the corner (visual + cover)
        Box(s, "CornerCrate", V(-2, 0.8f, 8), V(1.5f, 1.5f, 1.5f), matDark);
    }

    // ═══════════════════════════════════════════════════
    //  5. MASK SHRINE — pedestal with Vision Mask
    //
    //  Mask placement:
    //    Drop MaskPickup (with VisionMaskAbility) onto the
    //    VISION_MASK_SPAWN marker, right on top of the pedestal.
    // ═══════════════════════════════════════════════════
    private void BuildSection5_MaskShrine()
    {
        var s = Section("5_MaskShrine", V(-24, 0, 76));

        Floor(s, "Floor",     V(0, 0, 0),         V(16, 1, 14));
        Wall(s,  "WallBack",  V(0, 2.5f, -7.5f),  V(16, 5, 1));
        Wall(s,  "WallLeft",  V(-8.5f, 2.5f, 0),  V(1, 5, 14));
        Wall(s,  "WallFront", V(0, 2.5f, 7.5f),   V(16, 5, 1));
        // Right wall with entry gap
        Wall(s,  "WallRightTop", V(8.5f, 2.5f, -4.5f), V(1, 5, 7));

        // Pedestal
        Cylinder(s, "PedestalBase", V(0, 0.6f, 0), V(2.5f, 1.2f, 2.5f), matDark);
        Box(s,      "PedestalTop",  V(0, 1.5f, 0), V(1.8f, 0.4f, 1.8f), matAccent);

        // Framing pillars
        Cylinder(s, "ShrineP1", V(-4, 2.5f, -4), V(0.8f, 5, 0.8f), matDark);
        Cylinder(s, "ShrineP2", V(4, 2.5f, -4),  V(0.8f, 5, 0.8f), matDark);
        Cylinder(s, "ShrineP3", V(-4, 2.5f, 4),  V(0.8f, 5, 0.8f), matDark);
        Cylinder(s, "ShrineP4", V(4, 2.5f, 4),   V(0.8f, 5, 0.8f), matDark);

        // Floor accent cross (draws eyes to center)
        Accent(s, "CrossH", V(0, 0.52f, 0), V(5, 0.06f, 0.2f));
        Accent(s, "CrossV", V(0, 0.52f, 0), V(0.2f, 0.06f, 5));

        Marker(s, "VISION_MASK_SPAWN", V(0, 2.5f, 0));
    }

    // ═══════════════════════════════════════════════════
    //  6. DEAD-END ROOM — wall blocks path
    //     Hidden routes (tagged "HiddenObject"):
    //       Route A (LEFT)  — steps climb OVER the wall
    //       Route B (RIGHT) — walkway through a fake wall panel
    // ═══════════════════════════════════════════════════
    private void BuildSection6_DeadEnd()
    {
        var s = Section("6_DeadEndRoom", V(-24, 0, 92));

        Floor(s, "Floor",       V(0, 0, 0),         V(16, 1, 16));
        Wall(s,  "WallLeft",    V(-8.5f, 2.5f, 0),  V(1, 5, 16));
        Wall(s,  "WallRight",   V(8.5f, 2.5f, 0),   V(1, 5, 16));
        Wall(s,  "WallEntryL",  V(-5.5f, 2.5f, -8.5f), V(5, 5, 1));
        Wall(s,  "WallEntryR",  V(5.5f, 2.5f, -8.5f),  V(5, 5, 1));

        // THE BLOCKER — tall wall cutting room in half
        Wall(s, "BlockerWall", V(0, 3, 2), V(16, 6, 1.5f));

        // Purple hint on blocker (signals "use Vision Mask here")
        Box(s, "HintStripe", V(0, 5.5f, 1.1f), V(6, 0.3f, 0.1f), matHidden);

        // ── HIDDEN ROUTE A — staircase over the wall (left side) ──
        var hA1 = Box(s, "HiddenStairA1", V(-5, 1.5f, -1),    V(3, 0.4f, 2.5f), matHidden);
        var hA2 = Box(s, "HiddenStairA2", V(-4, 2.8f, 0.5f),  V(3, 0.4f, 2.5f), matHidden);
        var hA3 = Box(s, "HiddenStairA3", V(-3, 4.2f, 2),     V(3, 0.4f, 2.5f), matHidden);
        var hA4 = Box(s, "HiddenBridgeA",  V(-2, 5.0f, 4),    V(3, 0.4f, 4),    matHidden);
        var hA5 = Box(s, "HiddenDropA",    V(-1, 3.5f, 6.5f), V(3, 0.4f, 3),    matHidden);

        // ── HIDDEN ROUTE B — through fake wall (right side) ──
        var fakePanel = Box(s, "FakeWallPanel", V(8.5f, 2.5f, 2), V(1, 5, 3), matHidden);
        var hB1 = Box(s, "HiddenPathB1", V(10, 0.5f, 0),  V(4, 1, 3), matHidden);
        var hB2 = Box(s, "HiddenPathB2", V(10, 0.5f, 4),  V(4, 1, 4), matHidden);
        var hB3 = Box(s, "HiddenPathB3", V(8, 0.5f, 7),   V(4, 1, 3), matHidden);

        TagAsHidden(hA1); TagAsHidden(hA2); TagAsHidden(hA3);
        TagAsHidden(hA4); TagAsHidden(hA5);
        TagAsHidden(fakePanel); TagAsHidden(hB1); TagAsHidden(hB2); TagAsHidden(hB3);

        // Landing zone beyond the wall
        Floor(s, "BeyondFloor", V(0, 0, 10), V(16, 1, 6));
    }

    // ═══════════════════════════════════════════════════
    //  7. VERTICAL CLIMB — zigzag platforms going up
    //     Hidden shortcut for Vision Mask users
    // ═══════════════════════════════════════════════════
    private void BuildSection7_Climb()
    {
        var s = Section("7_Climb", V(-24, 0, 110));

        // Base
        Floor(s, "Base", V(0, 0, 0), V(12, 1, 8));

        // Zigzag steps — each ~2 units higher, alternating left/right
        Box(s, "StepR1", V(4, 2, 5),   V(4.5f, 0.8f, 3), matGround);
        Box(s, "StepL1", V(-4, 4, 9),  V(4.5f, 0.8f, 3), matGround);
        Box(s, "StepR2", V(3, 6, 13),  V(4.5f, 0.8f, 3), matGround);
        Box(s, "StepL2", V(-3, 8, 17), V(4.5f, 0.8f, 3), matGround);
        Box(s, "StepC",  V(0, 10, 21), V(5, 0.8f, 3),    matGround);

        // Side walls channeling the climb
        Wall(s, "ClimbWL",    V(-7, 6, 13), V(1, 16, 28));
        Wall(s, "ClimbWR",    V(7, 6, 13),  V(1, 16, 28));
        Wall(s, "ClimbWBack", V(0, 6, -1),  V(14, 16, 1));

        // Accent stripes on steps (direction cue)
        Accent(s, "StepAccR1", V(4, 2.42f, 5),   V(3, 0.06f, 0.2f));
        Accent(s, "StepAccL1", V(-4, 4.42f, 9),   V(3, 0.06f, 0.2f));
        Accent(s, "StepAccR2", V(3, 6.42f, 13),   V(3, 0.06f, 0.2f));
        Accent(s, "StepAccL2", V(-3, 8.42f, 17),  V(3, 0.06f, 0.2f));
        Accent(s, "StepAccC",  V(0, 10.42f, 21),  V(3.5f, 0.06f, 0.2f));

        // Hidden shortcut (Vision Mask reward — skips middle steps)
        var hs = Box(s, "HiddenShortcut", V(0, 6, 8), V(3, 0.4f, 3), matHidden);
        TagAsHidden(hs);
    }

    // ═══════════════════════════════════════════════════
    //  8. PORTAL ROOM — finale
    //
    //  Portal placement:
    //    Drop the Portal prefab on PORTAL_SPAWN,
    //    set targetSceneName to your next scene.
    // ═══════════════════════════════════════════════════
    private void BuildSection8_PortalRoom()
    {
        var s = Section("8_PortalRoom", V(-24, 10, 133));

        Floor(s, "Floor",     V(0, 0, 0),          V(18, 1, 14));
        Wall(s,  "WallL",     V(-9.5f, 3, 0),      V(1, 6, 14));
        Wall(s,  "WallR",     V(9.5f, 3, 0),       V(1, 6, 14));
        Wall(s,  "WallBack",  V(0, 3, -7.5f),      V(18, 6, 1));
        Wall(s,  "WallFront", V(0, 3, 7.5f),       V(18, 6, 1));

        // Portal pedestal
        Cylinder(s, "PortalBase", V(0, 0.6f, 3), V(3, 1.2f, 3), matDark);
        Box(s,      "PortalTop",  V(0, 1.5f, 3), V(2.2f, 0.3f, 2.2f), matPortal);

        // Flanking pillars
        Cylinder(s, "FinalP1", V(-6, 3.5f, -4), V(1, 7, 1), matDark);
        Cylinder(s, "FinalP2", V(6, 3.5f, -4),  V(1, 7, 1), matDark);
        Cylinder(s, "FinalP3", V(-6, 3.5f, 4),  V(1, 7, 1), matDark);
        Cylinder(s, "FinalP4", V(6, 3.5f, 4),   V(1, 7, 1), matDark);

        // Floor accent lines guiding toward portal
        Accent(s, "Guide1", V(0, 0.52f, -3),  V(0.2f, 0.06f, 8));
        Accent(s, "Guide2", V(-2, 0.52f, -1), V(0.2f, 0.06f, 5));
        Accent(s, "Guide3", V(2, 0.52f, -1),  V(0.2f, 0.06f, 5));

        Marker(s, "PORTAL_SPAWN", V(0, 2.5f, 3));
    }

    // ═══════════════════════════════════════════════════════════
    //  Primitive Builders
    // ═══════════════════════════════════════════════════════════

    private Transform Section(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        return go.transform;
    }

    private GameObject Floor(Transform p, string n, Vector3 pos, Vector3 size)
        => Box(p, n, pos, size, matGround);

    private GameObject Wall(Transform p, string n, Vector3 pos, Vector3 size)
        => Box(p, n, pos, size, matWall);

    private GameObject Accent(Transform p, string n, Vector3 pos, Vector3 size)
        => Box(p, n, pos, size, matAccent);

    private GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.isStatic = true;
        ApplyMat(go, mat);
        return go;
    }

    private GameObject Cylinder(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.isStatic = true;
        ApplyMat(go, mat);
        return go;
    }

    private void Marker(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        // Visible red cube in editor (no collider)
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "⬤ MARKER";
        vis.transform.SetParent(go.transform, false);
        vis.transform.localScale = Vector3.one * 0.6f;
        var col = vis.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        ApplyMat(vis, matDanger);
    }

    private void TagAsHidden(GameObject go)
    {
        try { go.tag = "HiddenObject"; }
        catch { Debug.LogWarning($"Create tag 'HiddenObject' in Edit > Project Settings > Tags. ({go.name})"); }
        foreach (Transform child in go.transform)
        {
            try { child.gameObject.tag = "HiddenObject"; } catch { }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Materials (URP)
    // ═══════════════════════════════════════════════════════════

    private void CreateMaterials()
    {
        matGround = MakeMat("CH2_Ground", groundColor);
        matWall   = MakeMat("CH2_Wall",   wallColor);
        matAccent = MakeMat("CH2_Accent", accentColor, true);
        matHidden = MakeMat("CH2_Hidden", hiddenColor, true);
        matDanger = MakeMat("CH2_Danger", dangerColor, true);
        matPortal = MakeMat("CH2_Portal", portalColor, true);
        matDark   = MakeMat("CH2_Dark",   darkColor);
    }

    private Material MakeMat(string name, Color color, bool emissive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader) { name = name };
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Smoothness", 0.25f);

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.35f);
        }
        return mat;
    }

    private void ApplyMat(GameObject go, Material mat)
    {
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    private Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
}

// ═══════════════════════════════════════════════════════════
//  Custom Editor
// ═══════════════════════════════════════════════════════════
#if UNITY_EDITOR
[CustomEditor(typeof(CH2MapGenerator))]
public class CH2MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(15);

        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
        if (GUILayout.Button("🔨  GENERATE CH2 MAP", GUILayout.Height(45)))
        {
            var gen = (CH2MapGenerator)target;
            Undo.RegisterCompleteObjectUndo(gen.gameObject, "Generate CH2 Map");
            gen.GenerateMap();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = new Color(1f, 0.35f, 0.25f);
        if (GUILayout.Button("🗑  Clear Map", GUILayout.Height(30)))
        {
            var gen = (CH2MapGenerator)target;
            Undo.RegisterCompleteObjectUndo(gen.gameObject, "Clear CH2 Map");
            while (gen.transform.childCount > 0)
                DestroyImmediate(gen.transform.GetChild(0).gameObject);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(12);
        EditorGUILayout.HelpBox(
            "PLACEMENT GUIDE — drop prefabs onto the RED marker cubes:\n\n" +
            "👤  Player         → 1_Spawn / PLAYER_SPAWN\n" +
            "🗡️  Sword          → 1_Spawn / SWORD_SPAWN\n" +
            "🪨  Rock Enemy #1  → 3_RockArena / ROCK_ENEMY_1\n" +
            "🪨  Rock Enemy #2  → 3_RockArena / ROCK_ENEMY_2\n" +
            "👁️  Vision Mask    → 5_MaskShrine / VISION_MASK_SPAWN\n" +
            "🌀  Portal         → 8_PortalRoom / PORTAL_SPAWN\n\n" +
            "═══ ENEMY TIPS ═══\n" +
            "• Rocks hover — Y=4 above local floor is correct\n" +
            "• They're spaced apart so charges don't stack\n" +
            "• 5 cover blocks in the arena = survival cover\n\n" +
            "═══ VISION MASK TIPS ═══\n" +
            "• Purple blocks are tagged 'HiddenObject' (invisible at start)\n" +
            "• Room 6 has TWO hidden routes:\n" +
            "    LEFT = stairs over the blocker wall\n" +
            "    RIGHT = walkway through a fake wall panel\n" +
            "• Climb section has 1 hidden shortcut platform\n\n" +
            "═══ WORLD COORDINATES (approx) ═══\n" +
            "  PLAYER_SPAWN    (0, 1.5, -3)\n" +
            "  SWORD_SPAWN     (3.5, 1, 1)\n" +
            "  ROCK_ENEMY_1    (-5, 4, 36)   ← in arena\n" +
            "  ROCK_ENEMY_2    (6, 4, 41)    ← in arena\n" +
            "  VISION_MASK     (-24, 2.5, 76)\n" +
            "  PORTAL          (-24, 12.5, 136)",
            MessageType.Info);
    }
}
#endif
