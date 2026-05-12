using GLTFast.Schema;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

namespace POV_Unity
{
    public class InfoCard_generic : MonoBehaviour
    {
        public UIDocument Document;

        [SerializeField]
        private string m_cardID;
        public string CardID => m_cardID;

        [SerializeField]
        private InfoCardGenericData m_cardContent;
        public InfoCardGenericData CardContent => m_cardContent;

        [SerializeField]
        private int m_currentImage;
        public int CurrentImage => m_currentImage;

        public event Action CloseInfoCardEvent;
        public event Action<int> ChangeTabEvent;
        public event Action<int> ChangeImageEvent;

        public void Initialise(InfoCardGenericData a_infoCardData, string a_cardID)
        {
            m_cardID = a_cardID;

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

                if (CardContent.images.Length <= 0)
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
                nextButton.clicked += OnNextImage;
                var previousButton = Document.rootVisualElement.Q<Button>("previousImageBtn");
                previousButton.clicked += OnPreviousImage;
                var contentTab = Document.rootVisualElement.Q<TabView>("Content");
                contentTab.activeTabChanged += OnTabChanged;

                if (CardContent.images.Length < 2)
                {
                    nextButton.visible = false;
                    previousButton.visible = false;
                }
            }
        }

        public void UpdateCardDetails(InfoCardGenericData a_infoCardData)
        {
            m_cardContent = a_infoCardData;

            SetCardData();
        }

        void SetCardData()
        {
            Document.rootVisualElement.Q<Label>("title").text = m_cardContent.title;
        }

        private async void LoadImageAsync(UnityEngine.UIElements.Image a_image)
        {
            var texture = await FileLoader.Instance.GetImageAsync(m_cardContent.images[m_currentImage]);
            if (texture != null)
            {
                a_image.image = texture;
            }
            else
            {
                // Optionally hide the container if loading failed
                //a_imageContainer.visible = false;
                Debug.LogWarning($"[ImageCard] Failed to load image: {m_cardContent.images[m_currentImage]}");
            }
        }

        void OnNextImage()
        {
            if (CardContent.images.Length == 0)
                return;
            m_currentImage = (m_currentImage + 1) % CardContent.images.Length;
            var ui_image = Document.rootVisualElement.Q<UnityEngine.UIElements.Image>("cardImage");
            LoadImageAsync(ui_image);
            ChangeImageEvent?.Invoke(m_currentImage);
        }

        void OnPreviousImage()
        {
            if (CardContent.images.Length == 0)
                return;
            m_currentImage = (m_currentImage - 1 + CardContent.images.Length) % CardContent.images.Length;
            var ui_image = Document.rootVisualElement.Q<UnityEngine.UIElements.Image>("cardImage");
            LoadImageAsync(ui_image);
            ChangeImageEvent?.Invoke(m_currentImage);
        }

        public void ChangeImage(int a_imageIndex)
        {
            if (CardContent.images.Length == 0)
                return;
            m_currentImage = a_imageIndex % CardContent.images.Length;
            var ui_image = Document.rootVisualElement.Q<UnityEngine.UIElements.Image>("cardImage");
            LoadImageAsync(ui_image);
        }

        public void ChangeTab(int a_tabIndex)
        {
            var contentTab = Document.rootVisualElement.Q<TabView>("Content");
            contentTab.activeTabChanged -= OnTabChanged;
            contentTab.activeTab = contentTab.GetTab(a_tabIndex);
            contentTab.activeTabChanged += OnTabChanged;
        }

        void OnTabChanged(Tab a_oldTab,Tab a_newTab)
        {
            var contentTab = Document.rootVisualElement.Q<TabView>("Content");
            ChangeTabEvent?.Invoke(a_newTab.tabIndex);
        }

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
            InfoCardGenericData cardData = JsonUtility.FromJson<InfoCardGenericData>(a_jsonData);
            m_cardContent = cardData;
        }
    }

    public class InfoCardGenericData
    {
        public string type = "";
        public string title = "";
        public string[] images = new string[0];
        public InfoCardGenericData[] content = new InfoCardGenericData[0];
    }
    public class InfoCardGenericContentData
    {
        public string category = "";
        public string type = "";
        public string title = "";
        public string content = "";
    }
}