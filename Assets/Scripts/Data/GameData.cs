using System;
using UnityEngine;

namespace Game.Data
{
    // 아이템 타입 정의
    public enum ItemType
    {
        NONE = 0,
        JELLY,      // 점수 획득용
        POTION,     // 체력 회복용
        COIN,       // 재화
        ITEM        // 자석, 광속질주 등
    }

    // 특수 효과 정의
    public enum EffectType
    {
        NONE = 0,
        MAGNET,     // 자력
        BLAST,      // 광속질주
        GIANT       // 거대화
    }

    // 1. 젤리/아이템 데이터 구조체
    [Serializable]
    public struct JellyData
    {
        public int ID;
        public ItemType Type;
        public string Name;
        public int BaseScore;
        public float HealAmount;
        public EffectType Effect;
        public string PrefabPath;
    }

    // 2. 장애물 데이터 구조체
    [Serializable]
    public struct ObstacleData
    {
        public int ID;
        public string Name;
        public float Damage;
        public bool IsDestructible;
        public float SpeedDebuff;
        public string PrefabPath;
    }

    // 3. 레벨 밸런스 데이터 구조체
    [Serializable]
    public struct LevelData
    {
        public int Level;
        public int ExpRequired;
        public float LevelBonusRate;
        public int BasicJellyOverride;
    }

    // 4. 게임 상수 데이터 구조체
    [Serializable]
    public struct ConstantData
    {
        public string Key;
        public float Value;
        public string Description;
    }
}
