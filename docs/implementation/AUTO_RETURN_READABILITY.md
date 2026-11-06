# Auto Return continuity and readability

Follow-up to Draft PR #4, based on `667c764e6ce7dca05ee4f0578dd1021bf4bab6d9`.
Scope: existing B auto-return presentation, camera/HUD, and existing tactical cases.
No new character, cabinet, package, or gameplay mode. A and the default One Night boards stay available.

## Implementation choices

- Keep the existing PhysX release and first observed toy contact. After that contact's short 60ms observation window, settle through BoardState once, avoiding a long bounce before showing the push.
- BoardState records immutable pose values after impact/contact, Knight follow-up, and enemy action. These are results of the existing rules, not a second simulator. Revision/token guards still reject duplicate and cancelled resolves.
- B playback locks input and disables actor collision while it shows recoil (120ms), slide (380ms), chute fall/shrink (550ms), then follow-up actions separately (400ms each). No animation or collision callback applies damage. Non-captured defeated toys tip and shrink. The next input/result screen waits for playback.
- Pause freezes presentation; restart/mode switch discards it with the old cabinet. HP and remaining-enemy badges follow the visible beats instead of revealing the final board early.
- B camera moves smoothly between board overview, lifted transport framing, and landing view. A retains its camera. A larger claw and thicker cable help connect the held toy with the crane. HOME/CAPTURE, HELD, grip clock, and phase prompts identify what is held, where it is going, and when to release. The bottom strip replaces B's large corner card. These are deliberate practical framing changes, not a reproduction of VISION artwork.

The camera-only alternative would leave instantaneous target movement unresolved. Driving board rules from new rigidbody pushes would add ambiguous collision outcomes and duplicate authority. Recorded authoritative beats fit the existing architecture and the requested continuity.

## Tactical comparison

The default Battle 1 and Battle 2 layouts are unchanged. Esc exposes the existing optional cases.

- **CASE A, early-release combo:** Knight `(4,3)` HP 2, hostile Heavy `(1,2)` HP 1, Guard `(4,2)` HP 3, chute `(5,2)`. Heavy's existing 2 damage leaves Guard alive to be pushed into the chute. Knight's adjacent follow-up finishes Heavy. This offers a one-action win if timed correctly. A central hold/capture of Heavy followed by Guard costs two actions and leaves Knight at 1 HP after the first turn. Both approaches remain legal. Knight sits off the flight line; the first trial at `(3,2)` sometimes intercepted the falling Heavy and was rejected after a PlayMode failure.
- **CASE B, direct capture:** Guard `(4,1)` HP 2, Knight `(1,4)` HP 3, chute `(5,2)`. The short return permits a central grip to capture the sole rival in one action. There is no impact partner and no advantage to spending an extra move creating one.
- Existing CASE C still demonstrates a failed contact pushing Scout into the chute without phantom damage.

Only CASE A's positions/HP change from PR #4. This controlled example demonstrates a rational reason for each action. It does **not** establish that direct capture dominance has been eliminated throughout default One Night or that players will discover/enjoy the combo. The broad default balance and human input feel remain subjects for playtesting.

## Verification and media

Verified locally on **2026-11-07 JST**, Unity **6000.3.9f1 (`7a9955a4f2fa`)** / URP. Automated input drives the real Unity crane, PhysX, renderer and Windows player. It is not a human playtest, normal OS-input usability study, audio listening test or FPS benchmark.

| Check | Final result / local evidence |
| --- | --- |
| Unity C# compilation | No C# compiler errors in final Editor test/build logs; both player logs have no reported runtime exceptions |
| Independent rules | 36 assertions passed, including the existing 100 seeded runs; `dotnet run --project Tests/Rules/Rules.csproj` |
| EditMode | **12/12**; `artifacts/readability-final-EditMode.xml` and `.log` |
| PlayMode | **14/14**; `artifacts/readability-final-PlayMode.xml` and `.log`. Includes physical collision, full A/B loops, duplicate contact/release immunity, continuous slide/fall samples, presentation pause, reset cancellation, direct vs early-release tactics |
| Windows development build | **Success / exit 0**, official Unity CLI beta.13; `artifacts/readability-final-build.log` and `.provenance.json` |
| A Windows player | **PASS** full One Night, Prize miss/retry, two-drop Battle 2 reference and replay; `artifacts/readability-free-carry/result.txt` and `timeline.txt` |
| B Windows player | **PASS** full One Night, 1-turn early combo vs 2-turn direct route, 1-turn near-home direct capture and useful failed-contact capture; `artifacts/readability-after/result.txt`, `timeline.txt`, `input-results.jsonl` |
| Real rendered screen review | 1280×800 waiting, held Heavy, push-in-progress, chute fall and three-friend ending inspected. Fixed held/HP label overlap and moved HOME label beside the chute after the first rendered review |
| Human feel/fun, blind discoverability, normal OS input, audio comfort | **Unverified**. Sound is off in these silent recordings. No subjective score inferred from automation |

