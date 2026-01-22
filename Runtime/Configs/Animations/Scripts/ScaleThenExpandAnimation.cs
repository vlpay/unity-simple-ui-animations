using DG.Tweening;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

/// <summary>
/// Two-part animation: scale first, then expand RectTransform to fill space
/// </summary>
[CreateAssetMenu(fileName = "ScaleThenExpandAnimation", menuName = "Configs/UI/ScaleThenExpandAnimation")]
public class ScaleThenExpandAnimation : UIAnimation<RectTransform>
{
    [Serializable]
    public class ExpandSettings
    {
        public Vector2 targetSize = new Vector2(800, 600);
        public Vector2 targetPosition = Vector2.zero;
        [Range(0f, 1f)] public float delay = 0.2f;
        [Min(0.01f)] public float duration = 0.4f;
        public Ease easeType = Ease.OutQuad;
    }

    [Title("Scale Settings")]
    [SerializeField, Range(0f, 2f)]
    private float _scaleFrom = 0f;

    [SerializeField, Range(0f, 2f)]
    private float _scaleTo = 1f;

    [Title("Expand Settings")]
    [SerializeField]
    private ExpandSettings _expandSettings = new ExpandSettings();

    [Title("Original State")]
    [SerializeField]
    private Vector2 _originalSize = new Vector2(200, 150);

    [SerializeField]
    private Vector2 _originalPosition = Vector2.zero;

    [Title("Timing")]
    [SerializeField, Min(0f)]
    private float _pauseBetweenAnimations = 0.1f;

    /// <summary>
    /// Creates forward animation sequence
    /// </summary>
    protected override Tween CreateForwardAnimation(RectTransform target)
    {
        if (target == null) return null;

        Sequence sequence = DOTween.Sequence();

        // Store current size/position if not set
        if (_originalSize == Vector2.zero)
        {
            _originalSize = target.sizeDelta;
            _originalPosition = target.anchoredPosition;
        }

        // Reset to initial state
        target.localScale = Vector3.one * _scaleFrom;
        target.sizeDelta = _originalSize;
        target.anchoredPosition = _originalPosition;

        // Part 1: Scale animation
        Tween scaleTween = target.DOScale(Vector3.one * _scaleTo, Duration);

        sequence.Append(ApplyEasing(scaleTween));

        // Pause between animations
        if (_pauseBetweenAnimations > 0)
            sequence.AppendInterval(_pauseBetweenAnimations);

        // Part 2: Expand animation
        Tween expandTween = CreateExpandTween(target, _expandSettings);
        sequence.Append(expandTween);

        return ApplyEasing(sequence);
    }

    /// <summary>
    /// Creates reverse animation sequence
    /// </summary>
    protected override Tween CreateReverseAnimation(RectTransform target)
    {
        if (target == null) return null;

        Sequence sequence = DOTween.Sequence();

        // Get current state
        Vector2 currentSize = target.sizeDelta;
        Vector2 currentPosition = target.anchoredPosition;
        float currentScale = target.localScale.x;

        // Part 1: Shrink from expanded size back to original size
        // First ensure we have valid original size (not zero)
        Vector2 shrinkTargetSize = _originalSize;
        if (shrinkTargetSize == Vector2.zero)
        {
            shrinkTargetSize = new Vector2(200, 150); // Default fallback
        }

        Vector2 shrinkTargetPosition = _originalPosition;

        Tween shrinkTween = target.DOSizeDelta(shrinkTargetSize, _expandSettings.duration)
            .SetEase(_expandSettings.easeType);

        Tween positionTween = target.DOAnchorPos(shrinkTargetPosition, _expandSettings.duration)
            .SetEase(_expandSettings.easeType);

        sequence.Append(shrinkTween);
        sequence.Join(positionTween);

        // Pause
        if (_pauseBetweenAnimations > 0)
            sequence.AppendInterval(_pauseBetweenAnimations);

        // Part 2: Scale down
        // First set scale to _scaleTo so we animate from there
        target.localScale = Vector3.one * _scaleTo;
        Tween scaleDownTween = target.DOScale(Vector3.one * _scaleFrom, Duration);
        sequence.Append(scaleDownTween);

        // Reset to original state at the end
        sequence.OnComplete(() => {
            if (target != null)
            {
                target.sizeDelta = shrinkTargetSize;
                target.anchoredPosition = shrinkTargetPosition;
                target.localScale = Vector3.one * _scaleFrom;
            }
        });

        return ApplyReverseEasing(sequence);
    }

    /// <summary>
    /// Creates reverse animation with custom settings
    /// </summary>
    protected override Tween CreateReverseAnimationWithCustomSettings(RectTransform target)
    {
        return CreateReverseAnimation(target);
    }

    /// <summary>
    /// Creates expand tween
    /// </summary>
    private Tween CreateExpandTween(RectTransform target, ExpandSettings settings)
    {
        Sequence expandSequence = DOTween.Sequence();

        expandSequence.Append(target.DOSizeDelta(settings.targetSize, settings.duration).SetEase(settings.easeType));
        expandSequence.Join(target.DOAnchorPos(settings.targetPosition, settings.duration).SetEase(settings.easeType));
        expandSequence.SetDelay(settings.delay);

        return expandSequence;
    }

    /// <summary>
    /// Updates animation settings
    /// </summary>
    public void UpdateSettings(float scaleFrom, float scaleTo, ExpandSettings expandSettings)
    {
        _scaleFrom = Mathf.Clamp(scaleFrom, 0f, 5f);
        _scaleTo = Mathf.Clamp(scaleTo, 0f, 5f);
        _expandSettings = expandSettings;
    }

    /// <summary>
    /// Gets animation settings
    /// </summary>
    public (float scaleFrom, float scaleTo, ExpandSettings expandSettings) GetSettings()
    {
        return (_scaleFrom, _scaleTo, _expandSettings);
    }

    /// <summary>
    /// Sets original size and position values
    /// </summary>
    public void SetOriginalValues(Vector2 size, Vector2 position)
    {
        _originalSize = size;
        _originalPosition = position;
    }

    /// <summary>
    /// Gets original values
    /// </summary>
    public (Vector2 size, Vector2 position) GetOriginalValues()
    {
        return (_originalSize, _originalPosition);
    }

    /// <summary>
    /// Captures original values from target
    /// </summary>
    [Button("Capture Original Values")]
    public void CaptureOriginalValues(RectTransform target)
    {
        if (target != null)
        {
            _originalSize = target.sizeDelta;
            _originalPosition = target.anchoredPosition;
            Debug.Log($"Captured original values: Size={_originalSize}, Position={_originalPosition}");
        }
    }

    /// <summary>
    /// Set expand size to fill parent with margins
    /// </summary>
    [Button("Set Expand To Fill Parent")]
    public void SetExpandToFillParent(RectTransform parent, float margin = 20f)
    {
        if (parent != null)
        {
            _expandSettings.targetSize = new Vector2(
                parent.rect.width - margin * 2,
                parent.rect.height - margin * 2
            );
            _expandSettings.targetPosition = Vector2.zero;
        }
    }
}
