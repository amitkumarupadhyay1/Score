using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Score
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            WriteLog("App instance created.");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                Directory.CreateDirectory(dataDirectory);
                AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);
                WriteLog("DataDirectory set to: " + dataDirectory);
                base.OnStartup(e);
                WriteLog("OnStartup completed.");
            }
            catch (Exception ex)
            {
                WriteLog("OnStartup failed: " + ex);
                throw;
            }
        }

        private static void WriteLog(string message)
        {
            try
            {
                File.AppendAllText(LogFilePath, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteLog("AppDomain unhandled exception: " + e.ExceptionObject);
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteLog("DispatcherUnhandledException: " + e.Exception);
            e.Handled = true;
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteLog("UnobservedTaskException: " + e.Exception);
            e.SetObserved();
        }
    }
}
