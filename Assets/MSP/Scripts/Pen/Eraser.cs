using UnityEngine;

public class Eraser : MonoBehaviour
{
	void OnCollisionEnter(Collision collision)
	{
		PenLine penLine = collision.gameObject.GetComponent<PenLine>();

		if (penLine != null)
		{
			penLine.EraseLine();
		}
	}
}
