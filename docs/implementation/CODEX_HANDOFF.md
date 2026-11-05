# CRANE TACTICS — Unity / Blender implementation handoff

Updated: 2026-11-05. Repository: https://github.com/madowaku/crane-tactics

## Current evidence and target

At inspection, main (`a930882e1a7bbccb82eba51a094e3c58f6d36192`) contains only the original README. There are no Unity assets, Editor version settings, packages, scenes, or MCP configurations in that revision. This handoff adds documentation, not a runnable Unity project.

A separate Canvas 2.5D interaction study reached Study 03:
https://crane-tactics-one-night-lab.cacao-ixora-coccinea.chatgpt.site/
It is owner-private and requires ChatGPT sign-in. Its source is separate from this GitHub repository. Last published source commit: `049d917273a6e71bac40b72c245f81771d928292` in the Sites source repository; that SHA is not a commit in crane-tactics. Do not try fetching it from this repository.

The browser study verified carry/release, swing momentum, pit collection, hold timeout, friendly fire, slime push, battle/prize transitions and a two-drop Battle 2 route in Node simulations. Browser checks covered rendering and held ability text. These results do not establish Unity compilation, 3D physics, Blender export quality, or Windows build readiness.

Target: Unity 6.3 LTS / 3D URP / Windows first / mouse and keyboard. Use the installed compatible Editor patch and record it after project creation. The cloud handoff environment has no Unity Editor, Blender executable, C# compiler, or connected Unity/Blender MCP. A local Codex session with those applications is needed for live editing and validation.

## Experience to preserve

A closed arcade wakes at midnight. The player controls a huge claw above living miniature prizes. One crane action changes a tactical board. After winning, the player personally catches Slimy in another cabinet, then immediately uses the new friend in battle. End with Knight, Heavy and Slimy together. Aim for a 3–5 minute first play; this is an observation target, not a countdown.

Core sensation: "Release here and I win!" → a swing or collision changes the result → the new position remains → try a different next move.

The v0.1 claw auto-grabs reliably. Make transport, release, falling, impact and lasting board changes satisfying first. Full contact-based grasping, RPG progression, equipment and monetization are later decisions.

## Sources and precedence

