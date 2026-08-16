using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nurungi.World
{
    /// <summary>
    /// 01_기획서 §5-1: 세계 = 킷(월드) 여러 개, 킷 하나 = 챕터 여러 개.
    /// 타이틀의 월드/맵 선택이 이 목록을 읽는다.
    /// 데이터는 Resources/worlds.json — 챕터가 늘어나도 코드를 고치지 않는다 (03 §4 정신).
    /// </summary>
    [Serializable]
    public class WorldCatalog
    {
        public List<WorldEntry> worlds = new List<WorldEntry>();

        [Serializable]
        public class WorldEntry
        {
            public string id;
            public string name;
            public string description;
            public string timeOfDay = "day";
            public List<ChapterEntry> chapters = new List<ChapterEntry>();
        }

        [Serializable]
        public class ChapterEntry
        {
            public string id;
            public string name;
            public string scene;              // 로드할 씬 이름
            public bool unlockedByDefault;
            public string unlockedBy = "";    // 이 챕터를 클리어하면 해금
        }

        private const string ResourcePath = "worlds";
        private static WorldCatalog _cache;

        public static WorldCatalog Load()
        {
            if (_cache != null) return _cache;

            var text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null)
            {
                Debug.LogError($"[World] Resources/{ResourcePath}.json 없음 — 빈 목록으로 진행");
                _cache = new WorldCatalog();
                return _cache;
            }

            try
            {
                _cache = JsonUtility.FromJson<WorldCatalog>(text.text) ?? new WorldCatalog();
            }
            catch (Exception e)
            {
                Debug.LogError($"[World] worlds.json 파싱 실패: {e.Message}");
                _cache = new WorldCatalog();
            }
            return _cache;
        }

        public WorldEntry FindWorld(string id)
        {
            foreach (var w in worlds) if (w.id == id) return w;
            return null;
        }

        public ChapterEntry FindChapter(string chapterId)
        {
            foreach (var w in worlds)
                foreach (var c in w.chapters)
                    if (c.id == chapterId) return c;
            return null;
        }

        /// 잠금 해제 여부 (기본 해금이거나, 선행 챕터를 클리어했으면 열림)
        public static bool IsUnlocked(ChapterEntry chapter, Save.SaveData save)
        {
            if (chapter == null) return false;
            if (chapter.unlockedByDefault) return true;
            if (string.IsNullOrEmpty(chapter.unlockedBy)) return true;
            return save != null && save.IsCleared(chapter.unlockedBy);
        }
    }
}
