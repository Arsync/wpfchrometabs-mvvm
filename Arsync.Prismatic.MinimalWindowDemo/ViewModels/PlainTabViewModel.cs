namespace Arsync.Prismatic.MinimalWindowDemo.ViewModels
{
    public class PlainTabViewModel : TabViewModelBase
    {
        private static int _counter;

        public PlainTabViewModel()
        {
            TabNumber = _counter;
            _counter++;
        }

        public int TabNumber { get; }

        public override void PrepareToWindowTransfer()
        {
            // Do nothing at this time - its just a plain, non-prismatic tab.
        }
    }
}
