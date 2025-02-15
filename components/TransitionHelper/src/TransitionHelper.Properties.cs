// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if WINAPPSDK
using CommunityToolkit.WinUI.UI.Animations;
using Microsoft.UI.Xaml.Media.Animation;
#else
using Microsoft.Toolkit.Uwp.UI.Animations;
using Windows.UI.Xaml.Media.Animation;
#endif

namespace CommunityToolkit.Labs.WinUI;

/// <summary>
/// A animation helper that morphs between two controls.
/// </summary>
public sealed partial class TransitionHelper
{
    private FrameworkElement? _source;
    private int _sourceZIndex = -1;
    private FrameworkElement? _target;
    private int _targetZIndex = -1;

    /// <summary>
    /// Gets or sets the source control.
    /// </summary>
    public FrameworkElement? Source
    {
        get
        {
            return this._source;
        }

        set
        {
            if (this._source == value)
            {
                return;
            }

            var needReset = IsAnimating || IsTargetState;
            if (IsAnimating && this._source is not null)
            {
                this.Stop();
                RestoreElements(this.SourceAnimatedElements.All());
            }

            this._currentAnimationGroupController = null;
            this._source = value;
            this._sourceZIndex = value is null ? -1 : Canvas.GetZIndex(value);
            this._sourceAnimatedElements = null;
            if (needReset)
            {
                this.Reset(true);
            }
        }
    }

    /// <summary>
    /// Gets or sets the target control.
    /// </summary>
    public FrameworkElement? Target
    {
        get
        {
            return this._target;
        }

        set
        {
            if (this._target == value)
            {
                return;
            }

            var needReset = IsAnimating || IsTargetState;
            if (IsAnimating && this._target is not null)
            {
                this.Stop();
                RestoreElements(this.TargetAnimatedElements.All());
            }

            this._currentAnimationGroupController = null;
            this._target = value;
            this._targetZIndex = value is null ? -1 : Canvas.GetZIndex(value);
            this._targetAnimatedElements = null;
            if (needReset)
            {
                this.Reset(true);
            }
        }
    }

    /// <summary>
    /// Gets or sets transition configurations of UI elements that need to be connected by animation.
    /// </summary>
    public List<TransitionConfig> Configs { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether the source control has been morphed to the target control.
    /// The default value is false.
    /// </summary>
    public bool IsTargetState { get; private set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the contained area of the source or target control can return true values for hit testing when animating.
    /// The default value is false.
    /// </summary>
    public bool IsHitTestVisibleWhenAnimating { get; set; } = false;

    /// <summary>
    /// Gets or sets the method of changing the visibility of the source control.
    /// The default value is <see cref="VisualStateToggleMethod.ByVisibility"/>.
    /// </summary>
    public VisualStateToggleMethod SourceToggleMethod { get; set; } = VisualStateToggleMethod.ByVisibility;

    /// <summary>
    /// Gets or sets the method of changing the visibility of the target control.
    /// The default value is <see cref="VisualStateToggleMethod.ByVisibility"/>.
    /// </summary>
    public VisualStateToggleMethod TargetToggleMethod { get; set; } = VisualStateToggleMethod.ByVisibility;

    /// <summary>
    /// Gets or sets the duration of the connected animation between two UI elements.
    /// The default value is 600ms.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Gets or sets the reverse duration of the connected animation between two UI elements.
    /// The default value is 600ms.
    /// </summary>
    public TimeSpan ReverseDuration { get; set; } = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Gets or sets a value indicating whether to use the inverse easing function when animating in reverse direction.
    /// The default value is true.
    /// </summary>
    public bool InverseEasingFunctionWhenReversing { get; set; } = true;

    /// <summary>
    /// Gets or sets the duration of the show animation for independent or unpaired UI elements.
    /// The default value is 200ms.
    /// </summary>
    public TimeSpan IndependentElementShowDuration { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the delay of the show animation for independent or unpaired UI elements.
    /// The default value is 300ms.
    /// </summary>
    public TimeSpan IndependentElementShowDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Gets or sets the interval between the show animations for independent or unpaired UI elements.
    /// The default value is 50ms.
    /// </summary>
    public TimeSpan IndependentElementShowInterval { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets the duration of the hide animation for independent or unpaired UI elements.
    /// The default value is 100ms.
    /// </summary>
    public TimeSpan IndependentElementHideDuration { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the default easing function type for the transition.
    /// The default value is <see cref="EasingType.Default"/>.
    /// </summary>
    public EasingType DefaultEasingType { get; set; } = EasingType.Default;

    /// <summary>
    /// Gets or sets the default easing function mode for the transition.
    /// The default value is <see cref="EasingMode.EaseInOut"/>.
    /// </summary>
    public EasingMode DefaultEasingMode { get; set; } = EasingMode.EaseInOut;

    /// <summary>
    /// Gets or sets the default translation used by the show or hide animation for independent or unpaired UI elements.
    /// The default value is (0, 20).
    /// </summary>
    public Point DefaultIndependentTranslation { get; set; } = new(0, 20);

    /// <summary>
    /// Gets or sets the default key point of opacity transition.
    /// The time the keyframe of opacity from 0 to 1 or from 1 to 0 should occur at, expressed as a percentage of the animation duration. The allowed values are from (0, 0) to (1, 1).
    /// <see cref="DefaultOpacityTransitionProgressKey"/>.X will be used in the animation of the normal direction.
    /// <see cref="DefaultOpacityTransitionProgressKey"/>.Y will be used in the animation of the reverse direction.
    /// The default value is (0.3, 0.3).
    /// </summary>
    public Point DefaultOpacityTransitionProgressKey { get; set; } = new(0.3, 0.3);

    /// <summary>
    /// Gets or sets the easing function type for animation of independent or unpaired UI elements.
    /// The default value is <see cref="EasingType.Default"/>.
    /// </summary>
    public EasingType IndependentElementEasingType { get; set; } = EasingType.Default;

    /// <summary>
    /// Gets or sets the easing function mode for animation of independent or unpaired UI elements.
    /// The default value is <see cref="EasingMode.EaseInOut"/>.
    /// </summary>
    public EasingMode IndependentElementEasingMode { get; set; } = EasingMode.EaseInOut;
}
