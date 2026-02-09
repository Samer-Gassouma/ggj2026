using System.Collections;
using UnityEngine;

/// <summary>
/// CH2 Tutorial Narrator — introduces the Rock Charger enemy and the Vision Mask.
/// Same snarky narrator from CH1. Self-configuring, no Inspector wiring needed.
///
/// Flow:
/// /// ///   Phase 1 — Intro: welcome back, you still have double jump from the Dash Mask
/// /// ///   Phase 2 — Rock Arena: meet the Rock Charger — it charges and pushes you
/// /// ///   Phase 3 — Vision Mask Pickup: find the Vision Mask past the wall
///   Phase 4 — Hidden Route: objects with glitch effect appear — use them to pass
/// /// ///   Phase 5 — Climb & Finale: reach the portal
/// /// /// </summary>
public class CH2TutorialManager : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float initialDelay   = 2f;
    [SerializeField] private float narrativePause  = 1.6f;
    [SerializeField] private float shortPause      = 0.8f;

    [Header("Distances")]
    [SerializeField] private float rockDetectRange = 22f;
    [SerializeField] private float maskPickupRange = 8f;
    [SerializeField] private float deadEndRange    = 6f;

    // ─── Auto-found ───
    private NarrativeUI       narrativeUI;
    private CombatKeyPromptUI combatPrompt;
    private SwordController   sword;
    private Transform         playerTransform;
    private PlayerHealth      playerHP;
    private PlayerMana        playerMana;

    // Rock enemies in this scene
    private RockChargerEnemy[] rockEnemies;
    private int rockEnemiesTotal;

    // Vision mask pickup
    private MaskPickup visionMaskPickup;

    // Portal
    private GameObject portalObject;

    // ─── State ───
    private int  timesHit;
    private int  playerHpSnapshot;
    private bool playerDied;
    private bool hasVisionMask;

    // ═══════════════════════════════════════════════════════════
    private void Start()
    {
        // Player
        GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerHP   = player.GetComponent<PlayerHealth>() ?? PlayerHealth.Instance;
            playerMana = player.GetComponent<PlayerMana>()   ?? PlayerMana.Instance;
        }

        // Sword
        sword = FindAnyObjectByType<SwordController>(FindObjectsInactive.Include);

        // Rock enemies
        rockEnemies = FindObjectsByType<RockChargerEnemy>(FindObjectsSortMode.None);
        rockEnemiesTotal = rockEnemies != null ? rockEnemies.Length : 0;

        // Vision mask pickup
        var pickups = FindObjectsByType<MaskPickup>(FindObjectsSortMode.None);
        foreach (var p in pickups)
        {
            if (p.maskToGive != null && p.maskToGive is VisionMaskAbility)
            {
                visionMaskPickup = p;
                break;
            }
        }

        // Portal
        Portal portalComp = FindAnyObjectByType<Portal>(FindObjectsInactive.Include);
        if (portalComp != null)
        {
            portalObject = portalComp.gameObject;
            if (portalComp.GetComponent<PortalVisual>() == null)
                portalComp.gameObject.AddComponent<PortalVisual>();
        }

        // UI
        narrativeUI = FindAnyObjectByType<NarrativeUI>();
        if (narrativeUI == null)
        {
            var go = new GameObject("NarrativeUI");
            go.transform.SetParent(transform, false);
            narrativeUI = go.AddComponent<NarrativeUI>();
        }

        combatPrompt = FindAnyObjectByType<CombatKeyPromptUI>();
        if (combatPrompt == null)
        {
            var go = new GameObject("CombatKeyPromptUI");
            go.transform.SetParent(transform, false);
            combatPrompt = go.AddComponent<CombatKeyPromptUI>();
        }

        StartCoroutine(TutorialSequence());
    }

    // ═══════════════════════════════════════════════════════════
    //  Refresh references (in case of timing issues)
    // ═══════════════════════════════════════════════════════════
    private void RefreshReferences()
    {
        if (sword == null)
            sword = FindAnyObjectByType<SwordController>(FindObjectsInactive.Include);

        if (playerTransform == null || playerHP == null)
        {
            GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerHP   = player.GetComponent<PlayerHealth>() ?? PlayerHealth.Instance;
                playerMana = player.GetComponent<PlayerMana>()   ?? PlayerMana.Instance;
            }
        }

        // Re-count living rock enemies
        rockEnemies = FindObjectsByType<RockChargerEnemy>(FindObjectsSortMode.None);
    }

    // ═══════════════════════════════════════════════════════════
    //  Main Flow
    // ═══════════════════════════════════════════════════════════
    private IEnumerator TutorialSequence()
    {
        yield return new WaitForSeconds(initialDelay);
        RefreshReferences();

        // Phase 1 — Intro
        yield return StartCoroutine(Phase_Intro());

        // Phase 2 — Rock Arena
        RefreshReferences();
        if (rockEnemies != null && rockEnemies.Length > 0 && AnyRockAlive())
        {
            yield return StartCoroutine(Phase_RockArena());
        }
        else
        {
            yield return Say("The rocks are already dead? You don't even wait for my monologues anymore.");
        }

        // Phase 3 — Vision Mask Pickup
        RefreshReferences();
        yield return StartCoroutine(Phase_VisionMaskPickup());

        // Phase 4 — Hidden Route
        RefreshReferences();
        yield return StartCoroutine(Phase_HiddenRoute());

        // Phase 5 — Climb to Finale
        yield return StartCoroutine(Phase_Finale());
    }

    // ═══════════════════════════════════════════════════════════
    //  Phase 1 — Intro
    // ═══════════════════════════════════════════════════════════
    private IEnumerator Phase_Intro()
    {
        yield return Say("Oh, you're back. Survived chapter 1, congratulations.");
        yield return Say("Good news — you still have the double jump from the Dash Mask.");
        yield return Say("Press SPACE mid-air to double jump. You'll need it.");
        yield return new WaitForSeconds(shortPause);
        yield return Say("This time the enemies are... bigger. And angrier. Good luck.");

        if (sword != null && sword.IsHeld)
        {
            yield return Say("Good, you already have the sword. Let's move.");
        }
        else
        {
            yield return Say("Grab your sword first. You remember how, right? Press F. Go RIGHT.");
            // Wait for sword pickup
            float t = Time.time;
            while (sword == null || !sword.IsHeld)
            {
                if (sword == null) sword = FindAnyObjectByType<SwordController>(FindObjectsInactive.Include);
                if (Time.time - t > 15f) { yield return Say("The sword. Pick it up. Please."); t = Time.time; }
                yield return null;
            }
            yield return Say("There we go. Now move forward.");
        }

        yield return new WaitForSeconds(shortPause);
        combatPrompt.ShowCombatKeys();
        yield return new WaitForSeconds(3f);
        combatPrompt.Hide();
    }

    // ═══════════════════════════════════════════════════════════
    //  Phase 2 — Rock Arena
    // ═══════════════════════════════════════════════════════════
    private IEnumerator Phase_RockArena()
    {
        // Wait until player gets close to a rock enemy
        yield return WaitForNearEnemy(rockDetectRange);

        if (!AnyRockAlive()) yield break;

        yield return Say("See that big floating boulder? That's a Rock Charger.");
        yield return Say("It'll charge straight at you, HIT you and PUSH you back. Hard.");
        yield return Say("Get knocked off the edge and... well, you know how gravity works.");
        yield return Say("Use the cover blocks to bait its charges, then strike while it recovers.");

        playerHpSnapshot = playerHP != null ? playerHP.currentHealth : 100;
        timesHit = 0;

        // Monitor combat
        float lastCommentTime = Time.time;
        int lastRockCount = CountAliveRocks();

        while (AnyRockAlive())
        {
            // Player died?
            if (playerHP != null && playerHP.currentHealth <= 0)
            {
                playerDied = true;
                yield return Say("You got flattened by a rock. In chapter 2. Wonderful.");
                yield break;
            }

            // Took damage
            if (playerHP != null && playerHP.currentHealth < playerHpSnapshot)
            {
                playerHpSnapshot = playerHP.currentHealth;
                timesHit++;

                if (timesHit == 1)
                    yield return SayQuick("Ouch! I told you to dodge. Use the cover blocks!");
                else if (timesHit == 2)
                    yield return SayQuick("Again?! It literally winds up before charging. READ the telegraph.");
                else if (timesHit == 3)
                    yield return SayQuick("You're getting demolished. Maybe try... moving?");
                else if (timesHit >= 4 && Time.time - lastCommentTime > 8f)
                {
                    yield return SayQuick("At this point the rocks are bullying you.");
                    lastCommentTime = Time.time;
                }
            }

            // A rock died
            int currentCount = CountAliveRocks();
            if (currentCount < lastRockCount)
            {
                int killed = lastRockCount - currentCount;
                lastRockCount = currentCount;

                if (currentCount > 0)
                    yield return SayQuick($"One down! {currentCount} more to go.");
                // else loop will exit
            }

            yield return null;
        }

        combatPrompt.Hide();
        yield return new WaitForSeconds(0.8f);

        // React to performance
        if (timesHit == 0)
            yield return Say("Not a single hit taken against the Rock Chargers. Okay, you're getting good.");
        else if (timesHit <= 2)
            yield return Say("Rock Chargers down. You took a couple hits, but nothing embarrassing.");
        else
            yield return Say("They're dead, but you look like you went through a rock tumbler. Moving on...");

        yield return new WaitForSeconds(shortPause);
        yield return Say("Now keep going. There's something interesting ahead...");
    }

    // ═══════════════════════════════════════════════════════════
    //  Phase 3 — Vision Mask Pickup
    // ═══════════════════════════════════════════════════════════
    private IEnumerator Phase_VisionMaskPickup()
    {
        // Check if player already has a vision mask equipped
        var maskCtrl = playerTransform != null ? playerTransform.GetComponent<PlayerMaskController>() : null;
        bool alreadyHasVision = maskCtrl != null && maskCtrl.ActiveMask is VisionMaskAbility;

        if (alreadyHasVision)
        {
            hasVisionMask = true;
            yield return Say("You already have the Vision Mask? You're full of surprises.");
            yield break;
        }

        yield return Say("Keep going. Past the wall you'll find something useful.");
        yield return Say("The Vision Mask. It lets you see what's hidden.");

        // If there's a vision mask pickup, wait for player to be near it
        if (visionMaskPickup != null)
        {
            yield return WaitUntilClose(visionMaskPickup.transform, maskPickupRange);

            yield return Say("There it is. The Vision Mask. Walk into it to pick it up.");
            yield return Say("Then press TAB to equip it.");

            // Wait for them to pick it up
            float t = Time.time;
            while (visionMaskPickup != null)
            {
                if (Time.time - t > 20f)
                {
                    yield return Say("The glowing mask. Right there. Walk into it.");
                    t = Time.time;
                }
                yield return null;
            }

            hasVisionMask = true;
            yield return new WaitForSeconds(0.5f);
            yield return Say("Got it! Now press TAB to put it on.");
            yield return Say("With the Vision Mask on, you'll see objects with a glitch effect.");
            yield return Say("Those glitchy platforms? They're real. You can walk on them.");
        }
        else
        {
            yield return Say("There should be a Vision Mask around here somewhere...");
            yield return Say("Keep exploring. You'll need it to progress.");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Phase 4 — Hidden Route
    // ═══════════════════════════════════════════════════════════
    private IEnumerator Phase_HiddenRoute()
    {
        yield return new WaitForSeconds(shortPause);
        yield return Say("See that wall blocking your path? Looks like a dead end.");
        yield return Say("Equip the Vision Mask — TAB — and look again.");
        yield return Say("See those objects with the glitch effect? Those are hidden platforms.");
        yield return Say("They're real. You can stand on them. Use them to pass.");

        yield return new WaitForSeconds(shortPause);

        // Wait until player reaches higher ground (past the hidden route)
        if (playerTransform != null)
        {
            float targetY = playerTransform.position.y + 3f; // they need to climb
            float waitStart = Time.time;
            bool hinted = false;

            while (playerTransform != null && playerTransform.position.y < targetY)
            {
                if (!hinted && Time.time - waitStart > 25f)
                {
                    hinted = true;
                    yield return Say("Still stuck? Press TAB to activate the Vision Mask. Look for the glitchy objects.");
                    yield return Say("There's more than one hidden route. Check both sides.");
                }
                yield return null;
            }
        }

        yield return new WaitForSeconds(1f);
        yield return Say("You found the way through! Those glitchy platforms are your new best friend.");
        yield return Say("Keep the Vision Mask handy. You never know what's hidden ahead.");
    }

    // ═══════════════════════════════════════════════════════════
    //  Phase 5 — Finale
    // ═══════════════════════════════════════════════════════════
    private IEnumerator Phase_Finale()
    {
        if (playerDied) yield break;

        yield return new WaitForSeconds(shortPause);
        yield return Say("Almost there. The portal is just ahead.");
        yield return Say("Chapter 2 done. Rocks that push you, glitchy platforms, double jumps...");

        if (timesHit == 0)
            yield return Say("And the Rock Charger didn't land a single hit. Okay, I'm impressed.");
        else if (timesHit >= 4)
            yield return Say("You got pushed around a lot by those rocks. But you're still alive.");

        yield return Say("Step into the portal when you're ready. Things only get harder from here.");

        yield return new WaitForSeconds(4f);
        narrativeUI.HideNarrative();
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private bool AnyRockAlive()
    {
        if (rockEnemies == null) return false;
        foreach (var r in rockEnemies)
            if (r != null && r.GetCurrentHealth() > 0) return true;
        return false;
    }

    private int CountAliveRocks()
    {
        int count = 0;
        if (rockEnemies == null) return 0;
        foreach (var r in rockEnemies)
            if (r != null && r.GetCurrentHealth() > 0) count++;
        return count;
    }

    private IEnumerator WaitForNearEnemy(float range)
    {
        if (playerTransform == null) yield break;
        if (rockEnemies == null || rockEnemies.Length == 0) yield break;

        while (true)
        {
            foreach (var r in rockEnemies)
            {
                if (r != null && Vector3.Distance(playerTransform.position, r.transform.position) < range)
                    yield break;
            }
            yield return null;
        }
    }

    private IEnumerator WaitUntilClose(Transform target, float range)
    {
        if (playerTransform == null || target == null) yield break;
        while (target != null && Vector3.Distance(playerTransform.position, target.position) > range)
            yield return null;
    }

    private IEnumerator Say(string line)
    {
        narrativeUI.ShowNarrative(line);
        yield return new WaitForSeconds(line.Length * 0.03f + narrativePause);
    }

    private IEnumerator SayQuick(string line)
    {
        narrativeUI.ShowNarrative(line);
        yield return new WaitForSeconds(line.Length * 0.03f + shortPause);
    }
}
