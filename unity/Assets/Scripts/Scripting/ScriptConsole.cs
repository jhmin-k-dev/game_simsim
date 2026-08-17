using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nurungi.Scripting
{
    /// <summary>
    /// 인게임 스크립트 콘솔. F1으로 열고, 문법을 쳐서 바로 실행해 본다.
    /// IMGUI를 쓴다 — 개발 도구라 룩에 영향이 없고, 프리팹·폰트 자산이 필요 없다.
    /// </summary>
    public class ScriptConsole : MonoBehaviour
    {
        private const string DefaultScript =
@"# 누룽이 스크립트 — F5로 실행, F1로 창 닫기
dog at (4, 0.3)
dog face right
cam fov 28 in 0s

dog move (16, 0.3) in 4s
& cam pan 2 in 4s

cam follow
dog motion sniff for 1.5s
dog jump

dog run (40, 0.3) in 3.5s
& cam fov 22 in 3.5s

dog stance biped
say 산책 끝";

        private bool _open;
        private string _text = DefaultScript;
        private Vector2 _scrollEditor, _scrollLog;
        private string _status = "";
        private GUIStyle _mono, _box;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) _open = !_open;
            if (_open && kb.f5Key.wasPressedThisFrame) RunScript();
            if (_open && kb.f6Key.wasPressedThisFrame) ScriptRunner.GetOrCreate().Stop();
        }

        private void RunScript()
        {
            var runner = ScriptRunner.GetOrCreate();
            var errors = runner.Run(_text);
            if (errors.Count > 0)
            {
                var sb = new StringBuilder("문법 오류:\n");
                foreach (var e in errors) sb.AppendLine("  " + e);
                _status = sb.ToString();
            }
            else
            {
                _status = $"실행 중 ({runner.StepCount}단계)";
            }
        }

        private void OnGUI()
        {
            if (!_open)
            {
                GUI.Label(new Rect(10, 10, 300, 22), "F1 : 스크립트 콘솔");
                return;
            }

            EnsureStyles();
            float w = Mathf.Min(720f, Screen.width - 40f);
            float h = Mathf.Min(620f, Screen.height - 40f);
            var area = new Rect(20, 20, w, h);
            GUI.Box(area, "누룽이 스크립트  (F5 실행 · F6 정지 · F1 닫기)", _box);

            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 30, area.width - 24, area.height - 42));

            // 편집기
            _scrollEditor = GUILayout.BeginScrollView(_scrollEditor, GUILayout.Height(h * 0.45f));
            _text = GUILayout.TextArea(_text, _mono, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ 실행 (F5)", GUILayout.Height(30))) RunScript();
            if (GUILayout.Button("■ 정지 (F6)", GUILayout.Height(30))) ScriptRunner.GetOrCreate().Stop();
            if (GUILayout.Button("예제 되돌리기", GUILayout.Height(30))) _text = DefaultScript;
            GUILayout.EndHorizontal();

            // 상태
            var runner = ScriptRunner.Instance;
            if (runner != null && runner.IsRunning)
                GUILayout.Label($"{runner.ElapsedSeconds:F2}s  [{runner.StepIndex + 1}/{runner.StepCount}]  {runner.CurrentLabel}");
            else if (!string.IsNullOrEmpty(_status))
                GUILayout.Label(_status, _mono);

            // 실행 로그
            GUILayout.Label("실행 로그");
            _scrollLog = GUILayout.BeginScrollView(_scrollLog, GUILayout.ExpandHeight(true));
            if (runner != null)
            {
                var sb = new StringBuilder();
                foreach (var l in runner.Log) sb.AppendLine(l);
                GUILayout.Label(sb.ToString(), _mono);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_mono == null)
            {
                _mono = new GUIStyle(GUI.skin.textArea)
                {
                    font = Font.CreateDynamicFontFromOSFont("Consolas", 14),
                    fontSize = 14,
                    wordWrap = false,
                };
            }
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 14,
                };
            }
        }
    }
}
