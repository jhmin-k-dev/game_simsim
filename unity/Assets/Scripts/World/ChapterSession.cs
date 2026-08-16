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

            // Esc → 저장하고 타이틀로
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) ReturnToTitle();
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
