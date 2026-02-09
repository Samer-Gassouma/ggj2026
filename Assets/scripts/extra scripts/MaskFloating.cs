using UnityEngine;

public class MaskFloating : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationSpeed = new Vector3(0f, 50f, 0f); // degrees per second

    [Header("Floating Settings")]
    public float floatAmplitude = 0.25f; // how high it moves up and down
    public float floatFrequency = 1f;    // speed of floating

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // Rotate
        transform.Rotate(rotationSpeed * Time.deltaTime);

        // Float up and down
        float newY = startPos.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
