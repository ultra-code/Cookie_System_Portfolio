using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    [Header("Map Scrolling Settings")]
    public GameObject[] mapPrefabs;
    public float scrollSpeed = 10f;
    public float patternWidth = 20f;

    // 활성화된 맵 패턴들 (Spawn & Destroy 방식)
    private Queue<GameObject> activePatterns = new Queue<GameObject>();

    // 화면 시작 X 좌표(플레이어는 -6에 고정. 초기 맵 배치 계산용)
    private float startX = -6f;
    
    // [수정 6] 마지막 생성된 패턴의 X 좌표를 멤버로 유지 (O(1) 계산)
    private float lastPatternX;

    void Start()
    {
        // Fail Fast: scrollSpeed 검증
        if (scrollSpeed <= 0f)
        {
            Debug.LogError("[MapManager] scrollSpeed는 0보다 커야 합니다! (현재값: " + scrollSpeed + ")");
            enabled = false;
            return;
        }

        // Fail Fast: mapPrefabs 배열 검증
        if (mapPrefabs == null || mapPrefabs.Length == 0)
        {
            Debug.LogError("[MapManager] mapPrefabs가 설정되지 않았습니다!");
            enabled = false;
            return;
        }

        // Fail Fast: mapPrefabs 내부 null 검증
        foreach (var prefab in mapPrefabs)
        {
            if (prefab == null)
            {
                Debug.LogError("[MapManager] mapPrefabs 배열에 null 프리팹이 포함되어 있습니다!");
                enabled = false;
                return;
            }
        }

        // [수정 6] 초기화
        lastPatternX = startX - patternWidth; // 첫 패턴이 startX에 생성되도록

        // 화면을 꽉 채울 만큼 미리 맵 생성 (약 4개)
        int initialPatternCount = 4;

        for (int i = 0; i < initialPatternCount; i++)
        {
            // [수정 6] lastPatternX 기반으로 생성
            float nextX = lastPatternX + patternWidth;
            SpawnRandomPattern(nextX);
        }
    }

    void Update()
    {
        // [거리 SSOT 정책] scrollSpeed가 실제 월드 이동 속도이며, 거리 누적의 단일 진실 원천입니다.
        // 거리 누적은 activePatterns 존재 여부와 무관하게 항상 실행되어야 합니다.
        if (GameManager.Instance != null && GameManager.Instance.isGameActive)
        {
            GameManager.Instance.AddDistanceFromWorldScroll(scrollSpeed);
        }

        // activePatterns가 없으면 이동 로직 스킵
        if (activePatterns.Count == 0)
            return;

        // 모든 활성화된 맵 패턴 이동
        foreach (GameObject pattern in activePatterns)
        {
            if (pattern != null)
            {
                pattern.transform.position += Vector3.left * scrollSpeed * Time.deltaTime;
            }
        }

        // Spawn & Destroy 로직: 가장 앞의 패턴이 화면 왼쪽(-25) 밖으로 나가면 파괴하고 새로 생성
        GameObject firstPattern = activePatterns.Peek();
        if (firstPattern != null && firstPattern.transform.position.x < -25f)
        {
            // 1. 큐에서 제거
            firstPattern = activePatterns.Dequeue();

            // 2. 오브젝트 파괴
            Destroy(firstPattern);

            // 3. O(1) 계산: lastPatternX를 바로 사용
            float newX = lastPatternX + patternWidth;

            // 4. 새로운 랜덤 패턴 생성
            SpawnRandomPattern(newX);
        }
    }

    /// <summary>
    /// 지정된 X 위치에 랜덤 맵 패턴을 생성하고 큐에 추가
    /// </summary>
    /// <param name="xPosition">생성할 X 좌표</param>
    private void SpawnRandomPattern(float xPosition)
    {
        // mapPrefabs 배열에서 랜덤으로 선택
        int randomIndex = Random.Range(0, mapPrefabs.Length);
        GameObject selectedPrefab = mapPrefabs[randomIndex];

        // 인스턴스 생성 및 위치 지정
        GameObject newPattern = Instantiate(
            selectedPrefab,
            new Vector3(xPosition, 0f, 0f),
            Quaternion.identity,
            transform
        );

        // 큐에 추가
        activePatterns.Enqueue(newPattern);
        
        // [수정 6] 마지막 생성 위치 갱신
        lastPatternX = xPosition;
    }
}

