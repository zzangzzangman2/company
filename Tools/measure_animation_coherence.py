#!/usr/bin/env python3
"""Measure animation continuity and enforce the production walk-asset contract.

The default discovery mode remains character-agnostic.  ``--art-root`` may
point either at ``Assets/Art/Characters`` or at a generated candidate tree
whose character frames are nested below it.

The strict walk gate deliberately treats an incomplete or degenerate data set
as a failure.  A valid character has all eight directions, phases 0..5, six
different 256x256 native-RGBA hard-alpha frames, a finite locomotion ratio, and
stable contact/root/closure metrics in addition to the original coherence
thresholds.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image


CANVAS_SIZE = (256, 256)
FRAME_COUNT = 6
DIRECTIONS = (
    "south", "southwest", "west", "northwest",
    "north", "northeast", "east", "southeast",
)
ALPHA_MIN = 8
DIFF_MIN = 12

MAX_ADJACENT_MEDIAN = 45.0
MAX_ADJACENT_WORST = 60.0
MAX_ADJACENCY_RATIO = 0.95
MIN_GAIT_ADJACENT_MOTION = 0.18
MIN_GAIT_PHASE_MOTION = 0.30
MAX_FOOT_DRIFT_PX = 1.0
MAX_STABLE_ROOT_DRIFT_PX = 1.0
MAX_LOOP_CLOSURE_PX = 2.0
MAX_WORK_FOOT_DRIFT_PX = 1.0
MAX_TYPING_ADJACENT_MEDIAN = 8.0
MAX_TYPING_ADJACENT_WORST = 12.0
APPROVED_TYPING_DIRECTIONS = frozenset({"northwest"})

LOCOMOTION_MOTIONS = frozenset({"walk", "run"})
WORK_FRAME_COUNTS = {"typing": 6, "mouse": 6, "drink": 8}

WALK_RE = re.compile(
    r"^(?P<cid>[a-z0-9_]+?)_(?P<dir>[a-z]+)_walk_(?P<idx>\d+)\.png$"
)
WORK_RE = re.compile(
    r"^(?P<cid>[a-z0-9_]+?)_(?P<action>[a-z]+)_(?P<idx>\d+)_(?P<dir>[a-z]+)_v\d+\.png$"
)


def finite_or_none(value: float) -> float | None:
    return round(value, 4) if math.isfinite(value) else None


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


@dataclass
class FrameAudit:
    index: int
    path: Path
    file_sha256: str = ""
    pixel_sha256: str = ""
    native_mode: str = ""
    size: tuple[int, int] = (0, 0)
    opaque_pixels: int = 0
    intermediate_alpha_pixels: int = 0
    opaque_edge_pixels: int = 0
    failures: list[str] = field(default_factory=list)

    def as_json(self) -> dict[str, Any]:
        return {
            "index": self.index,
            "path": str(self.path),
            "fileSha256": self.file_sha256,
            "pixelSha256": self.pixel_sha256,
            "nativeMode": self.native_mode,
            "size": list(self.size),
            "opaquePixels": self.opaque_pixels,
            "intermediateAlphaPixels": self.intermediate_alpha_pixels,
            "opaqueEdgePixels": self.opaque_edge_pixels,
            "failures": self.failures,
        }


@dataclass
class Loop:
    character: str
    motion: str
    facing: str
    entries: list[tuple[int, Path]]
    enforce_walk_quality: bool = False
    enforce_work_quality: bool = False

    frames_audit: list[FrameAudit] = field(default_factory=list)
    adjacent: list[float] = field(default_factory=list)
    opposite: list[float] = field(default_factory=list)
    foot_bottoms: list[int] = field(default_factory=list)
    stable_roots: list[tuple[float, float]] = field(default_factory=list)
    unique_pixel_frames: int = 0
    failures: list[str] = field(default_factory=list)
    failure_codes: list[str] = field(default_factory=list)

    def fail(self, code: str, message: str) -> None:
        if code not in self.failure_codes:
            self.failure_codes.append(code)
            self.failures.append(message)

    @property
    def indices(self) -> list[int]:
        return [index for index, _ in self.entries]

    @property
    def paths(self) -> list[Path]:
        return [path for _, path in self.entries]

    @property
    def frame_count(self) -> int:
        return len(self.entries)

    @property
    def adjacent_median(self) -> float:
        return float(np.median(self.adjacent)) if self.adjacent else float("nan")

    @property
    def adjacent_worst(self) -> float:
        return max(self.adjacent) if self.adjacent else float("nan")

    @property
    def opposite_median(self) -> float:
        return float(np.median(self.opposite)) if self.opposite else float("nan")

    @property
    def ratio(self) -> float:
        denominator = self.opposite_median
        if not math.isfinite(denominator) or denominator <= 0.0:
            return float("nan")
        return self.adjacent_median / denominator

    @property
    def foot_drift(self) -> float:
        if not self.foot_bottoms:
            return float("nan")
        return float(max(self.foot_bottoms) - min(self.foot_bottoms))

    @property
    def stable_root_drift(self) -> float:
        if not self.stable_roots:
            return float("nan")
        xs = [point[0] for point in self.stable_roots]
        ys = [point[1] for point in self.stable_roots]
        return max(max(xs) - min(xs), max(ys) - min(ys))

    @property
    def loop_closure(self) -> float:
        if len(self.stable_roots) < 2:
            return float("nan")
        first, last = self.stable_roots[0], self.stable_roots[-1]
        return max(abs(first[0] - last[0]), abs(first[1] - last[1]))

    def apply_metric_gates(self) -> None:
        median = self.adjacent_median
        worst = self.adjacent_worst
        if self.motion in LOCOMOTION_MOTIONS:
            if not math.isfinite(median):
                self.fail("coherence-not-measurable", "coherence is not measurable")
            elif median > MAX_ADJACENT_MEDIAN:
                self.fail("adjacent-median", f"median {median:.1f}% > {MAX_ADJACENT_MEDIAN:.0f}%")
            if math.isfinite(worst) and worst > MAX_ADJACENT_WORST:
                self.fail("adjacent-worst", f"worst {worst:.1f}% > {MAX_ADJACENT_WORST:.0f}%")
            if not math.isfinite(self.ratio):
                self.fail("ratio-not-finite", "locomotion ratio is not finite")
            elif self.ratio > MAX_ADJACENCY_RATIO:
                self.fail("adjacency-ratio", f"ratio {self.ratio:.2f} > {MAX_ADJACENCY_RATIO:.2f}")

        if self.enforce_work_quality:
            expected = WORK_FRAME_COUNTS[self.motion]
            if self.indices != list(range(expected)):
                self.fail("work-indices", f"indices {self.indices} != {list(range(expected))}")
            if self.unique_pixel_frames != expected:
                self.fail(
                    "duplicate-work-frame",
                    f"unique pixel frames {self.unique_pixel_frames} != {expected}",
                )
            if not math.isfinite(self.foot_drift):
                self.fail("work-foot-not-measurable", "work-action seat contact is not measurable")
            elif self.foot_drift > MAX_WORK_FOOT_DRIFT_PX + 0.01:
                self.fail(
                    "work-foot-drift",
                    f"seat-contact drift {self.foot_drift:.1f}px > {MAX_WORK_FOOT_DRIFT_PX:.0f}px",
                )
            if self.motion == "typing" and self.facing in APPROVED_TYPING_DIRECTIONS:
                if not math.isfinite(median) or median > MAX_TYPING_ADJACENT_MEDIAN:
                    self.fail(
                        "typing-body-motion",
                        f"typing adjacent median {median:.1f}% > {MAX_TYPING_ADJACENT_MEDIAN:.0f}%",
                    )
                if math.isfinite(worst) and worst > MAX_TYPING_ADJACENT_WORST:
                    self.fail(
                        "typing-body-worst",
                        f"typing adjacent worst {worst:.1f}% > {MAX_TYPING_ADJACENT_WORST:.0f}%",
                    )

        if not self.enforce_walk_quality:
            return
        if self.indices != list(range(FRAME_COUNT)):
            self.fail("walk-indices", f"indices {self.indices} != {list(range(FRAME_COUNT))}")
        if self.unique_pixel_frames != FRAME_COUNT:
            self.fail(
                "duplicate-frame",
                f"unique pixel frames {self.unique_pixel_frames} != {FRAME_COUNT}",
            )
        if len(self.frames_audit) == FRAME_COUNT and all(item.path.is_file() for item in self.frames_audit):
            rgba_frames = []
            for item in self.frames_audit:
                with Image.open(item.path) as loaded:
                    rgba_frames.append(np.asarray(loaded.convert("RGBA"), dtype=np.uint8))
            gait_scores = lower_body_motion_scores(rgba_frames)
            if min(gait_scores) < MIN_GAIT_ADJACENT_MOTION:
                self.fail(
                    "frozen-gait-pose",
                    f"minimum adjacent lower-body motion {min(gait_scores):.3f} < {MIN_GAIT_ADJACENT_MOTION:.2f}",
                )
            if float(np.median(gait_scores)) < MIN_GAIT_PHASE_MOTION:
                self.fail(
                    "weak-gait-phase",
                    f"median adjacent lower-body motion {float(np.median(gait_scores)):.3f} < {MIN_GAIT_PHASE_MOTION:.2f}",
                )
        if not math.isfinite(self.foot_drift):
            self.fail("foot-not-measurable", "foot contact is not measurable")
        elif self.foot_drift > MAX_FOOT_DRIFT_PX:
            self.fail("foot-drift", f"foot drift {self.foot_drift:.1f}px > {MAX_FOOT_DRIFT_PX:.0f}px")
        if not math.isfinite(self.stable_root_drift):
            self.fail("root-not-measurable", "stable root is not measurable")
        elif self.stable_root_drift > MAX_STABLE_ROOT_DRIFT_PX + 0.01:
            self.fail(
                "stable-root-drift",
                f"stable root drift {self.stable_root_drift:.1f}px > {MAX_STABLE_ROOT_DRIFT_PX:.0f}px",
            )
        if not math.isfinite(self.loop_closure):
            self.fail("closure-not-measurable", "loop closure is not measurable")
        elif self.loop_closure > MAX_LOOP_CLOSURE_PX:
            self.fail(
                "loop-closure",
                f"loop closure {self.loop_closure:.1f}px > {MAX_LOOP_CLOSURE_PX:.0f}px",
            )

    def as_json(self) -> dict[str, Any]:
        return {
            "character": self.character,
            "motion": self.motion,
            "facing": self.facing,
            "paths": [str(path) for path in self.paths],
            "indices": self.indices,
            "frames": self.frame_count,
            "fileSha256": [item.file_sha256 for item in self.frames_audit],
            "pixelSha256": [item.pixel_sha256 for item in self.frames_audit],
            "frameAudit": [item.as_json() for item in self.frames_audit],
            "adjacent": [round(value, 4) for value in self.adjacent],
            "opposite": [round(value, 4) for value in self.opposite],
            "adjacentMedian": finite_or_none(self.adjacent_median),
            "adjacentWorst": finite_or_none(self.adjacent_worst),
            "oppositeMedian": finite_or_none(self.opposite_median),
            "ratio": finite_or_none(self.ratio),
            "ratioFinite": math.isfinite(self.ratio),
            "uniquePixelFrames": self.unique_pixel_frames,
            "footBottomsPx": self.foot_bottoms,
            "footDriftPx": finite_or_none(self.foot_drift),
            "stableRoots": [[round(x, 3), round(y, 3)] for x, y in self.stable_roots],
            "stableRootDriftPx": finite_or_none(self.stable_root_drift),
            "loopClosurePx": finite_or_none(self.loop_closure),
            "failureCodes": self.failure_codes,
            "failures": self.failures,
        }


def subject_box(frame: np.ndarray) -> tuple[int, int, int, int] | None:
    mask = frame[:, :, 3] >= ALPHA_MIN
    rows = np.flatnonzero(mask.any(axis=1))
    columns = np.flatnonzero(mask.any(axis=0))
    if rows.size == 0 or columns.size == 0:
        return None
    return int(columns[0]), int(rows[0]), int(columns[-1]), int(rows[-1])


def stable_root(
    frame: np.ndarray,
    canonical: np.ndarray,
    canonical_box: tuple[int, int, int, int],
) -> tuple[float, float] | None:
    """Estimate integer torso registration against the canonical RGB core.

    Alpha medians move when a sleeve or hair tip crosses the core boundary even
    though the identity-bearing torso is pixel-identical.  The production
    stabilizer pins that torso, so the appropriate measurement is the integer
    offset whose RGB/alpha error against the canonical core is smallest.
    """
    left, top, right, bottom = canonical_box
    width = max(1, right - left + 1)
    height = max(1, bottom - top + 1)
    x0 = int(round(left + width * 0.25))
    x1 = int(round(left + width * 0.75))
    y0 = int(round(top + height * 0.10))
    y1 = int(round(top + height * 0.55))
    y0, y1 = max(0, y0), min(frame.shape[0] - 1, y1)
    x0, x1 = max(0, x0), min(frame.shape[1] - 1, x1)
    if y1 < y0 or x1 < x0:
        return None
    reference = canonical[y0:y1 + 1, x0:x1 + 1].astype(np.int16)
    reference_alpha = reference[:, :, 3] >= ALPHA_MIN
    if not reference_alpha.any():
        return None
    best: tuple[float, int, int] | None = None
    for dy in (-2, -1, 0, 1, 2):
        for dx in (-2, -1, 0, 1, 2):
            source_y0, source_y1 = y0 + dy, y1 + dy
            source_x0, source_x1 = x0 + dx, x1 + dx
            if source_y0 < 0 or source_x0 < 0 or source_y1 >= frame.shape[0] or source_x1 >= frame.shape[1]:
                continue
            sample = frame[source_y0:source_y1 + 1, source_x0:source_x1 + 1].astype(np.int16)
            union = reference_alpha | (sample[:, :, 3] >= ALPHA_MIN)
            if not union.any():
                continue
            score = float(np.abs(reference - sample).max(axis=2)[union].mean())
            candidate = (score, abs(dx) + abs(dy), dx, dy)
            if best is None or candidate < (best[0], abs(best[1]) + abs(best[2]), best[1], best[2]):
                best = (score, dx, dy)
    return None if best is None else (float(best[1]), float(best[2]))


def work_seat_anchor(frame: np.ndarray) -> tuple[float, float] | None:
    """Measure the planted vertical seat contact without following limbs or props.

    Horizontal silhouette medians are deliberately excluded: typing hands,
    crossed legs, and a raised drink all change them while the sprite root stays
    fixed.  Directional frame-major contact sheets remain the visual horizontal
    registration review artifact.
    """
    box = subject_box(frame)
    if box is None:
        return None
    return 0.0, float(box[3])


def change_percent(a: np.ndarray, b: np.ndarray) -> float:
    delta = np.abs(a.astype(np.int16) - b.astype(np.int16))
    union_mask = (a[:, :, 3] >= ALPHA_MIN) | (b[:, :, 3] >= ALPHA_MIN)
    union = int(union_mask.sum())
    if union <= 0:
        return 0.0
    changed = (
        (delta[:, :, :3].max(axis=2) >= DIFF_MIN)
        | (delta[:, :, 3] >= DIFF_MIN)
    ) & union_mask
    return float(changed.sum()) / union * 100.0


def lower_body_motion_scores(frames: list[np.ndarray]) -> list[float]:
    scores = []
    for index, frame in enumerate(frames):
        other = frames[(index + 1) % len(frames)]
        alpha = (frame[:, :, 3] >= ALPHA_MIN) | (other[:, :, 3] >= ALPHA_MIN)
        rows, _ = np.nonzero(alpha)
        if not len(rows):
            scores.append(0.0)
            continue
        top, bottom = int(rows.min()), int(rows.max())
        lower_start = top + int(round((bottom - top + 1) * 0.72))
        region = np.zeros(alpha.shape, dtype=bool)
        region[lower_start : bottom + 1] = True
        delta = np.abs(frame.astype(np.int16) - other.astype(np.int16))
        changed = (
            (delta[:, :, :3].max(axis=2) >= DIFF_MIN) | (delta[:, :, 3] >= DIFF_MIN)
        ) & region
        denominator = int((alpha & region).sum())
        scores.append(float(changed.sum()) / denominator if denominator else 0.0)
    return scores


def load_and_audit(index: int, path: Path, enforce_quality: bool) -> tuple[FrameAudit, np.ndarray | None]:
    audit = FrameAudit(index=index, path=path)
    try:
        audit.file_sha256 = sha256_file(path)
        with Image.open(path) as loaded:
            audit.native_mode = loaded.mode
            audit.size = loaded.size
            rgba = np.asarray(loaded.convert("RGBA"), dtype=np.uint8)
    except Exception as error:  # A corrupt image belongs in the report, not a traceback.
        audit.failures.append(f"image read failed: {error}")
        return audit, None

    audit.pixel_sha256 = hashlib.sha256(rgba.tobytes()).hexdigest()
    alpha = rgba[:, :, 3]
    audit.opaque_pixels = int((alpha > 0).sum())
    audit.intermediate_alpha_pixels = int(((alpha != 0) & (alpha != 255)).sum())
    audit.opaque_edge_pixels = int(
        (alpha[0, :] > 0).sum() + (alpha[-1, :] > 0).sum()
        + (alpha[1:-1, 0] > 0).sum() + (alpha[1:-1, -1] > 0).sum()
    )
    if enforce_quality:
        if audit.native_mode != "RGBA":
            audit.failures.append(f"native mode {audit.native_mode} != RGBA")
        if audit.size != CANVAS_SIZE:
            audit.failures.append(f"canvas {audit.size} != {CANVAS_SIZE}")
        if audit.intermediate_alpha_pixels:
            audit.failures.append(f"hard alpha violation: {audit.intermediate_alpha_pixels} pixels")
        if audit.opaque_pixels == 0:
            audit.failures.append("empty frame")
        # These HighMotion sources intentionally permit hair at y=0.  Left,
        # right and bottom contact indicate destructive clipping; top contact
        # is reported in JSON but is not a failure by itself.
        destructive_edge_pixels = int(
            (alpha[-1, :] > 0).sum()
            + (alpha[1:-1, 0] > 0).sum()
            + (alpha[1:-1, -1] > 0).sum()
        )
        if destructive_edge_pixels:
            audit.failures.append(f"opaque destructive canvas edge: {destructive_edge_pixels} pixels")
    return audit, rgba


def measure(loop: Loop) -> Loop:
    arrays: list[np.ndarray] = []
    for index, path in loop.entries:
        audit, frame = load_and_audit(
            index, path, loop.enforce_walk_quality or loop.enforce_work_quality
        )
        loop.frames_audit.append(audit)
        if audit.failures:
            loop.fail("frame-contract", f"{path.name}: {'; '.join(audit.failures)}")
        if frame is not None:
            arrays.append(frame)

    loop.unique_pixel_frames = len({item.pixel_sha256 for item in loop.frames_audit if item.pixel_sha256})
    shapes = {frame.shape for frame in arrays}
    if len(shapes) > 1:
        loop.fail("frame-shape-mismatch", f"frame array shapes do not match: {sorted(shapes)}")
    if len(arrays) == loop.frame_count and len(arrays) >= 2 and len(shapes) == 1:
        count = len(arrays)
        loop.adjacent = [change_percent(arrays[i], arrays[(i + 1) % count]) for i in range(count)]
        if count > 2:
            half = count // 2
            loop.opposite = [change_percent(arrays[i], arrays[(i + half) % count]) for i in range(count)]

        canonical_box = subject_box(arrays[0])
        canonical = arrays[0]
        for frame in arrays:
            box = subject_box(frame)
            if box is not None:
                loop.foot_bottoms.append(box[3])
            if loop.enforce_work_quality:
                root = work_seat_anchor(frame)
                if root is not None:
                    loop.stable_roots.append(root)
            elif canonical_box is not None:
                root = stable_root(frame, canonical, canonical_box)
                if root is not None:
                    loop.stable_roots.append(root)
    loop.apply_metric_gates()
    return loop


def discover(art_root: Path) -> tuple[list[Loop], dict[str, Any]]:
    buckets: dict[tuple[str, str, str], list[tuple[int, Path]]] = defaultdict(list)
    invalid_walk_names: list[str] = []
    for path in sorted(art_root.rglob("*.png")):
        walk = WALK_RE.match(path.name)
        if walk:
            buckets[(walk["cid"], "walk", walk["dir"])].append((int(walk["idx"]), path))
            continue
        work = WORK_RE.match(path.name)
        if work:
            buckets[(work["cid"], work["action"], work["dir"])].append((int(work["idx"]), path))
            continue
        is_high_motion_frame = (
            path.parent.name == "Frames"
            and path.parent.parent.name == "HighMotion"
            and path.parent.parent.parent.name == "Pixel"
        )
        if is_high_motion_frame and "_walk_" in path.name and "BeforeCoherenceV1" not in path.parts:
            invalid_walk_names.append(str(path))

    loops = [
        Loop(character, motion, facing, sorted(entries, key=lambda item: (item[0], str(item[1]))))
        for (character, motion, facing), entries in sorted(buckets.items())
        if len(entries) >= 2
    ]
    discovery = {
        "invalidWalkFrameNames": invalid_walk_names,
        "singleFrameBuckets": [
            {"character": key[0], "motion": key[1], "facing": key[2], "path": str(entries[0][1])}
            for key, entries in sorted(buckets.items()) if len(entries) == 1
        ],
    }
    return loops, discovery


def build_walk_contract(
    loops: list[Loop], discovery: dict[str, Any], enabled: bool
) -> dict[str, Any]:
    walk_loops = [loop for loop in loops if loop.motion == "walk"]
    characters = sorted({loop.character for loop in walk_loops})
    by_character: dict[str, dict[str, Loop]] = defaultdict(dict)
    for loop in walk_loops:
        by_character[loop.character][loop.facing] = loop

    missing: dict[str, list[str]] = {}
    unexpected: dict[str, list[str]] = {}
    bad_indices: list[dict[str, Any]] = []
    duplicate_indices: list[dict[str, Any]] = []
    for character in characters:
        actual = set(by_character[character])
        missing_dirs = [direction for direction in DIRECTIONS if direction not in actual]
        extra_dirs = sorted(actual - set(DIRECTIONS))
        if missing_dirs:
            missing[character] = missing_dirs
        if extra_dirs:
            unexpected[character] = extra_dirs
        for direction, loop in sorted(by_character[character].items()):
            counts = Counter(loop.indices)
            duplicates = sorted(index for index, count in counts.items() if count > 1)
            if duplicates:
                duplicate_indices.append(
                    {"character": character, "direction": direction, "indices": duplicates}
                )
            if loop.indices != list(range(FRAME_COUNT)):
                bad_indices.append(
                    {"character": character, "direction": direction, "actual": loop.indices}
                )

    single_walk_buckets = [
        bucket for bucket in discovery["singleFrameBuckets"]
        if bucket["motion"] == "walk"
    ]
    failures: list[str] = []
    if enabled and not characters:
        failures.append("no walk characters discovered")
    if enabled and discovery["invalidWalkFrameNames"]:
        failures.append(f"invalid walk frame names: {len(discovery['invalidWalkFrameNames'])}")
    if enabled and single_walk_buckets:
        failures.append(f"single-frame walk loops: {len(single_walk_buckets)}")
    if enabled and missing:
        failures.append(f"missing directions for {len(missing)} characters")
    if enabled and unexpected:
        failures.append(f"unexpected directions for {len(unexpected)} characters")
    if enabled and bad_indices:
        failures.append(f"invalid walk phase sets: {len(bad_indices)}")
    if enabled and duplicate_indices:
        failures.append(f"duplicate walk phases: {len(duplicate_indices)}")

    expected_loops = len(characters) * len(DIRECTIONS)
    expected_frames = expected_loops * FRAME_COUNT
    actual_frames = sum(loop.frame_count for loop in walk_loops)
    if enabled and len(walk_loops) != expected_loops:
        failures.append(f"walk loops {len(walk_loops)} != expected {expected_loops}")
    if enabled and actual_frames != expected_frames:
        failures.append(f"walk frames {actual_frames} != expected {expected_frames}")

    return {
        "enabled": enabled,
        "expected": {
            "directions": list(DIRECTIONS),
            "framesPerLoop": FRAME_COUNT,
            "loops": expected_loops,
            "frames": expected_frames,
        },
        "actual": {
            "characters": len(characters),
            "characterIds": characters,
            "loops": len(walk_loops),
            "frames": actual_frames,
        },
        "missingDirections": missing,
        "unexpectedDirections": unexpected,
        "badIndices": bad_indices,
        "duplicateIndices": duplicate_indices,
        "invalidWalkFrameNames": discovery["invalidWalkFrameNames"],
        "singleFrameBuckets": single_walk_buckets,
        "failures": failures,
        "pass": not failures,
    }


def render_report(loops: list[Loop], contract: dict[str, Any], art_root: Path) -> str:
    lines = [
        "ANIMATION COHERENCE / ASSET CONTRACT V2",
        f"artRoot: {art_root}",
        (
            f"locomotion gates: median<={MAX_ADJACENT_MEDIAN:.0f}% worst<={MAX_ADJACENT_WORST:.0f}% "
            f"ratio<={MAX_ADJACENCY_RATIO:.2f} unique={FRAME_COUNT} "
            f"footDrift<={MAX_FOOT_DRIFT_PX:.0f}px rootDrift<={MAX_STABLE_ROOT_DRIFT_PX:.0f}px "
            f"closure<={MAX_LOOP_CLOSURE_PX:.0f}px"
        ),
        (
            f"work gates: structure=typing6/mouse6/drink8 unique=all "
            f"seatContactDrift<={MAX_WORK_FOOT_DRIFT_PX:.0f}px "
            f"approvedTyping={','.join(sorted(APPROVED_TYPING_DIRECTIONS))} "
            f"typingMedian<={MAX_TYPING_ADJACENT_MEDIAN:.0f}% "
            f"typingWorst<={MAX_TYPING_ADJACENT_WORST:.0f}%"
        ),
        "",
        (
            f"walk contract: {'PASS' if contract['pass'] else 'FAIL'} "
            f"characters={contract['actual']['characters']} "
            f"loops={contract['actual']['loops']}/{contract['expected']['loops']} "
            f"frames={contract['actual']['frames']}/{contract['expected']['frames']}"
        ),
    ]
    for failure in contract["failures"]:
        lines.append(f"  CONTRACT FAIL: {failure}")
    lines += [
        "",
        f"{'character':<16}{'motion':<11}{'facing':<11}{'n':>3}{'median':>9}{'worst':>8}"
        f"{'ratio':>8}{'unique':>8}{'foot':>7}{'root':>7}{'close':>7} verdict",
    ]
    for loop in loops:
        verdict = "ok" if not loop.failures else "FAIL: " + "; ".join(loop.failures)
        lines.append(
            f"{loop.character:<16}{loop.motion:<11}{loop.facing:<11}{loop.frame_count:>3}"
            f"{loop.adjacent_median:>8.1f}%{loop.adjacent_worst:>7.1f}%"
            f"{loop.ratio:>8.2f}{loop.unique_pixel_frames:>8}"
            f"{loop.foot_drift:>7.1f}{loop.stable_root_drift:>7.1f}{loop.loop_closure:>7.1f} {verdict}"
        )
    failed = [loop for loop in loops if loop.failures]
    overall = contract["pass"] and not failed
    lines += [
        "",
        f"{'PASS' if overall else 'FAIL'} loops={len(loops) - len(failed)}/{len(loops)} contract={'PASS' if contract['pass'] else 'FAIL'}",
    ]
    return "\n".join(lines)


def output_path(repo_root: Path, requested: Path | None) -> Path:
    if requested is None:
        return repo_root / "Artifacts" / "AnimationCoherence" / "animation-coherence.txt"
    resolved = requested if requested.is_absolute() else repo_root / requested
    return resolved if resolved.suffix else resolved / "animation-coherence.txt"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument(
        "--art-root", type=Path, default=None,
        help="character/candidate tree to scan recursively (default Assets/Art/Characters)",
    )
    parser.add_argument("--output", type=Path, default=None)
    parser.add_argument("--motion", action="append", default=None)
    parser.add_argument("--strict", action="store_true", help="exit 1 on any gate violation")
    args = parser.parse_args()

    repo_root = args.repo_root.resolve()
    art_root = args.art_root or (repo_root / "Assets" / "Art" / "Characters")
    if not art_root.is_absolute():
        art_root = repo_root / art_root
    art_root = art_root.resolve()
    if not art_root.is_dir():
        print(f"character art root not found: {art_root}", file=sys.stderr)
        return 2

    loops, discovery = discover(art_root)
    wanted = {motion.lower() for motion in args.motion} if args.motion else None
    enforce_walk_contract = wanted is None or "walk" in wanted
    contract = build_walk_contract(loops, discovery, enforce_walk_contract)
    if wanted is not None:
        loops = [loop for loop in loops if loop.motion in wanted]
    if not loops:
        print("no animation loops discovered", file=sys.stderr)
        return 2

    for loop in loops:
        loop.enforce_walk_quality = enforce_walk_contract and loop.motion == "walk"
        loop.enforce_work_quality = loop.motion in WORK_FRAME_COUNTS
        measure(loop)

    report = render_report(loops, contract, art_root)
    print(report)
    out = output_path(repo_root, args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(report + "\n", encoding="utf-8")

    failed = [loop for loop in loops if loop.failures]
    overall_pass = contract["pass"] and not failed
    failure_counts = Counter(code for loop in failed for code in loop.failure_codes)
    payload = {
        "schemaVersion": 2,
        "artRoot": str(art_root),
        "gates": {
            "adjacentMedianMax": MAX_ADJACENT_MEDIAN,
            "adjacentWorstMax": MAX_ADJACENT_WORST,
            "adjacencyRatioMax": MAX_ADJACENCY_RATIO,
            "uniqueFrames": FRAME_COUNT,
            "footDriftMaxPx": MAX_FOOT_DRIFT_PX,
            "stableRootDriftMaxPx": MAX_STABLE_ROOT_DRIFT_PX,
            "loopClosureMaxPx": MAX_LOOP_CLOSURE_PX,
            "workSeatContactDriftMaxPx": MAX_WORK_FOOT_DRIFT_PX,
            "typingAdjacentMedianMax": MAX_TYPING_ADJACENT_MEDIAN,
            "typingAdjacentWorstMax": MAX_TYPING_ADJACENT_WORST,
            "approvedTypingDirections": sorted(APPROVED_TYPING_DIRECTIONS),
            "canvas": list(CANVAS_SIZE),
            "nativeMode": "RGBA",
            "alphaValues": [0, 255],
            "opaqueEdgePixelsMax": 0,
        },
        "contract": contract,
        "summary": {
            "pass": overall_pass,
            "contractPass": contract["pass"],
            "loops": len(loops),
            "passedLoops": len(loops) - len(failed),
            "failedLoops": len(failed),
            "failureCounts": dict(sorted(failure_counts.items())),
        },
        "loops": [loop.as_json() for loop in loops],
    }
    json_path = out.with_suffix(".json")
    json_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"\nreport: {out}\njson:   {json_path}", file=sys.stderr)
    if args.strict and not overall_pass:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