The first PlayMode run was 13/14: the proposed CASE A Knight at `(3,2)` intercepted some physical drops. Moving Knight off the flight line to `(4,3)` fixed that optional layout. The full final PlayMode rerun passed; rejected evidence remains locally under `artifacts/readability-first-PlayMode.*` and `artifacts/readability-diagnostic/`.

No connected Unity MCP tools were exposed. `unity status --json` returned no Pipeline instances and the project has no Pipeline dependency; no MCP connection is claimed. Used the existing installed Editor/CLI without engine, package or CLI upgrades. The CLI's update check reported beta.17 available; no update was needed. Scene regeneration changed local file IDs and a redundant legacy material color; those generated source diffs were restored rather than included as feature changes.

### Screens and short real gameplay clips

All pictures below are real Windows player captures. Before was re-recorded with the existing PR #4 development player while the checkout was clean at `667c764`; after uses the final build above. The waiting board is identical. The Heavy-case comparison intentionally has the documented Knight/HP changes, so it is not a controlled timing benchmark.

| Scene | Before | After |
| --- | --- | --- |
| Waiting / board framing | [Before](evidence/auto-return-readability/before-waiting.png) | [After](evidence/auto-return-readability/after-waiting.png) |
| Carry / held toy and HOME | [Before](evidence/auto-return-readability/before-return.png) | [After](evidence/auto-return-readability/after-return.png) |
| Impact / collection | [Immediate result](evidence/auto-return-readability/before-result.png) | [Pushing](evidence/auto-return-readability/after-pushing.png) · [Falling](evidence/auto-return-readability/after-falling.png) |
| Short silent gameplay | [Before, 3.8s](evidence/auto-return-readability/before-push.mp4) | [After, 4.3s](evidence/auto-return-readability/after-push.mp4) |
| Preserved ending | — | [Knight, Heavy and Slimy](evidence/auto-return-readability/after-ending.png) |

Full local recordings: `artifacts/readability-before.mp4` (36.8s), `artifacts/readability-after.mp4` (64.2s). Encoded from 731/1276 actual PNG frames with the development driver's 20fps simulation clock. Async screenshot writes omitted 5/7 numbered slots; `scripts/encode_capture.py` holds the preceding frame across those gaps rather than shortening time or inventing motion. This is not a real-time performance recording. Raw frames and timestamped selected screenshots remain in the corresponding capture folders.

Final built managed assembly SHA-256:

```text
CraneTactics.Runtime.dll 42591252F653E64C94BD02E555B690EC497579C8468008C9095E0F0C32F6280A
CraneTactics.Rules.dll   1B67ECB5DB32C7362A316F5B895AF7612BA0721A977363D514A713F969650073
```

### Reproduce

```powershell
dotnet run --project Tests/Rules/Rules.csproj
./scripts/verify.ps1 -Editor '<Unity 6000.3.9f1 Unity.exe>' -Mode EditMode
./scripts/verify.ps1 -Editor '<Unity 6000.3.9f1 Unity.exe>' -Mode PlayMode
unity build . --target StandaloneWindows64 --execute-method CraneTactics.Editor.BuildOneNight.BuildWindows --allow-dirty-build --no-tail --log-file artifacts/readability-final-build.log
./Builds/Windows/CraneTactics.exe --auto-output '<absolute B evidence directory>'
./Builds/Windows/CraneTactics.exe --smoke-output '<absolute A evidence directory>'
python scripts/encode_capture.py artifacts/readability-after artifacts/readability-after.mp4
```

Player capture requires a visible render surface. Build output remains local and ignored; it is not published/distributed. No merge was performed.

## Documentation differences and precedence

- `CODEX_HANDOFF.md` “Current evidence and target” describes historical documentation-only main. Actual PR #4 contains the Unity project; followed the verified checkout.
- `CODEX_HANDOFF.md` reliable grab/free carry/hold table and solid neighboring-cell displacement describe A. B keeps PR #4's existing grip and directional impact rules; no further rule change is introduced here.
- `AUTO_RETURN_EXPERIMENT.md` “Tactical cases” describes CASE A's former Knight `(1,4)` HP 3 and Heavy HP 3. This follow-up intentionally changes only that optional case, as specified above; original report remains historical evidence.
- `AUTO_RETURN_EXPERIMENT.md` target-snap limitation and reliable-direct-capture finding describe PR #4. This follow-up replaces B's snap presentation and adds measured optional choices; it does not retroactively change that report's conclusions.
- `CODEX_HANDOFF.md` “Implementation sequence” item 2 specifies a fixed three-quarter camera; the current request authorizes changing B's framing. A still follows the original behavior.
- `README.md` final handoff paragraph said the project/models were still to be made despite its opening Unity section. Followed the actual project and corrected that stale paragraph, adding links to this report and the B controls. Its clearly labeled early concept remains historical.
- `docs/art-direction/VISION.md` “Adopt” item 3 calls for a lower-left cluster. B uses a compact bottom strip instead to keep the enlarged board and crane clear; the current request permits practical camera/UI changes without exact image reproduction. Preserved the existing models and original source art; visual parity is not claimed.
