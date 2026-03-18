using OpenCVForUnity.DnnModule;
using System.Collections;
using UnityEngine;

namespace POV_Unity
{
    public class CardObject : MonoBehaviour
    {
        [HideInInspector] public ADisplayMethod m_displayMethod;

        public void Initialise(ADisplayMethod a_displayMethod)
        {
            m_displayMethod = a_displayMethod;
            GameObject go = Instantiate(AssetManager.GetTemplate("CardTemplate"), transform);
        }
    }
}