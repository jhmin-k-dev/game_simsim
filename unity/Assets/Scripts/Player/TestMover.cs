using Nurungi.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nurungi.Player
{
    /// <summary>
    /// [임시] SafeBoxCamera 검증용 WASD 이동체.
    /// 정식 이동 컨트롤러(01_기획서 §3: 두 조작 방식 + 마지막 입력 규칙)는 별도 작업으로 대체 예정.
    /// 속도·가감속 값은 GameConstants(01 §3-3 초안값)를 그대로 사용해 손맛 미리보기를 겸한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TestMover : MonoBehaviour
    {
        private CharacterController _cc;
        private Vector3 _velocity;      // 수평 속도
        private float _verticalVel;     // 중력

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            Vector2 input = ReadInput();
            bool run = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            float maxSpeed = run ? GameConstants.RunSpeed : GameConstants.WalkSpeed;

            Vector3 wishDir = new Vector3(input.x, 0f, input.y);
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            if (wishDir.sqrMagnitude > GameConstants.InputDeadZone * GameConstants.InputDeadZone)
            {
                // 가속 (01 §3-3)
                _velocity = Vector3.MoveTowards(_velocity, wishDir * maxSpeed, GameConstants.MoveAccel * Time.deltaTime);

                // 이동 방향으로 회전
                float turnSpeed = run ? GameConstants.TurnSpeedRunDeg : GameConstants.TurnSpeedWalkDeg;
                Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
            else
            {
                // 감속 (01 §3-3)
                _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, GameConstants.MoveDecel * Time.deltaTime);
            }

            // 중력
            if (_cc.isGrounded) _verticalVel = -1f;
            else _verticalVel += Physics.gravity.y * Time.deltaTime;

            Vector3 motion = _velocity + Vector3.up * _verticalVel;
            _cc.Move(motion * Time.deltaTime);
        }

        private static Vector2 ReadInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = 0f, y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
    }
}
