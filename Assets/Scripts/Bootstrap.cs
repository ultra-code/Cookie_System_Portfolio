using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using CookieRun.Data;

namespace CookieRun
{
    /// <summary>
    /// Bootstrap: 게임 시작 전 모든 데이터 검증
    /// FailFast 철학 - 데이터 문제 발생 시 즉시 실패 처리, 명확한 에러 컨텍스트 제공
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        public enum State
        {
            NotStarted,
            Loading,
            Ready,
            Failed
        }

        public static State SystemState { get; private set; } = State.NotStarted;
        public static string LastError { get; private set; } = string.Empty;
        public static List<PatternEventRow> LoadedPatternEvents { get; private set; } = new List<PatternEventRow>();

        private const int PatternMetaColumns = 4; // ID, TotalDistance, Difficulty, Weight
        private const int PatternEventColumns = 4; // PatternID, SpawnDistance, SpawnID, LaneIndex
        private const int MinLaneIndex = 0;
        private const int MaxLaneIndex = 2;

        private void Start()
        {
            StartCoroutine(InitSystem());
        }

        /// <summary>
        /// [수정 요약]
        /// - CS1626 해결: yield return null을 try-catch 블록 완전히 밖으로 이동
        /// - 검증 로직을 LoadAndValidateAll() 메서드로 분리
        /// - try-catch는 순수 동기 코드만 감싸도록 구조 변경
        /// - FailFast 철학 및 상태 전이 로직 유지
        /// </summary>
        private IEnumerator InitSystem()
        {
            SystemState = State.Loading;
            Debug.Log("[System] Bootstrap Sequence Started...");
            
            // [변경점 1] yield를 try-catch 밖으로 이동 (Work slicing for frame distribution)
            yield return null;

            // [변경점 2] 검증 로직을 별도 메서드로 분리하여 try-catch로 감싸기
            bool success = false;
            try
            {
                success = LoadAndValidateAll();
            }
            catch (Exception ex)
            {
                FailFast($"[Unexpected Error] {ex.Message}\nStackTrace: {ex.StackTrace}");
            }

            // [변경점 3] 실패 시 즉시 코루틴 종료 (FailFast 철학)
            if (!success)
            {
                yield break;
            }

            // [변경점 4] 모든 검증 성공 시에만 Ready 상태로 전이
            SystemState = State.Ready;
            Debug.Log("<color=green>[Bootstrap] All Gameplay Data Validated. System Ready.</color>");
        }

        /// <summary>
        /// [신규 메서드]
        /// 모든 데이터 로드 및 검증을 순차적으로 수행하는 동기 메서드
        /// - PatternMeta 검증 → PatternEvent 검증
        /// - 실패 시 false 반환 (FailFast 내부에서 상태 변경)
        /// - 성공 시 true 반환
        /// </summary>
        private bool LoadAndValidateAll()
        {
            // [Task A, B] PatternMeta.csv 로드 및 검증
            if (!LoadAndValidatePatternMeta())
            {
                return false;
            }

            // [Task C] PatternEvent.csv 로드 및 검증
            if (!LoadAndValidatePatternEvent())
            {
                return false;
            }

            return true;
        }

        private bool LoadAndValidatePatternMeta()
        {
            // 파일 로드
            TextAsset csvFile = Resources.Load<TextAsset>("Data/PatternMeta");
            if (csvFile == null)
            {
                FailFast("[File: PatternMeta.csv, Line: N/A] File Not Found");
                return false;
            }

            // CSV 파싱 (for 루프로 라인 번호 정확히 추적)
            string[] lines = csvFile.text.Split('\n');
            bool headerValidated = false;
            bool validDataFound = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace("\r", "").Trim();
                int lineNumber = i + 1;

                // 빈 줄 스킵
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // 헤더 검증 (첫 유효 라인)
                if (!headerValidated)
                {
                    if (!ValidateHeader(line, lineNumber, "PatternMeta.csv", PatternMetaColumns, 
                        new string[] { "ID", "TotalDistance", "Difficulty", "Weight" }))
                    {
                        return false;
                    }
                    headerValidated = true;
                    continue;
                }

                // 첫 번째 유효 데이터 파싱
                if (!validDataFound)
                {
                    if (!ParseAndValidatePatternMeta(line, lineNumber))
                    {
                        return false;
                    }
                    validDataFound = true;
                    break; // Day 1 정책: 첫 번째 유효 데이터만
                }
            }

