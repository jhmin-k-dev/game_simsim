using System;
using System.IO;
using UnityEngine;

namespace Nurungi.Save
{
    /// <summary>
    /// 01_기획서 §8: 자동 저장(챕터 종료 시), 수동 슬롯 없음.
    /// 쓰기는 임시 파일 → 교체 방식이라 저장 중 종료돼도 기존 세이브가 깨지지 않는다.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "nurungi_save.json";
        private static SaveData _cache;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// 현재 세이브. 없으면 새로 만든다.
        public static SaveData Current
        {
            get
            {
                if (_cache == null) _cache = Load();
                return _cache;
            }
        }

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    var data = JsonUtility.FromJson<SaveData>(json);
                    if (data != null)
                    {
                        _cache = data;
                        return data;
                    }
                    Debug.LogWarning("[Save] 세이브 파싱 실패 — 새 세이브로 시작");
                }
            }
            catch (Exception e)
            {
                // 세이브가 깨져도 게임은 시작되어야 한다
                Debug.LogError($"[Save] 로드 실패: {e.Message} — 새 세이브로 시작");
            }
            _cache = new SaveData();
            return _cache;
        }

        public static void Save()
        {
            var data = Current;
            data.lastSavedIso = DateTime.Now.ToString("s");
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패: {e.Message}");
            }
        }

        // ---- 게임 이벤트 ----

        public static void MarkChapterEntered(string chapterId)
        {
            var rec = Current.GetChapter(chapterId, createIfMissing: true);
            rec.visitCount++;
            Current.lastPlayedChapterId = chapterId;
            Save();
        }

        /// 챕터 끝점 도달 (01 §8: 클리어 = 끝점 도달, 실패 없음)
        public static void MarkChapterCleared(string chapterId)
        {
            var rec = Current.GetChapter(chapterId, createIfMissing: true);
            rec.cleared = true;
            Save();
        }

        public static void AddPlayTime(float seconds)
        {
            Current.totalPlaySeconds += seconds;
        }

        /// 테스트·디버그용. 되돌릴 수 없으므로 호출부에서 확인을 받을 것.
        public static void DeleteSave()
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); }
            catch (Exception e) { Debug.LogError($"[Save] 삭제 실패: {e.Message}"); }
            _cache = new SaveData();
        }
    }
}
