using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TabletExtend.Core;

// Native receiver acknowledges only after it renders a real captured frame.
// A socket connection or an unrelated heartbeat cannot enable mouse crossing.
public sealed class FrameServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly string token;
    private readonly MousePolicy policy;
    private readonly Func<byte[]> capture;
    private readonly CancellationTokenSource stop = new();
    private readonly HashSet<Task> clients = [];
    private readonly SemaphoreSlim slots = new(4);
    private readonly int interval;
    private Task? acceptLoop;
    private int active;
    public event Action<string>? Diagnostic;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public FrameServer(IPAddress address, int port, string token, MousePolicy policy, Func<byte[]> capture, int fps = 10)
    {
        if (fps is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(fps));
        this.listener = new(address, port); this.token = token; this.policy = policy;
        this.capture = capture; interval = 1000 / fps;
    }

    public void Start() { listener.Start(4); acceptLoop = AcceptLoop(); }

    private async Task AcceptLoop()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(stop.Token);
                if (!slots.Wait(0)) { client.Dispose(); continue; }
                // Session works on the pool: capture/encoding must never block the tray.
                Task task = Task.Run(async () => { try { await Serve(client); } finally { slots.Release(); } });
                lock (clients) clients.Add(task);
                _ = task.ContinueWith(t => { lock (clients) clients.Remove(t); }, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (stop.IsCancellationRequested) { }
    }

    private async Task Serve(TcpClient client)
    {
        bool owner = false;
        using (client)
        using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
        {
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    handshake.CancelAfter(TimeSpan.FromSeconds(3));
                    var packet = await Wire.Read(stream, 1024, handshake.Token);
                    var hello = packet.Type == Wire.HelloType ? JsonSerializer.Deserialize<Hello>(packet.Body, Wire.Json) : null;
                    if (hello is null || hello.Version != Wire.Version || !ValidToken(hello.Token))
                    {
                        await Wire.Write(stream, Wire.ErrorType, Wire.Encode(new { message = "配对码或协议版本不正确" }), handshake.Token);
                        return;
                    }
                    owner = Interlocked.CompareExchange(ref active, 1, 0) == 0;
                    if (!owner)
                    {
                        await Wire.Write(stream, Wire.ErrorType, Wire.Encode(new { message = "已有平板连接" }), handshake.Token);
                        return;
                    }
                }
                policy.Disconnect();
                long sequence = 0;
                while (!lifetime.IsCancellationRequested)
                {
                    // Budget covers capture, send and acknowledgement. Policy.Tick
                    // separately expires the lease if capture or socket stalls.
                    using var budget = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    budget.CancelAfter(policy.Lease);
                    byte[] jpeg = capture();
                    budget.Token.ThrowIfCancellationRequested();
                    if (jpeg.Length == 0) throw new InvalidDataException("Empty frame");
                    await Wire.Write(stream, Wire.StatusType, Wire.Encode(policy.Status), budget.Token);
                    await Wire.Write(stream, Wire.FrameType, Wire.FrameBody(new(++sequence, jpeg)), budget.Token);
                    var ack = await Wire.Read(stream, 8, budget.Token);
                    if (!Wire.IsAck(ack, sequence)) throw new InvalidDataException("Invalid frame acknowledgement");
                    budget.Token.ThrowIfCancellationRequested();
                    policy.AcknowledgeFrame();
                    await Task.Delay(interval, lifetime.Token);
                }
            }
            catch (Exception ex)
            {
                Diagnostic?.Invoke(ex is OperationCanceledException ? "连接已结束或画面确认超时" : ex.Message);
            }
            finally
            {
                if (owner) { policy.Disconnect(); Interlocked.Exchange(ref active, 0); }
            }
        }
    }

    private bool ValidToken(string? input) => input is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(input), Encoding.UTF8.GetBytes(token));

    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        if (acceptLoop is not null) await acceptLoop;
        Task[] pending; lock (clients) pending = clients.ToArray();
        await Task.WhenAll(pending);
        policy.Disconnect(); stop.Dispose(); slots.Dispose();
    }
}
