namespace ChromeTabs
{
    public enum TabItemGrabMode
    {
        None,

        /// <summary>
        /// Для сохранения положения вкладки, которая была вытащена в отдельное окно.
        /// </summary>
        Fixed,

        /// <summary>
        /// Для вставки вкладки в окно.
        /// </summary>
        Sliding
    }
}