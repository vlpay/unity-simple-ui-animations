using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fade animation for Image components with forward/reverse support
/// </summary>
[CreateAssetMenu(fileName = "ImageFadeAnimation", menuName = "Configs/UI/ImageFadeAnimation")]
public class ImageFadeAnimation : UIAnimation<Image>
{
    [Title("Fade Settings")]
    [SerializeField, Range(0f, 1f)]
    private float _fadeFrom = 0f;

    [SerializeField, Range(0f, 1f)]
    private float _fadeTo = 1f;

    [Title("Color Settings")]
    [SerializeField]
    private bool _preserveColor = true;

    [SerializeField, ShowIf(nameof(_preserveColor))]
    private bool _saveOriginalColor = true;

    [SerializeField, HideIf(nameof(_preserveColor))]
    private Color _customColor = Color.white;

    private Color? _originalColor;

    /// <summary>
    /// Creates forward fade animation
    /// </summary>
    protected override Tween CreateForwardAnimation(Image target)
    {
        if (target == null) return null;

        if (_saveOriginalColor && !_originalColor.HasValue)
        {
            _originalColor = target.color;
        }

        Color startColor = _preserveColor ? target.color : _customColor;
        startColor.a = _fadeFrom;
        target.color = startColor;

        float targetAlpha = _fadeTo;
        Color targetColor = startColor;
        targetColor.a = targetAlpha;

        var tween = target.DOColor(targetColor, Duration);
        return ApplyEasing(tween);
    }

    /// <summary>
    /// Creates reverse fade animation
    /// </summary>
    protected override Tween CreateReverseAnimation(Image target)
    {
        if (target == null) return null;

        Color startColor = target.color;
        float startAlpha = _fadeTo;
        startColor.a = startAlpha;
        target.color = startColor;

        Color targetColor = startColor;
        targetColor.a = _fadeFrom;

        if (_saveOriginalColor && _originalColor.HasValue)
        {
            targetColor.r = _originalColor.Value.r;
            targetColor.g = _originalColor.Value.g;
            targetColor.b = _originalColor.Value.b;
        }

        var tween = target.DOColor(targetColor, Duration);
        return ApplyReverseEasing(tween);
    }

    /// <summary>
    /// Creates reverse animation with custom settings
    /// </summary>
    protected override Tween CreateReverseAnimationWithCustomSettings(Image target)
    {
        if (target == null) return null;

        Color startColor = target.color;
        float startAlpha = _fadeTo;
        startColor.a = startAlpha;
        target.color = startColor;

        Color targetColor = startColor;
        targetColor.a = _fadeFrom;

        if (_saveOriginalColor && _originalColor.HasValue)
        {
            targetColor.r = _originalColor.Value.r;
            targetColor.g = _originalColor.Value.g;
            targetColor.b = _originalColor.Value.b;
        }

        float duration = hasSeparateReverseSettings ? reverseDuration : Duration;
        var tween = target.DOColor(targetColor, duration);

        return ApplyReverseEasing(tween);
    }
}
