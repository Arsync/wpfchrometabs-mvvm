using Arsync.Prismatic.MinimalWindowDemo.Views;
using Prism.Ioc;
using System.Windows;

namespace Arsync.Prismatic.MinimalWindowDemo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }

        protected override Window CreateShell()
        {
            return Container.Resolve<HostWindow>();
        }
    }
}
