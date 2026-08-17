using UnityEngine;

namespace Nurungi.World
{
    /// <summary>
    /// 맵에 놓인 가방. 닿으면 이펙트와 함께 누룽이가 들고 다닌다.
    /// 이미 획득한 세이브면 시작 시 바로 들려 있음.
    /// </summary>
    public class BagPickup : MonoBehaviour
    {
        [SerializeField] private string collectId = "bag_01";
        [SerializeField] private float bobAmount = 0.025f;
        [SerializeField] private float bobSpeed = 2.2f;

        private Vector3 _basePos;
        private bool _taken;

        private void Start()
        {
            _basePos = transform.position;

            // 이미 획득한 상태로 챕터 재입장 → 즉시 소지
            if (Save.SaveSystem.Current.HasCollected(collectId))
            {
                var carrier = FindFirstObjectByType<Player.BagCarrier>();
                if (carrier != null && !carrier.HasBag)
                {
                    _taken = true;
                    carrier.PickUp(transform);
                }
            }
        }

        private void Update()
        {
            if (_taken) return;
            // 두둥실 (놓여 있을 때만)
            float y = _basePos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            transform.position = new Vector3(_basePos.x, y, _basePos.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            var carrier = other.GetComponentInParent<Player.BagCarrier>();
            if (carrier == null || carrier.HasBag) return;

            _taken = true;
            PlayPickupFx();
            carrier.PickUp(transform);
        }

        /// 획득 이펙트: 따뜻한 색 반짝임 버스트 (에셋 없이 코드로)
        private void PlayPickupFx()
        {
            var go = new GameObject("PickupFX");
            go.transform.position = transform.position;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.45f), new Color(1f, 0.65f, 0.35f));
            main.gravityModifier = -0.15f; // 살짝 떠오르게
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", Color.white);
                renderer.material = mat;
            }

            ps.Play();
            Destroy(go, 2f);
        }
    }
}
