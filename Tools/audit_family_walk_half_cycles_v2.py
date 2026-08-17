#!/usr/bin/env python3
"""Read-only audit for the four-family, eight-direction, six-pose walk set.

The retired audit projected phases 3..5 by mirroring another direction and
rewarded a 30 percent alpha-silhouette jump. That contract caused the visible
mid-stride body reversal. The canonical builder now owns the complete gait
contract, so this wrapper delegates to that single source of truth and never
synthesizes a projected frame.
"""

from __future__ import annotations

import argparse

import build_family_walk_half_cycles_v2 as build


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--strict-current",
        action="store_true",
        help="return nonzero when any tracked six-pose source row fails",
    )
    args = parser.parse_args()

    failures = build.validate_sources()
    print(
        f"SUMMARY rows={len(build.CHARACTERS) * len(build.DIRECTIONS)} "
        f"failures={len(failures)} contract=v5-two-step-tile-facing"
    )
    if failures:
        print("FAILURES " + ", ".join(failures))
    return 1 if args.strict_current and failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