            // 유효 데이터가 없는 경우
            if (!validDataFound)
            {
                FailFast("[File: PatternMeta.csv, Line: 2] Data Empty");
                return false;
            }

            return true;
        }

        private bool LoadAndValidatePatternEvent()
        {
            // 파일 로드
            TextAsset csvFile = Resources.Load<TextAsset>("Data/PatternEvent");
            if (csvFile == null)
            {
                FailFast("[File: PatternEvent.csv, Line: N/A] File Not Found");
                return false;
            }

            // CSV 파싱
            string[] lines = csvFile.text.Split('\n');
            bool headerValidated = false;
            int validDataCount = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace("\r", "").Trim();
                int lineNumber = i + 1;

                // 빈 줄 스킵
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // 헤더 검증
                if (!headerValidated)
                {
                    if (!ValidateHeader(line, lineNumber, "PatternEvent.csv", PatternEventColumns,
                        new string[] { "PatternID", "SpawnDistance", "SpawnID", "LaneIndex" }))
                    {
                        return false;
                    }
                    headerValidated = true;
                    continue;
                }

                // 유효 데이터 파싱 (Day2에서는 모든 이벤트 로드)
                if (!ParseAndValidatePatternEvent(line, lineNumber))
                {
                    return false;
                }
                validDataCount++;
            }

            // 유효 데이터가 없는 경우
            if (validDataCount == 0)
            {
                FailFast("[File: PatternEvent.csv, Line: 2] Data Empty");
                return false;
            }

            return true;
        }

        private bool ValidateHeader(string line, int lineNumber, string fileName, int expectedColumns, string[] expectedHeaders)
        {
            string[] cols = line.Split(',');

            // 컬럼 수 검증
            if (cols.Length != expectedColumns)
            {
                FailFast($"[File: {fileName}, Line: {lineNumber}] Header column count mismatch. Expected {expectedColumns}, got {cols.Length}. Raw: {line}");
                return false;
            }

            // 헤더 명 검증 (대소문자 무시, Trim)
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                string actual = cols[i].Trim();
                string expected = expectedHeaders[i];
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    FailFast($"[File: {fileName}, Line: {lineNumber}] Header column {i} mismatch. Expected '{expected}', got '{actual}'. Raw: {line}");
                    return false;
                }
            }

            return true;
        }

        private bool ParseAndValidatePatternMeta(string line, int lineNumber)
        {
            // 컬럼 분리
            string[] cols = line.Split(',');

            // 컬럼 수 검증
            if (cols.Length != PatternMetaColumns)
            {
                FailFast($"[File: PatternMeta.csv, Line: {lineNumber}] Column count mismatch. Expected {PatternMetaColumns}, got {cols.Length}. Raw: {line}");
                return false;
            }

            // 타입 변환
            int id;
            float totalDistance;
            int difficulty;

            try
            {
                id = int.Parse(cols[0].Trim());
            }
            catch (Exception ex)
            {
                FailFast($"[File: PatternMeta.csv, Line: {lineNumber}] Failed to parse ID. Raw: {line}, Error: {ex.Message}");
                return false;
            }

            try
            {
                totalDistance = float.Parse(cols[1].Trim(), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                FailFast($"[File: PatternMeta.csv, Line: {lineNumber}] Failed to parse TotalDistance. Raw: {line}, Error: {ex.Message}");
                return false;
            }

            try
            {
                difficulty = int.Parse(cols[2].Trim());
            }
            catch (Exception ex)
            {
                FailFast($"[File: PatternMeta.csv, Line: {lineNumber}] Failed to parse Difficulty. Raw: {line}, Error: {ex.Message}");
                return false;
            }

            // 논리 검증
            if (id <= 0)
            {
                FailFast($"[File: PatternMeta.csv, Line: {lineNumber}] ID must be > 0. Got: {id}, Raw: {line}");
                return false;
            }

            if (totalDistance <= 0)
            {
                FailFast($"[File: PatternMeta.csv, Line: {lineNumber}] TotalDistance must be > 0. Got: {totalDistance}, Raw: {line}");
                return false;
            }

            if (difficulty <= 0)
            {
                FailFast($"[File: PatternMeta.csv, Line: {lineNumber}] Difficulty must be > 0. Got: {difficulty}, Raw: {line}");
                return false;
            }

            // struct 생성
            PatternMeta meta = new PatternMeta(id, totalDistance, difficulty);

            // Design 로그 출력
            Debug.Log($"<color=cyan>[Design] Pattern {meta.ID} defines total play distance {meta.TotalDistance}m (Difficulty: {meta.Difficulty})</color>");

            return true;
        }

        private bool ParseAndValidatePatternEvent(string line, int lineNumber)
        {
            // 컬럼 분리
            string[] cols = line.Split(',');

            // 컬럼 수 검증
            if (cols.Length != PatternEventColumns)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] Column count mismatch. Expected {PatternEventColumns}, got {cols.Length}. Raw: {line}");
                return false;
            }

            // 타입 변환
            int patternID;
            float spawnDistance;
            int spawnID;
            int laneIndex;

            try
            {
                patternID = int.Parse(cols[0].Trim());
            }
            catch (Exception ex)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] Failed to parse PatternID. Raw: {line}, Error: {ex.Message}");
                return false;
            }

            try
            {
                spawnDistance = float.Parse(cols[1].Trim(), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] Failed to parse SpawnDistance. Raw: {line}, Error: {ex.Message}");
                return false;
            }

            try
            {
                spawnID = int.Parse(cols[2].Trim());
            }
            catch (Exception ex)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] Failed to parse SpawnID. Raw: {line}, Error: {ex.Message}");
                return false;
            }

            try
            {
                laneIndex = int.Parse(cols[3].Trim());
            }
            catch (Exception ex)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] Failed to parse LaneIndex. Raw: {line}, Error: {ex.Message}");
                return false;
            }

            // 논리 검증
            if (patternID <= 0)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] PatternID must be > 0. Got: {patternID}, Raw: {line}");
                return false;
            }

            if (spawnDistance < 0)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] SpawnDistance must be >= 0. Got: {spawnDistance}, Raw: {line}");
                return false;
            }

            if (spawnID <= 0)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] SpawnID must be > 0. Got: {spawnID}, Raw: {line}");
                return false;
            }

            if (laneIndex < MinLaneIndex || laneIndex > MaxLaneIndex)
            {
                FailFast($"[File: PatternEvent.csv, Line: {lineNumber}] LaneIndex must be in range [{MinLaneIndex}-{MaxLaneIndex}]. Got: {laneIndex}, Raw: {line}");
                return false;
            }

            // struct 생성 및 저장
            PatternEventRow eventRow = new PatternEventRow(patternID, spawnDistance, spawnID, laneIndex);
            LoadedPatternEvents.Add(eventRow);

            // Design 로그 출력 (첫 번째 이벤트만)
            if (LoadedPatternEvents.Count == 1)
            {
                Debug.Log($"<color=cyan>[Design] Event: Pattern {eventRow.PatternID} spawns {eventRow.SpawnID} at {eventRow.SpawnDistance}m (Lane {eventRow.LaneIndex})</color>");
            }

            return true;
        }

        private void FailFast(string errorContext)
        {
            SystemState = State.Failed;
            LastError = errorContext;
            Debug.LogError($"[Fatal Error] {LastError}");
        }
    }
}
