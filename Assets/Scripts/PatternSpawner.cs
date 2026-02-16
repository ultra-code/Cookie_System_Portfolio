using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CookieRun.Data;

namespace CookieRun
{
    /// <summary>
    /// PatternSpawner: CSV 데이터 기반 스폰 검증기
    /// "엑셀에 적은 SpawnDistance대로 씬에 큐브가 찍힌다"를 증명
    /// </summary>
    public class PatternSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("장애물 큐브 프리팹 (null이면 기본 Cube 생성)")]
        public GameObject obstacleCubePrefab;

        [Tooltip("스크롤 속도 (거리 증가용, SSOT)")]
        public float testScrollSpeed = 10f;

        [Header("Lane Configuration")]
        [Tooltip("레인별 Y 좌표")]
        public float[] laneYPositions = new float[] { 0f, 1.5f, 3.0f };

        // 내부 상태
        private float currentDistance = 0f;
        private List<PatternEventRow> sortedEvents = new List<PatternEventRow>();
        private int nextIndex = 0;

        private void Start()
        {
            // 프리팹 검증
            if (obstacleCubePrefab == null)
            {
                Debug.LogWarning("[PatternSpawner] obstacleCubePrefab is null. Using default Cube primitive.");
            }

            // Bootstrap 완료 대기는 Update에서 처리
        }

        private void Update()
        {
            // Bootstrap이 Ready 상태가 아니면 대기
            if (Bootstrap.SystemState != Bootstrap.State.Ready)
                return;

            // 이벤트 로드 (최초 1회)
            if (sortedEvents.Count == 0)
            {
                LoadAndSortEvents();
                if (sortedEvents.Count == 0)
                {
                    Debug.LogError("[PatternSpawner] No events loaded. Disabling spawner.");
                    enabled = false;
                    return;
                }
            }

            // 거리 증가
            currentDistance += testScrollSpeed * Time.deltaTime;

            // 스폰 처리
            while (nextIndex < sortedEvents.Count && currentDistance >= sortedEvents[nextIndex].SpawnDistance)
            {
                SpawnObstacle(sortedEvents[nextIndex]);
                nextIndex++;
            }

            // 모든 이벤트 처리 완료
            if (nextIndex >= sortedEvents.Count)
            {
                Debug.Log("<color=yellow>[PatternSpawner] All events spawned. Disabling spawner.</color>");
                enabled = false;
            }
        }

        private void LoadAndSortEvents()
        {
            sortedEvents = Bootstrap.LoadedPatternEvents.OrderBy(e => e.SpawnDistance).ToList();
            Debug.Log($"<color=green>[PatternSpawner] Loaded {sortedEvents.Count} events, sorted by SpawnDistance.</color>");
        }

        private void SpawnObstacle(PatternEventRow eventData)
        {
            // Y 좌표 계산
            float yPos = 0f;
            if (eventData.LaneIndex >= 0 && eventData.LaneIndex < laneYPositions.Length)
            {
                yPos = laneYPositions[eventData.LaneIndex];
            }
            else
            {
                Debug.LogWarning($"[PatternSpawner] LaneIndex {eventData.LaneIndex} out of range. Using Y=0.");
            }

            // 위치 설정 (X = SpawnDistance)
            Vector3 spawnPosition = new Vector3(eventData.SpawnDistance, yPos, 0f);

            // 오브젝트 생성
            GameObject obstacle;
            if (obstacleCubePrefab != null)
            {
                obstacle = Instantiate(obstacleCubePrefab, spawnPosition, Quaternion.identity);
            }
            else
            {
                // 기본 Cube 생성
                obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.transform.position = spawnPosition;
                obstacle.name = $"Obstacle_{eventData.SpawnID}";
                
                // 색상 랜덤화 (시각적 구분)
                Renderer renderer = obstacle.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.5f, 1f);
                }
            }

            // Design 로그 출력
            Debug.Log($"<color=cyan>[Design] Spawned {eventData.SpawnID} at Distance {eventData.SpawnDistance}m Lane {eventData.LaneIndex}</color>");
        }
    }
}
