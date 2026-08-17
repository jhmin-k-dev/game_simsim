"""누룽이 캐릭터 후보 로컬 생성 (SDXL, 2패스).
사용: python gen_character.py [--count 3] [--ckpt <이름>]
출력: art_raw/character/char_{sheet|front|side}_{n}.png
"""
import argparse, os
from gen_anchors import submit, wait_and_fetch, NEGATIVE

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "art_raw", "character")

# 스타일 문장은 앵커 원본(첫 배치)과 동일 계열 — 디렉터 판정: 첫 배치 스타일이 기준
CHAR = ("a chubby cream-colored puppy mascot character, chibi proportions, "
        "big round head half of total height, droopy warm-brown ears, "
        "tiny black dot eyes, small round nose, red collar with round gold tag, "
        "standing upright on two short legs, small stubby arms, tiny tail, "
        "flat pastel cartoon illustration, storybook style, warm cream palette, "
        "soft paper texture, bold warm brown outlines, minimal detail, "
        "plain white background, no text, no watermark")

VARIANTS = {
    "sheet": CHAR + ", character reference sheet, same character shown three times in a row: "
             "front view, side view, back view, consistent design, T-pose arms slightly out",
    "front": CHAR + ", single full body front view, arms slightly out, centered",
    "side":  CHAR + ", single full body side profile view facing right, centered",
    "quad":  CHAR + ", running on four legs like a real dog, side profile view facing right, centered",
}

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--count", type=int, default=3)
    ap.add_argument("--ckpt", default="sd_xl_base_1.0.safetensors")
    args = ap.parse_args()

    os.makedirs(OUT, exist_ok=True)
    jobs = []
    for name, prompt in VARIANTS.items():
        for n in range(args.count):
            dest = os.path.join(OUT, f"char_{name}_{n}.png")
            if os.path.exists(dest):
                continue
            w, h = (1216, 832) if name == "sheet" else (896, 1152)
            seed = hash((name, n)) % (2**31)
            pid = submit(prompt, seed, args.ckpt, w=w, h=h)
            ok = wait_and_fetch(pid, dest)
            print(f"char_{name}_{n}: {'OK' if ok else 'FAIL'}")

if __name__ == "__main__":
    main()
