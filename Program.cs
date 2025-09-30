using System.Threading;
using System.Threading.Tasks;
using System;
using RenderChatColor.Infrastructure;

namespace RenderChatColor.ServerApp;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        var prefix = $"http://+:{port}/";
        
        var server = new HttpRelayServer(prefix);
        using var cts = new CancellationTokenSource();
        
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await server.RunAsync(cts.Token);
    }
}