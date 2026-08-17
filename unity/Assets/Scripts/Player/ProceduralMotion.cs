using Nurungi.Config;
using UnityEngine;

namespace Nurungi.Player
{
    /// <summary>
    /// 01_기획서 §4-3 절차적 레이어. 판정 기준은 04 §1-4.
    /// "애니메이션 클립을 새로 넣을 때마다 이 6개가 그 위에서 정상 동작하는지 확인한다."
    ///
    /// Visual 트랜스폼의 위치·회전·스케일을 매 프레임 **여기서만** 합성한다
    /// (StanceController.DriveVisual = false로 꺼서 이중 쓰기를 막음).
    ///
    /// 리깅 전 한계: ①발IK는 지면 경사 정렬로, ②룩앳은 몸 전체 요(yaw)로 근사.
    /// 본이 생기면 각 레이어의 출력 대상만 본으로 바꾼다 — 상태·타이밍 로직은 그대로.
    /// </summary>
    public class ProceduralMotion : MonoBehaviour
    {
        [SerializeField] private Transform visual;

        [Header("① 지면 정렬 (발 IK 전 단계)")]
        [SerializeField] private float slopeTiltMaxDeg = 18f;
        [SerializeField] private float slopeSmooth = 8f;

        [Header("② 룩앳 (관심 지점으로 몸을 살짝 틈)")]
        [SerializeField] private float lookYawMaxDeg = 28f;
        [SerializeField] private float lookSmooth = 4f;

        [Header("④ 숨쉬기 — 정지 시에만")]
        [SerializeField] private float breathScale = 0.016f;
        [SerializeField] private float breathPeriod = 2.6f;

        [Header("⑤ 깜빡임 (3~6초 랜덤)")]
        [SerializeField] private Vector2 blinkInterval = new Vector2(3f, 6f);

        [Header("⑥ 스쿼시&스트레치 (01 §4-3: ±8%, 0.12초)")]
        [SerializeField] private float landSquashMax = 0.08f;
        [SerializeField] private float squashHalfLife = 0.12f;

        [Header("[임시] 걷기 뒤뚱거림 — 걷기 클립(09 §A-6) 도입 시 제거")]
        [SerializeField] private float waddleRollDeg = 4.5f;
        [SerializeField] private float waddleBobMeters = 0.028f;
        [SerializeField] private float stepsPerMeter = 1.7f;

        /// 깜빡임 이벤트 — 얼굴 시트(09 §A-5)가 들어오면 여기 연결
        public event System.Action Blinked;
        public bool EyesClosed { get; private set; }

        private PlayerMover _mover;
        private StanceController _stance;
        private Vector3 _basePos;
        private Vector3 _baseScale;

        private float _slopePitch;         // ① 현재 경사 각
        private float _lookYaw;            // ② 현재 관심 요
        private float _breathPhase;        // ④
        private float _blinkTimer;         // ⑤
        private float _blinkHold;
        private float _squash;             // ⑥ 양수=눌림, 음수=늘어남
        private float _stridePhase;        // 뒤뚱거림 위상 (이동 거리 기반)
        private float _prevVerticalVel;
        private bool _wasGrounded = true;
        private Vector3 _lastPos;
        private float _smoothedSpeed;

        private bool _initialized;

        private void Awake() => EnsureInit();

        /// Awake가 돌지 않는 상황(에디터 시뮬레이션)에서도 안전하게
        private void EnsureInit()
        {
            if (_initialized) return;
            _mover = GetComponent<PlayerMover>();
            _stance = GetComponent<StanceController>();
            if (visual == null && transform.childCount > 0)
                visual = transform.GetChild(0);
            if (visual == null) return;
            _basePos = visual.localPosition;
            _baseScale = visual.localScale;
            _lastPos = transform.position;
            if (_stance != null) _stance.DriveVisual = false;
            ResetBlinkTimer();
            _initialized = true;
        }

        private void LateUpdate() => Tick(Time.deltaTime);

