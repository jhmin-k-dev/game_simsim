using System.IO;
using Nurungi.CameraSystem;
using Nurungi.Config;
using Nurungi.Player;
using Nurungi.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Nurungi.Build
{
    /// <summary>
    /// M0 룩 목업: 참조 영상(가로수 인도 장면)의 느낌을 placeholder 아트로 재현.
    /// 레이어 구조는 03_대형스크롤맵 §2-1: 하늘(원경) / 배경판 / 컷아웃 중경·근경 / 3D 바닥 / 전경.
    /// 실행 후 build/mockup_preview.png 로 자동 캡처.
    /// </summary>
    public static class CreateStreetMockScene
    {
        private const string ScenePath = "Assets/Scenes/StreetMock.unity";
        private const string ArtBg = "Assets/Art/BG/street_mock";
        private const string ArtProp = "Assets/Art/Prop/street_mock";

        // 참조 영상 팔레트
        private static readonly Color Sky = C(237, 227, 208);
        private static readonly Color Sidewalk = C(216, 210, 192);
        private static readonly Color Curb = C(196, 189, 168);
        private static readonly Color Road = C(185, 180, 166);
        private static readonly Color DogFur = C(240, 205, 148);
        private static readonly Color DogEar = C(150, 116, 78);
        private static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        [MenuItem("Tools/누룽이/Create StreetMock Scene")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 환경: 웜톤 앰비언트 + 베이지 안개 ----
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = C(235, 224, 202);
            RenderSettings.fog = false; // 안개는 플랫 카툰 톤을 탁하게 만든다 (참조 영상은 완전 플랫)

            var lightGo = new GameObject("Sun");
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = C(255, 248, 232);
            sun.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            // ---- 3D 바닥: 인도 + 차도 (03 §2-1 시차 1.0 레인), 챕터 600m ----
            CreateGroundBox("Sidewalk", new Vector3(300f, -0.25f, 0.9f), new Vector3(620f, 0.5f, 3.2f), Sidewalk,
                $"{ArtBg}/pavement_tile.png", new Vector2(310f, 1.6f));
            CreateGroundBox("Curb", new Vector3(300f, -0.28f, -0.85f), new Vector3(620f, 0.5f, 0.25f), Curb);
            CreateGroundBox("Road", new Vector3(300f, -0.40f, -2.2f), new Vector3(620f, 0.5f, 2.5f), Road);

            // ---- 배경판: 세그먼트 스트리밍 (03 §3) + 은은한 시차 (03 §2-1) ----
            const float bgW = 15f, bgH = bgW * 1152f / 2048f;
            const float wallTopY = 1.2f;
            float quadCenterY = wallTopY - bgH * (0.5f - 0.775f); // 담벼락 상단(그림 77.5%)을 y=1.2에

            var bgStream = new GameObject("BGStream");
            var streamer = bgStream.AddComponent<SegmentStreamer>();
            streamer.segmentWidth = bgW;
            streamer.segmentCount = 40;            // 챕터 600m (03 §3 검증용 길이)
            streamer.quadHeight = bgH;
            streamer.quadCenterY = quadCenterY;
            streamer.quadZ = 14f;
            streamer.textures = new[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtBg}/bg_street_00.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtBg}/bg_street_01.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtBg}/bg_street_02.png"),
            };
            streamer.templateMaterial = MakeUnlitMaterial(Color.white, null, false);
            bgStream.AddComponent<ParallaxLayer>().factor = 0.85f; // 담벼락이 지면에 붙어 있어 은은하게만

            // ---- 원경 하늘 (z=+40) ----
            var skyQuad = CreateTexQuad("SkyFar", $"{ArtBg}/sky.png", unlit: true, alphaClip: false);
            skyQuad.transform.position = new Vector3(48f, 18f, 39f);
            skyQuad.transform.localScale = new Vector3(320f, 44f, 1f);
            skyQuad.AddComponent<ParallaxLayer>().factor = 0.1f; // 원경 (03 §2-1)

            // ---- 컷아웃: 가로수 (중경 z=+7) / 덤불 (근경 z=+2, 전경 z=-2.5) ----
            // 가로수: 인도 안쪽에 성기게 (참조 영상은 화면당 1그루 정도)
            for (int i = 0; i < 37; i++)
            {
                var tree = CreateTexQuad($"Tree_{i}", $"{ArtProp}/cutout_tree.png", unlit: true, alphaClip: true);
                // 나무 밑동이 인도(y=0)에 닿도록: 컷아웃 하단 여백 30px/1024 보정
                const float treeH = 4.2f;
                tree.transform.position = new Vector3(9f + i * 16f, treeH * 0.5f - 0.12f, 2.4f);
                tree.transform.localScale = new Vector3(treeH, treeH, 1f);
                // 누룽이가 지나가며 쳐다보는 관심 지점 (01 §4-3 2)
                var interest = tree.AddComponent<Player.LookInterestPoint>();
                interest.radius = 3.5f;
            }
            // 덤불: 담벼락 앞
            for (int i = 0; i < 28; i++)
            {
                var bush = CreateTexQuad($"Bush_{i}", $"{ArtProp}/cutout_bush.png", unlit: true, alphaClip: true);
                // 덤불 컷아웃은 512px 중 하단 ~90px가 여백 → 중심을 살짝 올려 밑동을 지면에 붙임
                const float bushH = 1.6f;
                bush.transform.position = new Vector3(3f + i * 21f, bushH * 0.5f - 0.28f, 2.1f);
                bush.transform.localScale = new Vector3(bushH, bushH, 1f);
            }

            // ---- 플레이어: 누룽이 초안 모델 ----
            var player = new GameObject("Player");
            player.transform.position = new Vector3(4f, 0.05f, 0.3f);
            // 진행 방향(+X) 기준이되, 정지 상태에서는 살짝 카메라 쪽으로 튼 3/4 뷰 (참조 영상 구도)
            player.transform.rotation = Quaternion.Euler(0f, 125f, 0f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 0.9f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0f, 0.45f, 0f);
            player.AddComponent<PlayerMover>();

            GameObject visualRoot = null;
            var dogAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Char/nurungi_draft.obj");
            if (dogAsset != null)
            {
                var visual = (GameObject)Object.Instantiate(dogAsset, player.transform);
                visual.name = "Visual";
                visualRoot = visual;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity; // Player가 이미 +X를 향함
                var furMat = MakeToonMaterial(DogFur);
                foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
                    r.sharedMaterial = furMat;

                // 블롭 섀도우 (02 §3-3 D5) — 발밑 접지감
                var shadow = CreateTexQuad("BlobShadow", $"{ArtProp}/blob_shadow.png", unlit: true, alphaClip: false);
                shadow.transform.SetParent(player.transform, false);
                shadow.transform.localPosition = new Vector3(0f, 0.012f, 0f);
                shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shadow.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
                var sm = shadow.GetComponent<MeshRenderer>().sharedMaterial;
                sm.SetFloat("_Surface", 1f); // Transparent
                sm.SetFloat("_Blend", 0f);
                sm.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sm.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                sm.SetFloat("_ZWrite", 0f);
                sm.renderQueue = 3000;
                sm.SetFloat("_AlphaClip", 0f);
                sm.DisableKeyword("_ALPHATEST_ON");
                sm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                Debug.LogWarning("[StreetMock] nurungi_draft.obj 미발견 — 캡슐로 대체");
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.transform.SetParent(player.transform, false);
                cap.transform.localScale = new Vector3(0.5f, 0.45f, 0.5f);
                cap.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                cap.name = "Visual";
                visualRoot = cap;
            }

            // 하이브리드 보행 (01 §4-4): 질주·지침 시 4족
            var stance = player.AddComponent<StanceController>();
            var stanceSo = new SerializedObject(stance);
            stanceSo.FindProperty("visual").objectReferenceValue = visualRoot != null ? visualRoot.transform : null;
            stanceSo.ApplyModifiedPropertiesWithoutUndo();

            // 절차적 레이어 6종 (01 §4-3) — 시각 합성을 전담
            var proc = player.AddComponent<ProceduralMotion>();
            var procSo = new SerializedObject(proc);
            procSo.FindProperty("visual").objectReferenceValue = visualRoot != null ? visualRoot.transform : null;
            procSo.ApplyModifiedPropertiesWithoutUndo();

            // 스크립트 콘솔 (F1) — 개발 도구
            new GameObject("ScriptConsole").AddComponent<Nurungi.Scripting.ScriptConsole>();

            // 챕터 진행·세이브 (01 §8)
            var sessionGo = new GameObject("ChapterSession");
            var session = sessionGo.AddComponent<ChapterSession>();
            var sessionSo = new SerializedObject(session);
            sessionSo.FindProperty("chapterId").stringValue = "street_01";
            sessionSo.FindProperty("player").objectReferenceValue = player.transform;
            sessionSo.FindProperty("goalX").floatValue = 590f; // 챕터 600m 끝점
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 카메라 + SafeBox + 포스트 ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.fieldOfView = GameConstants.CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            cam.backgroundColor = Sky;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            camGo.transform.rotation = Quaternion.Euler(GameConstants.CameraPitchDeg, 0f, 0f);
            camGo.transform.position = player.transform.position
                - camGo.transform.forward * GameConstants.CameraDistance
                + Vector3.up * GameConstants.CameraHeight;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            var safeBox = camGo.AddComponent<SafeBoxCamera>();
            safeBox.SetTarget(player.transform);

            // 볼륨: 비네트 + 웜 색보정 (02 §3-2)
            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.25f);
            vig.smoothness.Override(0.6f);
            var colAdj = profile.Add<ColorAdjustments>(true);
            colAdj.saturation.Override(4f);       // 참조 영상은 채도가 죽지 않은 따뜻한 크림톤
            colAdj.contrast.Override(-6f);        // 플랫하게
            colAdj.postExposure.Override(0.12f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(16f);
            wb.tint.Override(-3f);
            Directory.CreateDirectory("Assets/Settings/Mock");
            AssetDatabase.CreateAsset(profile, "Assets/Settings/Mock/StreetMockVolume.asset");
            vol.sharedProfile = profile;

            // 종이 그레인: 화면 고정 오버레이 (02 §3-2 — 카메라 따라 흐르지 않음)
            var canvasGo = new GameObject("PaperGrainOverlay");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // 캡처(RenderRequest)에도 찍히도록
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;
            canvas.sortingOrder = 100;
            var rawGo = new GameObject("Grain");
            rawGo.transform.SetParent(canvasGo.transform, false);
            var raw = rawGo.AddComponent<RawImage>();
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/FX/paper_grain.png");
            raw.uvRect = new Rect(0f, 0f, 3.75f, 2.11f);
            raw.color = new Color(1f, 1f, 1f, 0.55f);
            raw.raycastTarget = false;
            var rt = raw.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[StreetMock] 씬 생성: {ScenePath}");

            Capture();
        }

        [MenuItem("Tools/누룽이/Capture StreetMock Preview")]
        public static void Capture()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) { Debug.LogError("[StreetMock] Main Camera 없음"); return; }
            var rt = new RenderTexture(1920, 1080, 24);
            // URP는 Camera.Render() 미지원 → RenderRequest 사용 (Unity 6)
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            bool supported = RenderPipeline.SupportsRenderRequest(cam, request);
            // 씬을 갓 만든 직후엔 컬링 데이터가 비어 첫 렌더가 빈 화면이 된다 → 두 번 렌더
            for (int pass = 0; pass < 2; pass++)
            {
                if (supported) RenderPipeline.SubmitRenderRequest(cam, request);
                else { cam.targetTexture = rt; cam.Render(); }
            }
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Directory.CreateDirectory("../build");
            File.WriteAllBytes("../build/mockup_preview.png", tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("[StreetMock] 캡처 저장: build/mockup_preview.png");
        }

        // ---- helpers ----

        private static void CreateGroundBox(string name, Vector3 pos, Vector3 scale, Color color,
                                            string texPath = null, Vector2? tiling = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            Texture2D tex = texPath == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var mat = MakeUnlitMaterial(tex != null ? Color.white : color, tex, false);
            if (tiling.HasValue) mat.SetTextureScale("_BaseMap", tiling.Value);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static GameObject CreateTexQuad(string name, string texPath, bool unlit, bool alphaClip)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) Debug.LogWarning($"[StreetMock] 텍스처 미발견: {texPath}");
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeUnlitMaterial(Color.white, tex, alphaClip);
            return go;
        }

        private static Material MakeUnlitMaterial(Color color, Texture2D tex, bool alphaClip)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            if (alphaClip)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            return mat;
        }

        /// 02 §3-1 툰 셰이더 (2단 램프 + 웜톤 그림자 + 아웃라인)
        private static Material MakeToonMaterial(Color color)
        {
            var shader = Shader.Find("Nurungi/Toon");
            if (shader == null)
            {
                Debug.LogWarning("[StreetMock] Nurungi/Toon 셰이더 미발견 — URP Lit로 대체");
                var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fallback.SetColor("_BaseColor", color);
                fallback.SetFloat("_Smoothness", 0.05f);
                return fallback;
            }
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_ShadowColor", C(224, 186, 140)); // 웜톤 그림자
            mat.SetColor("_OutlineColor", C(122, 96, 66));
            mat.SetFloat("_OutlineWidth", 0.0021f);
            mat.SetFloat("_ShadowStep", 0.52f);
            return mat;
        }
    }
}
