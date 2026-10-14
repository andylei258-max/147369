namespace TabletExtend.Core;

public interface ICursorBoundary
{
    void Release();
    void MoveToMain();
    void RestrictToMain();
}

public sealed record MouseStatus(bool Connected, bool Allowed, bool Locked, string? Error);

// Serializes keyboard, tray, network and timer transitions. A lock survives a
// disconnect; only an explicit lock toggle unlocks it. A fresh session is off.
public sealed class MousePolicy(ICursorBoundary cursor, TimeProvider? clock = null)
{
    private readonly object gate = new();
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private bool connected, allowed, locked;
    private long lastAck;
    private string? error;
    public TimeSpan Lease { get; init; } = TimeSpan.FromMilliseconds(1500);
    public MouseStatus Status { get { lock (gate) return new(connected, allowed, locked, error); } }

    public void AcknowledgeFrame()
    {
        lock (gate)
        {
            lastAck = time.GetTimestamp();
            if (connected) return;
            connected = true;
            allowed = false;
            Apply(moveHome: true);
        }
    }

    public void ToggleCrossing()
    {
        lock (gate)
        {
            if (locked || !connected || error is not null) return;
            allowed = !allowed;
            Apply(moveHome: !allowed);
        }
    }

    public void ToggleForcedLock()
    {
        lock (gate)
        {
            locked = !locked;
            allowed = false;
            Apply(moveHome: true);
        }
    }

    public void Disconnect()
    {
        lock (gate)
        {
            connected = allowed = false;
            Apply(moveHome: true);
        }
    }

    public void Tick()
    {
        lock (gate)
        {
            if (connected && time.GetElapsedTime(lastAck) >= Lease)
            {
                connected = allowed = false;
                Apply(moveHome: true);
            }
            else if (connected && !allowed)
            {
                // Other applications can change ClipCursor; enforce while our
                // healthy display session requests containment.
                Apply(moveHome: false);
            }
        }
    }

    private void Apply(bool moveHome)
    {
        try
        {
            if (!connected || allowed) cursor.Release();
            else
            {
                if (moveHome) cursor.Release();
                if (moveHome) cursor.MoveToMain();
                cursor.RestrictToMain();
            }
            if (!connected && moveHome) cursor.MoveToMain();
            error = null;
        }
        catch (Exception ex)
        {
            allowed = false;
            error = "鼠标控制失败：" + ex.Message;
            // Never leave a possibly stale rectangle after a topology/API fault.
            try { cursor.Release(); } catch { /* reported by Error */ }
        }
    }
}
