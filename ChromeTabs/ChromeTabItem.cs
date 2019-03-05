using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChromeTabs
{
    public class ChromeTabItem : HeaderedContentControl
    {
        public static readonly DependencyProperty IsSelectedProperty =
            Selector.IsSelectedProperty.AddOwner(typeof(ChromeTabItem),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsParentMeasure |
                    FrameworkPropertyMetadataOptions.AffectsParentArrange,
                    OnIsSelectedChanged));

        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(ChromeTabItem),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsParentMeasure |
                    FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public static readonly DependencyProperty SelectedTabBrushProperty =
            DependencyProperty.Register(nameof(SelectedTabBrush), typeof(Brush), typeof(ChromeTabItem),
                new PropertyMetadata(Brushes.White));

        private DispatcherTimer _persistentTimer;

        static ChromeTabItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChromeTabItem),
                new FrameworkPropertyMetadata(typeof(ChromeTabItem)));

            CommandManager.RegisterClassCommandBinding(typeof(ChromeTabItem),
                new CommandBinding(CloseTabCommand, OnCloseTab));

            CommandManager.RegisterClassCommandBinding(typeof(ChromeTabItem),
                new CommandBinding(CloseAllTabsCommand, OnCloseAllTabs));

            CommandManager.RegisterClassCommandBinding(typeof(ChromeTabItem),
                new CommandBinding(CloseOtherTabsCommand, OnCloseOtherTabs));

            CommandManager.RegisterClassCommandBinding(typeof(ChromeTabItem),
                new CommandBinding(PinTabCommand, OnPinTab));
        }

        public ChromeTabItem()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            Loaded += OnLoaded;
        }

        // TODO: Add localization to RoutedUICommand!

        public static RoutedUICommand CloseTabCommand { get; } =
            new RoutedUICommand("Close tab", "CloseTab", typeof(ChromeTabItem));

        public static RoutedUICommand CloseAllTabsCommand { get; } =
            new RoutedUICommand("Close all tabs", "CloseAllTabs", typeof(ChromeTabItem));

        public static RoutedUICommand CloseOtherTabsCommand { get; } =
            new RoutedUICommand("Close other tabs", "CloseOtherTabs", typeof(ChromeTabItem));

        public static RoutedUICommand PinTabCommand { get; } =
            new RoutedUICommand("Pin Tab", "PinTab", typeof(ChromeTabItem));

        public bool IsSelected
        {
            get => (bool) GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public bool IsPinned
        {
            get => (bool) GetValue(IsPinnedProperty);
            set => SetValue(IsPinnedProperty, value);
        }

        public Brush SelectedTabBrush
        {
            get => (Brush) GetValue(SelectedTabBrushProperty);
            set => SetValue(SelectedTabBrushProperty, value);
        }

        public int Index => ParentTabControl?.GetTabIndex(this) ?? -1;

        private ChromeTabControl ParentTabControl =>
            ItemsControl.ItemsControlFromItemContainer(this) as ChromeTabControl;

        // TODO: Проверить, нужны ли GetIsSelected и SetIsSelected при наличии одноимённого свойства. 

        public static void SetIsSelected(DependencyObject item, bool value)
        {
            item.SetValue(IsSelectedProperty, value);
        }

        public static bool GetIsSelected(DependencyObject item)
        {
            return (bool)item.GetValue(IsSelectedProperty);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space || e.Key == Key.Return)
            {
                ParentTabControl.ChangeSelectedItem(this);
            }
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tabItem = (ChromeTabItem) d;

            if (tabItem.ParentTabControl?.TabPersistMode != TabPersistMode.Timed)
                return;

            if ((bool) e.NewValue)
                tabItem.StopPersistentTimer();
            else
                tabItem.StartPersistentTimer();
        }

        private static void OnCloseTab(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ChromeTabItem item)
                item.ParentTabControl.RemoveTab(item);
        }

        private static void OnCloseAllTabs(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ChromeTabItem item)
                item.ParentTabControl.RemoveAllTabs();
        }

        private static void OnCloseOtherTabs(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ChromeTabItem item)
                item.ParentTabControl.RemoveAllTabs(item.DataContext);
        }

        private static void OnPinTab(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ChromeTabItem item)
                item.ParentTabControl.PinTab(item.DataContext);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnUnloaded;
            StopPersistentTimer();
        }

        private void StartPersistentTimer()
        {
            StopPersistentTimer();

            _persistentTimer = new DispatcherTimer
            {
                Interval = ParentTabControl.TabPersistDuration
            };

            _persistentTimer.Tick += PersistentTimer_OnTick;
            _persistentTimer.Start();
        }

        private void StopPersistentTimer()
        {
            if (_persistentTimer == null)
                return;

            _persistentTimer.Stop();
            _persistentTimer.Tick -= PersistentTimer_OnTick;
            _persistentTimer = null;
        }

        private void PersistentTimer_OnTick(object sender, EventArgs e)
        {
            StopPersistentTimer();
            ParentTabControl?.RemoveFromItemHolder(this);
        }
    }
}
