using UnityEngine;
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.Mathematics;

public class Pen : MonoBehaviour
{
	public event Action<Color> ColorChangedEvent;
	public event Action<float> WidthChangedEvent;
	public event Action<Vector3> ExternalLineSpawnerEvent;

	[Header("General")]
	[SerializeField] private GameObject[] m_coloredObjects;
	[SerializeField] private LayerMask m_drawingLayerMask;
	[SerializeField] [Required] private XRGrabInteractable m_xrGrabInteractable;
	[SerializeField] [Required] private Transform m_castOrigin;
	[SerializeField] private float m_castDistance = 0.08f;
	[SerializeField] private Color m_penColor = Color.black;
	public Color PenColor => m_penColor;

	[SerializeField] private float m_penWidth = 0.01f;
	public float PenWidth => m_penWidth;

	[Header("Feel")]
	[SerializeField] private float m_drawingOffset = 0.1f;	
	[SerializeField] [Required] private Transform m_tipPivotTransform;	
	[SerializeField] private float m_minTipLength = 0.1f;
	[SerializeField] private float m_maxTipLength = 0.75f;	
	[SerializeField] private float m_restTipLength = 0.5f;	
	[SerializeField] private float m_tipCastOffset = 0.75f;

	[Header("Line")]
	[SerializeField] [Required] private PenLine m_linePrefab;
	public PenLine LinePrefab => m_linePrefab;

	[SerializeField] [Required] private Eraser m_eraser;
	[SerializeField] private float m_minSampleDistance = 0.003f; //World-space metres. Raw samples closer than this to the previous sample are ignored.
	[SerializeField] private float m_straightTolerance = 0.002f; //World-space metres. Max deviation from a straight segment before a new point is committed.
	[SerializeField] private float m_maxSegmentLength = 0.15f; //World-space metres. Straight segments longer than this get a new point anyway. 0 disables.
	[SerializeField] [Range(0f, 0.95f)] private float m_smoothing = 0f; //Exponential smoothing of the pen tip position (per 60 Hz frame). 0 disables.

	[Header("Simplification")]
	[SerializeField] private bool m_simplifyOnFinish = true; //Run Douglas-Peucker on the whole line when the stroke ends.
	[SerializeField] [ShowIf(nameof(m_simplifyOnFinish))] private float m_simplifyTolerance = 0.0015f; //World-space metres. Douglas-Peucker tolerance.
	[SerializeField] private TransformReference m_penLineRootTransformReference;
	public TransformReference PenLineRootTransformReference => m_penLineRootTransformReference;

	[Header("Continue drawing")]
	[SerializeField] [Required] private GameObjectSet m_penLineSet; //Set containing all PenLines (same asset as the PenLine prefab's set).
	[SerializeField] private float m_continueDistance = 0.02f; //World-space metres. Starting a stroke this close to the start/end of an open line continues that line.
	public float ContinueDistance => m_continueDistance;

	[Header("Debug")] 
	[SerializeField] [ReadOnly] private bool m_drawing = false;
	[SerializeField] [ReadOnly] private PenLine m_currentLine = null;
	[SerializeField] [ReadOnly] private float m_hitDistance;
	[SerializeField] [ReadOnly] private Vector3 m_lastHitPosition;
	[SerializeField] [ReadOnly] private bool m_holdingPen = false;

	private readonly PenStrokeBuilder m_strokeBuilder = new PenStrokeBuilder();
	private PenLine m_strokeBuilderLine = null;
	private Vector3 m_smoothedHitPosition;
	private readonly List<Vector3> m_pointBuffer = new List<Vector3>();

	//Line this pen asked to continue, waiting for the answer
	private PenLine m_pendingContinueLine = null;
	private Vector3 m_pendingContinueStartPosition;

	private static readonly List<Pen> ms_heldPens = new List<Pen>();


	public static IReadOnlyList<Pen> HeldPens => ms_heldPens; //Pens that are currently held by a local user.
	public Vector3 TipPosition => m_castOrigin.position; //World position of the pen tip (the drawing cast origin).
	public bool IsDrawing => m_drawing;

	private void Start()
	{
		m_xrGrabInteractable.selectEntered.AddListener(StartHoldingPen);
		m_xrGrabInteractable.selectExited.AddListener(EndHoldingPen);

		SetColor(m_penColor);
		SetWidth(m_penWidth);
	}

