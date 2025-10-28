using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class DraggableInsideCircle : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    public Transform circleCenter; // Center of the circle in world space
    public float circleRadius = 3f; // Radius in world units

    private Vector3 offset;

    public Texture2D cursorTexture;

    public GameObject glow; // child sprite for glow

    public int level = 0; // 0 = surface, -1 = sunk a bit, etc, up to -5

    public Image img;

    public string info;
    public string id;

    private void Start()
    {
    }


    [Serializable]
    public class SavableMemory
    {
        public string id;
        public int lvl;
        public float x;
        public float y;
        public string info;
        public string imageFileName;
    }

    public static class ItemStorage
    {
        static string ItemsFolder => Path.Combine(Application.persistentDataPath, "items");

        static void EnsureFolder()
        {
            if (!Directory.Exists(ItemsFolder)) Directory.CreateDirectory(ItemsFolder);
        }

        public static string GetItemJsonPath(string id) => Path.Combine(ItemsFolder, id + ".json");
        public static string GetItemImagePath(string fileName) => Path.Combine(ItemsFolder, fileName);

        // Synchronous save (simple)
        public static void SaveItem(SavableMemory item, Texture2D image)
        {
            EnsureFolder();

            // save image if provided
            if (image != null)
            {
                byte[] imgBytes = image.EncodeToPNG();
                if (string.IsNullOrEmpty(item.imageFileName))
                {
                    item.imageFileName = item.id + ".png";
                }
                File.WriteAllBytes(GetItemImagePath(item.imageFileName), imgBytes);
            }

            // save metadata
            string json = JsonUtility.ToJson(item, true);
            File.WriteAllText(GetItemJsonPath(item.id), json);
        }

        // Load item metadata and image (synchronous)
        public static (SavableMemory, Texture2D) LoadItem(string id)
        {
            string jsonPath = GetItemJsonPath(id);
            if (!File.Exists(jsonPath)) return (null, null);

            string json = File.ReadAllText(jsonPath);
            SavableMemory item = JsonUtility.FromJson<SavableMemory>(json);

            Texture2D tex = null;
            if (!string.IsNullOrEmpty(item.imageFileName))
            {
                string imgPath = GetItemImagePath(item.imageFileName);
                if (File.Exists(imgPath))
                {
                    byte[] bytes = File.ReadAllBytes(imgPath);
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(bytes); // auto-resizes
                }
            }

            return (item, tex);
        }

        // Load all items (metadata only or with images)
        public static List<SavableMemory> LoadAllMetadata()
        {
            EnsureFolder();
            var list = new List<SavableMemory>();
            foreach (var f in Directory.GetFiles(ItemsFolder, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(f);
                    var item = JsonUtility.FromJson<SavableMemory>(json);
                    list.Add(item);
                }
                catch { /* handle parse errors if necessary */ }
            }
            return list;
        }

        public static List<(SavableMemory, Texture2D)> LoadAllWithImages()
        {
            var outList = new List<(SavableMemory, Texture2D)>();
            foreach (var meta in LoadAllMetadata())
            {
                var tup = LoadItem(meta.id);
                outList.Add(tup);
            }
            return outList;
        }

        // Delete item (meta + image)
        public static void DeleteItem(string id)
        {
            var metaPath = GetItemJsonPath(id);
            if (File.Exists(metaPath)) File.Delete(metaPath);

            var imgPng = GetItemImagePath(id + ".png");

            if (File.Exists(imgPng)) File.Delete(imgPng);
        }
    }

    public static List<GameObject> LoadAndSpawn(List<(SavableMemory, Texture2D)> list)
    {
        List<GameObject> result = new List<GameObject>();

        return result;

    }




    public void save()
    {
        SavableMemory item = new SavableMemory { id = this.id, imageFileName = this.id, 
            info = this.info, lvl = this.level, x = this.transform.position.x, y = this.transform.position.y };

        Texture2D smallTex = this.img.sprite.texture;
        ItemStorage.SaveItem(item, smallTex);
    }

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

        this.save();
    }
}
