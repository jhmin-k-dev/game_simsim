using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Nurungi.Scripting
{
    /// <summary>여러 동작이 동시에 실행되는 한 단계. 전부 끝나야 다음 단계로.</summary>
    public class ScriptStep
    {
        public List<ScriptAction> Actions = new List<ScriptAction>();
    }

    public class ParseResult
    {
        public List<ScriptStep> Steps = new List<ScriptStep>();
        public List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// 누룽이 스크립트 파서. 문법은 docs/10_누룽이_스크립트.md 참조.
    /// 한 줄 = 한 동작. 줄 앞에 '&'를 붙이면 바로 앞 줄과 동시에 실행된다.
    /// </summary>
    public static class NurungiScriptParser
    {
        public static ParseResult Parse(string source)
        {
            var result = new ParseResult();
            if (string.IsNullOrEmpty(source)) return result;

            string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string raw = lines[i];

                // 주석(#, //)과 빈 줄 제거
                int hash = raw.IndexOf('#');
                if (hash >= 0) raw = raw.Substring(0, hash);
                int slashes = raw.IndexOf("//");
                if (slashes >= 0) raw = raw.Substring(0, slashes);
                string line = raw.Trim();
                if (line.Length == 0) continue;

                bool parallel = line.StartsWith("&");
                if (parallel) line = line.Substring(1).Trim();

                var action = ParseLine(line, lineNo, result.Errors);
                if (action == null) continue;
                action.Line = lineNo;

                if (parallel && result.Steps.Count > 0)
                    result.Steps[result.Steps.Count - 1].Actions.Add(action);
                else
                {
                    var step = new ScriptStep();
                    step.Actions.Add(action);
                    result.Steps.Add(step);
                }
            }
            return result;
        }

        private static ScriptAction ParseLine(string line, int lineNo, List<string> errors)
        {
            var tokens = Tokenize(line);
            if (tokens.Count == 0) return null;

            switch (tokens[0].ToLowerInvariant())
            {
                case "map": return ParseMap(tokens, lineNo, errors);
                case "wait": return ParseWait(tokens, lineNo, errors);
                case "say": return new LogAction { Message = line.Substring(3).Trim() };
                case "dog": return ParseDog(tokens, lineNo, errors);
                case "cam":
                case "camera": return ParseCam(tokens, lineNo, errors);
                default:
                    errors.Add($"{lineNo}행: 모르는 명령 '{tokens[0]}'");
                    return null;
            }
        }

        // ---- map <chapterId|sceneName> ----
        private static ScriptAction ParseMap(List<string> t, int lineNo, List<string> errors)
        {
            if (t.Count < 2) { errors.Add($"{lineNo}행: map 뒤에 맵 이름이 필요합니다"); return null; }
            return new MapAction { ChapterId = t[1] };
        }

        // ---- wait <n>s ----
        private static ScriptAction ParseWait(List<string> t, int lineNo, List<string> errors)
        {
            if (t.Count < 2 || !TryNumber(t[1], out float s))
            {
                errors.Add($"{lineNo}행: wait 뒤에 시간이 필요합니다 (예: wait 1.5s)");
                return null;
            }
            return new WaitAction { Seconds = s };
        }

        // ---- dog ... ----
        private static ScriptAction ParseDog(List<string> t, int lineNo, List<string> errors)
        {
            if (t.Count < 2) { errors.Add($"{lineNo}행: dog 뒤에 동작이 필요합니다"); return null; }
            string verb = t[1].ToLowerInvariant();

            switch (verb)
            {
                case "at":
                {
                    if (!TryVector2(t, 2, out Vector2 p))
                    {
                        errors.Add($"{lineNo}행: 좌표 형식은 (x, z) 입니다");
                        return null;
                    }
                    return new DogTeleportAction { Xz = p };
                }

                case "move":
                case "walk":
                case "run":
                {
                    int idx = 2;
                    if (t.Count > 2 && t[2].ToLowerInvariant() == "to") idx = 3;
                    if (!TryVector2(t, idx, out Vector2 target))
                    {
                        errors.Add($"{lineNo}행: 좌표 형식은 (x, z) 입니다 — 예: dog move (18, 0.3) in 4s");
                        return null;
                    }

                    var act = new DogMoveAction { Target = target, Sprint = verb == "run" };
                    // in <t>s  /  at <v>
                    for (int i = idx; i < t.Count - 1; i++)
                    {
                        string k = t[i].ToLowerInvariant();
                        if (k == "in" && TryNumber(t[i + 1], out float dur)) act.Duration = dur;
                        else if ((k == "at" || k == "speed") && TryNumber(t[i + 1], out float spd)) act.Speed = spd;
                    }
                    return act;
                }

                case "face":
                {
                    if (t.Count < 3) { errors.Add($"{lineNo}행: face 뒤에 각도 또는 방향이 필요합니다"); return null; }
                    string dir = t[2].ToLowerInvariant();
                    float deg;
                    switch (dir)
                    {
                        case "right": deg = 90f; break;
                        case "left": deg = 270f; break;
                        case "camera": case "front": deg = 180f; break;
                        case "away": case "back": deg = 0f; break;
                        default:
                            if (!TryNumber(t[2], out deg))
                            {
                                errors.Add($"{lineNo}행: face 값 '{t[2]}' 을(를) 이해할 수 없습니다");
                                return null;
                            }
                            break;
                    }
                    return new DogFaceAction { Degrees = deg };
                }

                case "jump": return new DogJumpAction();

                case "stance":
                {
                    if (t.Count < 3) { errors.Add($"{lineNo}행: stance 뒤에 biped 또는 quad"); return null; }
                    string s = t[2].ToLowerInvariant();
                    if (s != "biped" && s != "quad" && s != "2" && s != "4")
                    {
                        errors.Add($"{lineNo}행: stance 는 biped 또는 quad 입니다");
                        return null;
                    }
                    return new DogStanceAction { Quad = s == "quad" || s == "4" };
                }

                case "motion":
                {
                    if (t.Count < 3) { errors.Add($"{lineNo}행: motion 뒤에 이름이 필요합니다"); return null; }
                    var m = new DogMotionAction { Motion = t[2] };
                    for (int i = 3; i < t.Count - 1; i++)
                        if (t[i].ToLowerInvariant() == "for" && TryNumber(t[i + 1], out float s)) m.Seconds = s;
                    return m;
                }

                default:
                    errors.Add($"{lineNo}행: dog 의 모르는 동작 '{verb}'");
                    return null;
            }
        }

        // ---- cam ... ----
        private static ScriptAction ParseCam(List<string> t, int lineNo, List<string> errors)
        {
            if (t.Count < 2) { errors.Add($"{lineNo}행: cam 뒤에 동작이 필요합니다"); return null; }
            string verb = t[1].ToLowerInvariant();

            if (verb == "follow") return new CamFollowAction();

            if (verb == "pan")
            {
                // cam pan <m/s> in <t>s   (기본 방향 +X)
                if (t.Count < 3 || !TryNumber(t[2], out float mps))
                {
                    errors.Add($"{lineNo}행: cam pan 뒤에 초당 이동거리가 필요합니다 — 예: cam pan 3 in 5s");
                    return null;
                }
                float dur = 1f;
                for (int i = 3; i < t.Count - 1; i++)
                    if (t[i].ToLowerInvariant() == "in" && TryNumber(t[i + 1], out float d)) dur = d;
                return new CamPanAction { MetersPerSecond = new Vector3(mps, 0f, 0f), Duration = dur };
            }

            CamValueAction.Kind kind;
            switch (verb)
            {
                case "fov": kind = CamValueAction.Kind.Fov; break;
                case "distance": case "dist": kind = CamValueAction.Kind.Distance; break;
                case "height": kind = CamValueAction.Kind.Height; break;
                case "pitch": kind = CamValueAction.Kind.Pitch; break;
                default:
                    errors.Add($"{lineNo}행: cam 의 모르는 동작 '{verb}'");
                    return null;
            }

            if (t.Count < 3 || !TryNumber(t[2], out float target))
            {
                errors.Add($"{lineNo}행: cam {verb} 뒤에 값이 필요합니다");
                return null;
            }
            float duration = 0f;
            for (int i = 3; i < t.Count - 1; i++)
                if (t[i].ToLowerInvariant() == "in" && TryNumber(t[i + 1], out float d)) duration = d;

            return new CamValueAction { Which = kind, Target = target, Duration = duration };
        }

        // ---- 토큰화: 괄호·쉼표를 공백처럼 다루되 (x, z) 는 하나로 묶는다 ----
        private static List<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '(')
                {
                    int close = line.IndexOf(')', i);
                    if (close < 0) close = line.Length - 1;
                    tokens.Add(line.Substring(i, close - i + 1));
                    i = close + 1;
                    continue;
                }

                int start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '(') i++;
                tokens.Add(line.Substring(start, i - start));
            }
            return tokens;
        }

        /// "4s", "1.5", "-3" 모두 숫자로 받는다
        private static bool TryNumber(string s, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim().TrimEnd('s', 'S').TrimEnd('m', 'M');
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// "(x, z)" 토큰을 Vector2로
        private static bool TryVector2(List<string> tokens, int index, out Vector2 v)
        {
            v = Vector2.zero;
            if (index >= tokens.Count) return false;
            string s = tokens[index].Trim();
            if (!s.StartsWith("(")) return false;
            s = s.Trim('(', ')');
            string[] parts = s.Split(',');
            if (parts.Length < 2) return false;
            if (!TryNumber(parts[0], out float x)) return false;
            if (!TryNumber(parts[1], out float z)) return false;
            v = new Vector2(x, z);
            return true;
        }
    }
}
