using Nurungi.Config;
using Nurungi.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nurungi.Scripting
{
    /// <summary>스크립트 한 동작. Update가 true를 반환하면 완료.</summary>
    public abstract class ScriptAction
    {
        public int Line;
        public abstract void Start(ScriptContext ctx);
        public abstract bool Update(ScriptContext ctx, float dt);
        public virtual void Stop(ScriptContext ctx) { }
        public virtual string Describe() => GetType().Name;
    }

    // ---------- 대기 ----------

    public class WaitAction : ScriptAction
    {
        public float Seconds;
        private float _t;
        public override void Start(ScriptContext ctx) { _t = 0f; }
        public override bool Update(ScriptContext ctx, float dt)
        {
            _t += dt;
            return _t >= Seconds;
        }
        public override string Describe() => $"wait {Seconds}s";
    }

    // ---------- 맵 ----------

    public class MapAction : ScriptAction
    {
        public string ChapterId;
        private bool _requested;
        private float _settle;

        public override void Start(ScriptContext ctx)
        {
            _requested = false;
            _settle = 0f;
            var chapter = World.WorldCatalog.Load().FindChapter(ChapterId);
            string sceneName = chapter != null ? chapter.scene : ChapterId;

            if (SceneManager.GetActiveScene().name == sceneName)
            {
                _requested = true; // 이미 그 맵이면 로드 생략
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[Script] {Line}행: 맵 '{sceneName}' 을(를) 찾을 수 없음");
                _requested = true;
                return;
            }
            if (chapter != null) World.ChapterSession.PendingChapterId = chapter.id;
            SceneManager.LoadScene(sceneName);
            _requested = true;
        }

        public override bool Update(ScriptContext ctx, float dt)
        {
            if (!_requested) return false;
            // 씬 로드 직후 한 프레임 이상 여유를 두고 대상들을 다시 찾는다
            _settle += dt;
            if (_settle < 0.2f) return false;
            ctx.Refresh();
            return ctx.IsReady;
        }

        public override string Describe() => $"map {ChapterId}";
    }

    // ---------- 누룽이 ----------

    public class DogTeleportAction : ScriptAction
    {
        public Vector2 Xz;
        public override void Start(ScriptContext ctx)
        {
            if (ctx.Dog == null) return;
            var cc = ctx.DogController;
            if (cc != null) cc.enabled = false;   // 워프하려면 컨트롤러를 잠깐 꺼야 한다
            var p = ctx.Dog.transform.position;
            ctx.Dog.transform.position = new Vector3(Xz.x, p.y, Xz.y);
            if (cc != null) cc.enabled = true;
        }
        public override bool Update(ScriptContext ctx, float dt) => true;
        public override string Describe() => $"dog at ({Xz.x}, {Xz.y})";
    }

    /// 목표 지점까지 이동. duration(초) 또는 speed(m/s) 중 하나로 지정.
    public class DogMoveAction : ScriptAction
    {
        public Vector2 Target;
        public float Duration = -1f;   // >0 이면 이 시간에 맞춰 도착
        public float Speed = -1f;      // >0 이면 이 속도로 이동
        public bool Sprint;            // 질주(4족)
        public bool FaceMoveDirection = true;

        private Vector3 _start;
        private float _t, _total;

        public override void Start(ScriptContext ctx)
        {
            if (ctx.Dog == null) return;
            ctx.Dog.ExternalControl = true;
            _start = ctx.Dog.transform.position;
            Vector3 end = new Vector3(Target.x, _start.y, Target.y);
            float dist = Vector3.Distance(new Vector3(_start.x, 0f, _start.z),
                                          new Vector3(end.x, 0f, end.z));

            if (Duration > 0f) _total = Duration;
            else
            {
                float v = Speed > 0f ? Speed : (Sprint ? GameConstants.RunSpeed : GameConstants.WalkSpeed);
                _total = v > 0.01f ? dist / v : 0f;
            }
            _t = 0f;
        }

        public override bool Update(ScriptContext ctx, float dt)
        {
            if (ctx.Dog == null) return true;
            _t += dt;
            float k = _total <= 0.0001f ? 1f : Mathf.Clamp01(_t / _total);
            // 가감속 없이 등속 — 스크립트는 예측 가능해야 한다 (연출 타이밍이 맞아야 함)
            Vector3 from = new Vector3(_start.x, ctx.Dog.transform.position.y, _start.z);
            Vector3 to = new Vector3(Target.x, ctx.Dog.transform.position.y, Target.y);
            Vector3 next = Vector3.Lerp(from, to, k);

            Vector3 delta = next - ctx.Dog.transform.position;
            if (FaceMoveDirection)
            {
                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                if (flat.sqrMagnitude > 1e-6f)
                    ctx.Dog.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }

            var cc = ctx.DogController;
            if (cc != null && cc.enabled) cc.Move(delta + Vector3.up * -0.02f);
            else ctx.Dog.transform.position = next;

            if (ctx.Stance != null) ctx.Stance.Tick(Sprint, dt);
            return k >= 1f;
        }

        public override void Stop(ScriptContext ctx)
        {
            if (ctx.Stance != null) ctx.Stance.Tick(false, 0.016f);
        }

        public override string Describe()
            => $"dog {(Sprint ? "run" : "move")} ({Target.x}, {Target.y})" +
               (Duration > 0f ? $" in {Duration}s" : Speed > 0f ? $" at {Speed}" : "");
    }

    public class DogFaceAction : ScriptAction
    {
        public float Degrees;
        public override void Start(ScriptContext ctx)
        {
            if (ctx.Dog != null)
                ctx.Dog.transform.rotation = Quaternion.Euler(0f, Degrees, 0f);
        }
        public override bool Update(ScriptContext ctx, float dt) => true;
        public override string Describe() => $"dog face {Degrees}";
    }

    public class DogJumpAction : ScriptAction
    {
        private float _t;
        private float _air;
        public override void Start(ScriptContext ctx)
        {
            _t = 0f;
            _air = 2f * Mathf.Sqrt(2f * -GameConstants.Gravity * GameConstants.JumpHeight) / -GameConstants.Gravity;
            if (ctx.Dog != null) ctx.Dog.RequestScriptedJump();
        }
        public override bool Update(ScriptContext ctx, float dt)
        {
            _t += dt;
            return _t >= _air;   // 착지까지 기다린다
        }
        public override string Describe() => "dog jump";
    }

    public class DogStanceAction : ScriptAction
    {
        public bool Quad;
        private float _t;
        public override void Start(ScriptContext ctx) { _t = 0f; }
        public override bool Update(ScriptContext ctx, float dt)
        {
            _t += dt;
            if (ctx.Stance != null) ctx.Stance.Tick(Quad, dt);
            return _t >= GameConstants.StanceTransitionSeconds;
        }
        public override string Describe() => Quad ? "dog stance quad" : "dog stance biped";
    }

    /// 애니메이션 클립 도입 전 자리표시자 — 로그를 남기고 지정 시간만큼 기다린다.
    public class DogMotionAction : ScriptAction
    {
        public string Motion;
        public float Seconds = 1f;
        private float _t;
        public override void Start(ScriptContext ctx)
        {
            _t = 0f;
            Debug.Log($"[Script] motion '{Motion}' ({Seconds}s) — 클립 미구현, 대기만 함");
        }
        public override bool Update(ScriptContext ctx, float dt)
        {
            _t += dt;
            return _t >= Seconds;
        }
        public override string Describe() => $"dog motion {Motion} for {Seconds}s";
    }

    // ---------- 카메라 ----------

    /// FOV·거리·높이·피치를 시간에 걸쳐 보간
    public class CamValueAction : ScriptAction
    {
        public enum Kind { Fov, Distance, Height, Pitch }
        public Kind Which;
        public float Target;
        public float Duration;

        private float _from, _t;

        public override void Start(ScriptContext ctx)
        {
            _t = 0f;
            _from = Read(ctx);
        }

        public override bool Update(ScriptContext ctx, float dt)
        {
            _t += dt;
            float k = Duration <= 0.0001f ? 1f : Mathf.Clamp01(_t / Duration);
            Write(ctx, Mathf.Lerp(_from, Target, Mathf.SmoothStep(0f, 1f, k)));
            return k >= 1f;
        }

        private float Read(ScriptContext ctx)
        {
            if (Which == Kind.Fov) return ctx.Camera != null ? ctx.Camera.fieldOfView : GameConstants.CameraFov;
            if (ctx.Cam == null) return 0f;
            switch (Which)
            {
                case Kind.Distance: return ctx.Cam.ScriptDistance;
                case Kind.Height: return ctx.Cam.ScriptHeight;
                case Kind.Pitch: return ctx.Camera != null ? ctx.Camera.transform.eulerAngles.x : 0f;
            }
            return 0f;
        }

        private void Write(ScriptContext ctx, float v)
        {
            switch (Which)
            {
                case Kind.Fov:
                    if (ctx.Camera != null) ctx.Camera.fieldOfView = v;
                    break;
                case Kind.Distance:
                    if (ctx.Cam != null) ctx.Cam.ScriptDistance = v;
                    break;
                case Kind.Height:
                    if (ctx.Cam != null) ctx.Cam.ScriptHeight = v;
                    break;
                case Kind.Pitch:
                    if (ctx.Camera != null)
                    {
                        var e = ctx.Camera.transform.eulerAngles;
                        ctx.Camera.transform.rotation = Quaternion.Euler(v, e.y, e.z);
                    }
                    break;
            }
        }

        public override string Describe() => $"cam {Which.ToString().ToLower()} {Target} in {Duration}s";
    }

    /// 초당 N미터로 카메라만 이동 (누룽이 추적 해제) — "초당 카메라 움직임"
    public class CamPanAction : ScriptAction
    {
        public Vector3 MetersPerSecond;
        public float Duration;
        private float _t;

        public override void Start(ScriptContext ctx)
        {
            _t = 0f;
            ctx.CameraDetached = true;
            if (ctx.Cam != null) ctx.Cam.enabled = false;
        }

        public override bool Update(ScriptContext ctx, float dt)
        {
            _t += dt;
            if (ctx.Camera != null) ctx.Camera.transform.position += MetersPerSecond * dt;
            return _t >= Duration;
        }

        public override string Describe() => $"cam pan {MetersPerSecond.x} in {Duration}s";
    }

    /// 다시 누룽이를 따라가게
    public class CamFollowAction : ScriptAction
    {
        public override void Start(ScriptContext ctx)
        {
            ctx.CameraDetached = false;
            if (ctx.Cam != null)
            {
                ctx.Cam.enabled = true;
                if (ctx.Dog != null) ctx.Cam.SetTarget(ctx.Dog.transform);
            }
        }
        public override bool Update(ScriptContext ctx, float dt) => true;
        public override string Describe() => "cam follow";
    }

    public class LogAction : ScriptAction
    {
        public string Message;
        public override void Start(ScriptContext ctx) => Debug.Log($"[Script] {Message}");
        public override bool Update(ScriptContext ctx, float dt) => true;
        public override string Describe() => $"say {Message}";
    }
}
