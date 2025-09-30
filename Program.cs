namespace RenderChatColor.ServerApp;
using System.Threading;
using System.Threading.Tasks;
using System;
using RenderChatColor.Infrastructure;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 15000;
        var server = new TcpRelayServer(port);
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
        Console.WriteLine($"TCP relay server listening on port {port}");
        await server.RunAsync(cts.Token);
    }
}