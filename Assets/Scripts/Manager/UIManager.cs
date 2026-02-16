using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 게임 UI를 관리하는 싱글톤 매니저
/// </summary>
public class UIManager : MonoBehaviour
{
    #region Singleton (Scene-based)
    private static UIManager instance;
    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<UIManager>();
            }
            return instance;
        }
    }

    // 성능 방어: 씬당 1회만 전체 스캔 수행 (buildIndex 기반으로 씬 구분)
    private int scannedSceneBuildIndex = -1;

    private void Awake()
    {
        // Fail Fast: 씬 전용 싱글톤 중복 감지
        if (instance != null && instance != this)
        {
            Debug.LogError("[UIManager] 씬에 중복된 UIManager가 존재합니다! 이 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        
        // 씬 전용 싱글톤: 현재 씬의 매니저가 주인이 됨
        instance = this;
        
        // SerializeField 연결을 우선 사용, 누락된 경우에만 전체 스캔 (씬당 1회)
        int currentSceneBuildIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        
        if (IsAnyEssentialUIElementMissing())
        {
            if (scannedSceneBuildIndex != currentSceneBuildIndex)
            {
                Debug.LogWarning($"[UIManager] 필수 UI 요소가 누락되어 씬 #{currentSceneBuildIndex}의 전체 스캔을 수행합니다. (씬당 1회)");
                FindAndAssignUIElements();
                scannedSceneBuildIndex = currentSceneBuildIndex;
            }
            else
            {
                Debug.LogError("[UIManager] 필수 UI 요소가 여전히 누락되었지만, 이미 스캔을 완료했습니다. 인스펙터 연결을 확인하세요.");
            }
        }
        else
        {
            Debug.Log("[UIManager] 모든 필수 UI 요소가 인스펙터에서 연결되어 있습니다.");
        }
        
        Debug.Log("[UIManager] 씬 전용 싱글톤 초기화 완료");
    }

    // 이벤트 구독 (매니저가 활성화될 때마다 실행)
    private void OnEnable() 
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log("[UIManager] SceneManager.sceneLoaded 이벤트 구독");
    }

    // 이벤트 해지 (메모리 누수 방지)
    private void OnDisable() 
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Debug.Log("[UIManager] SceneManager.sceneLoaded 이벤트 해지");
    }

    /// <summary>
    /// 씬 로딩이 완료될 때마다 호출됨 (재시작 시 실행됨)
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[UIManager] 씬 로드 감지: {scene.name}. UI를 초기화합니다.");

        // 필수 요소가 null일 때만 전체 스캔 (씬당 1회 보장)
        int currentSceneBuildIndex = scene.buildIndex;
        
        if (IsAnyEssentialUIElementMissing())
        {
            if (scannedSceneBuildIndex != currentSceneBuildIndex)
            {
                Debug.LogWarning($"[UIManager] 필수 UI 요소가 누락되어 씬 #{currentSceneBuildIndex}의 전체 스캔을 수행합니다. (씬당 1회)");
                FindAndAssignUIElements();
                scannedSceneBuildIndex = currentSceneBuildIndex;
            }
            else
            {
                Debug.LogError("[UIManager] 필수 UI 요소가 여전히 누락되었지만, 이미 스캔을 완료했습니다. 인스펙터 연결을 확인하세요.");
            }
        }

        // [중요] 찾았으면 바로 숨기기 (에디터에서 켜놨던 패널을 끄는 역할)
        HideGameOver();
        
        // 점수/거리 UI 0으로 리셋
        UpdateScore(0);
        UpdateDistance(0);
    }

    /// <summary>
    /// [수정 5] 필수 UI 요소가 하나라도 null인지 확인
    /// </summary>
    private bool IsAnyEssentialUIElementMissing()
    {
        return scoreText == null || 
               distanceText == null || 
               hpBar == null || 
               gameOverPanel == null || 
               restartButton == null;
    }
    
    private void FindAndAssignUIElements()
    {
        Debug.Log("[UIManager] 씬 루트 전수 조사 시작 (최후의 수단)");

        // 1. 현재 씬의 모든 최상위(Root) 오브젝트 가져오기 (꺼진 것도 포함됨)
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        
        // 2. 모든 루트 오브젝트를 순회하며 자식들까지 샅샅이 검색
        foreach (GameObject root in rootObjects)
        {
            // 각 루트 오브젝트 산하의 모든 Transform 가져오기 (true = 비활성화 포함)
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allChildren)
            {
                string objName = t.name.Trim();

                // UI 요소 연결
                if (objName == "ScoreText" && scoreText == null)
                {
                    scoreText = t.GetComponent<TextMeshProUGUI>();
                    Debug.Log($"[UIManager] ScoreText 발견! (부모: {t.parent?.name})");
                }
                else if (objName == "DistanceText" && distanceText == null)
                {
                    distanceText = t.GetComponent<TextMeshProUGUI>();
                    Debug.Log($"[UIManager] DistanceText 발견! (부모: {t.parent?.name})");
                }
                else if (objName == "HpBar" && hpBar == null)
                {
                    hpBar = t.GetComponent<Slider>();
                    Debug.Log($"[UIManager] HpBar 발견! (부모: {t.parent?.name})");
                }
                else if (objName == "HpText" && hpText == null)
                {
                    hpText = t.GetComponent<TextMeshProUGUI>();
                }
                else if (objName == "GameOverPanel" && gameOverPanel == null)
                {
                    gameOverPanel = t.gameObject;
                    Debug.Log($"[UIManager] GameOverPanel 발견! (부모: {t.parent?.name})");
                }
            }
        }

        // 3. 게임 오버 패널 자식 찾기
        if (gameOverPanel != null)
        {
            Transform[] panelChildren = gameOverPanel.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in panelChildren)
            {
                string objName = t.name.Trim();

                if ((objName == "FinalScoreText" || objName == "ResultText") && finalScoreText == null)
                    finalScoreText = t.GetComponent<TextMeshProUGUI>();
                else if (objName == "FinalDistanceText" && finalDistanceText == null)
                    finalDistanceText = t.GetComponent<TextMeshProUGUI>();
                else if (objName == "RestartButton" && restartButton == null)
                    restartButton = t.GetComponent<Button>();
                else if (objName == "QuitButton" && quitButton == null)
                    quitButton = t.GetComponent<Button>();
            }
        }
        else
        {
            Debug.LogError("[UIManager]  씬 전체를 뒤졌지만 GameOverPanel을 찾을 수 없습니다. 이름 철자를 확인하거나 오브젝트가 씬에 있는지 확인하세요.");
        }
    }
    #endregion

    #region UI 요소
    [Header("점수 및 거리 UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("체력 UI")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private TextMeshProUGUI hpText; // 선택사항: "HP: 3/3" 같은 텍스트

    [Header("게임 오버 UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI finalDistanceText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    #endregion

    #region 초기화
    private void Start()
    {
        // [중요] UI 요소 유효성 검증 (재시작 버그 디버깅용)
        ValidateUIElements();

        // 게임 오버 패널 초기 비활성화
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // 버튼 이벤트 연결 (중복 방어)
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(OnRestartButtonClicked);
            restartButton.onClick.AddListener(OnRestartButtonClicked);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitButtonClicked);
            quitButton.onClick.AddListener(OnQuitButtonClicked);
        }

        // 체력바 슬라이더 범위 설정 (0~1 비율로 작동)
        if (hpBar != null)
        {
            hpBar.minValue = 0f;
            hpBar.maxValue = 1f;
            hpBar.value = 1f; // 초기값 100%로 설정
        }

        // 초기 UI 설정
        UpdateScore(0);
        UpdateDistance(0f);
        
        Debug.Log("UIManager 초기화 완료");
    }

    /// <summary>
    /// UI 요소들이 제대로 할당되었는지 검증 (재시작 버그 방지)
    /// </summary>
    private void ValidateUIElements()
    {
        bool allValid = true;

        if (scoreText == null)
        {
            Debug.LogError("[UIManager] scoreText가 할당되지 않았습니다! 인스펙터에서 연결해주세요.");
            allValid = false;
        }
        if (distanceText == null)
        {
            Debug.LogError("[UIManager] distanceText가 할당되지 않았습니다! 인스펙터에서 연결해주세요.");
            allValid = false;
        }
        if (hpBar == null)
        {
            Debug.LogError("[UIManager] hpBar가 할당되지 않았습니다! 인스펙터에서 연결해주세요.");
            allValid = false;
        }
        if (gameOverPanel == null)
        {
            Debug.LogError("[UIManager] gameOverPanel이 할당되지 않았습니다! 인스펙터에서 연결해주세요.");
            allValid = false;
        }
        if (finalScoreText == null)
        {
            Debug.LogWarning("[UIManager] finalScoreText가 할당되지 않았습니다.");
        }
        if (finalDistanceText == null)
        {
            Debug.LogWarning("[UIManager] finalDistanceText가 할당되지 않았습니다.");
        }
        if (restartButton == null)
        {
            Debug.LogError("[UIManager] restartButton이 할당되지 않았습니다! 인스펙터에서 연결해주세요.");
            allValid = false;
        }

        if (allValid)
        {
            Debug.Log("[UIManager] 모든 필수 UI 요소가 정상적으로 연결되었습니다.");
        }
    }
    #endregion

    #region 점수 및 거리 UI
    // DEVELOPMENT_BUILD / UNITY_EDITOR에서 최초 1회 경고를 위한 가드 플래그
    #if DEVELOPMENT_BUILD || UNITY_EDITOR
    private bool hasWarnedScoreTextMissing = false;
    private bool hasWarnedDistanceTextMissing = false;
    #endif

    /// <summary>
    /// 점수 텍스트를 업데이트합니다
    /// </summary>
    /// <param name="score">현재 점수</param>
    public void UpdateScore(int score)
    {
        // 방어 코드: scoreText가 null이면 조용히 리턴
        if (scoreText == null)
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!hasWarnedScoreTextMissing)
            {
                Debug.LogWarning("[UIManager] scoreText가 null입니다. 점수 UI를 업데이트할 수 없습니다.");
                hasWarnedScoreTextMissing = true;
            }
            #endif
            return;
        }

        scoreText.text = $"Score: {score:N0}";
    }

    /// <summary>
    /// 거리 텍스트를 업데이트합니다
    /// </summary>
    /// <param name="dist">현재 거리</param>
    public void UpdateDistance(float dist)
    {
        // 방어 코드: distanceText가 null이면 조용히 리턴
        if (distanceText == null)
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!hasWarnedDistanceTextMissing)
            {
                Debug.LogWarning("[UIManager] distanceText가 null입니다. 거리 UI를 업데이트할 수 없습니다.");
                hasWarnedDistanceTextMissing = true;
            }
            #endif
            return;
        }

        distanceText.text = $"{dist:F0}m";
    }
    #endregion

    #region 체력 UI
    /// <summary>
    /// 체력바를 업데이트합니다
    /// </summary>
    /// <param name="currentHp">현재 체력</param>
    /// <param name="maxHp">최대 체력</param>
    public void UpdateHp(float currentHp, float maxHp)
    {
        // 방어 코드: hpBar가 null이면 조용히 리턴
        if (hpBar == null)
        {
            return;
        }

        // 비율 기반으로 계산 (0~1 사이 값)
        float ratio = maxHp > 0 ? currentHp / maxHp : 0f;
        hpBar.value = ratio;

        // 선택사항: HP 텍스트 표시
        if (hpText != null)
        {
            hpText.text = $"HP: {currentHp:F0}/{maxHp:F0}";
        }
    }

    /// <summary>
    /// 체력바만 업데이트 (비율로)
    /// </summary>
    /// <param name="hpRatio">체력 비율 (0.0 ~ 1.0)</param>
    public void UpdateHpRatio(float hpRatio)
    {
        // 방어 코드: hpBar가 null이면 조용히 리턴
        if (hpBar == null)
        {
            return;
        }

        hpBar.value = Mathf.Clamp01(hpRatio);
    }
    #endregion

    #region 게임 오버 UI
    /// <summary>
    /// 게임 오버 패널을 표시합니다
    /// </summary>
    /// <param name="score">최종 점수</param>
    /// <param name="dist">최종 거리</param>
    public void ShowGameOver(int score, float dist)
    {
        Debug.Log($"[UIManager] ShowGameOver 호출됨 - 점수: {score}, 거리: {dist:F0}m");

        // 방어 코드: gameOverPanel이 null이면 조용히 리턴 (에러 없이 안전하게 처리)
        if (gameOverPanel == null)
        {
            Debug.LogWarning("[UIManager] gameOverPanel이 null입니다. 게임 오버 UI를 표시할 수 없습니다. (인스펙터 연결 확인 필요)");
            return;
        }

        // 패널 활성화
        gameOverPanel.SetActive(true);
        Debug.Log("[UIManager] 게임 오버 패널 활성화 성공");

        // 최종 점수 표시
        if (finalScoreText != null)
        {
            finalScoreText.text = $"Final Score: {score:N0}";
        }
        else
        {
            Debug.LogWarning("[UIManager] finalScoreText가 null입니다.");
        }

        // 최종 거리 표시
        if (finalDistanceText != null)
        {
            finalDistanceText.text = $"Distance: {dist:F0}m";
        }
        else
        {
            Debug.LogWarning("[UIManager] finalDistanceText가 null입니다.");
        }

        Debug.Log($"[UIManager] 게임 오버 UI 표시 완료");
    }

    /// <summary>
    /// 게임 오버 패널을 숨깁니다
    /// </summary>
    public void HideGameOver()
    {
        // 방어 코드: gameOverPanel이 null이면 조용히 리턴
        if (gameOverPanel == null)
        {
            return;
        }

        gameOverPanel.SetActive(false);
        Debug.Log("[UIManager] 게임 오버 패널 비활성화");
    }
    #endregion

    #region 버튼 이벤트
    /// <summary>
    /// 재시작 버튼 클릭 이벤트
    /// </summary>
    private void OnRestartButtonClicked()
    {
        Debug.Log("재시작 버튼 클릭");
        HideGameOver();
        
        // GameManager를 통해 게임 재시작
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
        }
    }

    /// <summary>
    /// 종료 버튼 클릭 이벤트
    /// </summary>
    private void OnQuitButtonClicked()
    {
        Debug.Log("게임 종료");
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    #endregion

    #region 추가 유틸리티
    /// <summary>
    /// 모든 게임 UI를 표시합니다
    /// </summary>
    public void ShowGameUI()
    {
        if (scoreText != null) scoreText.gameObject.SetActive(true);
        if (distanceText != null) distanceText.gameObject.SetActive(true);
        if (hpBar != null) hpBar.gameObject.SetActive(true);
    }

    /// <summary>
    /// 모든 게임 UI를 숨깁니다
    /// </summary>
    public void HideGameUI()
    {
        if (scoreText != null) scoreText.gameObject.SetActive(false);
        if (distanceText != null) distanceText.gameObject.SetActive(false);
        if (hpBar != null) hpBar.gameObject.SetActive(false);
    }
    #endregion
}
