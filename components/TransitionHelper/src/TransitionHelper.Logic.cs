// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

#if WINAPPSDK
using CommunityToolkit.WinUI.UI;
using CommunityToolkit.WinUI.UI.Animations;
using Microsoft.UI.Xaml.Media.Animation;
using AnimationDirection = Microsoft.UI.Composition.AnimationDirection;
#else
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Windows.UI.Xaml.Media.Animation;
using AnimationDirection = Windows.UI.Composition.AnimationDirection;
#endif

namespace CommunityToolkit.Labs.WinUI;

/// <summary>
/// A animation helper that morphs between two controls.
/// </summary>
public sealed partial class TransitionHelper
{
    private void RestoreState(bool isTargetState)
    {
        this.IsTargetState = isTargetState;
        if (this.Source is not null)
        {
            Canvas.SetZIndex(this.Source, _sourceZIndex);
            ToggleVisualState(this.Source, this.SourceToggleMethod, !isTargetState);
            RestoreElements(this.SourceAnimatedElements.All());
        }

        if (this.Target is not null)
        {
            Canvas.SetZIndex(this.Target, _targetZIndex);
            ToggleVisualState(this.Target, this.TargetToggleMethod, isTargetState);
            RestoreElements(this.TargetAnimatedElements.All());
        }
    }

    private async Task InitControlsStateAsync(bool forceUpdateAnimatedElements = false)
    {
        var maxZIndex = Math.Max(_sourceZIndex, _targetZIndex) + 1;
        Canvas.SetZIndex(this.IsTargetState ? this.Source : this.Target, maxZIndex);

        await Task.WhenAll(
            this.InitControlStateAsync(this.Source),
            this.InitControlStateAsync(this.Target));

        if (forceUpdateAnimatedElements)
        {
            _sourceAnimatedElements = null;
            _targetAnimatedElements = null;
        }
    }

    private async Task InitControlStateAsync(FrameworkElement? target)
    {
        if (target is null)
        {
            return;
        }

        target.IsHitTestVisible = IsHitTestVisibleWhenAnimating;
        if (target.Visibility == Visibility.Collapsed)
        {
            target.Visibility = Visibility.Visible;
            await UpdateControlLayout(target);
        }
        else if (target.Opacity < AlmostZero)
        {
            target.Opacity = 1;
        }
        else if (target.GetVisual() is { IsVisible: false } visual)
        {
            visual.IsVisible = true;
        }
    }

    private async Task AnimateControlsAsync(bool reversed, CancellationToken token, bool forceUpdateAnimatedElements)
    {
        IsNotNullAndIsInVisualTree(this.Source, nameof(this.Source));
        IsNotNullAndIsInVisualTree(this.Target, nameof(this.Target));
        if (IsAnimating)
        {
            if ((_currentAnimationGroupController?.CurrentDirection is AnimationDirection.Reverse) == reversed)
            {
                return;
            }
            else
            {
                this.Stop();
            }
        }
        else if (this.IsTargetState == !reversed)
        {
            return;
        }
        else
        {
            this._currentAnimationGroupController = null;
            await this.InitControlsStateAsync(forceUpdateAnimatedElements);
        }

        this._currentAnimationCancellationTokenSource = new CancellationTokenSource();
        var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, this._currentAnimationCancellationTokenSource.Token);
        await this.AnimateControlsImpAsync(reversed ? this.ReverseDuration : this.Duration, reversed, linkedTokenSource.Token);
        if (linkedTokenSource.Token.IsCancellationRequested)
        {
            return;
        }

