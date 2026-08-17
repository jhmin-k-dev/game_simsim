using UnityEngine;

namespace Nurungi.Player
{
    /// <summary>
    /// 누룽이의 가방 소지. 2족 = 손(hand.L)에 들고, 4족 = 입(머리 앞)에 문다.
    /// 스탠스 전환(QuadBlend)에 따라 두 앵커 사이를 부드럽게 이동.
    /// </summary>
    public class BagCarrier : MonoBehaviour
    {
        public Transform Bag { get; private set; }
        public bool HasBag => Bag != null;

        private StanceController _stance;
        private Transform _hand;
        private Transform _head;

        private void Awake()
        {
            _stance = GetComponent<StanceController>();
        }

        public void PickUp(Transform bag)
        {
            Bag = bag;
            // 본 찾기 (리깅 FBX)
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name == "hand.L") _hand = t;
                else if (t.name == "head") _head = t;
            }
            foreach (var c in bag.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var rb in bag.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            bag.SetParent(null); // 월드 유지, LateUpdate에서 직접 배치

            Save.SaveSystem.Current.Collect("bag_01");
            Save.SaveSystem.Save();
        }

        private void LateUpdate()
        {
            if (Bag == null) return;
            float quad = _stance != null ? Mathf.SmoothStep(0f, 1f, _stance.QuadBlend) : 0f;

            // 손 앵커: 왼손 본 옆, 아래로 늘어뜨림 (본이 없으면 몸 옆)
            Vector3 handPos = _hand != null
                ? _hand.position + Vector3.down * 0.06f + transform.right * -0.02f
                : transform.position + transform.right * -0.12f + Vector3.up * 0.12f;

            // 입 앵커: 머리 앞쪽 살짝 아래
            Vector3 mouthPos = _head != null
                ? _head.position + transform.forward * 0.14f + Vector3.down * 0.05f
                : transform.position + transform.forward * 0.2f + Vector3.up * 0.2f;

            Bag.position = Vector3.Lerp(handPos, mouthPos, quad);
            // 손: 손잡이가 위 / 입: 손잡이를 물어 가방이 앞으로 늘어짐
            Quaternion handRot = transform.rotation;
            Quaternion mouthRot = transform.rotation * Quaternion.Euler(35f, 0f, 0f);
            Bag.rotation = Quaternion.Slerp(handRot, mouthRot, quad);
        }
    }
}
