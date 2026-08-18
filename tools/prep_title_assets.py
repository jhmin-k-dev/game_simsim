"""타이틀 화면 에셋 준비 → unity/Assets/Resources/title/
- title_bg.png : 제미나이 노을 시장 원화 (1920 폭)
- dog_walk.png : 걷는 누룽이 컷 알파 추출
- panel.png    : 라운드 패널 (버튼 배경, 9-slice용)
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = r"C:\project\game_simsim\unity\Assets\Resources\title"
os.makedirs(OUT, exist_ok=True)

# 1) 배경 — 노을 시장 (가장 인상적인 원화)
bg = Image.open(r"C:\project\game_simsim\art_raw\gemini_inbox\Gemini_Generated_Image_nd4juxnd4juxnd4j.png").convert("RGB")
r = 1920 / bg.width
bg = bg.resize((1920, int(bg.height * r)))
bg.save(os.path.join(OUT, "title_bg.png"))
print("title_bg", bg.size)

# 2) 걷는 누룽이 — 흰 배경 제거 (밝고 채도 낮은 픽셀 → 투명)
dog = Image.open(r"C:\project\game_simsim\art_raw\gemini_inbox\Gemini_Generated_Image_o7vk6lo7vk6lo7vk.png").convert("RGB")
a = np.asarray(dog).astype(np.int16)
mx = a.max(axis=2); mn = a.min(axis=2)
bg_mask = (mn > 228) & (mx - mn < 22)
# 가장자리 부드럽게
alpha = np.where(bg_mask, 0, 255).astype(np.uint8)
alpha_img = Image.fromarray(alpha, "L").filter(ImageFilter.GaussianBlur(1.2))
rgba = np.dstack([a.astype(np.uint8), np.asarray(alpha_img)])
out = Image.fromarray(rgba, "RGBA")
# 내용 영역만 크롭
bbox = alpha_img.getbbox()
out = out.crop(bbox)
r = 600 / out.width
out = out.resize((600, int(out.height * r)))
out.save(os.path.join(OUT, "dog_walk.png"))
print("dog_walk", out.size)

# 3) 라운드 패널 (크림 반투명은 코드에서 tint)
S_W, S_H, R = 256, 96, 28
panel = Image.new("RGBA", (S_W, S_H), (0, 0, 0, 0))
d = ImageDraw.Draw(panel)
d.rounded_rectangle([2, 2, S_W - 3, S_H - 3], radius=R, fill=(255, 255, 255, 255),
                    outline=(110, 95, 73, 255), width=3)
panel.save(os.path.join(OUT, "panel.png"))
print("panel ok")
