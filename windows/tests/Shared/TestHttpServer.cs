using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IptvPlayer.Testing;

/// <summary>Servidor HTTP mínimo (TcpListener) para servir listas en las pruebas sin permisos especiales.</summary>
public sealed class TestHttpServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<string, (int Status, string Body)> _handler;

    public ConcurrentQueue<(string Path, Dictionary<string, string> Headers)> Requests { get; } = new();

    public TestHttpServer(Func<string, (int Status, string Body)> handler)
    {
        _handler = handler;
        _listener.Start();
        _ = AcceptLoop();
    }

    public string BaseUrl => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cts.Token); }
            catch { return; }
            _ = Task.Run(() => Handle(client));
        }
    }

    private async Task Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync() ?? "";
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    var colon = line.IndexOf(':');
                    if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }
                var parts = requestLine.Split(' ');
                var path = parts.Length > 1 ? parts[1] : "/";
                Requests.Enqueue((path, headers));
                var (status, body) = _handler(path);
                var bytes = Encoding.UTF8.GetBytes(body);
                var head = $"HTTP/1.1 {status} X\r\nContent-Type: audio/x-mpegurl; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
                await stream.WriteAsync(bytes);
                await stream.FlushAsync();
            }
            catch
            {
                // Conexión cortada por el cliente: no importa en las pruebas.
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
    }
}
