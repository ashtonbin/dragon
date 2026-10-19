using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Physical one-pixel label travel and one activation click for the authored desktop buttons.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class FacilityDesktopButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public FacilityMenuAudio MenuAudio;
    public Image face;
    public Sprite raisedSprite, pressedSprite, hoverSprite;
    public RectTransform content;
    public Vector2 pressedOffset = new Vector2(1f, -1f);

    Button button;
    Vector2 restingPosition;
    bool haveRestingPosition;
    bool hovered, selected;
    float releaseAt;

    void Awake()
    {
        button = GetComponent<Button>();
        if (face == null) face = button.targetGraphic as Image;
        CaptureRestingPosition();
        SetPressed(false);
    }

    void CaptureRestingPosition()
    {
        if (!haveRestingPosition && content != null)
        {
            restingPosition = content.anchoredPosition;
            haveRestingPosition = true;
        }
    }

    // Persistently registered before the session's runtime listeners so scene changes cannot cut it off.
    // Button.onClick covers pointer activation, keyboard/gamepad Submit and accessibility invocation.
    public void Activate()
    {
        if (this == null) return;
        if (button == null) button = GetComponent<Button>();
        FacilityMenuAudio audio = MenuAudio != null ? MenuAudio : FacilityMenuAudio.Instance;
        if (audio != null && button != null && button.IsInteractable()) audio.Activate(button);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (this == null || !isActiveAndEnabled || eventData.button != PointerEventData.InputButton.Left || button == null || !button.IsInteractable()) return;
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) SetPressed(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        SetPressed(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        SetPressed(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        SetPressed(false);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        SetPressed(false);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        // Button's earlier Submit handler can synchronously load another scene. ExecuteEvents may
        // still hold this handler in its snapshot after Unity has destroyed the old button object.
        if (this == null || !isActiveAndEnabled || button == null || !button.IsInteractable()) return;
        SetPressed(true);
        releaseAt = Time.unscaledTime + .10f;
    }

    void Update()
    {
        if (releaseAt > 0f && Time.unscaledTime >= releaseAt)
        {
            releaseAt = 0f;
            SetPressed(false);
        }
    }

    void SetPressed(bool pressed)
    {
        CaptureRestingPosition();
        if (content != null && haveRestingPosition)
            content.anchoredPosition = restingPosition + (pressed ? pressedOffset : Vector2.zero);
        if (face != null)
        {
            Sprite sprite = pressed ? pressedSprite : (hovered || selected) ? hoverSprite : raisedSprite;
            if (sprite == null) sprite = raisedSprite;
            if (sprite != null) face.sprite = sprite;
        }
    }

    void OnDisable()
    {
        releaseAt = 0f;
        hovered = selected = false;
        SetPressed(false);
    }
}
