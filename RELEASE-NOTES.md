Vehicle Radio mixes your microphone and music into Wardogs voice chat.

- Selectable microphone, music playback device and virtual cable destination.
- Independent voice/music volume and mute controls, speech ducking and meters.
- Automatically holds K while active; releases K on stop, audio disconnect or normal close.
- Original vehicle backdrop and compact audio controls.
- Apache 2.0 source license; portable Windows x64 build includes the .NET runtime.

Download `Wardogs-Vehicle-Radio-win-x64.zip`, extract the entire folder, and run `WardogsBridge.exe`. Install VB-CABLE separately from https://vb-audio.com/Cable/ and restart Windows if needed. Select CABLE Input in the app and CABLE Output as Wardogs' microphone. Bind in-game push-to-talk to K.

The music source captures every app on the chosen playback device. Keep game audio on a separate device for music-only transmission. Stop the bridge before typing elsewhere because K is held globally. Automatic K behavior needs an in-game check; the key lifecycle is covered by automated tests, and the earlier audio bridge was confirmed working by its user.
