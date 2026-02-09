using UnityEngine;
using UnityEngine.UI;

public class healthbar : MonoBehaviour
{
    public Slider slider;
    public Gradient gradient;
    public Image fill;
    public void SetHealth(int health)
    {
        slider.value = health;
        slider.maxValue = health;
        fill.color = gradient.Evaluate(slider.normalizedValue);
    }
    public void SetMaxHealth(int health)
    {
        slider.maxValue = health;
        slider.value = health;
        if (fill != null && gradient != null)
            fill.color = gradient.Evaluate(1f);
    }
    public void SetHealthBar(int health)
    {
        slider.value = health;
        fill.color = gradient.Evaluate(slider.normalizedValue);
    }

    public int CurrentHealth
    {
        get { return (int)slider.value; }
    }
}
