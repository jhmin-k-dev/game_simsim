using System.Collections.Generic;
using Nurungi.Save;
using Nurungi.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nurungi.UI
{
    /// <summary>
    /// 시작 화면: 월드(킷) 선택 → 챕터 선택 → 씬 로드.
    /// 01_기획서 §9: HUD 최소, 손그림 톤. UI를 코드로 구성해 프리팹 의존을 없앤다.
    ///
    /// 폰트는 임시로 OS 폰트(맑은 고딕)를 쓴다 — 정식 손글씨 폰트는 09 §C-4(눈누)로 교체.
    /// </summary>
    public class TitleMenu : MonoBehaviour
    {
        // 참조 영상 팔레트
        private static readonly Color Cream = new Color(237 / 255f, 227 / 255f, 208 / 255f);
        private static readonly Color Ink = new Color(110 / 255f, 95 / 255f, 73 / 255f);
        private static readonly Color Btn = new Color(216 / 255f, 210 / 255f, 192 / 255f);
        private static readonly Color BtnLocked = new Color(200 / 255f, 196 / 255f, 186 / 255f);
        private static readonly Color Accent = new Color(168 / 255f, 173 / 255f, 126 / 255f);

        private Font _font;
        private RectTransform _listRoot;
        private Text _headline;
        private Text _subline;
        private Button _backButton;

        private WorldCatalog _catalog;
        private SaveData _save;
        private WorldCatalog.WorldEntry _selectedWorld;

        private void Start()
        {
            _font = LoadFont();
            _catalog = WorldCatalog.Load();
            _save = SaveSystem.Load();
            BuildLayout();
            ShowWorlds();
        }

        private static Font LoadFont()
        {
            // 한글이 나오는 OS 폰트를 우선 사용 (임시)
            string[] candidates = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Gulim", "Arial" };
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 28);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ---- 레이아웃 (제미나이 원화 기반 리뉴얼 2026-08-18) ----

        private RectTransform _bgRect;
        private RectTransform _dogRect;
        private RectTransform _titleRect;
        private Sprite _panelSprite;

        private void BuildLayout()
        {
            var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            var mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mainCam;
                canvas.planeDistance = 1f;
            }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // ---- 원화 배경 (노을 시장) + 켄 번즈용 여유 확대 ----
            var bgTex = Resources.Load<Texture2D>("title/title_bg");
            if (bgTex != null)
            {
                var bgGo = new GameObject("BG", typeof(RectTransform), typeof(RawImage));
                bgGo.transform.SetParent(canvasGo.transform, false);
                var raw = bgGo.GetComponent<RawImage>();
                raw.texture = bgTex;
                raw.raycastTarget = false;
                _bgRect = raw.rectTransform;
                Anchor(_bgRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2110f, 1180f)); // 110% — 팬 여유
            }
            else
            {
                var bg = CreateImage(canvasGo.transform, "BG", Cream);
                Stretch(bg.rectTransform);
            }

            // 하단 그라데이션(가독성): 어두운 잉크색 반투명 띠
            var shade = CreateImage(canvasGo.transform, "Shade", new Color(0.16f, 0.12f, 0.10f, 0.34f));
            Anchor(shade.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(2200f, 560f));
            shade.raycastTarget = false;

            // ---- 걷는 누룽이 (원화 컷) ----
            var dogTex = Resources.Load<Texture2D>("title/dog_walk");
            if (dogTex != null)
            {
                var dogGo = new GameObject("Dog", typeof(RectTransform), typeof(RawImage));
                dogGo.transform.SetParent(canvasGo.transform, false);
                var raw = dogGo.GetComponent<RawImage>();
                raw.texture = dogTex;
                raw.raycastTarget = false;
                _dogRect = raw.rectTransform;
                Anchor(_dogRect, new Vector2(0f, 0f), new Vector2(300f, 240f), new Vector2(330f, 293f));
            }

            // ---- 타이틀 ----
            _panelSprite = LoadPanelSprite();
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(canvasGo.transform, false);
            _titleRect = titleGo.GetComponent<RectTransform>();
            Anchor(_titleRect, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1400f, 170f));

            var titleShadow = CreateText(titleGo.transform, "TitleShadow", "누룽이의 세계", 96,
                new Color(0.16f, 0.12f, 0.08f, 0.55f), TextAnchor.MiddleCenter);
            Stretch(titleShadow.rectTransform);
            titleShadow.rectTransform.anchoredPosition = new Vector2(4f, -5f);
            titleShadow.fontStyle = FontStyle.Bold;

            _headline = CreateText(titleGo.transform, "Headline", "누룽이의 세계", 96,
                new Color(1f, 0.97f, 0.9f), TextAnchor.MiddleCenter);
            Stretch(_headline.rectTransform);
            _headline.fontStyle = FontStyle.Bold;

            _subline = CreateText(canvasGo.transform, "Subline", "", 30,
                new Color(1f, 0.95f, 0.85f, 0.92f), TextAnchor.MiddleCenter);
            Anchor(_subline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(1200f, 50f));

            // ---- 메뉴 목록 ----
            var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(canvasGo.transform, false);
            _listRoot = listGo.GetComponent<RectTransform>();
            Anchor(_listRoot, new Vector2(0.5f, 0.42f), new Vector2(0f, -60f), new Vector2(640f, 520f));
            var vlg = listGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;

            _backButton = CreateButton(canvasGo.transform, "Back", "← 뒤로", ShowWorlds);
            Anchor(_backButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(150f, 80f), new Vector2(210f, 62f));
            _backButton.gameObject.SetActive(false);

            var hint = CreateText(canvasGo.transform, "Hint",
                "WASD 이동 · 클릭 이동 · Shift 대시 · Space 점프 · Esc 타이틀",
                22, new Color(1f, 0.96f, 0.88f, 0.75f), TextAnchor.MiddleCenter);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1700f, 36f));
        }

        private static Sprite LoadPanelSprite()
        {
            var tex = Resources.Load<Texture2D>("title/panel");
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(34, 34, 34, 34)); // 9-slice
        }

        private void Update()
        {
            // 켄 번즈: 배경이 아주 천천히 흐르고, 누룽이가 아장아장 걷는 듯 들썩임 (04 §2-4 3번)
            float t = Time.time;
            if (_bgRect != null)
                _bgRect.anchoredPosition = new Vector2(Mathf.Sin(t * 0.05f) * 55f, Mathf.Sin(t * 0.037f) * 22f - 10f);
            if (_dogRect != null)
            {
                _dogRect.anchoredPosition = new Vector2(300f + Mathf.Sin(t * 0.5f) * 10f,
                    240f + Mathf.Abs(Mathf.Sin(t * 2.6f)) * 7f);
                _dogRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.6f) * 1.6f);
            }
            if (_titleRect != null)
                _titleRect.anchoredPosition = new Vector2(0f, -190f + Mathf.Sin(t * 0.7f) * 4f);
        }

        // ---- 화면 전환 ----

        private void ShowWorlds()
        {
            ClearList();
            _selectedWorld = null;
            _backButton.gameObject.SetActive(false);
            _subline.text = _save.totalPlaySeconds > 1f
                ? $"지금까지 {Mathf.FloorToInt(_save.totalPlaySeconds / 60f)}분 산책했어요"
                : "어디로 산책 갈까요?";

            // 이어하기
            if (!string.IsNullOrEmpty(_save.lastPlayedChapterId))
            {
                var last = _catalog.FindChapter(_save.lastPlayedChapterId);
                if (last != null)
                    AddButton($"▶ 이어하기 — {last.name}", Accent, () => LoadChapter(last));
            }

            foreach (var world in _catalog.worlds)
            {
                var w = world; // 클로저 캡처
                int cleared = 0;
                foreach (var c in w.chapters) if (_save.IsCleared(c.id)) cleared++;
                string label = $"{w.name}   ({cleared}/{w.chapters.Count})";
                AddButton(label, Btn, () => ShowChapters(w));
            }

            if (_catalog.worlds.Count == 0)
                AddLabel("worlds.json 을 읽지 못했습니다");
        }

        private void ShowChapters(WorldCatalog.WorldEntry world)
        {
            ClearList();
            _selectedWorld = world;
            _backButton.gameObject.SetActive(true);
            _subline.text = world.description;

            foreach (var chapter in world.chapters)
            {
                var c = chapter;
                bool unlocked = WorldCatalog.IsUnlocked(c, _save);
                bool cleared = _save.IsCleared(c.id);
                string mark = cleared ? "✓ " : (unlocked ? "" : "🔒 ");
                string label = $"{mark}{c.name}";

                if (unlocked) AddButton(label, cleared ? Accent : Btn, () => LoadChapter(c));
                else AddButton(label, BtnLocked, null);
            }
        }

        private void LoadChapter(WorldCatalog.ChapterEntry chapter)
        {
            if (chapter == null) return;
            ChapterSession.PendingChapterId = chapter.id;
            if (Application.CanStreamedLevelBeLoaded(chapter.scene))
            {
                SceneManager.LoadScene(chapter.scene);
            }
            else
            {
                Debug.LogError($"[Title] '{chapter.scene}' 씬이 빌드 설정에 없습니다");
                _subline.text = $"'{chapter.scene}' 씬을 찾을 수 없어요";
            }
        }

        // ---- UI 헬퍼 ----

        private void ClearList()
        {
            for (int i = _listRoot.childCount - 1; i >= 0; i--)
                Destroy(_listRoot.GetChild(i).gameObject);
        }

        private void AddButton(string label, Color color, System.Action onClick)
        {
            var btn = CreateButton(_listRoot, label, label, onClick);
            var img = btn.GetComponent<Image>();
            if (_panelSprite == null) img.color = color;
            else if (color == Accent) img.color = new Color(0.88f, 0.90f, 0.72f, 0.95f); // 클리어·이어하기 강조
            var rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 74f);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 74f;
            if (onClick == null) btn.interactable = false;
        }

        private void AddLabel(string text)
        {
            var t = CreateText(_listRoot, "Label", text, 28, Ink, TextAnchor.MiddleCenter);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        }

        private Button CreateButton(Transform parent, string name, string label, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            if (_panelSprite != null)
            {
                img.sprite = _panelSprite;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.98f, 0.94f, 0.85f, 0.94f); // 크림 패널
            }
            else img.color = Btn;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.98f, 0.9f, 1f);
            colors.pressedColor = new Color(0.93f, 0.86f, 0.72f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            btn.colors = colors;
            btn.transition = Selectable.Transition.ColorTint;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var text = CreateText(go.transform, "Label", label, 33, Ink, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return btn;
        }

        private Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }
    }
}
