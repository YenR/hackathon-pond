using UnityEngine;

public class PondWave : MonoBehaviour
{
    [Header("Scale Waves")]
    public float waveSpeed = 2f;
    public float waveAmplitude = 0.05f;

    [Header("Color Shift")]
    public float colorShiftAmplitude = 0.1f;
    public float colorShiftSpeed = 1.5f;

    [Header("Ripple Pulse")]
    public float rippleAmplitude = 0.1f;
    public float rippleSpeed = 1f;

    private Vector3 originalScale;
    private SpriteRenderer sr;
    private Color originalColor;

    void Start()
    {
        originalScale = transform.localScale;
        sr = GetComponent<SpriteRenderer>();
        originalColor = sr.color;
    }

    void Update()
    {
        // Basic wavy scale
        float waveX = Mathf.Sin(Time.time * waveSpeed) * waveAmplitude;
        float waveY = Mathf.Cos(Time.time * waveSpeed * 1.2f) * waveAmplitude;

        // Ripple pulse from center
        float ripple = Mathf.Sin(Time.time * rippleSpeed) * rippleAmplitude;

        transform.localScale = new Vector3(
            originalScale.x + waveX + ripple,
            originalScale.y + ripple,
            originalScale.z
        );

        // Color shift
        float shift = Mathf.Sin(Time.time * colorShiftSpeed) * colorShiftAmplitude;
        sr.color = originalColor + new Color(0f, 0f, shift, 0f); // adjust blue channel
    }
}
