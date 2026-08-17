"""LoRA 학습 데이터셋 준비: anchors_picked → 학습 폴더 + 캡션 txt.
캡션 = 트리거 워드 'nurungi style' + 슬롯의 장면 설명 (anchor_prompts.csv).
"""
import csv, os, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
PICKED = os.path.join(HERE, "..", "art_raw", "anchors_picked")
# kohya 규칙: <repeats>_<name> 폴더명
OUT = os.path.join(HERE, "..", "art_raw", "lora_dataset", "40_nurungi")

desc = {}
with open(os.path.join(HERE, "anchor_prompts.csv"), encoding="utf-8") as f:
    for row in csv.DictReader(f):
        desc[row["slot_id"]] = row["scene_desc"]

os.makedirs(OUT, exist_ok=True)
count = 0
for name in sorted(os.listdir(PICKED)):
    if not name.endswith(".png"):
        continue
    slot = name.rsplit("_", 1)[0]          # anchor_01_2 → anchor_01
    scene = desc.get(slot, "background scene")
    shutil.copy(os.path.join(PICKED, name), os.path.join(OUT, name))
    with open(os.path.join(OUT, name.replace(".png", ".txt")), "w", encoding="utf-8") as f:
        f.write(f"nurungi style, {scene}")
    count += 1

print(f"{count}장 준비 완료 → {os.path.abspath(OUT)}")
