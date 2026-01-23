using UnityEngine;
using System;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ColourPalette
{
    [Serializable]
    [CreateAssetMenu(fileName = "NewColourAsset", menuName = "ColourPalette/ColourAsset", order = 100)]
    public class ColourAsset : ScriptableObject, IColourContainer
    {
        [SerializeField]
        private Color value;
        [HideInInspector] public ColourChangedEvent valueChangedEvent = new ColourChangedEvent();

        private void OnEnable()
        {
#if UNITY_EDITOR
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Plugins/ColourPalette/Sprites/color_icon.png");
            EditorGUIUtility.SetIconForObject(this, icon);
#endif
        }

        public void SetValue(Color newValue)
        {
            this.value = newValue;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            if (valueChangedEvent != null)
                valueChangedEvent.Invoke(newValue);
        }

        public Color GetColour()
        {
            return value;
        }

        public void SubscribeToChanges(UnityAction<Color> a_callback)
        {
            valueChangedEvent.AddListener(a_callback);
        }

        public void UnSubscribeFromChanges(UnityAction<Color> a_callback)
        {
            valueChangedEvent.RemoveListener(a_callback);
        }
    }

    public class ColourChangedEvent : UnityEvent<Color> { }
}
