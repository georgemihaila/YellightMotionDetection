using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MotionDetection
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            App.Current.DispatcherUnhandledException += (se, ev) =>
            {
                //ev.Handled = true;
                //MessageBox.Show(ev.Exception.ToString());
            };
            base.OnStartup(e);
        }
    }
}
