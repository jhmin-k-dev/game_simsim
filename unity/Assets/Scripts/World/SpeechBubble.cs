using UnityEngine;

namespace Nurungi.World
{
    /// <summary>
    /// 간단한 월드 말풍선 (01 §6: 대사는 한 줄). TextMesh + 팝 애니메이션 + 빌보드.
    /// </summary>
    public class SpeechBubble : MonoBehaviour
    {
        private float _life;
        private float _age;
        private Transform _follow;
        private Vector3 _offset;

        public static void Say(Transform target, string text, float duration, float height, Color? color = null)
        {
            var go = new GameObject("SpeechBubble");
            var bubble = go.AddComponent<SpeechBubble>();
            bubble._life = duration;
            bubble._follow = target;
            bubble._offset = Vector3.up * height;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.012f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            var font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 48);
            if (font != null)
            {
                tm.font = font;
                var mr = go.GetComponent<MeshRenderer>();
                mr.material = font.material;
            }
            tm.color = color ?? new Color(0.35f, 0.28f, 0.2f);

            go.transform.position = target.position + bubble._offset;
        }

        private void LateUpdate()
        {
            _age += Time.deltaTime;
            if (_age >= _life) { Destroy(gameObject); return; }

            if (_follow != null)
                transform.position = _follow.position + _offset + Vector3.up * Mathf.Min(0.1f, _age * 0.25f);

            // 카메라를 향해 (빌보드)
            var cam = UnityEngine.Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            // 팝: 커졌다가 안착, 끝에서 축소
            float pop = _age < 0.18f ? Mathf.SmoothStep(0.2f, 1.15f, _age / 0.18f)
                      : _age < 0.3f ? Mathf.Lerp(1.15f, 1f, (_age - 0.18f) / 0.12f)
                      : _age > _life - 0.2f ? Mathf.Lerp(1f, 0f, (_age - (_life - 0.2f)) / 0.2f)
                      : 1f;
            transform.localScale = Vector3.one * pop;
        }
    }
}
