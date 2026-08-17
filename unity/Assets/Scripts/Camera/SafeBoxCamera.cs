using Nurungi.Config;
using UnityEngine;

namespace Nurungi.CameraSystem
{
    /// <summary>
    /// 02_기술사양 §2-3 SafeBoxCamera (원근 버전).
    /// 대상이 화면 중앙의 안전 상자 안에 있으면 카메라는 정지.
    /// 벗어난 축만, 뷰포트 좌표 기준으로 따라간다 (원근이라 월드 거리가 아니라 뷰포트로 판정).
    /// 회전은 고정 (부감 15°, 03 §1-1 Aim: Do Nothing).
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class SafeBoxCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("안전 상자 (뷰포트 half-extents) — 02 §2-3")]
        [SerializeField] private float halfX = GameConstants.SafeBoxHalfX;
        [SerializeField] private float halfY = GameConstants.SafeBoxHalfY;
        [SerializeField] private float centerX = GameConstants.SafeBoxCenterX;
        [SerializeField] private float centerY = GameConstants.SafeBoxCenterY;

        [Header("추적 — 02 §2-3 4~6")]
        [SerializeField] private float dampX = GameConstants.CameraDampX;
        [SerializeField] private float dampY = GameConstants.CameraDampY;
        [SerializeField] private float maxLead = GameConstants.CameraMaxLead;
        [SerializeField] private float leadBlend = GameConstants.CameraLeadBlend;

        /// 점프 중이면 외부(이동 컨트롤러)가 false로 내려줌 → 세로 추적 0.6배 (02 §2-3 5)
        public bool TargetGrounded { get; set; } = true;

        private UnityEngine.Camera _cam;
        private Vector3 _desired;        // 목표 카메라 위치
        private Vector3 _dampVelocity;   // SmoothDamp 내부 상태
        private Vector3 _lead;           // 현재 리드 오프셋
        private Vector3 _leadVelocity;
        private Vector3 _lastTargetPos;
        private bool _portrait;
        private float _baseDistanceScale = 1f;

        /// 스크립트(연출)에서 덮어쓰는 값. 기본값은 02 §2-1 상수.
        public float ScriptDistance { get; set; } = GameConstants.CameraDistance;
        public float ScriptHeight { get; set; } = GameConstants.CameraHeight;

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            _cam.fieldOfView = GameConstants.CameraFov;
            transform.rotation = Quaternion.Euler(GameConstants.CameraPitchDeg, 0f, 0f);
        }

        private void Start()
        {
            if (target == null) return;
            _lastTargetPos = target.position;
            SnapToTarget();
        }

        private Nurungi.Player.PlayerMover _mover;

        public void SetTarget(Transform t)
        {
            target = t;
            _mover = t != null ? t.GetComponent<Nurungi.Player.PlayerMover>() : null;
            if (isActiveAndEnabled && t != null)
            {
                _lastTargetPos = t.position;
                SnapToTarget();
            }
        }

        // 스냅 시점의 기준값 — 세로·깊이 추적은 월드 기준 오프셋으로만 한다
        private float _snapDogY;
        private float _snapDogZ;
        private Vector3 _snapCamPos;

        /// 초기 배치: 대상이 안전 상자 중심(뷰포트 centerX/centerY)에 오도록 즉시 이동
        private void SnapToTarget()
        {
            transform.position = ComputeCenteredPosition();
            _desired = transform.position;
            _dampVelocity = Vector3.zero;
            _lead = Vector3.zero;
            _snapDogY = target.position.y;
            _snapDogZ = target.position.z;
            _snapCamPos = transform.position;
        }

        private Vector3 ComputeCenteredPosition()
        {
            if (_cam == null) _cam = GetComponent<UnityEngine.Camera>(); // 에디터(Awake 이전) 호출 대비
            float dist = ScriptDistance * _baseDistanceScale;
            // 회전 고정 상태에서 distance만큼 뒤로 물러난 기본 위치
            Vector3 pos = target.position - transform.forward * dist;
            // 대상을 뷰포트 (centerX, centerY)에 놓기 위한 오프셋 보정
            float depth = Vector3.Dot(target.position - pos, transform.forward);
            Vector2 world = WorldPerViewport(depth);
            pos += transform.right * (0.5f - centerX) * world.x;
            pos += transform.up * (0.5f - centerY) * world.y;
            pos.y += ScriptHeight - GameConstants.CameraHeight; // 스크립트 높이 오프셋
            return pos;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdatePortraitMode();

            // 점프 중이면 세로 추적을 눌러 붙인다 (02 §2-3 5)
            if (_mover == null && target != null) _mover = target.GetComponent<Nurungi.Player.PlayerMover>();
            if (_mover != null) TargetGrounded = _mover.IsGrounded;

            // ---- 속도 리드 (02 §2-3 6): 이동 방향으로 최대 1.5m 선행, 0.4s 블렌드 ----
            Vector3 targetVel = (target.position - _lastTargetPos) / dt;
            _lastTargetPos = target.position;
            Vector3 planarVel = new Vector3(targetVel.x, 0f, targetVel.z);
            Vector3 desiredLead = Vector3.ClampMagnitude(planarVel * 0.5f, maxLead);
            _lead = Vector3.SmoothDamp(_lead, desiredLead, ref _leadVelocity, leadBlend);

            Vector3 focus = target.position + _lead;

            // ---- 가로(X): 뷰포트 안전 상자 판정 (02 §2-3 1~3) ----
            // 주의 1: 목표는 반드시 "현재 카메라 위치 기준 절대값"으로 계산한다.
            //   누적(+=) 방식은 대상이 계속 이동하면 초과분이 중복 가산되어 수조 단위로 발산했다 (2026-08-18).
            // 주의 2: 세로는 뷰포트로 판정하면 안 된다 — 대상이 깊이(z)로 다가올 때 화면상 내려가는 것을
            //   "아래 이동"으로 착각해 카메라가 땅속으로 파고든다.
            Vector3 vp = _cam.WorldToViewportPoint(focus);
            if (vp.z > 0f)
            {
                Vector2 world = WorldPerViewport(vp.z);
                float dx = OverflowAmount(vp.x, centerX, halfX);
                _desired.x = dx != 0f
                    ? transform.position.x + dx * world.x   // 상자 경계에 딱 놓이는 절대 위치
                    : transform.position.x;                  // 상자 안 = 정지 (02 §2-3 2)
            }

            // ---- 세로(Y)·깊이(Z): 월드 기준 오프셋 추적 (횡스크롤 표준) ----
            // 점프 등 실제 높이 변화만 60% 반영, 깊이 이동은 카메라가 같이 밀려 구도 유지
            _desired.y = _snapCamPos.y + (focus.y - _snapDogY) * 0.6f;
            _desired.z = _snapCamPos.z + (focus.z - _snapDogZ);

            // ---- SmoothDamp: 가로 빠르게, 세로 느리게 (02 §2-3 4~5) ----
            float yDamp = dampY * (TargetGrounded ? 1f : 1f / GameConstants.JumpVerticalDampMul);
            Vector3 pos = transform.position;
            pos.x = Mathf.SmoothDamp(pos.x, _desired.x, ref _dampVelocity.x, dampX);
            pos.y = Mathf.SmoothDamp(pos.y, _desired.y, ref _dampVelocity.y, yDamp);
            pos.z = Mathf.SmoothDamp(pos.z, _desired.z, ref _dampVelocity.z, 0.35f);
            transform.position = pos;
        }

        /// 상자 밖으로 벗어난 뷰포트 양 (안이면 0)
        private static float OverflowAmount(float v, float center, float half)
        {
            float min = center - half;
            float max = center + half;
            if (v < min) return v - min;
            if (v > max) return v - max;
            return 0f;
        }

        /// depth 위치에서 뷰포트 1.0이 덮는 월드 크기 (가로, 세로)
        private Vector2 WorldPerViewport(float depth)
        {
            float h = 2f * depth * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return new Vector2(h * _cam.aspect, h);
        }

        /// 세로 모드 전환 (02 §2-4): 화면비 < 1이면 거리 1.35배, 상자 교체
        private void UpdatePortraitMode()
        {
            bool portrait = _cam.aspect < 1f;
            if (portrait == _portrait) return;
            _portrait = portrait;
            if (portrait)
            {
                halfX = GameConstants.PortraitSafeBoxHalfX;
                halfY = GameConstants.PortraitSafeBoxHalfY;
                _baseDistanceScale = GameConstants.PortraitDistanceMul;
            }
            else
            {
                halfX = GameConstants.SafeBoxHalfX;
                halfY = GameConstants.SafeBoxHalfY;
                _baseDistanceScale = 1f;
            }
            if (target != null) SnapToTarget();
        }

        /// 씬 뷰에서 안전 상자 시각화 (선택 시)
        private void OnDrawGizmosSelected()
        {
            if (_cam == null) _cam = GetComponent<UnityEngine.Camera>();
            if (_cam == null || target == null) return;
            float depth = Vector3.Dot(target.position - transform.position, transform.forward);
            if (depth <= 0f) return;
            Vector2 world = WorldPerViewport(depth);
            Vector3 boxCenter = transform.position + transform.forward * depth
                + transform.right * (centerX - 0.5f) * world.x
                + transform.up * (centerY - 0.5f) * world.y;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(boxCenter, new Vector3(world.x * halfX * 2f, world.y * halfY * 2f, 0.01f));
        }
    }
}
