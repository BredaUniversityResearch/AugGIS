using GLTFast.Schema;
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

        [SerializeField]
        private string m_imageName;
        public string CardImage => m_imageName;

        public event Action CloseInfoCardEvent;

        public void Initialise(string a_title, string a_description, string a_cardID, string a_imageName = "")
        {
            m_title = a_title;
            m_description = a_description;
            m_cardID = a_cardID;
            m_imageName = a_imageName;
            
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
                ui_title.text = m_title;
                ui_text.text = m_description;

                var ui_imagecontainer = Document.rootVisualElement.Q<VisualElement>("imagecontainer");
                var ui_images = Document.rootVisualElement.Q<UnityEngine.UIElements.Image>("card-image");

                if (string.IsNullOrEmpty(m_imageName))
                {
                    ui_imagecontainer.visible = false;
                }
                else
                {
                    LoadImageAsync(ui_images);
                }

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

        private async void LoadImageAsync(UnityEngine.UIElements.Image a_image)
        {
            var texture = await FileLoader.Instance.GetImageAsync(m_imageName);
            if (texture != null)
            {
                a_image.image = texture;
            }
            else
            {
                // Optionally hide the container if loading failed
                //a_imageContainer.visible = false;
                Debug.LogWarning($"[ImageCard] Failed to load image: {m_imageName}");
            }
        }

        void CloseDocument()
        {
            ClearImages();

            CloseInfoCardEvent?.Invoke();

            Destroy(this.gameObject);
        }

        public void CloseDocument(bool a_invokeEvent = true)
        {
            ClearImages();

            if (a_invokeEvent)
                CloseInfoCardEvent?.Invoke();

            Destroy(this.gameObject);
        }

        void ClearImages()
        {
            if (!string.IsNullOrEmpty(m_imageName))
            {
                // Clear from UI
                Document.rootVisualElement.Q<VisualElement>("card-image").style.backgroundImage = StyleKeyword.Null;

                // Tell FileLoader we no longer need it
                FileLoader.Instance.ReleaseImage(m_imageName);
            }
        }
    }
}