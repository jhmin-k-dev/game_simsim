"""누룽이 자동 리깅 (09 §A-4) — Blender 헤드리스 실행용.

치비 2족(팔=앞다리) 하이브리드 리그: root/hips/spine/head/ear.L·R/tail/
arm_upper·hand.L·R/leg_upper·foot.L·R  (13본 + root)
본 배치는 바운딩박스 비례로 잡는다 (전고 0.9m, 발끝 y=0 전제 — decimate_glb.py 출력).

사용: blender --background --python blender_rig.py -- <in.obj> <out.fbx> <forward_sign>
  forward_sign: 모델이 +X를 보면 1, -X를 보면 -1 (Unity 검증값: -1)
"""
import sys
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, DST = argv[0], argv[1]
FWD = float(argv[2]) if len(argv) > 2 else -1.0   # 코가 향하는 X 부호

# ---- 초기화: 기본 씬 비우기 ----
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---- OBJ 임포트 ----
bpy.ops.wm.obj_import(filepath=SRC)
mesh_obj = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
mesh_obj.name = "Nurungi"

# 바운딩 박스 (월드)
bbox = [mesh_obj.matrix_world @ Vector(c) for c in mesh_obj.bound_box]
min_x = min(v.x for v in bbox); max_x = max(v.x for v in bbox)
min_y = min(v.y for v in bbox); max_y = max(v.y for v in bbox)
min_z = min(v.z for v in bbox); max_z = max(v.z for v in bbox)
H = max_z - min_z                      # Blender는 Z-up (OBJ 임포트 시 Y-up → Z-up 변환됨)
W = max_y - min_y                      # 좌우 폭 (OBJ x→? 축 매핑에 따라 다름)
print(f"[rig] bbox x[{min_x:.2f},{max_x:.2f}] y[{min_y:.2f},{max_y:.2f}] z[{min_z:.2f},{max_z:.2f}] H={H:.2f}")

# 축 정리: Blender OBJ 기본 임포트는 -Z forward, Y up → 씬은 Z-up.
# 원본(트리메시 출력)은 Y-up이므로 임포트 후: 원본 y → Blender z, 원본 z → Blender -y, x → x.
# 원본에서 코 방향이 X축이므로 Blender에서도 X축이 앞뒤, Y축이 좌우가 된다.
fx = FWD  # 코쪽 x 부호

def zh(f):  # 높이 비율 → z 좌표
    return min_z + H * f

side = (max_y - min_y) * 0.5

# ---- 아마추어 생성 ----
arm_data = bpy.data.armatures.new("NurungiRig")
arm_obj = bpy.data.objects.new("Armature", arm_data)
bpy.context.collection.objects.link(arm_obj)
bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.mode_set(mode="EDIT")

def bone(name, head, tail, parent=None):
    b = arm_data.edit_bones.new(name)
    b.head = Vector(head)
    b.tail = Vector(tail)
    if parent is not None:
        b.parent = arm_data.edit_bones[parent]
    return b

cx = (min_x + max_x) * 0.5             # 몸 중심 x (앞뒤 축)
cy = (min_y + max_y) * 0.5             # 좌우 중심

# 몸통 축 (치비: 머리가 상단 45%)
bone("root",      (cx, cy, 0.0),        (cx, cy, zh(0.08)))
bone("hips",      (cx, cy, zh(0.30)),   (cx, cy, zh(0.40)), "root")
bone("spine",     (cx, cy, zh(0.40)),   (cx, cy, zh(0.52)), "hips")
bone("head",      (cx, cy, zh(0.52)),   (cx, cy, zh(0.95)), "spine")

# 귀 (머리 꼭대기 양옆에서 바깥쪽 아래로 처짐 — 참조 모델은 늘어진 귀)
ear_z = zh(0.86)
bone("ear.L", (cx, cy + side * 0.55, ear_z), (cx, cy + side * 0.95, zh(0.70)), "head")
bone("ear.R", (cx, cy - side * 0.55, ear_z), (cx, cy - side * 0.95, zh(0.70)), "head")

# 꼬리 (코 반대쪽)
bone("tail", (cx - fx * side * 0.9, cy, zh(0.34)), (cx - fx * side * 1.5, cy, zh(0.42)), "hips")

# 팔 = 앞다리 (몸통 옆, 아래로 늘어짐)
for s, sign in (("L", 1), ("R", -1)):
    bone(f"arm_upper.{s}", (cx, cy + sign * side * 0.75, zh(0.44)),
                            (cx, cy + sign * side * 0.85, zh(0.30)), "spine")
    bone(f"hand.{s}",      (cx, cy + sign * side * 0.85, zh(0.30)),
                            (cx, cy + sign * side * 0.88, zh(0.20)), f"arm_upper.{s}")

# 다리 (짧고 굵은 스텁)
for s, sign in (("L", 1), ("R", -1)):
    bone(f"leg_upper.{s}", (cx, cy + sign * side * 0.38, zh(0.28)),
                            (cx, cy + sign * side * 0.38, zh(0.12)), "hips")
    bone(f"foot.{s}",      (cx, cy + sign * side * 0.38, zh(0.12)),
                            (cx + fx * side * 0.25, cy + sign * side * 0.38, zh(0.02)), f"leg_upper.{s}")

bpy.ops.object.mode_set(mode="OBJECT")

# ---- 자동 웨이트 ----
mesh_obj.select_set(True)
arm_obj.select_set(True)
bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
print("[rig] 자동 웨이트 완료")

# ---- FBX 익스포트 (Unity Generic용) ----
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=DST,
    use_selection=True,
    apply_scale_options="FBX_SCALE_ALL",
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward="-Z", axis_up="Y",
)
print(f"[rig] 완료 → {DST}")
