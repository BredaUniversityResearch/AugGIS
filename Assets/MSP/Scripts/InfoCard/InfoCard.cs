using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace POV_Unity
{
    public class InfoCard : MonoBehaviour
    {
        public UIDocument Document;

        private string m_title;
        public string CardTitle => m_title;

        private string m_description;
        public string CardDescription => m_description;


        bool initialized = false;

        public event Action<string> TitleChangedEvent;
        public event Action<string> DescriptionChangedEvent;
        public event Action CloseInfoCardEvent;

        public void Initialise(string title, string description)
        {
            m_title = title;
            m_description = description;

            if (Document == null)
                Document = GetComponentInChildren<UIDocument>();

            StartCoroutine(InitGUI());
            initialized = true;
        }

        void Start()
        {
            if(!initialized)
            {
                Initialise(gameObject.name, "No description provided");
            }
        }

        private IEnumerator InitGUI()
        {
            yield return null;

            if (Document != null)
            {
                var ui_title = Document.rootVisualElement.Q<Label>("title");
                var ui_text = Document.rootVisualElement.Q<Label>("text");
                ui_title.text = m_title;
                ui_text.text = m_description;

                var button = Document.rootVisualElement.Q<Button>("close-button");
                button.clicked += CloseDocument;

                Document.gameObject.GetComponent<BoxCollider>();
            }
        }

        public void SetTitle(string a_title, bool notify = true)
        {
            m_title = a_title;
            if (Document != null)
            {
                var ui_title = Document.rootVisualElement.Q<Label>("title");
                ui_title.text = m_title;
            }

            if (notify)
                TitleChangedEvent?.Invoke(a_title);
        }

        public void SetDescription(string a_description, bool notify = true)
        {
            m_title = a_description;
            if (Document != null)
            {
                var ui_title = Document.rootVisualElement.Q<Label>("title");
                ui_title.text = m_title;
            }

            if (notify)
                TitleChangedEvent?.Invoke(a_description);
        }

        void CloseDocument()
        {
            CloseInfoCardEvent?.Invoke();
        }
    }
}