using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using UnityEngine;

public class ArUcoMarker : AMarker
{
	[SerializeField]
	private TextMeshProUGUI m_idDisplayText;

	private int m_markerID = -1;
	public int markerID => m_markerID;

	public override void UpdateMarker(Vector3 a_targetPosition, Quaternion a_targetRotation, Vector3 a_targetScale, string a_decodedString)
	{
		base.UpdateMarker(a_targetPosition, a_targetRotation, a_targetScale, a_decodedString);

		if (m_idDisplayText != null)
		{
			m_idDisplayText.text = a_decodedString;
		}

		if (int.TryParse(a_decodedString, out int markerID))
		{
			m_markerID = markerID;
		}
	}
}
