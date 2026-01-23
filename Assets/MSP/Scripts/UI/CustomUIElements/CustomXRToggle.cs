using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR;
using System.Collections;
using static ICustomXRUIVisualHelper;
using static ICustomXRUIAudio;
using Sirenix.OdinInspector;



public class CustomXRToggle : MonoBehaviour, ICustomXRUIInteractions, ICustomXRUIVisualHelper, ICustomXRUIAudio
{
    private Coroutine togglePressRoutine;

    [SerializeField]
    private float m_holdDuration = 0.25f; // Duration to hold the toggle

    public UnityEvent OnPressOn;
    public UnityEvent OnPressOff;

    private bool m_selected = false;
    public bool IsSelected { get { return m_selected; } set { SetSelected(value); } }

    private bool m_hovered = false;

    private bool m_initialized = false;

    [SerializeField]
    private bool m_interactable = true;
    public bool isInteractable
    {
        get { return m_interactable; }
        set { SetInteractability(value); }
    }

    private CustomState m_currentState;
    CustomState ICustomXRUIVisualHelper.state => m_currentState;

    private StateChangeCallback stateChange;
    StateChangeCallback ICustomXRUIVisualHelper.stateChangeCallback { get => stateChange; set => stateChange = value; }

    private UpdateProgressBarCallback updateProgress;
    UpdateProgressBarCallback ICustomXRUIVisualHelper.updateProgressBarCallback { get => updateProgress; set => updateProgress = value; }

    private PlaySound playSound;
    PlaySound ICustomXRUIAudio.playSound { get => playSound; set => playSound = value; }

    void Start()
    {
#if UNITY_EDITOR
        // Add mouse pointer support in editor for testing.
        if (XRSettings.loadedDeviceName == "")
            gameObject.AddComponent<PointerImplCustomXRUI>();
#endif

        m_initialized = true;
        SetInteractability(m_interactable); // Set the initial interactability state.
        
        if (m_autoResizeCollider)
            StartCoroutine(ColliderResizeRoutine());
    }

    private void SetInteractability(bool state)
    {
        GetComponent<XRSimpleInteractable>().enabled = state;
        m_interactable = state;

        if (!m_initialized) return; // Don't try to update state if not initialized yet. The actual initial update will happen in Start().

        if (stateChange == null)
        {
            Debug.LogWarning("No stateChangeCallback assigned to " + gameObject.name);
            return;
        }

        if (m_interactable)
            UpdateState(m_selected ? CustomState.Selected : CustomState.Deselected);
        else
            UpdateState(CustomState.Disabled);
    }

    private void SetSelected(bool state)
    {
        m_selected = state;

        if (!m_initialized) return; // Don't try to update state if not initialized yet. The actual update will happen in Start().

        if (m_interactable)
            UpdateState(m_selected ? CustomState.Selected : CustomState.Deselected);
    }

    public void ElementHoverEnter()
    {
        m_hovered = true;
        if (!m_interactable)
            return;

        if (m_currentState == CustomState.Pressed)  // Sometimes, hover checks are triggered after press started.
            return;

        UpdateState(CustomState.Hovered);
    }
    public void ElementHoverExit()
    {
        m_hovered = false;
        if (!m_interactable)
            return;

        UpdateState(m_selected ? CustomState.Selected : CustomState.Deselected);
    }
    public void ElementPressed()
    {
        if (!m_interactable)
            return;

        togglePressRoutine = StartCoroutine(PressRoutine());

        UpdateState(CustomState.Pressed);
    }
    public void ElementReleased()
    {
        if (!m_interactable)
            return;

        if (togglePressRoutine != null)
        {
            StopCoroutine(togglePressRoutine);
        }

        if (m_hovered)
            UpdateState(CustomState.Hovered);
        else
            UpdateState(m_selected ? CustomState.Selected : CustomState.Deselected);
    }

    private void PressRoutineComplete()
    {
        playSound?.Invoke(SoundType.EndPress);
        if (m_selected)
        {
            m_selected = false;
            OnPressOff?.Invoke();
        }
        else
        {
            m_selected = true;
            OnPressOn?.Invoke();
        }
    }

    private IEnumerator PressRoutine()
    {
        float elapsed = 0f;
        playSound?.Invoke(SoundType.BeginPress);

        if (m_selected)
        {
            while (elapsed < m_holdDuration)
            {
                elapsed += Time.deltaTime;
                updateProgress(Mathf.Clamp01(1f - elapsed / m_holdDuration)); // Update progress bar to reflect progress.
                yield return null;
            }
            PressRoutineComplete();

            UpdateState(CustomState.Deselected);
        }
        else
        {
            while (elapsed < m_holdDuration)
            {
                elapsed += Time.deltaTime;
                updateProgress(Mathf.Clamp01(elapsed / m_holdDuration)); // Update progress bar to reflect progress.
                yield return null;
            }
            PressRoutineComplete();

            UpdateState(CustomState.Selected);
        }

        if (m_hovered)
            UpdateState(CustomState.Hovered);
    }

    private void UpdateState(CustomState newState)
    {
        if (stateChange == null)
        {
            Debug.LogError("No stateChangeCallback assigned to " + gameObject.name + " tried to set state to " + newState.ToString(), this);
            return;
        }
        stateChange(newState);

        ResetProgressBar();

        m_currentState = newState;
    }

    void ResetProgressBar()
    {
        updateProgress(m_selected ? 1f : 0f);
    }


    [Space]
    [SerializeField]
    private float m_colliderDepth = 100f;

    [SerializeField]
    [LabelText("Auto resize collider on start")]
    private bool m_autoResizeCollider = true;

    [Button("Reset collider size")]
    [DisableIf("@this.m_autoResizeCollider")]
    private void ResetCollider()
    {
        BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            Debug.LogError("No BoxCollider found on " + gameObject.name);
            return;
        }

        float width = gameObject.GetComponent<RectTransform>().rect.width;
        float height = gameObject.GetComponent<RectTransform>().rect.height;
        boxCollider.size = new Vector3(width, height, m_colliderDepth);
    }
    
    private IEnumerator ColliderResizeRoutine()
    {
        yield return new WaitForEndOfFrame(); // Wait one frame for RectTransform to update.
        ResetCollider();
    }
}
