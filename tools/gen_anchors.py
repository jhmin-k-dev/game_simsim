"""앵커 30장 생성 — ComfyUI API 배치 제출 (05 §2 / 09 §B-1).

전제: ComfyUI(127.0.0.1:8188)에 txt2img 체크포인트가 설치되어 있어야 한다.
  (2026-08-17 현재 미설치 — SDXL 등 설치 후 CKPT 이름만 바꿔 실행)

사용:
  python gen_anchors.py --list-models            # 사용 가능한 체크포인트 확인
  python gen_anchors.py --ckpt <이름> [--count 3] # 슬롯당 3후보 생성
출력: art_raw/anchors/<slot_id>_<n>.png
"""
import argparse, csv, json, os, time, urllib.request

BASE = "http://127.0.0.1:8188"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "art_raw", "anchors")

# 05 §2-1 고정 프롬프트 + 목업에서 검증한 팔레트 (조정: 부감 각도는 F-1 확정 후 낮음)
STYLE = ("flat pastel cartoon illustration, storybook style, warm cream palette, "
         "beige sky (#EDE3D0), muted olive greens, soft paper texture, "
         "bold warm brown outlines, minimal detail, large empty sky, "
         "eye-level view slightly above ground, horizon around 62% height, "
         "flat ground in lower third, no characters, no animals, no people, no text")
NEGATIVE = ("photo, realistic, 3d render, people, person, animal, character, text, "
            "letters, watermark, signature, high detail, cluttered, dark, saturated colors")

def submit(prompt_text, seed, ckpt, w=1216, h=832):
    """2패스 하이레즈 txt2img (SDXL 계열): 생성 → 1.5배 잠재 업스케일 → 약한 재샘플.
    선이 또렷해지고 형태가 정돈된다 — 최종 1824x1248."""
    g = {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": ckpt}},
        "2": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": prompt_text}},
        "3": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": NEGATIVE}},
        "4": {"class_type": "EmptyLatentImage", "inputs": {"width": w, "height": h, "batch_size": 1}},
        "5": {"class_type": "KSampler", "inputs": {
            "model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["4", 0],
            "seed": seed, "steps": 28, "cfg": 6.0, "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0}},
        # ---- 2패스: 잠재 업스케일 후 낮은 denoise 재샘플 ----
        "8": {"class_type": "LatentUpscaleBy", "inputs": {"samples": ["5", 0], "upscale_method": "nearest-exact", "scale_by": 1.5}},
        "9": {"class_type": "KSampler", "inputs": {
            "model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["8", 0],
            "seed": seed + 1, "steps": 14, "cfg": 5.5, "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 0.35}},
        "6": {"class_type": "VAEDecode", "inputs": {"samples": ["9", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": "nurungi_anchor"}},
    }
    req = urllib.request.Request(BASE + "/prompt", json.dumps({"prompt": g}).encode(),
                                 {"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read())["prompt_id"]

def wait_and_fetch(prompt_id, dest):
    for _ in range(600):  # 최대 10분
        with urllib.request.urlopen(BASE + f"/history/{prompt_id}", timeout=10) as r:
            hist = json.loads(r.read())
        if prompt_id in hist:
            outputs = hist[prompt_id].get("outputs", {})
            for node in outputs.values():
                for img in node.get("images", []):
                    url = BASE + f"/view?filename={img['filename']}&subfolder={img.get('subfolder','')}&type={img['type']}"
                    urllib.request.urlretrieve(url, dest)
                    return True
            return False
        time.sleep(1)
    return False

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ckpt")
    ap.add_argument("--count", type=int, default=3)
    ap.add_argument("--list-models", action="store_true")
    args = ap.parse_args()

    if args.list_models:
        with urllib.request.urlopen(BASE + "/object_info/CheckpointLoaderSimple", timeout=10) as r:
            info = json.loads(r.read())
        for name in info["CheckpointLoaderSimple"]["input"]["required"]["ckpt_name"][0]:
            print(name)
        return

    if not args.ckpt:
        print("--ckpt <체크포인트 이름> 필요 (--list-models로 확인)")
        return

    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(HERE, "anchor_prompts.csv"), encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    total = len(rows) * args.count
    done = 0
    for row in rows:
        for n in range(args.count):
            dest = os.path.join(OUT, f"{row['slot_id']}_{n}.png")
            if os.path.exists(dest):
                done += 1
                continue
            prompt = f"{STYLE}, {row['scene_desc']}, {row['time']} lighting"
            seed = hash((row["slot_id"], n)) % (2**31)
            pid = submit(prompt, seed, args.ckpt)
            ok = wait_and_fetch(pid, dest)
            done += 1
            print(f"[{done}/{total}] {row['slot_id']}_{n} {'OK' if ok else 'FAIL'}")

if __name__ == "__main__":
    main()
