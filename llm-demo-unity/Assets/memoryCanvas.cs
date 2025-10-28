using UnityEngine;
using System;
using System.Collections.Generic;

public class memoryCanvas : MonoBehaviour
{

    public static memoryCanvas instance;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        loadAndSpawnMemories();
    }

    private void Awake()
    {
        instance = this;

    }

    public void loadAndSpawnMemories()
    {
        if (memoPrefab == null)
            return;

        List<(DraggableInsideCircle.SavableMemory, Texture2D)> list = DraggableInsideCircle.ItemStorage.LoadAllWithImages();

        foreach((DraggableInsideCircle.SavableMemory, Texture2D) item in list)
        {
            Vector3 spawnPos = new Vector3(item.Item1.x, item.Item1.y, 0f);
            GameObject newMemo = Instantiate(memoPrefab, spawnPos, Quaternion.identity);
            // If the prefab should be under the Canvas (UI element)
            if (canvas != null)
            {
                newMemo.transform.SetParent(canvas.transform, worldPositionStays: true);
            }
            DraggableInsideCircle script = newMemo.GetComponent<DraggableInsideCircle>();

            script.id = item.Item1.id;
            script.info = item.Item1.info;


            //script.level = item.Item1.lvl;

            Debug.Log("Loaded Memory with ID " + script.id + " and text " + script.info);

            Sprite sprite = Sprite.Create(
                item.Item2,
                new Rect(0, 0, item.Item2.width, item.Item2.height),
                new Vector2(0.5f, 0.5f)  // pivot in center
            );

            script.img.sprite = sprite;

            while (script.level > item.Item1.lvl)
                script.sink();
        }

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
