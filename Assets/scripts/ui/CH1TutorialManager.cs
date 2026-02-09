using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the CH1 combat-tutorial scene with a snarky, reactive narrator.
/// 100 % self-configuring — just drop on an empty GameObject.
///
/// The narrator reacts in real-time to:
///   • How long the player takes to pick up the sword
///   • Throw results: hit enemy / missed / fell into the void
///   • Repeated failures (escalating sass)
///   • Taking damage from the eyeball
///   • Killing the enemy via melee vs throw
///   • Dying during the tutorial
/// </summary>
public class CH1TutorialManager : MonoBehaviour
{
    // ─────────────────── Timing ───────────────────
    [Header("Timing")]
    [SerializeField] private float initialDelay   = 1.5f;
    [SerializeField] private float narrativePause = 1.6f;
    [SerializeField] private float shortPause     = 0.8f;

    [Header("Distances")]
    [SerializeField] private float enemyDetectRange = 18f;

    // ─────────────────── Auto-found / auto-created ───────────────────
    private NarrativeUI        narrativeUI;
    private CombatKeyPromptUI  combatPrompt;
    private SwordController    sword;
    private EnemyBase          tutorialEnemy;
    private GameObject         portalObject;
    private Transform          playerTransform;
    private PlayerHealth       playerHP;

    // ─────────────────── State tracking ───────────────────
    private int   throwMissCount;
    private int   voidThrowCount;
    private int   timesHit;           // how many times the player took damage during combat
    private int   playerHpSnapshot;   // hp when combat starts
    private bool  playerDied;
    private bool  enemyKilledByThrow;
    private bool  enemyKilledByMelee;
    private float pickupStartTime;    // when we first told them to pick up

    // ═══════════════════════════════════════════════════════════════════
    private void Start()
    {
        // ── Find player ──
        GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerHP = player.GetComponent<PlayerHealth>();
            if (playerHP == null) playerHP = PlayerHealth.Instance;
        }

        // ── Find sword ──
        sword = FindAnyObjectByType<SwordController>(FindObjectsInactive.Include);

        // ── Find the tutorial enemy ──
        var shootEnemy = FindAnyObjectByType<EyeballShooterEnemy>(FindObjectsInactive.Include);
        if (shootEnemy != null)
            tutorialEnemy = shootEnemy;
        else
            tutorialEnemy = FindAnyObjectByType<EnemyBase>(FindObjectsInactive.Include);

        // ── Find portal ──
        Portal portalComp = FindAnyObjectByType<Portal>(FindObjectsInactive.Include);
        if (portalComp != null)
        {
            portalObject = portalComp.gameObject;

            if (portalComp.GetComponent<PortalVisual>() == null)
                portalComp.gameObject.AddComponent<PortalVisual>();
        }

        // ── Auto-create NarrativeUI ──
        narrativeUI = FindAnyObjectByType<NarrativeUI>();
        if (narrativeUI == null)
        {
            GameObject go = new GameObject("NarrativeUI");
            go.transform.SetParent(transform, false);
            narrativeUI = go.AddComponent<NarrativeUI>();
        }

        // ── Auto-create CombatKeyPromptUI ──
        combatPrompt = FindAnyObjectByType<CombatKeyPromptUI>();
        if (combatPrompt == null)
        {
            GameObject go = new GameObject("CombatKeyPromptUI");
            go.transform.SetParent(transform, false);
            combatPrompt = go.AddComponent<CombatKeyPromptUI>();
        }

