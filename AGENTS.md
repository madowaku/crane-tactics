# CRANE TACTICS

Read `docs/implementation/CODEX_HANDOFF.md` before implementation. It carries the current One Night experience beyond the original README seed.

- Preserve the complete loop: Battle 1 → earn Slimy in Prize Chance → Battle 2 → three friends in the ending.
- Inspect the actual local Unity project and available tools first. Record the exact Editor patch version; do not assume a project or MCP exists.
- Use one existing Unity MCP provider when available. Prove connection with a read-only hierarchy/console request before editing. Keep credentials and machine-specific paths out of git.
- Keep discrete board rules separate from carry/drop presentation. One live unit per cell; release consumes exactly one action; resolve each turn once.
- Preserve original art references and the identities of Knight, Heavy and Slimy. Blender visuals must have separate collision and presentation geometry.
- Make changes on a feature branch. Report compile, tests, Play Mode and build results separately, including any checks that could not run. Do not merge or publish builds without a user request.
