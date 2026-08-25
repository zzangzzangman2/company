"""Run the shared donor-preserving builder in Proof13 silhouette-gate mode."""

from __future__ import annotations

import runpy
import sys
from pathlib import Path


SCRIPT = Path(__file__).with_name("build_player_proof10_topology_gate.py")
if "--" not in sys.argv:
    sys.argv.extend(
        [
            "--",
            "--version",
            "13",
            "--output",
            "Artifacts/Family3DPlayerHumanV5/Proof13SilhouetteGate",
        ]
    )
else:
    separator = sys.argv.index("--")
    forwarded = sys.argv[separator + 1 :]
    if "--version" not in forwarded:
        sys.argv.extend(["--version", "13"])
    if "--output" not in forwarded:
        sys.argv.extend(
            ["--output", "Artifacts/Family3DPlayerHumanV5/Proof13SilhouetteGate"]
        )

runpy.run_path(str(SCRIPT), run_name="__main__")
