using System.Collections.Generic;
using UnityEngine;

namespace Nurungi.World
{
    /// <summary>
    /// 도로 차량 통행. 장난감 같은 토이카가 두 방향 차선으로 지나간다.
    /// §12(실패 없음): 차는 위험 요소가 아니다 — 누룽이가 앞에 있으면 멈춰서 기다려준다.
    /// 차 모델은 프리미티브 조립(에셋 불요) — 추후 3D 소품으로 교체.
    /// </summary>
    public class CarTraffic : MonoBehaviour
    {
        [Header("차선 (z) — 도로 박스 안")]
        public float laneRightZ = -1.7f;   // +X 방향 진행
        public float laneLeftZ = -2.9f;    // -X 방향 진행

        [Header("스폰")]
        public Vector2 spawnIntervalRange = new Vector2(5f, 10f);
        public Vector2 speedRange = new Vector2(3.2f, 4.4f);
        public float spawnDistance = 26f;   // 카메라 밖에서 등장
        public float despawnDistance = 34f;

        [Header("매너 운전 — 늦게 발견하면 못 멈춘다 (충돌 = 슬랩스틱 넉백)")]
        public float politeStopDistance = 1.6f;
        public float brakeDecel = 5f;
        public float startledPause = 2.2f;   // 치고 나서 놀라 멈추는 시간

        private static readonly Color[] Palette =
        {
            new Color(0.66f, 0.68f, 0.49f),   // 올리브
            new Color(0.80f, 0.55f, 0.42f),   // 테라코타
            new Color(0.93f, 0.87f, 0.72f),   // 크림
            new Color(0.55f, 0.58f, 0.64f),   // 블루그레이
        };

        private Transform _cam;
        private Transform _player;
        private readonly List<Car> _cars = new List<Car>();
        private float _nextSpawnR;
        private float _nextSpawnL;

        private class Car
        {
            public Transform Root;
            public Transform[] Wheels;
            public float Speed;
            public int Dir;             // +1 / -1
            public float CurrentSpeed;
            public float PauseTimer;    // 치고 나서 놀라 멈춘 시간
        }

        private void Start()
        {
            var cam = UnityEngine.Camera.main;
            _cam = cam != null ? cam.transform : transform;
            var mover = FindFirstObjectByType<Player.PlayerMover>();
            _player = mover != null ? mover.transform : null;
            _nextSpawnR = Random.Range(1f, 4f);
            _nextSpawnL = Random.Range(2f, 6f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _nextSpawnR -= dt;
            if (_nextSpawnR <= 0f)
            {
                Spawn(+1, laneRightZ);
                _nextSpawnR = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
            }
            _nextSpawnL -= dt;
            if (_nextSpawnL <= 0f)
            {
                Spawn(-1, laneLeftZ);
                _nextSpawnL = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
            }

            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                var car = _cars[i];
                if (car.Root == null) { _cars.RemoveAt(i); continue; }

                // 매너 운전: 앞에 누룽이가 보이면 브레이크 — 단, 갑자기 뛰어들면 못 멈춘다
                float targetSpeed = car.Speed;
                if (car.PauseTimer > 0f)
                {
                    car.PauseTimer -= dt;
                    targetSpeed = 0f;
                }
                else if (_player != null)
                {
                    Vector3 toPlayer = _player.position - car.Root.position;
                    bool ahead = Mathf.Sign(toPlayer.x) == car.Dir;
                    bool inLane = Mathf.Abs(toPlayer.z) < 1.1f;
                    if (ahead && inLane && Mathf.Abs(toPlayer.x) < politeStopDistance)
                        targetSpeed = 0f;
                }
                car.CurrentSpeed = Mathf.MoveTowards(car.CurrentSpeed, targetSpeed, brakeDecel * dt);
                car.Root.position += Vector3.right * car.Dir * car.CurrentSpeed * dt;

                // ---- 충돌: 달리는 차와 겹치면 누룽이가 붕 날아간다 (만화식) ----
                if (_player != null && car.CurrentSpeed > 1.1f)
                {
                    Vector3 diff = _player.position - car.Root.position;
                    if (Mathf.Abs(diff.x) < 0.95f && Mathf.Abs(diff.z) < 0.6f && _player.position.y < 0.6f)
                    {
                        var mover = _player.GetComponent<Player.PlayerMover>();
                        if (mover != null && !mover.IsKnocked)
                        {
                            Vector3 launch = new Vector3(
                                car.Dir * (car.CurrentSpeed * 1.1f + 2.2f),
                                5.6f,
                                Random.Range(-0.4f, 0.7f));
                            mover.Knockback(launch, -car.Dir);
                            HitEffects.ImpactBurst(car.Root.position
                                + Vector3.right * car.Dir * 0.7f + Vector3.up * 0.35f);
                            car.PauseTimer = startledPause;   // 차도 놀라서 급정거
                            car.CurrentSpeed = 0.2f;
                        }
                    }
                }

                // 만화적 바퀴 회전 + 몸통 미세 흔들림
                float wheelDeg = car.CurrentSpeed / 0.09f * Mathf.Rad2Deg * dt;
                foreach (var w in car.Wheels) w.Rotate(car.Dir * wheelDeg, 0f, 0f, Space.Self);
                float wobble = car.CurrentSpeed > 0.2f ? Mathf.Sin(Time.time * 21f) * 0.6f : 0f;
                car.Root.localRotation = Quaternion.Euler(0f, car.Dir > 0 ? 90f : -90f, wobble);

                if (Mathf.Abs(car.Root.position.x - _cam.position.x) > despawnDistance)
                {
                    Destroy(car.Root.gameObject);
                    _cars.RemoveAt(i);
                }
            }
        }

