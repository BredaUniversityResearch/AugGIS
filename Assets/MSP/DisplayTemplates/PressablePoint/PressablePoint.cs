using UnityEngine;

namespace POV_Unity
{
    public class PressablePoint : MonoBehaviour
    {
        public void PressPoint()
        {
            InfoCardManager.Instance.SpawnInfoCard(new Vector3(0, 0, 0));
            Debug.Log("Pressed point: " + gameObject.name);
        }
    }
}