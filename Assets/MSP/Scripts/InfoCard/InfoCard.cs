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
        private InfoCardData m_cardContent;
        public InfoCardData CardContent => m_cardContent;

        [SerializeField]
        private int m_currentImage;
        public int CurrentImage => m_currentImage;

        public event Action CloseInfoCardEvent;

        public void Initialise(InfoCardData a_infoCardData)
        {
            m_cardContent = a_infoCardData;

            if (Document == null)
                Document = GetComponentInChildren<UIDocument>();

            StartCoroutine(InitGUI());
        }

        private IEnumerator InitGUI()
        {
            yield return null;
            if (Document != null)
            {
                SetCardData();

                var ui_imagecontainer = Document.rootVisualElement.Q<VisualElement>("Images");
                var ui_images = Document.rootVisualElement.Q<UnityEngine.UIElements.Image>("cardImage");

                if (CardContent.images.Length < 0)
                {
                    ui_imagecontainer.visible = false;
                }
                else
                {
                    LoadImageAsync(ui_images);
                }

                var closeButton = Document.rootVisualElement.Q<Button>("close-button");
                closeButton.clicked += CloseCard;
                var nextButton = Document.rootVisualElement.Q<Button>("nextImageBtn");
                nextButton.clicked += NextImage;
                var previousButton = Document.rootVisualElement.Q<Button>("previousImageBtn");
                previousButton.clicked += PreviousImage;
                //TO DO ADD CALLBACK FOR TAB SWITCHING

                Document.gameObject.GetComponent<BoxCollider>();
            }
        }

        public void UpdateCardDetails(InfoCardData a_infoCardData)
        {
            m_cardContent = a_infoCardData;

            SetCardData();
        }

        void SetCardData()
        {
            Document.rootVisualElement.Q<Label>("title").text = m_cardContent.title;
            Document.rootVisualElement.Q<Label>("descriptionText").text = m_cardContent.description;
            Document.rootVisualElement.Q<Label>("timeText").text = m_cardContent.time;
            Document.rootVisualElement.Q<Label>("costText").text = m_cardContent.cost;
            Document.rootVisualElement.Q<Label>("phoneText").text = m_cardContent.phone;
            Document.rootVisualElement.Q<Label>("addressText").text = m_cardContent.location;
            Document.rootVisualElement.Q<Label>("websiteText").text = m_cardContent.cost;
        }

        private async void LoadImageAsync(UnityEngine.UIElements.Image a_image)
        {
            var texture = await FileLoader.Instance.GetImageAsync(m_cardContent.images[0]);
            if (texture != null)
            {
                a_image.image = texture;
            }
            else
            {
                // Optionally hide the container if loading failed
                //a_imageContainer.visible = false;
                Debug.LogWarning($"[ImageCard] Failed to load image: {m_cardContent.images[0]}");
            }
        }

        //TO DO, NETWORK THIS
        void NextImage()
        {
            if (CardContent.images.Length == 0)
                return;
            m_currentImage = (m_currentImage + 1) % CardContent.images.Length;
            var ui_image = Document.rootVisualElement.Q<UnityEngine.UIElements.Image>("cardImage");
            LoadImageAsync(ui_image);
        }

        //TO DO, NETWORK THIS
        void PreviousImage()
        {
            if (CardContent.images.Length == 0)
                return;
            m_currentImage = (m_currentImage - 1 + CardContent.images.Length) % CardContent.images.Length;
            var ui_image = Document.rootVisualElement.Q<UnityEngine.UIElements.Image>("cardImage");
            LoadImageAsync(ui_image);
        }


        //TO DO, ADD NETWORKING FOR THE TAB SWITCHING!!!

        void CloseCard()
        {
            ClearImages();

            CloseInfoCardEvent?.Invoke();

            Destroy(this.gameObject);
        }

        public void CloseCard(bool a_invokeEvent = true)
        {
            ClearImages();

            if (a_invokeEvent)
                CloseInfoCardEvent?.Invoke();

            Destroy(this.gameObject);
        }

        void ClearImages()
        {
            if (CardContent.images.Length>0)
            {
                // Clear from UI
                Document.rootVisualElement.Q<VisualElement>("cardImage").style.backgroundImage = StyleKeyword.Null;

                // Tell FileLoader we no longer need it
                FileLoader.Instance.ReleaseImage(m_cardContent.images[0]);
            }
        }

        string GetInfoCardDataJson()
        {
            string jsonData = JsonUtility.ToJson(m_cardContent);
            return jsonData;
        }

        void ParseInfoCardDataJson(string a_jsonData)
        {
            InfoCardData cardData = JsonUtility.FromJson<InfoCardData>(a_jsonData);
            m_cardContent = cardData;
        }
    }

    public class InfoCardData
    {
        public string title = "";
        public string description = "";
        public string[] images = new string[0];
        public string time = "";
        public string cost = "";
        public string phone = "";
        public string location = "";
        public string rating = "";
        public string website = "";
    }
}