using System.Collections.Generic;
using UnityEngine;

namespace Nurungi.Player
{
    /// <summary>
    /// 누룽이가 관심을 갖는 지점 (01 §4-3 2 룩앳의 대상).
    /// 전봇대·덤불·NPC 등에 붙인다. 상호작용 시스템(01 §6)이 생기면 그쪽과 통합.
    /// </summary>
    public class LookInterestPoint : MonoBehaviour
    {
        [Tooltip("이 거리 안에 들어와야 쳐다본다")]
        public float radius = 4f;

        [Tooltip("높을수록 다른 지점보다 우선")]
        public int priority = 0;

        private static readonly List<LookInterestPoint> All = new List<LookInterestPoint>();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// pos에서 가장 관심 가는 지점 (없으면 null)
        public static LookInterestPoint FindBest(Vector3 pos)
        {
            // OnEnable이 돌지 않는 에디터 시뮬레이션에서는 씬을 직접 스캔
            IEnumerable<LookInterestPoint> pool = All;
            if (All.Count == 0)
                pool = FindObjectsByType<LookInterestPoint>(FindObjectsSortMode.None);

            LookInterestPoint best = null;
            float bestScore = float.MinValue;
            foreach (var p in pool)
            {
                float d = Vector3.Distance(pos, p.transform.position);
                if (d > p.radius) continue;
                // 가깝고 우선순위 높을수록 점수 ↑
                float score = p.priority * 10f - d;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
