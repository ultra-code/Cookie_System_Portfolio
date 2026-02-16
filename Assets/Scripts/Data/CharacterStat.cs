using System;

namespace Game.Data
{
    /// <summary>
    /// 캐릭터 레벨별 스탯 데이터 구조체
    /// CSV 파일에서 로드되어 사용됩니다.
    /// </summary>
    [Serializable]
    public struct CharacterStat
    {
        public int Level;
        public int Hp;
        public float MagnetRange;    // 자력 범위 (0 = 없음)
        public int MaxJumpCount;     // 최대 점프 횟수

        public CharacterStat(int level, int hp, float magnetRange, int maxJumpCount)
        {
            Level = level;
            Hp = hp;
            MagnetRange = magnetRange;
            MaxJumpCount = maxJumpCount;
        }
    }
}
