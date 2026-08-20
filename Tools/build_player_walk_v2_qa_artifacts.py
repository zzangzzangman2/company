#!/usr/bin/env python3
"""Create compact human-review artifacts from the baked south V2 frames."""

from pathlib import Path

from PIL import Image, ImageDraw


PROJECT = Path(__file__).resolve().parents[1]
FRAME_ROOT = PROJECT / "Assets/Resources/FamilyCompany/PlayerBakedWalkV2/Frames/south"
OUTPUT = PROJECT / "Artifacts/PlayerWalkV2/SouthQa"


def framed(source: Image.Image, scale: float, label: str) -> Image.Image:
    width = round(source.width * scale)
    height = round(source.height * scale)
    sprite = source.resize((width, height), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (width, height + 24), (30, 34, 42, 255))
    canvas.alpha_composite(sprite, (0, 0))
    ImageDraw.Draw(canvas).text((8, height + 5), label, fill=(245, 245, 245, 255))
    return canvas


def main() -> None:
    frames = [
        Image.open(FRAME_ROOT / f"player_south_walk_{pose}_v2.png").convert("RGBA")
        for pose in range(8)
    ]
    if any(frame.size != (384, 512) for frame in frames):
        raise RuntimeError("south V2 QA requires eight fixed 384x512 frames")
    if any(value not in (0, 255) for frame in frames for value in frame.getchannel("A").getdata()):
        raise RuntimeError("south V2 QA found non-hard alpha")

    OUTPUT.mkdir(parents=True, exist_ok=True)
    cells = [framed(frame, 0.5, f"POSE {pose} / {'L' if pose < 4 else 'R'} SUPPORT")
             for pose, frame in enumerate(frames)]
    sheet = Image.new("RGBA", (cells[0].width * 4, cells[0].height * 2), (30, 34, 42, 255))
    for pose, cell in enumerate(cells):
        sheet.alpha_composite(cell, ((pose % 4) * cell.width, (pose // 4) * cell.height))
    sheet.save(OUTPUT / "player-south-8pose.png")

    compact = [frame.resize((192, 256), Image.Resampling.NEAREST) for frame in frames]
    compact[0].save(
        OUTPUT / "player-south-1x.gif",
        save_all=True,
        append_images=compact[1:],
        duration=110,
        loop=0,
        disposal=2,
        transparency=0,
    )
    compact[0].save(
        OUTPUT / "player-south-025x.gif",
        save_all=True,
        append_images=compact[1:],
        duration=440,
        loop=0,
        disposal=2,
        transparency=0,
    )
    print(f"PLAYER_WALK_V2_QA_ARTIFACTS: PASS | output={OUTPUT}")


if __name__ == "__main__":
    main()
