using DG.Tweening;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

/// <summary>
/// Base class for all UI animations with forward and reverse support
/// </summary>
/// <typeparam name="T">Component type that this animation works with</typeparam>
public abstract class UIAnimation<T> : ScriptableObject where T : Component
{
    [field: SerializeField, Min(0.01f)]
    public float Duration { get; private set; } = 0.3f;

    [SerializeField] private bool useCustomCurve = false;

    [field: SerializeField, EnumToggleButtons, HideIf(nameof(useCustomCurve))]
    public Ease EaseType { get; private set; } = Ease.OutBack;

    [field: SerializeField, ShowIf(nameof(useCustomCurve))]
    public AnimationCurve CustomCurve { get; private set; }

    [SerializeField, Title("Reverse Settings"), Space(10)]
    protected bool hasSeparateReverseSettings = false;

    [SerializeField, ShowIf(nameof(hasSeparateReverseSettings)), Min(0.01f)]
    protected float reverseDuration = 0.3f;

    [SerializeField,
     ShowIf("@hasSeparateReverseSettings && !useCustomCurve"),
     EnumToggleButtons]
    protected Ease reverseEaseType = Ease.InQuart;

    [SerializeField,
     ShowIf("@hasSeparateReverseSettings && useCustomCurve")]
    protected AnimationCurve reverseCustomCurve;

    /// <summary>
    /// Apply forward animation to target
    /// </summary>
    public Tween ApplyTo(T target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        return CreateForwardAnimation(target);
    }

    /// <summary>
    /// Apply reverse animation to target
    /// </summary>
    public Tween ApplyReverse(T target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        if (!hasSeparateReverseSettings)
        {
            return CreateReverseAnimation(target);
        }

        return CreateReverseAnimationWithCustomSettings(target);
    }

    /// <summary>
    /// Creates forward animation (to be implemented by derived classes)
    /// </summary>
    protected abstract Tween CreateForwardAnimation(T target);

    /// <summary>
    /// Creates reverse animation using forward settings (MUST be implemented by derived classes)
    /// </summary>
    protected abstract Tween CreateReverseAnimation(T target);

    /// <summary>
    /// Creates reverse animation with custom settings
    /// Default implementation uses CreateReverseAnimation with reverse settings
    /// </summary>
    protected virtual Tween CreateReverseAnimationWithCustomSettings(T target)
    {
        // Default implementation falls back to regular reverse
        return CreateReverseAnimation(target);
    }

    /// <summary>
    /// Applies easing to a tween
    /// </summary>
    protected Tween ApplyEasing(Tween tween,
        Ease? customEase = null,
        bool? useCustomCurveOverride = null,
        AnimationCurve customCurveOverride = null,
        float? durationOverride = null)
    {
        bool shouldUseCustomCurve = useCustomCurveOverride ?? useCustomCurve;
        AnimationCurve curveToUse = customCurveOverride ?? CustomCurve;
        Ease easeToUse = customEase ?? EaseType;
        float durationToUse = durationOverride ?? Duration;

        tween.SetDelay(0);

        if (shouldUseCustomCurve && curveToUse != null && curveToUse.keys.Length > 0)
        {
            tween.SetEase(curveToUse);
        }
        else
        {
            tween.SetEase(easeToUse);
        }

        return tween;
    }

    /// <summary>
    /// Applies reverse easing to a tween
    /// </summary>
    protected Tween ApplyReverseEasing(Tween tween)
    {
        if (!hasSeparateReverseSettings)
        {
            return ApplyEasing(tween);
        }

        return ApplyEasing(tween,
            customEase: reverseEaseType,
            useCustomCurveOverride: useCustomCurve,
            customCurveOverride: reverseCustomCurve,
            durationOverride: reverseDuration);
    }

    /// <summary>
    /// Creates a simple reversed tween by playing forward tween backwards
    /// Helper method for derived classes that want simple reverse behavior
    /// </summary>
    protected Tween CreateSimpleReverseTween(T target, Func<T, Tween> forwardTweenCreator)
    {
        var forwardTween = forwardTweenCreator(target);
        forwardTween.Goto(forwardTween.Duration(), andPlay: false);
        forwardTween.PlayBackwards();
        return forwardTween;
    }
}
