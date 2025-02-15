// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if WINAPPSDK
using CommunityToolkit.WinUI.UI;
#else
    using Microsoft.Toolkit.Uwp.UI;
#endif

using Windows.System;

namespace CommunityToolkit.Labs.WinUI;
public partial class TokenView : ListViewBase
{
    private void TokenView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollButtonsVisibility();
    }

    private void ScrollTabBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tokenViewScroller != null)
        {
            _tokenViewScroller.ChangeView(_tokenViewScroller.HorizontalOffset - _tokenViewScroller.ViewportWidth, null, null);
        }
    }
    
    private void ScrollTabForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tokenViewScroller != null)
        {
            _tokenViewScroller.ChangeView(_tokenViewScroller.HorizontalOffset + _tokenViewScroller.ViewportWidth, null, null);
        }
    }

    private void _tokenViewScroller_ViewChanging(object? sender, ScrollViewerViewChangingEventArgs e)
    {
#if !HAS_UNO
        if (_tokenViewScrollBackButton != null)
        {
            if (e.FinalView.HorizontalOffset < 1)
            {
                _tokenViewScrollBackButton.Visibility = Visibility.Collapsed;
            }
            else if (e.FinalView.HorizontalOffset > 1)
            {
                _tokenViewScrollBackButton.Visibility = Visibility.Visible;
            }
        }

        if (_tokenViewScrollForwardButton != null)
        {
            if (_tokenViewScroller != null)
            {
                if (e.FinalView.HorizontalOffset > _tokenViewScroller.ScrollableWidth - 1)
                {
                    _tokenViewScrollForwardButton.Visibility = Visibility.Collapsed;
                }
                else if (e.FinalView.HorizontalOffset < _tokenViewScroller.ScrollableWidth - 1)
                {
                    _tokenViewScrollForwardButton.Visibility = Visibility.Visible;
                }
            }
        }
#endif
    }

    private void ScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (_tokenViewScroller != null)
        {
            _tokenViewScroller.Loaded -= ScrollViewer_Loaded;
        }
        if (_tokenViewScrollBackButton != null)
        {
            _tokenViewScrollBackButton.Click -= ScrollTabBackButton_Click;
        }

        if (_tokenViewScrollForwardButton != null)
        {
            _tokenViewScrollForwardButton.Click -= ScrollTabForwardButton_Click;
        }

        if (_tokenViewScroller != null)
        {
            _tokenViewScroller.ViewChanging += _tokenViewScroller_ViewChanging;
            _tokenViewScrollBackButton = _tokenViewScroller.FindDescendant(TokenViewScrollBackButtonName) as ButtonBase;
            _tokenViewScrollForwardButton = _tokenViewScroller.FindDescendant(TokenViewScrollForwardButtonName) as ButtonBase;
        }

        if (_tokenViewScrollBackButton != null)
        {
            _tokenViewScrollBackButton.Click += ScrollTabBackButton_Click;
        }

        if (_tokenViewScrollForwardButton != null)
        {
            _tokenViewScrollForwardButton.Click += ScrollTabForwardButton_Click;
        }

        UpdateScrollButtonsVisibility();
    }

    private void Token_Removing(object? sender, TokenItemRemovingEventArgs e)
    {
        if (ItemFromContainer(e.TokenItem) is object item)
        {
            var args = new TokenItemRemovingEventArgs(item, e.TokenItem);
            TokenItemRemoving?.Invoke(this, args);

            if (ItemsSource != null)
            {
                _removeItemsSourceMethod?.Invoke(ItemsSource, new object[] { item });
            }
            else
            {
                if (_tokenViewScroller != null)
                {
                    _tokenViewScroller.UpdateLayout();
                }
                Items.Remove(item);
            }
        }
        UpdateScrollButtonsVisibility();
    }

    private void Token_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TokenItem token)
        {
            token.Loaded -= Token_Loaded;
        }
    }

    private void OnIsWrappedChanged()
    {
        if (_tokenViewScroller != null)
        {
            if (IsWrapped)
            {
                _tokenViewScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
            else
            {
                _tokenViewScroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
        }
    }

    private void TokenView_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left: e.Handled = MoveFocus(MoveDirection.Previous); break;
            case VirtualKey.Right: e.Handled = MoveFocus(MoveDirection.Next); break;
            case VirtualKey.Back:
            case VirtualKey.Delete: e.Handled = RemoveItem(); break;
        }
    }
}
