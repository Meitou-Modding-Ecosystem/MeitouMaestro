# Maestro editor trials · MeitouClient v0.18.0

## Avalonia 0.1.0-prototype.2 · Ubuntu to Windows

The packaged Linux x64 application ran in a visible Ubuntu 24.04.5 WSLg window as user `maestro` (UID 1000). Its author project lived on ext4. Mesa llvmpipe rendered the actual Garru and Pack Beast meshes, their shared bone and the CC0 saddle. The UI driver selected both records, orbited and zoomed the Garru, reviewed the dependency and files, saved, reopened the fit and exported it. The same editor discovered Gear Conditions and reviewed the goggles condition on the gatekeeper’s line.

The Linux export and the Windows copy had SHA-256 `6a66b6f22fe2aaf474ed8b57107ff85d166680d2446e51fe6851fcf0f99d5e46`. All four installed files matched that ZIP byte for byte. The FCS dependency and schema-3 definition matched the released Garru Saddle semantically, and both art files matched exactly. The Windows client registered one mount fit from Garru Saddle.

| Stoat Garru ride on Windows x64 | Posed frames | Turn frames | Heading change | Max saddle / seat error (world units) |
| --- | ---: | ---: | ---: | ---: |
| Ubuntu WSLg Avalonia export | 147 | 33 | 91.9° | 0.00873 / 0.00873 |
| Windows WPF reference export | 148 | 33 | 91.9° | 0.00873 / 0.00873 |

The final ride used the released Windows renderer and Transport playtest.4 in a fresh profile. The trace recorded walk and run, 11.4 units of climb, an occupied save, a dismounted reload, remount, dismount and a 20.2-unit on-foot order. The mid-turn still is from tick 182. The handoff contains the original fitting view, the Windows mid-turn capture, payload hashes and the verifier output.

The extracted Windows Avalonia build independently rendered the fitting view through ANGLE on a GeForce RTX 3070, saved and reopened a scratch fit, exported it, and reviewed the goggles action. A separate Linux scratch project exercised saddle pitch/yaw/roll of `20 45 -10`, seat offset `1.5 2.6 -1.25`, and rider facing `65`. The exported values and shared transform checks passed. Both platforms rejected missing and mis-cased asset references. The Linux core suite also exercised external edits, rollback, custom records, schema upgrades and dialogue overrides.

The platform evidence covers Ubuntu under WSLg and Windows x64. A native-Linux desktop tester can follow the same [walkthrough](AVALONIA.md).

## WPF 0.1.0-prototype.1

The same portable Windows executable created both exports through the WPF record picker, inputs, Review, Save and Export. The saddle trial started with an empty folder. The dialogue trial started from the unconditioned Dune Gate fixture after installing Gear Conditions. The portable editor started with both DOTNET_ROOT variables cleared and its bundled Windows Desktop runtime.

| Live Stoat trial | Posed frames checked | Frames in turn | Heading change | Largest saddle / seat error |
| --- | ---: | ---: | ---: | ---: |
| Garru | 148 | 33 | 91.9° | 0.00873 / 0.00873 world units |
| Pack Beast with native bags | 136 | 31 | 91.9° | 0.00873 / 0.00873 world units |

Both runs mounted, walked, ran, turned, saved while mounted, reloaded and remounted. The released game's renderer supplied each image. The Pack Beast carried its stock backpack throughout all 136 verified frames. `pack-beast-side.png` captures the middle of the corner at simulation tick 179. `garru-side.png` shows the Garru at tick 183. The images show the existing seated character pose and placeholder saddle art.

The tested install contained Meitou Transport, the editor-exported Garru Saddle and Stoat Desert Ride. The Pack Beast scenario substituted the stock Pack Beast in its isolated copy of the Stoat start. The final exported saddle's four payload files matched both live installations byte for byte. The JSON, FCS dependencies and art also matched the released reference semantically or byte for byte as appropriate.

The dialogue client loaded Gear Conditions and the editor-exported Dune Gate. With Square Goggles in Nara's head slot, the East Gate Keeper said “Those square lenses will serve you well in the dunes.” With those same goggles transferred to the main inventory the same keeper said “Mind the sand out there.” The test observed the conversation text that feeds Meitou's normal dialogue window.

## Files in the kit's Evidence folder

- `fit-editor.png` and `dialogue-editor.png` show the WPF controls at review. `editor-checks.txt` identifies the same three editor binaries in both processes.
- `garru-side.png`, `pack-beast-side.png` and the final turn stills show the ride. The two `*-ride.txt` extracts include the runtime's attachment and motion measurements.
- `gate-conversation.txt` records both branches. `installed-payloads.txt` checks the final exported packs against the files loaded by each isolated game instance.
- `core-checks.txt` records the checks for custom project records, multi-file edits, legacy data, invalid providers and inherited conditions.

The WPF tests drive the real controls programmatically. The game trials use a headless Windows client with its renderer or conversation system running. The editor shows the model in rest pose with solid preview colours. Inspect the final material, backpack clearance and animated rider in-game.
