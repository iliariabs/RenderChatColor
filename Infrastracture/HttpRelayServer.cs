using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RenderChatColor.Infrastructure;

public class HttpRelayServer
{
    private readonly HttpListener _listener;
    private readonly ConcurrentQueue<string> _messages = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _clients = new();
    private const int MAX_MESSAGES = 100;

    public HttpRelayServer(string prefix)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _listener.Start();
        Console.WriteLine($"HTTP server started on {string.Join(", ", _listener.Prefixes)}");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequest(context, ct);
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        _listener.Stop();
    }

    private async Task HandleRequest(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var response = context.Response;

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        try
        {
            if (request.Url.AbsolutePath == "/send" && request.HttpMethod == "POST")
            {
                using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
                var json = await reader.ReadToEndAsync();
                
                _messages.Enqueue(json);
                while (_messages.Count > MAX_MESSAGES)
                {
                    _messages.TryDequeue(out _);
                }

                foreach (var client in _clients.Values)
                {
                    client.TrySetResult(true);
                }

                response.StatusCode = 200;
                var buffer = Encoding.UTF8.GetBytes("OK");
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);
            }
            else if (request.Url.AbsolutePath == "/poll" && request.HttpMethod == "GET")
            {
                var lastIdStr = request.QueryString["lastId"];
                int.TryParse(lastIdStr, out var lastId);

                var tcs = new TaskCompletionSource<bool>();
                var clientId = Guid.NewGuid().ToString();
                _clients[clientId] = tcs;

                try
                {
                    await Task.WhenAny(tcs.Task, Task.Delay(25000, ct));

                    var result = _messages.Skip(lastId).Take(50).ToList();

                    var resultObj = new
                    {
                        messages = result.Select(m => JsonSerializer.Deserialize<JsonElement>(m)).ToList(),
                        nextId = lastId + result.Count
                    };

                    var jsonResult = JsonSerializer.Serialize(resultObj);
                    var buffer = Encoding.UTF8.GetBytes(jsonResult);

                    response.ContentType = "application/json";
                    response.StatusCode = 200;
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);
                }
                finally
                {
                    _clients.TryRemove(clientId, out _);
                }
            }
            else
            {
                response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Request error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }
}