using System.Collections.Generic;
using UnityEngine;

namespace Nurungi.Scripting
{
    /// <summary>
    /// 누룽이 스크립트 실행기. 씬을 갈아타도 살아남아야 하므로 DontDestroyOnLoad.
    /// 한 번에 하나만 돈다 (Instance).
    /// </summary>
    public class ScriptRunner : MonoBehaviour
    {
        public static ScriptRunner Instance { get; private set; }

        private readonly ScriptContext _ctx = new ScriptContext();
        private List<ScriptStep> _steps;
        private int _stepIndex;
        private readonly List<ScriptAction> _running = new List<ScriptAction>();
        private bool _stepStarted;

        public bool IsRunning { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public string CurrentLabel { get; private set; } = "";
        public int StepIndex => _stepIndex;
        public int StepCount => _steps?.Count ?? 0;

        /// 실행 로그 (콘솔 UI가 보여준다)
        public readonly List<string> Log = new List<string>();

        public static ScriptRunner GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("NurungiScriptRunner");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ScriptRunner>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <returns>파싱 에러 목록 (비어 있으면 실행 시작)</returns>
        public List<string> Run(string source)
        {
            var parsed = NurungiScriptParser.Parse(source);
            if (!parsed.Ok) return parsed.Errors;

            Stop();
            _steps = parsed.Steps;
            _stepIndex = 0;
            _stepStarted = false;
            ElapsedSeconds = 0f;
            Log.Clear();
            _ctx.Refresh();
            IsRunning = _steps.Count > 0;
            if (!IsRunning) Log.Add("실행할 명령이 없습니다");
            return parsed.Errors;
        }

        public void Stop()
        {
            if (_running.Count > 0)
                foreach (var a in _running) a.Stop(_ctx);
            _running.Clear();
            _steps = null;
            IsRunning = false;
            CurrentLabel = "";
            _ctx.ReleaseAll();
        }

        private void Update()
        {
            if (!IsRunning || _steps == null) return;
            float dt = Time.deltaTime;
            ElapsedSeconds += dt;

            if (_stepIndex >= _steps.Count) { Finish(); return; }

            var step = _steps[_stepIndex];

            if (!_stepStarted)
            {
                _running.Clear();
                foreach (var action in step.Actions)
                {
                    action.Start(_ctx);
                    _running.Add(action);
                    Log.Add($"{ElapsedSeconds,6:F2}s  {action.Describe()}");
                }
                CurrentLabel = step.Actions.Count > 0 ? step.Actions[0].Describe() : "";
                _stepStarted = true;
            }

            // 동시 실행 동작 중 끝난 것부터 제거
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                if (_running[i].Update(_ctx, dt))
                {
                    _running[i].Stop(_ctx);
                    _running.RemoveAt(i);
                }
            }

            if (_running.Count == 0)
            {
                _stepIndex++;
                _stepStarted = false;
                if (_stepIndex >= _steps.Count) Finish();
            }
        }

        private void Finish()
        {
            Log.Add($"{ElapsedSeconds,6:F2}s  ── 끝 ──");
            IsRunning = false;
            CurrentLabel = "완료";
            _ctx.ReleaseAll();
        }
    }
}
