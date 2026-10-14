using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using TabletExtend.Core;

if (args.Length == 3 && args[0] == "--interop")
{
    var smokePolicy = new MousePolicy(new FakeCursor());
    byte[] fixture = File.ReadAllBytes(args[1]);
    await using var smoke = new FrameServer(IPAddress.Loopback, 0, new string('A', 64), smokePolicy, () => fixture);
    smoke.Start(); File.WriteAllText(args[2], smoke.Port.ToString());
    await Task.Delay(5000);
    return;
}

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS " + name); passed++;
}

var cursor = new FakeCursor(); var clock = new ManualClock(); var policy = new MousePolicy(cursor, clock);
policy.Disconnect();
Check(!policy.Status.Allowed && !cursor.Clipped && cursor.Moves == 1, "startup off and releases stale clipping");
policy.ToggleCrossing(); Check(!policy.Status.Allowed, "disconnected cannot enable crossing");
policy.AcknowledgeFrame(); Check(policy.Status.Connected && !policy.Status.Allowed && cursor.Clipped, "first rendered frame off and contained");
policy.ToggleCrossing(); Check(policy.Status.Allowed && !cursor.Clipped, "crossing on releases rectangle");
int moves = cursor.Moves; policy.ToggleCrossing();
Check(!policy.Status.Allowed && cursor.Clipped && cursor.Moves == moves + 1, "crossing off returns to main immediately");
policy.ToggleCrossing(); policy.ToggleForcedLock();
Check(policy.Status.Locked && !policy.Status.Allowed && cursor.Clipped, "forced lock overrides crossing");
policy.ToggleCrossing(); Check(!policy.Status.Allowed, "normal toggle cannot clear forced lock");
policy.ToggleForcedLock(); Check(!policy.Status.Locked && !policy.Status.Allowed && cursor.Clipped, "unlock keeps crossing off");
policy.ToggleForcedLock(); policy.Disconnect();
Check(policy.Status.Locked && !policy.Status.Connected && !cursor.Clipped, "disconnect releases even forced lock but remembers lock state");
policy.AcknowledgeFrame(); Check(policy.Status.Locked && !policy.Status.Allowed && cursor.Clipped, "reconnect remains off/locked");
clock.Advance(1499); policy.Tick(); Check(policy.Status.Connected, "lease before deadline stays connected");
clock.Advance(1); policy.Tick(); Check(!policy.Status.Connected && !cursor.Clipped, "network blackhole expires at 1500ms");
policy.AcknowledgeFrame(); clock.Advance(1000); policy.AcknowledgeFrame(); clock.Advance(1000); policy.Tick();
Check(policy.Status.Connected, "rendered frame renews lease");
cursor.FailClip = true; policy.Tick();
Check(policy.Status.Error is not null && !policy.Status.Allowed && !cursor.Clipped, "ClipCursor failure reported and fails open");
cursor.FailClip = false; policy.Tick(); Check(policy.Status.Error is null && cursor.Clipped, "cursor fault can recover safely");

using (var malformed = new MemoryStream(new byte[] { Wire.FrameType, 0x7f, 0xff, 0xff, 0xff }))
{
    try { await Wire.Read(malformed, 8, default); Check(false, "oversize must reject"); }
    catch (InvalidDataException) { Check(true, "oversized packet rejected before allocation"); }
}
using (var packet = new MemoryStream())
{
    await Wire.Write(packet, Wire.AckType, Wire.AckBody(0x0102030405060708), default);
    packet.Position = 0;
    Check(Wire.IsAck(await Wire.Read(packet, 8, default), 0x0102030405060708), "big endian wire roundtrip");
}

