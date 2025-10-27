using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;       // The player
    public Vector3 offset = new Vector3(0, 0, -10); // Keeps camera at a distance

    [Header("Follow Settings")]
    public float smoothSpeed = 0.125f; // How smooth the camera follows

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }
}
