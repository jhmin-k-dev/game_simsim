"""제미나이 3뷰 시트 → Hunyuan3D-mv 멀티뷰 3D 재생성 (로컬 ComfyUI).
사용: python gen_dog_3d.py [--views char_views] [--out nurungi_v2]
출력: art_raw/mesh_out/<out>.glb
"""
import argparse, json, os, shutil, time, urllib.request

BASE = "http://127.0.0.1:8188"
HERE = os.path.dirname(os.path.abspath(__file__))
COMFY_INPUT = r"C:\project\MiniMax H3\ComfyUI_windows_portable\ComfyUI\input"
COMFY_OUTPUT = r"C:\project\MiniMax H3\ComfyUI_windows_portable\ComfyUI\output"

def api(path, payload=None):
    if payload is None:
        with urllib.request.urlopen(BASE + path, timeout=30) as r:
            return json.loads(r.read())
    req = urllib.request.Request(BASE + path, json.dumps(payload).encode(),
                                 {"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read())

def optional_view(name):
    """뷰 이미지가 있으면 ComfyUI input으로 복사하고 파일명 반환"""
    src = os.path.join(HERE, "..", "art_raw", "char_cropped", ARGS.views, f"{name}.png")
    if not os.path.exists(src):
        return None
    dst_name = f"nurungi_view_{name}.png"
    shutil.copy(src, os.path.join(COMFY_INPUT, dst_name))
    return dst_name

ap = argparse.ArgumentParser()
ap.add_argument("--views", default="char_views")
ap.add_argument("--out", default="nurungi_v2")
ap.add_argument("--ckpt", default="hunyuan3d-dit-v2-mv_fp16.safetensors")
ap.add_argument("--steps", type=int, default=50)
ARGS = ap.parse_args()

os.makedirs(COMFY_INPUT, exist_ok=True)
front = optional_view("front")
side = optional_view("side")
back = optional_view("back")
if not front:
    raise SystemExit("front 뷰가 필요합니다")
print("views:", front, side, back)

g = {
    "1": {"class_type": "ImageOnlyCheckpointLoader", "inputs": {"ckpt_name": ARGS.ckpt}},
    "10": {"class_type": "LoadImage", "inputs": {"image": front}},
    "11": {"class_type": "CLIPVisionEncode", "inputs": {"clip_vision": ["1", 1], "image": ["10", 0], "crop": "center"}},
    "40": {"class_type": "EmptyLatentHunyuan3Dv2", "inputs": {"resolution": 3072, "batch_size": 1}},
}
mv_inputs = {"front": ["11", 0]}
if side:
    g["20"] = {"class_type": "LoadImage", "inputs": {"image": side}}
    g["21"] = {"class_type": "CLIPVisionEncode", "inputs": {"clip_vision": ["1", 1], "image": ["20", 0], "crop": "center"}}
    mv_inputs["left"] = ["21", 0]
if back:
    g["30"] = {"class_type": "LoadImage", "inputs": {"image": back}}
    g["31"] = {"class_type": "CLIPVisionEncode", "inputs": {"clip_vision": ["1", 1], "image": ["30", 0], "crop": "center"}}
    mv_inputs["back"] = ["31", 0]

g["50"] = {"class_type": "Hunyuan3Dv2ConditioningMultiView", "inputs": mv_inputs}
g["60"] = {"class_type": "KSampler", "inputs": {
    "model": ["1", 0], "positive": ["50", 0], "negative": ["50", 1], "latent_image": ["40", 0],
    "seed": 20792, "steps": ARGS.steps, "cfg": 5.5,
    "sampler_name": "euler", "scheduler": "sgm_uniform", "denoise": 1.0}}
g["70"] = {"class_type": "VAEDecodeHunyuan3D", "inputs": {
    "samples": ["60", 0], "vae": ["1", 2], "num_chunks": 8000, "octree_resolution": 256}}
g["80"] = {"class_type": "VoxelToMesh", "inputs": {"voxel": ["70", 0], "algorithm": "surface net", "threshold": 0.6}}
g["90"] = {"class_type": "SaveGLB", "inputs": {"mesh": ["80", 0], "filename_prefix": "nurungi_v2/mesh"}}

pid = api("/prompt", {"prompt": g})["prompt_id"]
print("submitted:", pid)

for i in range(1800):  # 최대 30분
    hist = api(f"/history/{pid}")
    if pid in hist:
        status = hist[pid].get("status", {})
        if status.get("status_str") == "error":
            print(json.dumps(hist[pid], ensure_ascii=False)[:3000])
            raise SystemExit("생성 실패")
        # 출력 GLB 찾기
        out_dir = os.path.join(COMFY_OUTPUT, "nurungi_v2")
        if os.path.isdir(out_dir):
            glbs = sorted((os.path.join(out_dir, f) for f in os.listdir(out_dir) if f.endswith(".glb")),
                          key=os.path.getmtime)
            if glbs:
                dest_dir = os.path.join(HERE, "..", "art_raw", "mesh_out")
                os.makedirs(dest_dir, exist_ok=True)
                dest = os.path.join(dest_dir, f"{ARGS.out}.glb")
                shutil.copy(glbs[-1], dest)
                print("완료 →", os.path.abspath(dest))
                break
        print("done but no glb?", json.dumps(hist[pid].get("outputs", {}))[:500])
        break
    time.sleep(2)
    if i % 15 == 14:
        q = api("/queue")
        print(f"  대기 중... queue={len(q.get('queue_running', []))}+{len(q.get('queue_pending', []))}")