	private void OnDisable()
	{
		ms_heldPens.Remove(this);
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
		if (!ms_heldPens.Contains(this))
		{
			ms_heldPens.Add(this);
		}
	}

	private void EndHoldingPen(SelectExitEventArgs a_args)
	{
		m_holdingPen = false;
		m_eraser.enabled = false;
		ms_heldPens.Remove(this);
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

	public void SetCurrentLine(PenLine a_newLine)
	{
		m_currentLine = a_newLine;
	}

	void UpdateDrawing()
	{
		if (m_drawing == false)
			return;

		//Update tip size
		float tipLength = Mathf.Clamp(m_hitDistance-m_tipCastOffset, m_minTipLength, m_maxTipLength);
		m_tipPivotTransform.localScale = new Vector3(m_penWidth, tipLength, m_penWidth);

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

		if (TryFindContinueTarget(a_hitPosition, out PenLine continueLine, out bool fromStart))
		{
			m_pendingContinueLine = continueLine;
			m_pendingContinueStartPosition = a_hitPosition;
			continueLine.RequestContinue(fromStart, a_granted => OnContinueResult(continueLine, a_granted));
			return;
		}

		SpawnNewLine(a_hitPosition);
	}

	public bool TryFindContinueTarget(Vector3 a_worldPosition, out PenLine a_line, out bool a_fromStart)
	{
		// Finds the open line whose start/end point is closest to a_worldPosition, within the continue distance.
		a_line = null;
		a_fromStart = false;
		if (m_penLineSet == null)
			return false;

		float bestDistance = float.MaxValue;
		foreach (GameObject lineObject in m_penLineSet)
		{
			if (lineObject == null)
				continue;

			PenLine line = lineObject.GetComponent<PenLine>();
			if (line == null || !line.CanContinue)
				continue;

			if (line.TryGetContinueEndpoint(a_worldPosition, m_continueDistance, out bool fromStart, out float distance) && distance < bestDistance)
			{
				bestDistance = distance;
				a_line = line;
				a_fromStart = fromStart;
			}
		}
		return a_line != null;
	}

	private void OnContinueResult(PenLine a_line, bool a_granted)
	{
		bool stillWaiting = m_pendingContinueLine == a_line && m_pendingContinueLine != null;
		if (stillWaiting)
		{
			m_pendingContinueLine = null;
		}

		if (!a_granted)
		{
			//Someone else got the line first, draw a new one instead
			if (stillWaiting && m_drawing)
			{
				SpawnNewLine(m_pendingContinueStartPosition);
			}
			return;
		}

		if (a_line == null)
			return;

		if (stillWaiting && m_drawing)
		{
			SetCurrentLine(a_line);
		}
		else
		{
			//The stroke already ended (or a new one started), release the line again
			a_line.SetClosedWithoutNotify(a_line.EvaluateClosed());
			a_line.FinishLine();
		}
	}

	void SpawnNewLine(Vector3 a_hitPosition)
	{
		if (ExternalLineSpawnerEvent == null)
		{
			m_currentLine = Instantiate<PenLine>(m_linePrefab, this.transform.parent);
			m_currentLine.transform.position = a_hitPosition;
			m_currentLine.transform.rotation = quaternion.Euler(90f,0f,0f);
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
		m_pendingContinueLine = null;

		//Reset tip
		m_tipPivotTransform.localScale = new Vector3(m_penWidth, m_restTipLength, m_penWidth);

		if (m_currentLine == null)
		{
			return;
		}

		if (m_simplifyOnFinish)
		{
			SimplifyCurrentLine();
		}

		//A stroke that barely moved becomes a single-point line (a dot)
		if (m_currentLine.PointCount > 1 && m_currentLine.IsCollapsed())
		{
			m_currentLine.ReplaceLocalPoints(new[] { m_currentLine.GetLocalCentroid() });
		}

		//Close the line if the stroke ended near its start (networked lines get this confirmed by the server on finish)
		m_currentLine.SetClosedWithoutNotify(m_currentLine.EvaluateClosed());
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
				// Debug.DrawRay(m_castOrigin.position, m_castOrigin.transform.forward * hit.distance, Color.yellow);
				m_lastHitPosition = m_castOrigin.transform.position + m_castOrigin.forward * (hit.distance - m_drawingOffset);
				m_hitDistance = hit.distance;
				
				if (!m_drawing)
				{
					StartDrawing(m_lastHitPosition);
				}
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
