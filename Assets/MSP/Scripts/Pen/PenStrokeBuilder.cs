using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides, per raw pen sample, whether the stroke needs a new point or whether the
/// last ("live") point can simply be moved because the stroke is still straight.
/// All values are in the line's local space.
/// </summary>
public class PenStrokeBuilder
{
	public enum Result
	{
		Skipped,
		UpdatedLastPoint,
		AddedPoint
	}

	private Vector3 m_anchor;
	private Vector3 m_lastRawSample;
	private bool m_hasLivePoint;
	private readonly List<Vector3> m_segmentSamples = new List<Vector3>();

	public void Begin(Vector3 a_localStart)
	{
		m_anchor = a_localStart;
		m_lastRawSample = a_localStart;
		m_hasLivePoint = false;
		m_segmentSamples.Clear();
	}

	/// <param name="a_minSampleDistance">Samples closer than this to the previous accepted sample are ignored.</param>
	/// <param name="a_straightTolerance">Max deviation of the samples in the current segment from the straight chord.</param>
	/// <param name="a_maxSegmentLength">Segments longer than this are committed even when straight. 0 or less disables the cap.</param>
	public Result AddSample(Vector3 a_sample, float a_minSampleDistance, float a_straightTolerance, float a_maxSegmentLength)
	{
		if ((a_sample - m_lastRawSample).sqrMagnitude < a_minSampleDistance * a_minSampleDistance)
			return Result.Skipped;

		m_lastRawSample = a_sample;

		if (!m_hasLivePoint)
		{
			StartSegment(a_sample);
			return Result.AddedPoint;
		}

		if (IsStraightSegment(a_sample, a_straightTolerance, a_maxSegmentLength))
		{
			m_segmentSamples.Add(a_sample);
			return Result.UpdatedLastPoint;
		}

		// Commit the current live point (the previous sample) and start a new segment from it.
		m_anchor = m_segmentSamples[m_segmentSamples.Count - 1];
		StartSegment(a_sample);
		return Result.AddedPoint;
	}

	private void StartSegment(Vector3 a_sample)
	{
		m_hasLivePoint = true;
		m_segmentSamples.Clear();
		m_segmentSamples.Add(a_sample);
	}

	private bool IsStraightSegment(Vector3 a_newEnd, float a_straightTolerance, float a_maxSegmentLength)
	{
		if (a_maxSegmentLength > 0f && (a_newEnd - m_anchor).sqrMagnitude > a_maxSegmentLength * a_maxSegmentLength)
			return false;

		// Check every sample absorbed into this segment, not only the last one, so slow arcs
		// do not get flattened into one long chord.
		for (int i = 0; i < m_segmentSamples.Count; i++)
		{
			if (LineSimplification.PerpendicularDistance(m_segmentSamples[i], m_anchor, a_newEnd) > a_straightTolerance)
				return false;
		}
		return true;
	}
}
