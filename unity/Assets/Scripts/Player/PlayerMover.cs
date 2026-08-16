using Nurungi.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nurungi.Player
{
    /// <summary>
    /// 01_기획서 §3: 두 조작 방식 동시 공존 + 마지막 입력 규칙.
    /// A. 직접 조작(WASD/방향키, 나중에 가상 조이스틱) — 아날로그, 즉각
    /// B. 지점 이동(마우스 클릭, 나중에 터치 탭) — 클릭한 바닥으로 걸어감
    /// 규칙: 가장 최근 입력이 제어권을 가진다. 전환 시 속도를 0으로 만들지 않는다 (§3-2).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : MonoBehaviour
    {
        private enum ControlMode { None, Direct, MoveTarget }

        [Header("지점 이동 (01 §3-3)")]
        [SerializeField] private float runDistanceThreshold = 6f;  // 이상이면 자동 뛰기
        [SerializeField] private float walkApproachDistance = 2f;  // 도착 전 걷기 전환
        [SerializeField] private float arriveDistance = 0.15f;

        private CharacterController _cc;
        private UnityEngine.Camera _cam;
        private ControlMode _mode = ControlMode.None;
        private Vector3 _velocity;
        private float _verticalVel;
        private Vector3 _moveTarget;
        private bool _runLeg;          // 이번 지점 이동이 뛰기 구간인가 (출발 시 거리로 결정)
        private Transform _clickMarker;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cam = UnityEngine.Camera.main;
        }

        private void Update()
        {
            // ---- 입력 수집: 마지막 입력이 이긴다 (01 §3-2) ----
            Vector2 direct = ReadDirectInput();
            bool directActive = direct.sqrMagnitude > GameConstants.InputDeadZone * GameConstants.InputDeadZone;
            if (directActive)
            {
                // 직접 조작이 들어오면 지점 이동 즉시 취소. 속도는 유지 (§3-2)
                _mode = ControlMode.Direct;
                HideMarker();
            }

            if (TryReadClick(out Vector3 clickPoint))
            {
                // 클릭이 들어오면 현재 속도 유지한 채 목적지만 재설정 (§3-2)
                _mode = ControlMode.MoveTarget;
                _moveTarget = clickPoint;
                Vector3 planar = clickPoint - transform.position;
                planar.y = 0f;
                _runLeg = planar.magnitude >= runDistanceThreshold;
                ShowMarker(clickPoint);
            }

            // ---- 모드별 희망 이동 ----
            bool runHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            Vector3 wishDir = Vector3.zero;
            float maxSpeed = GameConstants.WalkSpeed;

            switch (_mode)
            {
                case ControlMode.Direct:
                    if (directActive)
                    {
                        wishDir = new Vector3(direct.x, 0f, direct.y);
                        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
                        maxSpeed = runHeld ? GameConstants.RunSpeed : GameConstants.WalkSpeed;
                    }
                    // 키를 뗐으면 wishDir=0 → 감속 정지
                    break;

                case ControlMode.MoveTarget:
                    Vector3 toTarget = _moveTarget - transform.position;
                    toTarget.y = 0f;
                    float dist = toTarget.magnitude;
                    if (dist <= arriveDistance)
                    {
                        _mode = ControlMode.None;
                        HideMarker();
                    }
                    else
                    {
                        wishDir = toTarget / dist;
                        // 01 §3-3: 출발 시 6m 이상이면 뛰는 구간, 도착 2m 전에는 걷기로
                        maxSpeed = _runLeg && dist > walkApproachDistance
                            ? GameConstants.RunSpeed
                            : GameConstants.WalkSpeed;
                    }
                    break;
            }

            // ---- 가감속 (01 §3-3) — 전환 시 속도 리셋 없음 ----
            bool run = maxSpeed > GameConstants.WalkSpeed + 0.01f;
            if (wishDir.sqrMagnitude > 0f)
            {
                _velocity = Vector3.MoveTowards(_velocity, wishDir * maxSpeed, GameConstants.MoveAccel * Time.deltaTime);
                float turnSpeed = run ? GameConstants.TurnSpeedRunDeg : GameConstants.TurnSpeedWalkDeg;
                Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
            else
            {
                _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, GameConstants.MoveDecel * Time.deltaTime);
            }

            // ---- 중력 + 이동 ----
            if (_cc.isGrounded) _verticalVel = -1f;
            else _verticalVel += Physics.gravity.y * Time.deltaTime;
            _cc.Move((_velocity + Vector3.up * _verticalVel) * Time.deltaTime);
        }

        // ---- 입력 ----

        private static Vector2 ReadDirectInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = 0f, y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }

        /// 마우스 클릭 지점의 바닥 좌표. (나중에 터치 탭이 같은 경로를 탄다 — 01 §3-1)
        private bool TryReadClick(out Vector3 point)
        {
            point = default;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return false;
            if (_cam == null) _cam = UnityEngine.Camera.main;
            if (_cam == null) return false;

            Ray ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 300f)) return false;

            point = hit.point;
            return true;
        }

        // ---- 클릭 지점 마커 ----

        private void ShowMarker(Vector3 position)
        {
            if (_clickMarker == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "ClickMarker";
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
                _clickMarker = go.transform;
            }
            _clickMarker.gameObject.SetActive(true);
            _clickMarker.position = position + Vector3.up * 0.02f;
        }

        private void HideMarker()
        {
            if (_clickMarker != null) _clickMarker.gameObject.SetActive(false);
        }
    }
}
