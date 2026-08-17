using UnityEngine;

namespace Nurungi.Player
{
    /// <summary>
    /// PlayerMover·StanceController 상태를 Animator 파라미터로 전달.
    /// 파라미터: Speed(m/s), Quad(0~1). 클립은 BuildDogAnimations가 생성.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class DogAnimatorDriver : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int QuadHash = Animator.StringToHash("Quad");

        private Animator _animator;
        private PlayerMover _mover;
        private StanceController _stance;
        private Vector3 _lastPos;
        private float _smoothed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _mover = GetComponentInParent<PlayerMover>();
            _stance = GetComponentInParent<StanceController>();
            _lastPos = transform.position;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 실제 이동량 기반 (DSL 스크립트 이동도 포함)
            Vector3 planar = transform.position - _lastPos;
            planar.y = 0f;
            _lastPos = transform.position;
            float measured = planar.magnitude / dt;
            float reported = _mover != null ? _mover.HorizontalSpeed : 0f;
            _smoothed = Mathf.Lerp(_smoothed, Mathf.Min(Mathf.Max(measured, reported), 8f),
                1f - Mathf.Exp(-10f * dt));

            _animator.SetFloat(SpeedHash, _smoothed);
            _animator.SetFloat(QuadHash, _stance != null ? _stance.QuadBlend : 0f);
        }
    }
}
