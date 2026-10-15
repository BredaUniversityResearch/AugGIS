using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One reply in the voice panel's conversation list. Pressing it shows that sentence's results
/// beside the list.
/// The list hides what is scrolled out of view, but only the picture: a button's touch area is a
/// collider, which the list cannot hide. So each frame the touch area is cut to the part of the
/// bubble that can be seen, and switched off when none of it can, or a finger reaching for the
/// buttons above the list would press a reply scrolled up behind them.
/// </summary>
public class VoiceReplyBubble : MonoBehaviour
{
	[SerializeField]
	private CustomXRButton m_button;

	[SerializeField]
	private BoxCollider m_touchArea;

	[SerializeField]
	private TMP_Text m_text;

	[SerializeField]
	private Image m_selectedMarker;

	public event Action<int> Pressed;

	private int m_turnIndex;
	private RectTransform m_bubbleRect;
	private RectTransform m_viewport;
	private readonly Vector3[] m_viewportCorners = new Vector3[4];

	private void Awake()
	{
		m_bubbleRect = m_button.GetComponent<RectTransform>();
		m_button.OnPress.AddListener(OnButtonPressed);
	}

	public void Setup(int a_turnIndex, string a_text, RectTransform a_viewport)
	{
		m_turnIndex = a_turnIndex;
		m_text.text = a_text;
		m_viewport = a_viewport;
		SetSelected(false);
	}

	public void SetSelected(bool a_selected)
	{
		m_selectedMarker.enabled = a_selected;
	}

	private void LateUpdate()
	{
		ClipTouchAreaToViewport();
	}

	private void ClipTouchAreaToViewport()
	{
		// The viewport's bottom-left and top-right corners, in the bubble's own space.
		m_viewport.GetWorldCorners(m_viewportCorners);
		Vector3 viewportBottom = m_bubbleRect.InverseTransformPoint(m_viewportCorners[0]);
		Vector3 viewportTop = m_bubbleRect.InverseTransformPoint(m_viewportCorners[2]);

		Rect bubble = m_bubbleRect.rect;
		float visibleBottom = Mathf.Max(bubble.yMin, viewportBottom.y);
		float visibleTop = Mathf.Min(bubble.yMax, viewportTop.y);
		bool isVisible = visibleTop > visibleBottom;

		if (m_button.isInteractable != isVisible)
		{
			m_button.isInteractable = isVisible;
		}
		if (!isVisible)
		{
			return;
		}

		float visibleHeight = visibleTop - visibleBottom;
		float visibleMiddle = (visibleTop + visibleBottom) / 2f;
		Vector3 center = new Vector3(bubble.center.x, visibleMiddle, 0f);
		Vector3 size = new Vector3(bubble.width, visibleHeight, m_touchArea.size.z);
		// Changing a collider makes the physics engine rebuild it, so it is only changed when the list has moved.
		bool hasMoved = m_touchArea.center != center || m_touchArea.size != size;
		if (hasMoved)
		{
			m_touchArea.center = center;
			m_touchArea.size = size;
		}
	}

	private void OnButtonPressed()
	{
		Pressed?.Invoke(m_turnIndex);
	}
}
