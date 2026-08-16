using Nurungi.Config;
using UnityEngine;

namespace Nurungi.Player
{
    /// <summary>
    /// 01_기획서 §4-1, §4-4: 하이브리드 보행.
    /// 평소 2족 직립 → 질주하거나 지치면 4족. 전환 0.3s, **전환 중에도 속도는 유지**.
    ///
    /// 애니메이션 클립이 아직 없으므로 시각 표현은 Visual 트랜스폼을 기울이는 것으로 대신한다
    /// (몸을 앞으로 숙이고 낮춘다). 클립이 들어오면 이 스크립트는 Animator 파라미터만 넘기고
    /// 아래 시각 보간은 제거한다 — 상태 기계 자체는 그대로 쓴다.
    /// </summary>
    public class StanceController : MonoBehaviour
    {
        public enum Stance { Biped, Quadruped }

        [SerializeField] private Transform visual;

        [Header("4족 자세 (클립 도입 전 임시 표현)")]
        [SerializeField] private float quadPitchDeg = 52f;   // 앞으로 숙이는 각
        [SerializeField] private float quadDropY = -0.16f;   // 몸이 낮아짐
        [SerializeField] private float quadForwardZ = 0.12f; // 무게중심 앞으로

        [Header("지침 연출 (01 §4-4 — 게이지·수치 없음)")]
        [SerializeField] private float sprintSecondsUntilTired = 6f;
        [SerializeField] private float tiredRecoverSeconds = 4f;

        /// 애니메이터 도입 시 이 값들을 그대로 파라미터로 넘긴다.
        public Stance Current { get; private set; } = Stance.Biped;
        public float QuadBlend { get; private set; }     // 0=2족, 1=4족
        public bool IsTired { get; private set; }
        public bool IsTransitioning => QuadBlend > 0.001f && QuadBlend < 0.999f;

        private float _sprintTimer;
        private float _restTimer;
        private Vector3 _visualBasePos;

        private void Awake()
        {
            if (visual == null && transform.childCount > 0)
                visual = transform.GetChild(0);
            if (visual != null) _visualBasePos = visual.localPosition;
        }

        /// PlayerMover가 매 프레임 호출. sprinting = 질주 중인가.
        public void Tick(bool sprinting, float deltaTime)
        {
            UpdateTiredness(sprinting, deltaTime);

            // 질주 중이거나 지쳐 있으면 4족 (01 §4-1)
            Stance want = (sprinting || IsTired) ? Stance.Quadruped : Stance.Biped;
            Current = want;

            // 0.3s 전환 (§3-3 스탠스 전환)
            float target = want == Stance.Quadruped ? 1f : 0f;
            float step = deltaTime / Mathf.Max(0.0001f, GameConstants.StanceTransitionSeconds);
            QuadBlend = Mathf.MoveTowards(QuadBlend, target, step);

            ApplyVisual();
        }

        private void UpdateTiredness(bool sprinting, float deltaTime)
        {
            if (sprinting)
            {
                _sprintTimer += deltaTime;
                _restTimer = 0f;
                if (_sprintTimer >= sprintSecondsUntilTired) IsTired = true;
            }
            else
            {
                _sprintTimer = 0f;
                if (IsTired)
                {
                    _restTimer += deltaTime;
                    if (_restTimer >= tiredRecoverSeconds)
                    {
                        IsTired = false;
                        _restTimer = 0f;
                    }
                }
            }
        }

        private void ApplyVisual()
        {
            if (visual == null) return;
            // SmoothStep으로 전환 곡선을 부드럽게 (선형이면 툭 꺾여 보임)
            float t = Mathf.SmoothStep(0f, 1f, QuadBlend);
            visual.localRotation = Quaternion.Euler(quadPitchDeg * t, 0f, 0f);
            visual.localPosition = _visualBasePos + new Vector3(0f, quadDropY * t, quadForwardZ * t);
        }

        /// 지침 상태를 즉시 푼다 (챕터 전환 등)
        public void ResetFatigue()
        {
            IsTired = false;
            _sprintTimer = 0f;
            _restTimer = 0f;
        }
    }
}
