using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.UI;
using Newtonsoft.Json.Linq; // make sure Newtonsoft package is installed

public class LLMScript : MonoBehaviour
{
    [Header("UI")]
    public Button sendButton;
    public TMP_Text outputText; 

    [Header("LM Studio")]
    public string baseUrl = "http://127.0.0.1:1234";
    public string model = "google/gemma-3-4b";

    [Header("Generation params")]
    public int maxTokens = 256;
    public float temperature = 0.9f;

    // Example one-shot messages + actual user request
    // We will place them inside the messages array below.
    private JArray BuildMessages()
    {
        var messages = new JArray();

        // System instruction (optional but useful)
        messages.Add(new JObject
        {
            ["role"] = "system",
            ["content"] = "You are creative. Produce distinct, varied ideas — avoid repeating themes and wording from the example."
        });

        // ONE-SHOT EXAMPLE (emulate a short conversation)
        messages.Add(new JObject
        {
            ["role"] = "user",
            ["content"] = "Give me a list of 2 game ideas in this format: \"[game name](one-sentence description)\""
        });

        messages.Add(new JObject
        {
            ["role"] = "assistant",
            ["content"] =
                @"[Sunflower Bakery](A time-management game where you run a bakery with talking sunflowers.)
                [Cloud Garden](A gentle puzzle game where you connect floating islands for migrating birds.)"
        });

        // NOW the real user task we want the model to follow (one-shot style)
        messages.Add(new JObject
        {
            ["role"] = "user",
            ["content"] = "Give me a list of 3 game ideas related to living healthily in this format: \"[game name](one-sentence description)\""
        });

        return messages;
    }

    void Start()
    {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendClicked);

        if (outputText != null)
            outputText.text = "Ready. Press the button to send the one-shot prompt.";
    }

    void OnSendClicked()
    {
        StartCoroutine(SendChatCompletionCoroutine());
    }

    IEnumerator SendChatCompletionCoroutine()
    {
        string url = $"{baseUrl}/v1/chat/completions";

        // Build payload
        var payload = new JObject();
        payload["model"] = model;

        // messages array: system + example assistant + user task
        payload["messages"] = BuildMessages();

        // Add generation params — servers differ in naming; include both common fields
        payload["temperature"] = temperature;
        // Common OpenAI field:
        payload["max_tokens"] = maxTokens;
        // Some local servers (LM Studio) use "max_output_tokens" — harmless to include
        payload["max_output_tokens"] = maxTokens;

        // Optionally request n completions (defaults to 1)
        payload["n"] = 1;

        payload["sampler"] = "multinomial"; // request sampling rather than greedy (if server supports it)
        payload["seed"] = -1;               // -1 or omit to allow different randomness each request


        string json = payload.ToString();

        var uwr = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
        uwr.downloadHandler = new DownloadHandlerBuffer();
        uwr.SetRequestHeader("Content-Type", "application/json");
        // If LM Studio requires auth, set it here:
        // uwr.SetRequestHeader("Authorization", "Bearer YOUR_TOKEN_HERE");

        if (outputText != null) outputText.text += "\n\nSending chat/completions request...";

        yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
#else
        if (uwr.isNetworkError || uwr.isHttpError)
#endif
        {
            Debug.LogError($"Request error: {uwr.error}\n{uwr.downloadHandler.text}");
            if (outputText != null) outputText.text += $"\n\nRequest error: {uwr.error}\n{uwr.downloadHandler.text}";
            yield break;
        }

        string responseJson = uwr.downloadHandler.text;
        Debug.Log("Chat completions response: " + responseJson);

        string extracted = ParseChatResponse(responseJson);

        if (outputText != null)
            outputText.text = $"{extracted}";
    }

    // Robust parsing for several common response shapes:
    // - OpenAI-style: choices[].message.content (string)
    // - LM Studio variants: choices[].message.content.parts (array), output_text, output -> message content etc.
    private string ParseChatResponse(string responseJson)
    {
        try
        {
            var jo = JObject.Parse(responseJson);

            // 1) OpenAI-style: choices[].message.content (string) or message.content as object
            if (jo["choices"] is JArray choices && choices.Count > 0)
            {
                var first = choices[0];

                // message.content might be a string
                var messageObj = first["message"];
                if (messageObj != null)
                {
                    var contentToken = messageObj["content"];
                    if (contentToken != null)
                    {
                        // If content is string
                        if (contentToken.Type == JTokenType.String)
                        {
                            return contentToken.ToString().Trim();
                        }

                        // If content is object with "text" or "parts" or nested structures
                        if (contentToken.Type == JTokenType.Object)
                        {
                            var contentObj = contentToken as JObject;

                            // Try .parts (array of strings)
                            if (contentObj["parts"] is JArray parts && parts.Count > 0)
                            {
                                var sbParts = new StringBuilder();
                                foreach (var p in parts) sbParts.AppendLine(p.ToString());
                                return sbParts.ToString().Trim();
                            }

                            // Try "text" field
                            if (contentObj["text"] != null) return contentObj["text"].ToString().Trim();

                            // Try nested content -> message -> content -> as text
                            // Fallback: return serialized content object
                            return contentObj.ToString().Trim();
                        }
                    }
                }

                // If no message field, some completions return choices[].text
                if (first["text"] != null)
                {
                    return first["text"].ToString().Trim();
                }
            }

            // 2) LM Studio older shapes: output_text
            if (jo["output_text"] != null) return jo["output_text"].ToString().Trim();

            // 3) Some LM Studio versions return output -> list of message objects
            if (jo["output"] is JArray outArr && outArr.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var item in outArr)
                {
                    // If item is message-like
                    if (item["type"]?.ToString() == "message" && item["content"] != null)
                    {
                        var content = item["content"];
                        if (content["parts"] is JArray parts2)
                        {
                            foreach (var p in parts2) sb.AppendLine(p.ToString());
                        }
                        else if (content["text"] != null) sb.AppendLine(content["text"].ToString());
                        else sb.AppendLine(content.ToString());
                    }
                    else
                    {
                        sb.AppendLine(item.ToString());
                    }
                }
                var s = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(s)) return s;
            }

            // 4) Nothing matched — return raw JSON so you can inspect
            return responseJson;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Failed to parse chat response: " + ex);
            return responseJson;
        }
    }
}