# Maestro v0.1.0-playtest.2 release notes

Prepared for publication after PR review and merge. This page does not announce an available release.

## What this playtest supports

Author a riding pack using **MeitouClient v0.22.1** readers and **Meitou Transport v0.3.0-playtest.6**. Select the extracted game folder containing Meitou.Core.dll and Meitou.Data.dll; the editor does not bundle those assemblies. Both the Avalonia editor and WPF reference frontend now report the same supported reader version. Descriptor version 1 and Transport schema 3 remain unchanged.

Choose the Windows x64 ZIP or Linux x64 tar.gz. Each editor includes its .NET runtime, author walkthrough, licenses and exact source reference. Extract the whole folder before launching MeitouMaestro.exe on Windows or ./MeitouMaestro in a Linux graphical session. An installed copy of Kenshi is required to read its records and art.

- **Fit my own animal:** follow [the riding-pack walkthrough](../AVALONIA.md), with the [Transport playtest.6 framework and sample downloads](https://github.com/Meitou-Modding-Ecosystem/MeitouTransport/releases/tag/v0.3.0-playtest.6).
- **Add an action from my framework:** use the [descriptor contract](../DESCRIPTORS.md). Frameworks supply editor actions as data; their gameplay runs in Meitou.
- **Still using Meitou v0.20.1:** use [Maestro playtest.1](https://github.com/Meitou-Modding-Ecosystem/MeitouMaestro/releases/tag/v0.1.0-playtest.1).
- **Feedback:** [Maestro Issues](https://github.com/Meitou-Modding-Ecosystem/MeitouMaestro/issues). Include versions, platform, reproduction steps and relevant errors.

## Verification and limits

Garru Saddle was authored, saved, reopened and exported in the packaged Windows editor and Ubuntu 24.04 under WSLg. The Linux ZIP was transferred unchanged and ridden on Windows. Three Stoat rides passed with Transport playtest.6, including the Pack Beast with stock bags. Maximum rider error was 0.02344 world units against a 0.08 threshold. [Measurements and screenshots](readers-0221.md).

Windows used ANGLE on an RTX 3070. WSLg used Mesa llvmpipe software OpenGL; native Linux GPU/desktop coverage remains a tester opportunity. The bind-pose preview uses solid colours. Existing sample-art overlap is separate from attachment accuracy. The independent goggles provider was checked in the editor; its dialogue gameplay and the WPF authoring flow were not replayed in this pass.

## Publication checklist

1. Build both desktop packages from the final clean source commit using SDK 11.0.100 and the official v0.22.1 readers. Supply that full commit to the packager with --source-ref. Compare application payloads with the tested builds; investigate executable differences before relying on prior gameplay evidence.
2. Check archive layouts, case-sensitive local documentation links, source references, notices and absence of game assemblies. Publish the matching source archive and SHA-256 manifest alongside both desktop archives.
3. After review, merge and tag v0.1.0-playtest.2 at the selected release commit. Publish as a prerelease and verify downloaded hashes and source/download links.
4. Replace the README's candidate banner with the versioned playtest.2 download link, retain the playtest.1 compatibility link, and sync the personal fork. Do not describe a draft release as downloadable.
