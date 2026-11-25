# Build and replay the prototype

## Automated CI

Pull requests and branch pushes run Windows and Linux builds plus the asset-free reader-selection and fit/export proofs. Source-branch pushes also cover the initial workflow addition from a fork. Windows builds the full solution including WPF; Linux builds every non-WPF project. `Release readiness` requires both platform jobs to succeed. The workflow downloads official v0.22.1 and v0.20.1 reader fixtures and verifies their pinned ZIP SHA-256 hashes; it requires no Kenshi assets or secrets. Run the same checks locally with `pwsh Tools/CI.ps1 -CurrentReaders <v0.22.1-folder> -OldReaders <v0.20.1-folder> -Scratch <scratch-folder>`.

CI does not exercise rendered UI, authoring with original game assets, or in-game rides/dialogue. Those checks remain the manual procedures below.

Current candidate results: [v0.22.1 readers and cross-platform authoring](docs/readers-0221.md). The WPF and dialogue game scripts below are retained reproduction tools; the current pass exercised Avalonia and the ride verifier, with the goggles provider checked in the editor only.

## Avalonia on Linux and Windows

Build with the .NET 11 SDK. Run `Tools/PublishAvalonia.ps1 -Meitou <extracted-v0.22.1-game> -Output <publish-directory>` and then `python Tools/package-avalonia.py --publish <publish-directory> --nuget <NuGet-global-packages-directory> --out <handoff> --source-ref <full-build-commit>`. Use a clean checkout of the supplied full Git commit (`git rev-parse HEAD`) for the build. Each archive includes its runtime, third-party notices, license scope, CC0 text and a SOURCE.md pointing to that commit. Build work goes under the chosen publish directory.

The [author walkthrough](AVALONIA.md) covers the two desktop packages. To replay the Linux UI check, extract the Linux tarball under the test user's home, then run the following in a graphical Linux session with Bash, Python 3, xdotool and ImageMagick installed. To prepare the trial inputs on Windows, build the solution, run the seed generator, and assemble the provider archives from the published Transport ZIP. Extract the released Garru Saddle into a scratch directory and copy its inner folder as `Sample art`. Copy this kit to Linux or mount the same folder there.

```powershell
dotnet build MeitouMaestro.slnx -c Release
dotnet Tools/Seed/bin/Release/net11.0/Seed.dll "$qa/seeds"
python Tools/make_providers.py --transport "$transportDist/Meitou-Transport-0.3.0-playtest.6.zip" --seeds "$qa/seeds" --out "$kit/Packs"
Expand-Archive -LiteralPath "$transportDist/Garru-Saddle-0.3.0-playtest.6.zip" -DestinationPath "$qa/reference"
Copy-Item -LiteralPath "$qa/reference/Garru Saddle" -Recurse -Destination "$kit/Sample art"
```


```bash
bash Tools/linux-fit-trial.sh "$editor" "$kenshi" "$game" "$kit" "$HOME/maestro-trial"
```

Use a fresh final argument on each run. The driver reads Kenshi data and creates an isolated profile, a project and an export on the Linux filesystem. It switches between Garru and Pack Beast in real controls, waits for OpenGL to draw, sends orbit and zoom input, captures the visible window, saves, reopens and exports. It then opens the goggles provider in the same packaged executable. Copy the exported ZIP and Linux checksum file to Windows.

In a fresh Windows packs directory, put that Linux export beside `Meitou Transport.zip` and `Stoat Desert Ride.zip`. Run the Windows ride command below with `-Scenarios stoat`. Build `Tools/Checks/Checks.csproj` against the same release. `python Tools/verify-cross-platform.py --help` lists the inputs for comparing the Linux digest, JSON, FCS dependency, both assets, installed payloads and measured turn against playtest.6. The verifier selects a rendered mid-turn still from the recorded headings. Run `Checks --fit-proof <scratch-folder>` on each OS to exercise rotated fits, portable filenames and missing asset references.

## Reader selection regression

After building, run `dotnet Tools/Checks/bin/Release/net11.0/Checks.dll --reader-proof <v0.22.1-game> <v0.20.1-game> <scratch-folder>`. Use the actual extracted game folders. This rejects old readers, a missing assembly and mixed versions, then accepts the complete current pair and verifies the restart requirement when switching directories. The same check runs on Linux by publishing `Tools/Checks/Checks.csproj` with `-r linux-x64 --self-contained true` and running its `Checks` apphost. Keep check binaries and copied game assemblies local; they are not editor distribution files.

## WPF comparison tools

The WPF test kit is an optional local developer build. The public editor downloads use Avalonia. These instructions are for reproducing the local kit and checking a provider change. Paths below are PowerShell variables supplied by the tester.

## Build the editor and providers

