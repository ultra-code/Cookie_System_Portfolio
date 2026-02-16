using UnityEngine;

public class JellyItem : MonoBehaviour
{
    public int JellyId;

    private void OnValidate()
    {
        if (JellyId <= 0)
        {
            Debug.LogWarning($"[JellyItem] JellyId is invalid on {gameObject.name}");
        }
        
#if UNITY_EDITOR
        // DataManager가 로드된 상태라면 JellyId가 테이블에 존재하는지 검증
        if (JellyId > 0 && DataManager.Instance != null && DataManager.Instance.IsLoaded)
        {
            try
            {
                // 테이블에 ID가 존재하는지 확인 (GetJellyBaseScore는 없으면 예외 발생)
                int score = DataManager.Instance.GetJellyBaseScore(JellyId);
                Debug.Log($"[JellyItem] '{gameObject.name}' JellyId={JellyId} 검증 성공 (Score={score})");
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                Debug.LogError($"[JellyItem] '{gameObject.name}' JellyId={JellyId}는 JellyData.csv에 존재하지 않습니다! (유효한 ID 예: 1001=BasicJelly, 1002=YellowBear)", this);
            }
        }
#endif
    }

    private void Reset()
    {
        try
        {
            gameObject.tag = "Jelly";
        }
        catch (UnityException)
        {
        }
    }
}