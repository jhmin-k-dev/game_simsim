"""제미나이 캐릭터 시트 자동 크롭.
밝은 배경에서 그림 덩어리(연결 요소)를 찾아 개별 PNG로 저장.
- 3뷰 시트 → char_views/front|side|back.png (x 순서)
- 표정 시트 12종 x2 → expressions/expr_00..23.png (행 우선)
- 자세 컷 → poses/
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

SRC = r"C:\project\game_simsim\art_raw\char_source"
OUT = r"C:\project\game_simsim\art_raw\char_cropped"

def islands(img, bg_thresh=232, min_area=8000):
    """밝은 배경이 아닌 픽셀 덩어리들의 바운딩박스 (라벨 텍스트는 min_area로 걸러냄)"""
    a = np.asarray(img.convert("RGB")).astype(np.int16)
    # 배경: 밝고 채도 낮음
    mx = a.max(axis=2); mn = a.min(axis=2)
    fg = ~((mn > bg_thresh) & (mx - mn < 26))
    # 몇 픽셀 팽창해 조각 붙이기
    fg = ndimage.binary_dilation(fg, iterations=6)
    labels, n = ndimage.label(fg)
    boxes = []
    for sl in ndimage.find_objects(labels):
        h = sl[0].stop - sl[0].start
        w = sl[1].stop - sl[1].start
        if h * w < min_area:
            continue
        boxes.append((sl[1].start, sl[0].start, sl[1].stop, sl[0].stop))
    return boxes

def crop_save(img, boxes, folder, names=None, pad=14):
    os.makedirs(folder, exist_ok=True)
    out = []
    for i, (x0, y0, x1, y1) in enumerate(boxes):
        x0 = max(0, x0 - pad); y0 = max(0, y0 - pad)
        x1 = min(img.width, x1 + pad); y1 = min(img.height, y1 + pad)
        c = img.crop((x0, y0, x1, y1))
        name = names[i] if names and i < len(names) else f"part_{i:02d}"
        path = os.path.join(folder, f"{name}.png")
        c.save(path)
        out.append(path)
    return out

def row_major(boxes, row_tol=120):
    """행 우선 정렬 (y 근접한 것끼리 한 행, 행 안에서 x 순)"""
    boxes = sorted(boxes, key=lambda b: b[1])
    rows = []
    for b in boxes:
        cy = (b[1] + b[3]) / 2
        placed = False
        for row in rows:
            if abs(row[0] - cy) < row_tol:
                row[1].append(b)
                row[0] = (row[0] * (len(row[1]) - 1) + cy) / len(row[1])
                placed = True
                break
        if not placed:
            rows.append([cy, [b]])
    result = []
    for _, row in sorted(rows, key=lambda r: r[0]):
        result.extend(sorted(row, key=lambda b: b[0]))
    return result

def big_only(boxes, ratio=0.35):
    """가장 큰 박스 대비 ratio 미만 면적(라벨 등) 제거"""
    if not boxes:
        return boxes
    areas = [(b[2] - b[0]) * (b[3] - b[1]) for b in boxes]
    biggest = max(areas)
    return [b for b, a in zip(boxes, areas) if a >= biggest * ratio]

jobs = {
    # 파일: (출력 폴더, 이름 목록 or None)
    "views_labeled.png": ("char_views", ["front", "side", "back"]),
    "views_clean.png": ("char_views_alt", ["front", "side", "back"]),
    "poses.png": ("poses", ["lie", "front_stand", "back_stand"]),
    "expr_setA.png": ("expressions_a", [f"a_{i:02d}" for i in range(12)]),
    "expr_setB.png": ("expressions_b", [f"b_{i:02d}" for i in range(12)]),
    "expr_basic6.png": ("expressions_basic", [f"basic_{i}" for i in range(6)]),
}

for fname, (folder, names) in jobs.items():
    path = os.path.join(SRC, fname)
    if not os.path.exists(path):
        print("skip:", fname)
        continue
    img = Image.open(path)
    boxes = big_only(row_major(islands(img)))
    saved = crop_save(img, boxes, os.path.join(OUT, folder), names)
    print(f"{fname}: {len(saved)}개 → {folder}")
