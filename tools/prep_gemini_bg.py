"""제미나이 배경 파노라마 → 게임 세그먼트 텍스처.
담장 상단(측정 0.66)이 텍스처의 77.5% 지점에 오도록 세로 크롭 후 3분할.
(씬 빌더의 배치 수식과 맞추기 위함 — wallTop 77.5% = 월드 y 1.2m)
"""
import os
from PIL import Image

SRC = r"C:\project\game_simsim\art_raw\gemini_inbox\Gemini_Generated_Image_kqelifkqelifkqel.png"
OUT = r"C:\project\game_simsim\unity\Assets\Art\BG\street_gemini"
WALLTOP_FRAC_SRC = 0.66     # 측정값
WALLTOP_FRAC_DST = 0.775    # 빌더 기대값

img = Image.open(SRC).convert("RGB")
W, H = img.size
y_wt = WALLTOP_FRAC_SRC * H
# (y_wt - y0) / (y1 - y0) = 0.775, y1 = H (바닥까지 사용)
crop_h = (H - y_wt) / (1 - WALLTOP_FRAC_DST)
y0 = int(H - crop_h)
if y0 < 0:
    # 위가 모자라면 하늘색으로 패딩
    pad = -y0
    sky = img.crop((0, 0, W, 4)).resize((W, pad))
    canvas = Image.new("RGB", (W, H + pad))
    canvas.paste(sky, (0, 0))
    canvas.paste(img, (0, pad))
    img = canvas
    y0 = 0
    H = img.height

band = img.crop((0, y0, W, H))
print(f"crop band: {band.size}, wallTop@{(y_wt - y0) / band.height:.3f}")

os.makedirs(OUT, exist_ok=True)
tw = band.width // 3
for i in range(3):
    tile = band.crop((i * tw, 0, (i + 1) * tw, band.height))
    tile.save(os.path.join(OUT, f"bg_gem_{i:02d}.png"))
    print("saved", f"bg_gem_{i:02d}.png", tile.size)
