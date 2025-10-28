using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class GradioStreamer : MonoBehaviour
{
    [Header("Server")]
    public string host = "http://192.168.1.88:7860";
    public string[] data = new string[] { "Hello!!", "Hello!!" };

    [Header("Options")]
    public bool autoStart = true;
    public int connectTimeoutMs = 5000;

    // Thread-safe queue for passing data from network thread -> Unity thread
    private readonly ConcurrentQueue<string> recvQueue = new ConcurrentQueue<string>();
    private CancellationTokenSource cts;

    private void Start()
    {
        if (autoStart) StartCall();
    }

    private void OnDestroy()
    {
        StopCall();
    }

    public void StartCall()
    {
        if (cts != null) return;
        cts = new CancellationTokenSource();
        _ = Task.Run(() => RunFlowAsync(cts.Token));
    }

    public void StopCall()
    {
        if (cts == null) return;
        cts.Cancel();
        cts.Dispose();
        cts = null;
    }

    private void Update()
    {
        // consume queue on main thread
        while (recvQueue.TryDequeue(out var chunk))
        {
            OnStreamChunk(chunk);
        }
    }

    private void OnStreamChunk(string chunk)
    {
        // main-thread callback for chunks
        Debug.Log($"[stream chunk] {chunk}");
        // TODO: parse JSON lines or SSE here and call your game logic
    }

    private async Task RunFlowAsync(CancellationToken token)
    {
        try
        {
            var uri = new Uri(host);
            string hostOnly = uri.Host;
            int port = uri.Port;

            // 1) POST /gradio_api/call/begin
            string postPath = "/gradio_api/call/begin";
            string postBody = BuildJsonBody(data);
            string postRequest = BuildHttpRequest("POST", postPath, hostOnly, postBody);

            string postResponse = await SendRawHttpRequest(hostOnly, port, postRequest, token);
            if (string.IsNullOrEmpty(postResponse))
            {
                recvQueue.Enqueue($"POST returned empty response");
                return;
            }

            // parse headers/body
            var (statusCode, headers, body) = ParseHttpResponse(postResponse);
            if (statusCode < 200 || statusCode >= 300)
            {
                recvQueue.Enqueue($"POST returned status {statusCode}. Body: {body}");
                return;
            }

            // Expect JSON body like {"event_id":"..."}
            string eventId = ExtractEventIdFromJson(body);
            if (string.IsNullOrEmpty(eventId))
            {
                recvQueue.Enqueue($"Could not extract event_id from: {body}");
                return;
            }

            recvQueue.Enqueue($"Got event_id: {eventId}");

            // 2) Streaming GET /gradio_api/call/begin/{event_id}
            string getPath = $"/gradio_api/call/begin/{Uri.EscapeDataString(eventId)}";
            await StreamGet(hostOnly, port, getPath, token);
        }
        catch (OperationCanceledException)
        {
            recvQueue.Enqueue("[info] cancelled");
        }
        catch (Exception e)
        {
            recvQueue.Enqueue($"[error] {e}");
        }
    }

    private async Task StreamGet(string host, int port, string path, CancellationToken token)
    {
        using (var client = new TcpClient())
        {
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(connectTimeoutMs, token);
            var finished = await Task.WhenAny(connectTask, timeoutTask);
            if (finished != connectTask)
            {
                throw new TimeoutException("Timed out connecting to streaming endpoint");
            }

            using (var stream = client.GetStream())
            {
                stream.ReadTimeout = Timeout.Infinite; // we'll use cancellation token
                // send GET
                string req = BuildHttpRequest("GET", path, host, null, extraHeaders: "Accept: text/event-stream, text/plain, */*\r\nConnection: keep-alive\r\n");
                byte[] reqBytes = Encoding.UTF8.GetBytes(req);
                await stream.WriteAsync(reqBytes, 0, reqBytes.Length, token);

                // Read loop - stream continuously until server closes or token cancels
                var buffer = new byte[4096];
                while (!token.IsCancellationRequested)
                {
                    int read = 0;
                    // Use Task-based read so cancellation can be observed
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length, token);
                    try
                    {
                        read = await readTask;
                    }
                    catch (OperationCanceledException) { break; }

                    if (read == 0)
                    {
                        // Stream closed by remote
                        recvQueue.Enqueue("[stream closed by server]");
                        break;
                    }

                    string chunk = Encoding.UTF8.GetString(buffer, 0, read);
                    recvQueue.Enqueue(chunk);
                }
            }
        }
    }

    private async Task<string> SendRawHttpRequest(string host, int port, string request, CancellationToken token)
    {
        using (var client = new TcpClient())
        {
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(connectTimeoutMs, token);
            var finished = await Task.WhenAny(connectTask, timeoutTask);
            if (finished != connectTask) throw new TimeoutException("Connection timed out");

            using (var stream = client.GetStream())
            {
                byte[] reqBytes = Encoding.UTF8.GetBytes(request);
                await stream.WriteAsync(reqBytes, 0, reqBytes.Length, token);

                // Read response until socket closes or we have the full body per Content-Length
                var ms = new System.IO.MemoryStream();
                var buffer = new byte[4096];
                int bytesRead = 0;

                // First read headers (until \r\n\r\n)
                while (!token.IsCancellationRequested)
                {
                    int r = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (r == 0) break;
                    ms.Write(buffer, 0, r);
                    bytesRead += r;
                    string soFar = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                    int headersEnd = soFar.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headersEnd >= 0)
                    {
                        // We have headers; parse Content-Length if present and then read the remaining bytes if any
                        var (status, headers, bodyPart) = ParseHttpResponse(soFar);
                        if (headers.TryGetValue("content-length", out string clStr) && int.TryParse(clStr, out int cl))
                        {
                            int already = soFar.Length - (headersEnd + 4);
                            int need = cl - already;
                            while (need > 0)
                            {
                                int r2 = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, need), token);
                                if (r2 == 0) break;
                                ms.Write(buffer, 0, r2);
                                need -= r2;
                            }
                            // return full response string
                            return Encoding.UTF8.GetString(ms.ToArray());
                        }
                        else
                        {
                            // no content-length: return what we have (server likely small)
                            return Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }
                }

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }

    private static (int statusCode, System.Collections.Generic.Dictionary<string, string> headers, string body) ParseHttpResponse(string raw)
    {
        var headers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int headerEnd = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        string head = headerEnd >= 0 ? raw.Substring(0, headerEnd) : raw;
        string body = headerEnd >= 0 ? raw.Substring(headerEnd + 4) : "";

        var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
        int statusCode = 0;
        if (lines.Length > 0)
        {
            var statusParts = lines[0].Split(' ');
            if (statusParts.Length >= 2 && int.TryParse(statusParts[1], out var s)) statusCode = s;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var idx = lines[i].IndexOf(':');
            if (idx > 0)
            {
                var k = lines[i].Substring(0, idx).Trim();
                var v = lines[i].Substring(idx + 1).Trim();
                headers[k.ToLowerInvariant()] = v;
            }
        }

        return (statusCode, headers, body);
    }

    private static string ExtractEventIdFromJson(string json)
    {
        // expecting {"event_id":"..."} - simple parse without pulling JSON libs
        if (string.IsNullOrEmpty(json)) return null;
        int k = json.IndexOf("\"event_id\"", StringComparison.OrdinalIgnoreCase);
        if (k < 0) return null;
        int colon = json.IndexOf(':', k);
        if (colon < 0) return null;
        int quote1 = json.IndexOf('"', colon);
        if (quote1 < 0) return null;
        int quote2 = json.IndexOf('"', quote1 + 1);
        if (quote2 < 0) return null;
        return json.Substring(quote1 + 1, quote2 - quote1 - 1);
    }

    private static string BuildJsonBody(string[] arr)
    {
        var sb = new StringBuilder();
        sb.Append("{\"data\":[");
        for (int i = 0; i < arr.Length; i++)
        {
            sb.Append('\"').Append(EscapeJson(arr[i])).Append('\"');
            if (i < arr.Length - 1) sb.Append(',');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private static string BuildHttpRequest(string method, string path, string host, string body, string extraHeaders = null)
    {
        var sb = new StringBuilder();
        sb.AppendFormat("{0} {1} HTTP/1.1\r\n", method, path);
        sb.AppendFormat("Host: {0}\r\n", host);
        sb.Append("User-Agent: Unity/GradioTcpStreamer\r\n");
        if (!string.IsNullOrEmpty(extraHeaders)) sb.Append(extraHeaders);
        if (string.IsNullOrEmpty(body))
        {
            sb.Append("Connection: close\r\n");
            sb.Append("\r\n");
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            sb.Append("Content-Type: application/json\r\n");
            sb.AppendFormat("Content-Length: {0}\r\n", bytes.Length);
            sb.Append("Connection: close\r\n");
            sb.Append("\r\n");
            sb.Append(body);
        }

        return sb.ToString();
    }
}