        StartCoroutine(TutorialSequence());
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Main flow
    // ═══════════════════════════════════════════════════════════════════
    /// <summary>Re-acquire references that may have been null at Start or got destroyed.</summary>
    private void RefreshReferences()
    {
        if (sword == null)
            sword = FindAnyObjectByType<SwordController>(FindObjectsInactive.Include);

        if (tutorialEnemy == null)
        {
            var shootEnemy = FindAnyObjectByType<EyeballShooterEnemy>(FindObjectsInactive.Include);
            if (shootEnemy != null)
                tutorialEnemy = shootEnemy;
            else
                tutorialEnemy = FindAnyObjectByType<EnemyBase>(FindObjectsInactive.Include);
        }

        if (playerTransform == null || playerHP == null)
        {
            GameObject player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerHP = player.GetComponent<PlayerHealth>() ?? PlayerHealth.Instance;
            }
        }
    }

    private IEnumerator TutorialSequence()
    {
        yield return new WaitForSeconds(initialDelay);

        // Always refresh before decisions
        RefreshReferences();

        // Skip sword pickup if player already has it
        if (sword == null || !sword.IsHeld)
        {
            yield return StartCoroutine(Phase_PickupSword());
        }
        else
        {
            yield return Say("You already grabbed the sword. Nice, you don't waste time.");
        }

        // Refresh again — time may have passed during Phase 1
        RefreshReferences();

        // Skip enemy approach if enemy is already dead
        bool enemyAlive = tutorialEnemy != null && tutorialEnemy.GetCurrentHealth() > 0;

        if (enemyAlive)
        {
            // Skip approach narration if already close
            bool alreadyClose = playerTransform != null && tutorialEnemy != null &&
                Vector3.Distance(playerTransform.position, tutorialEnemy.transform.position) < enemyDetectRange;

            if (!alreadyClose)
            {
                yield return StartCoroutine(Phase_ApproachEnemy());
            }
            else
            {
                yield return Say("You're already face-to-face with the eyeball. Bold move.");
            }

            // Refresh — enemy may have died during approach narration
            RefreshReferences();
            yield return StartCoroutine(Phase_Combat());
        }
        else
        {
            yield return Say("The eyeball is already dead?! You work fast. I'm impressed... sort of.");
            enemyKilledByMelee = true; // assume they killed it
        }

        yield return StartCoroutine(Phase_Victory());
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Phase 1  —  Pick Up the Sword
    // ═══════════════════════════════════════════════════════════════════
    private IEnumerator Phase_PickupSword()
    {
        // Double-check — player might have grabbed it during initial delay
        if (sword != null && sword.IsHeld)
        {
            combatPrompt.Hide();
            yield return Say("Oh, you already picked up the sword. Quick hands!");
            yield break;
        }

        yield return Say("Something glimmers to your right... not left, RIGHT.");

        combatPrompt.ShowPickupOnly();
        yield return Say("Walk over and press  F  to pick it up. Go RIGHT.");

        pickupStartTime = Time.time;
        bool nagged = false;
        float lastRefresh = Time.time;

        // Wait for pickup — re-find sword reference periodically in case it was null at start
        while (sword == null || !sword.IsHeld)
        {
            // Re-find sword every 2 seconds in case it was null initially
            if (sword == null && Time.time - lastRefresh > 2f)
            {
                sword = FindAnyObjectByType<SwordController>(FindObjectsInactive.Include);
                lastRefresh = Time.time;
            }

            float waited = Time.time - pickupStartTime;

            if (!nagged && waited > 12f)
            {
                nagged = true;
                yield return Say("It's RIGHT there... the shiny thing. To your RIGHT. Not left. Walk. To. It.");
            }
            else if (nagged && waited > 25f)
            {
                yield return Say("Okay I'm starting to worry. The sword is on the RIGHT. Press F near it.");
                nagged = false; // reset so it doesn't spam
                pickupStartTime = Time.time; // reset timer
            }

            yield return null;
        }

        // Sword is picked up — IMMEDIATELY hide the F prompt
        combatPrompt.Hide();
        yield return new WaitForSeconds(0.3f);

        // Only comment on pickup time if we actually waited
        if (pickupStartTime > 0f)
        {
            float pickupTime = Time.time - pickupStartTime;
            if (pickupTime < 5f)
                yield return Say("A sword. Not bad — you found it pretty fast.");
            else if (pickupTime < 15f)
                yield return Say("A sword. Took you a moment, but you got there.");
            else
                yield return Say("A sword. Finally. I was about to give up on you.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Phase 2  —  Walk to the Enemy
    // ═══════════════════════════════════════════════════════════════════
    private IEnumerator Phase_ApproachEnemy()
    {
        // Enemy already dead mid-approach? Skip.
        if (tutorialEnemy == null || tutorialEnemy.GetCurrentHealth() <= 0)
        {
            yield return Say("The eyeball's already gone. You're ahead of the script.");
            yield break;
        }

        yield return Say("Now head forward... see that floating eyeball?");
        yield return Say("That's a Shooter Enemy. It fires projectiles. You'll need to deal with it.");
        yield return Say("Why? Because the portal is past it — and you can't reach the platform without a mask.");
        yield return Say("So... kill the eyeball, pick up a mask from it, equip it (press TAB), then jump to the portal.");
        narrativeUI.HideNarrative();

        if (tutorialEnemy != null && playerTransform != null)
        {
            yield return new WaitUntil(() =>
                tutorialEnemy == null ||
                Vector3.Distance(playerTransform.position, tutorialEnemy.transform.position) < enemyDetectRange);
        }

        yield return new WaitForSeconds(0.3f);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Phase 3  —  Combat (the big one)
    // ═══════════════════════════════════════════════════════════════════
    private IEnumerator Phase_Combat()
    {
        if (tutorialEnemy == null || tutorialEnemy.GetCurrentHealth() <= 0)
            yield break;

        // Snapshot player HP
        playerHpSnapshot = playerHP != null ? playerHP.currentHealth : 100;
        timesHit = 0;

        yield return Say("Alright, the eyeball sees you. Time to fight!");
        yield return new WaitForSeconds(shortPause);

        // Show combat keys
        combatPrompt.ShowCombatKeys();

        yield return Say("Left Click  to swing your sword up close, or hold  G  to aim and throw it.");
        yield return Say("The sword will fly back to you after a throw — don't worry about losing it.");

        // ── Monitor the fight frame-by-frame ──
        int lastThrowCompletion = sword != null ? sword.ThrowCompletionCount : 0;
        bool wasThrown = false;
        int enemyHpWhenThrown = 0;

        while (tutorialEnemy != null && tutorialEnemy.GetCurrentHealth() > 0)
        {
            // Check if player died
            if (playerHP != null && playerHP.currentHealth <= 0)
            {
                playerDied = true;
                yield return Say("You died. In the tutorial. Let that sink in.");
                yield break;
            }

            // Track taking damage
            if (playerHP != null && playerHP.currentHealth < playerHpSnapshot)
            {
                playerHpSnapshot = playerHP.currentHealth;
                timesHit++;

                if (timesHit == 1)
                    yield return SayQuick("Ow! Try dodging those projectiles. Move around!");
                else if (timesHit == 2)
                    yield return SayQuick("Again?! Seriously, don't just stand there.");
                else if (timesHit == 3)
                    yield return SayQuick("You're getting hit a LOT. This is chapter 1, you know.");
                else if (timesHit >= 4)
                    yield return SayQuick("At this rate the eyeball's gonna win. Move your feet!");
            }

            // Detect throw start
            if (sword != null && (sword.IsThrown || sword.IsReturning) && !wasThrown)
            {
                wasThrown = true;
                enemyHpWhenThrown = tutorialEnemy != null ? tutorialEnemy.GetCurrentHealth() : 0;
            }

            // Detect throw completion (sword returned to hand)
            if (sword != null && sword.ThrowCompletionCount > lastThrowCompletion)
            {
                lastThrowCompletion = sword.ThrowCompletionCount;
                wasThrown = false;

                // Read what happened
                var result = sword.LastThrowResult;
                int enemyHpNow = (tutorialEnemy != null) ? tutorialEnemy.GetCurrentHealth() : 0;
                bool hitEnemy = result == SwordController.ThrowResult.HitEnemy ||
                                enemyHpNow < enemyHpWhenThrown;

                if (hitEnemy && enemyHpNow > 0)
                {
                    // Hit but didn't kill
                    yield return StartCoroutine(ReactToThrowHit());
                }
                else if (hitEnemy && enemyHpNow <= 0)
                {
                    // Kill shot with throw!
                    enemyKilledByThrow = true;
                    break;
                }
                else
                {
                    // Missed
                    yield return StartCoroutine(ReactToThrowMiss(result));
                }
            }

            // Detect melee kill (enemy HP drops to 0 while sword is held, not mid-throw)
            if (sword != null && sword.IsHeld && !wasThrown &&
                tutorialEnemy != null && tutorialEnemy.GetCurrentHealth() <= 0)
            {
                enemyKilledByMelee = true;
                break;
            }

            yield return null;
        }

        combatPrompt.Hide();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Throw-hit reactions
    // ─────────────────────────────────────────────────────────────────
    private int throwHitCount = 0;
    private IEnumerator ReactToThrowHit()
    {
        throwHitCount++;
        switch (throwHitCount)
        {
            case 1:
                yield return Say("Nice! You're not as useless as I thought. It actually hit.");
                break;
            case 2:
                yield return Say("Another hit! Okay okay, maybe you DO know what you're doing.");
                break;
            default:
                yield return SayQuick("Hit! Keep it up.");
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Throw-miss reactions (escalating sass)
    // ─────────────────────────────────────────────────────────────────
    private IEnumerator ReactToThrowMiss(SwordController.ThrowResult result)
    {
        throwMissCount++;

        if (result == SwordController.ThrowResult.FellInVoid)
        {
            voidThrowCount++;
            yield return StartCoroutine(ReactToVoidThrow());
        }
        else
        {
            yield return StartCoroutine(ReactToGenericMiss());
        }
    }

    private IEnumerator ReactToVoidThrow()
    {
        switch (voidThrowCount)
        {
            case 1:
                yield return Say("You just threw your sword into the VOID. Seriously?!");
                yield return Say("We're in chapter 1. The TUTORIAL. What a loser... here, it came back.");
                break;
            case 2:
                yield return Say("Into the void. AGAIN. What are you aiming at, the ground beneath the ground?!");
                yield return SayQuick("The sword returned. Please... aim at the ENEMY this time.");
                break;
            case 3:
                yield return Say("THREE times into the abyss!! Are you doing this on purpose?!");
                yield return SayQuick("I'm begging you. The eyeball is RIGHT THERE.");
                break;
            case 4:
                yield return Say("I... I don't even have words anymore. The void thanks you for the donations.");
                break;
            default:
                yield return Say($"Void throw #{voidThrowCount}. I've stopped being surprised. Just... try again, champ.");
                break;
        }
    }

    private IEnumerator ReactToGenericMiss()
    {
        int genericMiss = throwMissCount - voidThrowCount;
        switch (genericMiss)
        {
            case 1:
                yield return Say("Missed! The sword came back though. Try aiming AT the eyeball next time.");
                break;
            case 2:
                yield return Say("Still can't hit it? It's a GIANT EYEBALL. It's not exactly a small target.");
                break;
            case 3:
                yield return Say("You know what, maybe just try Left Click. Walk up and smack it. Old school.");
                break;
            case 4:
                yield return Say("I'm starting to think the eyeball is safer from your throws than anything else.");
                break;
            default:
                if (throwMissCount >= 6)
                    yield return Say($"Miss #{throwMissCount}. I respect your persistence, if not your aim.");
                else
                    yield return SayQuick("Missed. Sword's back. Try again.");
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Phase 4  —  Victory & Portal
    // ═══════════════════════════════════════════════════════════════════
    private IEnumerator Phase_Victory()
    {
        if (playerDied) yield break;

        yield return new WaitForSeconds(0.8f);

        // ── React to HOW they killed it ──
        if (enemyKilledByThrow)
        {
            if (throwMissCount == 0)
                yield return Say("One throw, one kill. Okay... I'll shut up. That was genuinely clean.");
            else if (throwMissCount <= 2)
                yield return Say("Got it with the throw! Took a couple tries, but hey — dead eyeball is dead eyeball.");
            else
                yield return Say($"FINALLY the throw connected! Only took you... {throwMissCount + 1} attempts. But dead is dead, right?");
        }
        else if (enemyKilledByMelee)
        {
            if (throwMissCount == 0 && timesHit == 0)
                yield return Say("Pure melee, no damage taken. Okay you actually went full berserker and it WORKED.");
            else if (throwMissCount > 2)
                yield return Say("Gave up on throwing and just hacked it to death? Honestly... fair enough.");
            else if (timesHit > 2)
                yield return Say("You killed it! But you look like you went through a blender doing it.");
            else
                yield return Say("Sliced and diced! Not bad for a tutorial fight.");
        }
        else
        {
            yield return Say("Well... it's dead somehow. I won't question it.");
        }

        yield return new WaitForSeconds(shortPause);

        // ── Overall performance summary ──
        if (timesHit == 0 && throwMissCount == 0)
        {
            yield return Say("Flawless run. No damage, no misses. Chapter 1 didn't stand a chance.");
        }
        else if (timesHit >= 4 && throwMissCount >= 3)
        {
            yield return Say("That was... rough. You missed a lot AND got hit a lot. But you're alive. Barely.");
        }
        else if (timesHit >= 4)
        {
            yield return Say("You took a LOT of hits back there. Maybe try not standing still next time.");
        }
        else if (throwMissCount >= 4)
        {
            yield return Say("Your aim needs... work. Serious work. But you survived, so there's that.");
        }

        yield return new WaitForSeconds(shortPause);
        yield return Say("Good. The eyeball dropped a mask — pick it up, press TAB to equip it.");
        yield return Say("Then use the mask's ability to reach the portal platform. You'll figure it out.");

        // Activate portal
        if (portalObject != null)
            portalObject.SetActive(true);

        yield return new WaitForSeconds(4f);
        narrativeUI.HideNarrative();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════
    /// <summary>Show narrative + wait for typewriter + full pause.</summary>
    private IEnumerator Say(string line)
    {
        narrativeUI.ShowNarrative(line);
        yield return new WaitForSeconds(line.Length * 0.03f + narrativePause);
    }

    /// <summary>Show narrative + shorter wait (for mid-combat quips).</summary>
    private IEnumerator SayQuick(string line)
    {
        narrativeUI.ShowNarrative(line);
        yield return new WaitForSeconds(line.Length * 0.03f + shortPause);
    }
}