        /// 수동 dt 공급용 (테스트·시뮬레이션)
        public void Tick(float dt)
        {
            EnsureInit();
            if (visual == null || dt <= 0f) return;

            // 속도는 실제 이동량으로 측정 — 플레이어 조작·스크립트(DSL)·외부 이동 모두 잡힌다
            Vector3 planar = transform.position - _lastPos;
            planar.y = 0f;
            float measured = planar.magnitude / dt;
            _lastPos = transform.position;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, Mathf.Min(measured, 12f), 1f - Mathf.Exp(-10f * dt));
            float speed = Mathf.Max(_smoothedSpeed, _mover != null ? _mover.HorizontalSpeed : 0f);
            bool grounded = _mover == null || _mover.IsGrounded;
            // CharacterController.isGrounded는 Move() 이후에만 유효 — 첫 프레임·시뮬레이션 폴백
            if (!grounded && _mover != null && Mathf.Abs(_mover.VerticalVelocity) < 0.01f)
                grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.25f);
            bool moving = speed > 0.1f;
            float quad = _stance != null ? _stance.QuadBlend : 0f;

            TickGroundTilt(grounded, moving, dt);       // ①
            TickLook(moving, dt);                        // ②
            // ③ 스프링본: SpringBone 컴포넌트가 본 체인에 붙으면 스스로 동작 (리깅 대기)
            TickBreath(moving, grounded, dt);            // ④
            TickBlink(dt);                               // ⑤
            TickSquash(grounded, dt);                    // ⑥
            TickWaddle(speed, grounded, dt);             // [임시]

            Compose(quad);

