using Nurungi.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nurungi.Player
{
    /// <summary>
    /// 01_기획서 §3: 두 조작 방식 동시 공존 + 마지막 입력 규칙.
    /// A. 직접 조작(WASD/방향키/좌스틱) — 아날로그, 즉각
    /// B. 지점 이동(마우스 클릭 → 나중에 터치 탭) — 클릭한 바닥으로 걸어감
    /// 규칙: 가장 최근 입력이 제어권을 가진다. **전환 시 속도를 0으로 만들지 않는다** (§3-2).
    ///
    /// 질주(§3-3)는 StanceController를 통해 4족으로 전환되며, 전환 중에도 속도는 유지된다.
    /// 점프는 §4-4. 버퍼·코요테 타임으로 입력이 씹히지 않게 한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : MonoBehaviour
    {
        private enum ControlMode { None, Direct, MoveTarget }

        [Header("지점 이동 (01 §3-3)")]
        [SerializeField] private float runDistanceThreshold = 6f;  // 이상이면 자동 질주
        [SerializeField] private float walkApproachDistance = 2f;  // 도착 전 걷기 전환
        [SerializeField] private float arriveDistance = 0.15f;

        [Header("참조")]
        [SerializeField] private StanceController stance;

        private CharacterController _cc;
        private UnityEngine.Camera _cam;
        private ControlMode _mode = ControlMode.None;

        private Vector3 _velocity;      // 수평 속도
        private float _verticalVel;
        private Vector3 _moveTarget;
        private bool _runLeg;           // 이번 지점 이동이 질주 구간인가
        private Transform _clickMarker;

        private float _jumpBufferTimer;
        private float _coyoteTimer;
        private bool _wasGrounded = true;
        private Vector3 _lastGroundedPos;   // 낙사 복구용

        /// SafeBoxCamera가 점프 중 세로 추적을 줄이는 데 쓴다 (02 §2-3 5)
        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public bool IsSprinting { get; private set; }

        /// 절차적 레이어(ProceduralMotion)가 읽는 상태
        public float HorizontalSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;
        public float VerticalVelocity => _verticalVel;

        // ---- 대시 (토글 + 게이지, 2026-08-18) ----
        private float _dashGauge = GameConstants.DashMaxSeconds;
        private bool _dashOn;
        public float DashNormalized => _dashGauge / GameConstants.DashMaxSeconds;
        public bool DashOn => _dashOn;

        // ---- 넉백 (차 충돌 슬랩스틱 — §12: 실패 아님, 연출) ----
        private bool _knocked;          // 붕 떠서 나는 중
        private float _recoverTimer;    // 착지 후 어지러움 (입력 잠금)
        private ProceduralMotion _proc;
        public bool IsKnocked => _knocked || _recoverTimer > 0f;

        /// 차 등에 치였을 때: 포물선으로 날아가 데굴데굴 → 착지 → 어지러움 → 회복
        public void Knockback(Vector3 launchVelocity, float tumbleDir)
        {
            if (_knocked) return;
            _knocked = true;
            _mode = ControlMode.None;
            _dashOn = false;
            HideMarker();
            _velocity = new Vector3(launchVelocity.x, 0f, launchVelocity.z);
            _verticalVel = launchVelocity.y;
            if (_proc == null) _proc = GetComponent<ProceduralMotion>();
            if (_proc != null) _proc.TumbleSpeed = 640f * tumbleDir;

            // 연출: 히트스톱 + 카메라 셰이크 + 충격 버스트
            World.HitEffects.HitStop(0.09f);
            World.HitEffects.ImpactBurst(transform.position + Vector3.up * 0.25f);
            var cam = UnityEngine.Camera.main;
            var sbx = cam != null ? cam.GetComponent<CameraSystem.SafeBoxCamera>() : null;
            if (sbx != null) sbx.Shake(0.55f, 0.3f);
        }

        private void UpdateKnocked(float dt)
        {
            // 공기 저항 살짝 + 중력(만화식으로 20% 가볍게 — 체공감)
            _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, 1.4f * dt);
            _verticalVel += GameConstants.Gravity * 0.8f * dt;
            _cc.Move((_velocity + Vector3.up * _verticalVel) * dt);

            if (_cc.isGrounded && _verticalVel < 0f)
            {
                // 착지: 구르기 종료, 어지러움 시작
                _knocked = false;
                _recoverTimer = 1.15f;
                _velocity = Vector3.zero;
                _verticalVel = -1f;
                if (_proc != null) _proc.TumbleSpeed = 0f;
                World.HitEffects.DizzyStars(transform, 1.15f,
                    _cc.height + 0.12f);
                var camMain = UnityEngine.Camera.main;
                var sbx2 = camMain != null ? camMain.GetComponent<CameraSystem.SafeBoxCamera>() : null;
                if (sbx2 != null) sbx2.Shake(0.25f, 0.12f);
            }
        }

        /// 스크립트(연출) 제어 중에는 플레이어 입력을 받지 않는다
        public bool ExternalControl { get; set; }

        /// 스크립트에서 점프시키기 (dog jump)
        public void RequestScriptedJump()
        {
            _verticalVel = Mathf.Sqrt(2f * -GameConstants.Gravity * GameConstants.JumpHeight);
            _scriptedAirborne = true;
        }

        private bool _scriptedAirborne;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cam = UnityEngine.Camera.main;
            if (stance == null) stance = GetComponent<StanceController>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_knocked)
            {
                UpdateKnocked(dt);
                return;
            }
            if (_recoverTimer > 0f)
            {
                // 어지러움: 입력 잠금, 제자리 비틀
                _recoverTimer -= dt;
                _velocity = Vector3.zero;
                if (_cc.isGrounded) _verticalVel = -1f;
                else _verticalVel += GameConstants.Gravity * dt;
                _cc.Move(Vector3.up * _verticalVel * dt);
                if (stance != null) stance.Tick(false, dt);
                return;
            }

            if (ExternalControl)
            {
                // 스크립트가 수평 이동을 맡는다. 중력·점프만 여기서 처리
                UpdateScriptedVertical(dt);
                return;
            }

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
            Vector3 wishDir = Vector3.zero;
            float maxSpeed = GameConstants.WalkSpeed;

            // ---- 대시 토글 (2026-08-18): 누르면 켜짐, 게이지 소진 시 자동 꺼짐, 다시 눌러야 재대시 ----
            if (WasDashPressed())
            {
                if (_dashOn) _dashOn = false;                                   // 수동 해제
                else if (_dashGauge >= GameConstants.DashMinToStart) _dashOn = true;
            }

            switch (_mode)
            {
                case ControlMode.Direct:
                    if (directActive)
                    {
                        wishDir = new Vector3(direct.x, 0f, direct.y);
                        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
                        maxSpeed = _dashOn ? GameConstants.RunSpeed : GameConstants.WalkSpeed;
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
                        // 클릭 이동도 대시 토글이 켜져 있을 때만 질주 (도착 직전엔 걷기)
                        maxSpeed = _dashOn && dist > walkApproachDistance
                            ? GameConstants.RunSpeed
                            : GameConstants.WalkSpeed;
                    }
                    break;
            }

            bool sprinting = maxSpeed > GameConstants.WalkSpeed + 0.01f && wishDir.sqrMagnitude > 0f;
            IsSprinting = sprinting;

            // ---- 게이지: 대시 중 소모(이동할 때만), 아니면 회복 ----
            if (sprinting)
            {
                _dashGauge -= dt;
                if (_dashGauge <= 0f)
                {
                    _dashGauge = 0f;
                    _dashOn = false;   // 소진 → 자동 해제, 다시 눌러야 함
                }
            }
            else
            {
                _dashGauge = Mathf.Min(GameConstants.DashMaxSeconds,
                    _dashGauge + GameConstants.DashRegenPerSec * dt);
            }

            // ---- 가감속 (§3-3) — 모드/스탠스 전환에서 속도 리셋 없음 ----
            bool grounded = _cc.isGrounded;
            float control = grounded ? 1f : GameConstants.AirControlMul;

            if (wishDir.sqrMagnitude > 0f)
            {
                _velocity = Vector3.MoveTowards(_velocity, wishDir * maxSpeed,
                    GameConstants.MoveAccel * control * dt);

                float turnSpeed = sprinting ? GameConstants.TurnSpeedRunDeg : GameConstants.TurnSpeedWalkDeg;
                Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * control * dt);
            }
            else
            {
                _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, GameConstants.MoveDecel * control * dt);
            }

            UpdateJump(grounded, dt);

            // ---- 스탠스 (01 §4-4) ----
            if (stance != null) stance.Tick(sprinting, dt);

            _cc.Move((_velocity + Vector3.up * _verticalVel) * dt);

            if (grounded) _lastGroundedPos = transform.position;
            FallGuard();
            _wasGrounded = grounded;
        }

        /// 맵 밖 낙사 안전망: 경계벽이 놓쳐도 마지막 접지 지점으로 복귀
        private void FallGuard()
        {
            if (transform.position.y > -3f) return;
            Vector3 back = _lastGroundedPos != Vector3.zero ? _lastGroundedPos : new Vector3(4f, 0.3f, 0.3f);
            _cc.enabled = false;
            transform.position = back + Vector3.up * 0.2f;
            _cc.enabled = true;
            _velocity = Vector3.zero;
            _verticalVel = -1f;
        }

        /// 스크립트 제어 중의 중력·점프. 수평은 ScriptAction이 직접 옮긴다.
        private void UpdateScriptedVertical(float dt)
        {
            bool grounded = _cc.isGrounded;
            if (grounded && _verticalVel < 0f)
            {
                _verticalVel = -2f;
                _scriptedAirborne = false;
            }
            _verticalVel += GameConstants.Gravity * dt;
            _velocity = Vector3.zero;
            _cc.Move(Vector3.up * _verticalVel * dt);
        }

        // ---- 점프 (01 §4-4) ----

        private void UpdateJump(bool grounded, float dt)
        {
            if (grounded)
            {
                _coyoteTimer = GameConstants.CoyoteSeconds;
                if (_verticalVel < 0f) _verticalVel = -2f; // 접지 유지
            }
            else
            {
                _coyoteTimer -= dt;
            }

            if (WasJumpPressed()) _jumpBufferTimer = GameConstants.JumpBufferSeconds;
            else _jumpBufferTimer -= dt;

            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                // v = sqrt(2gh) — 목표 높이에서 역산
                _verticalVel = Mathf.Sqrt(2f * -GameConstants.Gravity * GameConstants.JumpHeight);
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }

            _verticalVel += GameConstants.Gravity * dt;
        }

        // ---- 입력 ----

        private static Vector2 ReadDirectInput()
        {
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
            }
            // 게임패드 좌스틱 (아날로그 깊이가 질주 판정에 쓰인다)
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > v.sqrMagnitude) v = stick;
            }
            return v;
        }

        /// 대시 토글 버튼: Shift 또는 패드 X (누르는 순간만)
        private static bool WasDashPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.leftShiftKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonWest.wasPressedThisFrame;
        }

        private static bool WasJumpPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        /// 마우스 클릭 지점의 바닥 좌표. 나중에 터치 탭이 같은 경로를 탄다 (01 §3-1)
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
                Destroy(go.GetComponent<Collider>());
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
