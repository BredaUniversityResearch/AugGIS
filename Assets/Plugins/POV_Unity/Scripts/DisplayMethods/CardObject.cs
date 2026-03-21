using OpenCVForUnity.DnnModule;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace POV_Unity
{
    public class CardObject : MonoBehaviour
    {
        [HideInInspector] public ADisplayMethod m_displayMethod;
        public UIDocument Document;

        public void Initialise(VectorLayer a_layer, VectorObject a_object, ADisplayMethod a_displayMethod)
        {
            m_displayMethod = a_displayMethod;

            GameObject go = Instantiate(AssetManager.GetTemplate("CardTemplate"), transform);

            Document = go.GetComponentInChildren<UIDocument>();

            if (Document == null)
                return;

            var ui_title = Document.rootVisualElement.Q<Label>("title");
            var ui_text = Document.rootVisualElement.Q<Label>("text");
            ui_title.text = a_displayMethod.GetVariable<string>("title", a_layer, a_object);
            ui_text.text = a_displayMethod.GetVariable<string>("description", a_layer, a_object);

            var button = Document.rootVisualElement.Q<Button>("accept-button");
            button.clicked += CloseDocument;
        }

        void CloseDocument()
        {
            Document.gameObject.SetActive(false);
        }
    }
}