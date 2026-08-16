using System.IO;
using Nurungi.CameraSystem;
using Nurungi.Config;
using Nurungi.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nurungi.Build
{
    /// <summary>
    /// 07 §8 1주차: SafeBoxCamera + 회색 박스 맵 자동 생성.
    /// 새 기능은 회색 큐브 테스트 씬에서 먼저 검증 (02 §8-8).
    /// </summary>
    public static class CreateGreyBoxScene
    {
        private const string ScenePath = "Assets/Scenes/GreyBox.unity";

        [MenuItem("Tools/누룽이/Create GreyBox Scene")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 조명 ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ---- 바닥: +X 진행 방향으로 길게 (03 §5) ----
            CreateBox("Ground", new Vector3(48f, -0.25f, 0f), new Vector3(120f, 0.5f, 10f));

            // ---- 장애물·구조물 ----
            CreateBox("Box_1", new Vector3(8f, 0.5f, 1.5f), Vector3.one);
            CreateBox("Box_2", new Vector3(14f, 0.75f, -2f), new Vector3(1.5f, 1.5f, 1.5f));
            CreateBox("Box_3", new Vector3(20f, 0.5f, 0f), new Vector3(1f, 1f, 3f));
            CreateBox("Wall_1", new Vector3(30f, 1f, 4.2f), new Vector3(8f, 2f, 0.5f));

            // 경사 15° (04 §1-4 발 IK 검증용을 미리 준비)
            var ramp = CreateBox("Ramp_15deg", new Vector3(40f, 0.8f, 0f), new Vector3(8f, 0.4f, 4f));
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -15f);

            // 계단
            for (int i = 0; i < 4; i++)
            {
                CreateBox($"Stair_{i}", new Vector3(52f + i * 0.8f, 0.15f + i * 0.3f, 0f), new Vector3(0.8f, 0.3f + i * 0.6f, 3f));
            }

            // ---- 플레이어(임시) ----
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player_Temp";
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.transform.position = new Vector3(0f, 0.6f, 0f);
            player.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            player.AddComponent<PlayerMover>();

            // ---- 카메라 (02 §2-1: FOV 28, 부감 15°, 거리 12, 높이 3.2) ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.fieldOfView = GameConstants.CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.rotation = Quaternion.Euler(GameConstants.CameraPitchDeg, 0f, 0f);
            camGo.transform.position = player.transform.position
                - camGo.transform.forward * GameConstants.CameraDistance
                + Vector3.up * GameConstants.CameraHeight;
            var safeBox = camGo.AddComponent<SafeBoxCamera>();
            safeBox.SetTarget(player.transform);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[GreyBox] 씬 생성 완료: {ScenePath} — Play 모드에서 WASD(+Shift 달리기)로 확인");
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            return go;
        }
    }
}
