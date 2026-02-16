using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Game.Data;

/// <summary>
/// CSV 데이터를 로드하고 관리하는 싱글톤 매니저
/// Resources/Data/CharacterData.csv 파일을 읽어서 캐릭터 스탯 정보를 제공합니다.
/// Fail Fast 정책: 데이터 로드 오류 시 부분 성공을 허용하지 않고 전체 실패 처리합니다.
/// </summary>
public class DataManager : MonoBehaviour
{
    #region Singleton
    private static DataManager instance;
    private static bool hasSearchedInstance = false;
    
    public static DataManager Instance
    {
        get
        {
            if (instance == null && !hasSearchedInstance)
            {
                // FindObjectOfType는 최초 1회만 수행
                hasSearchedInstance = true;
                instance = FindObjectOfType<DataManager>();

                // v9 Bootstrap 준비: 씬에 없으면 에러 처리 (자동 생성 금지)
                if (instance == null)
                {
                    Debug.LogError("[DataManager] 씬에 DataManager가 존재하지 않습니다! 씬에 DataManager를 배치해야 합니다.");
                }
            }
            return instance;
        }
    }
    
    /// <summary>
    /// DataManager를 안전하게 가져오는 TryGet 패턴
    /// </summary>
    public static bool TryGet(out DataManager dm)
    {
        dm = Instance;
        return dm != null && dm.IsLoaded;
    }
    #endregion

    // 캐릭터 스탯 데이터를 레벨별로 저장 (Key: Level, Value: CharacterStat)
    private Dictionary<int, CharacterStat> characterStatDict = new Dictionary<int, CharacterStat>();
    
    // 젤리 데이터를 ID별로 저장 (Key: ID, Value: JellyData)
    private Dictionary<int, JellyData> jellyDataDict = new Dictionary<int, JellyData>();

    // v9 준비: 로드 상태 및 오류 컨텍스트
    public bool IsLoaded { get; private set; } = false;
    public string LastErrorContext { get; private set; } = string.Empty;

