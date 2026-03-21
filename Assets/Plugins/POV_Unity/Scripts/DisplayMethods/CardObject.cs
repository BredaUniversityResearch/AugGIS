using OpenCVForUnity.DnnModule;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace POV_Unity
{
    public class CardObject : MonoBehaviour
    {
        [HideInInspector] public ADisplayMethod m_displayMethod;
        [HideInInspector] public VectorObject m_object;
        [HideInInspector] public VectorLayer m_layer;
        public UIDocument Document;

        public void Initialise(VectorLayer a_layer, VectorObject a_object, ADisplayMethod a_displayMethod)
        {
            m_displayMethod = a_displayMethod;
            m_object = a_object;
            m_layer = a_layer;

            GameObject go = Instantiate(AssetManager.GetTemplate("CardTemplate"), transform);

            Document = go.GetComponentInChildren<UIDocument>();

        }

        //Initializing GUI on start coroutine since the UIDocument needs a frame to finalize, so it cannot be done in initialise
        private IEnumerator Start()
        {
            yield return null;

            if (Document != null)
            {
                var ui_title = Document.rootVisualElement.Q<Label>("title");
                var ui_text = Document.rootVisualElement.Q<Label>("text");
                ui_title.text = m_displayMethod.GetVariable<string>("title", m_layer, m_object);
                ui_text.text = m_displayMethod.GetVariable<string>("description", m_layer, m_object);

                var button = Document.rootVisualElement.Q<Button>("accept-button");
                button.clicked += CloseDocument;
            }
        }

        void CloseDocument()
        {
            Document.gameObject.SetActive(false);
        }
    }
}