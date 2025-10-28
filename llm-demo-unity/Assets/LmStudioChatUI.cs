// LmStudioChatUI.cs  (modified to support Enter = submit, Shift+Enter = newline)
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;

public class LmStudioChatUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject messagePrefab;       // prefab (Background -> TMP_Text)
    public RectTransform contentTransform; // Content under ScrollRect
    public ScrollRect scrollRect;
    public TMP_InputField inputField;
    public Button sendButton;
    public Button resetButton;
    public GameObject loadingIcon;         // rotating image
    public TMP_Text inputPlaceholderText;


    [Header("LM Studio")]
    public string baseUrl = "http://127.0.0.1:1234";
    public string model = "google/gemma-3-4b";
    public int maxTokens = 256;
    public float temperature = 1.0f;
    public float topP = 0.95f;
    public int n = 1; // number of variants (usually 1 for chat)

    // Keep a local structured history for building messages array
    private List<(string role, string content)> history = new List<(string role, string content)>();

    // Initial system instruction
    [TextArea(2, 20)]
    public string systemPrompt = "You are a concise assistant that replies in the exact format requested.";

    void Start()
    {
        sendButton.onClick.AddListener(OnSendClicked);
        resetButton.onClick.AddListener(OnResetClicked);

        loadingIcon.SetActive(false);

        ResetConversation();

        // Ensure the input field is multi-line (so Shift+Enter can insert newline)
        // and that Submit is handled by our script via keyboard logic below.
        if (inputField != null)
            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;

        if (ComfyImageCtr.avatarSprite != null)
            playerimg.sprite = ComfyImageCtr.avatarSprite;

        AddMessageToUI("system", "Welcome. Tell me about your day!");
    }

    public Image playerimg;

    public void onPressFinished()
    {
        Scene newScene = SceneManager.GetSceneByName("Dialogue");
        if (newScene.isLoaded)
        {
            SceneManager.UnloadSceneAsync(newScene);
        }

        SceneManager.LoadScene("imgGen", LoadSceneMode.Additive);

        //if (UIScript.instance != null && UIScript.instance.cam != null)
         //   UIScript.instance.cam.gameObject.SetActive(true);

    }

    public static string result;


    void Update()
    {
        // Keyboard submit behaviour:
        // - Enter (Return / KeypadEnter) -> submit (if input not empty)
        // - Shift + Enter -> insert a newline at caret position
        if (inputField != null && inputField.isFocused)
        {
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (enterPressed)
            {
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shiftHeld)
                {
                    // Insert newline at caret/string position
                    int pos = inputField.stringPosition;
                    string t = inputField.text ?? "";
                    t = t.Insert(pos, "\n");
                    inputField.text = t;

                    // Move caret forward to after the newline
                    int newPos = pos + 1;
                    inputField.caretPosition = newPos;
                    inputField.stringPosition = newPos;

                    // Keep focus so user can continue typing
                    inputField.ActivateInputField();
                }
                else
                {
                    // Submit if there's something to send
                    if (!string.IsNullOrWhiteSpace(inputField.text))
                    {
                        OnSendClicked();
                    }
                }
            }
        }
    }

    void OnSendClicked()
    {
        string userText = inputField.text?.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        // Add user text to UI and history immediately
        AddMessageToUI("user", userText);
        history.Add(("user", userText));

        inputField.text = "";
        inputField.ActivateInputField();

        StartCoroutine(SendChatRequestCoroutine());
    }

    void OnResetClicked()
    {
        ResetConversation();
    }

    void ResetConversation()
    {
        // Clear UI
        foreach (Transform t in contentTransform) Destroy(t.gameObject);

        // Reset history to initial system message only
        history.Clear();
        history.Add(("system", systemPrompt));

        // Optionally show a small system bubble in the chat UI (or not)
        //AddMessageToUI("system", "(system prompt active)");

        // Scroll to bottom
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }


    IEnumerator SendChatRequestCoroutine()
    {
        // Show loading
        //loadingIcon.SetActive(true);
        SetLoading(true);

        string url = $"{baseUrl}/v1/chat/completions";

        // Build messages array from history
        var messages = new JArray();
        foreach (var (role, content) in history)
        {
            messages.Add(new JObject { ["role"] = role, ["content"] = content });
        }

        // Build payload
        var payload = new JObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["top_p"] = topP,
            ["n"] = n,
            ["max_tokens"] = maxTokens,
            ["max_output_tokens"] = maxTokens
        };

        string json = payload.ToString();

        var uwr = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
        uwr.downloadHandler = new DownloadHandlerBuffer();
        uwr.SetRequestHeader("Content-Type", "application/json");

        yield return uwr.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
#else
        if (uwr.isNetworkError || uwr.isHttpError)
