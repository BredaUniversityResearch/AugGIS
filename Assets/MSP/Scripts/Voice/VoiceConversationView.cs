using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The two columns of the voice panel. On the left, every sentence of the conversation as a
/// scrolling list of bubbles: what you said, and under it the reply with what changed on the map.
/// On the right, the results of one turn: the searched words, the layers found per map server,
/// and the timings. A new answer is selected and scrolled to; pressing an older reply selects it.
/// </summary>
public class VoiceConversationView : MonoBehaviour
{
	// How far one press of the arrow buttons moves the list, as a share of what is visible.
	private const float SCROLL_STEP = 0.8f;

	[SerializeField]
	private ScrollRect m_scrollRect;

	[SerializeField]
	private CustomXRButton m_scrollUpButton;

	[SerializeField]
	private CustomXRButton m_scrollDownButton;

	[SerializeField]
	private GameObject m_userBubblePrefab;

	[SerializeField]
	private GameObject m_thinkingBubblePrefab;

	[SerializeField]
	private VoiceReplyBubble m_replyBubblePrefab;

	// The same reply in the team's red cancel-button colours, for a sentence that failed.
	[SerializeField]
	private VoiceReplyBubble m_failedReplyBubblePrefab;

	[SerializeField]
	private TMP_Text m_resultsHeadingText;

	[SerializeField]
	private TMP_Text m_geoServerText;

	[SerializeField]
	private TMP_Text m_emodnetText;

	[SerializeField]
	private TMP_Text m_timingText;

	private List<VoiceTurn> m_turns = new List<VoiceTurn>();
	private List<VoiceReplyBubble> m_replyBubbles = new List<VoiceReplyBubble>();
	private int m_selectedIndex = -1;

	// The sentence being recorded or waiting for its answer, shown below the finished turns.
	private GameObject m_pendingTurn;
	private TMP_Text m_pendingWordsText;
	private TMP_Text m_pendingReplyText;

	private void Awake()
	{
		m_scrollUpButton.OnPress.AddListener(OnScrollUpPressed);
		m_scrollDownButton.OnPress.AddListener(OnScrollDownPressed);
		ShowResults(-1);
	}

	/// <summary>Adds the bubble for a sentence that is being recorded, below the finished turns.</summary>
	public void BeginPendingTurn(string a_words)
	{
		DropPendingTurn();
		m_pendingTurn = AddTurnRow("Turn in progress");
		m_pendingWordsText = AddTextBubble(m_userBubblePrefab, m_pendingTurn.transform, a_words);
		ScrollToBottom();
	}

	public void ShowPendingWords(string a_words)
	{
		if (m_pendingTurn == null)
		{
			return;
		}
		m_pendingWordsText.text = a_words;
	}

	/// <summary>Shows a waiting line, such as "Thinking 3s", where the reply will come.</summary>
	public void ShowPendingReply(string a_text)
	{
		if (m_pendingTurn == null)
		{
			return;
		}
		if (m_pendingReplyText == null)
		{
			m_pendingReplyText = AddTextBubble(m_thinkingBubblePrefab, m_pendingTurn.transform, a_text);
			ScrollToBottom();
			return;
		}
		m_pendingReplyText.text = a_text;
	}

	public void DropPendingTurn()
	{
		if (m_pendingTurn == null)
		{
			return;
		}
		// Destroy only happens at the end of the frame. Switched off, the row already stops counting
		// for the list's height, so a finished turn added right after scrolls to the right bottom.
		m_pendingTurn.SetActive(false);
		Destroy(m_pendingTurn);
		m_pendingTurn = null;
		m_pendingWordsText = null;
		m_pendingReplyText = null;
	}

	/// <summary>Replaces the sentence in progress with its finished turn, selects it and scrolls to it.</summary>
	public void FinishPendingTurn(VoiceTurn a_turn)
	{
		DropPendingTurn();
		AddTurn(a_turn);
	}

	public void AddTurn(VoiceTurn a_turn)
	{
		int index = m_turns.Count;
		m_turns.Add(a_turn);

		GameObject row = AddTurnRow($"Turn {index + 1}");
		// Nothing heard has no words to show, only the reply saying so.
		if (!a_turn.NothingHeard)
		{
			AddTextBubble(m_userBubblePrefab, row.transform, DescribeWords(a_turn));
		}

		VoiceReplyBubble replyPrefab = m_replyBubblePrefab;
		if (a_turn.HasFailed)
		{
			replyPrefab = m_failedReplyBubblePrefab;
		}
		VoiceReplyBubble reply = Instantiate(replyPrefab, row.transform);
		reply.Setup(index, DescribeReply(a_turn), m_scrollRect.viewport);
		reply.Pressed += SelectTurn;
		m_replyBubbles.Add(reply);

		SelectTurn(index);
		ScrollToBottom();
	}

	/// <summary>
	/// Removes every finished turn and empties the results column. A sentence still being recorded
	/// stays, because it will be sent as the first sentence of the new conversation.
	/// </summary>
	public void Clear()
	{
		foreach (Transform turnRow in m_scrollRect.content)
		{
			bool isPending = turnRow.gameObject == m_pendingTurn;
			if (!isPending)
			{
				Destroy(turnRow.gameObject);
			}
		}
		m_turns.Clear();
		m_replyBubbles.Clear();
		ShowResults(-1);
	}

	public void SelectTurn(int a_index)
	{
		if (a_index >= m_replyBubbles.Count)
		{
			// A press that landed in the same frame as New conversation, on a bubble that is about to go.
			return;
		}
		if (m_selectedIndex >= 0 && m_selectedIndex < m_replyBubbles.Count)
		{
			m_replyBubbles[m_selectedIndex].SetSelected(false);
		}
		m_replyBubbles[a_index].SetSelected(true);
		ShowResults(a_index);
	}

