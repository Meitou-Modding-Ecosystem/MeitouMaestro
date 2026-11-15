# Meitou Maestro

[Download the Windows x64 or Linux x64 editor](https://github.com/mattresspad/MeitouMaestro/releases/tag/v0.1.0-playtest.1) · [MeitouClient v0.20.1](https://github.com/VlcekM/MeitouClient/releases/tag/v0.20.1)

## Fit my own animal

Install Kenshi, the packaged Meitou v0.20.1 game, and [Meitou Transport playtest.5](https://github.com/mattresspad/MeitouTransport/releases/tag/v0.3.0-playtest.5). Open the Avalonia editor, choose your mod folder, select your animal and choose **Add riding fit**. Review the dependency and seat, save the project, and export an installable ZIP. Players install the authored pack and its dependencies. [Make a riding pack](AVALONIA.md)

## Add an action from my framework

Place a version-1 `maestro.json` beside your framework's FCS file. It identifies the eligible records, inputs, dependencies and edits the author can review. The framework supplies gameplay when the pack runs in Meitou. [Descriptor reference](DESCRIPTORS.md) · [Independent goggles action](Providers/Gate/maestro.json) · [Transport riding action](https://github.com/mattresspad/MeitouTransport/blob/v0.3.0-playtest.5/mods/Meitou%20Transport/maestro.json)

## Build and test

Use .NET SDK 11.0.100, point `MEITOU_DIR` at the extracted Windows game folder, and build `dotnet build MeitouMaestro.slnx -c Release`. Avalonia is the distributed desktop app. The WPF implementation in `App/` is available for comparison. [Publish and replay](TESTING.md) · [License terms](LICENSING.md)

[Measured Stoat turns and voyage checks on v0.20.1](https://github.com/mattresspad/MeitouTransport/blob/v0.3.0-playtest.5/docs/playtest-5.md)
