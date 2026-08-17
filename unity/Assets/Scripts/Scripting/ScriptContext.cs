using Nurungi.CameraSystem;
using Nurungi.Player;
using UnityEngine;

namespace Nurungi.Scripting
{
    /// <summary>
    /// 스크립트가 조작하는 대상 묶음. 씬이 바뀌면 Refresh()로 다시 찾는다.
    /// </summary>
    public class ScriptContext
    {
        public PlayerMover Dog;
        public StanceController Stance;
        public CharacterController DogController;
        public SafeBoxCamera Cam;
        public UnityEngine.Camera Camera;

        /// 카메라 자유 이동 중이면 SafeBoxCamera 추적을 끈다
        public bool CameraDetached;
        public Vector3 CameraFreeVelocity;

        public bool IsReady => Dog != null && Camera != null;

        public void Refresh()
        {
            Dog = Object.FindFirstObjectByType<PlayerMover>();
            if (Dog != null)
            {
                Stance = Dog.GetComponent<StanceController>();
                DogController = Dog.GetComponent<CharacterController>();
            }
            Cam = Object.FindFirstObjectByType<SafeBoxCamera>();
            Camera = UnityEngine.Camera.main;
        }

        /// 스크립트가 끝나면 원상복구 — 연출이 중간에 끊겨도 카메라 값이 어중간하게 남지 않게
        public void ReleaseAll()
        {
            if (Dog != null) Dog.ExternalControl = false;
            if (Cam != null) Cam.enabled = true;
            if (Camera != null) Camera.fieldOfView = Config.GameConstants.CameraFov;
            if (Cam != null)
            {
                Cam.ScriptDistance = Config.GameConstants.CameraDistance;
                Cam.ScriptHeight = Config.GameConstants.CameraHeight;
            }
            CameraDetached = false;
            CameraFreeVelocity = Vector3.zero;
        }
    }
}