var netCursor = new FakeCursor(); var netPolicy = new MousePolicy(netCursor) { Lease = TimeSpan.FromMilliseconds(400) };
string token = new('A', 64);
int captures = 0;
await using var server = new FrameServer(IPAddress.Loopback, 0, token, netPolicy, () => { Interlocked.Increment(ref captures); return [0xff, 0xd8, 0xff, 0xd9]; }, 30);
server.Start();
async Task<TcpClient> Connect(string key)
{
    var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, server.Port);
    await Wire.Write(client.GetStream(), Wire.HelloType, Wire.Encode(new Hello(1, key)), default); return client;
}
async Task<long> ReadFrame(TcpClient client)
{
    using var timeout = new CancellationTokenSource(3000);
    var stream = client.GetStream();
    var state = await Wire.Read(stream, 4096, timeout.Token);
    Check(state.Type == Wire.StatusType, "status precedes captured frame");
    var frame = await Wire.Read(stream, Wire.MaxJpeg + 8, timeout.Token);
    Check(frame.Type == Wire.FrameType && frame.Body.Length > 8, "frame has sequence and image bytes");
    return BinaryPrimitives.ReadInt64BigEndian(frame.Body);
}
async Task WaitFor(Func<bool> condition)
{
    using var deadline = new CancellationTokenSource(3000);
    while (!condition()) await Task.Delay(10, deadline.Token);
}
using (var bad = await Connect(new string('B', 64)))
{
    var response = await Wire.Read(bad.GetStream(), 4096, default);
    Check(response.Type == Wire.ErrorType && !netPolicy.Status.Connected && captures == 0, "wrong token cannot capture or alter mouse");
}
using (var client = await Connect(token))
{
    long seq = await ReadFrame(client);
    Check(!netPolicy.Status.Connected, "TCP alone does not activate cursor containment");
    await Wire.Write(client.GetStream(), Wire.AckType, Wire.AckBody(seq), default);
    await WaitFor(() => netPolicy.Status.Connected);
    Check(netCursor.Clipped && !netPolicy.Status.Allowed, "valid rendered-frame ack activates off policy");
    netPolicy.ToggleCrossing();
    using (var other = await Connect(token))
    {
        var response = await Wire.Read(other.GetStream(), 4096, default);
        Check(response.Type == Wire.ErrorType && netPolicy.Status.Allowed, "second client rejected without resetting first session");
    }
    seq = await ReadFrame(client);
    await Wire.Write(client.GetStream(), Wire.AckType, Wire.AckBody(seq + 99), default);
    await WaitFor(() => !netPolicy.Status.Connected);
    Check(!netCursor.Clipped && !netPolicy.Status.Allowed, "wrong-sequence ack disconnects and releases");
}
await Task.Delay(50);
using (var silent = await Connect(token))
{
    long seq = await ReadFrame(silent);
    await Wire.Write(silent.GetStream(), Wire.AckType, Wire.AckBody(seq), default);
    await WaitFor(() => netPolicy.Status.Connected);
    Check(!netPolicy.Status.Allowed, "new session never inherits crossing-on");
    await ReadFrame(silent); // deliberately stop acknowledging frames
    await WaitFor(() => !netPolicy.Status.Connected);
    Check(!netCursor.Clipped, "silent renderer disconnects and releases");
}
await Task.Delay(50);
var eof = await Connect(token);
long last = await ReadFrame(eof);
await Wire.Write(eof.GetStream(), Wire.AckType, Wire.AckBody(last), default);
await WaitFor(() => netPolicy.Status.Connected); eof.Dispose();
await WaitFor(() => !netPolicy.Status.Connected);
Check(!netCursor.Clipped, "TCP close releases without waiting full lease");

// Thread stress is useful here: keyboard/tray/renderer/timer are independent.
var stress = new MousePolicy(new FakeCursor());
await Task.WhenAll(Enumerable.Range(0, 4).Select(n => Task.Run(() =>
{
    for (int i = 0; i < 2000; i++)
        switch (n) { case 0: stress.ToggleCrossing(); break; case 1: stress.ToggleForcedLock(); break; case 2: stress.AcknowledgeFrame(); break; default: stress.Disconnect(); break; }
})));
Check(!stress.Status.Locked || !stress.Status.Allowed, "concurrent transitions preserve forced-lock invariant");
Console.WriteLine($"{passed} checks passed. Fake cursor and synthetic frame; no Windows/device claim.");

sealed class FakeCursor : ICursorBoundary
{
    public bool Clipped, FailClip; public int Moves;
    public void Release() => Clipped = false;
    public void MoveToMain() => Moves++;
    public void RestrictToMain() { if (FailClip) throw new IOException("simulated API failure"); Clipped = true; }
}
sealed class ManualClock : TimeProvider
{
    private long value;
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => value;
    public void Advance(long milliseconds) => value += milliseconds;
}
