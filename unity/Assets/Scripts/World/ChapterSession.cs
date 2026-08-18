using Nurungi.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nurungi.World
{
    /// <summary>
    /// 챕터 씬에 하나 올려두는 진행 관리자.
    /// 01_기획서 §8: 클리어 = 끝점 도달, 실패 없음. 자동 저장(챕터 종료 시).
    /// </summary>
    public class ChapterSession : MonoBehaviour
    {
        [SerializeField] private string chapterId = "street_01";
        [SerializeField] private Transform player;
        [SerializeField] private float goalX = 92f;      // 03 §4-1 goalX
        [SerializeField] private string titleScene = "Title";

        [Header("진입 연출 (04 §2-4 5번 — 영화 룩)")]
        [SerializeField, TextArea(4, 10)] private string introScript =
            "cam distance 17 in 0s\n" +
            "cam fov 34 in 0s\n" +
            "dog move (7.5, 0.3) in 3.2s\n" +
            "& cam distance 11 in 3.2s\n" +
            "& cam fov 28 in 3.2s\n" +
            "cam follow";

        private float _elapsed;
        private bool _cleared;

        /// 타이틀에서 챕터를 고르면 여기에 담아두고 씬을 넘어간다.
        public static string PendingChapterId;

        private void Start()
        {
            if (!string.IsNullOrEmpty(PendingChapterId)) chapterId = PendingChapterId;
            if (player == null)
            {
                var mover = FindFirstObjectByType<Player.PlayerMover>();
                if (mover != null) player = mover.transform;
            }
            SaveSystem.MarkChapterEntered(chapterId);

            // 진입 연출 컷 — 실패해도 게임은 계속
            if (!string.IsNullOrEmpty(introScript))
            {
                var errors = Scripting.ScriptRunner.GetOrCreate().Run(introScript);
                foreach (var e in errors) Debug.LogWarning($"[Chapter] 인트로 스크립트 오류: {e}");
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            // 끝점 도달 → 클리어 (한 번만)
            if (!_cleared && player != null && player.position.x >= goalX)
            {
                _cleared = true;
                SaveSystem.MarkChapterCleared(chapterId);
                Debug.Log($"[Chapter] {chapterId} 클리어");
            }
            // Esc 처리는 PauseMenu가 담당 (2026-08-18)
        }

        /// 외부(일시정지 메뉴 등)에서 저장만 하고 싶을 때
        public void SaveNow()
        {
            SaveSystem.AddPlayTime(_elapsed);
            _elapsed = 0f;
            SaveSystem.Save();
        }

        public void ReturnToTitle()
        {
            SaveSystem.AddPlayTime(_elapsed);
            SaveSystem.Save();
            _elapsed = 0f;
            if (Application.CanStreamedLevelBeLoaded(titleScene)) SceneManager.LoadScene(titleScene);
            else Debug.LogWarning($"[Chapter] '{titleScene}' 씬이 빌드 설정에 없음");
        }

        private void OnApplicationQuit()
        {
            SaveSystem.AddPlayTime(_elapsed);
            SaveSystem.Save();
        }
    }
}
