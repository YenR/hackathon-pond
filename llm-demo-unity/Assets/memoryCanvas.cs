using UnityEngine;

public class memoryCanvas : MonoBehaviour
{

    public static memoryCanvas instance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public GameObject memoPrefab;

    public Transform player, pond;

    public Canvas canvas;

    public void spawnNewMemory()
    {
        if (memoPrefab == null || player == null || pond == null) return;

        // Calculate spawn position: 2/3 towards pond, 1/3 towards player
        Vector3 spawnPos = Vector3.Lerp(pond.position, player.position, 0.6f);

        // Instantiate prefab
        GameObject newMemo = Instantiate(memoPrefab, spawnPos, Quaternion.identity);

        // If the prefab should be under the Canvas (UI element)
        if (canvas != null)
        {
            newMemo.transform.SetParent(canvas.transform, worldPositionStays: true);
        }

        if (UIScript.instance.selected != null)
            UIScript.instance.selected.glow.SetActive(false);
        UIScript.instance.selected = newMemo.GetComponent<DraggableInsideCircle>();
    }

}
