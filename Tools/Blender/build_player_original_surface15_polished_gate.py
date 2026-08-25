"""Run the donor-preserving original-surface builder in Player15 gate mode."""

from __future__ import annotations

import runpy
import sys
from pathlib import Path


SCRIPT = Path(__file__).with_name("build_sister_proof11_original_surface_style.py")
REPO = Path(__file__).resolve().parents[2]
SOURCE_DIR = (
    REPO
    / "Artifacts"
    / "ExternalReferenceStudy"
    / "UserProvided_BlueArchive_OriginalMeshes_2026-08-24"
    / "Yuuka"
    / "Yuuka_Original_Mesh"
)
OUTPUT = REPO / "Artifacts" / "Family3DPlayerHumanV5" / "PlayerOriginalSurface15PolishedGate"

sys.argv = [
    str(SCRIPT),
    "--",
    "--input",
    str(SOURCE_DIR / "Yuuka_Original_Mesh.fbx"),
    "--texture-dir",
    str(SOURCE_DIR),
    "--output",
    str(OUTPUT),
    "--style",
    "player_original15",
]
runpy.run_path(str(SCRIPT), run_name="__main__")
