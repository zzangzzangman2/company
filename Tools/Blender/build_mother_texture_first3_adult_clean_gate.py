"""Run the owned-Mika texture-first builder in AdultClean3 audit mode."""

from pathlib import Path
import runpy
import sys


if "--adult-clean3" not in sys.argv:
    sys.argv.append("--adult-clean3")

runpy.run_path(
    str(Path(__file__).with_name("build_mother_texture_first1.py")),
    run_name="__main__",
)
