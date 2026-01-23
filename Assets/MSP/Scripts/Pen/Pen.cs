using UnityEngine;
using System;
using Sirenix.OdinInspector;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

public class Pen : MonoBehaviour
{
	public event Action<Color> ColorChangedEvent;
	public event Action<float> WidthChangedEvent;
	public event Action<Vector3> ExternalLineSpawnerEvent;

	[Header("General")]
	[SerializeField]
	private GameObject[] m_coloredObjects;

	[SerializeField]
	private LayerMask m_drawingLayerMask;

	[SerializeField]
	[Required]
	private XRGrabInteractable m_xrGrabInteractable;

	[SerializeField]
	[Required]
	private Transform m_castOrigin;

	[SerializeField]
	private float m_castDistance = 0.08f;

	[SerializeField]
	private Color m_penColor = Color.black;
	public Color PenColor => m_penColor;

	[SerializeField]
	private float m_penWidth = 0.01f;
	public float PenWidth => m_penWidth;

	[Header("Feel")]
	[SerializeField]
	private float m_drawingOffset = 0.1f;
	
	[SerializeField]
	[Required]
	private Transform m_tipPivotTransform;
	
	[SerializeField]
	private float m_minTipLength = 0.1f;
	
	[SerializeField]
	private float m_maxTipLength = 0.75f;
	
	[SerializeField]
	private float m_restTipLength = 0.5f;
	
	[SerializeField]
	private float m_tipCastOffset = 0.75f;

	[Header("Line")]
	[SerializeField]
	[Required]
	private PenLine m_linePrefab;
	public PenLine LinePrefab => m_linePrefab;

	[SerializeField]
	[Required]
	private Eraser m_eraser;

	[SerializeField]
	private float m_drawingUpdateDistance = 0.05f;

	[SerializeField]
	private TransformReference m_penLineRootTransformReference;
	public TransformReference PenLineRootTransformReference => m_penLineRootTransformReference;

	[Header("Debug")]
	[SerializeField]
	[ReadOnly]
	private bool m_drawing = false;

	[SerializeField]
	[ReadOnly]
	private PenLine m_currentLine = null;

	[SerializeField]
	[ReadOnly]
	private float m_hitDistance;

	[SerializeField]
	[ReadOnly]
	private Vector3 m_lastHitPosition;

	[SerializeField]
	[ReadOnly]
	private bool m_holdingPen = false;

	private RaycastHit m_latestHit;

	private void Start()
	{
		m_xrGrabInteractable.selectEntered.AddListener(StartHoldingPen);
		m_xrGrabInteractable.selectExited.AddListener(EndHoldingPen);

		SetColor(m_penColor);
		SetWidth(m_penWidth);
	}

	private void Update()
	{
		CheckPenTouchState();
		UpdateDrawing();
	}
	
	private void StartHoldingPen(SelectEnterEventArgs a_args)
	{
		m_holdingPen = true;
		m_eraser.enabled = true;
	}

	private void EndHoldingPen(SelectExitEventArgs a_args)
	{
		m_holdingPen = false;
		m_eraser.enabled = false;
	}

	public void SetColor(Color a_color)
	{
		SetColorWithoutNotify(a_color);
		ColorChangedEvent?.Invoke(a_color);
	}
	public void SetWidth(float a_width)
	{
		SetWidthWithoutNotify(a_width);
		WidthChangedEvent?.Invoke(a_width);
	}

	public void SetColorWithoutNotify(Color a_color)
	{
		m_penColor = a_color;
		foreach (GameObject coloredObject in m_coloredObjects)
		{
			coloredObject.GetComponent<Renderer>().material.SetColor("_DynamicColor", m_penColor);
		}
	}

	public void SetWidthWithoutNotify(float a_width)
	{
		m_penWidth = a_width;
		m_tipPivotTransform.transform.localScale = new Vector3(a_width, m_tipPivotTransform.transform.localScale.y, a_width);
	}

	void UpdateTipSize()
	{
		float tipLength = Mathf.Clamp(m_hitDistance-m_tipCastOffset, m_minTipLength, m_maxTipLength);
		m_tipPivotTransform.localScale = new Vector3(m_penWidth, tipLength, m_penWidth);
	}

	void ResetTip()
	{
		m_tipPivotTransform.localScale = new Vector3(m_penWidth, m_restTipLength, m_penWidth);
	}

	public void SetCurrentLine(PenLine a_newLine)
	{
		m_currentLine = a_newLine;
	}

	void UpdateDrawing()
	{
		if (m_drawing == false)
			return;

		UpdateTipSize();

		if(m_currentLine != null)
		{
			float pointDistance = m_currentLine.GetLastPointDistance(m_lastHitPosition);

			if (pointDistance > m_drawingUpdateDistance)
			{
				m_currentLine.AddGlobalPoint(m_lastHitPosition);
			}
		}
	}

	void StartDrawing(Vector3 a_hitPosition)
	{
		m_currentLine = null;

		m_drawing = true;

		if (ExternalLineSpawnerEvent == null)
		{
			m_currentLine = Instantiate<PenLine>(m_linePrefab, this.transform.parent);
			m_currentLine.transform.position = a_hitPosition;
			m_currentLine.ChangeLineColor(m_penColor);
			m_currentLine.ChangeLineWidth(m_penWidth);
		}
		else
		{
			ExternalLineSpawnerEvent.Invoke(m_penLineRootTransformReference.TransformRef.InverseTransformPoint(a_hitPosition));
		}
	}

	void StopDrawing()
	{
		m_drawing = false;
		ResetTip();

		if (m_currentLine == null)
		{
			return;
		}

		m_currentLine.FinishLine();
		m_currentLine = null;
	}

	//ENABLE TO VISUALIZE CASTING AND TIP VISUALIZATION OUTSIDE PLAY MODE
   //void OnDrawGizmos()
   //{
   //    Gizmos.color = Color.red;
   //    Gizmos.DrawLine(m_castOrigin.position, m_castOrigin.position+m_castOrigin.transform.forward* m_castDistance);
   //    Gizmos.color = Color.blue;
   //    Gizmos.DrawLine(m_tipPivotTransform.position, m_tipPivotTransform.position+-m_tipPivotTransform.transform.up*m_maxTipLength);
   //}

    void CheckPenTouchState()
	{
		if(m_holdingPen)
		{
			RaycastHit hit;
			// Does the ray intersect any objects excluding the player layer
			if (Physics.Raycast(m_castOrigin.position, m_castOrigin.transform.forward, out hit, m_castDistance, m_drawingLayerMask))
			{
				Debug.DrawRay(m_castOrigin.position, m_castOrigin.transform.forward * hit.distance, Color.yellow);
				m_lastHitPosition = m_castOrigin.transform.position + m_castOrigin.forward * (hit.distance - m_drawingOffset);
				m_hitDistance = m_latestHit.distance;
				
				if (!m_drawing)
				{
					StartDrawing(m_lastHitPosition);
				}

				m_latestHit = hit;
			}
			else if(m_drawing)
			{
				StopDrawing();
			}
		}
		else if(m_drawing)
		{
			StopDrawing();
		}
	}

#if UNITY_EDITOR
	[Button("Start Drawing")]
	private void EditorStartDrawing()
	{
		m_holdingPen = true;
		StartDrawing(transform.position);
	}


	[Button("Stop Drawing")]
	private void EditorStopDrawing()
	{
		m_holdingPen = false;
		StopDrawing();
	}
#endif
}
