using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Project.Dev.Scripts.AnimatedUI
{
    public sealed class AnimatedButton : MonoBehaviour, IAnimatedButton, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        #region Serialized Fields
        [Title("Animation")]
        [SerializeField, InlineEditor] private UIAnimation<Transform> _animation;

        [Title("References")]
        [SerializeField, Required] private Transform _visualRoot;
        [SerializeField] private Button _button;

        [Title("State Settings")]
        [SerializeField] private bool _syncWithButton = true;

        [Title("Interactive State Animation")]
        [SerializeField] private bool _animateStateChange = true;
        [SerializeField, ShowIf(nameof(_animateStateChange)), Range(0.1f, 1f)] private float _disabledAlpha = 0.5f;
        [SerializeField, ShowIf(nameof(_animateStateChange)), Range(0.1f, 1f)] private float _stateTransitionDuration = 0.2f;

        [Title("Appear/Disappear Animation")]
        [SerializeField, InlineEditor] private UIAnimation<Transform> _appearAnimation;

        [Title("Threshold Settings")]
        [SerializeField, Range(0.01f, 1f)] private float _pressThreshold = 0.1f;
        [SerializeField, Range(0.01f, 1f)] private float _releaseAnimationDelay = 0.1f;
        #endregion

        #region Private Fields
        private Tween _currentTween;
        private Tween _appearDisappearTween;
        private Tween _stateTween;
        private bool _isPressed;
        private bool _isInteractable = true;
        private CanvasGroup _visualCanvasGroup;
        private float _pressStartTime;
        private bool _pendingReleaseAnimation;
        private bool _isAppearingOrDisappearing;
        private Image _visualImage;
        #endregion

        #region Properties
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
        #endregion

        #region Events
        public event Action OnButtonDownPlayed;
        public event Action OnButtonUpPlayed;
        public event Action OnButtonClick;
        public event Action<bool> OnInteractableStateChanged;
        public event Action OnButtonAppeared;
        public event Action OnButtonDisappeared;
        #endregion

        #region Public Methods
        public void PlayAppearAnimation()
        {
            KillAppearDisappearTween();
            _isAppearingOrDisappearing = true;
            SetRaycastTarget(true);

            var target = _visualRoot != null ? _visualRoot : transform;

            if (_appearAnimation != null)
            {
                _appearDisappearTween = _appearAnimation.ApplyTo(target);
            }
            else if (_animation != null)
            {
                _appearDisappearTween = _animation.ApplyTo(target);
            }
            else
            {
                target.localScale = Vector3.zero;
                _appearDisappearTween = target.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
            }

            _appearDisappearTween.OnComplete(() =>
            {
                _isAppearingOrDisappearing = false;
                OnButtonAppeared?.Invoke();
            });
        }

        public void PlayDisappearAnimation()
        {
            KillAppearDisappearTween();
            _isAppearingOrDisappearing = true;
            KillTween();

            var target = _visualRoot != null ? _visualRoot : transform;

            if (_appearAnimation != null)
            {
                _appearDisappearTween = _appearAnimation.ApplyReverse(target);
            }
            else if (_animation != null)
            {
                _appearDisappearTween = _animation.ApplyReverse(target);
            }
            else
            {
                _appearDisappearTween = target.DOScale(0f, 0.2f).SetEase(Ease.InBack);
            }

            _appearDisappearTween.OnComplete(() =>
            {
                _isAppearingOrDisappearing = false;
                SetRaycastTarget(false);
                OnButtonDisappeared?.Invoke();
            });
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

            OnInteractableStateChanged?.Invoke(interactable);
        }

        public void HideImmediate()
        {
            KillAppearDisappearTween();
            KillTween();
            _isPressed = false;
            _pendingReleaseAnimation = false;
            _isAppearingOrDisappearing = false;

            var target = _visualRoot != null ? _visualRoot : transform;
            target.localScale = Vector3.zero;
            SetRaycastTarget(false);
        }

        public void ShowImmediate()
        {
            KillAppearDisappearTween();
            KillTween();
            _isPressed = false;
            _pendingReleaseAnimation = false;
            _isAppearingOrDisappearing = false;

            var target = _visualRoot != null ? _visualRoot : transform;
            target.localScale = Vector3.one;
            SetRaycastTarget(true);
        }

        public void ForceReset()
        {
            KillAppearDisappearTween();
            KillTween();
            _isPressed = false;
            _pendingReleaseAnimation = false;
            _isAppearingOrDisappearing = false;

            var target = _visualRoot != null ? _visualRoot : transform;
            target.localScale = Vector3.one;
            SetRaycastTarget(true);
        }

        public void ForceResetVisual()
        {
            KillTween();
            _isPressed = false;
            _pendingReleaseAnimation = false;

            if (_visualRoot != null && !_isAppearingOrDisappearing)
            {
                _visualRoot.DOKill();
                _visualRoot.localScale = Vector3.one;
                SetRaycastTarget(true);
            }
        }

        public void ResetToDefaultState()
        {
            var target = _visualRoot != null ? _visualRoot : transform;
            target.localScale = Vector3.one;
            _isPressed = false;
            _pendingReleaseAnimation = false;
            SetRaycastTarget(true);
        }

        public void SimulatePress(bool animateRelease = true, float delay = 0.1f)
        {
            if (!IsInteractable) return;

            PlayPressed();

            if (!animateRelease) return;

            this.DOKill();
            DOVirtual.DelayedCall(delay, () =>
            {
                PlayReleased();
                OnButtonUpPlayed?.Invoke();
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
            if (_visualRoot != null && _visualRoot.localScale == Vector3.zero) return;

            _isPressed = true;
            _pressStartTime = Time.unscaledTime;
            _pendingReleaseAnimation = false;

            PlayPressed();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed) return;

            _isPressed = false;
            var pressDuration = Time.unscaledTime - _pressStartTime;

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
            if (_visualRoot != null && _visualRoot.localScale == Vector3.zero) return;
            OnButtonClick?.Invoke();
        }
        #endregion

        #region Animation Methods
        private void PlayPressed()
        {
            if (_isAppearingOrDisappearing) return;
            KillTween();
            _pendingReleaseAnimation = false;

            var target = _visualRoot != null ? _visualRoot : transform;

            if (_animation != null)
                _currentTween = _animation.ApplyReverse(target);
            else
                _currentTween = target.DOScale(0.9f, 0.1f);

            OnButtonDownPlayed?.Invoke();
        }

        private void PlayReleased()
        {
            if (_pendingReleaseAnimation || _isAppearingOrDisappearing) return;

            KillTween();

            var target = _visualRoot != null ? _visualRoot : transform;

            if (_animation != null)
                _currentTween = _animation.ApplyTo(target);
            else
                _currentTween = target.DOScale(1f, 0.1f);

            OnButtonUpPlayed?.Invoke();
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

        private void KillAppearDisappearTween()
        {
            if (_appearDisappearTween?.IsActive() == true)
                _appearDisappearTween.Kill();
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

        private void SetRaycastTarget(bool enabled)
        {
            if (_visualImage == null && _visualRoot != null)
            {
                _visualImage = _visualRoot.GetComponent<Image>();
            }
            if (_visualImage != null)
            {
                _visualImage.raycastTarget = enabled;
            }
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            SetupButtonComponent();

            if (_visualRoot != null)
            {
                _visualImage = _visualRoot.GetComponent<Image>();
            }

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
            KillAppearDisappearTween();
            KillStateTween();
        }
        #endregion

        #region Validation
        private bool IsValidVisual(Transform visual)
        {
            if (visual == null) return false;
            if (visual == transform) return false;
            if (!visual.IsChildOf(transform)) return false;

            var image = visual.GetComponent<Image>();
            if (image == null) return false;

            return true;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
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
            if (_visualRoot != null) return;

            var images = GetComponentsInChildren<Image>(true);

            foreach (var img in images)
            {
                if (img.transform == transform) continue;
                Undo.RecordObject(this, "Assign Visual Root");
                _visualRoot = img.transform;
                EditorUtility.SetDirty(this);
                return;
            }

            Debug.LogError($"{name}: No child Image found", this);
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
#endif
        #endregion
    }
}
