"""ComfyUI 강아지 GLB → 받침대 제거 + 데시메이션 → OBJ (Unity 네이티브 임포트).
목업용 초안 ~20k tris. 정식 리토폴로지(8k, 쿼드)는 별도 작업.
사용: python decimate_glb.py <in.glb> <out.obj> [target_tris]
"""
import sys
import numpy as np
import trimesh

src, dst = sys.argv[1], sys.argv[2]
target = int(sys.argv[3]) if len(sys.argv) > 3 else 20000
no_cut = "nocut" in sys.argv  # 받침대 없는 모델은 바닥 절단 생략

mesh = trimesh.load(src, force="mesh")
print(f"입력: {len(mesh.vertices):,} verts / {len(mesh.faces):,} tris")

if not no_cut:
    # ---- 받침대 제거: 바닥 근처 얇은 원반 = y 최저부 슬랩 ----
    y = mesh.vertices[:, 1]
    y0, y1 = y.min(), y.max()
    h = y1 - y0
    cut = y0 + h * 0.045  # 받침대 두께 추정 (전체 높이의 ~4.5%)
    face_y = y[mesh.faces].min(axis=1)
    keep = face_y > cut
    mesh.update_faces(keep)
    mesh.remove_unreferenced_vertices()

# 연결 요소 중 가장 큰 것만 (받침대 잔여물·부유 조각 제거)
parts = mesh.split(only_watertight=False)
if len(parts) > 1:
    parts = sorted(parts, key=lambda m: len(m.faces), reverse=True)
    print(f"연결 요소 {len(parts)}개 → 최대(faces {len(parts[0].faces):,})만 유지")
    mesh = parts[0]

# ---- 표면 노이즈 제거 (툰 아웃라인이 잔주름을 다 그려버리므로 필수) ----
try:
    # 잔주름만 걷어내고 주둥이·귀 같은 실루엣은 살린다 (과하면 얼굴이 뭉개짐)
    trimesh.smoothing.filter_taubin(mesh, lamb=0.5, nu=-0.53, iterations=6)
    print("Taubin 스무딩 적용")
except BaseException as e:
    print("스무딩 생략:", e)

# ---- 데시메이션 ----
try:
    dec = mesh.simplify_quadric_decimation(face_count=target)
except BaseException as e:
    print("quadric 실패, fast_simplification 시도:", e)
    import fast_simplification
    v, f = fast_simplification.simplify(mesh.vertices, mesh.faces, target_count=target)
    dec = trimesh.Trimesh(vertices=v, faces=f)

# 발바닥이 y=0에 오도록 + 실제 크기(어깨높이 ~0.55m 치비 기준 전고 0.9m)로 스케일
dec.vertices[:, 1] -= dec.vertices[:, 1].min()
height = dec.vertices[:, 1].max()
dec.vertices *= 0.9 / height
# 중심 정렬 (xz)
c = (dec.vertices[:, [0, 2]].max(0) + dec.vertices[:, [0, 2]].min(0)) / 2
dec.vertices[:, 0] -= c[0]
dec.vertices[:, 2] -= c[1]

trimesh.repair.fix_normals(dec)
dec.export(dst)
print(f"출력: {len(dec.vertices):,} verts / {len(dec.faces):,} tris → {dst}")
