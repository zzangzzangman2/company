"""Run the MotherRetopo4Style branch of the shared donor-retopo builder."""

from pathlib import Path
import runpy
import sys


if "--style4" not in sys.argv:
    sys.argv.append("--style4")

runpy.run_path(
    str(Path(__file__).with_name("build_mother_retopo3.py")),
    run_name="__main__",
)
