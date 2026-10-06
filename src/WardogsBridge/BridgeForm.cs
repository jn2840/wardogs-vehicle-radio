using System.Diagnostics;
using System.Text.Json;
using NAudio.CoreAudioApi;

namespace WardogsBridge;

public sealed class BridgeForm : Form
{
    private record Device(string Id, string Name) { public override string ToString() => Name; }
    private record Settings(string? Mic, string? Music, string? Output, int MicVolume, int MusicVolume, bool Duck);
    private readonly ComboBox mic = Pick(), music = Pick(), output = Pick();
    private readonly TrackBar micVolume = Slider(100), musicVolume = Slider(30);
    private readonly CheckBox duck = new() { Text = "Lower music while I speak", Checked = true, AutoSize = true };
    private readonly CheckBox micMute = new() { Text = "Mute mic", AutoSize = true }, musicMute = new() { Text = "Mute music", AutoSize = true };
    private readonly Button start = new() { Text = "START BRIDGE", AutoSize = true }, refresh = new() { Text = "Refresh devices", AutoSize = true };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(640, 0) };
    private readonly RadioMeter micMeter = new(), musicMeter = new(), outMeter = new();
    private readonly MixControls controls = new();
    private AudioEngine? engine;
    private readonly PushToTalk pushToTalk = new();
    private readonly Label liveBadge = new() { Text = "●  OFF AIR", AutoSize = true, ForeColor = Color.FromArgb(179, 194, 186), BackColor = Color.Transparent };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 80 };
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WardogsBridge", "settings.json");
    private Settings? settings;
    private static ComboBox Pick() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 630 };
    private static TrackBar Slider(int value) => new() { Minimum = 0, Maximum = 150, Value = value, TickFrequency = 25, Width = 410 };
    public BridgeForm()
    {
        Text = "Wardogs • Vehicle Radio"; Size = new Size(1030, 930); MinimumSize = new Size(1000, 830);
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(13, 19, 21); ForeColor = Color.FromArgb(232, 238, 231);
        var backdrop = new RadioBackdrop { Dock = DockStyle.Fill }; Controls.Add(backdrop);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 650, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(26, 22, 12, 18), BackColor = Color.Transparent };
        backdrop.Controls.Add(panel);
        Label Caption(string text, int size = 10, Color? color = null) => new() { Text = text, AutoSize = true, MaximumSize = new Size(555, 0), Font = new Font("Segoe UI", size), ForeColor = color ?? ForeColor, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 6) };
        FlowLayoutPanel Card() => new() { Width = 595, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(22, 31, 32), Padding = new Padding(18, 14, 18, 14), Margin = new Padding(0, 0, 0, 12) };
        var accent = Color.FromArgb(215, 187, 113);
        panel.Controls.Add(Caption("WARDOGS  /  COMMUNITY COMMS", 10, accent));
        var title = Caption("VEHICLE RADIO", 30); title.Font = new Font("Segoe UI Semibold", 30, FontStyle.Bold); panel.Controls.Add(title);
        panel.Controls.Add(Caption("Bring the soundtrack. Keep your crew connected.", 11));
        liveBadge.Margin = new Padding(0, 6, 0, 16); panel.Controls.Add(liveBadge);
        var routing = Card(); panel.Controls.Add(routing);
        routing.Controls.Add(Caption("01   AUDIO ROUTING", 10, accent));
        foreach (var (label, box) in new[] { ("Microphone", mic), ("Music playback device", music), ("Virtual destination  ·  CABLE Input", output) })
        {
            routing.Controls.Add(Caption(label, 9));
            box.Width = 555; box.BackColor = Color.FromArgb(37, 48, 49); box.ForeColor = ForeColor;
            box.DrawMode = DrawMode.OwnerDrawFixed; box.ItemHeight = 24;
            box.DrawItem += (_, e) =>
            {
                using var brush = new SolidBrush(box.BackColor); e.Graphics.FillRectangle(brush, e.Bounds);
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, box.Items[e.Index]?.ToString() ?? "", box.Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), box.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
            box.FlatStyle = FlatStyle.Flat; box.Margin = new Padding(0, 0, 0, 10); routing.Controls.Add(box);
        }
        var mix = Card(); panel.Controls.Add(mix); mix.Controls.Add(Caption("02   MIX YOUR TRANSMISSION", 10, accent));
        var channels = new FlowLayoutPanel { Width = 555, AutoSize = true, Margin = Padding.Empty, BackColor = Color.Transparent, WrapContents = false };
        mix.Controls.Add(channels);
        void Channel(string name, TrackBar slider, CheckBox mute, RadioMeter meter)
        {
            var channel = new FlowLayoutPanel { Width = 270, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 12, 0), BackColor = Color.Transparent };
            var level = Caption($"{name}  ·  {slider.Value}%", 11); channel.Controls.Add(level);
            slider.Width = 260; slider.Height = 40; slider.TickStyle = TickStyle.None; slider.BackColor = mix.BackColor; slider.Margin = Padding.Empty; channel.Controls.Add(slider);
            slider.ValueChanged += (_, _) => level.Text = $"{name}  ·  {slider.Value}%";
            mute.BackColor = Color.Transparent; channel.Controls.Add(mute);
            meter.Width = 260; meter.Height = 8; meter.Margin = new Padding(0, 8, 0, 12); channel.Controls.Add(meter);
            channels.Controls.Add(channel);
        }
        Channel("VOICE", micVolume, micMute, micMeter); Channel("MUSIC", musicVolume, musicMute, musicMeter);
        duck.BackColor = Color.Transparent; mix.Controls.Add(duck);
        mix.Controls.Add(Caption("TRANSMISSION LEVEL", 8, Color.FromArgb(157, 177, 168)));
        outMeter.Width = 555; outMeter.Height = 8; outMeter.Margin = Padding.Empty; mix.Controls.Add(outMeter);
        var transmission = Card(); panel.Controls.Add(transmission);
        transmission.Controls.Add(Caption("03   GO LIVE", 10, accent));
        transmission.Controls.Add(Caption("K is held automatically while the bridge is active.", 10));
        var buttons = new FlowLayoutPanel { AutoSize = true, Width = 555, Margin = Padding.Empty };
        buttons.Controls.Add(start); buttons.Controls.Add(refresh);
        var setup = new Button { Text = "Audio setup", AutoSize = true }; buttons.Controls.Add(setup);
        var cable = new Button { Text = "Get VB-CABLE", AutoSize = true }; buttons.Controls.Add(cable);
        foreach (Button b in buttons.Controls)
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Color.FromArgb(69, 87, 78);
            b.ForeColor = ForeColor; b.BackColor = Color.FromArgb(37, 48, 44); b.Padding = new Padding(8, 6, 8, 6); b.Cursor = Cursors.Hand;
        }
        start.BackColor = accent; start.ForeColor = Color.FromArgb(19, 25, 23); start.Font = new Font(Font, FontStyle.Bold);
        transmission.Controls.Add(buttons); status.MaximumSize = new Size(555, 0); status.Margin = new Padding(0, 10, 0, 0); transmission.Controls.Add(status);
        panel.Controls.Add(Caption("Set Wardogs' microphone to CABLE Output and push-to-talk to K. Keep music separate from game audio. Stop the bridge before typing in other apps.", 9, Color.FromArgb(186, 203, 192)));
        setup.Click += (_, _) => Open("ms-settings:apps-volume"); cable.Click += (_, _) => Open("https://vb-audio.com/Cable/");
        refresh.Click += (_, _) => LoadDevices(); start.Click += (_, _) => Toggle();
        micVolume.ValueChanged += (_, _) => controls.MicGain = micVolume.Value / 100f;
        musicVolume.ValueChanged += (_, _) => controls.MusicGain = musicVolume.Value / 100f;
        micMute.CheckedChanged += (_, _) => controls.MicMuted = micMute.Checked;
        musicMute.CheckedChanged += (_, _) => controls.MusicMuted = musicMute.Checked;
        duck.CheckedChanged += (_, _) => controls.Duck = duck.Checked;
        timer.Tick += (_, _) =>
        {
            Meter(micMeter, controls.MicPeak); Meter(musicMeter, controls.MusicPeak); Meter(outMeter, controls.OutputPeak);
            try { if (engine != null) pushToTalk.Refresh(); else if (pushToTalk.Held) pushToTalk.Release(); }
            catch (Exception e) { Stop(); status.Text = "Push-to-talk stopped: " + e.Message; }
        };
        try { settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)); } catch { }
        if (settings != null) { micVolume.Value = Math.Clamp(settings.MicVolume, 0, 150); musicVolume.Value = Math.Clamp(settings.MusicVolume, 0, 150); duck.Checked = settings.Duck; }
        LoadDevices(); timer.Start();
        FormClosing += (_, _) => { Stop(); timer.Dispose(); Save(); };
        Application.ApplicationExit += OnApplicationExit;
        Disposed += (_, _) => { try { pushToTalk.Dispose(); } catch { } Application.ApplicationExit -= OnApplicationExit; };
    }
    private static void Meter(RadioMeter bar, float value) => bar.Value = Math.Clamp((int)(value * 100), 0, 100);
    private void Open(string target) { try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch (Exception e) { status.Text = e.Message; } }
    private void LoadDevices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            void Fill(ComboBox box, DataFlow flow, string? saved, bool virtualOnly = false)
            {
                string? previous = (box.SelectedItem as Device)?.Id ?? saved;
                box.Items.Clear();
                foreach (var d in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
                {
                    using (d) { if (!virtualOnly || d.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase) || d.FriendlyName.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase)) box.Items.Add(new Device(d.ID, d.FriendlyName)); }
                }
                box.SelectedItem = box.Items.Cast<Device>().FirstOrDefault(d => d.Id == previous);
                if (box.SelectedIndex < 0 && box.Items.Count > 0) box.SelectedIndex = 0;
            }
            Fill(mic, DataFlow.Capture, settings?.Mic); Fill(music, DataFlow.Render, settings?.Music); Fill(output, DataFlow.Render, settings?.Output, true);
            // Prefer a physical microphone and default playback endpoint on first launch.
            if (settings == null)
            {
                mic.SelectedItem = mic.Items.Cast<Device>().FirstOrDefault(d => !d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) && !d.Name.Contains("Sonar", StringComparison.OrdinalIgnoreCase) && !d.Name.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase)) ?? mic.SelectedItem;
                using var playback = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                music.SelectedItem = music.Items.Cast<Device>().FirstOrDefault(d => d.Id == playback.ID) ?? music.SelectedItem;
            }
            status.Text = output.Items.Count == 0 ? "Virtual cable missing. Click Get VB-CABLE, install the driver, restart Windows, then refresh." : "Ready. Choose devices and start the bridge.";
        }
        catch (Exception e) { status.Text = "Could not list devices: " + e.Message; }
    }
    private void Toggle()
    {
        if (engine != null) { Stop(); status.Text = "Bridge stopped."; return; }
        if (mic.SelectedItem is not Device m || music.SelectedItem is not Device s || output.SelectedItem is not Device o) { status.Text = "Select all three devices. A virtual cable must be installed first."; return; }
        if (m.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase)) { status.Text = "Choose a physical microphone to avoid feeding the bridge back into itself."; return; }
        try
        {
            engine = new AudioEngine();
            engine.Failed += message => { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() => { Stop(); status.Text = "Audio disconnected: " + message; })); };
            engine.Start(m.Id, s.Id, o.Id, controls);
            pushToTalk.Hold();
            start.Text = "STOP BRIDGE"; mic.Enabled = music.Enabled = output.Enabled = refresh.Enabled = false;
            liveBadge.Text = "●  LIVE  /  K HELD"; liveBadge.ForeColor = Color.FromArgb(215, 187, 113);
            status.Text = "Broadcasting voice + music. K is held until you stop the bridge.";
            Save();
        }
        catch (Exception e) { Stop(); status.Text = "Could not start: " + e.Message; }
    }
    private void Stop()
    {
        try { pushToTalk.Release(); }
        catch (Exception e) { status.Text = "Could not release K: " + e.Message + " Press and release K manually."; }
        engine?.Dispose(); engine = null; start.Text = "START BRIDGE";
        liveBadge.Text = "●  OFF AIR"; liveBadge.ForeColor = Color.FromArgb(179, 194, 186);
        mic.Enabled = music.Enabled = output.Enabled = refresh.Enabled = true;
        controls.MicPeak = controls.MusicPeak = controls.OutputPeak = 0;
    }
    private void OnApplicationExit(object? sender, EventArgs e) { try { pushToTalk.Release(); } catch { } }
    private void Save()
    {
        try
        {
            settings = new Settings((mic.SelectedItem as Device)?.Id, (music.SelectedItem as Device)?.Id, (output.SelectedItem as Device)?.Id, micVolume.Value, musicVolume.Value, duck.Checked);
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings));
        }
        catch { /* Preferences must not prevent audio operation. */ }
    }
}
