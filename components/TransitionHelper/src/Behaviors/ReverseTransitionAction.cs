// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Xaml.Interactivity;

#if WINAPPSDK
using CommunityToolkit.WinUI.UI.Animations;
#else
using Microsoft.Toolkit.Uwp.UI.Animations;
#endif

namespace CommunityToolkit.Labs.WinUI;

/// <summary>
/// An <see cref="IAction"/> implementation that can trigger a target <see cref="TransitionHelper"/> instance.
/// </summary>
public sealed partial class ReverseTransitionAction : DependencyObject, IAction
{
    /// <summary>
    /// Gets or sets the linked <see cref="TransitionHelper"/> instance to reverse.
    /// </summary>
    public TransitionHelper Transition
    {
        get
        {
            return (TransitionHelper)this.GetValue(TransitionProperty);
        }

        set
        {
            this.SetValue(TransitionProperty, value);
        }
    }

    /// <summary>
    /// Identifies the <seealso cref="Transition"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TransitionProperty = DependencyProperty.Register(
        nameof(Transition),
        typeof(TransitionHelper),
        typeof(StartTransitionAction),
        new PropertyMetadata(null));

    /// <inheritdoc/>
    public object Execute(object sender, object parameter)
    {
        if (this.Transition is null)
        {
            throw new ArgumentNullException(nameof(this.Transition));
        }

        _ = this.Transition.ReverseAsync();

        return null!;
    }
}
