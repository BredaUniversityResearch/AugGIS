using UnityEngine;
using UnityEngine.UIElements;
using System;

public class UIButtonHold : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string buttonName = "accept-button";
    [SerializeField] private float holdDuration = 1f;
    [SerializeField] private Color fillColor = new Color(0.3f, 0.7f, 1f, 0.5f);

    public event Action OnHoldCompleted;

    private VisualElement root;
    private Button button;
    private VisualElement fillBar;

    private bool isHovering;
    private float holdTimer;
    private bool hasTriggered;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;
        button = root.Q<Button>(buttonName);

        if (button == null)
        {
            Debug.LogWarning($"HoldButton: Could not find button '{buttonName}'");
            return;
        }

        // Create the fill overlay
        fillBar = new VisualElement();
        fillBar.name = "hold-fill";
        fillBar.style.position = Position.Absolute;
        fillBar.style.top = 0;
        fillBar.style.bottom = 0;
        fillBar.style.left = 0;
        fillBar.style.width = Length.Percent(0);
        fillBar.style.backgroundColor = fillColor;
        fillBar.pickingMode = PickingMode.Ignore;

        // Insert fill behind the button's text but inside the button
        button.Insert(0, fillBar);

        // Make sure the button clips the fill
        button.style.overflow = Overflow.Hidden;

        // Prevent the default click behavior
        button.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);

        button.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        button.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        button.RegisterCallback<PointerDownEvent>(OnPointerDown);
        button.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void OnDisable()
    {
        if (button == null) return;

        button.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
        button.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
        button.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        button.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        button.UnregisterCallback<PointerUpEvent>(OnPointerUp);

        if (fillBar != null && button.Contains(fillBar))
            button.Remove(fillBar);
    }

    private void OnClick(ClickEvent evt)
    {
        // Block the default click — we only want hold-to-confirm
        evt.StopImmediatePropagation();
        evt.PreventDefault();
    }

    private void OnPointerEnter(PointerEnterEvent evt)
    {
        isHovering = true;
        hasTriggered = false;
    }

    private void OnPointerLeave(PointerLeaveEvent evt)
    {
        ResetFill();
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        // Optional: only start filling on press rather than hover
        // Uncomment the lines below and remove the hover-based filling
        // in Update if you prefer press-and-hold instead of hover-and-hold
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        // If you use press-and-hold, reset here
    }

    private void Update()
    {
        if (!isHovering || hasTriggered || button == null)
            return;

        holdTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(holdTimer / holdDuration);

        // Update fill bar width
        fillBar.style.width = Length.Percent(progress * 100f);

        if (progress >= 1f)
        {
            hasTriggered = true;
            OnHoldCompleted?.Invoke();

            // Optional: visual feedback on completion
            fillBar.style.backgroundColor = fillColor;
            ResetFill();
        }
    }

    private void ResetFill()
    {
        isHovering = false;
        holdTimer = 0f;

        if (fillBar != null)
            fillBar.style.width = Length.Percent(0);
    }
}