        this._currentAnimationGroupController = null;
        this.RestoreState(!reversed);
    }

    private Task AnimateControlsImpAsync(TimeSpan duration, bool reversed, CancellationToken token)
    {
        var sourceUnpairedElements = this.SourceAnimatedElements.ConnectedElements
            .Where(item => !this.TargetAnimatedElements.ConnectedElements.ContainsKey(item.Key))
            .SelectMany(item =>
            {
                var result = new[] { item.Value };
                if (this.SourceAnimatedElements.CoordinatedElements.TryGetValue(item.Key, out var coordinatedElements))
                {
                    return result.Concat(coordinatedElements);
                }

                return result;
            });
        var targetUnpairedElements = this.TargetAnimatedElements.ConnectedElements
            .Where(item => !this.SourceAnimatedElements.ConnectedElements.ContainsKey(item.Key))
            .SelectMany(item =>
            {
                var result = new[] { item.Value };
                if (this.TargetAnimatedElements.CoordinatedElements.TryGetValue(item.Key, out var coordinatedElements))
                {
                    return result.Concat(coordinatedElements);
                }

                return result;
            });
        var pairedElementKeys = this.SourceAnimatedElements.ConnectedElements
            .Where(item => this.TargetAnimatedElements.ConnectedElements.ContainsKey(item.Key))
            .Select(item => item.Key);
        if (_currentAnimationGroupController is null)
        {
            _currentAnimationGroupController = new KeyFrameAnimationGroupController();
            foreach (var key in pairedElementKeys)
            {
                var source = this.SourceAnimatedElements.ConnectedElements[key];
                var target = this.TargetAnimatedElements.ConnectedElements[key];
                var animationConfig = this.Configs.FirstOrDefault(config => config.Id == key) ??
                                      this.DefaultConfig;
                this.SourceAnimatedElements.CoordinatedElements.TryGetValue(key, out var sourceAttachedElements);
                this.TargetAnimatedElements.CoordinatedElements.TryGetValue(key, out var targetAttachedElements);
                this.BuildConnectedAnimationController(
                    _currentAnimationGroupController,
                    source,
                    target,
                    sourceAttachedElements,
                    targetAttachedElements,
                    duration,
                    animationConfig);
            }
        }

        TimeSpan? startTime = null;
        if (_currentAnimationGroupController.LastStopProgress.HasValue)
        {
            var startProgress = reversed ? (1 - _currentAnimationGroupController.LastStopProgress.Value) : _currentAnimationGroupController.LastStopProgress.Value;
            startTime = startProgress * duration;
        }

        var animationTasks = new[]
        {
            reversed
                    ? _currentAnimationGroupController.ReverseAsync(token, this.InverseEasingFunctionWhenReversing, duration)
                    : _currentAnimationGroupController.StartAsync(token, duration),

            this.AnimateIndependentElements(
                this.SourceAnimatedElements.IndependentElements.Concat(sourceUnpairedElements),
                reversed,
                token,
                startTime,
                IndependentElementEasingType,
                IndependentElementEasingMode),
            this.AnimateIndependentElements(
                this.TargetAnimatedElements.IndependentElements.Concat(targetUnpairedElements),
                !reversed,
                token,
                startTime,
                IndependentElementEasingType,
                IndependentElementEasingMode)
        };

        return Task.WhenAll(animationTasks);
    }

    private void BuildConnectedAnimationController(
        IKeyFrameAnimationGroupController controller,
        UIElement source,
        UIElement target,
        IList<UIElement>? sourceAttachedElements,
        IList<UIElement>? targetAttachedElements,
        TimeSpan duration,
        TransitionConfig config)
    {
        var sourceActualSize = source is FrameworkElement sourceElement ? new Vector2((float)sourceElement.ActualWidth, (float)sourceElement.ActualHeight) : source.ActualSize;
        var targetActualSize = target is FrameworkElement targetElement ? new Vector2((float)targetElement.ActualWidth, (float)targetElement.ActualHeight) : target.ActualSize;
        var sourceCenterPoint = sourceActualSize * config.NormalizedCenterPoint.ToVector2();
        var targetCenterPoint = targetActualSize * config.NormalizedCenterPoint.ToVector2();

        source.GetVisual().CenterPoint = new Vector3(sourceCenterPoint, 0);
        target.GetVisual().CenterPoint = new Vector3(targetCenterPoint, 0);
        var easingType = config.EasingType ?? this.DefaultEasingType;
        var easingMode = config.EasingMode ?? this.DefaultEasingMode;
        var (sourceTranslationAnimation, targetTranslationAnimation, sourceTargetTranslation) = this.AnimateTranslation(
            source,
            target,
            sourceCenterPoint,
            targetCenterPoint,
            duration,
            easingType,
            easingMode);
        var (sourceOpacityAnimation, targetOpacityAnimation) = this.AnimateOpacity(duration, config.OpacityTransitionProgressKey ?? this.DefaultOpacityTransitionProgressKey);
        var (sourceScaleAnimation, targetScaleAnimation, sourceTargetScale) = config.ScaleMode switch
        {
            ScaleMode.None => (null, null, Vector2.One),
            ScaleMode.Scale => this.AnimateScale(
                sourceActualSize,
                targetActualSize,
                duration,
                easingType,
                easingMode),
            ScaleMode.ScaleX => this.AnimateScaleX(
                sourceActualSize,
                targetActualSize,
                duration,
                easingType,
                easingMode),
            ScaleMode.ScaleY => this.AnimateScaleY(
                sourceActualSize,
                targetActualSize,
                duration,
                easingType,
                easingMode),
            ScaleMode.Custom => this.AnimateScaleWithScaleCalculator(
                source,
                target,
                config.CustomScalingCalculator,
                duration,
                easingType,
                easingMode),
            _ => (null, null, Vector2.One),
        };

        controller.AddAnimationGroupFor(
            source,
            new[]
            {
                sourceTranslationAnimation,
                sourceOpacityAnimation,
                sourceScaleAnimation
            });
        controller.AddAnimationGroupFor(
            target,
            new[]
            {
                targetTranslationAnimation,
                targetOpacityAnimation,
                targetScaleAnimation
            });
        if (sourceAttachedElements?.Count > 0 && this.Target != null)
        {
            var targetControlBounds = new Rect(0, 0, this.Target.ActualWidth, this.Target.ActualHeight);
            var targetTransformedBounds = this.Target.TransformToVisual(this.Source).TransformBounds(targetControlBounds);
            var targetScale = sourceTargetScale;
            foreach (var coordinatedElement in sourceAttachedElements)
            {
                var coordinatedElementActualSize = coordinatedElement is FrameworkElement coordinatedFrameworkElement ? new Vector2((float)coordinatedFrameworkElement.ActualWidth, (float)coordinatedFrameworkElement.ActualHeight) : coordinatedElement.ActualSize;
                var coordinatedElementCenterPoint = coordinatedElementActualSize * config.NormalizedCenterPoint.ToVector2();
                coordinatedElement.GetVisual().CenterPoint = new Vector3(coordinatedElementCenterPoint, 0);
                controller.AddAnimationGroupFor(
                    coordinatedElement,
                    new[]
                    {
                        sourceTranslationAnimation,
                        sourceOpacityAnimation,
                        sourceScaleAnimation
                    });
                var initialLocation = coordinatedElement.TransformToVisual(this.Source).TransformPoint(default).ToVector2();
                var targetClip = GetTargetClip(initialLocation, coordinatedElementActualSize, coordinatedElementCenterPoint, targetScale, sourceTargetTranslation, targetTransformedBounds);
                if (targetClip.HasValue)
                {
                    controller.AddAnimationGroupFor(
                        coordinatedElement,
                        this.Clip(
                            targetClip.Value,
                            GetEasingFunctionFactory(easingType, easingMode),
                            duration: duration));
                }
            }
        }

        if (targetAttachedElements?.Count > 0 && this.Source != null)
        {
            var sourceControlBounds = new Rect(0, 0, this.Source.ActualWidth, this.Source.ActualHeight);
            var sourceTransformedBounds = this.Source.TransformToVisual(this.Target).TransformBounds(sourceControlBounds);
            var targetScale = GetInverseScale(sourceTargetScale);
            foreach (var coordinatedElement in targetAttachedElements)
            {
                var coordinatedElementActualSize = coordinatedElement is FrameworkElement coordinatedFrameworkElement ? new Vector2((float)coordinatedFrameworkElement.ActualWidth, (float)coordinatedFrameworkElement.ActualHeight) : coordinatedElement.ActualSize;
                var coordinatedElementCenterPoint = coordinatedElementActualSize * config.NormalizedCenterPoint.ToVector2();
                coordinatedElement.GetVisual().CenterPoint = new Vector3(coordinatedElementCenterPoint, 0);
                controller.AddAnimationGroupFor(
                    coordinatedElement,
                    new[]
                    {
                        targetTranslationAnimation,
                        targetOpacityAnimation,
                        targetScaleAnimation
                    });
                var initialLocation = coordinatedElement.TransformToVisual(this.Target).TransformPoint(default).ToVector2();
                var targetClip = GetTargetClip(initialLocation, coordinatedElementActualSize, coordinatedElementCenterPoint, targetScale, -sourceTargetTranslation, sourceTransformedBounds);
                if (targetClip.HasValue)
                {
                    controller.AddAnimationGroupFor(
                        coordinatedElement,
                        this.Clip(
                            default,
                            GetEasingFunctionFactory(easingType, easingMode),
                            from: targetClip.Value,
                            duration: duration));
                }
            }
        }

        if (config.EnableClipAnimation)
        {
            var (sourceClipAnimationGroup, targetClipAnimationGroup) = this.AnimateClip(
                sourceActualSize,
                targetActualSize,
                sourceCenterPoint,
                targetCenterPoint,
                sourceTargetScale,
                duration,
                easingType,
                easingMode);
            if (sourceClipAnimationGroup is not null)
            {
                controller.AddAnimationGroupFor(source, sourceClipAnimationGroup);
            }

            if (targetClipAnimationGroup is not null)
            {
                controller.AddAnimationGroupFor(target, targetClipAnimationGroup);
            }
        }
    }

    private Task AnimateIndependentElements(
        IEnumerable<UIElement> independentElements,
        bool isShow,
        CancellationToken token,
        TimeSpan? startTime,
        EasingType easingType,
        EasingMode easingMode)
    {
        if (independentElements?.ToArray() is not { Length: > 0 } elements)
        {
            return Task.CompletedTask;
        }

        var controller = new KeyFrameAnimationGroupController();
        var duration = isShow ? this.IndependentElementShowDuration : this.IndependentElementHideDuration;
        var delay = isShow ? this.IndependentElementShowDelay : TimeSpan.Zero;
        var opacityFrom = isShow ? 0 : 1;
        var opacityTo = isShow ? 1 : 0;
        foreach (var item in elements)
        {
            if (startTime.HasValue && delay < startTime)
            {
                if (isShow)
                {
                    RestoreElement(item);
                }
                else
                {
                    item.GetVisual().Opacity = 0;
                }
            }
            else
            {
                var independentTranslation = GetIndependentTranslation(item) ?? this.DefaultIndependentTranslation;
                var translationFrom = isShow ? independentTranslation.ToVector2() : Vector2.Zero;
                var translationTo = isShow ? Vector2.Zero : independentTranslation.ToVector2();
                var useDelay = delay - (startTime ?? TimeSpan.Zero);
                if (Math.Abs(independentTranslation.X) > AlmostZero ||
                    Math.Abs(independentTranslation.Y) > AlmostZero)
                {
                    controller.AddAnimationFor(item, this.Translation(
                        translationTo,
                        GetEasingFunctionFactory(easingType, easingMode),
                        startTime.HasValue ? null : translationFrom,
                        useDelay,
                        duration: duration));
                }

                controller.AddAnimationFor(item, this.Opacity(
                    opacityTo,
                    GetEasingFunctionFactory(easingType, easingMode),
                    startTime.HasValue ? null : opacityFrom,
                    useDelay,
                    duration));
            }

            if (isShow)
            {
                delay += this.IndependentElementShowInterval;
            }
        }

        return controller.StartAsync(token, null);
    }

    private (IKeyFrameCompositionAnimationFactory, IKeyFrameCompositionAnimationFactory, Vector2) AnimateTranslation(
        UIElement source,
        UIElement target,
        Vector2 sourceCenterPoint,
        Vector2 targetCenterPoint,
        TimeSpan duration,
        EasingType easingType,
        EasingMode easingMode)
    {
        var translation = target.TransformToVisual(source).TransformPoint(default).ToVector2() - sourceCenterPoint + targetCenterPoint;
        return (this.Translation(translation, GetEasingFunctionFactory(easingType, easingMode), Vector2.Zero, duration: duration),
            this.Translation(Vector2.Zero, GetEasingFunctionFactory(easingType, easingMode), -translation, duration: duration),
            translation);
    }

    private (IKeyFrameCompositionAnimationFactory, IKeyFrameCompositionAnimationFactory, Vector2) AnimateScale(
        Vector2 sourceActualSize,
        Vector2 targetActualSize,
        TimeSpan duration,
        EasingType easingType,
        EasingMode easingMode)
    {
        var scaleX = targetActualSize.X / sourceActualSize.X;
        var scaleY = targetActualSize.Y / sourceActualSize.Y;
        var scale = new Vector2(scaleX, scaleY);
        var (sourceFactory, targetFactory) = this.AnimateScaleImp(scale, duration, easingType, easingMode);
        return (sourceFactory, targetFactory, scale);
    }

    private (IKeyFrameCompositionAnimationFactory, IKeyFrameCompositionAnimationFactory, Vector2) AnimateScaleX(
        Vector2 sourceActualSize,
        Vector2 targetActualSize,
        TimeSpan duration,
        EasingType easingType,
        EasingMode easingMode)
    {
        var scaleX = targetActualSize.X / sourceActualSize.X;
        var scale = new Vector2(scaleX, scaleX);
        var (sourceFactory, targetFactory) = this.AnimateScaleImp(scale, duration, easingType, easingMode);
        return (sourceFactory, targetFactory, scale);
    }

    private (IKeyFrameCompositionAnimationFactory, IKeyFrameCompositionAnimationFactory, Vector2) AnimateScaleY(
        Vector2 sourceActualSize,
        Vector2 targetActualSize,
        TimeSpan duration,
        EasingType easingType,
        EasingMode easingMode)
    {
        var scaleY = targetActualSize.Y / sourceActualSize.Y;
        var scale = new Vector2(scaleY, scaleY);
        var (sourceFactory, targetFactory) = this.AnimateScaleImp(scale, duration, easingType, easingMode);
        return (sourceFactory, targetFactory, scale);
    }

    private (IKeyFrameCompositionAnimationFactory?, IKeyFrameCompositionAnimationFactory?, Vector2) AnimateScaleWithScaleCalculator(
        UIElement source,
        UIElement target,
        IScalingCalculator? scalingCalculator,
        TimeSpan duration,
        EasingType easingType,
        EasingMode easingMode)
    {
        if (scalingCalculator is null)
        {
            return (null, null, Vector2.One);
        }

        var scale = scalingCalculator.GetScaling(source, target);
        var (sourceFactory, targetFactory) = this.AnimateScaleImp(scale, duration, easingType, easingMode);
        return (sourceFactory, targetFactory, scale);
    }

    private (IKeyFrameCompositionAnimationFactory, IKeyFrameCompositionAnimationFactory) AnimateScaleImp(
        Vector2 targetScale,
        TimeSpan duration,
        EasingType easingType,
        EasingMode easingMode)
    {
        return (this.Scale(targetScale, GetEasingFunctionFactory(easingType, easingMode), Vector2.One, duration: duration),
            this.Scale(Vector2.One, GetEasingFunctionFactory(easingType, easingMode), GetInverseScale(targetScale), duration: duration));
    }

    private (IKeyFrameCompositionAnimationFactory, IKeyFrameCompositionAnimationFactory) AnimateOpacity(TimeSpan duration, Point opacityTransitionProgressKey)
    {
        var normalKey = (float)opacityTransitionProgressKey.X;
        var normalKeyForTarget = Math.Clamp(normalKey - 0.1f, 0, 1);
        var sourceNormalizedKeyFrames = new Dictionary<float, (float, IEasingFunctionFactory?)>
        {
            [GetOpacityTransitionStartKey(normalKey)] = (1, null),
            [GetOpacityTransitionEndKey(normalKey)] = (0, GetEasingFunctionFactory(EasingType.Cubic, EasingMode.EaseIn))
        };
        var targetNormalizedKeyFrames = new Dictionary<float, (float, IEasingFunctionFactory?)>
        {
            [GetOpacityTransitionStartKey(normalKeyForTarget)] = (0, null),
            [GetOpacityTransitionEndKey(normalKeyForTarget)] = (1, GetEasingFunctionFactory(EasingType.Cubic, EasingMode.EaseOut))
        };

        var reversedKey = (float)(1 - opacityTransitionProgressKey.Y);
        var reversedKeyForSource = Math.Clamp(reversedKey + 0.1f, 0, 1);
        var reversedSourceNormalizedKeyFrames = new Dictionary<float, (float, IEasingFunctionFactory?)>
        {
            [GetOpacityTransitionStartKey(reversedKeyForSource)] = (1, null),
            [GetOpacityTransitionEndKey(reversedKeyForSource)] = (0, GetEasingFunctionFactory(EasingType.Cubic, EasingMode.EaseIn, true))
        };
        var reversedTargetNormalizedKeyFrames = new Dictionary<float, (float, IEasingFunctionFactory?)>
        {
            [GetOpacityTransitionStartKey(reversedKey)] = (0, null),
            [GetOpacityTransitionEndKey(reversedKey)] = (1, GetEasingFunctionFactory(EasingType.Cubic, EasingMode.EaseOut, true)),
        };

        return (this.Opacity(0, null, 1, duration: duration, normalizedKeyFrames: sourceNormalizedKeyFrames, reversedNormalizedKeyFrames: reversedSourceNormalizedKeyFrames),
            this.Opacity(1, null, 0, duration: duration, normalizedKeyFrames: targetNormalizedKeyFrames, reversedNormalizedKeyFrames: reversedTargetNormalizedKeyFrames));
    }

    private (IKeyFrameCompositionAnimationFactory[]?, IKeyFrameCompositionAnimationFactory[]?) AnimateClip(
        Vector2 sourceActualSize,
        Vector2 targetActualSize,
        Vector2 sourceCenterPoint,
        Vector2 targetCenterPoint,
        Vector2 sourceTargetScale,
        TimeSpan duration,
        EasingType easingType,
        EasingMode easingMode)
    {
        var sourceToClip = GetTargetClip(-sourceCenterPoint, sourceActualSize, sourceCenterPoint, sourceTargetScale, default, new Rect((-targetCenterPoint).ToPoint(), targetActualSize.ToSize()));
        var targetFromClip = GetTargetClip(-targetCenterPoint, targetActualSize, targetCenterPoint, GetInverseScale(sourceTargetScale), default, new Rect((-sourceCenterPoint).ToPoint(), sourceActualSize.ToSize()));
        return (
            sourceToClip.HasValue
                ? this.Clip(
                    sourceToClip.Value,
                    GetEasingFunctionFactory(easingType, easingMode),
                    default,
                    duration: duration)
                : null,
            targetFromClip.HasValue
                ? this.Clip(
                    default,
                    GetEasingFunctionFactory(easingType, easingMode),
                    targetFromClip.Value,
                    duration: duration)
                : null
            );
    }
}
