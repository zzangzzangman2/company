#!/usr/bin/env python3
"""Measure animation-loop coherence for every character, discovered automatically.

Why this exists
---------------
A walk cycle reads as smooth when consecutive frames are *small edits* of each
other: the legs and arms move, the head/torso/outline stay put. When every
frame is an independent drawing instead, the character boils and shimmers, and
adding more frames makes it worse rather than better - the eye starts expecting
in-between poses and never gets them.

That failure is invisible to frame counting and to the existing QA sheets, so
it needs its own metric.

    adjacent  frame i vs frame i+1            should be SMALL
    opposite  frame i vs frame i+n/2          the largest legitimate change
    ratio     median(adjacent) / median(opposite)

For a real cycle the ratio is well below 1: one frame step covers a fraction of
what half a cycle covers. A ratio near 1 means the loop has no in-betweens - it
is a set of unrelated poses being flipped through.

Baselines measured on this project (2026-08-13, before any walk fix):

    mother seated mouse/typing,   median 41.7 / 47.1%   worst 56.8 / 58.4%
      stabilised by
      Tools/stabilize_mother_work_frames.py
    father / sister / player      median 46.1 - 56.6%   worst 65.5 - 84.4%
      seated, untreated
    every walk cycle, all 12      median 58.5 - 78.1%   worst 70.4 - 92.7%
      characters, untreated       ratio  0.97 - 1.16  <- no in-betweens at all

Two things to read out of that. The stabilisation technique's real win is the
*worst* column, not the median: it caps the single ugliest frame pair in a loop,
and that pair is what the eye catches. And every walk cycle sits at ratio ~1.0
while the seated drink loops reach 0.59 - 0.73, so the metric does discriminate
- walk is genuinely the broken one.

The gates are set at what the stabilised frames already achieve, so they are a
ratchet against the current best in the repo rather than an aesthetic ideal.
Tighten them as the art improves.

The ratio gate applies to locomotion only. A micro-action loop pins the body on
purpose and varies one small region, so adjacent and opposite frames differ by
similar amounts by design and a ratio near 1.0 is correct there, not a defect.

Characters are found by scanning for the folder layout rather than from a list,
so a new employee is covered the moment their frames land - unlike
Tools/build_high_motion_qa.py, which carries a hardcoded CHARACTERS tuple.

    python Tools/measure_animation_coherence.py
    python Tools/measure_animation_coherence.py --strict   # exit 1 on failure

Reads only. Writes nothing except the report under Artifacts/.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
from PIL import Image

# A pixel counts as opaque, or as changed, above these 0-255 values. Low enough
# to catch anti-aliased outline shifts, high enough to ignore encoder noise.
ALPHA_MIN = 8
DIFF_MIN = 12

# Gates. See the baseline table above for where these come from.
MAX_ADJACENT_MEDIAN = 45.0
MAX_ADJACENT_WORST = 60.0
MAX_ADJACENCY_RATIO = 0.70

# Motions that are a travelling cycle and must therefore have in-between poses.
# Everything else is a micro-action loop and is exempt from the ratio gate.
LOCOMOTION_MOTIONS = frozenset({"walk", "run"})

# Informational only - reported, never gated. The spread between family members
# is real but small, and a hard gate here would fail existing art for a defect
# the eye barely registers next to the ratio problem.
FOOT_DRIFT_NOTE_PX = 1
SLIDE_NOTE_PX = 5

WALK_RE = re.compile(r"^(?P<cid>[a-z0-9_]+?)_(?P<dir>[a-z]+)_walk_(?P<idx>\d+)\.png$")
WORK_RE = re.compile(
    r"^(?P<cid>[a-z0-9_]+?)_(?P<action>[a-z]+)_(?P<idx>\d+)_(?P<dir>[a-z]+)_v\d+\.png$"
)


@dataclass
class Loop:
    """One animation cycle: a character, a motion, a facing."""

    character: str
    motion: str
    facing: str
    paths: list[Path]

    adjacent: list[float] = field(default_factory=list)
    opposite: list[float] = field(default_factory=list)
    foot_drift: int = 0
    slide: float = 0.0

    @property
    def frames(self) -> int:
        return len(self.paths)

    @property
    def adjacent_median(self) -> float:
        return float(np.median(self.adjacent)) if self.adjacent else float("nan")

    @property
    def adjacent_worst(self) -> float:
        return max(self.adjacent) if self.adjacent else float("nan")

    @property
    def ratio(self) -> float:
        """median adjacent change over median opposite-phase change."""
        if not self.adjacent or not self.opposite:
            return float("nan")
        opp = float(np.median(self.opposite))
        if opp <= 0.0:
            return float("nan")
        return self.adjacent_median / opp

    def failures(self) -> list[str]:
        bad = []
        if self.adjacent_median > MAX_ADJACENT_MEDIAN:
            bad.append(f"median {self.adjacent_median:.1f}% > {MAX_ADJACENT_MEDIAN:.0f}%")
        if self.adjacent_worst > MAX_ADJACENT_WORST:
            bad.append(f"worst {self.adjacent_worst:.1f}% > {MAX_ADJACENT_WORST:.0f}%")
        # A 2-frame loop has no in-between to measure; its ratio is 1 by
        # definition and must not be treated as a defect.
        if (self.motion in LOCOMOTION_MOTIONS
                and self.frames > 2
                and self.ratio == self.ratio
                and self.ratio > MAX_ADJACENCY_RATIO):
            bad.append(f"no in-betweens (ratio {self.ratio:.2f} > {MAX_ADJACENCY_RATIO:.2f})")
        return bad


def load_rgba(path: Path) -> np.ndarray:
    with Image.open(path) as im:
        return np.asarray(im.convert("RGBA"), dtype=np.int16)


def opaque_mask(frame: np.ndarray) -> np.ndarray:
    return frame[:, :, 3] >= ALPHA_MIN


def change_percent(a: np.ndarray, b: np.ndarray) -> float:
    """Share of the subject whose pixels differ between two frames.

    Normalised by the union of both subjects, not by either one alone: when the
    silhouette moves, the changed pixels cover both the old and the new
    position, and dividing by a single frame's area would push the result past
    100% and make the scale meaningless.
    """
    delta = np.abs(a - b)
    changed = (delta[:, :, :3].max(axis=2) >= DIFF_MIN) | (delta[:, :, 3] >= DIFF_MIN)
    union = int((opaque_mask(a) | opaque_mask(b)).sum())
    if union <= 0:
        return 0.0
    return float(changed.sum()) / union * 100.0


def subject_box(frame: np.ndarray) -> tuple[int, int, int, int] | None:
    mask = opaque_mask(frame)
    rows = np.flatnonzero(mask.any(axis=1))
    cols = np.flatnonzero(mask.any(axis=0))
    if rows.size == 0 or cols.size == 0:
        return None
    return int(cols[0]), int(rows[0]), int(cols[-1]), int(rows[-1])


def measure(loop: Loop) -> Loop:
    frames = [load_rgba(p) for p in loop.paths]
    count = len(frames)

    for i in range(count):
        loop.adjacent.append(change_percent(frames[i], frames[(i + 1) % count]))
    if count > 2:
        half = count // 2
        for i in range(count):
            loop.opposite.append(change_percent(frames[i], frames[(i + half) % count]))

    bottoms, centres = [], []
    for frame in frames:
        box = subject_box(frame)
        if box is None:
            continue
        left, _, right, bottom = box
        bottoms.append(bottom)
        centres.append((left + right) / 2.0)
    if bottoms:
        loop.foot_drift = max(bottoms) - min(bottoms)
    if centres:
        loop.slide = max(centres) - min(centres)
    return loop


def discover(art_root: Path) -> list[Loop]:
    """Find every animation loop by folder layout, with no character registry."""
    buckets: dict[tuple[str, str, str], list[tuple[int, Path]]] = defaultdict(list)

    for frames_dir in sorted(art_root.rglob("Pixel/HighMotion/Frames")):
        for png in sorted(frames_dir.glob("*.png")):
            m = WALK_RE.match(png.name)
            if m:
                key = (m["cid"], "walk", m["dir"])
                buckets[key].append((int(m["idx"]), png))

    for action_dir in sorted(art_root.rglob("Pixel/OfficeWorkActionsV1/Frames/*")):
        if not action_dir.is_dir():
            continue
        for png in sorted(action_dir.glob("*.png")):
            m = WORK_RE.match(png.name)
            if m:
                key = (m["cid"], m["action"], m["dir"])
                buckets[key].append((int(m["idx"]), png))

    loops = []
    for (cid, motion, facing), entries in sorted(buckets.items()):
        ordered = [p for _, p in sorted(entries)]
        if len(ordered) >= 2:
            loops.append(Loop(cid, motion, facing, ordered))
    return loops


def render_report(loops: list[Loop]) -> str:
    lines: list[str] = []
    w = lines.append

    w("ANIMATION COHERENCE")
    w("")
    w(f"gates: adjacent median <= {MAX_ADJACENT_MEDIAN:.0f}%   "
      f"worst <= {MAX_ADJACENT_WORST:.0f}%   ratio <= {MAX_ADJACENCY_RATIO:.2f}")
    w("ratio = median(adjacent change) / median(opposite-phase change).")
    w("Near 1.00 means one frame step changes as much as half a cycle should,")
    w("so the loop has no in-between poses and reads as flicker, not motion.")
    w("")

    by_motion: dict[str, list[Loop]] = defaultdict(list)
    for loop in loops:
        by_motion[loop.motion].append(loop)

    for motion in sorted(by_motion):
        group = by_motion[motion]
        w("=" * 88)
        w(f"{motion.upper()}   ({len(group)} loops)")
        w("=" * 88)
        w(f"{'character':<14}{'facing':<11}{'n':>3}{'median':>9}{'worst':>8}"
          f"{'ratio':>8}{'footDrift':>11}{'slide':>7}  verdict")

        per_character: dict[str, list[Loop]] = defaultdict(list)
        for loop in group:
            per_character[loop.character].append(loop)

        for character in sorted(per_character):
            for loop in sorted(per_character[character], key=lambda x: x.facing):
                bad = loop.failures()
                verdict = "FAIL: " + "; ".join(bad) if bad else "ok"
                ratio = "  n/a" if loop.ratio != loop.ratio else f"{loop.ratio:.2f}"
                w(f"{loop.character:<14}{loop.facing:<11}{loop.frames:>3}"
                  f"{loop.adjacent_median:>8.1f}%{loop.adjacent_worst:>7.1f}%"
                  f"{ratio:>8}{loop.foot_drift:>11}{loop.slide:>7.1f}  {verdict}")
            w("-" * 88)

    w("")
    w("=" * 88)
    w("PER CHARACTER SUMMARY")
    w("=" * 88)
    w(f"{'character':<14}{'motion':<9}{'loops':>6}{'median':>9}{'worst':>8}"
      f"{'ratio':>8}{'failing':>9}")

    grouped: dict[tuple[str, str], list[Loop]] = defaultdict(list)
    for loop in loops:
        grouped[(loop.character, loop.motion)].append(loop)

    for (character, motion), group in sorted(grouped.items()):
        medians = [x.adjacent_median for x in group]
        worsts = [x.adjacent_worst for x in group]
        ratios = [x.ratio for x in group if x.ratio == x.ratio]
        failing = sum(1 for x in group if x.failures())
        ratio = f"{float(np.median(ratios)):.2f}" if ratios else "  n/a"
        w(f"{character:<14}{motion:<9}{len(group):>6}{float(np.median(medians)):>8.1f}%"
          f"{max(worsts):>7.1f}%{ratio:>8}{failing:>4}/{len(group):<4}")

    failed = [x for x in loops if x.failures()]
    w("")
    w("=" * 88)
    if failed:
        by_char = defaultdict(int)
        for loop in failed:
            by_char[loop.character] += 1
        w(f"FAIL  {len(failed)} of {len(loops)} loops")
        w("      " + ", ".join(f"{c} x{n}" for c, n in sorted(by_char.items())))
        w("")
        w("Reordering frames does not fix a bad ratio and neither does animator")
        w("tuning - the poses themselves have no continuity. The remedy that")
        w("already works in this repo is the canonical-body technique in")
        w("Tools/stabilize_mother_work_frames.py: pin one frame as the body and")
        w("let later frames contribute pixels only inside the moving-limb region,")
        w("after integer registration. Generalising that to walk cycles for every")
        w("character is what this gate is waiting for.")
    else:
        w(f"PASS  all {len(loops)} loops")
    w("=" * 88)
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--repo-root", type=Path,
                       default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path, default=None,
                       help="report path (default Artifacts/AnimationCoherence/)")
    parser.add_argument("--motion", action="append", default=None,
                       help="limit to a motion, e.g. --motion walk")
    parser.add_argument("--strict", action="store_true",
                       help="exit 1 when any loop fails a gate")
    args = parser.parse_args()

    art_root = args.repo_root / "Assets" / "Art" / "Characters"
    if not art_root.is_dir():
        print(f"character art root not found: {art_root}", file=sys.stderr)
        return 2

    loops = discover(art_root)
    if args.motion:
        wanted = {m.lower() for m in args.motion}
        loops = [x for x in loops if x.motion in wanted]
    if not loops:
        print("no animation loops discovered", file=sys.stderr)
        return 2

    print(f"measuring {len(loops)} loops from {art_root}", file=sys.stderr)
    for index, loop in enumerate(loops, start=1):
        measure(loop)
        if index % 20 == 0:
            print(f"  {index}/{len(loops)}", file=sys.stderr)

    report = render_report(loops)
    print(report)

    out = args.output or (args.repo_root / "Artifacts" / "AnimationCoherence"
                          / "animation-coherence.txt")
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(report + "\n", encoding="utf-8")

    summary = [
        {
            "character": x.character,
            "motion": x.motion,
            "facing": x.facing,
            "frames": x.frames,
            "adjacentMedian": round(x.adjacent_median, 2),
            "adjacentWorst": round(x.adjacent_worst, 2),
            "ratio": None if x.ratio != x.ratio else round(x.ratio, 3),
            "footDriftPx": x.foot_drift,
            "slidePx": round(x.slide, 2),
            "failures": x.failures(),
        }
        for x in loops
    ]
    out.with_suffix(".json").write_text(
        json.dumps({"gates": {"adjacentMedian": MAX_ADJACENT_MEDIAN,
                              "adjacentWorst": MAX_ADJACENT_WORST,
                              "adjacencyRatio": MAX_ADJACENCY_RATIO},
                    "loops": summary}, indent=2),
        encoding="utf-8")
    print(f"\nreport: {out}", file=sys.stderr)

    failed = [x for x in loops if x.failures()]
    if failed and args.strict:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
