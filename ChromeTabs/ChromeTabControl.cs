using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ChromeTabs
{
    [TemplatePart(Name = "PART_ItemsHost", Type = typeof(Panel))]
    public class ChromeTabControl : Selector
    {
        public const double DefaultTabOverlap = 10.0;
        public const double DefaultMinTabWidth = 40.0;
        public const double DefaultMaxTabWidth = 125.0;

        public static readonly DependencyProperty TabPanelHeightProperty =
            DependencyProperty.Register(nameof(TabPanelHeight), typeof(double), typeof(ChromeTabControl),
                new PropertyMetadata(32.0));

        public static readonly DependencyProperty TabTearTriggerDistanceProperty =
            DependencyProperty.Register(nameof(TabTearTriggerDistance), typeof(double), typeof(ChromeTabControl),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty TabOverlapProperty =
            DependencyProperty.Register(nameof(TabOverlap), typeof(double), typeof(ChromeTabControl),
                new PropertyMetadata(DefaultTabOverlap));

        public static readonly DependencyProperty TabPersistModeProperty =
            DependencyProperty.Register(nameof(TabPersistMode), typeof(TabPersistMode), typeof(ChromeTabControl),
                new PropertyMetadata(TabPersistMode.None, OnTabItemPersistModePropertyChanged));

        public static readonly DependencyProperty TabPersistDurationProperty =
            DependencyProperty.Register(nameof(TabPersistDuration), typeof(TimeSpan), typeof(ChromeTabControl),
                new PropertyMetadata(TimeSpan.FromMinutes(30)));

        public static readonly DependencyProperty AddButtonTemplateProperty =
            DependencyProperty.Register(nameof(AddButtonTemplate), typeof(ControlTemplate), typeof(ChromeTabControl),
                new PropertyMetadata(null, OnAddButtonTemplatePropertyChanged));

        public static readonly DependencyProperty AddButtonBrushProperty =
            DependencyProperty.Register(nameof(AddButtonBrush), typeof(Brush), typeof(ChromeTabControl),
                new PropertyMetadata(Brushes.Transparent));

        public static readonly DependencyProperty AddButtonMouseOverBrushProperty =
            DependencyProperty.Register(nameof(AddButtonMouseOverBrush), typeof(Brush), typeof(ChromeTabControl),
                new PropertyMetadata(Brushes.White));

        public static readonly DependencyProperty AddButtonPressedBrushProperty =
            DependencyProperty.Register(nameof(AddButtonPressedBrush), typeof(Brush), typeof(ChromeTabControl),
                new PropertyMetadata(Brushes.DarkGray));
        
        public static readonly DependencyProperty AddButtonDisabledBrushProperty =
            DependencyProperty.Register(nameof(AddButtonDisabledBrush), typeof(Brush), typeof(ChromeTabControl),
                new PropertyMetadata(Brushes.DarkGray));

        public static readonly DependencyProperty IsAddButtonVisibleProperty =
            DependencyProperty.Register(nameof(IsAddButtonVisible), typeof(bool), typeof(ChromeTabControl),
                new PropertyMetadata(true, OnIsAddButtonVisibleChanged));

        public static readonly DependencyProperty SelectedTabBrushProperty =
            DependencyProperty.Register(nameof(SelectedTabBrush), typeof(Brush), typeof(ChromeTabControl),
                new PropertyMetadata(null, OnSelectedTabBrushPropertyChanged));

        public static readonly DependencyProperty AddTabCommandProperty =
            DependencyProperty.Register(nameof(AddTabCommand), typeof(ICommand), typeof(ChromeTabControl), 
                new PropertyMetadata(OnAddItemCommandPropertyChanged));

        public static readonly DependencyProperty AddTabCommandParameterProperty =
            DependencyProperty.Register(nameof(AddTabCommandParameter), typeof(object), typeof(ChromeTabControl));

        public static readonly DependencyProperty ReorderTabsCommandProperty =
            DependencyProperty.Register(nameof(ReorderTabsCommand), typeof(ICommand), typeof(ChromeTabControl));

        public static readonly DependencyProperty CloseTabCommandProperty =
            DependencyProperty.Register(nameof(CloseTabCommand), typeof(ICommand), typeof(ChromeTabControl));

        public static readonly DependencyProperty DetachTabCommandProperty =
            DependencyProperty.Register(nameof(DetachTabCommand), typeof(ICommand), typeof(ChromeTabControl));

        public static readonly DependencyProperty PinTabCommandProperty =
            DependencyProperty.Register(nameof(PinTabCommand), typeof(ICommand), typeof(ChromeTabControl));

        public static readonly DependencyProperty SelectedContentProperty =
            DependencyProperty.Register(nameof(SelectedContent), typeof(object), typeof(ChromeTabControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinTabWidthProperty =
            DependencyProperty.Register(nameof(MinTabWidth), typeof(double), typeof(ChromeTabControl),
                new PropertyMetadata(DefaultMinTabWidth, OnMinTabWidthPropertyChanged));

        public static readonly DependencyProperty MaxTabWidthProperty =
            DependencyProperty.Register(nameof(MaxTabWidth), typeof(double), typeof(ChromeTabControl),
                new PropertyMetadata(DefaultMaxTabWidth, null, OnCoerceMaxTabWidth));

        public static readonly DependencyProperty PinnedTabWidthProperty =
            DependencyProperty.Register(nameof(PinnedTabWidth), typeof(double), typeof(ChromeTabControl),
                new PropertyMetadata(DefaultMaxTabWidth, null, OnCoercePinnedTabWidth));

        public static readonly DependencyProperty DragWindowWithOneTabProperty =
            DependencyProperty.Register(nameof(DragWindowWithOneTab), typeof(bool), typeof(ChromeTabControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty CanMoveTabsProperty =
            DependencyProperty.Register(nameof(CanMoveTabs), typeof(bool), typeof(ChromeTabControl),
                new PropertyMetadata(true));

        public static readonly RoutedEvent TabDraggedOutsideBoundsEvent = EventManager.RegisterRoutedEvent(
            nameof(TabDraggedOutsideBounds), RoutingStrategy.Bubble, typeof(TabDragEventHandler), typeof(ChromeTabControl));

        public static readonly RoutedEvent ContainerItemPreparedForOverrideEvent = EventManager.RegisterRoutedEvent(
            nameof(ContainerItemPreparedForOverride), RoutingStrategy.Bubble, typeof(ContainerOverrideEventHandler), typeof(ChromeTabControl));

        private Panel _itemsHolder;
        private object _lastSelectedItem;

        private ConditionalWeakTable<object, DependencyObject> _objectToContainerMap;

        static ChromeTabControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChromeTabControl),
                new FrameworkPropertyMetadata(typeof(ChromeTabControl)));
        }

        public ChromeTabControl()
        {
            Loaded += OnLoaded;
        }

        // Provide CLR accessors for the event
        public event TabDragEventHandler TabDraggedOutsideBounds
        {
            add => AddHandler(TabDraggedOutsideBoundsEvent, value);
            remove => RemoveHandler(TabDraggedOutsideBoundsEvent, value);
        }

        // Provide CLR accessors for the event
        public event ContainerOverrideEventHandler ContainerItemPreparedForOverride
        {
            add => AddHandler(ContainerItemPreparedForOverrideEvent, value);
            remove => RemoveHandler(ContainerItemPreparedForOverrideEvent, value);
        }

        public double TabPanelHeight
        {
            get => (double) GetValue(TabPanelHeightProperty);
            set => SetValue(TabPanelHeightProperty, value);
        }

        /// <summary>
        /// The extra pixel distance you need to drag up or down the tab before it tears out.
        /// </summary>
        public double TabTearTriggerDistance
        {
            get => (double) GetValue(TabTearTriggerDistanceProperty);
            set => SetValue(TabTearTriggerDistanceProperty, value);
        }

        public double TabOverlap
        {
            get => (double) GetValue(TabOverlapProperty);
            set => SetValue(TabOverlapProperty, value);
        }

        public TabPersistMode TabPersistMode
        {
            get => (TabPersistMode) GetValue(TabPersistModeProperty);
            set => SetValue(TabPersistModeProperty, value);
        }

        public TimeSpan TabPersistDuration
        {
            get => (TimeSpan) GetValue(TabPersistDurationProperty);
            set => SetValue(TabPersistDurationProperty, value);
        }

        public ControlTemplate AddButtonTemplate
        {
            get => (ControlTemplate) GetValue(AddButtonTemplateProperty);
            set => SetValue(AddButtonTemplateProperty, value);
        }

        public Brush AddButtonBrush
        {
            get => (Brush) GetValue(AddButtonBrushProperty);
            set => SetValue(AddButtonBrushProperty, value);
        }

        public Brush AddButtonMouseOverBrush
        {
            get => (Brush) GetValue(AddButtonMouseOverBrushProperty);
            set => SetValue(AddButtonMouseOverBrushProperty, value);
        }

        public Brush AddButtonPressedBrush
        {
            get => (Brush) GetValue(AddButtonPressedBrushProperty);
            set => SetValue(AddButtonPressedBrushProperty, value);
        }

        public Brush AddButtonDisabledBrush
        {
            get => (Brush) GetValue(AddButtonDisabledBrushProperty);
            set => SetValue(AddButtonDisabledBrushProperty, value);
        }

        public bool IsAddButtonVisible
        {
            get => (bool) GetValue(IsAddButtonVisibleProperty);
            set => SetValue(IsAddButtonVisibleProperty, value);
        }

        public Brush SelectedTabBrush
        {
            get => (Brush)GetValue(SelectedTabBrushProperty);
            set => SetValue(SelectedTabBrushProperty, value);
        }

        public ICommand AddTabCommand
        {
            get => (ICommand) GetValue(AddTabCommandProperty);
            set => SetValue(AddTabCommandProperty, value);
        }

        public object AddTabCommandParameter
        {
            get => GetValue(AddTabCommandParameterProperty);
            set => SetValue(AddTabCommandParameterProperty, value);
        }

        public ICommand ReorderTabsCommand
        {
            get => (ICommand) GetValue(ReorderTabsCommandProperty);
            set => SetValue(ReorderTabsCommandProperty, value);
        }

        public ICommand CloseTabCommand
        {
            get => (ICommand) GetValue(CloseTabCommandProperty);
            set => SetValue(CloseTabCommandProperty, value);
        }

        public ICommand DetachTabCommand
        {
            get => (ICommand) GetValue(DetachTabCommandProperty);
            set => SetValue(DetachTabCommandProperty, value);
        }

        public ICommand PinTabCommand
        {
            get => (ICommand) GetValue(PinTabCommandProperty);
            set => SetValue(PinTabCommandProperty, value);
        }

        public object SelectedContent
        {
            get => GetValue(SelectedContentProperty);
            set => SetValue(SelectedContentProperty, value);
        }

        public double MinTabWidth
        {
            get => (double) GetValue(MinTabWidthProperty);
            set => SetValue(MinTabWidthProperty, value);
        }

        public double MaxTabWidth
        {
            get => (double) GetValue(MaxTabWidthProperty);
            set => SetValue(MaxTabWidthProperty, value);
        }

        public double PinnedTabWidth
        {
            get => (double)GetValue(PinnedTabWidthProperty);
            set => SetValue(PinnedTabWidthProperty, value);
        }

        public bool DragWindowWithOneTab
        {
            get => (bool) GetValue(DragWindowWithOneTabProperty);
            set => SetValue(DragWindowWithOneTabProperty, value);
        }

        public bool CanMoveTabs
        {
            get => (bool) GetValue(CanMoveTabsProperty);
            set => SetValue(CanMoveTabsProperty, value);
        }

        public bool IsTabDragging => ((ChromeTabPanel) ItemsHost).IsTabDragging;

        internal bool CanAddTabInternal { get; set; }

        protected Panel ItemsHost => (Panel) typeof(MultiSelector).InvokeMember(nameof(ItemsHost),
            BindingFlags.NonPublic | BindingFlags.GetProperty | BindingFlags.Instance,
            null, this, null);

        private ConditionalWeakTable<object, DependencyObject> ObjectToContainer =>
            _objectToContainerMap ?? (_objectToContainerMap = new ConditionalWeakTable<object, DependencyObject>());

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _itemsHolder = GetTemplateChild("PART_ItemsHolder") as Panel;
            SetSelectedContent(false);
        }

        /// <summary>
        /// Grabs hold of the tab based on the input viewmodel and positions it at the mouse cursor.
        /// </summary>
        /// <param name="viewModel"></param>
        public void GrabTab(object viewModel)
        {
            var p = (ChromeTabPanel) ItemsHost;
            var item = AsTabItem(viewModel);

            p.StartDragTabItem(item, TabItemGrabMode.Sliding);
        }

        public void GrabTabFixed(object viewModel)
        {
            var p = (ChromeTabPanel) ItemsHost;
            var item = AsTabItem(viewModel);

            p.StartDragTabItem(item, TabItemGrabMode.Fixed);
        }

        internal int GetTabIndex(ChromeTabItem item)
        {
            for (int i = 0, c = Items.Count; i < c; i++)
            {
                var tabItem = AsTabItem(Items[i]);

                if (Equals(tabItem, item))
                    return i;
            }

            return -1;
        }

        internal void ChangeSelectedIndex(int index)
        {
            if (Items.Count <= index)
                return;

            var item = AsTabItem(Items[index]);
            ChangeSelectedItem(item);
        }

        internal void ChangeSelectedItem(ChromeTabItem item)
        {
            var index = GetTabIndex(item);

            if (index != SelectedIndex)
            {
                if (index > -1)
                {
                    if (SelectedItem != null)
                        Panel.SetZIndex(AsTabItem(SelectedItem), 0);

                    SelectedIndex = index;
                    Panel.SetZIndex(item, 1001);
                }
            }

            if (SelectedContent == null && item != null)
                SetSelectedContent(false);
        }

        internal void MoveTab(int fromIndex, int toIndex)
        {
            if (Items.Count == 0 || fromIndex == toIndex || fromIndex >= Items.Count)
                return;

            var fromTab = Items[fromIndex];
            var toTab = Items[toIndex];

            var fromItem = AsTabItem(fromTab);
            var toItem = AsTabItem(toTab);

            if (fromItem.IsPinned != toItem.IsPinned)
                return;

            var tabReorder = new TabReorder(fromIndex, toIndex);

            if (ReorderTabsCommand != null && ReorderTabsCommand.CanExecute(tabReorder))
            {
                ReorderTabsCommand.Execute(tabReorder);
            }
            else
            {
                var sourceType = ItemsSource.GetType();

                if (sourceType.IsGenericType)
                {
                    var sourceDefinition = sourceType.GetGenericTypeDefinition();

                    if (sourceDefinition == typeof(ObservableCollection<>))
                    {
                        var method = sourceType.GetMethod("Move");
                        method?.Invoke(ItemsSource, new object[] { fromIndex, toIndex });
                    }
                }
            }

            // Re-arrange CollectionView after Move.
            Items.Refresh(); 

            for (int i = 0, c = Items.Count; i < c; i++)
            {
                var v = AsTabItem(Items[i]);
                v.Margin = new Thickness(0);
            }

            SelectedItem = fromTab;
        }

        internal void AddTab()
        {
            if (!CanAddTabInternal)
                return;

            if (AddTabCommand?.CanExecute(null) != true)
                return;

            AddTabCommand?.Execute(null);
        }

        internal void RemoveTab(object item)
        {
            var removeItem = AsTabItem(item);

            if (CloseTabCommand?.CanExecute(removeItem.DataContext) != true)
                return;

            CloseTabCommand.Execute(removeItem.DataContext);
            RemoveFromItemHolder(removeItem);
        }

        internal void RemoveFromItemHolder(ChromeTabItem item)
        {
            if (_itemsHolder == null)
                return;

            var presenter = FindChildContentPresenter(item);

            if (presenter == null)
                return;

            _itemsHolder.Children.Remove(presenter);
            Debug.WriteLine("Removing cached ContentPresenter");
        }

        internal void RemoveAllTabs(object exceptThis = null)
        {
            var objects = ItemsSource.Cast<object>().Where(x => x != exceptThis).ToList();

            foreach (var obj in objects)
            {
                if (CloseTabCommand?.CanExecute(obj) == true)
                    CloseTabCommand.Execute(obj);
            }
        }

        internal void PinTab(object item)
        {
            var tab = AsTabItem(item);

            if (PinTabCommand?.CanExecute(tab.DataContext) == true)
                PinTabCommand.Execute(tab.DataContext);
        }

        protected ChromeTabItem AsTabItem(object item)
        {
            switch (item)
            {
                case null:
                    return null;

                case ChromeTabItem tabItem:
                    return tabItem;
            }

            ObjectToContainer.TryGetValue(item, out var dp);
            return dp as ChromeTabItem;
        }

        protected void SetSelectedContent(bool removeContent)
        {
            // Экспериментальный "отсекатель".
            // Убрать, если вкладки при запуске начнут вести себя странно.
            //
            if (!IsLoaded)
                return;

            if (removeContent)
            {
                if (SelectedItem == null)
                {
                    if (Items.Count > 0)
                    {
                        SelectedItem = _lastSelectedItem ?? Items[0];
                    }
                    else
                    {
                        SelectedItem = null;
                        SelectedContent = null;
                    }
                }

                return;
            }

            if (SelectedIndex > 0)
            {
                _lastSelectedItem = Items[SelectedIndex - 1];
            }
            else if (SelectedIndex == 0 && Items.Count > 1)
            {
                _lastSelectedItem = Items[SelectedIndex + 1];
            }
            else
            {
                _lastSelectedItem = null;
            }

            var item = AsTabItem(SelectedItem);

            if (TabPersistMode != TabPersistMode.None)
            {
                if (item != null && _itemsHolder != null)
                {
                    CreateChildContentPresenter(SelectedItem); 

                    foreach (ContentPresenter child in _itemsHolder.Children)
                    {
                        var childTabItem = AsTabItem(child.Content);
                        child.Visibility = childTabItem.IsSelected ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }

            // TODO: Убрать отдельное свойтсво SelectedContent, пересоздавать каждый раз в _itemsHolder через новый ContentPresenter!
            // Чтобы избежать кэширования темплейта при TabPersistMode = None (из-за этого проблемы с навигацией в Prism).

            SelectedContent = item?.Content;
        }

        protected void SetChildrenZ()
        {
            var zIndex = Items.Count - 1;

            foreach (var item in Items)
            {
                var tabItem = AsTabItem(item);

                if (tabItem == null)
                    continue;

                Panel.SetZIndex(tabItem, ChromeTabItem.GetIsSelected(tabItem) ? Items.Count : zIndex);
                zIndex--;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            var tab = new ChromeTabItem();

            if (SelectedTabBrush != null)
                tab.SelectedTabBrush = SelectedTabBrush;

            return tab;
        }

        protected override bool IsItemItsOwnContainerOverride(object item) => item is ChromeTabItem;

        protected override void PrepareContainerForItemOverride(DependencyObject d, object item)
        {
            base.PrepareContainerForItemOverride(d, item);

            if (!Equals(d, item))
            {
                ObjectToContainer.Remove(item);
                ObjectToContainer.Add(item, d);

                SetChildrenZ();
            }

            RaiseEvent(new ContainerOverrideEventArgs(ContainerItemPreparedForOverrideEvent, this, item, AsTabItem(d)));
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            SetInitialSelection();
            KeyboardNavigation.SetIsTabStop(this, false);
        }

        protected void SetInitialSelection()
        {
            bool? somethingSelected = null;

            foreach (var element in Items)
            {
                if (element is DependencyObject o)
                    somethingSelected |= ChromeTabItem.GetIsSelected(o);
            }

            if (somethingSelected.HasValue && somethingSelected.Value == false)
                SelectedIndex = 0;
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);

            if (_itemsHolder != null)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Replace:
                    case NotifyCollectionChangedAction.Reset:
                        {
                            var itemsToRemove = _itemsHolder.Children.Cast<ContentPresenter>()
                                .Where(x => !Items.Contains(x.Content)).ToList();

                            foreach (var item in itemsToRemove)
                                _itemsHolder.Children.Remove(item);
                        }
                        break;

                    // Управление вынесено во внешнюю ViewModel:
                    // при добавлении вкладки установить SelectedItem для активации.
                    //
                    //case NotifyCollectionChangedAction.Add:
                    //    {
                    //        // Don't do anything with new items not created by the add button, because we don't want to
                    //        // create visuals that aren't being shown.
                    //        //
                    //        if (_addTabButtonClicked && TabAddMode == TabAddMode.NewTab)
                    //        {
                    //            _addTabButtonClicked = false;

                    //            if (e.NewItems != null)
                    //                ChangeSelectedItem(AsTabItem(e.NewItems.Cast<object>().Last()));
                    //        }
                    //    }
                    //    break;

                    case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                        {
                            foreach (var item in e.OldItems)
                            {
                                var presenter = FindChildContentPresenter(item);

                                if (presenter != null)
                                    _itemsHolder.Children.Remove(presenter);
                            }
                        }
                        break;
                }
            }

            SetSelectedContent(Items.Count == 0);
            SetChildrenZ();
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            SetChildrenZ();
            SetSelectedContent(e.AddedItems.Count == 0);
        }

        private static void OnTabItemPersistModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ChromeTabControl) d;

            if ((TabPersistMode) e.NewValue == TabPersistMode.None)
                control._itemsHolder.Children.Clear();
            else
                control.SetSelectedContent(false);
        }

        private static void OnMinTabWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ChromeTabControl) d;

            control.CoerceValue(PinnedTabWidthProperty);
            control.CoerceValue(MaxTabWidthProperty);
        }

        private static object OnCoerceMaxTabWidth(DependencyObject d, object baseValue)
        {
            var control = (ChromeTabControl) d;

            if ((double) baseValue <= control.MinTabWidth)
                return control.MinTabWidth + 1;

            return baseValue;
        }

        private static object OnCoercePinnedTabWidth(DependencyObject d, object baseValue)
        {
            var control = (ChromeTabControl) d;

            if (control.MinTabWidth > (double)baseValue)
                return control.MinTabWidth;

            return baseValue;
        }

        private static void OnAddButtonTemplatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ChromeTabControl) d;

            var panel = control.ItemsHost as ChromeTabPanel;
            panel?.SetAddButtonControlTemplate((ControlTemplate) e.NewValue);
        }

        private static void OnIsAddButtonVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(d))
                return;

            var control = (ChromeTabControl) d;
            var panel = (ChromeTabPanel) control.ItemsHost;

            panel?.InvalidateVisual();
        }

        private static void OnSelectedTabBrushPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ChromeTabControl) d;

            if (e.NewValue != null && control.SelectedItem != null)
                control.AsTabItem(control.SelectedItem).SelectedTabBrush = (Brush) e.NewValue;
        }

        private static void OnAddItemCommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(d))
                return;

            var control = (ChromeTabControl) d;

            if (e.NewValue != null)
            {
                var command = (ICommand) e.NewValue;
                command.CanExecuteChanged += control.CanAddChanged;
            }

            if (e.OldValue != null)
            {
                var command = (ICommand) e.OldValue;
                command.CanExecuteChanged -= control.CanAddChanged;
            }
        }

        private void CanAddChanged(object sender, EventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (ItemsHost is ChromeTabPanel panel && AddTabCommand != null)
                panel.IsAddButtonEnabled = AddTabCommand.CanExecute(AddTabCommandParameter);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ItemsHost is ChromeTabPanel panel && AddTabCommand != null)
                panel.IsAddButtonEnabled = AddTabCommand.CanExecute(AddTabCommandParameter);
        }

        private ContentPresenter FindChildContentPresenter(object data)
        {
            if (data is ChromeTabItem tabItem)
                data = tabItem.Content;

            if (data == null || _itemsHolder == null)
                return null;

            foreach (ContentPresenter presenter in _itemsHolder.Children)
            {
                if (presenter.Content == data)
                    return presenter;
            }

            return null;
        }

        private ContentPresenter CreateChildContentPresenter(object item)
        {
            if (item == null)
                return null;

            var presenter = FindChildContentPresenter(item);

            if (presenter != null)
                return presenter;

            presenter = new ContentPresenter
            {
                Content = item is ChromeTabItem tabItem ? tabItem.Content : item,
                Visibility = Visibility.Collapsed
            };

            _itemsHolder.Children.Add(presenter);

            return presenter;
        }
    }
}
