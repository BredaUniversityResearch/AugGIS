using Sirenix.OdinInspector;
using UnityEngine;


[RequireComponent(typeof(MapPlaceableTool))]
public class MapPlaceableSoundCues : MonoBehaviour
{
    [SerializeField]
    private AudioPreset m_snapToMapPreset;

    [SerializeField]
    private AudioPreset m_removeFromMapPreset;

    [SerializeField]
    [ShowIf("@this.GetComponent<MapPlaceableToolDuplicator>()")]
    private AudioPreset m_duplicationCompletePreset;

    private MapPlaceableTool m_mapPlaceableTool;
    private MapPlaceableToolDuplicator m_mapPlaceableToolDuplicator;

    void Awake()
    {
        m_mapPlaceableTool = GetComponent<MapPlaceableTool>();
        m_mapPlaceableTool.SnappedToMap += PlayPlaceOnMapSound;
        m_mapPlaceableTool.UnsnappedFromMap += PlayRemoveFromMapSound;

        if (TryGetComponent<MapPlaceableToolDuplicator>(out var duplicator))
        {
            m_mapPlaceableToolDuplicator = duplicator;
            m_mapPlaceableToolDuplicator.OnDuplicationComplete += PlayDuplicationCompleteSound;
        }
    }

    void OnDestroy()
    {
        m_mapPlaceableTool.SnappedToMap -= PlayPlaceOnMapSound;
        m_mapPlaceableTool.UnsnappedFromMap -= PlayRemoveFromMapSound;

        if (m_mapPlaceableToolDuplicator != null)
        {
            m_mapPlaceableToolDuplicator.OnDuplicationComplete -= PlayDuplicationCompleteSound;
        }
    }

    public void PlayPlaceOnMapSound()
    {
        if (m_snapToMapPreset != null)
        {
            AudioManager.Instance.PlaySound3D(m_snapToMapPreset, transform.position);
        }
    }

    public void PlayRemoveFromMapSound()
    {
        if (m_removeFromMapPreset != null)
        {
            AudioManager.Instance.PlaySound3D(m_removeFromMapPreset, transform.position);
        }
    }

    public void PlayDuplicationCompleteSound()
    {
        if (m_duplicationCompletePreset != null)
        {
            AudioManager.Instance.PlaySound3D(m_duplicationCompletePreset, transform.position);
        }
    }
}