using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;

namespace POV_Unity
{
	public class FacingAnchor : NetworkBehaviour
	{
		[SerializeField]
		[Required]
		private XRKnob m_xrKnob;

		[SerializeField]
		[Required]
		private Transform m_transformToRotate;

		private void Awake()
		{
			m_xrKnob.onValueChange.AddListener(OnValueChanged);
		}

		private void OnEnable()
		{
			if(IsServer)
			{
				float knobValue = (m_xrKnob.transform.localEulerAngles.y - m_xrKnob.minAngle) / (m_xrKnob.maxAngle - m_xrKnob.minAngle);
				m_xrKnob.value = knobValue;
			}
		}

		private void OnValueChanged(float a_newValue)
		{
			m_transformToRotate.transform.localRotation = Quaternion.Euler(0,m_xrKnob.ValueToRotation(),0);
		}
	}
}
