"""참조 영상(KakaoTalk_20260810) f01 팔레트 기반 placeholder 아트 생성.
정식 배경은 05_배경_파이프라인으로 대체 예정 — 이것은 M0 룩 검증용 목업.
출력: unity/Assets/Art/ 하위 PNG.
"""
import os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

random.seed(20792)
np.random.seed(20792)

OUT = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Art")

# ---- 참조 영상 팔레트 ----
SKY      = (237, 227, 208)
WALL     = (217, 204, 180)
WALL_TOP = (200, 186, 160)
BUSH     = (168, 173, 126)
BUSH_D   = (146, 152, 105)
SIDEWALK = (216, 210, 192)
CURB     = (196, 189, 168)
ROAD     = (185, 180, 166)
CANOPY   = (152, 164, 104)
CANOPY_D = (128, 141, 84)
TRUNK    = (138, 111, 77)
OUTLINE  = (110, 95, 73)

W, H = 2048, 1152
# 배경판 = 하늘(위 77.5%) + 담벼락(아래 22.5%).
# 판 폭 15m → 높이 8.44m. 담벼락 상단이 월드 y=1.2m에 오도록 씬에서 배치한다.
# 참조 영상처럼 하늘 여백이 화면 절반을 차지하는 것이 이 구도의 핵심.
HORIZON = int(H * 0.775)

def paper_noise(img, strength=8):
    """은은한 종이 섬유 노이즈를 굽는다."""
    a = np.asarray(img).astype(np.int16)
    n = np.random.randint(-strength, strength + 1, size=a.shape[:2])[:, :, None]
    fiber = np.random.randint(-3, 4, size=(a.shape[0], 1, 1))  # 가로 결
    a[:, :, :3] = np.clip(a[:, :, :3] + n + fiber, 0, 255)
    return Image.fromarray(a.astype(np.uint8), img.mode)

def blob_row(d, y, x0, x1, r_range, color, outline, n):
    for _ in range(n):
        cx = random.randint(x0, x1)
        r = random.randint(*r_range)
        d.ellipse([cx - r, y - r, cx + r, y + int(r * 0.7)], fill=color, outline=outline, width=5)

# ================= 배경 세그먼트 =================
def gen_bg(idx):
    img = Image.new("RGB", (W, H), SKY)
    d = ImageDraw.Draw(img)
    # 벽 뒤 덤불 (지평선 위로 삐죽) — 참조 영상의 담장 너머 초록
    blob_row(d, HORIZON + 6, -60, W + 60, (46, 96), BUSH, OUTLINE, 30)
    blob_row(d, HORIZON + 2, -60, W + 60, (32, 66), BUSH_D, OUTLINE, 20)
    # 담벼락 (지평선부터 판 바닥까지 — 아래쪽은 3D 인도에 가려짐)
    d.rectangle([0, HORIZON, W, H], fill=WALL)
    d.line([0, HORIZON, W, HORIZON], fill=OUTLINE, width=5)
    d.rectangle([0, HORIZON, W, HORIZON + 10], fill=WALL_TOP)
    # 난간 (담벼락 위 얇은 살)
    rail_top = HORIZON - 34
    for x in range(30, W, 76):
        d.line([x, rail_top, x, HORIZON], fill=OUTLINE, width=3)
    d.line([0, rail_top, W, rail_top], fill=OUTLINE, width=4)
    # 벽 얼룩 (은은한 세로 터치)
    for _ in range(12):
        x = random.randint(0, W); y = random.randint(HORIZON + 24, H - 30)
        d.line([x, y, x, y + random.randint(10, 34)], fill=WALL_TOP, width=3)
    img = paper_noise(img)
    img.save(f"{OUT}/BG/street_mock/bg_street_{idx:02d}.png")

# ================= 하늘 (원경) =================
def gen_sky():
    img = Image.new("RGB", (1024, 512), SKY)
    a = np.asarray(img).astype(np.int16)
    grad = np.linspace(6, -6, 512)[:, None, None]  # 위가 살짝 밝게
    a = np.clip(a + grad, 0, 255).astype(np.uint8)
    img = paper_noise(Image.fromarray(a))
    img.save(f"{OUT}/BG/street_mock/sky.png")

