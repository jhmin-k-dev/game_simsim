using Nurungi.Player;
using UnityEngine;

namespace Nurungi.UI
{
    /// <summary>
    /// 대시 게이지 표시 (01 §9 HUD 최소화: 꽉 차 있으면 사라지고, 쓰는 중에만 보임).
    /// 임시 IMGUI — 손그림 UI(09 §C)로 교체 예정.
    /// </summary>
    public class DashGaugeUI : MonoBehaviour
    {
        private PlayerMover _mover;
        private float _visible; // 0~1 페이드
        private Texture2D _white;

        private void Awake()
        {
            _mover = FindFirstObjectByType<PlayerMover>();
            _white = Texture2D.whiteTexture;
        }

        private void Update()
        {
            if (_mover == null) return;
            bool show = _mover.DashOn || _mover.DashNormalized < 0.995f;
            _visible = Mathf.MoveTowards(_visible, show ? 1f : 0f, Time.deltaTime * 3f);
        }

        private void OnGUI()
        {
            if (_mover == null || _visible <= 0.01f) return;

            float w = 150f, h = 10f;
            float x = 24f, y = Screen.height - 40f;
            float fill = Mathf.Clamp01(_mover.DashNormalized);

            // 배경
            GUI.color = new Color(0.28f, 0.23f, 0.18f, 0.45f * _visible);
            GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, h + 4), _white);
            // 채움: 대시 중 주황, 회복 중 크림
            Color fillColor = _mover.DashOn
                ? new Color(1f, 0.62f, 0.3f, 0.95f)
                : new Color(0.95f, 0.85f, 0.6f, 0.9f);
            GUI.color = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * _visible);
            GUI.DrawTexture(new Rect(x, y, w * fill, h), _white);
            GUI.color = Color.white;
        }
    }
}
