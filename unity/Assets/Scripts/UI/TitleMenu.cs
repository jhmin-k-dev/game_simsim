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

        // ---- 레이아웃 ----

        private void BuildLayout()
        {
            var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            // ScreenSpaceCamera: 오버레이와 보이는 결과는 같지만 카메라 렌더에 포함돼
            // 스크린샷·무비 모드 캡처에도 UI가 찍힌다 (06 §5-3)
            var mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mainCam;
                canvas.planeDistance = 1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
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

            // 배경
            var bg = CreateImage(canvasGo.transform, "BG", Cream);
            Stretch(bg.rectTransform);

            // 제목
            _headline = CreateText(canvasGo.transform, "Headline", "누룽이의 세계", 76, Ink, TextAnchor.MiddleCenter);
            Anchor(_headline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 110f));

            _subline = CreateText(canvasGo.transform, "Subline", "", 30, Ink, TextAnchor.MiddleCenter);
            Anchor(_subline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(1200f, 50f));

            // 목록 영역 (세로 정렬)
            var listGo = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(canvasGo.transform, false);
            _listRoot = listGo.GetComponent<RectTransform>();
            Anchor(_listRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 620f));
            var vlg = listGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;

            // 뒤로
            _backButton = CreateButton(canvasGo.transform, "Back", "← 뒤로", ShowWorlds);
            Anchor(_backButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(150f, 80f), new Vector2(220f, 64f));
            _backButton.gameObject.SetActive(false);

            // 안내
            var hint = CreateText(canvasGo.transform, "Hint",
                "WASD·방향키 이동   ·   클릭으로 이동   ·   Shift 질주(4족)   ·   Space 점프   ·   Esc 타이틀",
                24, new Color(Ink.r, Ink.g, Ink.b, 0.65f), TextAnchor.MiddleCenter);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1700f, 40f));
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
            img.color = color;
            var rt = btn.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 76f);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 76f;
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
            img.color = Btn;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.86f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var text = CreateText(go.transform, "Label", label, 34, Ink, TextAnchor.MiddleCenter);
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
