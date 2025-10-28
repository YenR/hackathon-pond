using UnityEngine;
using UnityEngine.UI;

using UnityEngine.SceneManagement;

public class UIScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public Camera cam;

    public Transform pond;

    public Button up, down, delete, create, read;

    public static UIScript instance;

    public DraggableInsideCircle selected = null;

    //public Sprite newSprite = null; 

    public void onPressNewMemory()
    {
        SceneManager.LoadScene("Dialogue", LoadSceneMode.Additive);

        if (memoryCanvas.instance != null)
            memoryCanvas.instance.spawnNewMemory();

        cam.gameObject.SetActive(false); 
    }

    public void setNewSprite(Sprite s)
    {
        if (selected == null)
            return;

        selected.gameObject.GetComponent<Image>().sprite = s;
        selected.glow.SetActive(false);

        selected.info = LmStudioChatUI.result;
    }

    public void onPressUp()
    {
        if (selected == null)
            return;

        selected.up();
    }

    public void onPressDelete()
    {
        if (selected != null)
            Destroy(selected.gameObject);
    }

    public GameObject infopane;
    public TMPro.TMP_Text info;

    public void onPressRead()
    {
        if (selected == null)
            return;

        infopane.SetActive(true);

        info.SetText(selected.info);
    }

    public void onPressDown()
    {
        if (selected == null)
            return;

        selected.sink();
    }

    void Start()
    {
        instance = this;
        
    }

    public void showUI()
    {
        up.gameObject.SetActive(true);
        down.gameObject.SetActive(true);
        read.gameObject.SetActive(true);
        delete.gameObject.SetActive(true);
    }

    public void hideUI()
    {

        up.gameObject.SetActive(false);
        down.gameObject.SetActive(false);
        read.gameObject.SetActive(false);
        delete.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