	// -1 shows the empty column, for when there is no turn yet.
	private void ShowResults(int a_index)
	{
		m_selectedIndex = a_index;

		if (a_index < 0)
		{
			ShowNoResults("<b>Results</b>");
			return;
		}

		VoiceTurn turn = m_turns[a_index];
		string heading = $"<b>Results for turn {a_index + 1}</b>";
		if (turn.HasFailed)
		{
			// Without this the empty boxes would read as a search that found nothing.
			ShowNoResults($"{heading}\nThe sentence failed, so nothing was searched.");
			return;
		}

		VoiceResponse response = turn.response;
		m_resultsHeadingText.text = $"{heading}{DescribeSearches(response.searches)}";
		m_geoServerText.text = DescribeFound("Team GeoServer", response.picks.geoserver);
		m_emodnetText.text = DescribeFound("EMODnet", response.picks.emodnet);
		m_timingText.text = DescribeTimings(response);
	}

	private void ShowNoResults(string a_heading)
	{
		m_resultsHeadingText.text = a_heading;
		m_geoServerText.text = DescribeFound("Team GeoServer", new List<VoiceFoundLayer>());
		m_emodnetText.text = DescribeFound("EMODnet", new List<VoiceFoundLayer>());
		m_timingText.text = "";
	}

	private GameObject AddTurnRow(string a_name)
	{
		GameObject row = new GameObject(a_name, typeof(RectTransform), typeof(VerticalLayoutGroup));
		row.transform.SetParent(m_scrollRect.content, false);
		row.layer = m_scrollRect.content.gameObject.layer;
		VerticalLayoutGroup layout = row.GetComponent<VerticalLayoutGroup>();
		layout.spacing = 4f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		return row;
	}

	private static TMP_Text AddTextBubble(GameObject a_prefab, Transform a_parent, string a_text)
	{
		GameObject bubble = Instantiate(a_prefab, a_parent);
		TMP_Text text = bubble.GetComponentInChildren<TMP_Text>();
		text.text = a_text;
		return text;
	}

	private void ScrollToBottom()
	{
		// The list's height is worked out at the end of the frame, so it is worked out now, or the
		// scroll would stop at the old bottom, one turn short.
		LayoutRebuilder.ForceRebuildLayoutImmediate(m_scrollRect.content);
		m_scrollRect.StopMovement();
		m_scrollRect.verticalNormalizedPosition = 0f;
	}

	private void OnScrollUpPressed()
	{
		ScrollBy(1f);
	}

	private void OnScrollDownPressed()
	{
		ScrollBy(-1f);
	}

	// 1 moves towards the first turn, -1 towards the newest.
	private void ScrollBy(float a_direction)
	{
		float visibleHeight = m_scrollRect.viewport.rect.height;
		float hiddenHeight = m_scrollRect.content.rect.height - visibleHeight;
		if (hiddenHeight <= 0f)
		{
			return;
		}

		float step = SCROLL_STEP * visibleHeight / hiddenHeight;
		float position = m_scrollRect.verticalNormalizedPosition + a_direction * step;
		m_scrollRect.StopMovement();
		m_scrollRect.verticalNormalizedPosition = Mathf.Clamp01(position);
	}

	private static string DescribeWords(VoiceTurn a_turn)
	{
		if (a_turn.transcript == null)
		{
			return "<i>nothing was heard</i>";
		}
		return $"<noparse>{a_turn.transcript}</noparse>";
	}

	private static string DescribeReply(VoiceTurn a_turn)
	{
		if (a_turn.HasFailed)
		{
			return $"<b>Failed</b>\n<noparse>{a_turn.error}</noparse>";
		}
		if (a_turn.NothingHeard)
		{
			return "<i>nothing was heard</i>";
		}

		StringBuilder sb = new StringBuilder();
		if (!string.IsNullOrWhiteSpace(a_turn.response.reply))
		{
			sb.Append($"<noparse>{a_turn.response.reply}</noparse>\n");
		}
		sb.Append($"<size=85%>On the map: {a_turn.applied}</size>");
		return sb.ToString();
	}

	// The words each search of this sentence used, so a strange result can be traced to what was searched.
	private static string DescribeSearches(List<VoiceSearch> a_searches)
	{
		if (a_searches.Count == 0)
		{
			return "";
		}

		List<string> queries = new List<string>();
		foreach (VoiceSearch search in a_searches)
		{
			queries.Add(search.query);
		}
		string joined = string.Join(", ", queries);
		return $"\nSearched: <noparse>{joined}</noparse>";
	}

	// A heading with the count, then one layer per line: its score, its name, and the sea or theme it belongs to.
	private static string DescribeFound(string a_server, List<VoiceFoundLayer> a_layers)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append($"<b>{a_server} ({a_layers.Count})</b>");
		foreach (VoiceFoundLayer layer in a_layers)
		{
			// noparse: a "<" in a layer's name must show as text and not be read as a tag.
			sb.Append($"\n{layer.meaning:F2}  <noparse>{layer.spoken}</noparse> <size=80%><noparse>{layer.group}</noparse></size>");
		}
		return sb.ToString();
	}

	private static string DescribeTimings(VoiceResponse a_response)
	{
		if (a_response.timings_ms == null)
		{
			return "";
		}
		float speech = a_response.timings_ms.stt / 1000f;
		float model = a_response.timings_ms.llm / 1000f;
		float total = a_response.timings_ms.total / 1000f;
		return $"speech {speech:F1}s, model {model:F1}s, total {total:F1}s";
	}
}
