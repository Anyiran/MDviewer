#!/usr/bin/env python3
"""Generate MDviewer's multi-resolution application icon.

Renders each size with supersampling (8x) and Lanczos downscaling so the
rounded square and letterforms stay smooth at 16px as well as 256px.

Usage:
    python tools/make_icon.py
"""
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps
import os
import struct

SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
MASTER = 1024

# Modern indigo -> azure gradient with a soft cyan glow.
TOP_LEFT = (92, 143, 255)     # #5C8FFF
BOTTOM_RIGHT = (67, 56, 202)   # #4338CA
GLOW = (34, 211, 238)          # #22D3EE


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def gradient(size):
    img = Image.new("RGB", (size, size))
    px = img.load()
    for y in range(size):
        for x in range(size):
            # Diagonal blend, slightly biased toward vertical.
            t = (x / size) * 0.42 + (y / size) * 0.58
            px[x, y] = lerp(TOP_LEFT, BOTTOM_RIGHT, min(1.0, max(0.0, t)))
    return img


def rounded_mask(size, radius, inset=0):
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle(
        [inset, inset, size - 1 - inset, size - 1 - inset],
        radius=radius,
        fill=255,
    )
    return mask


def find_font():
    candidates = [
        r"C:\Windows\Fonts\seguisb.ttf",   # Segoe UI Semibold
        r"C:\Windows\Fonts\segoeuib.ttf",  # Segoe UI Bold
        r"C:\Windows\Fonts\arialbd.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ]
    for path in candidates:
        if os.path.exists(path):
            return path
    return None


def render(size):
    """Render one icon size at 8x supersampling, then downscale."""
    scale = 8
    s = size * scale
    canvas = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    # --- squircle background
    radius = int(s * 0.225)
    mask = rounded_mask(s, radius)
    bg = gradient(s).convert("RGBA")

    # Soft cyan glow, bottom-right.
    glow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse(
        [int(s * 0.42), int(s * 0.42), int(s * 1.12), int(s * 1.12)],
        fill=GLOW + (70,),
    )
    glow = glow.filter(ImageFilter.GaussianBlur(int(s * 0.16)))
    bg = Image.alpha_composite(bg, Image.composite(glow, Image.new("RGBA", (s, s), (0, 0, 0, 0)), mask))

    # Gentle top light for depth.
    light = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ld = ImageDraw.Draw(light)
    ld.ellipse(
        [int(-s * 0.25), int(-s * 0.55), int(s * 0.95), int(s * 0.42)],
        fill=(255, 255, 255, 46),
    )
    light = light.filter(ImageFilter.GaussianBlur(int(s * 0.10)))
    bg = Image.alpha_composite(bg, Image.composite(light, Image.new("RGBA", (s, s), (0, 0, 0, 0)), mask))

    canvas.paste(bg, (0, 0), mask)

    # Inner highlight border.
    border = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    bd = ImageDraw.Draw(border)
    bd.rounded_rectangle(
        [int(s * 0.035), int(s * 0.035), s - 1 - int(s * 0.035), s - 1 - int(s * 0.035)],
        radius=int(radius * 0.94),
        outline=(255, 255, 255, 52),
        width=max(1, int(s * 0.006)),
    )
    canvas = Image.alpha_composite(canvas, border)

    # --- white "M" with a soft shadow
    font_path = find_font()
    letter = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ld = ImageDraw.Draw(letter)
    if font_path:
        font = ImageFont.truetype(font_path, int(s * 0.60))
        bbox = ld.textbbox((0, 0), "M", font=font)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]
        x = (s - tw) / 2 - bbox[0]
        y = (s - th) / 2 - bbox[1] - s * 0.012
        ld.text((x, y), "M", font=font, fill=(255, 255, 255, 255))
    else:
        # Geometric fallback: a clean stroked M.
        w = int(s * 0.13)
        pts = [
            (s * 0.27, s * 0.72),
            (s * 0.27, s * 0.32),
            (s * 0.50, s * 0.56),
            (s * 0.73, s * 0.32),
            (s * 0.73, s * 0.72),
        ]
        ld.line(pts, fill=(255, 255, 255, 255), width=w, joint="curve")
        for p in pts:
            ld.ellipse(
                [p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2],
                fill=(255, 255, 255, 255),
            )

    shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    shadow.paste((12, 24, 64, 110), (0, 0), letter.split()[3])
    shadow = shadow.filter(ImageFilter.GaussianBlur(int(s * 0.018)))
    shadow = shadow.transform(
        shadow.size, Image.AFFINE, (1, 0, 0, 0, 1, -int(s * 0.012))
    )
    canvas = Image.alpha_composite(canvas, shadow)
    canvas = Image.alpha_composite(canvas, letter)

    # --- downsample
    canvas = canvas.resize((size, size), Image.LANCZOS)
    return canvas


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, "src", "MDviewer", "Assets", "app.ico")
    os.makedirs(os.path.dirname(out), exist_ok=True)

    frames = [render(sz) for sz in SIZES]

    # Build a proper ICO container (PNG frames for modern Windows, BMP-like
    # payloads produced by Pillow for small sizes via the built-in encoder).
    # Pillow's ICO writer handles the container and size list for us.
    master = frames[-1]
    master.save(out, format="ICO", sizes=[(s, s) for s in SIZES])

    # Also write a preview PNG for documentation.
    preview = os.path.join(root, "docs", "icon-preview.png")
    frames[-1].save(preview, format="PNG")
    print("wrote", out)
    print("wrote", preview)


if __name__ == "__main__":
    main()
