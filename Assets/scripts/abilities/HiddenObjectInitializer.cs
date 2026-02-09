using UnityEngine;

/// <summary>
/// Automatically hides all "HiddenObject" tagged objects at game start.
/// They become invisible + non-collidable until the Vision Mask reveals them.
/// No setup required — runs automatically via [RuntimeInitializeOnLoadMethod].
/// </summary>
public static class HiddenObjectInitializer
{
    private const string HIDDEN_TAG = "HiddenObject";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HideAllOnSceneLoad()
    {
        HideAll();
    }

    public static void HideAll()
    {
        GameObject[] hiddenObjects;

        try
        {
            hiddenObjects = GameObject.FindGameObjectsWithTag(HIDDEN_TAG);
        }
        catch (UnityException)
        {
            // Tag doesn't exist yet — nothing to hide
            Debug.LogWarning("HiddenObjectInitializer: Tag '" + HIDDEN_TAG + "' not found. Create it in Edit > Project Settings > Tags.");
            return;
        }

        if (hiddenObjects.Length == 0) return;

        foreach (var obj in hiddenObjects)
        {
            // Disable all renderers (make invisible)
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.enabled = false;
            }

            // Disable all colliders (can't interact / walk on)
            var colliders = obj.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col == null) continue;
                col.enabled = false;
            }
        }

        Debug.Log($"👁️ HiddenObjectInitializer: Hid {hiddenObjects.Length} objects with tag '{HIDDEN_TAG}'");
    }
}
