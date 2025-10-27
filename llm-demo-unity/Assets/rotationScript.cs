// RotateWhileActive.cs
using UnityEngine;

public class rotationScript : MonoBehaviour
{
    public float speed = 360f; // degrees / second

    void Update()
    {
        if (gameObject.activeSelf)
            transform.Rotate(0f, 0f, speed * Time.deltaTime);
    }
}
