namespace ChromeTabs
{
    public class TabReorder
    {
        public TabReorder(int fromIndex, int toIndex)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }

        public int FromIndex { get; set; }

        public int ToIndex { get; set; }
    }
}