#endif
        {
            Debug.LogError($"Request error: {uwr.error}\n{uwr.downloadHandler.text}");
            AddMessageToUI("assistant", $"<Request error: {uwr.error}>");
            loadingIcon.SetActive(false);
            yield break;
        }

        string responseJson = uwr.downloadHandler.text;
        Debug.Log("Chat response: " + responseJson);

        // Parse assistant text from response (robustly)
        string assistantText = ParseChatResponse(responseJson);

        // Add assistant message to history and UI
        history.Add(("assistant", assistantText));
        AddMessageToUI("assistant", assistantText);

        // hide loading
        //loadingIcon.SetActive(false);
        SetLoading(false);
    }

    // Put these inside your LmStudioChatUI class (replace prior methods)

    private void SetLoading(bool isLoading)
    {
        if (loadingIcon != null) loadingIcon.SetActive(isLoading);

        if (sendButton != null) sendButton.interactable = !isLoading;
        if (resetButton != null) resetButton.interactable = !isLoading;

        if (inputField != null)
        {
            // Make the field read-only so keyboard won't edit it
            inputField.readOnly = isLoading;
            inputField.interactable = !isLoading;

            // hide caret while loading
            //inputField.caretBlinkRate = isLoading ? 0f : 1f;

            if (isLoading)
            {
                // remove focus so keystrokes are not routed into it
                //EventSystem.current?.SetSelectedGameObject(null);
                inputField.DeactivateInputField();
            }
            else
            {
                // restore focus
                inputField.ActivateInputField();
            }
        }

        // hide placeholder
        if (inputPlaceholderText != null)
            inputPlaceholderText.gameObject.SetActive(!isLoading);
    }

    void AddMessageToUI(string role, string text)
    {
        if (messagePrefab == null || contentTransform == null)
        {
            Debug.LogWarning("Message prefab or contentTransform not assigned.");
            return;
        }

        var inst = Instantiate(messagePrefab, contentTransform);
        inst.transform.localScale = Vector3.one;
        inst.transform.SetAsLastSibling(); // ensure newest message is last

        // Find Background Rect and TMP text (adjust name if different)
        var bgTransform = inst.transform.Find("Image") as RectTransform;
        var tmp = inst.GetComponentInChildren<TMP_Text>();

        if (tmp == null)
        {
            Debug.LogWarning("TMP_Text not found in message prefab.");
            return;
        }

        // Ensure predictable sizing
        tmp.enableAutoSizing = false;
        //tmp.enableWordWrapping = true;
        tmp.text = text;

        // Compute widths/heights
        float parentWidth = 600f;
        var scrollRectRT = scrollRect?.GetComponent<RectTransform>();
        if (scrollRectRT != null) parentWidth = scrollRectRT.rect.width;
        float maxBubbleWidth = parentWidth * 0.75f;

        float padX = 100f; // total horizontal padding
        float padY = 100f;
        float minBubbleWidth = Mathf.Min(parentWidth * 0.25f, 220f);
        float minBubbleHeight = 36f;

        tmp.ForceMeshUpdate();
        Vector2 pref = tmp.GetPreferredValues(text, Mathf.Max(20f, maxBubbleWidth - padX), 0f);
        pref.x = Mathf.Max(pref.x, 0f);
        pref.y = Mathf.Max(pref.y, 0f);

        float bgWidth = Mathf.Ceil(pref.x + padX);
        float bgHeight = Mathf.Ceil(pref.y + padY);
        bgWidth = Mathf.Max(bgWidth, minBubbleWidth);
        bgHeight = Mathf.Max(bgHeight, minBubbleHeight);

        // Resize background if found
        if (bgTransform != null)
        {
            // Reset anchors/pivot to center first (defensive)
            bgTransform.anchorMin = new Vector2(0.5f, 1f);
            bgTransform.anchorMax = new Vector2(0.5f, 1f);
            bgTransform.pivot = new Vector2(0.5f, 1f);

            bgTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bgWidth);
            bgTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bgHeight);
        }

        // Update LayoutElement on the root so VerticalLayoutGroup respects sizes
        var layout = inst.GetComponent<LayoutElement>();
        if (layout == null) layout = inst.AddComponent<LayoutElement>();
        layout.preferredWidth = bgWidth;
        layout.preferredHeight = bgHeight;
        layout.flexibleWidth = 0;
        layout.flexibleHeight = 0;

        //inst.GetComponent<RectTransform>().rect.Set = bgHeight;

        // Alignment and mirroring:
        // - assistant: left aligned (bubble on left)
        // - user: right aligned (bubble on right + mirrored sprite)
        if (role == "user")
        {
            // place bg anchored to right
            if (bgTransform != null)
            {
                bgTransform.anchorMin = new Vector2(1f, 1f);
                bgTransform.anchorMax = new Vector2(1f, 1f);
                bgTransform.pivot = new Vector2(1f, 1f);
                bgTransform.anchoredPosition = new Vector2(-20f, -10f); // small right margin
            }

            tmp.alignment = TextAlignmentOptions.MidlineRight;
        }
        else
        {
            result = text;

            // Mirror bubble horizontally
            if (bgTransform != null) bgTransform.localScale = new Vector3(-1f, 1f, 1f);
            // Keep text readable by flipping text back
            tmp.rectTransform.localScale = new Vector3(-1f, 1f, 1f);


            // assistant/system: left side
            if (bgTransform != null)
            {
                bgTransform.anchorMin = new Vector2(0f, 1f);
                bgTransform.anchorMax = new Vector2(0f, 1f);
                bgTransform.pivot = new Vector2(1f, 1f);
                bgTransform.anchoredPosition = new Vector2(20f, 10f); // small left margin
            }


            // Normal scale
            //if (bgTransform != null) bgTransform.localScale = new Vector3(1f, 1f, 1f);
            //tmp.rectTransform.localScale = Vector3.one;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        // Tint background color (optional; keep what you had)
        var bgImg = bgTransform != null ? bgTransform.GetComponent<Image>() : inst.GetComponentInChildren<Image>();
        if (bgImg != null)
        {
            if (role == "user") bgImg.color = new Color(0.9f, 0.9f, 0.75f, 1f);
            else bgImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        }

        // Force immediate rebuild and then finalize layout on next frame to avoid overlap/truncation
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)inst.transform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform);


        contentTransform.GetComponent<VerticalLayoutGroup>().CalculateLayoutInputVertical();
        contentTransform.GetComponent<ContentSizeFitter>().SetLayoutVertical();

        scrollRect.content.GetComponent<VerticalLayoutGroup>().CalculateLayoutInputVertical();
        scrollRect.content.GetComponent<ContentSizeFitter>().SetLayoutVertical();

        scrollRect.verticalNormalizedPosition = 0;

        // Start coroutine to finalize (wait one frame then rebuild & scroll)
        //StartCoroutine(FinishLayoutCoroutine());
        //EnsureContentHeightMatchesChildren();
    }

    



    private void EnsureContentHeightMatchesChildren()
    {
        if (contentTransform == null) return;

        var vLayout = contentTransform.GetComponent<VerticalLayoutGroup>();
        float spacing = vLayout != null ? vLayout.spacing : 0f;
        RectOffset padding = vLayout != null ? vLayout.padding : new RectOffset();

        float total = padding.top + padding.bottom;

        // Sum preferred heights of children (use layout components if present)
        for (int i = 0; i < contentTransform.childCount; i++)
        {
            var child = contentTransform.GetChild(i) as RectTransform;
            if (child == null) continue;

            // If child has LayoutElement preferredHeight, use that, otherwise use rect height
            var le = child.GetComponent<LayoutElement>();
            float h = (le != null && le.preferredHeight > 0) ? le.preferredHeight : child.rect.height;
            total += h;
            if (i < contentTransform.childCount - 1) total += spacing;
        }

        // Set content height explicitly
        contentTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(total));


        Debug.Log("contentTransform.rect.height: " + contentTransform.rect.height + ", scrollRect.viewport.rect.height: " + scrollRect.viewport.rect.height);
    }

    private string ParseChatResponse(string responseJson)
    {
        try
        {
            var jo = JObject.Parse(responseJson);

            // OpenAI-style: choices[].message.content (string)
            if (jo["choices"] is JArray choices && choices.Count > 0)
            {
                var first = choices[0];
                var msg = first["message"];
                if (msg != null)
                {
                    var content = msg["content"];
                    if (content != null)
                    {
                        if (content.Type == JTokenType.String)
                            return content.ToString().Trim();

                        if (content.Type == JTokenType.Object)
                        {
                            var cobj = content as JObject;
                            if (cobj["parts"] is JArray parts && parts.Count > 0)
                            {
                                var sbParts = new StringBuilder();
                                foreach (var p in parts) sbParts.AppendLine(p.ToString());
                                return sbParts.ToString().Trim();
                            }
                            if (cobj["text"] != null) return cobj["text"].ToString().Trim();
                            return cobj.ToString().Trim();
                        }
                    }
                }

                // fallback: choices[].text
                if (first["text"] != null) return first["text"].ToString().Trim();
            }

            // LM Studio older shapes
            if (jo["output_text"] != null) return jo["output_text"].ToString().Trim();

            if (jo["output"] is JArray outArr && outArr.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var item in outArr)
                {
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
                        sb.AppendLine(item.ToString());
                }
                var s = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(s)) return s;
            }

            return responseJson;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Parse error: " + ex);
            return responseJson;
        }
    }
}
