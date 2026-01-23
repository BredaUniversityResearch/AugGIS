using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public abstract class ToolStationElementBase : MonoBehaviour
{
	[SerializeField]
	[Required]
	protected MeshFilter m_arcMesh;

	[SerializeField]
	[Required]
	protected MeshRenderer m_arcMeshRenderer;

	[SerializeField]
	[Required]
	protected MeshCollider m_arcMeshCollider;

	protected MaterialPropertyBlock m_materialPropertyBlock;

	[SerializeField]
	[HideInInspector]
	private Color m_colorProperty;


	[SerializeField]
	[Required]
	protected Transform m_pivotedTransform;

	[SerializeField]
	[Required]
	protected XRSimpleInteractable m_xrSimpleInteractable;

	[ReadOnly]
	[SerializeField]
	protected int m_indexInMenu = -1;

	[SerializeField]
	[ReadOnly]
	protected ToolStation m_toolStation = null;

	public int IndexInMenu => m_indexInMenu;
	public bool isRegisteredToMenu => m_toolStation != null;

	private bool m_activated = false;

    protected virtual void OnValidate()
    {
		if (m_materialPropertyBlock == null)
		{
			m_materialPropertyBlock = new MaterialPropertyBlock();
		}
        SetColor(m_colorProperty);
    }

    void Awake()
	{
		m_xrSimpleInteractable.selectEntered.AddListener(OnSelectEntered);
		OnValidate();
	}

	void OnEnable()
	{
		//Affordances reset when the object is disabled and enabled so this hack resolves that....
		StartCoroutine(ActivateWithDelay());
	}

	private IEnumerator ActivateWithDelay()
	{
		yield return new WaitForSeconds(0.1f);

		if (m_activated)
		{
			m_xrSimpleInteractable.activated?.Invoke(new ActivateEventArgs());
		}
		else
		{
			m_xrSimpleInteractable.deactivated?.Invoke(new DeactivateEventArgs());
		}
	}

	public void RegisterToStation(ToolStation a_toolStation, int a_indexInMenu)
	{
		m_toolStation = a_toolStation;
		m_indexInMenu = a_indexInMenu;
	}

	public virtual void Initialise(Vector3 a_pivotPosition, Quaternion a_pivotRotation, float a_pivotYRotation, Color a_color, Mesh a_mesh)
	{
		m_arcMesh.mesh = a_mesh;
		m_arcMeshCollider.sharedMesh = a_mesh;
		m_pivotedTransform.localPosition = a_pivotPosition;
		m_pivotedTransform.localRotation = a_pivotRotation;

		transform.localRotation = Quaternion.Euler(0, a_pivotYRotation, 0);

		SetColor(a_color);
	}

	public void SetColor(Color a_color)
	{
		if (m_materialPropertyBlock == null)
		{
			m_materialPropertyBlock = new MaterialPropertyBlock();
		}

		m_colorProperty = a_color;
        m_materialPropertyBlock.SetColor("_Color", m_colorProperty);
		m_arcMeshRenderer.SetPropertyBlock(m_materialPropertyBlock);
	}

	public void ActivateXRInteractable()
	{
		m_xrSimpleInteractable.activated?.Invoke(new ActivateEventArgs());
		m_activated = true;
	}

	public void DeActivateXRInteractable()
	{
		m_xrSimpleInteractable.deactivated?.Invoke(new DeactivateEventArgs());
		m_activated = false;
	}

	[ButtonGroup("Debug Buttons", 100)]
	[Button("Select Entered")]
	private void OnSelectEntered(SelectEnterEventArgs a_args)
	{
		Debug.Assert(isRegisteredToMenu,"Entry is not registeredToMenu! Selection won't work");
		m_toolStation.StartSelectForElement(this, (XRBaseInteractor)a_args.interactorObject);
	}
}
