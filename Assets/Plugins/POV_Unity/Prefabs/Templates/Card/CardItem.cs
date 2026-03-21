using Mono.Cecil.Cil;
using UnityEngine;
using UnityEngine.UIElements;

namespace POV_Unity
{

    public class CardItem : MonoBehaviour
    {
        public UIDocument Document;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            var button = Document.rootVisualElement.Q<Button>("accept-button");
            button.clicked += CloseDocument;
        }

        void CloseDocument()
        {
            Document.gameObject.SetActive(false);
        }
    }
}