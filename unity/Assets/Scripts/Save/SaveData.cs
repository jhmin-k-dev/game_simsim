using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nurungi.Save
{
    /// <summary>
    /// 01_기획서 §8: 세이브는 JSON 1파일 (챕터 진행도·컬렉션·설정).
    /// 사진은 여기 넣지 않고 별도 폴더 (06 §4-2).
    /// 자동 저장만 있고 수동 슬롯은 없다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string lastPlayedChapterId = "";
        public float totalPlaySeconds;
        public string lastSavedIso = "";

        public List<ChapterRecord> chapters = new List<ChapterRecord>();
        public List<string> collectedIds = new List<string>();
        public Settings settings = new Settings();

        [Serializable]
        public class ChapterRecord
        {
            public string id;
            public bool cleared;
            public int visitCount;
            public int photoCount;
        }

        [Serializable]
        public class Settings
        {
            public float bgmVolume = 0.8f;
            public float sfxVolume = 1.0f;
            public bool showGrid;          // 촬영 모드 격자 (06 §3-4)
        }

        // ---- 조회·갱신 헬퍼 ----

        public ChapterRecord GetChapter(string id, bool createIfMissing = false)
        {
            for (int i = 0; i < chapters.Count; i++)
                if (chapters[i].id == id) return chapters[i];

            if (!createIfMissing) return null;
            var rec = new ChapterRecord { id = id };
            chapters.Add(rec);
            return rec;
        }

        public bool IsCleared(string id)
        {
            var rec = GetChapter(id);
            return rec != null && rec.cleared;
        }

        public bool HasCollected(string id) => collectedIds.Contains(id);

        public void Collect(string id)
        {
            if (!collectedIds.Contains(id)) collectedIds.Add(id);
        }
    }
}
