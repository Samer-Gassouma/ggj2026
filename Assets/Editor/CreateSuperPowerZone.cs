using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helper: adds a menu command to create a SuperPowerUnlockZone GameObject
/// with a trigger BoxCollider and the SuperPowerUnlockZone component.
/// Use: GameObject -> Create Other -> SuperPower Unlock Zone
/// </summary>
public static class CreateSuperPowerZone
{
    [MenuItem("GameObject/Create Other/SuperPower Unlock Zone", false, 10)]
    public static void CreateZone(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("SuperPowerUnlockZone");
        Undo.RegisterCreatedObjectUndo(go, "Create SuperPowerUnlockZone");

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(3f, 2f, 3f);

        go.AddComponent<SuperPowerUnlockZone>();

        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    [MenuItem("GameObject/Create Other/SuperPower Unlock Zone/Dash Mask Zone", false, 11)]
    public static void CreateDashZone(MenuCommand menuCommand)
    {
        var go = CreateBaseZone(menuCommand, "SuperPowerUnlockZone_Dash");
        AssignMaskAssetByName(go, "Dash");
    }

    [MenuItem("GameObject/Create Other/SuperPower Unlock Zone/Vision Mask Zone", false, 12)]
    public static void CreateVisionZone(MenuCommand menuCommand)
    {
        var go = CreateBaseZone(menuCommand, "SuperPowerUnlockZone_Vision");
        AssignMaskAssetByName(go, "Vision");
    }

    private static GameObject CreateBaseZone(MenuCommand menuCommand, string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create SuperPowerUnlockZone");

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(3f, 2f, 3f);

        go.AddComponent<SuperPowerUnlockZone>();

        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        return go;
    }

    private static void AssignMaskAssetByName(GameObject go, string partialName)
    {
        var zone = go.GetComponent<SuperPowerUnlockZone>();
        if (zone == null) return;

        string[] guids = AssetDatabase.FindAssets("t:MaskAbility");
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var asset = AssetDatabase.LoadAssetAtPath<MaskAbility>(path);
            if (asset == null) continue;
            if (asset.name.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                zone.maskToUnlock = asset;

                // Ensure the superpower is locked by default (so zone unlocks it)
                if (asset.superpowerUnlocked)
                {
                    asset.superpowerUnlocked = false;
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }

                EditorUtility.SetDirty(zone);
                Debug.Log($"Assigned mask '{asset.name}' to zone '{go.name}' and ensured superpower is locked.");
                return;
            }
        }

        Debug.LogWarning($"No MaskAbility asset matching '{partialName}' found to assign to zone '{go.name}'.");
    }
}
