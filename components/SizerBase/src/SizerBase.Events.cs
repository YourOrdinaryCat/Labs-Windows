// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.Labs.WinUI;

/// <summary>
/// Event implementations for <see cref="SizerBase"/>.
/// </summary>
public partial class SizerBase
{
    /// <inheritdoc />
    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        // If we're manipulating with mouse/touch, we ignore keyboard inputs.
        if (_dragging)
        {
            return;
        }

        //// TODO: Do we want Ctrl/Shift to be a small increment (kind of inverse to old GridSplitter logic)?
        //// var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
        //// if (ctrl.HasFlag(CoreVirtualKeyStates.Down))
        //// Note: WPF doesn't do anything here.
        //// I think if we did anything, we'd create a SmallKeyboardIncrement property?

        // Initialize a drag event for this keyboard interaction.
        OnDragStarting();

        if (Orientation == Orientation.Vertical)
        {
            var horizontalChange = KeyboardIncrement;

            // Important: adjust for RTL language flow settings and invert horizontal axis
#if !HAS_UNO
            if (this.FlowDirection == FlowDirection.RightToLeft)
            {
                horizontalChange *= -1;
            }
#endif

            if (e.Key == Windows.System.VirtualKey.Left)
            {
                OnDragHorizontal(-horizontalChange);
            }
            else if (e.Key == Windows.System.VirtualKey.Right)
            {
                OnDragHorizontal(horizontalChange);
            }
        }
        else
        {
            if (e.Key == Windows.System.VirtualKey.Up)
            {
                OnDragVertical(-KeyboardIncrement);
            }
            else if (e.Key == Windows.System.VirtualKey.Down)
            {
                OnDragVertical(KeyboardIncrement);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnManipulationStarting(ManipulationStartingRoutedEventArgs e)
    {
        base.OnManipulationStarting(e);

        OnDragStarting();
    }

    /// <inheritdoc />
    protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
    {
        // We use Trancate here to provide 'snapping' points with the DragIncrement property
        // It works for both our negative and positive values, as otherwise we'd need to use
        // Ceiling when negative and Floor when positive to maintain the correct behavior.
        var horizontalChange =
            Math.Truncate(e.Cumulative.Translation.X / DragIncrement) * DragIncrement;
        var verticalChange =
            Math.Truncate(e.Cumulative.Translation.Y / DragIncrement) * DragIncrement;

        // Important: adjust for RTL language flow settings and invert horizontal axis
#if !HAS_UNO
        if (this.FlowDirection == FlowDirection.RightToLeft)
        {
            horizontalChange *= -1;
        }
#endif

        if (Orientation == Orientation.Vertical)
        {
            if (!OnDragHorizontal(horizontalChange))
            {
                return;
            }
        }
        else if (Orientation == Orientation.Horizontal)
        {
            if (!OnDragVertical(verticalChange))
            {
                return;
            }
        }

        base.OnManipulationDelta(e);
    }

    // private helper bools for Visual States
    private bool _pressed = false;
    private bool _dragging = false;
    private bool _pointerEntered = false;

    private void SizerBase_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _pressed = false;

        if (IsEnabled)
        {
            VisualStateManager.GoToState(this, _pointerEntered ? "PointerOver" : "Normal", true);
        }
    }

    private void SizerBase_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _pressed = true;

        if (IsEnabled)
        {
            VisualStateManager.GoToState(this, "Pressed", true);
        }
    }

    private void SizerBase_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _pointerEntered = false;

        if (!_pressed && !_dragging && IsEnabled)
        {
            VisualStateManager.GoToState(this, "Normal", true);
        }
    }

    private void SizerBase_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _pointerEntered = true;

        if (!_pressed && !_dragging && IsEnabled)
        {
            VisualStateManager.GoToState(this, "PointerOver", true);
        }
    }

    private void SizerBase_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        _dragging = false;
        _pressed = false;
        VisualStateManager.GoToState(this, _pointerEntered ? "PointerOver" : "Normal", true);
    }

    private void SizerBase_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        _dragging = true;
        VisualStateManager.GoToState(this, "Pressed", true);
    }

    private void SizerBase_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsEnabled)
        {
            VisualStateManager.GoToState(this, "Disabled", true);
        }
        else
        {
            VisualStateManager.GoToState(this, _pointerEntered ? "PointerOver" : "Normal", true);
        }
    }
}
