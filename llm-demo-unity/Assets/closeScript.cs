using UnityEngine;
using UnityEngine.SceneManagement;

public class closeScript : MonoBehaviour
{



    public void onClickClose()
    {


        Scene newScene = SceneManager.GetSceneByName("imgGen");
        if (newScene.isLoaded)
        {
            SceneManager.UnloadSceneAsync(newScene);
        }


        if (UIScript.instance != null && UIScript.instance.cam != null)
            UIScript.instance.cam.gameObject.SetActive(true);
    }

    public void onClickStartGame()
    {
        ComfyImageCtr.started = true;
        SceneManager.LoadScene("pond");
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
