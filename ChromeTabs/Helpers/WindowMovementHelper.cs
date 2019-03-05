using System.Windows;
using System.Windows.Input;
using ChromeTabs.Utilities;

namespace ChromeTabs.Helpers
{
    public class WindowMovementHelper
    {
        private readonly Window _window;
        private bool _needsRestore;

        private WindowMovementHelper(Window window)
        {
            _window = window;

            if (_window.WindowState == WindowState.Maximized)
                _needsRestore = true;
        }

        public static WindowMovementHelper OnMouseLeftButtonDown(Window window, MouseButtonEventArgs e)
        {
            if (e == null)
                return null;

            if (e.ClickCount == 2)
            {
                if (window.ResizeMode == ResizeMode.CanResize ||
                    window.ResizeMode == ResizeMode.CanResizeWithGrip)
                {
                    switch (window.WindowState)
                    {
                        case WindowState.Normal:
                        {
                            window.WindowState = WindowState.Maximized;
                            break;
                        }

                        case WindowState.Maximized:
                        {
                            window.WindowState = WindowState.Normal;
                            break;
                        }
                    }
                }

                return null;
            }

            return new WindowMovementHelper(window);
        }

        public void OnMouseLeftButtonUp()
        {
            _needsRestore = false;
        }

        public void OnMouseMove(MouseEventArgs e)
        {
            if (!_needsRestore)
                return;

            _needsRestore = false;

            var position = e.GetPosition(_window);
            var bounds = _window.RestoreBounds;

            var percentHorizontal = position.X / _window.ActualWidth;
            var targetHorizontal = bounds.Width * percentHorizontal;

            var percentVertical = position.Y / _window.ActualHeight;
            var targetVertical = bounds.Height * percentVertical;

            var screenPosition = MouseUtilities.GetScreenPosition();

            _window.Left = screenPosition.X - targetHorizontal;
            _window.Top = screenPosition.Y - targetVertical;

            _window.WindowState = WindowState.Normal;

            _window.DragMove();
        }
    }
}
