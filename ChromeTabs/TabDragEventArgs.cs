using System.Windows;

namespace ChromeTabs
{
    public delegate void TabDragEventHandler(object sender, TabDragEventArgs e);

    public class TabDragEventArgs : RoutedEventArgs
    {
        public TabDragEventArgs(RoutedEvent routedEvent, object tabItem, Point cursorPosition)
            : base(routedEvent)
        {
            Item = tabItem;
            CursorPosition = cursorPosition;
        }

        public TabDragEventArgs(RoutedEvent routedEvent, object source, object tabItem, Point cursorPosition)
            : base(routedEvent, source)
        {
            Item = tabItem;
            CursorPosition = cursorPosition;
        }

        public object Item { get; set; }

        public Point CursorPosition { get; set; }
    }
}
