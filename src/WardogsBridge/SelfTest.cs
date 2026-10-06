using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace WardogsBridge;

internal static class SelfTest
{
    private sealed class Constant(float value, int length = int.MaxValue) : ISampleProvider
    {
        public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        public int Read(float[] buffer, int offset, int count)
        {
            int read = Math.Min(count, length);
            Array.Fill(buffer, value, offset, read); return read;
        }
    }
    public static void Run(string path)
    {
        var report = new List<string>();
        try
        {
            void Check(bool pass, string name) { if (!pass) throw new Exception(name); report.Add("PASS " + name); }
            var keyEvents = new List<bool>();
            using (var key = new PushToTalk(keyEvents.Add))
            {
                key.Release(); Check(keyEvents.Count == 0, "Inactive bridge does not release an unrelated K key");
                key.Hold(); key.Refresh(); key.Refresh();
                Check(key.Held && keyEvents.SequenceEqual(new[] { false, false, false }), "K stays down while live; renewals do not tap/release it");
                key.Release(); key.Refresh(); key.Release();
                Check(!key.Held && keyEvents.SequenceEqual(new[] { false, false, false, true }), "Stop releases K once and cancels renewals");
                key.Hold();
            }
            Check(keyEvents[^1], "Close/dispose releases K");
            var failedEvents = new List<bool>();
            using (var key = new PushToTalk(up => { failedEvents.Add(up); if (!up) throw new Exception("Simulated input failure"); }))
            {
                try { key.Hold(); } catch { key.Release(); }
                Check(!key.Held && failedEvents.SequenceEqual(new[] { false, true }), "Failed activation releases potentially held K");
            }
            Check(System.Runtime.InteropServices.Marshal.SizeOf<PushToTalk.Input>() == (IntPtr.Size == 8 ? 40 : 28), "Native INPUT layout matches Windows");
            Check(PushToTalk.KEvent(false).Data.Keyboard.Scan == 0x25 && PushToTalk.KEvent(false).Data.Keyboard.Flags == 8 && PushToTalk.KEvent(true).Data.Keyboard.Flags == 10, "K uses physical scan code and correct down/up flags");
            bool failRelease = true;
            using (var key = new PushToTalk(up => { if (up && failRelease) throw new Exception("Simulated blocked release"); }))
            {
                key.Hold(); try { key.Release(); } catch { }
                Check(key.Held, "Blocked key release remains pending for retry");
                failRelease = false; key.Release(); Check(!key.Held, "Key release can recover on retry");
            }
            var c = new MixControls { Duck = false, MicGain = 1, MusicGain = .5f };
            var mixer = new BridgeMixer(new Constant(.2f), new Constant(.4f), c);
            var b = new float[962]; b[0] = b[^1] = 42;
            Check(mixer.Read(b, 1, 960) == 960 && Math.Abs(b[1] - .4f) < .0001f && b[0] == 42 && b[^1] == 42, "Mix, gains and buffer boundaries");
            c.MusicMuted = true; mixer.Read(b, 1, 960); Check(Math.Abs(b[1] - .2f) < .0001f, "Music mute preserves microphone");
            c.MicMuted = true; mixer.Read(b, 1, 960); Check(b[1] == 0, "Both mutes give silence");
            mixer = new BridgeMixer(new Constant(2), new Constant(2), new MixControls { Duck = false, MusicGain = 1 });
            mixer.Read(b, 1, 960); Check(b[1] == .98f, "Clipping guard");
            c = new MixControls { Duck = true, MusicGain = 1 };
            mixer = new BridgeMixer(new Constant(.1f), new Constant(.4f), c);
            for (int i = 0; i < 100; i++) mixer.Read(b, 1, 960);
            Check(b[960] < .21f && b[960] > .19f, "Speech ducks music to 25 percent");
            mixer = new BridgeMixer(new Constant(.1f), new Constant(.4f, 0), new MixControls());
            mixer.Read(b, 1, 960); Check(Math.Abs(b[960] - .1f) < .0001f, "Music underrun preserves voice");
            using var enumerator = new MMDeviceEnumerator();
            foreach (var flow in new[] { DataFlow.Capture, DataFlow.Render })
                foreach (var d in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                    using (d) report.Add($"DEVICE {flow}: {d.FriendlyName}");
            using var form = new BridgeForm();
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(Path.ChangeExtension(path, ".png"));
            form.Close();
            report.Add("PASS UI creation and rendering");
        }
        catch (Exception e) { report.Add("FAIL " + e); Environment.ExitCode = 1; }
        File.WriteAllLines(path, report);
    }
}
