using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;

namespace ColourPalette
{
    [CreateAssetMenu(fileName = "NewColourPalette", menuName = "ColourPalette/ColourPalette", order = 101)]
    public class ColourPalette : SerializedScriptableObject
    {
        [SerializeField, OnValueChanged("ColourAssetsChanged"), ListDrawerSettings(NumberOfItemsPerPage = 40)]
        List<ColourAsset> colourAssets = null;

        [SerializeField, ListDrawerSettings(CustomAddFunction = "AddColourSet", NumberOfItemsPerPage = 40)]
        List<ColourSet> colourSets = null;

        private void OnEnable()
        {
#if UNITY_EDITOR
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Plugins/ColourPalette/Sprites/palette_icon.png");
            EditorGUIUtility.SetIconForObject(this, icon);
#endif
        }

        void ColourAssetsChanged()
        {
            if (colourSets != null)
            {
                foreach (ColourSet set in colourSets)
                    set.UpdateSize(NumberAssets);
            }
        }

        ColourSet AddColourSet()
        {
            return new ColourSet(NumberAssets, this);
        }

        int NumberAssets
        {
            get
            {
                if (colourAssets == null)
                    return 0;
                return colourAssets.Count;
            }
        }

        public void ActivateColorSet(ColourSet set)
        {
            set.ActivateSet(colourAssets);
        }

        public void ForceActivateColorSet(ColourSet set)
        {
            set.ForceActivateSet(colourAssets);
        }

        public void ActivateColorSet(int index)
        {
            if (colourSets == null || index >= colourSets.Count)
                return;
            colourSets[index].ActivateSet(colourAssets);
        }

        public void ActivateColorSet(string name)
        {
            if (colourSets == null)
                return;
            foreach (ColourSet set in colourSets)
            {
                if (set.name == name)
                {
                    set.ActivateSet(colourAssets);
                    break;
                }
            }
        }
    }
}
