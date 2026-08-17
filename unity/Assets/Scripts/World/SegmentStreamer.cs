using System.Collections.Generic;
using UnityEngine;

namespace Nurungi.World
{
    /// <summary>
    /// 03_대형스크롤맵 §3: 세그먼트 스트리밍.
    /// 현재 위치 ±2 = 최대 5장만 유지 (02 §4 예산). 윈도우를 벗어나면 1.5초 유예 후 해제(§3-2),
    /// 전환 판정선은 진행 방향에 따라 ±1.5m 어긋난다(§3-3 히스테리시스).
    ///
    /// 정식판은 Addressables 비동기 로드(08 G3)로 교체 — 지금은 텍스처 풀에서 quad를 만들어
    /// 같은 윈도우·유예·히스테리시스 로직을 돌린다 (로직이 본체, 로더는 부품).
    /// </summary>
    public class SegmentStreamer : MonoBehaviour
    {
        [Header("세그먼트 규격 (03 §2-3)")]
        public float segmentWidth = 15f;
        public int segmentCount = 40;          // 챕터 길이 = width × count
        public int keepRadius = 2;             // 현재 ±2 (02 §4)
        public float unloadDelay = 1.5f;       // §3-2 해제 유예
        public float hysteresis = 1.5f;        // §3-3

        [Header("세그먼트 판 규격")]
        public float quadHeight = 8.44f;
        public float quadCenterY = 4.52f;
        public float quadZ = 14f;

        public Texture2D[] textures;           // 순환 사용
        public Material templateMaterial;      // Unlit 템플릿

        private Transform _target;             // 보통 카메라
        private readonly Dictionary<int, GameObject> _loaded = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, float> _pendingUnload = new Dictionary<int, float>();
        private int _currentIndex = int.MinValue;

        public int LoadedCount => _loaded.Count;

        private void Start()
        {
            var cam = UnityEngine.Camera.main;
            _target = cam != null ? cam.transform : transform;
            RefreshWindow(ComputeIndex(_target.position.x - transform.position.x, int.MinValue));
        }

        private void Update()
        {
            if (_target == null) return;
            // 부모가 시차(ParallaxLayer)로 움직여도 어긋나지 않게 로컬 좌표로 판정
            float relX = _target.position.x - transform.position.x;
            int idx = ComputeIndex(relX, _currentIndex);
            if (idx != _currentIndex) RefreshWindow(idx);
            TickUnload();
        }

        /// §3-3: 전환선을 진행 방향 쪽으로 ±1.5m 밀어 경계 왕복 시 로드가 튀지 않게
        private int ComputeIndex(float x, int current)
        {
            int naive = Mathf.FloorToInt(x / segmentWidth);
            if (current == int.MinValue) return naive;
            if (naive > current)
            {
                float boundary = naive * segmentWidth + hysteresis;
                return x >= boundary ? naive : current;
            }
            if (naive < current)
            {
                float boundary = current * segmentWidth - hysteresis;
                return x <= boundary ? naive : current;
            }
            return current;
        }

        private void RefreshWindow(int center)
        {
            _currentIndex = center;

            for (int i = center - keepRadius; i <= center + keepRadius; i++)
            {
                if (i < 0 || i >= segmentCount) continue;
                _pendingUnload.Remove(i);          // 다시 창에 들어오면 해제 취소
                if (!_loaded.ContainsKey(i)) Load(i);
            }

            var toSchedule = new List<int>();
            foreach (var kv in _loaded)
                if (Mathf.Abs(kv.Key - center) > keepRadius && !_pendingUnload.ContainsKey(kv.Key))
                    toSchedule.Add(kv.Key);
            foreach (int i in toSchedule)
                _pendingUnload[i] = Time.time + unloadDelay;   // §3-2 유예
        }

        private void TickUnload()
        {
            if (_pendingUnload.Count == 0) return;
            var due = new List<int>();
            foreach (var kv in _pendingUnload)
                if (Time.time >= kv.Value) due.Add(kv.Key);
            foreach (int i in due)
            {
                _pendingUnload.Remove(i);
                if (_loaded.TryGetValue(i, out var go))
                {
                    _loaded.Remove(i);
                    Destroy(go);
                }
            }
        }

        private void Load(int index)
        {
            // §3-4: 실패해도 게임은 계속 — 텍스처가 없으면 단색 판
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Seg_{index:000}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3((index + 0.5f) * segmentWidth, quadCenterY, quadZ);
            go.transform.localScale = new Vector3(segmentWidth + 0.02f, quadHeight, 1f);

            var mr = go.GetComponent<MeshRenderer>();
            var mat = templateMaterial != null
                ? new Material(templateMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (textures != null && textures.Length > 0)
                mat.SetTexture("_BaseMap", textures[((index % textures.Length) + textures.Length) % textures.Length]);
            mr.sharedMaterial = mat;

            _loaded[index] = go;
        }
    }
}
