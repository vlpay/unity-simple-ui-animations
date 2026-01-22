using DG.Tweening;
using Sirenix.OdinInspector;
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Universal window animation component with support for different animation types
/// </summary>
public class AnimatedWindow : MonoBehaviour, IDisposable
{
    public enum AnimationTargetType
    {
        Transform,
        Image,
        CanvasGroup,
        RectTransform
    }

    [Title("Animation Settings")]
    [SerializeField, EnumToggleButtons]
    private AnimationTargetType _targetType = AnimationTargetType.Transform;

    [SerializeField, ShowIf(nameof(ShowTransformAnimation)), InlineEditor]
    private UIAnimation<Transform> _transformAnimation;

    [SerializeField, ShowIf(nameof(ShowImageAnimation)), InlineEditor]
    private UIAnimation<Image> _imageAnimation;

    [SerializeField, ShowIf(nameof(ShowCanvasGroupAnimation)), InlineEditor]
    private UIAnimation<CanvasGroup> _canvasGroupAnimation;

    [SerializeField, ShowIf(nameof(ShowRectTransformAnimation)), InlineEditor]
    private UIAnimation<RectTransform> _rectTransformAnimation;

    [Title("Target Settings")]
    [SerializeField, ShowIf(nameof(ShowTransformTarget))]
    private Transform _transformTarget;

    [SerializeField, ShowIf(nameof(ShowImageTarget))]
    private Image _imageTarget;

    [SerializeField, ShowIf(nameof(ShowCanvasGroupTarget))]
    private CanvasGroup _canvasGroupTarget;

    [SerializeField, ShowIf(nameof(ShowRectTransformTarget))]
    private RectTransform _rectTransformTarget;

    [Title("Animation State")]
    [SerializeField, ReadOnly]
    private bool _isAnimating;

    [SerializeField, ReadOnly]
    private bool _isVisible = true;

    [Title("Settings")]
    [SerializeField]
    private bool _allowInterruption = true;

    [SerializeField]
    private float _interruptionDelay = 0.05f;

    private Tween _currentTween;
    private bool _isKillingTween = false;
    private float _lastInterruptionTime = 0f;

    // Odin conditionals
    private bool ShowTransformAnimation => _targetType == AnimationTargetType.Transform;
    private bool ShowImageAnimation => _targetType == AnimationTargetType.Image;
    private bool ShowCanvasGroupAnimation => _targetType == AnimationTargetType.CanvasGroup;
    private bool ShowRectTransformAnimation => _targetType == AnimationTargetType.RectTransform;

    private bool ShowTransformTarget => ShowTransformAnimation;
    private bool ShowImageTarget => ShowImageAnimation;
    private bool ShowCanvasGroupTarget => ShowCanvasGroupAnimation;
    private bool ShowRectTransformTarget => ShowRectTransformAnimation;

    private void Awake()
    {
        InitializeTargets();
        _isVisible = gameObject.activeSelf;
    }

    private void InitializeTargets()
    {
        switch (_targetType)
        {
            case AnimationTargetType.Transform:
                if (_transformTarget == null)
                    _transformTarget = transform;
                break;

            case AnimationTargetType.Image:
                if (_imageTarget == null)
                    _imageTarget = GetComponent<Image>();
                break;

            case AnimationTargetType.CanvasGroup:
                if (_canvasGroupTarget == null)
                    _canvasGroupTarget = GetComponent<CanvasGroup>();
                break;

            case AnimationTargetType.RectTransform:
                if (_rectTransformTarget == null)
                    _rectTransformTarget = GetComponent<RectTransform>();
                break;
        }
    }

    /// <summary>
    /// Shows the window with animation
    /// </summary>
    [Button(ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 0.4f)]
    public void Show()
    {
        if (Time.unscaledTime - _lastInterruptionTime < _interruptionDelay)
            return;

        if (_isAnimating && _isVisible)
            return;

        _lastInterruptionTime = Time.unscaledTime;
        _isVisible = true;

        StopCurrentAnimationSafe();
        EnsureGameObjectActive();

        _isAnimating = true;

        Tween tween = CreateTweenForAnimation(true);

        if (tween != null)
        {
            _currentTween = tween;
            _currentTween
                .OnComplete(() =>
                {
                    _isAnimating = false;
                    OnShowComplete?.Invoke();
                })
                .OnKill(() =>
                {
                    // Проверяем, что это тот же твин, который мы запустили
                    if (_currentTween == tween)
                    {
                        _isAnimating = false;
                        _currentTween = null;
                    }
                });
        }
        else
        {
            _isAnimating = false;
            OnShowComplete?.Invoke();
        }
    }

