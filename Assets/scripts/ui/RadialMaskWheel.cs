using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadialMaskWheel : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerMaskController maskController;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("UI")]
    [SerializeField] private CanvasGroup wheelGroup;
    [SerializeField] private RectTransform container;
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private float radius = 200f;
    [SerializeField] private float iconSize = 64f;

    [Header("Input")]
    [SerializeField] private KeyCode openKey = KeyCode.Tab;
    [SerializeField] private float slowTimeScale = 0.05f;

    [Header("Hover Settings")]
    [SerializeField] private float hoverScale = 1.35f;
    [SerializeField] private float scaleSpeed = 10f;

    private bool isOpen = false;
    private readonly List<Image> itemIcons = new List<Image>();

    private void Awake()
    {
        if (wheelGroup == null) wheelGroup = GetComponent<CanvasGroup>();
        HideWheel();
    }

    private void Update()
    {
        if (!isOpen && Input.GetKeyDown(openKey))
            OpenWheel();
        else if (isOpen && Input.GetKeyUp(openKey))
            ConfirmSelection();
        else if (isOpen)
            UpdateSelectionVisual();
    }

    private void OpenWheel()
    {
        if (maskController == null || maskController.Inventory.Count == 0) return;

        isOpen = true;
        maskController.SetInputSelectionEnabled(false);
        if (playerMovement != null) playerMovement.SetCanMove(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = slowTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        BuildItems();
        ShowWheel();
    }

    private void ConfirmSelection()
    {
        isOpen = false;

        int sel = GetHoveredIndex();
        if (sel >= 0) maskController.ActivateIndex(sel);

        ClearItems();
        HideWheel();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        maskController.SetInputSelectionEnabled(true);
        if (playerMovement != null) playerMovement.SetCanMove(true);
    }

    private void BuildItems()
    {
        ClearItems();
        var inv = maskController.Inventory;
        int count = inv.Count;
        if (count == 0 || container == null) return;

        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("MaskIcon_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(container, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            float angle = (angleStep * i) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            rt.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            var ability = inv[i];
            img.sprite = ability.icon != null ? ability.icon : fallbackIcon;
            img.color = Color.white;

            itemIcons.Add(img);
        }
    }

    private void ClearItems()
    {
        itemIcons.Clear();
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    private void ShowWheel()
    {
        if (wheelGroup == null) return;
        StopAllCoroutines();
        StartCoroutine(FadeWheel(1f, 0.2f));
    }

    private void HideWheel()
    {
        if (wheelGroup == null) return;
        StopAllCoroutines();
        StartCoroutine(FadeWheel(0f, 0.2f));
    }

    private System.Collections.IEnumerator FadeWheel(float targetAlpha, float duration)
    {
        float startAlpha = wheelGroup.alpha;
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            wheelGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }
        wheelGroup.alpha = targetAlpha;
        wheelGroup.blocksRaycasts = targetAlpha > 0f;
        wheelGroup.interactable = targetAlpha > 0f;
    }


    private void UpdateSelectionVisual()
    {
        int sel = GetHoveredIndex();
        for (int i = 0; i < itemIcons.Count; i++)
        {
            var img = itemIcons[i];
            float targetScale = (i == sel) ? hoverScale : 1f;

            // Smoothly interpolate scale
            img.transform.localScale = Vector3.Lerp(img.transform.localScale, Vector3.one * targetScale, Time.unscaledDeltaTime * scaleSpeed);

            // Smoothly interpolate alpha
            float targetAlpha = (i == sel) ? 1f : 0.7f;
            Color c = img.color;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.unscaledDeltaTime * scaleSpeed);
            img.color = c;
        }
    }

    private int GetHoveredIndex()
    {
        var inv = maskController.Inventory;
        int count = inv.Count;
        if (count == 0) return -1;

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 mouse = Input.mousePosition;
        Vector2 dir = (mouse - center).normalized;

        // If mouse is very close to center, keep current selection
        if ((mouse - center).sqrMagnitude < 25f)
            return maskController.ActiveIndex;

        // Build direction vector for each icon
        float angleStep = 360f / count;
        int bestIndex = 0;
        float maxDot = -1f;

        for (int i = 0; i < count; i++)
        {
            float angle = angleStep * i;
            Vector2 iconDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            float dot = Vector2.Dot(dir, iconDir); // higher dot = closer direction
            if (dot > maxDot)
            {
                maxDot = dot;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

}
