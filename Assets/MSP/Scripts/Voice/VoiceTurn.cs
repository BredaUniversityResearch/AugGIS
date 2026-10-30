/// <summary>
/// One spoken sentence on the voice panel and what came back for it: the answer, or the error
/// that stopped it. The panel keeps one per sentence until New conversation.
/// </summary>
public class VoiceTurn
{
	// Null when no words came back, either because nothing was heard or because the request failed first.
	public string transcript;

	// Null when the sentence failed.
	public VoiceResponse response;

	// What VoiceLayerCommands.Apply did. Null when nothing was heard, because nothing was applied then.
	public string applied;

	// Null unless the sentence failed.
	public string error;

	public static VoiceTurn Answered(VoiceResponse a_response, string a_applied)
	{
		VoiceTurn turn = new VoiceTurn();
		turn.response = a_response;
		turn.applied = a_applied;
		if (!string.IsNullOrWhiteSpace(a_response.transcript))
		{
			turn.transcript = a_response.transcript;
		}
		return turn;
	}

	/// <param name="a_transcript">The words, when the interface sent them back before it failed, otherwise null.</param>
	public static VoiceTurn Failed(string a_transcript, string a_error)
	{
		VoiceTurn turn = new VoiceTurn();
		turn.transcript = a_transcript;
		turn.error = a_error;
		return turn;
	}

	public bool HasFailed
	{
		get { return error != null; }
	}

	public bool HasRefusedDownload
	{
		get
		{
			if (response == null)
			{
				return false;
			}
			foreach (VoiceDownload download in response.downloads)
			{
				if (!download.ok)
				{
					return true;
				}
			}
			return false;
		}
	}

	public bool NothingHeard
	{
		get
		{
			bool answered = response != null;
			return answered && transcript == null;
		}
	}
}
