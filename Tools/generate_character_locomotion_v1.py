#!/usr/bin/env python3
"""Canonical CLI for the four-family, eight-direction, six-phase locomotion rig.

Default execution is non-destructive and writes review frames, anatomy markers, quantitative
manifests, contact sheets and fixed-floor GIFs under Artifacts. Publishing is an explicit second
step and preserves every existing Unity PNG .meta/GUID.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import build_family_locomotion_rig_v1 as family_rig


DEFAULT_OUTPUT = family_rig.OUTPUT


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--write", action="store_true", help="Build and publish the candidate.")
    parser.add_argument(
        "--publish-existing", action="store_true",
        help="Publish a previously generated and visually reviewed candidate without rebuilding it.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    output = args.output.resolve()
    if not args.publish_existing:
        family_rig.build_candidate(output)
    if args.write or args.publish_existing:
        family_rig.publish(output)
    print(f"CHARACTER_LOCOMOTION_GENERATION_V1: CANDIDATE_READY output={output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
