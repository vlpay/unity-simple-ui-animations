// IAnimatedButton.cs
using System;
using UnityEngine;

namespace _Project.Dev.Scripts.AnimatedUI
{
    public interface IAnimatedButton
    {
        bool IsInteractable { get; }

        event Action OnButtonPointerDown;
        event Action OnButtonPointerUp;
        event Action OnButtonClick;
        event Action<bool> OnInteractableStateChanged;

        void SetInteractable(bool interactable);
        void ForceReset();
        void HideImmediate();
        void ShowImmediate();
        void SimulatePress(bool animateRelease = true, float delay = 0.1f);
        void PlayPressAnimationOnly(bool animateRelease = true, float delay = 0.1f);

        void PlayAppearAnimation();
        void PlayDisappearAnimation();
    }
}
