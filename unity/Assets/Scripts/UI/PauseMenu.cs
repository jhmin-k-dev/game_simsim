using Nurungi.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nurungi.UI
{
    /// <summary>
    /// ESC 일시정지 메뉴: 계속하기 / 타이틀로 / 게임 종료.
    /// 타이틀과 같은 크림 라운드 패널 스타일 (Resources/title/panel).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private static readonly Color Ink = new Color(110 / 255f, 95 / 255f, 73 / 255f);

        private GameObject _root;
        private bool _paused;
        private Font _font;
        private Sprite _panel;

        private void Start()
        {
            _font = LoadFont();
            var panelTex = Resources.Load<Texture2D>("title/panel");
            if (panelTex != null)
                _panel = Sprite.Create(panelTex, new Rect(0, 0, panelTex.width, panelTex.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(34, 34, 34, 34));
            BuildUi();
            _root.SetActive(false);
        }

        private static Font LoadFont()
        {
            string[] candidates = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Arial" };
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 30);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Toggle();
        }

        public void Toggle()
        {
            _paused = !_paused;
            _root.SetActive(_paused);
            Time.timeScale = _paused ? 0f : 1f;
        }

        private void Resume()
        {
            if (_paused) Toggle();
        }

        private void ToTitle()
        {
            Time.timeScale = 1f;
            _paused = false;
            var session = FindFirstObjectByType<ChapterSession>();
            if (session != null) session.ReturnToTitle();
        }

        private void QuitGame()
        {
            Time.timeScale = 1f;
            var session = FindFirstObjectByType<ChapterSession>();
            if (session != null) session.SaveNow();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- UI ----

        private void BuildUi()
        {
            _root = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // 어둑한 배경
            var dim = NewImage(_root.transform, "Dim", new Color(0.13f, 0.1f, 0.08f, 0.55f));
            Stretch(dim.rectTransform);

            var title = NewText(_root.transform, "잠깐 쉬는 중", 54, new Color(1f, 0.96f, 0.88f));
            Anchor(title.rectTransform, new Vector2(0.5f, 0.68f), new Vector2(800f, 80f));

            string[] labels = { "계속하기", "타이틀로", "게임 종료" };
            System.Action[] actions = { Resume, ToTitle, QuitGame };
            for (int i = 0; i < 3; i++)
            {
                var btn = NewButton(_root.transform, labels[i], actions[i]);
                Anchor(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.52f - i * 0.105f), new Vector2(420f, 78f));
            }

            var hint = NewText(_root.transform, "ESC — 계속하기", 22, new Color(1f, 0.95f, 0.86f, 0.6f));
            Anchor(hint.rectTransform, new Vector2(0.5f, 0.12f), new Vector2(600f, 40f));
        }

        private Button NewButton(Transform parent, string label, System.Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            if (_panel != null)
            {
                img.sprite = _panel;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.98f, 0.94f, 0.85f, 0.96f);
            }
            else img.color = new Color(0.9f, 0.86f, 0.76f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var text = NewText(go.transform, label, 33, Ink);
            Stretch(text.rectTransform);
            return btn;
        }

        private Text NewText(Transform parent, string content, int size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return t;
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }
    }
}
