# Arcade cabinets

Branch `claude/arcade-cabinets`. One Night (Battle 1 → Prize → Battle 2 → three friends) keeps its handoff boards, HP and rules. Everything below is added around that loop. Normal play now starts on the cabinet's lit buttons (mode B in code, historically "auto return"); mouse free carry stays under pause → LAB.

**2026-11-08 change, after the first playtest feedback.** The claw used to carry every toy home along a fixed L-shaped return route, and a toy could only be let go on that line. That meant an ally could not be put where you wanted it, and the tactics disappeared. Now the claw stops after the grab and waits: the player carries the toy anywhere with the four direction buttons, one axis at a time, and lets go with DROP. The grip timer is the carry budget.

## What makes it Crane Tactics

**Your grip is your range.** A held toy can go anywhere within the steps its grip allows, shown as gold dots under the board. Every tactic is "where do I put this toy, which side do I bring it in from, and what will the floor do after I let go". Allies hold on long enough to cross the board; rivals in new cabinets carry a short grip (`GripScale`), so only a rival already near the chute can be carried home. That and a Trick Shot medal for winning without carrying one home answer the earlier finding that carrying rivals home dominated.

| Cabinet | Floor | New toy | Feel |
| --- | --- | --- | --- |
| CLASSIC | Plain | Slimy (push 1) | The original One Night |
| SWEETS | Icing slides a toy until stopped. Jelly launches an unobstructed drop +2 cells | **Koromochi**: narrow grip, 1.9s at best; rolls until something stops it, then pushes | Loose arm, wide swing, faster claw |
| MAGNET LAB | Magnets pull metal (Knight, Guard, Clockwork) one cell per turn, reach 3, blocked by toys, and clamp it | **Clockwork Robot**: dashes where it was dropped when its key runs down, 2 damage to anyone; ally or rival | Stiff arm, straight lines, slower claw |
| GUM FACTORY | Belts move riders one cell each turn, into the chute too. A toy in gum cannot act | **Petapetan**: paints gum where it lands; immune to it | Enemies blunder into gum; belts do the work |
| JACKPOT | All of the above | Every friend, hostile Clockwork | Finale; carrying rivals home loses |

Turn order extends the handoff's: release → fall/settle → impact/pit → terrain slide → Knight (unless stuck) → magnets → belts → clockworks → one enemy action. Each phase is a recorded `ResolutionBeat`; playback never calls rules back.

### Buttons

The player-facing controls are the cabinet's own lit domes, left to right **←  ↓  1 →  2 ↑  DROP** (cyan across, amber in depth). Only buttons that do something glow and pulse; they sink when pressed. Hold 1, let go to lock; hold 2, let go to descend. While a toy is held, all four direction domes light: hold one to carry along that axis, let go to stop, and press DROP (or Space) to release. Arrow keys and WASD steer too. A click on the board never releases. The grip running out drops the toy where it is. Every menu button uses the same lit-pill treatment. `ArcadeDeck.cs` owns layout and round hit-testing.

The ring now forecasts flight plus floor skid (`LandingForecast`), and spatial arrows show rival intent, clockwork dash lines, magnet pulls and belt moves using the same planners as execution.

## Tactical validation

`BoardSolver` searches the actual `BoardState` using the same legal claw actions as play: any landing within the toy's reach (Manhattan steps, 90% of a best grip times the cabinet's carry speed), dropped still or on the move from any side the reach allows, plus nudges. `HomeRush` is a naive player who always carries the nearest rival as close to the chute as its grip allows. Search is exhaustive to depth 3; deeper pars are shown by a found line, and the reference lines in `BoardSolver.ReferenceLines` are replayed by the tests.

```powershell
dotnet run -c Release --project Tests/Rules/Rules.csproj -- --report 0 1 2 3 4 5 6 7 8 9
```

| Stage | Par (true fewest) | Winning first moves | Home rush | Random win |
| --- | ---: | ---: | --- | ---: |
| 0 One Night battle 1 (unchanged board) | 2 | 42 | Victory in 2 | 59% |
| 2 One Night battle 2 (unchanged board) | 2 | 111 | Victory in 2 | 35% |
| 4 SWEETS battle | 2 | 18 | Victory in 6 | 20% |
| 6 MAGNET LAB battle | 3 | 75 | Victory in 6 | 7% |
| 8 GUM FACTORY battle | 3 | 2 | Victory in 10 | 1% |
| 9 JACKPOT | 4 | (not exhaustive) | **Defeat** | 2% |

