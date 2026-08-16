using System.Collections.Generic;
using System.IO;
using Nurungi.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nurungi.Build
{
    /// <summary>
    /// 타이틀(월드/맵 선택) 씬 생성 + 빌드 씬 목록 정리.
    /// UI는 TitleMenu가 런타임에 구성하므로 씬에는 카메라와 매니저만 둔다.
    /// </summary>
    public static class CreateTitleScene
    {
        private const string ScenePath = "Assets/Scenes/Title.unity";

        [MenuItem("Tools/누룽이/Create Title Scene")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(237 / 255f, 227 / 255f, 208 / 255f);
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            var menuGo = new GameObject("TitleMenu");
            menuGo.AddComponent<TitleMenu>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Title] 씬 생성: {ScenePath}");

            SyncBuildScenes();
        }

        /// 빌드 씬 목록을 Title → 챕터 씬 순서로 맞춘다.
        /// (기본 URP 템플릿의 SampleScene만 들어 있으면 EXE가 타이틀을 못 띄운다)
        [MenuItem("Tools/누룽이/Sync Build Scenes")]
        public static void SyncBuildScenes()
        {
            string[] wanted =
            {
                "Assets/Scenes/Title.unity",
                "Assets/Scenes/StreetMock.unity",
                "Assets/Scenes/GreyBox.unity",
            };

            var list = new List<EditorBuildSettingsScene>();
            foreach (var path in wanted)
            {
                if (File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, true));
                else Debug.LogWarning($"[Build] 씬 없음, 건너뜀: {path}");
            }

            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[Build] 빌드 씬 {list.Count}개 등록 (첫 씬 = {(list.Count > 0 ? list[0].path : "없음")})");
        }
    }
}
