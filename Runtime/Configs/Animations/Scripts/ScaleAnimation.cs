using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Scale animation for Transform components
/// </summary>
[CreateAssetMenu(fileName = "ScaleAnimation", menuName = "Configs/UI/ScaleAnimation")]
public class ScaleAnimation : UIAnimation<Transform>
{
    [SerializeField, Title("Scale Settings")]
    private Vector3 scaleFrom = Vector3.zero;

    [SerializeField]
    private Vector3 scaleTo = Vector3.one;

    /// <summary>
    /// Creates forward scale animation
    /// </summary>
    protected override Tween CreateForwardAnimation(Transform target)
    {
        Vector3 targetScale = scaleTo;
        target.localScale = scaleFrom;

        var tween = target.DOScale(targetScale, Duration);
        return ApplyEasing(tween);
    }

    /// <summary>
    /// Creates reverse scale animation
    /// </summary>
    protected override Tween CreateReverseAnimation(Transform target)
    {
        Vector3 startScale = scaleTo;
        target.localScale = startScale;

        var tween = target.DOScale(scaleFrom, Duration);
        return ApplyReverseEasing(tween);
    }

    /// <summary>
    /// Creates reverse animation with custom settings
    /// </summary>
    protected override Tween CreateReverseAnimationWithCustomSettings(Transform target)
    {
        Vector3 startScale = scaleTo;
        target.localScale = startScale;

        float duration = hasSeparateReverseSettings ? reverseDuration : Duration;
        var tween = target.DOScale(scaleFrom, duration);
        return ApplyReverseEasing(tween);
    }
}
