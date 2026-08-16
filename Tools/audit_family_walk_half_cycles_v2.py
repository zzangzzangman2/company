#!/usr/bin/env python3
"""Audit the four-family 8-direction walk rows against the V2 half-cycle contract.

North/south can mirror their own half-cycle.  A lateral or diagonal sprite cannot:
mirroring it changes the facing, so its second half must mirror the authored half-cycle
from the opposite-facing row.  This audit reports both the current 0<->3 change and the
direction-preserving projected change before tracked assets are rebuilt.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageChops, ImageOps


REPO_ROOT = Path(__file__).resolve().parents[1]
MIN_OPPOSITE_CHANGE = 0.30
PRIORITY_DIRECTIONS = (
    "northeast",
    "northwest",
    "southeast",
    "southwest",
    "north",
    "south",
    "east",
    "west",
)
MIRROR_SOURCE = {
    "north": "north",
    "south": "south",
    "northeast": "northwest",
    "northwest": "northeast",
    "east": "west",
    "west": "east",
    "southeast": "southwest",
    "southwest": "southeast",
}


@dataclass(frozen=True)
class Character:
    member_id: str
    folder: str
    prefix: str

    @property
    def frames_root(self) -> Path:
        return (
            REPO_ROOT
            / "Assets"
            / "Art"
            / "Characters"
            / self.folder
            / "Pixel"
            / "HighMotion"
            / "Frames"
        )

    def frame_path(self, direction: str, phase: int) -> Path:
        return self.frames_root / f"{self.prefix}_{direction}_walk_{phase}.png"


CHARACTERS = (
    Character("player", "Player", "player"),
    Character("older_sister", "OlderSister", "older_sister"),
    Character("father", "Father", "father"),
    Character("mother", "Mother", "mother"),
)


def silhouette_change(left: Image.Image, right: Image.Image) -> float:
    left_alpha = left.convert("RGBA").getchannel("A").point(lambda value: 255 if value else 0)
    right_alpha = right.convert("RGBA").getchannel("A").point(lambda value: 255 if value else 0)
    union = ImageChops.lighter(left_alpha, right_alpha)
    difference = ImageChops.logical_xor(left_alpha.convert("1"), right_alpha.convert("1"))
    union_pixels = sum(1 for value in union.get_flattened_data() if value)
    difference_pixels = sum(1 for value in difference.get_flattened_data() if value)
    return difference_pixels / union_pixels if union_pixels else 0.0


def audit() -> list[tuple[str, str, float, float, float, tuple[int, int]]]:
    rows: list[tuple[str, str, float, float, float, tuple[int, int]]] = []
    for direction in PRIORITY_DIRECTIONS:
        source_direction = MIRROR_SOURCE[direction]
        for character in CHARACTERS:
            frame0 = Image.open(character.frame_path(direction, 0)).convert("RGBA")
            frame3 = Image.open(character.frame_path(direction, 3)).convert("RGBA")
            mirror_source0 = Image.open(character.frame_path(source_direction, 0)).convert("RGBA")
            current = silhouette_change(frame0, frame3)
            projected = silhouette_change(frame0, ImageOps.mirror(mirror_source0))
            best = (-1.0, (0, 0))
            for own_phase in range(6):
                own = Image.open(character.frame_path(direction, own_phase)).convert("RGBA")
                for source_phase in range(6):
                    source = Image.open(
                        character.frame_path(source_direction, source_phase)
                    ).convert("RGBA")
                    change = silhouette_change(own, ImageOps.mirror(source))
                    if change > best[0]:
                        best = (change, (own_phase, source_phase))
            rows.append(
                (
                    character.member_id,
                    direction,
                    current,
                    projected,
                    best[0],
                    best[1],
                )
            )
    return rows


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--strict-current",
        action="store_true",
        help="fail if any tracked 0<->3 row is below 30 percent",
    )
    args = parser.parse_args()

    rows = audit()
    print("FAMILY WALK HALF-CYCLE V2 AUDIT")
    print("member          direction      current03 projected03 maxExisting best current projected")
    current_failures = 0
    projected_failures = 0
    for member_id, direction, current, projected, maximum, best_phases in rows:
        current_ok = current >= MIN_OPPOSITE_CHANGE
        projected_ok = projected >= MIN_OPPOSITE_CHANGE
        current_failures += int(not current_ok)
        projected_failures += int(not projected_ok)
        print(
            f"{member_id:<15} {direction:<14} {current * 100:>8.1f}% "
            f"{projected * 100:>10.1f}% {maximum * 100:>10.1f}% "
            f"{best_phases[0]}->{best_phases[1]} "
            f"{'PASS' if current_ok else 'FAIL':<7} {'PASS' if projected_ok else 'FAIL'}"
        )
    print(
        f"SUMMARY rows={len(rows)} currentFail={current_failures} "
        f"projectedFail={projected_failures} threshold={MIN_OPPOSITE_CHANGE * 100:.0f}%"
    )
    return 1 if args.strict_current and current_failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
