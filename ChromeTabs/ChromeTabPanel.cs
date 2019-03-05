using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ChromeTabs.Helpers;
using ChromeTabs.Utilities;

namespace ChromeTabs
{
    [ToolboxItem(false)]
    public class ChromeTabPanel : Panel
    {
        private const double StickyReanimateDuration = 0.10;
        private const double TabWidthSlidePercent = 0.5;

        private readonly object _locker = new object();
        private readonly Button _addButton;

        private readonly double _leftMargin;
        private readonly double _defaultMeasureHeight;

        private bool _isReleasingTab;
        private bool _hideAddButton;
        private Size _finalSize;

        private double _rightMargin;

        private double _currentTabWidth;

        private int _captureGuard;
        private int _originalIndex;
        private int _slideIndex;

        private List<double> _slideIntervals;

        private ChromeTabControl _parent;
        private ChromeTabItem _draggedTab;

        private Point _downPoint;
        private Point _downTabBoundsPoint;

        private Rect _addButtonRect;
        private Size _addButtonSize;
        
        private bool _isAddButtonEnabled;
        
        private DateTime _lastMouseDown;
        private bool _isTabDragging;

        private WindowMovementHelper _movementHelper;

        static ChromeTabPanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChromeTabPanel),
                new FrameworkPropertyMetadata(typeof(ChromeTabPanel)));
        }

        public ChromeTabPanel()
        {
            _leftMargin = 0.0;
            _rightMargin = 25.0;
            _defaultMeasureHeight = 30.0;

            var key = new ComponentResourceKey(typeof(ChromeTabPanel), "AddButtonStyle");
            var addButtonStyle = (Style) FindResource(key);

            _addButton = new Button
            {
                Style = addButtonStyle
            };

            _addButtonSize = new Size(20, 12);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public bool IsAddButtonEnabled
        {
            get => _isAddButtonEnabled;

            set
            {
                if (_isAddButtonEnabled == value)
                    return;

                _isAddButtonEnabled = value;
                _addButton.IsEnabled = value;

                if (ParentTabControl == null)
                    return;

                _addButton.Background = value == false
                    ? ParentTabControl.AddButtonDisabledBrush
                    : ParentTabControl.AddButtonBrush;

                InvalidateVisual();
            }
        }

        public bool IsTabDragging => _isTabDragging;

        protected double Overlap => ParentTabControl?.TabOverlap ?? ChromeTabControl.DefaultTabOverlap;

        protected double MinTabItemWidth => _parent?.MinTabWidth ?? ChromeTabControl.DefaultMinTabWidth;

        protected double MaxTabItemWidth => _parent?.MaxTabWidth ?? ChromeTabControl.DefaultMaxTabWidth;

        protected double PinnedTabWidth => _parent?.PinnedTabWidth ?? MinTabItemWidth;

        protected override int VisualChildrenCount => base.VisualChildrenCount + 1;

        private ChromeTabControl ParentTabControl
        {
            get
            {
                if (_parent != null)
                    return _parent;

                DependencyObject parent = this;

                do
                {
                    parent = VisualTreeHelper.GetParent(parent);
                }
                while (parent != null && !(parent is ChromeTabControl));

                return _parent = (ChromeTabControl) parent;
            }
        }

        internal void SetAddButtonControlTemplate(ControlTemplate template)
        {
            var style = new Style(typeof(Button));

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            _addButton.Style = style;
        }

        // Dragging by external source.
        internal void StartDragTabItem(ChromeTabItem tab = null, TabItemGrabMode grabMode = TabItemGrabMode.None)
        {
            var downPoint = MouseUtilities.CorrectGetPosition(this);

            if (tab != null)
            {
                UpdateLayout();

                double totalWidth = 0;

                for (var i = 0; i < tab.Index; i++)
                    totalWidth += GetTabItemWidth(Children[i] as ChromeTabItem) - Overlap;

                var xPos = totalWidth + GetTabItemWidth(tab) / 2;
                _downPoint = new Point(xPos, downPoint.Y);
            }
            else
            {
                _downPoint = downPoint;
            }

            StartDragTabItem(downPoint, tab, grabMode);
        }

        // Dragging by mouse.
        internal void StartDragTabItem(Point p, ChromeTabItem tab = null, TabItemGrabMode grabMode = TabItemGrabMode.None, MouseButtonEventArgs eventArgs = null)
        {
            _lastMouseDown = DateTime.UtcNow;

            if (tab == null)
                tab = GetTabItemFromMousePosition(_downPoint);

            // The mouse is not over a tab item, so just return.
            if (tab == null)
            {
                var win = Window.GetWindow(this);

                if (win == null)
                    return;

                _movementHelper = WindowMovementHelper.OnMouseLeftButtonDown(win, eventArgs);

                if (_movementHelper != null)
                {
                    win.DragMove();
                }
                else
                {
                    if (Mouse.LeftButton == MouseButtonState.Pressed)
                        Window.GetWindow(this)?.DragMove();
                }

                return;
            }

            _draggedTab = tab;
            _isTabDragging = true;

            if (grabMode == TabItemGrabMode.Fixed)
                return;

            if (Children.Count == 1
                && ParentTabControl.DragWindowWithOneTab
                && Mouse.LeftButton == MouseButtonState.Pressed
                && grabMode == TabItemGrabMode.None)
            {
                _draggedTab = null;
                Window.GetWindow(this)?.DragMove();
            }
            else
            {
                _downTabBoundsPoint = MouseUtilities.CorrectGetPosition(_draggedTab);

                SetZIndex(_draggedTab, 1000);
                ParentTabControl.ChangeSelectedItem(_draggedTab);

                if (grabMode == TabItemGrabMode.Sliding)
                    ProcessMouseMove(new Point(p.X + 0.1, p.Y));
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            SetTabItemsOnTabs();

            if (Children.Count > 0 && Children[0] is ChromeTabItem item)
                ParentTabControl.ChangeSelectedItem(item);

            if (ParentTabControl?.AddButtonTemplate != null)
                SetAddButtonControlTemplate(ParentTabControl.AddButtonTemplate);
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            lock (_locker)
            {
                if (_slideIntervals != null)
                    return;

                if (_addButtonRect.Contains(e.GetPosition(this)) && IsAddButtonEnabled)
                {
                    if (ParentTabControl != null)
                    {
                        _addButton.Background = ParentTabControl.AddButtonPressedBrush;
                        InvalidateVisual();
                    }

                    return;
                }

                var originalSource = e.OriginalSource as DependencyObject;
                var isButton = false;

                while (originalSource != null && originalSource.GetType() != typeof(ChromeTabPanel))
                {
                    var parent = VisualTreeHelper.GetParent(originalSource);

                    if (parent is Button)
                    {
                        isButton = true;
                        break;
                    }

                    originalSource = parent;
                }

                if (isButton)
                    return;

                _downPoint = e.GetPosition(this);
                StartDragTabItem(_downPoint, eventArgs: e);
            }
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            if (_movementHelper != null)
            {
                _movementHelper.OnMouseLeftButtonUp();
                _movementHelper = null;

                return;
            }

            OnTabRelease(e.GetPosition(this), IsMouseCaptured, false);
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (_movementHelper != null)
            {
                _movementHelper.OnMouseMove(e);
                _movementHelper = null;

                return;
            }

            var currentPoint = e.GetPosition(this);
            ProcessMouseMove(currentPoint);

            if (_draggedTab == null || DateTime.UtcNow.Subtract(_lastMouseDown).TotalMilliseconds < 50 || Children.Count == 1)
                return;

            // TODO: Разобраться с константой (+ 5) в isOutsideTabPanel

            var isOutsideTabPanel = currentPoint.X < -ParentTabControl.TabTearTriggerDistance
                                    || currentPoint.X > ActualWidth + ParentTabControl.TabTearTriggerDistance
                                    || currentPoint.Y < -ActualHeight
                                    || currentPoint.Y > ActualHeight + 5 + ParentTabControl.TabTearTriggerDistance;

            if (!isOutsideTabPanel || Mouse.LeftButton != MouseButtonState.Pressed)
                return;

            var content = _draggedTab.Content;

            var eventArgs = new TabDragEventArgs(ChromeTabControl.TabDraggedOutsideBoundsEvent, this, content,
                PointToScreen(currentPoint));

            RaiseEvent(eventArgs);

            var detachTab = eventArgs.Handled;
            OnTabRelease(currentPoint, IsMouseCaptured, detachTab, 0.01); //If we set it to 0 the completed event never fires, so we set it to a small decimal.
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (_draggedTab == null || Mouse.LeftButton == MouseButtonState.Pressed || _isReleasingTab)
                return;

            var p = e.GetPosition(this);
            Debug.WriteLine("Mouse Leave!");

            OnTabRelease(p, true, false);
        }

        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
            SetTabItemsOnTabs();
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index == VisualChildrenCount - 1)
                return _addButton;

            if (index < VisualChildrenCount - 1)
                return base.GetVisualChild(index);

            throw new IndexOutOfRangeException("Not enough visual children in the ArtTabPanel.");
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            UpdateAddButtonVisibility(finalSize);

            _rightMargin = ParentTabControl.IsAddButtonVisible ? 25 : 0;
            _finalSize = finalSize;

            var offset = _leftMargin;

            foreach (UIElement element in Children)
            {
                var item = (ChromeTabItem) ItemsControl.ContainerFromElement(ParentTabControl, element);

                if (item == null)
                    continue;

                var thickness = item.Margin.Bottom;
                var tabWidth = element.DesiredSize.Width;

                element.Arrange(new Rect(offset, 0, tabWidth, finalSize.Height - thickness));
                offset += tabWidth - Overlap;
            }

            if (ParentTabControl.IsAddButtonVisible)
            {
                _addButtonRect = new Rect(
                    new Point(offset + Overlap, (finalSize.Height - _addButtonSize.Height) / 2), _addButtonSize);

                _addButton.Arrange(_addButtonRect);
            }

            return finalSize;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            UpdateAddButtonVisibility(availableSize);

            var height = double.IsPositiveInfinity(availableSize.Height) ? _defaultMeasureHeight : availableSize.Height;
            var resultSize = new Size(0, availableSize.Height);

            foreach (UIElement element in Children)
            {
                var item = (ChromeTabItem) ItemsControl.ContainerFromElement(ParentTabControl, element);

                if (item == null)
                    continue;

                var tabSize = new Size(GetTabItemWidth(item), height - item.Margin.Bottom);

                element.Measure(tabSize);
                resultSize.Width += element.DesiredSize.Width - Overlap;
            }

            if (ParentTabControl.IsAddButtonVisible)
            {
                _addButton.Measure(_addButtonSize);
                resultSize.Width += _addButtonSize.Width;
            }

            return resultSize;
        }

        private void UpdateAddButtonVisibility(Size availableSize)
        {
            _currentTabWidth = CalculateTabWidth(availableSize);
            ParentTabControl.CanAddTabInternal = _currentTabWidth > MinTabItemWidth;

            if (_hideAddButton)
                _addButton.Visibility = Visibility.Hidden;
            else if (ParentTabControl.IsAddButtonVisible)
                _addButton.Visibility = _currentTabWidth > MinTabItemWidth ? Visibility.Visible : Visibility.Collapsed;
            else
                _addButton.Visibility = Visibility.Collapsed;
        }

        // Расставляет правильные индексы вкладкам.
        private void SetTabItemsOnTabs()
        {
            for (int i = 0, c = Children.Count; i < c; i++)
            {
                if (Children[i] is DependencyObject dp)
                {
                    var tabItem = (ChromeTabItem) ItemsControl.ContainerFromElement(ParentTabControl, dp);

                    if (tabItem != null)
                        KeyboardNavigation.SetTabIndex(tabItem, i);
                }
            }
        }

        private void RealignTabs()
        {
            for (int i = 0, c = Children.Count; i < c; i++)
            {
                var tab = (ChromeTabItem) Children[i];

                var offset = GetTabItemWidth(tab) - Overlap;
                tab.Margin = new Thickness(0);
            }
        }

        private ChromeTabItem GetTabItemFromMousePosition(Point mousePoint)
        {
            var source = VisualTreeHelper.HitTest(this, mousePoint)?.VisualHit;

            while (source != null && !Children.Contains(source as UIElement))
                source = VisualTreeHelper.GetParent(source);

            return source as ChromeTabItem;
        }

        private double GetTabItemWidth(ChromeTabItem item)
        {
            return item.IsPinned ? PinnedTabWidth : _currentTabWidth;
        }

        private double CalculateTabWidth(Size availableSize)
        {
            var activeWidth = double.IsPositiveInfinity(availableSize.Width)
                ? 500
                : availableSize.Width - _leftMargin - _rightMargin;

            var pinnedTabsCount = Children.Cast<ChromeTabItem>().Count(x => x.IsPinned);

            var totalPinnedTabsWidth = pinnedTabsCount > 0 ? pinnedTabsCount * PinnedTabWidth : 0;
            var totalUnpinnedTabsWidth = activeWidth + (Children.Count - 1) * Overlap - totalPinnedTabsWidth;

            return Math.Min(Math.Max(totalUnpinnedTabsWidth / (Children.Count - pinnedTabsCount), MinTabItemWidth), MaxTabItemWidth);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);

            if (window != null)
                window.Deactivated += OnDeactivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);

            if (window != null)
                window.Activated -= OnDeactivated;
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_draggedTab == null || IsMouseCaptured || _isReleasingTab || PresentationSource.FromVisual(this) == null)
                return;

            var p = MouseUtilities.CorrectGetPosition(this);
            OnTabRelease(p, true, false);
        }

        private double GetTabOffset()
        {
            if (_slideIntervals == null)
                return 0;

            var offset = 0.0;

            if (_slideIndex < _originalIndex + 1)
            {
                offset = _slideIntervals[_slideIndex + 1] -
                         GetTabItemWidth(_draggedTab) * (1 - TabWidthSlidePercent) + Overlap;
            }
            else if (_slideIndex > _originalIndex + 1)
            {
                offset = _slideIntervals[_slideIndex - 1] +
                         GetTabItemWidth(_draggedTab) * (1 - TabWidthSlidePercent) - Overlap;
            }

            return offset;
        }

        private void OnTabRelease(Point p, bool isDragging, bool detachTabOnRelease,
            double animationDuration = StickyReanimateDuration)
        {
            lock (_locker)
            {
                if (ParentTabControl != null && ParentTabControl.IsAddButtonVisible)
                {
                    if (_addButtonRect.Contains(p) && IsAddButtonEnabled)
                    {
                        _addButton.Background = ParentTabControl.AddButtonBrush;
                        InvalidateVisual();

                        if (_addButton.Visibility == Visibility.Visible)
                            ParentTabControl.AddTab();

                        return;
                    }
                }

                var offset = GetTabOffset();

                if (isDragging)
                {
                    Debug.WriteLine("Dragging!");

                    ReleaseMouseCapture();
                    var localSlideIndex = _slideIndex;

                    _isReleasingTab = Reanimate(_draggedTab, offset, animationDuration, () =>
                    {
                        if (_draggedTab == null)
                            return;

                        try
                        {
                            ParentTabControl.ChangeSelectedItem(_draggedTab);
                            var content = _draggedTab.Content;

                            _draggedTab.Margin = new Thickness(offset, 0, -offset, 0);
                            _draggedTab = null;

                            _isTabDragging = false;

                            _captureGuard = 0;
                            ParentTabControl.MoveTab(_originalIndex, Math.Max(0, localSlideIndex - 1));

                            _slideIntervals = null;
                            _addButton.Visibility = Visibility.Visible;
                            _hideAddButton = false;

                            InvalidateVisual();

                            if (detachTabOnRelease && ParentTabControl.DetachTabCommand != null)
                            {
                                Debug.WriteLine("Sent to close tab command");
                                ParentTabControl.DetachTabCommand.Execute(content);
                            }

                            // Fix for where sometimes tabs got stuck in the wrong position.
                            if (Children.Count > 1)
                                RealignTabs();
                        }
                        finally
                        {
                            _isReleasingTab = false;
                        }
                    });
                }
                else
                {
                    Debug.WriteLine("Positioning!");

                    if (_draggedTab != null)
                    {
                        ParentTabControl.ChangeSelectedItem(_draggedTab);
                        _draggedTab.Margin = new Thickness(offset, 0, -offset, 0);
                    }

                    _draggedTab = null;
                    
                    _isTabDragging = false;

                    _captureGuard = 0;
                    _slideIntervals = null;
                }
            }
        }

        private bool Reanimate(ChromeTabItem tab, double left, double duration, Action completeCallback)
        {
            if (tab == null)
                return false;

            var offset = new Thickness(left, 0, -left, 0);
            var moveBackAnimation =
                new ThicknessAnimation(tab.Margin, offset, new Duration(TimeSpan.FromSeconds(duration)));

            Storyboard.SetTarget(moveBackAnimation, tab);
            Storyboard.SetTargetProperty(moveBackAnimation, new PropertyPath(MarginProperty));

            var sb = new Storyboard
            {
                FillBehavior = FillBehavior.Stop,
                AutoReverse = false
            };

            sb.Completed += (s, e) =>
            {
                sb.Remove();
                completeCallback();
            };

            sb.Children.Add(moveBackAnimation);
            sb.Begin();

            return true;
        }

        private void StickyReanimate(ChromeTabItem tab, double left, double duration)
        {
            Reanimate(tab, left, duration, () =>
            {
                if (_draggedTab != null)
                    tab.Margin = new Thickness(left, 0, -left, 0);
            });
        }

        private void SwapSlideInterval(int index)
        {
            _slideIntervals[_slideIndex] = _slideIntervals[index];
            _slideIntervals[index] = 0;
        }

        private void ProcessMouseMove(Point p)
        {
            var currentPoint = p;

            if (ParentTabControl?.IsAddButtonVisible == true && IsAddButtonEnabled)
            {
                _addButton.Background = _addButtonRect.Contains(currentPoint)
                    ? ParentTabControl.AddButtonMouseOverBrush
                    : ParentTabControl.AddButtonBrush;

                InvalidateVisual();
            }

            if (_draggedTab == null || ParentTabControl?.CanMoveTabs != true)
                return;

            var insideTabPoint = TranslatePoint(p, _draggedTab);
            var margin = new Thickness(currentPoint.X - _downPoint.X, 0, _downPoint.X - currentPoint.X, 0);

            var guardValue = Interlocked.Increment(ref _captureGuard);

            if (guardValue == 1)
            {
                _draggedTab.Margin = margin;

                // We capture the mouse and start tab movement
                _originalIndex = _draggedTab.Index;
                _slideIndex = _originalIndex + 1;

                //Add slide intervals, the positions  where the tab slides over the next.
                _slideIntervals = new List<double>
                {
                    double.NegativeInfinity
                };

                for (int i = 1, c = Children.Count; i <= c; i++)
                {
                    var tab = Children[i - 1] as ChromeTabItem;
                    var tabWidth = GetTabItemWidth(tab);

                    var diff = i - _slideIndex;
                    var absDiff = Math.Abs(diff);

                    var sign = Math.Sign(diff);

                    var bound = Math.Min(1, absDiff) * (sign * tabWidth * TabWidthSlidePercent
                                                        + (absDiff < 2 ? 0 : (diff - sign) * (tabWidth - Overlap)));

                    _slideIntervals.Add(bound);
                }

                _slideIntervals.Add(double.PositiveInfinity);

#if DEBUG
                Dispatcher.BeginInvoke(new Action(() => Debug.WriteLine($"Has mouse capture = {CaptureMouse()}")));
#endif
            }
            else if (_slideIntervals != null)
            {
                if (insideTabPoint.X > 0 && currentPoint.X + (_draggedTab.ActualWidth - insideTabPoint.X) >= ActualWidth)
                    return;

                if (insideTabPoint.X < _downTabBoundsPoint.X && currentPoint.X - insideTabPoint.X <= 0)
                    return;

                _draggedTab.Margin = margin;

                // We return on small marging changes to avoid the tabs jumping around when quickly clicking between tabs.
                if (Math.Abs(_draggedTab.Margin.Left) < 10)
                    return;

                _addButton.Visibility = Visibility.Hidden;
                _hideAddButton = true;

                var changed = 0;

                var prevIndex = _slideIndex - 1;
                var currentIndex = _slideIndex;
                var postIndex = _slideIndex + 1;

                if (prevIndex >= 0 && prevIndex < _slideIntervals.Count && margin.Left < _slideIntervals[prevIndex])
                {
                    SwapSlideInterval(prevIndex);
                    currentIndex--;
                    changed = 1;
                }
                else if (postIndex >= 0 && postIndex < _slideIntervals.Count && margin.Left > _slideIntervals[postIndex])
                {
                    SwapSlideInterval(postIndex);
                    currentIndex++;
                    changed = -1;
                }

                if (changed == 0)
                    return;

                var rightedOriginalIndex = _originalIndex + 1;
                var diff = 1;

                if (changed > 0 && currentIndex >= rightedOriginalIndex)
                {
                    changed = 0;
                    diff = 0;
                }
                else if (changed < 0 && currentIndex <= rightedOriginalIndex)
                {
                    changed = 0;
                    diff = 2;
                }

                var index = currentIndex - diff;

                if (index < 0 || index >= Children.Count)
                    return;

                var shiftedTab = (ChromeTabItem) Children[index];

                if (shiftedTab.Equals(_draggedTab) || shiftedTab.IsPinned != _draggedTab.IsPinned)
                    return;

                var offset = changed * (GetTabItemWidth(_draggedTab) - Overlap);
                StickyReanimate(shiftedTab, offset, StickyReanimateDuration);

                _slideIndex = currentIndex;
            }
        }
    }
}
