using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableInsideCircle : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    public Transform circleCenter; // Center of the circle in world space
    public float circleRadius = 3f; // Radius in world units

    private Vector3 offset;

    public Texture2D cursorTexture;

    public GameObject glow; // child sprite for glow

    public int level = 0; // 0 = surface, -1 = sunk a bit, etc, up to -5

    public Image img;

    public void onEnterCursor()
    {

        Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);

        //glow.SetActive(true);
    }

    public void sink()
    {
        if (level == -5)
            return;

        level--;

        Vector3 originalScale = transform.localScale;

        transform.localScale = new Vector3(
            originalScale.x * 0.8f,
            originalScale.y * 0.8f,
            originalScale.z
        );

        img.color = img.color - new Color(0f, 0f, 0f, 0.15f);
    }

    public void up()
    {
        if (level == 0)
            return;

        level++;

        Vector3 originalScale = transform.localScale;

        transform.localScale = new Vector3(
            originalScale.x / 0.8f,
            originalScale.y / 0.8f,
            originalScale.z
        );

        img.color = img.color + new Color(0f, 0f, 0f, 0.15f);
    }

    public void onClick()
    {
        if (UIScript.instance.selected != null)
            UIScript.instance.selected.glow.SetActive(false);

        glow.SetActive(true);
        UIScript.instance.showUI();
        UIScript.instance.selected = this;
    }

    public void onExitCursor()
    {

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); // revert
        //glow.SetActive(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Calculate offset in world space
        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldMousePos.z = transform.position.z;
        offset = transform.position - worldMousePos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (circleCenter == null)
        {
            if (UIScript.instance.pond == null)
                return;
            else
                circleCenter = UIScript.instance.pond;
        }

        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldMousePos.z = transform.position.z;
        Vector3 desiredPos = worldMousePos + offset;

        // Constrain to circle in world space
        Vector3 direction = desiredPos - circleCenter.position;
        if (direction.magnitude > circleRadius)
        {
            desiredPos = circleCenter.position + direction.normalized * circleRadius;
        }

        transform.position = desiredPos;
    }
}
