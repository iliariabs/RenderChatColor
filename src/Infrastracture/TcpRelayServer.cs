
namespace TcpChat.Infrastructure;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

public class TcpRelayServer
{
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
    private readonly int _port;

    public TcpRelayServer(int port)
    {
        _port = port;
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _listener.Start();
        while (!ct.IsCancellationRequested)
        {
            var acceptTask = _listener.AcceptTcpClientAsync();
            var completed = await Task.WhenAny(acceptTask, Task.Delay(-1, ct));
            if (completed != acceptTask) break;
            var client = acceptTask.Result;
            var key = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
            _clients[key] = client;
            _ = HandleClient(client, key, ct);
        }
        _listener.Stop();
    }

    private async Task HandleClient(TcpClient client, string key, CancellationToken ct)
    {
        var ns = client.GetStream();
        var buffer = new byte[8192];
        try
        {
            while (!ct.IsCancellationRequested && client.Connected)
            {
                var len = await ns.ReadAsync(buffer, 0, buffer.Length, ct);
                if (len == 0) break;
                var bytes = new byte[len];
                Array.Copy(buffer, bytes, len);
                foreach (var kv in _clients)
                {
                    try
                    {
                        var other = kv.Value;
                        if (!other.Connected) continue;
                        var os = other.GetStream();
                        await os.WriteAsync(bytes, 0, bytes.Length, ct);
                    }
                    catch { }
                }
            }
        }
        catch { }
        finally
        {
            _clients.TryRemove(key, out _);
            try { client.Close(); } catch { }
        }
    }
}