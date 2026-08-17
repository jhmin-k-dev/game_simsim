using UnityEngine;

namespace Nurungi.Player
{
    /// <summary>
    /// 04 §2-4 품질 1원칙: "영화 애니메이션처럼".
    /// 애니메이터를 초당 12스텝(애니메이션 '2컷 찍기')으로 재생해
    /// 60fps 보간의 '게임스러운' 미끈함을 지우고 만화 특유의 끊김을 만든다.
    /// (스파이더버스·길티기어 기법 — 카메라·이동은 60fps 그대로, 캐릭터 포즈만 스텝)
    ///
    /// 절차적 레이어(ProceduralMotion)는 60fps 유지 — 숨쉬기·스프링본까지 스텝하면
    /// 살아있는 느낌이 죽는다. 뼈 포즈(클립)만 끊는다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SteppedAnimation : MonoBehaviour
    {
        [Tooltip("애니메이션 초당 스텝 수. 12 = 2컷, 8 = 3컷 (더 만화적)")]
        [Range(4f, 30f)] public float stepsPerSecond = 12f;

        private Animator _animator;
        private float _accumulated;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animator.enabled = false;   // 수동 스텝
        }

        private void OnDisable()
        {
            if (_animator != null) _animator.enabled = true;
        }

        private void Update()
        {
            _accumulated += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, stepsPerSecond);
            if (_accumulated >= step)
            {
                // 밀린 시간을 스텝 단위로 한 번에 — 프레임 드랍에도 재생 속도 유지
                float advance = Mathf.Floor(_accumulated / step) * step;
                _animator.Update(advance);
                _accumulated -= advance;
            }
        }
    }
}
