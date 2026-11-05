# Local One Night prototype

## Verified environment

- Unity Editor 6000.3.9f1 (`7a9955a4f2fa`), Windows x64.
- Official installed 3D cross-platform URP template 17.0.14; Editor resolved URP 17.3.0, Input System 1.18.0 and Test Framework 1.6.0. Exact dependencies are locked in `Packages/packages-lock.json`.
- Blender 5.2.1 LTS (`9e2066aef7ef`). `ArtSource/create_toys.py` produces independent FBX meshes and a `.blend` source, using metres and explicit export axes.
- Unity/Blender MCP tools were not exposed to this Codex session. Unity batch CLI and the handoff's explicitly permitted Blender background fallback were used. No bridge was installed and no live MCP connection is claimed.
- `game-dev` was not on PATH. Its canonical packaging, vendoring, sealed capture and performance workflows were not executed. No provider generation, paid API call or external service initialization was used.
- The first Unity launch failed with no valid Editor license. After owner sign-in, subsequent compilation and scene creation succeeded.

## Open and play

Open this repository in Unity 6000.3.9f1. Open `Assets/_CraneTactics/Scenes/OneNight.unity`, then Play. If regenerating the scene, use **Crane Tactics > Create OneNight Scene**. The command imports the toy prefabs only after the FBX one-metre probe passes.

Click a toy to auto-grab. Move the pointer across the floor to carry, then click or Space to release. WASD/arrows move the claw; Space selects the toy under its footprint. Esc pauses. Sound starts off. Retry resets only the current cabinet; the Battle 2 roster always retains Slimy. Completion offers replay.

The gold footprint estimates landing from current horizontal momentum; it is deliberately not a guaranteed cell. The green bar shows Slimy's push direction. Discrete rules settle by centre position and clamp coordinate ties toward the higher cell. Enemy movement tries Y before X. A spring drives visible carried motion; Rigidbody/PhysX drives the drop. A 2-second fallback prevents indefinitely unsettled drops.

## Source boundaries

- `Scripts/Rules`: engine-independent board, occupancy, push, damage, outcome and intent planning.
- `CraneInput` / `CraneController`: input and spring motion, separately from board rules.
- `OneNightGame`: runtime graybox assembly, phase control, toy presentation, HUD and feedback.
- `Editor/BuildOneNight`: reproducible scene, calibrated FBX import and Windows development build.
- `SmokePlayback`: development-only, explicit `--smoke-output` automated input driver. It runs real carry/drop/PhysX but is not human-play evidence.
- `Tests/Rules`: dependency-free .NET executable tests.
- Unity EditMode tests cover rules; PlayMode tests cover physics flow, timeout, pause and synthetic pointer raycast.

## Visual provenance and limitations

Models are original procedural study meshes, not downloaded third-party models. Knight's owner-provided Library turnaround was viewed and copied to the ignored local path `ArtSource/References/Knight.png`. The model is a simplified study rather than a faithful finished reconstruction. Heavy/Slimy follow the handoff's text identities; their original sheets and the cabinet sheet have not yet been visually checked. Cabinet, claw, and both rivals also have Blender study meshes. The generated [visual target](../art-direction/VISION.md) guides further refinement; current models do not claim final fidelity. No asset-marketplace license, canonical `game-dev` package, or reference-image redistribution license is asserted.

FBX import checks include an asymmetric orientation probe: Blender +X must map to Unity +X, and Blender front −Y must map to Unity −Z. This project uses FBX `axis_forward='Z', axis_up='Y'` plus a half-turn baked into export objects to satisfy Unity's importer convention. The script restores the original authoring transforms for `.blend` saving. A scale-only first pass missed a 180-degree facing error; fresh runtime images exposed it and the directional probe was added.

No UGS authentication, cloud save, economy, analytics calls, or monetization gameplay are implemented. Template dependencies are not evidence of service usage.
