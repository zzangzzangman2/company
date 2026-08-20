[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
throw @'
PLAYER_WALK_HUMANOID_PIPELINE_DISABLED:
The 3D humanoid protagonist pipeline was rejected and cannot bake, promote, build, or run.
There is no override switch. Continue the source-only east 6-pose workflow documented in
Docs/HOME_PC_WALK_CHECKPOINT_2026-08-20.md.
'@
