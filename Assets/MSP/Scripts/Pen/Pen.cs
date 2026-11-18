using UnityEngine;
using System;
using System.Collections.Generic;
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
	[Tooltip("World-space metres. Raw samples closer than this to the previous sample are ignored.")]
	private float m_minSampleDistance = 0.003f;

	[SerializeField]
	[Tooltip("World-space metres. Max deviation from a straight segment before a new point is committed.")]
	private float m_straightTolerance = 0.002f;

	[SerializeField]
	[Tooltip("World-space metres. Straight segments longer than this get a new point anyway. 0 disables.")]
	private float m_maxSegmentLength = 0.15f;

	[SerializeField]
	[Range(0f, 0.95f)]
	[Tooltip("Exponential smoothing of the pen tip position (per 60 Hz frame). 0 disables.")]
	private float m_smoothing = 0f;

	[Header("Simplification")]
	[SerializeField]
	[Tooltip("Run Douglas-Peucker on the whole line when the stroke ends.")]
	private bool m_simplifyOnFinish = true;

	[SerializeField]
	[ShowIf(nameof(m_simplifyOnFinish))]
	[Tooltip("World-space metres. Douglas-Peucker tolerance.")]
	private float m_simplifyTolerance = 0.0015f;

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

	private readonly PenStrokeBuilder m_strokeBuilder = new PenStrokeBuilder();
	private PenLine m_strokeBuilderLine = null;
	private Vector3 m_smoothedHitPosition;
	private readonly List<Vector3> m_pointBuffer = new List<Vector3>();

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

		if (m_smoothing > 0f)
		{
			float t = 1f - Mathf.Pow(m_smoothing, Time.deltaTime * 60f);
			m_smoothedHitPosition = Vector3.Lerp(m_smoothedHitPosition, m_lastHitPosition, t);
		}
		else
		{
			m_smoothedHitPosition = m_lastHitPosition;
		}

		if (m_currentLine == null)
			return;

		// The networked line arrives a few frames after StartDrawing, so start the builder lazily.
		if (m_strokeBuilderLine != m_currentLine)
		{
			m_strokeBuilderLine = m_currentLine;
			m_strokeBuilder.Begin(m_currentLine.GetLastLocalPoint());
		}

		Vector3 localSample = m_currentLine.GlobalToLocal(m_smoothedHitPosition);
		PenStrokeBuilder.Result result = m_strokeBuilder.AddSample(localSample,
			m_currentLine.GlobalToLocalDistance(m_minSampleDistance),
			m_currentLine.GlobalToLocalDistance(m_straightTolerance),
			m_currentLine.GlobalToLocalDistance(m_maxSegmentLength));

		switch (result)
		{
			case PenStrokeBuilder.Result.AddedPoint:
				m_currentLine.AddLocalPoint(localSample);
				break;
			case PenStrokeBuilder.Result.UpdatedLastPoint:
				m_currentLine.UpdateLastLocalPoint(localSample);
				break;
		}
	}

	void SimplifyCurrentLine()
	{
		m_currentLine.GetLocalPoints(m_pointBuffer);
		if (m_pointBuffer.Count <= 2)
			return;

		List<Vector3> simplified = LineSimplification.DouglasPeucker(m_pointBuffer, m_currentLine.GlobalToLocalDistance(m_simplifyTolerance));
		if (simplified.Count < m_pointBuffer.Count)
		{
			m_currentLine.ReplaceLocalPoints(simplified);
		}
	}

	void StartDrawing(Vector3 a_hitPosition)
	{
		m_currentLine = null;
		m_strokeBuilderLine = null;
		m_smoothedHitPosition = a_hitPosition;

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

		if (m_simplifyOnFinish)
		{
			SimplifyCurrentLine();
		}

		m_currentLine.FinishLine();
		m_currentLine = null;
		m_strokeBuilderLine = null;
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
				m_hitDistance = hit.distance;
				
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
