using UnityEngine;

namespace CookieRun.Data
{
    /// <summary>
    /// 패턴 메타데이터 구조체
    /// CSV에서 로드된 패턴 정보를 담는 불변 데이터
    /// </summary>
    public readonly struct PatternMeta
    {
        public readonly int ID;
        public readonly float TotalDistance;
        public readonly int Difficulty;

        public PatternMeta(int id, float totalDistance, int difficulty)
        {
            ID = id;
            TotalDistance = totalDistance;
            Difficulty = difficulty;
        }
    }

    /// <summary>
    /// 패턴 이벤트 데이터 구조체
    /// 특정 거리에서 스폰될 오브젝트 정보를 담는 불변 데이터
    /// </summary>
    public readonly struct PatternEventRow
    {
        public readonly int PatternID;
        public readonly float SpawnDistance;
        public readonly int SpawnID;
        public readonly int LaneIndex;

        public PatternEventRow(int patternID, float spawnDistance, int spawnID, int laneIndex)
        {
            PatternID = patternID;
            SpawnDistance = spawnDistance;
            SpawnID = spawnID;
            LaneIndex = laneIndex;
        }
    }
}