        private void Spawn(int dir, float laneZ)
        {
            float x = _cam.position.x - dir * spawnDistance;
            if (x < 2f || x > 598f) return;   // 챕터 밖 스폰 방지

            var root = new GameObject(dir > 0 ? "Car_R" : "Car_L");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(x, 0f, laneZ);

            Color body = Palette[Random.Range(0, Palette.Length)];
            var car = new Car
            {
                Root = root.transform,
                Dir = dir,
                Speed = Random.Range(speedRange.x, speedRange.y),
                CurrentSpeed = 0.5f,
                Wheels = BuildToyCar(root.transform, body),
            };
            _cars.Add(car);
        }

        /// 프리미티브 토이카 (로컬 +Z가 정면, 루트 회전으로 방향 결정)
        private Transform[] BuildToyCar(Transform root, Color bodyColor)
        {
            Material mat = MakeToon(bodyColor);
            Material dark = MakeToon(bodyColor * 0.72f);
            Material wheelMat = MakeToon(new Color(0.32f, 0.28f, 0.24f));

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.26f, 1.5f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cabin.GetComponent<Collider>());
            cabin.name = "Cabin";
            cabin.transform.SetParent(root, false);
            cabin.transform.localPosition = new Vector3(0f, 0.44f, -0.12f);
            cabin.transform.localScale = new Vector3(0.54f, 0.22f, 0.72f);
            cabin.GetComponent<MeshRenderer>().sharedMaterial = dark;

            var wheels = new Transform[4];
            var offsets = new[]
            {
                new Vector3(-0.3f, 0.11f, 0.48f), new Vector3(0.3f, 0.11f, 0.48f),
                new Vector3(-0.3f, 0.11f, -0.48f), new Vector3(0.3f, 0.11f, -0.48f),
            };
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(w.GetComponent<Collider>());
                w.name = "Wheel";
                w.transform.SetParent(root, false);
                w.transform.localPosition = offsets[i];
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                w.transform.localScale = new Vector3(0.22f, 0.045f, 0.22f);
                w.GetComponent<MeshRenderer>().sharedMaterial = wheelMat;
                wheels[i] = w.transform;
            }

            // 부드러운 차단용 콜라이더 (누룽이를 밀지 않고 막기만)
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.3f, 0f);
            col.size = new Vector3(0.65f, 0.6f, 1.55f);
            var rb = root.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            return wheels;
        }

        private static Material MakeToon(Color c)
        {
            var shader = Shader.Find("Nurungi/Toon");
            var mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", c);
            if (shader != null)
            {
                mat.SetColor("_ShadowColor", new Color(0.88f, 0.76f, 0.6f));
                mat.SetColor("_OutlineColor", new Color(0.43f, 0.37f, 0.29f));
                mat.SetFloat("_OutlineWidth", 0.0026f);
            }
            return mat;
        }
    }
}