# ================= 컷아웃: 가로수 =================
def gen_tree():
    S = 1024
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx, base = S // 2, S - 30
    # 줄기
    d.rectangle([cx - 26, S // 2, cx + 26, base], fill=TRUNK, outline=OUTLINE, width=6)
    d.polygon([(cx - 60, base), (cx + 60, base), (cx + 30, base - 40), (cx - 30, base - 40)], fill=TRUNK)
    # 가지 두 개
    d.line([cx, S // 2 + 60, cx - 120, S // 2 - 40], fill=TRUNK, width=30)
    d.line([cx, S // 2 + 80, cx + 130, S // 2 - 20], fill=TRUNK, width=28)
    # 캐노피: 뭉게 원 클러스터
    cl = [(cx, 250, 190), (cx - 170, 330, 130), (cx + 170, 330, 130),
          (cx - 90, 210, 120), (cx + 90, 210, 120), (cx, 380, 150),
          (cx - 220, 420, 90), (cx + 220, 420, 90)]
    for x, y, r in cl:
        d.ellipse([x - r, y - r, x + r, y + r], fill=CANOPY, outline=OUTLINE, width=7)
    for x, y, r in cl[:5]:
        d.ellipse([x - r + 25, y - r + 25, x + r - 40, y + r - 40], fill=CANOPY)
    # 안쪽 음영 뭉게
    for x, y, r in [(cx - 60, 330, 80), (cx + 80, 300, 70), (cx, 420, 85)]:
        d.ellipse([x - r, y - r, x + r, y + r], fill=CANOPY_D)
    img.save(f"{OUT}/Prop/street_mock/cutout_tree.png")

# ================= 컷아웃: 덤불 =================
def gen_bush():
    S = 512
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for x, y, r in [(140, 360, 110), (280, 330, 130), (400, 370, 100), (210, 400, 100), (350, 410, 90)]:
        d.ellipse([x - r, y - r, x + r, y + r], fill=BUSH, outline=OUTLINE, width=6)
    for x, y, r in [(230, 380, 70), (330, 370, 60)]:
        d.ellipse([x - r, y - r, x + r, y + r], fill=BUSH_D)
    img.save(f"{OUT}/Prop/street_mock/cutout_bush.png")

# ================= 종이 그레인 (화면 오버레이, 타일) =================
def gen_grain():
    S = 512
    n = np.random.randint(0, 255, (S, S)).astype(np.uint8)
    img = Image.fromarray(n, "L").filter(ImageFilter.GaussianBlur(0.6))
    a = np.asarray(img).astype(np.float32)
    a = (a - a.min()) / (a.max() - a.min()) * 255
    rgba = np.zeros((S, S, 4), dtype=np.uint8)
    rgba[:, :, 0] = rgba[:, :, 1] = rgba[:, :, 2] = 110  # 중성 회갈색
    rgba[:, :, 3] = (np.abs(a - 128) * 0.9).astype(np.uint8)  # 밝고 어두운 알갱이만 알파
    Image.fromarray(rgba, "RGBA").save(f"{OUT}/FX/paper_grain.png")

# ================= 블롭 섀도우 (02 §3-3 D5) =================
def gen_blob_shadow():
    S = 256
    yy, xx = np.mgrid[0:S, 0:S]
    cx = cy = (S - 1) / 2
    r = np.sqrt(((xx - cx) / (S * 0.42)) ** 2 + ((yy - cy) / (S * 0.30)) ** 2)
    alpha = np.clip(1.0 - r, 0, 1) ** 1.5 * 120
    rgba = np.zeros((S, S, 4), dtype=np.uint8)
    rgba[:, :, 0], rgba[:, :, 1], rgba[:, :, 2] = 120, 104, 82  # 웜톤 그림자
    rgba[:, :, 3] = alpha.astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(f"{OUT}/Prop/street_mock/blob_shadow.png")

# ================= 보도블럭 타일 (seamless) =================
def gen_pavement():
    S = 512
    img = Image.new("RGB", (S, S), SIDEWALK)
    d = ImageDraw.Draw(img)
    for i in range(0, S + 1, S // 4):
        d.line([i, 0, i, S], fill=CURB, width=3)
        d.line([0, i, S, i], fill=CURB, width=3)
    for _ in range(60):  # 미세한 얼룩
        x, y = random.randint(0, S), random.randint(0, S)
        d.ellipse([x, y, x + random.randint(2, 6), y + random.randint(2, 5)], fill=CURB)
    paper_noise(img, 5).save(f"{OUT}/BG/street_mock/pavement_tile.png")

for sub in ["BG/street_mock", "Prop/street_mock", "FX"]:
    os.makedirs(os.path.join(OUT, *sub.split("/")), exist_ok=True)

for i in range(3):
    gen_bg(i)
gen_sky()
gen_tree()
gen_bush()
gen_grain()
gen_blob_shadow()
gen_pavement()
print("done:", os.path.abspath(OUT))
