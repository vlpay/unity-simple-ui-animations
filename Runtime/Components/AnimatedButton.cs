using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace _Project.Dev.Scripts.AnimatedUI
{
    public sealed class AnimatedButton :
        MonoBehaviour,
        IAnimatedButton,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        #region Serialized Fields

        [Title("Animation")] [SerializeField] [InlineEditor] [Tooltip("Main button animation")]
        private UIAnimation<Transform> _animation;

        [Title("References")]
        [SerializeField]
        [Required]
        [Tooltip("Root transform for visual elements")]
        [ValidateInput(nameof(IsValidVisual), "VisualRoot must be child with Image (Raycast Target = false)")]
        private Transform _visualRoot;

        [SerializeField] [Tooltip("Optional Button component reference")]
        private Button _button;

        [Title("State Settings")] [SerializeField] [Tooltip("Sync interactable state with Button component")]
        private bool _syncWithButton = true;

        [Title("Interactive State Animation")] [SerializeField] [Tooltip("Animate interactable state changes")]
        private bool _animateStateChange = true;

        [SerializeField] [ShowIf(nameof(_animateStateChange))] [Range(0.1f, 1f)] [Tooltip("Alpha when disabled")]
        private float _disabledAlpha = 0.5f;

        [SerializeField] [ShowIf(nameof(_animateStateChange))] [Range(0.1f, 1f)] [Tooltip("State transition duration")]
        private float _stateTransitionDuration = 0.2f;

        [Title("Appear/Disappear Animation")]
        [SerializeField]
        [InlineEditor]
        [Tooltip("Specific appear/disappear animation")]
        private UIAnimation<Transform> _appearAnimation;

        [Title("Threshold Settings")]
        [SerializeField]
        [Range(0.01f, 1f)]
        [Tooltip("Minimum press duration for release animation")]
        private float _pressThreshold = 0.1f;

        [SerializeField] [Range(0.01f, 1f)] [Tooltip("Time to wait for release animation after quick press")]
        private float _releaseAnimationDelay = 0.1f;

        #endregion

        #region Private Fields

        private Tween _currentTween;
        private Tween _stateTween;
        private bool _isPressed;
        private bool _isInteractable = true;
        private CanvasGroup _visualCanvasGroup;
        private float _pressStartTime;
        private bool _pendingReleaseAnimation;

        #endregion

        #region IAnimatedButton Implementation

        public bool IsInteractable
        {
            get
            {
                if (!_syncWithButton || _button == null)
                    return _isInteractable;
                return _button.interactable && _isInteractable;
            }
            private set => _isInteractable = value;
        }

        public event Action OnButtonPointerDown;
        public event Action OnButtonPointerUp;
        public event Action OnButtonClick;
        public event Action<bool> OnInteractableStateChanged;

        public void PlayAppearAnimation()
        {
            KillTween();

            if (_appearAnimation != null)
            {
                _currentTween = _appearAnimation.ApplyTo(transform);
            }
            else if (_animation != null)
            {
                _currentTween = _animation.ApplyTo(transform);
            }
            else
            {
                transform.localScale = Vector3.zero;
                _currentTween = transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
            }
        }

        public void PlayDisappearAnimation()
        {
            KillTween();

            if (_appearAnimation != null)
                _currentTween = _appearAnimation.ApplyReverse(transform);
            else if (_animation != null)
                _currentTween = _animation.ApplyReverse(transform);
            else
                _currentTween = transform.DOScale(0f, 0.2f)
                    .SetEase(Ease.InBack);
        }

        public void SetButtonReference(Button button)
        {
            _button = button;
            if (_button != null && _syncWithButton) _isInteractable = _button.interactable;
        }

        public void SetInteractable(bool interactable)
        {
            if (IsInteractable == interactable) return;

            _isInteractable = interactable;

            if (_button != null && _syncWithButton) _button.interactable = interactable;

            if (_animateStateChange)
            {
                EnsureVisualCanvasGroup();

                if (_visualCanvasGroup != null)
                {
                    KillStateTween();

                    if (interactable)
                        _stateTween = _visualCanvasGroup.DOFade(1f, _stateTransitionDuration)
                            .SetEase(Ease.OutQuad);
                    else
                        _stateTween = _visualCanvasGroup.DOFade(_disabledAlpha, _stateTransitionDuration)
                            .SetEase(Ease.InQuad);
                }
            }
            else
            {
                if (_visualCanvasGroup != null) _visualCanvasGroup.alpha = interactable ? 1f : _disabledAlpha;
            }

            if (!interactable) ForceReset();

            OnInteractableStateChanged?.Invoke(interactable);
        }

        public void HideImmediate()
        {
            KillTween();
            _isPressed = false;
            _pendingReleaseAnimation = false;

            if (_visualRoot != null) _visualRoot.localScale = Vector3.zero;
        }

        public void ShowImmediate()
        {
            KillTween();
            _isPressed = false;
            _pendingReleaseAnimation = false;

            if (_visualRoot != null) _visualRoot.localScale = Vector3.one;
        }

        public void ForceReset()
        {
            KillTween();
            _isPressed = false;
            _pendingReleaseAnimation = false;

            if (_visualRoot != null) _visualRoot.localScale = Vector3.one;
        }

        public void SimulatePress(bool animateRelease = true, float delay = 0.1f)
        {
            if (!IsInteractable) return;

            PlayPressed();
            OnButtonPointerDown?.Invoke();

            if (!animateRelease) return;

            this.DOKill();
            DOVirtual.DelayedCall(delay, () =>
            {
                PlayReleased();
                OnButtonPointerUp?.Invoke();
                OnButtonClick?.Invoke();
            });
        }

        public void PlayPressAnimationOnly(bool animateRelease = true, float delay = 0.1f)
        {
            if (!IsInteractable) return;

            PlayPressed();

            if (!animateRelease) return;

            this.DOKill();
            DOVirtual.DelayedCall(delay, PlayReleased);
        }

        #endregion

        #region Input Handlers

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable) return;

            _isPressed = true;
            _pressStartTime = Time.unscaledTime;
            _pendingReleaseAnimation = false;

            PlayPressed();
            OnButtonPointerDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed) return;

            _isPressed = false;
            var pressDuration = Time.unscaledTime - _pressStartTime;

            OnButtonPointerUp?.Invoke();

            if (pressDuration < _pressThreshold)
            {
                _pendingReleaseAnimation = true;
                DOVirtual.DelayedCall(_releaseAnimationDelay - pressDuration, ExecutePendingReleaseAnimation);
            }
            else
            {
                PlayReleased();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            OnButtonClick?.Invoke();
        }

        #endregion

        #region Animation Methods

        private void PlayPressed()
        {
            KillTween();
            _pendingReleaseAnimation = false;

            if (_animation != null)
                _currentTween = _animation.ApplyReverse(_visualRoot);
            else
                _currentTween = _visualRoot.DOScale(0.9f, 0.1f);
        }

        private void PlayReleased()
        {
            if (_pendingReleaseAnimation) return;

            KillTween();

            if (_animation != null)
                _currentTween = _animation.ApplyTo(_visualRoot);
            else
                _currentTween = _visualRoot.DOScale(1f, 0.1f);
        }

        private void ExecutePendingReleaseAnimation()
        {
            if (!_pendingReleaseAnimation) return;

            _pendingReleaseAnimation = false;
            PlayReleased();
        }

        private void KillTween()
        {
            if (_currentTween?.IsActive() == true)
                _currentTween.Kill();
        }

        private void KillStateTween()
        {
            if (_stateTween?.IsActive() == true)
                _stateTween.Kill();
        }

        private void EnsureVisualCanvasGroup()
        {
            if (_visualCanvasGroup == null && _visualRoot != null)
            {
                _visualCanvasGroup = _visualRoot.GetComponent<CanvasGroup>();
                if (_visualCanvasGroup == null) _visualCanvasGroup = _visualRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            SetupButtonComponent();

            if (_animateStateChange)
            {
                EnsureVisualCanvasGroup();
                if (_visualCanvasGroup != null) _visualCanvasGroup.alpha = IsInteractable ? 1f : _disabledAlpha;
            }
        }

        private void SetupButtonComponent()
        {
            if (!_syncWithButton) return;

            if (_button == null) _button = GetComponent<Button>();

            if (_button == null)
            {
                _button = gameObject.AddComponent<Button>();
                _button.transition = Selectable.Transition.None;
            }

            _isInteractable = _button.interactable;
        }

        private void OnDisable()
        {
            ForceReset();
            KillStateTween();
            _pendingReleaseAnimation = false;
        }

        private void OnDestroy()
        {
            KillTween();
            KillStateTween();
        }

        #endregion

        #region Validation

        private bool IsValidVisual(Transform visual)
        {
            if (visual == null)
                return false;

            if (visual == transform)
                return false;

            if (!visual.IsChildOf(transform))
                return false;

            var image = visual.GetComponent<Image>();
            if (image == null)
                return false;

            return !image.raycastTarget;
        }

        #endregion

#if UNITY_EDITOR

        #region Editor

        [Button(ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void SetupButton()
        {
            var wasModified = false;

            Undo.RecordObject(this, "Setup Button Component");

            if (!_syncWithButton)
            {
                Debug.LogWarning($"{name}: Button sync is disabled", this);
                return;
            }

            if (_button == null)
            {
                _button = GetComponent<Button>();
                wasModified = true;
            }

            if (_button == null)
            {
                _button = gameObject.AddComponent<Button>();
                _button.transition = Selectable.Transition.None;
                wasModified = true;
                Debug.Log($"{name}: Button component created", this);
            }
            else if (_button.transition != Selectable.Transition.None)
            {
                _button.transition = Selectable.Transition.None;
                wasModified = true;
                Debug.Log($"{name}: Button settings updated", this);
            }

            _isInteractable = _button.interactable;

            if (wasModified)
            {
                EditorUtility.SetDirty(this);
                EditorUtility.SetDirty(_button);
            }
        }

        [Button(ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        private void FindVisual()
        {
            if (_visualRoot != null)
                return;

            var images = GetComponentsInChildren<Image>(true);

            foreach (var img in images)
            {
                if (img.transform == transform)
                    continue;

                if (img.raycastTarget) continue;
                Undo.RecordObject(this, "Assign Visual Root");
                _visualRoot = img.transform;
                EditorUtility.SetDirty(this);
                return;
            }

            Debug.LogError(
                $"{name}: No child Image with Raycast Target = false found",
                this);
        }

        [Button(ButtonSizes.Medium)]
        [GUIColor(0.8f, 0.4f, 0.8f)]
        private void TestAnimation()
        {
            SimulatePress();
        }

        [Button(ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 0.4f)]
        private void ToggleInteractable()
        {
            Undo.RecordObject(this, "Toggle Interactable");
            SetInteractable(!IsInteractable);
            EditorUtility.SetDirty(this);
        }

        [Button(ButtonSizes.Medium)]
        [GUIColor(0.8f, 0.6f, 0.4f)]
        private void ResetState()
        {
            Undo.RecordObject(this, "Reset Button State");
            ForceReset();
            EditorUtility.SetDirty(this);
        }

        [Button(ButtonSizes.Medium)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void TestStateAnimation()
        {
            Undo.RecordObject(this, "Test State Animation");
            SetInteractable(!IsInteractable);
            EditorUtility.SetDirty(this);
        }

        #endregion

#endif
    }
}
