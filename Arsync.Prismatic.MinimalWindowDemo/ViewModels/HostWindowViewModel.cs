using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DryIoc;
using Prism.Commands;
using Prism.Mvvm;

namespace Arsync.Prismatic.MinimalWindowDemo.ViewModels
{
    public class HostWindowViewModel : BindableBase, IDisposable
    {
        private readonly IContainer _container;

        private string _title = "Prism Application";
        private TabViewModelBase _selectedTab;

        private DelegateCommand _createTabCommand;
        private DelegateCommand<TabViewModelBase> _closeTabCommand;
        private DelegateCommand<TabViewModelBase> _detachTabCommand;

        public HostWindowViewModel(IContainer container)
        {
            _container = container;
            TabItems = new ObservableCollection<TabViewModelBase>();
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ObservableCollection<TabViewModelBase> TabItems { get; }

        public TabViewModelBase SelectedTab
        {
            get => _selectedTab;
            set => SetProperty(ref _selectedTab, value);
        }

        public ICommand CreateTabCommand =>
            _createTabCommand ?? (_createTabCommand = new DelegateCommand(() =>
            {
                var tab = CreateTab();
                SelectedTab = tab; // Bring tab to the front.
            }));

        public ICommand CloseTabCommand =>
            _closeTabCommand ?? (_closeTabCommand = new DelegateCommand<TabViewModelBase>(OnCloseTab));

        public ICommand DetachTabCommand =>
            _detachTabCommand ?? (_detachTabCommand = new DelegateCommand<TabViewModelBase>(DetachTab));

        public void AttachTab(TabViewModelBase tab)
        {
            if (TabItems.Contains(tab))
                return;

            TabItems.Add(tab);
            SelectedTab = tab;
        }

        public void DetachTab(TabViewModelBase tab)
        {
            if (!TabItems.Contains(tab))
                return;

            TabItems.Remove(tab);
        }

        public void Open(string[] paths)
        {
            TabViewModelBase lastTab = null;

            foreach (var path in paths)
                lastTab = CreateTab(path);

            SelectedTab = lastTab;
        }

        private TabViewModelBase CreateTab(string path = null)
        {
            var tab = new PlainTabViewModel
            {
                TabHeader = DateTime.Now.ToString("T")
            };

            TabItems.Add(tab);

            return tab;
        }

        private void OnCloseTab(TabViewModelBase tab)
        {
            TabItems.Remove(tab);
            tab.Dispose();
        }

        public void Dispose()
        {
            foreach (var tab in TabItems)
                tab.Dispose();

            // TabItems.Clear();
        }
    }
}
