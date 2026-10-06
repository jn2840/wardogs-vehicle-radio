using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WardogsBridge;

public sealed class MixControls
{
    public volatile float MicGain = 1, MusicGain = .3f;
    public volatile bool MicMuted, MusicMuted, Duck = true;
    public volatile float MicPeak, MusicPeak, OutputPeak;
}

// The output clock pulls both sources. Silence on underrun keeps voice working
// even when the music app pauses or loopback delivers no packets.
public sealed class BridgeMixer(ISampleProvider mic, ISampleProvider music, MixControls controls) : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    private float[] voice = [], song = [];
    private float envelope, duckGain = 1;
    public int Read(float[] buffer, int offset, int count)
    {
        if (voice.Length < count) { voice = new float[count]; song = new float[count]; }
        Array.Clear(voice, 0, count); Array.Clear(song, 0, count);
        mic.Read(voice, 0, count); music.Read(song, 0, count);
        float vp = 0, sp = 0, op = 0;
        for (int i = 0; i < count; i++)
        {
            float v = controls.MicMuted ? 0 : voice[i] * controls.MicGain;
            float s = controls.MusicMuted ? 0 : song[i] * controls.MusicGain;
            envelope = Math.Max(Math.Abs(v), envelope * .99995f);
            float target = controls.Duck && envelope > .035f ? .25f : 1;
            duckGain += (target - duckGain) * (target < duckGain ? .002f : .00004f);
            s *= duckGain;
            // Guard against clipping when voice and music peak together.
            buffer[offset + i] = Math.Clamp(v + s, -.98f, .98f);
            vp = Math.Max(vp, Math.Abs(v)); sp = Math.Max(sp, Math.Abs(s));
            op = Math.Max(op, Math.Abs(buffer[offset + i]));
        }
        controls.MicPeak = vp; controls.MusicPeak = sp; controls.OutputPeak = op;
        return count;
    }
}

public sealed class AudioEngine : IDisposable
{
    private readonly List<IDisposable> resources = [];
    private WasapiCapture? mic, music;
    private WasapiOut? output;
    private bool stopping;
    public event Action<string>? Failed;
    public void Start(string micId, string musicId, string outputId, MixControls controls)
    {
        if (musicId == outputId) throw new InvalidOperationException("Music source and bridge destination must differ to avoid feedback.");
        try
        {
            var enumerator = new MMDeviceEnumerator(); resources.Add(enumerator);
            var md = enumerator.GetDevice(micId); resources.Add(md);
            var sd = enumerator.GetDevice(musicId); resources.Add(sd);
            var od = enumerator.GetDevice(outputId); resources.Add(od);
            mic = new WasapiCapture(md); resources.Add(mic);
            music = new WasapiLoopbackCapture(sd); resources.Add(music);
            var mixer = new BridgeMixer(Source(mic), Source(music), controls);
            output = new WasapiOut(od, AudioClientShareMode.Shared, false, 60); resources.Add(output);
            output.PlaybackStopped += (_, e) => { if (!stopping) Failed?.Invoke(e.Exception?.Message ?? "Audio output stopped. Refresh devices and reconnect."); };
            output.Init(mixer.ToWaveProvider());
            mic.StartRecording(); music.StartRecording(); output.Play();
        }
        catch { Dispose(); throw; }
    }
    private ISampleProvider Source(WasapiCapture capture)
    {
        var buffered = new BufferedWaveProvider(capture.WaveFormat)
        { BufferDuration = TimeSpan.FromMilliseconds(500), DiscardOnBufferOverflow = true, ReadFully = true };
        capture.DataAvailable += (_, e) =>
        {
            // Drop old packets after scheduling stalls instead of accumulating lag.
            if (buffered.BufferedDuration.TotalMilliseconds > 180) buffered.ClearBuffer();
            buffered.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };
        capture.RecordingStopped += (_, e) => { if (!stopping) Failed?.Invoke(e.Exception?.Message ?? "Capture stopped. Refresh devices and reconnect."); };
        ISampleProvider sample = buffered.ToSampleProvider();
        if (sample.WaveFormat.Channels == 1) sample = new MonoToStereoSampleProvider(sample);
        else if (sample.WaveFormat.Channels > 2)
        {
            // Select the front left/right pair from surround playback devices.
            var downmix = new MultiplexingSampleProvider([sample], 2);
            downmix.ConnectInputToOutput(0, 0); downmix.ConnectInputToOutput(1, 1);
            sample = downmix;
        }
        if (sample.WaveFormat.SampleRate != 48000) sample = new WdlResamplingSampleProvider(sample, 48000);
        return sample;
    }
    public void Dispose()
    {
        stopping = true;
        try { output?.Stop(); } catch { }
        try { mic?.StopRecording(); } catch { }
        try { music?.StopRecording(); } catch { }
        for (int i = resources.Count - 1; i >= 0; i--) { try { resources[i].Dispose(); } catch { } }
        resources.Clear();
    }
}
