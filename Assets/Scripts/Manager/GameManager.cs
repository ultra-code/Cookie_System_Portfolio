using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임의 점수와 상태를 관리하는 싱글톤 매니저
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton (Scene-based)
    private static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        // Fail Fast: 씬 전용 싱글톤 중복 감지
        if (instance != null && instance != this)
        {
            Debug.LogError("[GameManager] 씬에 중복된 GameManager가 존재합니다! 이 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        
        // 씬 전용 싱글톤: 현재 씬의 매니저가 주인이 됨
        instance = this;
        
        // 씬 로드 직후 즉시 시간 흐름 보장 (재시작 버그 방지)
        Time.timeScale = 1f;
        
        Debug.Log("[GameManager] 씬 전용 싱글톤 초기화 완료");
    }
    #endregion

    #region 상태 변수
    [Header("게임 상태")]
    [SerializeField] private bool _isGameActive = false;
    public bool isGameActive 
    { 
        get => _isGameActive; 
        private set => _isGameActive = value; 
    }

    [Header("점수 관리")]
    [SerializeField] private int _currentScore = 0;
    public int currentScore 
    { 
        get => _currentScore; 
        private set => _currentScore = value; 
    }

    [SerializeField] private float _currentDistance = 0f;
    public float currentDistance 
    { 
        get => _currentDistance; 
        private set => _currentDistance = value; 
    }

    // [정책] 거리 SSOT(단일 진실 원천)는 MapManager.scrollSpeed입니다.
    // Time 기반 거리 증가는 혼동을 유발하므로 사용하지 않습니다.
    // 거리는 MapManager.Update()에서 UpdateDistanceBySpeed(scrollSpeed)로만 업데이트됩니다.
    #endregion

    #region 초기화
    private void Start()
    {
        // 코루틴으로 게임 시작 (UI 매니저가 준비될 때까지 대기)
        StartCoroutine(InitializeGame());
    }

    /// <summary>
    /// 게임 초기화 (UIManager가 준비될 때까지 대기)
    /// </summary>
    private IEnumerator InitializeGame()
    {
        // UIManager가 필수 의존성이므로 최대 3초 대기
        float timeout = 3f;
        float elapsed = 0f;
        
        while (UIManager.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Fail Fast: 타임아웃 시 게임 시작 불가
        if (UIManager.Instance == null)
        {
            Debug.LogError("[GameManager] UIManager 초기화 타임아웃! 게임을 시작할 수 없습니다.");
            yield break;
        }

        // 게임 시작
        StartGame();
    }

    /// <summary>
    /// 게임 시작
    /// </summary>
    public void StartGame()
    {
        isGameActive = true;
        currentScore = 0;
        currentDistance = 0f;
        Time.timeScale = 1f;
        Debug.Log("게임 시작!");
        
        // UI 초기화
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideGameOver();
            UIManager.Instance.UpdateScore(currentScore);
            UIManager.Instance.UpdateDistance(currentDistance);
            Debug.Log("[GameManager] UI 초기화 완료");
        }
        else
        {
            Debug.LogError("[GameManager] UIManager를 찾을 수 없습니다!");
        }
    }
    #endregion

    #region 업데이트
    private void Update()
    {
        if (isGameActive)
        {
            // [정책] 거리 누적은 GameManager에서 하지 않습니다.
            // 거리 SSOT는 MapManager.scrollSpeed이며, MapManager.Update()에서 UpdateDistanceBySpeed()를 호출합니다.
            
            // UI 업데이트 (거리는 MapManager에서 UpdateDistanceBySpeed(scrollSpeed)로 업데이트됨)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateDistance(currentDistance);
            }
        }
    }
    #endregion

    #region 점수 관리
    /// <summary>
    /// 점수를 추가합니다 (젤리 획득 등)
    /// </summary>
    /// <param name="amount">추가할 점수</param>
    public void AddScore(int amount)
    {
        if (!isGameActive) return;

        currentScore += amount;
        Debug.Log($"점수 추가: +{amount} (총 점수: {currentScore})");
        
        // UI 업데이트
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(currentScore);
        }
    }

    /// <summary>
    /// 거리를 업데이트합니다 (내부 전용)
    /// </summary>
    /// <param name="distance">추가할 거리</param>
    private void UpdateDistance(float distance)
    {
        if (!isGameActive) return;

        currentDistance += distance;
        
        // TODO: UI 업데이트 이벤트 호출
        // OnDistanceChanged?.Invoke(currentDistance);
    }

    /// <summary>
    /// [거리 SSOT 정책] MapManager.scrollSpeed 기반으로 거리를 증가시킵니다.
    /// MapManager.Update()에서 호출하는 전용 API입니다.
    /// </summary>
    /// <param name="scrollSpeed">맵 스크롤 속도 (월드 이동 속도)</param>
    public void AddDistanceFromWorldScroll(float scrollSpeed)
    {
        if (!isGameActive) return;

        float distanceThisFrame = scrollSpeed * Time.deltaTime;
        UpdateDistance(distanceThisFrame);
    }

    /// <summary>
    /// [구버전 호환용 - 사용 금지] 플레이어 속도 기반 거리 증가 (혼동 유발)
    /// 거리 SSOT는 MapManager.scrollSpeed이므로 이 메서드는 사용하지 마세요.
    /// </summary>
    [System.Obsolete("거리 SSOT는 MapManager.scrollSpeed입니다. AddDistanceFromWorldScroll을 사용하세요.")]
    public void UpdateDistanceBySpeed(float speed)
    {
        if (!isGameActive) return;

        float distanceThisFrame = speed * Time.deltaTime;
        UpdateDistance(distanceThisFrame);
    }
    #endregion

    #region 게임 오버
    /// <summary>
    /// 게임 오버 처리
    /// </summary>
    public void GameOver()
    {
        if (!isGameActive)
        {
            Debug.LogWarning("[GameManager] 게임이 이미 비활성 상태입니다. GameOver 중복 호출 무시.");
            return;
        }

        isGameActive = false;
        Time.timeScale = 0f;
        
        Debug.Log($"[GameManager] 게임 오버! 최종 점수: {currentScore}, 최종 거리: {currentDistance:F2}m");
        
        // 게임 오버 UI 표시
        if (UIManager.Instance != null)
        {
            Debug.Log("[GameManager] UIManager에 게임 오버 UI 표시 요청");
            UIManager.Instance.ShowGameOver(currentScore, currentDistance);
        }
        else
        {
            Debug.LogError("[GameManager] UIManager를 찾을 수 없습니다! 게임 오버 UI를 표시할 수 없습니다.");
        }
        
        // TODO: 최고 점수 저장 (DataManager 연동)
    }

    /// <summary>
    /// 게임 재시작
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GameManager] 게임 재시작 시작...");
        
        // [중요] 씬 로드 전에 먼저 시간을 복원 (게임 오버 시 Time.timeScale = 0으로 설정되어 있음)
        Time.timeScale = 1f;
        Debug.Log("[GameManager] Time.timeScale을 1로 복원했습니다.");
        
        // [정책] 씬 리로드 전에는 상태를 건드리지 않음 (새 씬의 StartGame에서 재설정)
        // 데이터 초기화는 하지 않음 (씬 로드 시 새 GameManager가 생성됨)
        
        // 현재 활성화된 씬을 다시 로드 (모든 오브젝트와 매니저를 완벽히 초기화)
        // 주의: 씬 로드 후 새로운 GameManager가 생성되고 Awake() → Start() → StartGame() 순서로 실행됨
        string sceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[GameManager] 씬 '{sceneName}' 재로드 중...");
        SceneManager.LoadScene(sceneName);
    }
    #endregion

    #region 이벤트 (선택사항)
    // 필요시 UI 업데이트를 위한 이벤트 추가
    // public event System.Action<int> OnScoreChanged;
    // public event System.Action<float> OnDistanceChanged;
    // public event System.Action<int, float> OnGameOver;
    #endregion
}
