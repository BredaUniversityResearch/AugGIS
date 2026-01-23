using UnityEngine;

public class DebugScript : MonoBehaviour
{
    [SerializeField]
    private string m_defaultString = "DEBUG TEST";

    public void CallDebug()
    {
        Debug.Log(m_defaultString);
    }

    public void DebugString(string debugString)
    {
        Debug.Log(debugString);
    }

    public void DebugFloat(float debugFloat)
    {
        Debug.Log(debugFloat);
    }
    public void DebugInt(int debugInt)
    {
        Debug.Log(debugInt);
    }
    public void DebugBool(bool debugBool)
    {
        Debug.Log(debugBool);
    }
}
