// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#if WINAPPSDK
using CommunityToolkit.WinUI.UI;
#else
using Microsoft.Toolkit.Uwp.UI;
#endif

namespace CommunityToolkit.Labs.WinUI;
/// <summary>
/// Provide an abstraction around the Toolkit FrameworkElementExtensions for both UWP and WinUI 3 in the same namespace (until 8.0).
/// </summary>
/// <inheritdoc cref="FrameworkElementExtensions"/>
public static partial class FrameworkElementExtensions
{
    /// <summary>
    /// Retrieves the parent object of this framework element found of the given <see cref="AncestorTypeProperty"/>.
    /// </summary>
    /// <param name="obj">FrameworkElement</param>
    /// <returns>Parent object</returns>
    public static object GetAncestor(DependencyObject obj)
    {
        return obj.GetValue(AncestorProperty);
    }

    /// <summary>
    /// Sets the parent object of the framework element (internal).
    /// </summary>
    /// <param name="obj">Parent object</param>
    /// <param name="value">FrameworkElement</param>
    public static void SetAncestor(DependencyObject obj, object value)
    {
        obj.SetValue(AncestorProperty, value);
    }

    /// <summary>
    /// Attached <see cref="DependencyProperty"/> for retrieving a parent <see cref="object"/> for the <see cref="AncestorProperty"/>
    /// </summary>
    public static readonly DependencyProperty AncestorProperty =
        DependencyProperty.RegisterAttached("Ancestor", typeof(object), typeof(FrameworkElementExtensions), new PropertyMetadata(null));

    /// <summary>
    /// Gets the Type of Ancestor to look for from this element.
    /// </summary>
    /// <returns>Type of Ancestor to look for from this element</returns>
    public static Type GetAncestorType(DependencyObject obj)
    {
        return (Type)obj.GetValue(AncestorTypeProperty);
    }

    /// <summary>
    /// Sets the <see cref="Type"/> to look for from this element and place in the <see cref="AncestorProperty"/>.
    /// </summary>
    public static void SetAncestorType(DependencyObject obj, Type value)
    {
        obj.SetValue(AncestorTypeProperty, value);
    }

    /// <summary>
    /// Attached <see cref="DependencyProperty"/> for retrieving a parent <see cref="object"/> for the <see cref="AncestorProperty"/> based on the provided <see cref="Type"/> in the <see cref="AncestorTypeProperty"/>.
    /// </summary>
    public static readonly DependencyProperty AncestorTypeProperty =
        DependencyProperty.RegisterAttached("AncestorType", typeof(Type), typeof(FrameworkElementExtensions), new PropertyMetadata(null, AncestorType_PropertyChanged));

    private static void AncestorType_PropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is FrameworkElement fe)
        {
            fe.Loaded -= FrameworkElement_Loaded;

            if (args.NewValue != null)
            {
                fe.Loaded += FrameworkElement_Loaded;
                if (fe.Parent != null)
                {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                    FrameworkElement_Loaded(fe, null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                }
            }
        }
    }

    private static void FrameworkElement_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
#pragma warning disable CS8604 // Possible null reference argument.
            SetAncestor(fe, fe.FindAscendant(GetAncestorType(fe)));
#pragma warning restore CS8604 // Possible null reference argument.
        }
    }
}