1. This handoff records the Study 03 migration decisions below.
2. The full design draft supplies the broader flow, UI, recovery and QA requirements:
   [CRANE_TACTICS_ONE_NIGHT_PROTOTYPE_v0.1.md](https://chatgpt.com/library?file_id=libfile_34aa6810dea48191b8dc0cc25fe35a00&account_id=personal)
3. The README is the original concept, not the complete One Night specification.

The design draft and browser study differ. Use Study 03 Battle 2 placement below, not the earlier three-drop reference solution. Use the design draft's attack-first enemy planner described below; the browser used a simpler rotating actor. Revalidate tactics after implementing the shared Unity planner. The draft's 4×4 Prize board remains the Unity target, while the browser reused its larger board. Do not silently import all browser simplifications as final rules.

## Concrete rules

Coordinates below are zero-based `(x,y)` from the back-left of the board. Map to Unity X/Z; Unity Y is height. Store grid coordinates independently of world transforms.

| Unit | HP | Drop effect | Maximum carry |
| --- | ---: | --- | --- |
| Ring Knight | 3 | 1 damage | Unlimited |
| Heavy | 3 | 2 damage, including friendly fire | 5 seconds |
| Slimy | 2 | 0 damage; push target one cell | Unlimited |
| Scout | 1 | Solid enemy drop: 1 damage | 3 seconds |
| Guard | 2 | Solid enemy drop: 1 damage | 3 seconds |

Local prototype decision (2026-11-06 JST): the owner authorized balance changes during implementation. Battle 2 overrides Scout to **HP 2 / attack 2**; Battle 1 keeps Scout HP 1 / attack 1. This reconciles the two-drop reference below: Knight leaves Scout at 1 HP, Scout leaves Knight at 1 HP, then Heavy finishes. The original global HP 1 table would have ended Battle 2 after one Slimy drop. Default enemy attack is 1 otherwise.

Timeout releases at the current position. Start its timer when grasping completes. Pause suspends the timer. Allow grabbing either side. Grabbing alone consumes no action; release commits the action, including a miss.

Resolve in order: release → fall/settle → impact/pit → Knight adjacent attack → one enemy action → next selection. If victory/defeat occurs, transition once and cancel pending turn actions. Prevent repeated collision callbacks from dealing repeated damage.

Knight attacks one orthogonally adjacent enemy once after any player drop. Choose lowest HP, then stable coordinate order. An enemy action chooses an actor able to attack first, otherwise nearest to a living ally, with stable ID ties. Attack an adjacent ally with lowest HP, then stable ID. Otherwise step one orthogonal empty non-pit cell toward an ally; use Y before X ties. Share one planner between intent UI and execution. Recompute after landing and label the pre-release intent as provisional.

Pits remove allies and enemies in battle. In Prize, dropping Slimy into the pit recruits it. Walls rebound drops; only the visible pit allows removal. Settle by center position and snap to a cell. Resolve coordinate ties consistently. Never leave two living units in one cell. Solid impact damages first; a survivor is displaced to a valid neighboring cell. With no space, retain the target and put the dropper in the nearest empty cell.

Slime push direction uses dominant horizontal release velocity. Near rest, use pickup-cell → landed-cell displacement, X wins equal magnitudes, default right if both zero. Expose the near-rest threshold in world-units/second; do not copy the browser's grid-unit number without conversion. A wall or occupied push destination blocks the push. A pit destination removes the target. Put Slimy into the vacated cell on success, or the nearest empty cell if blocked. Keep the blocked-push behavior distinct from solid-unit displacement.

### Board data

Battle 1: 6×6. Knight `(1,4)`, Heavy `(1,3)`, Scout `(3,3)`, Guard `(4,1)`, pit `(5,2)`. Draft reference: Heavy onto Scout, then Guard into the pit. Confirm the intervening Guard action with the actual planner before baking the reference into tests.

Prize: 4×4. Slimy `(1,1)`, pit `(3,3)`. Unlimited attempts. A miss preserves its settled position. Optional stronger guide after two misses, optional guided transport after four; final release remains manual. Battle 2 retries keep recruitment and restore all starting HP.

Battle 2, Study 03: 6×6. Knight `(1,2)`, Heavy `(0,4)`, Slimy `(2,3)`, Scout `(1,1)`, Guard `(4,3)`, pit `(5,3)`.

Reference route: quietly carry Slimy from `(2,3)` onto Guard `(4,3)` → push Guard right into `(5,3)` → Knight damages adjacent Scout → Scout attacks Knight → Heavy onto Scout → win. In this route Knight has 1 HP after that enemy action. Enemy pickup and pit removal remain valid alternatives; do not force Slimy usage.

## Implementation sequence and dependencies

1. Inspect local repo, Unity Hub/Editor, render/input packages, Blender and MCP configuration. If no Unity project exists locally, create a Unity 6.3 LTS URP project in the repository root. If a local project exists but is uncommitted, inspect and preserve it. Keep Library, Temp, builds and machine-specific settings out of git. Record the actual environment in `docs/implementation/UnityProjectContext.md` only once verified.
2. Build a one-scene `OneNight` graybox: cabinet, readable 6×6/4×4 floor, visible chute, fixed three-quarter camera, lights and three distinct ally silhouettes. Establish flow state and board data before adding details.
3. Implement selection → automatic approach/grab → lift → carry → release → settle. Claw movement has finite speed. Use a controllable spring/pendulum while carrying; on release transfer visible horizontal velocity into the drop. Separate input from the crane controller. Click/Space releases; arrows/WASD provide movement alternatives. Show shadow and uncertain landing footprint.
4. Complete Battle 1, Prize, Team, Battle 2 and Ending with discrete rules, retries and enemy intent. Keep the entire loop playable before polishing individual models. Resolve a non-settling drop after at most two seconds; use a short quiet-velocity window then snap. Reset cancels all delayed effects and restores input coherently.
5. Replace graybox presentation with Blender-made cabinet, claw and figures. Preserve collider scale and gameplay anchors. Add weight, slime squash/stretch, collection and team reactions. Re-run gameplay after each asset replacement.
6. Validate in Editor and a Windows development build, then capture a complete first-play video and short success/miss/accident clips.

Suggested code boundaries: BoardDefinition/BoardState/BoardResolver, UnitProfile/UnitActor, CraneController/CarrySwing/DropResolver, BattleRoundController/EnemyIntentPlanner, PrizeCaptureController, OneNightFlowController, HUD/FeedbackFX. Prefer a small testable rule assembly with no scene dependency. Do not create an elaborate framework before the first complete playthrough.

## Unity MCP and Blender workflow

Discover installed bridges first. Use exactly one Unity MCP provider already present; inspect its current official/repository setup before adding or changing a package. The user wants MCP-assisted work, but no provider choice has been verified. Do not commit guessed package revisions, open unauthenticated network listeners, or claim a connection without a successful read-only probe. If Editor tools are unavailable, continue safe repository work and explicitly identify which live checks remain blocked.

For Blender, prefer the existing local MCP if it supports scene creation/export; otherwise use Blender's Python/background mode. Save reproducible source scripts and `.blend` sources under `ArtSource/`, exports under `Assets/_CraneTactics/Art/`. Use meters, apply transforms and validate a one-meter cube round trip before exporting the full cast. Check up-axis, forward, pivots, scale, normals and materials in Unity. Prefer exported FBX with documented options rather than requiring every checkout to import `.blend` through Blender automatically. Simple convex collision meshes are separate from detailed visual meshes. Keep claw origin, unit foot origin and grab anchor explicit.

Model identities: ivory/teal Ring Knight with gold overhead ring; chunky terracotta Heavy; translucent mint Slimy. Reference images are visual targets, not existing meshes. Make front/side silhouettes readable from the gameplay camera. First build a cohesive miniature cabinet with warm cream, muted teal and brass; the figures should feel like collectible toys.

Reference assets (owner Library; copy locally when accessible, then inspect before modeling):
- [Knight turnaround](https://chatgpt.com/library?file_id=libfile_6dc1413f3784819194d34d60e5e296f0&account_id=personal)
- [Heavy turnaround](https://chatgpt.com/library?file_id=libfile_b3cb01d45b3081919dcf425fdc261af5&account_id=personal)
- [Slimy deformation sheet](https://chatgpt.com/library?file_id=libfile_5057e5ad25dc819193d32e5a797dd32a&account_id=personal)
- [Cabinet reference](https://chatgpt.com/library?file_id=libfile_dc0d51bc804481919263e155b05003f4&account_id=personal)

If the local Codex cannot access these links, obtain the actual files from the user before claiming visual fidelity. Graybox work can continue meanwhile.

## Feedback and acceptance

Study 02 feedback to carry forward: Heavy's short 65 ms impact pause, low thud and expanding dust ring; nearby hops are cosmetic, not splash damage. Slime squashes at the floor then rebounds. Collection shrinks into the chute, plays a chime and waits for the animation before showing recruitment. Audio is opt-in; muting stops pending voices. Screen shake can be disabled and respects reduced-motion preference where available.

Study 03 readability: held ability/hold timer, remaining enemy objective, enemy intent, and Slimy push arrow. Keep these subordinate to the cabinet. No technical debugging text in player-facing flow.

Required validation:
- EditMode: damage, friendly fire, pit removal, blocked push, stable push direction, unique cells, deterministic planner ties, exactly one turn, reset/cancellation and victory precedence.
- PlayMode: raycast selection, grasp/lift, carry swing, momentum release, timeout, settling recovery and actual physics-driven Battle 2 reference route. Also run a deterministic quiet-drop rule test; report the two separately.
- Flow: Battle 1 → Prize miss/retry → GET → Team → Battle 2 → Ending → replay. Battle 2 defeat/retry preserves Slimy. Verify both direct collection and impact solutions.
- Visual: final gameplay camera screenshot, clipping/occlusion, floor vs figure shadow alignment, chute visibility and import scale.
- Editor: wait for compilation, inspect Console; report pre-existing vs introduced errors.
- Build: Windows development build boots and completes the loop. If the environment cannot build/run it, label it unverified.

Record the exact commands/tools, Editor version, test totals, screenshots and remaining failures in the PR. A generated scene or source file alone is not proof that the game runs.

## First local Codex task

> Read AGENTS.md and docs/implementation/CODEX_HANDOFF.md in madowaku/crane-tactics. Inspect my actual local Unity/Blender/MCP environment, preserve existing work, and start the Unity 3D One Night vertical slice on a feature branch. Use the existing Unity MCP and Blender tools when available; prove connectivity first. Complete a playable graybox grab/carry/drop loop in the cabinet, then connect Battle 1 → Prize recruitment → Battle 2 → Ending. Follow the Study 03 Battle 2 layout and explicit board rules. Validate compilation, rule tests and Play Mode; report unavailable checks truthfully. Continue autonomously through reversible implementation and fixes. Create a reviewable PR; do not merge or distribute a build without my request.