    /// <summary>
    /// Hides the window with animation
    /// </summary>
    [Button(ButtonSizes.Medium), GUIColor(0.8f, 0.4f, 0.4f)]
    public void Hide()
    {
        // Проверяем частоту прерываний
        if (Time.unscaledTime - _lastInterruptionTime < _interruptionDelay)
            return;

        // Если анимация уже выполняется в нужном направлении, ничего не делаем
        if (_isAnimating && !_isVisible)
            return;

        _lastInterruptionTime = Time.unscaledTime;
        _isVisible = false;

        StopCurrentAnimationSafe();
        if (!gameObject.activeSelf) return;

        _isAnimating = true;

        Tween tween = CreateTweenForAnimation(false);

        if (tween != null)
        {
            _currentTween = tween;
            _currentTween
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    _isAnimating = false;
                    _currentTween = null;
                    OnHideComplete?.Invoke();
                })
                .OnKill(() =>
                {
                    // Проверяем, что это тот же твин, который мы запустили
                    if (_currentTween == tween)
                    {
                        if (gameObject.activeSelf)
                            gameObject.SetActive(false);
                        _isAnimating = false;
                        _currentTween = null;
                    }
                });
        }
        else
        {
            gameObject.SetActive(false);
            _isAnimating = false;
            OnHideComplete?.Invoke();
        }
    }

    /// <summary>
    /// Shows the window immediately without animation
    /// </summary>
    public void ShowImmediate()
    {
        StopCurrentAnimationSafe();
        EnsureGameObjectActive();
        _isAnimating = false;
        _isVisible = true;
        OnShowComplete?.Invoke();
    }

    /// <summary>
    /// Hides the window immediately without animation
    /// </summary>
    public void HideImmediate()
    {
        StopCurrentAnimationSafe();
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
        _isAnimating = false;
        _isVisible = false;
        OnHideComplete?.Invoke();
    }

    private Tween CreateTweenForAnimation(bool isForward)
    {
        // Сначала убиваем все твины на целевом объекте
        KillTweensOnTarget();

        switch (_targetType)
        {
            case AnimationTargetType.Transform:
                if (_transformAnimation != null && _transformTarget != null)
                {
                    return isForward ?
                        _transformAnimation.ApplyTo(_transformTarget) :
                        _transformAnimation.ApplyReverse(_transformTarget);
                }
                break;

            case AnimationTargetType.Image:
                if (_imageAnimation != null && _imageTarget != null)
                {
                    return isForward ?
                        _imageAnimation.ApplyTo(_imageTarget) :
                        _imageAnimation.ApplyReverse(_imageTarget);
                }
                break;

            case AnimationTargetType.CanvasGroup:
                if (_canvasGroupAnimation != null && _canvasGroupTarget != null)
                {
                    return isForward ?
                        _canvasGroupAnimation.ApplyTo(_canvasGroupTarget) :
                        _canvasGroupAnimation.ApplyReverse(_canvasGroupTarget);
                }
                break;

            case AnimationTargetType.RectTransform:
                if (_rectTransformAnimation != null && _rectTransformTarget != null)
                {
                    return isForward ?
                        _rectTransformAnimation.ApplyTo(_rectTransformTarget) :
                        _rectTransformAnimation.ApplyReverse(_rectTransformTarget);
                }
                break;
        }

        return null;
    }

    /// <summary>
    /// Toggles window visibility
    /// </summary>
    [Button(ButtonSizes.Large)]
    public void Toggle()
    {
        if (_isVisible)
            Hide();
        else
            Show();
    }

    /// <summary>
    /// Sets animation and target type automatically based on component
    /// </summary>
    [Button("Auto Setup"), GUIColor(0.4f, 0.6f, 1f)]
    public void AutoSetup()
    {
        if (TryGetComponent<Image>(out var image))
        {
            _targetType = AnimationTargetType.Image;
            _imageTarget = image;
        }
        else if (TryGetComponent<CanvasGroup>(out var canvasGroup))
        {
            _targetType = AnimationTargetType.CanvasGroup;
            _canvasGroupTarget = canvasGroup;
        }
        else if (TryGetComponent<RectTransform>(out var rectTransform))
        {
            _targetType = AnimationTargetType.RectTransform;
            _rectTransformTarget = rectTransform;
        }
        else
        {
            _targetType = AnimationTargetType.Transform;
            _transformTarget = transform;
        }
    }

    // Events for external listeners
    public event Action OnShowComplete;
    public event Action OnHideComplete;

    private void StopCurrentAnimationSafe()
    {
        if (_isKillingTween) return;

        _isKillingTween = true;
        try
        {
            if (_currentTween != null && _currentTween.IsActive())
            {
                // Используем Complete() вместо Kill() для плавного завершения
                _currentTween.Complete();
                _currentTween = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error stopping animation: {e.Message}");
        }
        finally
        {
            _isKillingTween = false;
            _isAnimating = false;
        }
    }

    private void KillTweensOnTarget()
    {
        // Убиваем все твины на целевом объекте
        switch (_targetType)
        {
            case AnimationTargetType.Transform:
                if (_transformTarget != null)
                    DOTween.Kill(_transformTarget);
                break;
            case AnimationTargetType.Image:
                if (_imageTarget != null)
                    DOTween.Kill(_imageTarget);
                break;
            case AnimationTargetType.CanvasGroup:
                if (_canvasGroupTarget != null)
                    DOTween.Kill(_canvasGroupTarget);
                break;
            case AnimationTargetType.RectTransform:
                if (_rectTransformTarget != null)
                    DOTween.Kill(_rectTransformTarget);
                break;
        }
    }

    private void EnsureGameObjectActive()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        Dispose();
    }

    private void OnDestroy()
    {
        Dispose();
    }

    public void Dispose()
    {
        StopCurrentAnimationSafe();
        KillTweensOnTarget();
    }

    // Properties for external access
    public bool IsVisible => _isVisible;
    public bool IsAnimating => _isAnimating;
    public AnimationTargetType TargetType => _targetType;

    #region Быстрые методы управления

    /// <summary>
    /// Показывает окно с возможностью прерывания текущей анимации
    /// </summary>
    public void ShowWithInterruption()
    {
        if (!_allowInterruption && _isAnimating) return;
        Show();
    }

    /// <summary>
    /// Скрывает окно с возможностью прерывания текущей анимации
    /// </summary>
    public void HideWithInterruption()
    {
        if (!_allowInterruption && _isAnimating) return;
        Hide();
    }

    #endregion
}