    void Awake()
    {
        // 싱글톤 인스턴스 설정
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // CSV 데이터 로드
            LoadCharacterData();
            LoadJellyData();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Resources/Data/CharacterData.csv 파일을 로드하여 파싱합니다.
    /// Fail Fast 정책: 파싱 오류 발생 시 전체 로드를 실패 처리합니다.
    /// </summary>
    private void LoadCharacterData()
    {
        const int ExpectedColumns = 4; // CharacterData CSV는 컬럼 수 고정
        bool hasError = false;
        LastErrorContext = string.Empty;

        // Resources 폴더에서 CSV 파일 로드
        TextAsset csvFile = Resources.Load<TextAsset>("Data/CharacterData");

        if (csvFile == null)
        {
            hasError = true;
            LastErrorContext = "[File: CharacterData.csv, Line: N/A] 파일을 찾을 수 없습니다.";
            Debug.LogError($"[DataManager] {LastErrorContext}");
            characterStatDict.Clear();
            IsLoaded = false;
            return;
        }

        // CSV 데이터 파싱 (CRLF 크로스플랫폼 호환)
        string[] lines = csvFile.text.Split('\n');

        // 헤더 검증
        if (lines.Length < 2)
        {
            hasError = true;
            LastErrorContext = "[File: CharacterData.csv, Line: 0] CSV 파일이 비어있거나 헤더만 존재합니다.";
            Debug.LogError($"[DataManager] {LastErrorContext}");
            characterStatDict.Clear();
            IsLoaded = false;
            return;
        }

        string headerLine = lines[0].Replace("\r", "").Trim();
        string[] headerColumns = headerLine.Split(',');
        Debug.Log($"[DataManager] CSV 헤더: {headerLine} (컬럼 수: {headerColumns.Length})");

        // Fail Fast: 헤더 컬럼 수 검증
        if (headerColumns.Length != ExpectedColumns)
        {
            hasError = true;
            LastErrorContext = $"[File: CharacterData.csv, Line: 1] 헤더 컬럼 수 불일치 (예상: {ExpectedColumns}, 실제: {headerColumns.Length})";
            Debug.LogError($"[DataManager] {LastErrorContext}");
            characterStatDict.Clear();
            IsLoaded = false;
            return;
        }

        // 데이터 라인 파싱 (헤더 제외)
        for (int i = 1; i < lines.Length; i++)
        {
            // CRLF 제거 후 Trim
            string line = lines[i].Replace("\r", "").Trim();

            // 빈 줄 건너뛰기
            if (string.IsNullOrEmpty(line))
                continue;

            // 콤마로 데이터 분리
            string[] values = line.Split(',');

            // Fail Fast: 컬럼 수 검증
            if (values.Length != ExpectedColumns)
            {
                hasError = true;
                LastErrorContext = $"[File: CharacterData.csv, Line: {i + 1}] 컬럼 수 불일치 (예상: {ExpectedColumns}, 실제: {values.Length}) (Raw: \"{line}\")";
                Debug.LogError($"[DataManager] {LastErrorContext}");
                break;
            }

            try
            {
                // Culture-safe float 파싱 (InvariantCulture 사용)
                int level = int.Parse(values[0].Trim());
                int hp = int.Parse(values[1].Trim());
                float magnetRange = float.Parse(values[2].Trim(), CultureInfo.InvariantCulture);
                int maxJumpCount = int.Parse(values[3].Trim());

                // Fail Fast: 논리 무결성 검증
                if (level <= 0 || hp <= 0 || maxJumpCount < 1)
                {
                    hasError = true;
                    LastErrorContext = $"[File: CharacterData.csv, Line: {i + 1}] 논리 무결성 위반 (Level: {level}, Hp: {hp}, MaxJumpCount: {maxJumpCount}) (Raw: \"{line}\")";
                    Debug.LogError($"[DataManager] {LastErrorContext}");
                    break;
                }

                CharacterStat stat = new CharacterStat(level, hp, magnetRange, maxJumpCount);

                // 중복 Level 감지
                if (characterStatDict.ContainsKey(stat.Level))
                {
                    hasError = true;
                    LastErrorContext = $"[File: CharacterData.csv, Line: {i + 1}] 중복 Level 발견: {stat.Level} (Raw: \"{line}\")";
                    Debug.LogError($"[DataManager] {LastErrorContext}");
                    break;
                }

                // Dictionary에 추가
                characterStatDict[stat.Level] = stat;
            }
            catch (System.Exception e)
            {
                hasError = true;
                LastErrorContext = $"[File: CharacterData.csv, Line: {i + 1}] 데이터 변환 실패: {e.Message} (Raw: \"{line}\")";
                Debug.LogError($"[DataManager] {LastErrorContext}");
                break;
            }
        }

        // Fail Fast: 오류 발생 시 전체 실패 처리
        if (hasError)
        {
            characterStatDict.Clear();
            IsLoaded = false;
            Debug.LogError("[DataManager] CharacterData 로드 실패. 게임을 시작할 수 없습니다.");
        }
        // Fail Fast: 유효 데이터 0건도 실패로 처리
        else if (characterStatDict.Count == 0)
        {
            hasError = true;
            LastErrorContext = "[File: CharacterData.csv] 유효한 데이터가 0건입니다.";
            Debug.LogError($"[DataManager] {LastErrorContext}");
            IsLoaded = false;
            Debug.LogError("[DataManager] CharacterData 로드 실패. 게임을 시작할 수 없습니다.");
        }
        else
        {
            IsLoaded = true;
            Debug.Log($"[DataManager] {characterStatDict.Count}개의 캐릭터 스탯 데이터 로드 성공.");
        }
    }

    /// <summary>
    /// 특정 레벨의 캐릭터 스탯을 반환합니다.
    /// </summary>
    /// <param name="level">조회할 레벨</param>
    /// <returns>해당 레벨의 CharacterStat. 없으면 기본값을 반환합니다.</returns>
    public CharacterStat GetStat(int level)
    {
        if (characterStatDict.ContainsKey(level))
        {
            return characterStatDict[level];
        }
        else
        {
            Debug.LogWarning($"[DataManager] Level {level}에 해당하는 스탯 데이터가 없습니다.");
            return default(CharacterStat);
        }
    }

    /// <summary>
    /// 특정 레벨의 스탯이 존재하는지 확인합니다.
    /// </summary>
    /// <param name="level">확인할 레벨</param>
    /// <returns>스탯 존재 여부</returns>
    public bool HasStat(int level)
    {
        return characterStatDict.ContainsKey(level);
    }

    /// <summary>
    /// 로드된 전체 레벨 수를 반환합니다.
    /// </summary>
    /// <returns>전체 레벨 수</returns>
    public int GetTotalLevelCount()
    {
        return characterStatDict.Count;
    }

    /// <summary>
    /// Resources/Data/JellyData.csv 파일을 로드하여 파싱합니다.
    /// Fail Fast 정책: 파싱 오류 발생 시 전체 로드를 실패 처리합니다.
    /// </summary>
    private void LoadJellyData()
    {
        const int ExpectedColumns = 7; // JellyData CSV는 컬럼 수 고정
        string localErrorContext = string.Empty;

        // Resources 폴더에서 CSV 파일 로드
        TextAsset csvFile = Resources.Load<TextAsset>("Data/JellyData");

        if (csvFile == null)
        {
            localErrorContext = "[File: JellyData.csv, Line: N/A] 파일을 찾을 수 없습니다.";
            Debug.LogError($"[DataManager] {localErrorContext}");
            jellyDataDict.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new System.Exception($"[DataManager] {localErrorContext}");
#else
            return;
#endif
        }

        // CSV 데이터 파싱 (CRLF 크로스플랫폼 호환)
        string[] lines = csvFile.text.Split('\n');

        // 헤더 검증
        if (lines.Length < 2)
        {
            localErrorContext = "[File: JellyData.csv, Line: 0] CSV 파일이 비어있거나 헤더만 존재합니다.";
            Debug.LogError($"[DataManager] {localErrorContext}");
            jellyDataDict.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new System.Exception($"[DataManager] {localErrorContext}");
#else
            return;
#endif
        }

        string headerLine = lines[0].Replace("\r", "").Trim();
        string[] headerColumns = headerLine.Split(',');
        Debug.Log($"[DataManager] JellyData CSV 헤더: {headerLine} (컬럼 수: {headerColumns.Length})");

        // Fail Fast: 헤더 컬럼 수 검증
        if (headerColumns.Length != ExpectedColumns)
        {
            localErrorContext = $"[File: JellyData.csv, Line: 1] 헤더 컬럼 수 불일치 (예상: {ExpectedColumns}, 실제: {headerColumns.Length})";
            Debug.LogError($"[DataManager] {localErrorContext}");
            jellyDataDict.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new System.Exception($"[DataManager] {localErrorContext}");
#else
            return;
#endif
        }

        // 데이터 라인 파싱 (헤더 제외)
        for (int i = 1; i < lines.Length; i++)
        {
            // CRLF 제거 후 Trim
            string line = lines[i].Replace("\r", "").Trim();

            // 빈 줄 건너뛰기
            if (string.IsNullOrEmpty(line))
                continue;

            // 콤마로 데이터 분리
            string[] values = line.Split(',');

            // Fail Fast: 컬럼 수 검증
            if (values.Length != ExpectedColumns)
            {
                localErrorContext = $"[File: JellyData.csv, Line: {i + 1}] 컬럼 수 불일치 (예상: {ExpectedColumns}, 실제: {values.Length}) (Raw: \"{line}\")";
                Debug.LogError($"[DataManager] {localErrorContext}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new System.Exception($"[DataManager] {localErrorContext}");
#else
                jellyDataDict.Clear();
                return;
#endif
            }

            try
            {
                // Culture-safe float 파싱 (InvariantCulture 사용)
                int id = int.Parse(values[0].Trim());
                ItemType type = (ItemType)System.Enum.Parse(typeof(ItemType), values[1].Trim(), true);
                string name = values[2].Trim();
                int baseScore = int.Parse(values[3].Trim());
                float healAmount = float.Parse(values[4].Trim(), CultureInfo.InvariantCulture);
                EffectType effect = (EffectType)System.Enum.Parse(typeof(EffectType), values[5].Trim(), true);
                string prefabPath = values[6].Trim();

                JellyData data = new JellyData
                {
                    ID = id,
                    Type = type,
                    Name = name,
                    BaseScore = baseScore,
                    HealAmount = healAmount,
                    Effect = effect,
                    PrefabPath = prefabPath
                };

                // 중복 ID 감지
                if (jellyDataDict.ContainsKey(data.ID))
                {
                    localErrorContext = $"[File: JellyData.csv, Line: {i + 1}] 중복 ID 발견: {data.ID} (Raw: \"{line}\")";
                    Debug.LogError($"[DataManager] {localErrorContext}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    throw new System.Exception($"[DataManager] {localErrorContext}");
#else
                    jellyDataDict.Clear();
                    return;
#endif
                }

                // Dictionary에 추가
                jellyDataDict[data.ID] = data;
            }
            catch (System.Exception e)
            {
                localErrorContext = $"[File: JellyData.csv, Line: {i + 1}] 데이터 변환 실패: {e.Message} (Raw: \"{line}\")";
                Debug.LogError($"[DataManager] {localErrorContext}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new System.Exception($"[DataManager] {localErrorContext}");
#else
                jellyDataDict.Clear();
                return;
#endif
            }
        }

        // Fail Fast: 유효 데이터 0건도 실패로 처리
        if (jellyDataDict.Count == 0)
        {
            localErrorContext = "[File: JellyData.csv] 유효한 데이터가 0건입니다.";
            Debug.LogError($"[DataManager] {localErrorContext}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new System.Exception($"[DataManager] {localErrorContext}");
#else
            return;
#endif
        }
        else
        {
            Debug.Log($"[DataManager] {jellyDataDict.Count}개의 젤리 데이터 로드 성공.");
        }
    }

    /// <summary>
    /// 특정 ID의 젤리 기본 점수를 반환합니다.
    /// 
    /// [중요] JellyId는 JellyData.csv의 ID(예: BasicJelly=1001)를 입력해야 함. 1 같은 값 사용 금지.
    /// 프리팹/씬의 JellyItem 컴포넌트에서 올바른 ID를 설정하세요.
    /// </summary>
    /// <param name="id">조회할 젤리 ID</param>
    /// <returns>해당 젤리의 BaseScore. 없으면 0을 반환합니다.</returns>
    public int GetJellyBaseScore(int id)
    {
        if (jellyDataDict.TryGetValue(id, out JellyData data))
        {
            return data.BaseScore;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 디버깅을 위해 현재 로드된 Jelly ID 목록 일부 출력
        var availableIds = new System.Collections.Generic.List<int>(jellyDataDict.Keys);
        availableIds.Sort();
        int displayCount = System.Math.Min(10, availableIds.Count);
        string idList = string.Join(", ", availableIds.GetRange(0, displayCount));
        if (availableIds.Count > displayCount)
            idList += "...";
        
        Debug.LogError($"[DataManager] Jelly ID not found: {id}\nAvailable Jelly IDs: {idList}");
        throw new KeyNotFoundException($"[DataManager] Jelly ID not found: {id}");
#else
        Debug.LogError($"[DataManager] Jelly ID not found: {id}");
        return 0;
#endif
    }
}
