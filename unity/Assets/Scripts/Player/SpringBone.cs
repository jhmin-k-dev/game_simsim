using UnityEngine;

namespace Nurungi.Player
{
    /// <summary>
    /// ③ 스프링본 (01 §4-3): 귀·꼬리가 움직임의 관성으로 흔들린다.
    /// UnityChan SpringBone과 같은 원리의 단순 구현 — 본 하나가 목표 방향으로
    /// 스프링 복원하며 따라온다. 리깅된 모델의 귀·꼬리 본 체인에 붙인다.
    ///
    /// ⚠ 현재 모델(통짜 메시)에는 본이 없어 대기 상태.
    ///    리깅(09 §A-4) 후: 귀 2개·꼬리 3~4마디에 이 컴포넌트를 체인으로 추가.
    ///    판정(04 §1-4 3): 급정지·급회전 시 관성으로 늦게 따라오고, 진동이 발산하지 않을 것.
    /// </summary>
    public class SpringBone : MonoBehaviour
    {
        [Tooltip("복원력 — 클수록 빨리 제자리로")]
        public float stiffness = 180f;

        [Tooltip("감쇠 — 낮으면 출렁, 높으면 뻣뻣. 진동 발산 방지의 핵심")]
        [Range(0f, 1f)] public float damping = 0.35f;

        [Tooltip("본이 뻗은 로컬 축")]
        public Vector3 boneAxis = new Vector3(0f, 0f, 1f);

        [Tooltip("본 길이 (m)")]
        public float boneLength = 0.12f;

        [Tooltip("중력이 처지게 만드는 정도")]
        public float gravityInfluence = 0.4f;

        private Quaternion _initialLocalRotation;
        private Vector3 _tipPos;       // 본 끝의 현재 위치 (버렛 적분)
        private Vector3 _tipVelocity;

        private void Start()
        {
            _initialLocalRotation = transform.localRotation;
            _tipPos = TipTarget();
            _tipVelocity = Vector3.zero;
        }

        private Vector3 TipTarget()
        {
            return transform.position + (transform.parent != null
                ? transform.parent.rotation * (_initialLocalRotation * boneAxis.normalized) * boneLength
                : transform.rotation * boneAxis.normalized * boneLength);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 target = TipTarget();

            // 스프링-감쇠 적분
            Vector3 force = (target - _tipPos) * stiffness;
            force += Physics.gravity * gravityInfluence;
            _tipVelocity += force * dt;
            _tipVelocity *= Mathf.Clamp01(1f - damping);
            _tipPos += _tipVelocity * dt;

            // 길이 유지
            Vector3 dir = _tipPos - transform.position;
            if (dir.sqrMagnitude < 1e-8f) return;
            _tipPos = transform.position + dir.normalized * boneLength;

            // 본을 끝 위치로 회전
            Quaternion world = Quaternion.FromToRotation(
                transform.rotation * boneAxis.normalized, dir.normalized) * transform.rotation;
            transform.rotation = world;
        }
    }
}
