using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Arsync.Prismatic.MinimalWindowDemo.ViewModels;
using ChromeTabs;
using ChromeTabs.Utilities;

namespace Arsync.Prismatic.MinimalWindowDemo.Views
{
    /// <summary>
    /// Interaction logic for HostWindow.xaml
    /// </summary>
    public partial class HostWindow
    {
        private static int _number;

        private TabViewModelBase _tabItem;

        public HostWindow()
        {
            Loaded += OnLoaded;

            _number++;

            Title = $"Window #{_number}";
            InitializeComponent();
        }

        private HostWindow(TabViewModelBase tabItem) : this()
        {
            _tabItem = tabItem;
            _tabItem.PrepareToWindowTransfer();
        }

        /// <summary>
        /// Open tab with given Prism navigation path from external call (see full-charged prismatic demo project).
        /// </summary>
        /// <param name="paths"></param>
        public void Open(params string[] paths)
        {
            if (!IsLoaded || !(DataContext is HostWindowViewModel vm))
                return;

            this.BringToFront();
            vm.Open(paths);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            LocationChanged += OnLocationChanged;

            if (_tabItem == null)
                return;

            var vm = (HostWindowViewModel) DataContext;

            vm.AttachTab(_tabItem);
            TabControl.GrabTabFixed(_tabItem);

            _tabItem = null;
            MoveWindow(this);
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            var win = (HostWindow) sender;

            if (!win.IsLoaded || !TabControl.IsTabDragging || TabControl.Items.Count > 1)
                return;

            var absolutePoint = MouseUtilities.GetScreenPosition();

            var windowUnder = win.FindWindowUnderAt(absolutePoint);

            if (windowUnder == null)
                return;

            // The screen position relative to the main window
            var relativePoint = windowUnder.PointFromScreen(absolutePoint);

            if (TryDockToWindow(windowUnder, relativePoint, TabControl.SelectedItem as TabViewModelBase))
                win.Close();
        }

        private void MoveWindow(Window window)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                window.BringToFront();

                var objectivePoint = PointToScreen(MouseUtilities.CorrectGetPosition(this));
                window.Top = objectivePoint.Y - TabControl.TabPanelHeight / 2;

                Debug.WriteLine(DateTime.Now.ToShortTimeString() + " dragging window");

                if (Mouse.LeftButton == MouseButtonState.Pressed)
                    window.DragMove();
            }));
        }

        private void OnMinimize(object sender, ExecutedRoutedEventArgs e) =>
            SystemCommands.MinimizeWindow(this);

        private void CanMaximize(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = WindowState != WindowState.Maximized;

        private void OnMaximize(object sender, ExecutedRoutedEventArgs e) =>
            SystemCommands.MaximizeWindow(this);

        private void CanRestore(object sender, CanExecuteRoutedEventArgs e) =>
            e.CanExecute = WindowState != WindowState.Normal;

        private void OnRestore(object sender, ExecutedRoutedEventArgs e) =>
            SystemCommands.RestoreWindow(this);

        private void OnClose(object sender, ExecutedRoutedEventArgs e) =>
            SystemCommands.CloseWindow(this);

        private void OnTabDraggedOutsideBounds(object sender, TabDragEventArgs e)
        {
            var draggedTab = e.Item as TabViewModelBase;

            if (TryDragTabToWindow(e.CursorPosition, draggedTab))
                e.Handled = true;
        }

        private bool TryDragTabToWindow(Point position, TabViewModelBase tab)
        {
            if (tab.IsPinned)
                return false;

            var win = new HostWindow(tab)
            {
                Width = Width,
                Height = Height,
                Left = Left,
                Top = position.Y - TabControl.TabPanelHeight / 2
            };

            win.Show();

            return true;
        }

        private bool TryDockToWindow(HostWindow window, Point position, TabViewModelBase tab)
        {
            if (!(window.TabControl.InputHitTest(position) is FrameworkElement element))
                return false;

            // Test if the mouse is over the tab panel or a tab item.
            if (!CanInsertTabItem(element))
                return false;

            tab.PrepareToWindowTransfer();

            var sourceHost = (HostWindowViewModel) DataContext;
            var targetHost = (HostWindowViewModel) window.DataContext;

            sourceHost.DetachTab(tab);
            targetHost.AttachTab(tab);

            window.BringToFront();

            //We run this method on the tab control for it to grab the tab and position it at the mouse, ready to move again.
            window.TabControl.GrabTab(tab);

            return true;
        }

        private static bool CanInsertTabItem(FrameworkElement element)
        {
            if (element is ChromeTabItem || element is ChromeTabPanel ||
                LogicalTreeHelper.GetChildren(element).OfType<ChromeTabPanel>().Any())
                return true;

            var localElement = element;

            while (true)
            {
                var parent = localElement?.TemplatedParent;

                if (parent == null)
                    break;

                if (parent is ChromeTabItem)
                    return true;

                localElement = parent as FrameworkElement;
            }

            return false;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (DataContext is HostWindowViewModel hvm)
                hvm.Dispose();
        }
    }
}
