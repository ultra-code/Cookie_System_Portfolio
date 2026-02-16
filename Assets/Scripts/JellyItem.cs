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