            _prevVerticalVel = _mover != null ? _mover.VerticalVelocity : 0f;
            _wasGrounded = grounded;
        }

        // ---- ① 지면 정렬: 진행 방향 앞뒤 레이캐스트로 경사를 읽어 몸을 기울인다 ----
        private void TickGroundTilt(bool grounded, bool moving, float dt)
        {
            float target = 0f;
            if (grounded)
            {
                Vector3 fwd = transform.forward;
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                const float probe = 0.35f;
                // 자기 콜라이더(CharacterController)를 제외하고 지면을 찾는다
                if (RaycastGroundIgnoreSelf(origin + fwd * probe, out float yF) &&
                    RaycastGroundIgnoreSelf(origin - fwd * probe, out float yB))
                {
                    float dy = yF - yB;
                    target = -Mathf.Atan2(dy, probe * 2f) * Mathf.Rad2Deg; // 오르막이면 몸을 뒤로
                    target = Mathf.Clamp(target, -slopeTiltMaxDeg, slopeTiltMaxDeg);
                }
            }
            _slopePitch = Mathf.Lerp(_slopePitch, target, 1f - Mathf.Exp(-slopeSmooth * dt));
        }

        private bool RaycastGroundIgnoreSelf(Vector3 origin, out float groundY)
        {
            groundY = 0f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 2.5f);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.transform.root == transform.root) continue; // 자기 자신 무시
                if (h.distance < best) { best = h.distance; groundY = h.point.y; found = true; }
            }
            return found;
        }

        // ---- ② 룩앳: 관심 지점 쪽으로 몸을 살짝 틀어 시선을 표현 ----
        private void TickLook(bool moving, float dt)
        {
            float target = 0f;
            var interest = LookInterestPoint.FindBest(transform.position);
            if (interest != null)
            {
                Vector3 to = interest.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                {
                    float yaw = Vector3.SignedAngle(transform.forward, to.normalized, Vector3.up);
                    // 이동 중엔 약하게, 정지 시 충분히 (걷는 방향을 침범하지 않게)
                    float max = moving ? lookYawMaxDeg * 0.45f : lookYawMaxDeg;
                    target = Mathf.Clamp(yaw, -max, max);
                }
            }
            _lookYaw = Mathf.Lerp(_lookYaw, target, 1f - Mathf.Exp(-lookSmooth * dt));
        }

        // ---- ④ 숨쉬기: 멈춰 있을 때만 가슴이 부풀듯 ----
        private void TickBreath(bool moving, bool grounded, float dt)
        {
            bool idle = !moving && grounded;
            _breathPhase += dt / breathPeriod * Mathf.PI * 2f * (idle ? 1f : 2.2f);
            // 이동 중엔 진폭 0으로 (호흡은 Compose에서 idle 가중치 곱함)
            _breathIdleWeight = Mathf.Lerp(_breathIdleWeight, idle ? 1f : 0f, 1f - Mathf.Exp(-6f * dt));
        }
        private float _breathIdleWeight;

        // ---- ⑤ 깜빡임: 3~6초 랜덤, 0.09초 감김 ----
        private void TickBlink(float dt)
        {
            if (EyesClosed)
            {
                _blinkHold -= dt;
                if (_blinkHold <= 0f) EyesClosed = false;
                return;
            }
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                EyesClosed = true;
                _blinkHold = 0.09f;
                Blinked?.Invoke();
                ResetBlinkTimer();
            }
        }

        private void ResetBlinkTimer() => _blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);

        // ---- ⑥ 스쿼시&스트레치: 착지 눌림 + 점프 늘어남, 감쇠 복원 ----
        private void TickSquash(bool grounded, float dt)
        {
            if (grounded && !_wasGrounded)
            {
                // 착지: 낙하 속도에 비례해 눌림
                float impact = Mathf.Clamp01(-_prevVerticalVel / 8f);
                _squash = Mathf.Max(_squash, landSquashMax * impact / 0.5f * 0.5f);
                _squash = Mathf.Min(_squash, landSquashMax);
            }
            else if (!grounded && _wasGrounded && _mover != null && _mover.VerticalVelocity > 1f)
            {
                // 점프 이륙: 살짝 늘어남
                _squash = -landSquashMax * 0.6f;
            }
            // 반감기 기반 감쇠 (0.12초에 절반)
            _squash *= Mathf.Pow(0.5f, dt / squashHalfLife);
        }

        // ---- [임시] 걷기 뒤뚱거림: 이동 거리에 위상을 걸어 좌우 롤 + 상하 밥 ----
        private void TickWaddle(float speed, bool grounded, float dt)
        {
            if (grounded && speed > 0.1f)
                _stridePhase += speed * stepsPerMeter * dt * Mathf.PI * 2f;
            _waddleWeight = Mathf.Lerp(_waddleWeight, grounded && speed > 0.1f ? 1f : 0f,
                1f - Mathf.Exp(-8f * dt));
        }
        private float _waddleWeight;

        // ---- 합성: 스탠스 → 경사 → 룩 → 뒤뚱 → 숨 → 스쿼시 순서로 겹친다 ----
        private void Compose(float quad)
        {
            float quadT = Mathf.SmoothStep(0f, 1f, quad);

            // 회전: 스탠스 피치 + 경사 피치 + 관심 요 + 뒤뚱 롤
            float stancePitch = (_stance != null ? _stance.QuadPitchDeg : 52f) * quadT;
            float roll = Mathf.Sin(_stridePhase) * waddleRollDeg * _waddleWeight * (1f - quadT * 0.5f);
            visual.localRotation = Quaternion.Euler(stancePitch + _slopePitch, _lookYaw, roll);

            // 위치: 스탠스 오프셋 + 뒤뚱 밥
            float bob = Mathf.Abs(Mathf.Sin(_stridePhase)) * waddleBobMeters * _waddleWeight;
            Vector3 stanceOffset = new Vector3(
                0f,
                (_stance != null ? _stance.QuadDropY : -0.16f) * quadT + bob,
                (_stance != null ? _stance.QuadForwardZ : 0.12f) * quadT);
            visual.localPosition = _basePos + stanceOffset;

            // 스케일: 숨쉬기 + 스쿼시 (Y 줄면 XZ 늘어 부피 보존 느낌)
            float breath = Mathf.Sin(_breathPhase) * breathScale * _breathIdleWeight;
            float y = 1f + breath - _squash;
            float xz = 1f - breath * 0.5f + _squash * 0.6f;
            visual.localScale = new Vector3(_baseScale.x * xz, _baseScale.y * y, _baseScale.z * xz);
        }
    }
}
