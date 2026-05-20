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

                var ui_imagecontainer = Document.rootVisualElement.Q<VisualElement>("image-container");
                var ui_images = ui_imagecontainer.Q<UnityEngine.UIElements.Image>("cardImage");

                if (CardContent.images.Length <= 0)
                {
                    ui_imagecontainer.style.display = DisplayStyle.None;
                }
                else
                {
                    LoadImageAsync(ui_images);

                    var nextButton = ui_imagecontainer.Q<Button>("nextImageBtn");
                    nextButton.clicked += OnNextImage;
                    var previousButton = ui_imagecontainer.Q<Button>("previousImageBtn");
                    previousButton.clicked += OnPreviousImage;

                    if (CardContent.images.Length < 2)
                    {
                        nextButton.style.display = DisplayStyle.None;
                        previousButton.style.display = DisplayStyle.None;
                    }
                }

                var closeButton = Document.rootVisualElement.Q<Button>("close-button");
                closeButton.clicked += CloseCard;
            }
        }

        public void UpdateCardDetails(InfoCardGenericData a_infoCardData)
        {
            m_cardContent = a_infoCardData;

            SetCardData();
        }

        void SetCardData()
        {
            Document.rootVisualElement.Q<Label>("cardTitle").text = m_cardContent.title;

            var tabView = Document.rootVisualElement.Q<TabView>("content-container");

            foreach (var item in m_cardContent.content)
            {
                //Check if tab exists, otherwise create it
                var itemTab = tabView.Q<Tab>(item.category);
                if (itemTab == null)
                {
                    itemTab = new Tab(item.category);
                    itemTab.name = item.category;
                    tabView.Add(itemTab);
                }

                var template = Resources.Load<VisualTreeAsset>("GenericCardTextElementTemplate");
                var itemElement = template.CloneTree();
                itemTab.Add(itemElement);

                var titleLabel = itemElement.Q<Label>("itemTitle");
                // Optional title
                if (!string.IsNullOrEmpty(item.title))
                {
                    itemElement.name = item.title;
                    titleLabel.text = item.title;
                }
                else
                    titleLabel.style.display = DisplayStyle.None;

                var contentLabel = itemElement.Q<Label>("itemContent");
                contentLabel.text = item.content;

                tabView.activeTabChanged += OnTabChanged;
            }

            //Hide header if only 1 tab
            var headerContainer = tabView.Q<VisualElement>("unity-tab-view__header-container");
            var tabs = tabView.Query<Tab>().ToList();
            if (tabs.Count <= 1)
            {
                headerContainer.style.display = DisplayStyle.None;
            }
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

        public string GetInfoCardDataJson()
        {
            string jsonData = JsonUtility.ToJson(m_cardContent);
            return jsonData;
        }

       public InfoCardGenericData ParseInfoCardDataJson(string a_jsonData)
        {
            InfoCardGenericData cardData = JsonUtility.FromJson<InfoCardGenericData>(a_jsonData);
            m_cardContent = cardData;
            return cardData;
        }
    }

    [System.Serializable]
    public class InfoCardGenericData
    {
        public string type = "";
        public string title = "";
        public string[] images = new string[0];
        public InfoCardGenericContentData[] content = new InfoCardGenericContentData[0];
    }
    [System.Serializable]
    public class InfoCardGenericContentData
    {
        public string category = "";
        public string type = "";
        public string title = "";
        public string content = "";
    }
}