Free carry made two boards too easy, so they were retuned: SWEETS rivals' grip went from 0.3 to 0.25 (a rival could be thrown into the other one and both slid home on the icing, a one-drop win), and GUM FACTORY's par is now 3 instead of 4. Each battle above also has a trick-shot line at par. On the unchanged One Night boards, carrying rivals home is as fast as par; those boards follow the handoff and were not retuned. Prizes 1, 3, 5 and 7 are par 1 with unlimited tries. Solver figures measure the discrete board, not how a person plays; difficulty and fun remain human questions.

## Verification

Free carry re-verified locally on **2026-11-08 JST** (the first pass on 2026-11-07 covered the return-route version), Unity **6000.3.9f1 (`7a9955a4f2fa`)** / URP, on the working tree based on `5125bda`, via the official Unity CLI 1.0.0-beta.13 in batch mode. Automated input is not a human playtest.

| Check | Result |
| --- | --- |
| C# compilation | No `error CS` in the final build log; no exceptions in either player log |
| Independent rules | 36 assertions passed, unchanged; `dotnet run --project Tests/Rules/Rules.csproj` |
| Solver report (.NET) | Pars and table above; exhaustive to depth 3, a few seconds per stage, JACKPOT 33 s |
| EditMode | **33/33**: reference lines win at par with legal moves only, no shorter line (par ≤ 3), home rush slower, free-carry reach, five non-overlapping domes |
| PlayMode | **26/26**: stages 3 to 9 reach par through the real claw, carried with the direction controls; an ally carried away from the chute and placed; Input System mouse events: the claw waits after a grab, holding ← carries left, DROP releases once |
| Windows development build | Success, exit 0, no `error CS` |
| B player (default) | **PASS**, 73 timeline checks, 0 failures: full One Night carried home by steering, tactical cases, stages 3 to 9 at par, every planned landing matched |
| A player (mouse free carry) | **PASS**: full One Night with Prize miss/retry, two-drop Battle 2 and replay |
| Rendered screens | 1280×800 captures reviewed; see `evidence/arcade/`. Fixed after review: reversed belt arrows, moon over poster, clipped button label, unreadable grip color, sparse floor cards |
| Independent code review | `codex exec` read-only review found two bugs, both fixed: physics running behind the floor menu when opened from pause, and a stuck Knight still attacking |

No Unity MCP tool was exposed. `unity status` reports no Pipeline instance and `com.unity.pipeline` was not added, so no live Editor or hierarchy probe is claimed. Generated scene IDs and `RouteGuide.mat` color rewrites from the build step were restored, not included.

Evidence stays local under `artifacts/` (ignored): `free-carry-b/` screens, timeline and `input-results.jsonl`, `free-carry-a/`, `free-carry-*.xml`, `free-carry-build.log`.

Real-physics caveat: the automated player steers one axis at a time and releases by reading the ring, waiting for the swing to settle first. Without that wait, a leftover swing from the previous leg skewed a JACKPOT throw once and the grip ran out; a person will meet the same thing. Par is reachable, not guaranteed on any one try, and worth judging by hand.

## Art provenance

- `ArtSource/Generated/`: original images from `codex exec -m gpt-6-astra` native image generation, prompts in `PROMPTS.md`, stored as WebP quality 90 at full resolution (2.5 MiB instead of 30.8 MiB of PNG; regenerated runtime textures differ by at most 2.7/255 mean per channel). `scripts/process_generated_art.py` derives the runtime textures in `Resources/UI`, `Terrain` and `Marquee` (circular cut-out and downscale only).
- Koromochi, Petapetan and Clockwork are procedural Blender study toys appended to `ArtSource/create_toys.py`, with independent box colliders at runtime. Marquee illustrations are visual targets for these toys, and the 3D toys are simpler than the posters.
- No third-party or marketplace assets. No text is baked into the art.

## Not done

Human playtest, audio listening, FPS budget and normal OS mouse use in the player are unverified. Mouse coverage is Input System events in PlayMode, and player runs use programmatic input. UI text is English. `steel.png` was generated but is unused. The Unity CLI reports beta.17 available; beta.13 was used and not upgraded.
