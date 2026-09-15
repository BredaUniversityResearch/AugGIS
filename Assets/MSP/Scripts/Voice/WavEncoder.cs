using System;
using UnityEngine;

/// <summary>
/// Turns recorded samples into a 16-bit PCM WAV file in memory (44-byte header plus samples),
/// which is what the voice helper's speech-to-text expects.
/// </summary>
public static class WavEncoder
{
	private const int HEADER_BYTES = 44;

	public static byte[] Encode16(float[] a_samples, int a_sampleRate, int a_channels)
	{
		if (a_samples == null)
		{
			throw new ArgumentNullException(nameof(a_samples));
		}
		if (a_sampleRate <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(a_sampleRate));
		}
		if (a_channels <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(a_channels));
		}

		int dataSize = a_samples.Length * 2;
		byte[] bytes = new byte[HEADER_BYTES + dataSize];

		WriteAscii(bytes, 0, "RIFF");
		WriteInt32(bytes, 4, 36 + dataSize);
		WriteAscii(bytes, 8, "WAVEfmt ");
		WriteInt32(bytes, 16, 16);
		WriteInt16(bytes, 20, 1);
		WriteInt16(bytes, 22, (short)a_channels);
		WriteInt32(bytes, 24, a_sampleRate);
		WriteInt32(bytes, 28, a_sampleRate * a_channels * 2);
		WriteInt16(bytes, 32, (short)(a_channels * 2));
		WriteInt16(bytes, 34, 16);
		WriteAscii(bytes, 36, "data");
		WriteInt32(bytes, 40, dataSize);

		int offset = HEADER_BYTES;
		for (int i = 0; i < a_samples.Length; i++)
		{
			float clamped = Mathf.Clamp(a_samples[i], -1f, 1f);
			short value = (short)(clamped * short.MaxValue);
			bytes[offset] = (byte)(value & 0xFF);
			bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
			offset += 2;
		}

		return bytes;
	}

	public static float PeakAmplitude(float[] a_samples)
	{
		float peak = 0f;
		for (int i = 0; i < a_samples.Length; i++)
		{
			float abs = Mathf.Abs(a_samples[i]);
			if (abs > peak)
			{
				peak = abs;
			}
		}
		return peak;
	}

	private static void WriteAscii(byte[] a_bytes, int a_offset, string a_text)
	{
		for (int i = 0; i < a_text.Length; i++)
		{
			a_bytes[a_offset + i] = (byte)a_text[i];
		}
	}

	private static void WriteInt32(byte[] a_bytes, int a_offset, int a_value)
	{
		a_bytes[a_offset] = (byte)(a_value & 0xFF);
		a_bytes[a_offset + 1] = (byte)((a_value >> 8) & 0xFF);
		a_bytes[a_offset + 2] = (byte)((a_value >> 16) & 0xFF);
		a_bytes[a_offset + 3] = (byte)((a_value >> 24) & 0xFF);
	}

	private static void WriteInt16(byte[] a_bytes, int a_offset, short a_value)
	{
		a_bytes[a_offset] = (byte)(a_value & 0xFF);
		a_bytes[a_offset + 1] = (byte)((a_value >> 8) & 0xFF);
	}
}
