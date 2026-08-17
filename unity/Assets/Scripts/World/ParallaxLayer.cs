using UnityEngine;

namespace Nurungi.World
{
    /// <summary>
    /// 03_대형스크롤맵 §2-1: 시차 레이어.
    /// 카메라 X 이동량 × (1 - 계수)만큼 레이어를 같은 방향으로 밀어,
    /// 계수가 낮을수록 화면에서 느리게 흐르게 한다 (원경일수록 낮게).
    ///
    /// FOV 28 망원에서는 깊이에 의한 자연 시차가 약하므로 이 인위 시차가 주가 된다.
    /// 03 §2-1: 하늘 0.1 / 배경판 0.35 / 중경 0.6 / 근경 0.85 / (바닥 1.0 = 시차 없음)
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [Range(0f, 1.3f)] public float factor = 0.35f;

        private Transform _cam;
        private float _camStartX;
        private float _selfStartX;
        private bool _ready;

        private void LateUpdate()
        {
            if (!_ready)
            {
                var main = UnityEngine.Camera.main;
                if (main == null) return;
                _cam = main.transform;
                _camStartX = _cam.position.x;
                _selfStartX = transform.position.x;
                _ready = true;
            }

            float camDelta = _cam.position.x - _camStartX;
            var p = transform.position;
            // factor 1.0 = 월드 고정(시차 없음), 0.0 = 화면 고정(무한 원경)
            p.x = _selfStartX + camDelta * (1f - factor);
            transform.position = p;
        }
    }
}
