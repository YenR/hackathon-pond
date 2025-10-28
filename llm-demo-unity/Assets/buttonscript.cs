using UnityEngine;

public class buttonscript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public GameObject panel;

    public void onPressClose()
    {
        panel.SetActive(false);

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