Use a Windows x64 .NET 11 SDK with its Windows Desktop runtime, Python 3.11+ and MeitouClient v0.22.1. Open PowerShell at the Maestro source root. Set `$game` to the extracted game directory, `$kenshi` to the installed Kenshi directory, `$dotnetRoot` to the directory containing the x64 SDK's dotnet.exe, and `$transportDist` to a directory containing the four playtest.6 Transport release files used below (framework ZIP, source ZIP, Garru Saddle ZIP and Stoat Desert Ride ZIP). Choose a fresh working directory in `$qa`. The kit includes matching Transport source in **Source**.

```powershell
$env:MEITOU_DIR = $game
$env:Path = "$dotnetRoot;$env:Path"
.\build.ps1 -Meitou $game -Dotnet (Join-Path $dotnetRoot 'dotnet.exe')
dotnet Tools/Seed/bin/Release/net11.0/Seed.dll "$qa/seeds"
python Tools/make_providers.py --transport "$transportDist/Meitou-Transport-0.3.0-playtest.6.zip" --seeds "$qa/seeds" --out "$qa/packs"
python Tools/portable.py --dotnet-root $dotnetRoot --output "$qa/Editor"
```

## Recreate the examples

Set `$art` to the kit's **Sample art** folder. The WPF trial opens a new author project, selects both stock animal records, supplies the shared fit, presses Review, Save and Export, and captures the window. It installs the second provider, opens the untouched town starter and repeats the workflow with the same executable. Its JSON recipes and author projects remain in the trial directory.

```powershell
.\Tools\AuthorExamples.ps1 -Kenshi $kenshi -Meitou $game -Editor "$qa/Editor/Maestro.exe" -Packs "$qa/packs" -Art $art -Trial "$qa/author"
Copy-Item "$qa/author/exports/*.zip" "$qa/packs"
Copy-Item "$transportDist/Stoat-Desert-Ride-0.3.0-playtest.6.zip" "$qa/packs/Stoat Desert Ride.zip"
$env:MEITOU_MODS_CFG = "$qa/author/mods.cfg"
dotnet Tools/Checks/bin/Release/net11.0/Checks.dll "$qa/author/profile" $art "$qa/checks" "$qa/seeds/Dune Gate/Dune Gate.mod"
```

The checks use an isolated profile and temporary projects. They cover a custom animal record, bad bone, concurrent edits, interrupted save rollback, asset replacement, mount updates, legacy placement behavior, and inherited dialogue overrides. A broken descriptor is diagnosed alongside two good providers. Pass `--compare` and the generated saddle FCS, original saddle FCS, generated town FCS and starter town FCS as four paths to check the FCS records and dependencies.

## Run the game proofs

The two ride runs use the matching Transport verifier in a separate source checkout. Both installs contain the editor's exported saddle. The Pack Beast variant changes the Stoat start in its own test profile. The dialogue run loads only the condition framework and the editor-exported Dune Gate. Its trial driver talks to the keeper, uses normal inventory transfer to move the goggles and talks to the same keeper again. The ordinary town start lets the player do each step manually.

```powershell
.\Tools\LiveRides.ps1 -Game $game -Kenshi $kenshi -Packs "$qa/packs" -TransportSource "$transportDist/Meitou-Transport-0.3.0-playtest.6-source.zip" -TrialRoot "$qa/rides"
.\Tools\LiveGate.ps1 -Game $game -Kenshi $kenshi -Packs "$qa/packs" -Trial "$qa/gate"
python Tools/evidence.py --packs "$qa/packs" --reference "$transportDist/Garru-Saddle-0.3.0-playtest.6.zip" --garru "$qa/rides/stoat" --beast "$qa/rides/stoat-bags" --gate "$qa/gate" --output "$qa/author/exports/Evidence"
python Tools/kit.py --out "$qa/delivery" --dotnet-root $dotnetRoot --packs "$qa/packs" --exports "$qa/author/exports" --evidence "$qa/author/exports/Evidence" --transport-dist $transportDist
```

The kit script compares the distributed editor binaries with the hashes from both WPF trials and rereads the finished ZIP against its file checksums. Supplied game captures show the middle of the turn from the side. The full game logs and save slots stay in the trial directories. The archived WPF trial measurements in EVIDENCE.md were collected on v0.18.0.

## Hosted CI scope and reader-update order

Merge the CI bootstrap (PR #2) before the v0.22.1 reader update (PR #1). This branch incorporates that bootstrap while retaining its broader Windows/Linux regression matrix. Both platforms verify the official v0.22.1 reader fixture and the v0.20.1 rejection fixture, run `--reader-proof`, and run `--fit-proof`. Linux excludes the Windows-only WPF project. Development readiness requires both platform jobs.

Production promotion to `release` deliberately fails until validated production packaging and release acceptance checks are configured. Hosted checks do not run the editor UI, Kenshi-dependent checks, WSLg trials or live rides. Require fresh passing CI on the reader branch before merging; the bootstrap's v0.20.1 Windows result does not verify these readers.
