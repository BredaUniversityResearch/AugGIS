using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;


// Should become a generic class, with some default functions
//  Initiaze with title
//  Close / open
// Additional cards inherit and add their own functionality


// Probably want a default with categories. If only one then show that, if multiple then show several.
// Each option should just have a text-field, where new fields are added as needed
// 


namespace POV_Unity
{
    public class InfoCard : MonoBehaviour
    {
        public UIDocument Document;

        [SerializeField]
        private string m_cardID;
        public string CardID => m_cardID;

        [SerializeField]
        private string m_title;
        public string CardTitle => m_title;

        [SerializeField]
        private string m_description;
        public string CardDescription => m_description;

        public event Action CloseInfoCardEvent;

        public void Initialise(string a_title, string a_description, string a_cardID)
        {
            m_title = a_title;
            m_description = a_description;
            m_cardID = a_cardID;

            if (Document == null)
                Document = GetComponentInChildren<UIDocument>();

            StartCoroutine(InitGUI());
        }

        private IEnumerator InitGUI()
        {
            yield return null;

            if (Document != null)
            {
                var ui_title = Document.rootVisualElement.Q<Label>("title");
                var ui_text = Document.rootVisualElement.Q<Label>("text");
                var ui_images = Document.rootVisualElement.Q<VisualElement>("Images");
                ui_title.text = m_title;
                ui_text.text = m_description;
                ui_images.visible = false;

                var button = Document.rootVisualElement.Q<Button>("close-button");
                button.clicked += CloseDocument;

                Document.gameObject.GetComponent<BoxCollider>();
            }
        }

        public void SetTitle(string a_title)
        {
            m_title = a_title;
            if (Document != null)
            {
                var ui_title = Document.rootVisualElement.Q<Label>("title");
                ui_title.text = m_title;
            }
        }

        public void SetDescription(string a_description)
        {
            m_description = a_description;
            if (Document != null)
            {
                var ui_text = Document.rootVisualElement.Q<Label>("text");
                ui_text.text = m_description;
            }
        }

        void CloseDocument()
        {
            CloseInfoCardEvent?.Invoke();
        }
    }
}