using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR;
using System.Collections;
using static ICustomXRUIVisualHelper;
using static ICustomXRUIAudio;
using Sirenix.OdinInspector;



public class CustomXRButton : MonoBehaviour, ICustomXRUIInteractions, ICustomXRUIVisualHelper, ICustomXRUIAudio
{
    private Coroutine buttonPressRoutine;
    private Coroutine buttonCooldownRoutine;
    private Coroutine colliderResizeRoutine;

    [SerializeField]
    private float m_holdDuration = 0.25f; // Duration to hold the button to activate it.

    public UnityEvent OnPress;

    private bool m_hovered = false;

    private bool m_onCooldown = false;

    [SerializeField]
    private float m_cooldown = 0.75f; // In seconds
    private float m_cooldownTimeLeft;

    private bool m_initialized = false;

    [SerializeField]
    private bool m_interactable = false;

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

        m_cooldownTimeLeft = m_cooldown;

        if (m_autoResizeCollider)
            StartCoroutine(ColliderResizeRoutine());
    }

    void OnEnable()
    {
        if (m_onCooldown)
            buttonCooldownRoutine = StartCoroutine(ButtonCooldownRoutine());
    }

    private void SetInteractability(bool state)
    {
        GetComponent<XRSimpleInteractable>().enabled = state;
        m_interactable = state;

        if (!m_initialized) return; // Don't try to update state if not initialized yet. The actual update will happen in Start().

        if (stateChange == null)
        {
            Debug.LogWarning("No stateChangeCallback assigned to " + gameObject.name);
            return;
        }

        if (m_interactable)
            UpdateState(m_hovered ? CustomState.Hovered : CustomState.Normal);
        else
            UpdateState(CustomState.Disabled);
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

        UpdateState(CustomState.Normal);
    }
    public void ElementPressed()
    {
        if (!m_interactable)
            return;

        buttonPressRoutine = StartCoroutine(PressRoutine());

        UpdateState(CustomState.Pressed);
    }
    public void ElementReleased()
    {
        if (!m_interactable)
            return;

        if (buttonPressRoutine != null)
        {
            StopCoroutine(buttonPressRoutine);
            updateProgress(0f);
        }

        if (m_hovered)
            UpdateState(CustomState.Hovered);
        else
            UpdateState(CustomState.Normal);
    }

    private void PressRoutineComplete()
    {
        buttonCooldownRoutine = StartCoroutine(ButtonCooldownRoutine());
        playSound?.Invoke(SoundType.EndPress);
        OnPress?.Invoke();
    }

    private IEnumerator PressRoutine()
    {
        float elapsed = 0f; // Time elapsed

        while (m_onCooldown)
        {
            // Waiting until cooldown has passed
            yield return null;
        }
        
        playSound?.Invoke(SoundType.BeginPress);

        while (elapsed < m_holdDuration) // While the button is being held down do this logic
        {
            elapsed += Time.deltaTime; // Increment elapsed time
            updateProgress(Mathf.Clamp01(elapsed / m_holdDuration)); // Update progress bar to reflect progress.
            yield return null; // Wait for the next frame
        }
        updateProgress(0f);

        PressRoutineComplete(); // Proceed with the Button Logic when complete.

        if (!m_interactable) // If button got disabled while pressing
            yield break;

        if (m_hovered)
            UpdateState(CustomState.Hovered);
        else
            UpdateState(CustomState.Normal);
    }

    private IEnumerator ButtonCooldownRoutine()
    {
        m_onCooldown = true;

        while (m_cooldownTimeLeft > 0f)
        {
            m_cooldownTimeLeft -= Time.deltaTime;
            yield return null;
        }

        m_onCooldown = false;
        m_cooldownTimeLeft = m_cooldown;
    }
    private void UpdateState(CustomState newState)
    {
        m_currentState = newState;
        stateChange(m_currentState);
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
