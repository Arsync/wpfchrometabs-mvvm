using System;
using System.Windows.Media;
using Prism.Mvvm;

namespace Arsync.Prismatic.MinimalWindowDemo.ViewModels
{
    public abstract class TabViewModelBase : BindableBase, IDisposable
    {
        private ImageSource _tabIcon;
        private string _tabHeader;
        private bool _isPinned;

        protected TabViewModelBase()
        {

        }

        public ImageSource TabIcon
        {
            get => _tabIcon;
            set => SetProperty(ref _tabIcon, value);
        }

        public string TabHeader
        {
            get => _tabHeader;
            set => SetProperty(ref _tabHeader, value);
        }

        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        public abstract void PrepareToWindowTransfer();

        protected virtual void Dispose(bool disposing)
        {

        }

        public void Dispose()
        {
            Dispose(true);
            // GC.SuppressFinalize(this);
        }
    }
}
