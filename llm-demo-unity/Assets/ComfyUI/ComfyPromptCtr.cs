using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

[System.Serializable]
public class ResponseData
{
    public string prompt_id;
}
public class ComfyPromptCtr : MonoBehaviour
{
    public string addToPositive = ", video game sprite, pixelart, minimalistic";
    public string addToNegative = ", anime";

    public GameObject loading;

    //public InputField pInput,nInput,promptJsonInput;
    public TMPro.TMP_InputField pInput;
    private void Start()
    {
       // QueuePrompt("pretty man","watermark");
    }

    public void QueuePrompt()
    {
        if (loading != null)
            loading.SetActive(true);
        StartCoroutine(QueuePromptCoroutine(pInput.text, "")); //nInput.text));
    }
    private IEnumerator QueuePromptCoroutine(string positivePrompt,string negativePrompt)
    {
        string url = "http://127.0.0.1:8188/prompt";
        string promptText = GeneratePromptJson();
        promptText = promptText.Replace("PPrompt", positivePrompt + addToPositive);
        promptText = promptText.Replace("NPrompt", negativePrompt + addToNegative);
        promptText = promptText.Replace("RandomSeed", ""+ UnityEngine.Random.Range(0, int.MaxValue));
        Debug.Log("new prompt: " + promptText);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(promptText);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(request.error);
        }
        else
        {
            Debug.Log("Prompt queued successfully." + request.downloadHandler.text);

            ResponseData data = JsonUtility.FromJson<ResponseData>(request.downloadHandler.text);
            Debug.Log("Prompt ID: " + data.prompt_id);
            GetComponent<ComfyWebsocket>().promptID = data.prompt_id;
           // GetComponent<ComfyImageCtr>().RequestFileName(data.prompt_id);
        }
    }
    public string promptJson;

private string GeneratePromptJson()
    {
 string guid = Guid.NewGuid().ToString();

    string promptJsonWithGuid = $@"
{{
    ""id"": ""{guid}"",
    ""prompt"": {promptJson}
}}";

    return promptJsonWithGuid;
    }
}
