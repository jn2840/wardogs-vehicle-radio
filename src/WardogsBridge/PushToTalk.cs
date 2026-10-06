using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WardogsBridge;

// A down event is renewed while live so switching into the game still engages
// push-to-talk. No up events occur until the bridge stops.
internal sealed class PushToTalk : IDisposable
{
    private readonly Action<bool> sendKey;
    private readonly object gate = new();
    public PushToTalk(Action<bool>? send = null)
    {
        sendKey = send ?? SendK;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }
    public bool Held { get; private set; }
    public void Hold()
    {
        // Mark first so cleanup can release after a partially successful call.
        lock (gate) { Held = true; sendKey(false); }
    }
    public void Refresh() { lock (gate) { if (Held) sendKey(false); } }
    public void Release()
    {
        lock (gate)
        {
            if (!Held) return;
            sendKey(true);
            Held = false;
        }
    }
    private void OnProcessExit(object? sender, EventArgs e) { try { Release(); } catch { } }
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) { try { Release(); } catch { } }
    public void Dispose()
    {
        Release();
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public ushort VirtualKey, Scan;
        public uint Flags, Time;
        public UIntPtr ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int X, Y;
        public uint MouseData, Flags, Time;
        public UIntPtr ExtraInfo;
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    internal static Input KEvent(bool up) => new()
    {
        Type = 1,
        Data = new InputUnion { Keyboard = new KeyboardInput { Scan = 0x25, Flags = 0x0008u | (up ? 0x0002u : 0) } }
    };
    private static void SendK(bool up)
    {
        if (SendInput(1, [KEvent(up)], Marshal.SizeOf<Input>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not send the K key. Run the radio and game at the same permission level.");
    }
}
