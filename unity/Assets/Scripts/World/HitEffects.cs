using System.Collections;
using UnityEngine;

namespace Nurungi.World
{
    /// <summary>
    /// 충돌 연출 도우미: 히트스톱(순간 정지) + 충격 버스트 + 어지러움 별.
    /// §12 실패 없음 — 데미지가 아니라 만화식 슬랩스틱 연출이다.
    /// </summary>
    public static class HitEffects
    {
        public static void HitStop(float duration = 0.09f)
        {
            HitStopRunner.Get().Run(duration);
        }

        /// 충격 지점 버스트 (노랑·흰 파편)
        public static void ImpactBurst(Vector3 position)
        {
            var go = new GameObject("ImpactFX");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.95f, 0.75f), new Color(1f, 0.75f, 0.4f));
            main.gravityModifier = 0.6f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            ApplyUnlit(ps);
            ps.Play();
            Object.Destroy(go, 1.5f);
        }

        /// 머리 위 빙글빙글 별 (회복 시간 동안)
        public static void DizzyStars(Transform followTarget, float duration, float headHeight)
        {
            var go = new GameObject("DizzyStars");
            var follow = go.AddComponent<FollowPoint>();
            follow.target = followTarget;
            follow.offset = Vector3.up * headHeight;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startColor = new Color(1f, 0.85f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = 9f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.16f;

            // 궤도 회전 — 빙글빙글
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(6f);

            ApplyUnlit(ps);
            ps.Play();
            Object.Destroy(go, duration);
        }

        private static void ApplyUnlit(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", Color.white);
                renderer.material = mat;
            }
        }
    }

    public class FollowPoint : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset;
        private void LateUpdate()
        {
            if (target != null) transform.position = target.position + offset;
        }
    }

    public class HitStopRunner : MonoBehaviour
    {
        private static HitStopRunner _instance;
        public static HitStopRunner Get()
        {
            if (_instance == null)
            {
                var go = new GameObject("HitStopRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HitStopRunner>();
            }
            return _instance;
        }

        public void Run(float duration) => StartCoroutine(Stop(duration));

        private IEnumerator Stop(float duration)
        {
            float prev = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = prev >= 0.9f ? 1f : prev;
        }
    }
}
