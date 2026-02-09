using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single key prompt on screen (e.g. "W", "A", "S", "D", "Space").
/// Changes colour when the mapped key is pressed.
/// All references are set via <see cref="Init"/> — no Inspector wiring needed.
/// </summary>
public class KeyClickEffect : MonoBehaviour
{
    [Header("Key Mapping")]
    public KeyCode key = KeyCode.W;

    [Header("Colours")]
    public Color normalColor  = new Color(0.15f, 0.15f, 0.15f, 1f);
    public Color pressedColor = new Color(0.2f, 0.8f, 0.4f, 1f);

    [Header("Animation")]
    public float lerpSpeed = 10f;

    /// <summary>True after the player has pressed this key at least once.</summary>
    public bool HasBeenPressed { get; private set; }

    // Set at runtime by Init()
    private Image   keyBackground;
    private TextMeshProUGUI keyLabel;
    private Color   targetColor;

    /// <summary>
    /// One-shot initialiser called by KeyPromptUI after the GO is built.
    /// </summary>
    public void Init(KeyCode keyCode, string label, Image bg, TextMeshProUGUI tmp)
    {
        key           = keyCode;
        keyBackground = bg;
        keyLabel      = tmp;

        if (keyLabel != null) keyLabel.text = label;

        targetColor = normalColor;
        if (keyBackground != null) keyBackground.color = normalColor;
    }

    /// <summary>
    /// Legacy setup (label-only).
    /// </summary>
    public void Setup(KeyCode keyCode, string label)
    {
        key = keyCode;
        if (keyLabel != null) keyLabel.text = label;
    }

    private void Update()
    {
        if (Input.GetKeyDown(key))
        {
            targetColor    = pressedColor;
            HasBeenPressed = true;
        }
        else if (Input.GetKeyUp(key))
        {
            targetColor = normalColor;
        }

        if (keyBackground != null)
            keyBackground.color = Color.Lerp(keyBackground.color, targetColor, Time.deltaTime * lerpSpeed);
    }
}
