"""Run the owned-Mika texture-first builder in polished source-surface mode."""

from pathlib import Path
import runpy
import sys


if "--polished2" not in sys.argv:
    sys.argv.append("--polished2")

runpy.run_path(
    str(Path(__file__).with_name("build_mother_texture_first1.py")),
    run_name="__main__",
)
