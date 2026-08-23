using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace Score
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const int RestoreWindow = 9;
        private const string SingleInstanceName = "Local\\QuizScoreLive-8E9C9E4A-7E91-4A61-9F42-1C3D4AA4E0AB";
        private Mutex instanceMutex;
        private bool ownsInstanceMutex;
        private bool isHandlingFatalError;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
                instanceMutex = new Mutex(true, SingleInstanceName, out ownsInstanceMutex);
                if (!ownsInstanceMutex)
                {
                    BringExistingWindowToFront();
                    Shutdown(0);
                    return;
                }

                AppPaths.Initialize();
                AppDomain.CurrentDomain.SetData("DataDirectory", AppPaths.DataDirectory);
                AppDiagnostics.Write("Application starting. Version " + GetType().Assembly.GetName().Version + "; data directory: " + AppPaths.DataDirectory);
                NativeSqliteLoader.EnsureLoaded();
                DatabaseMaintenance.Prepare();

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
                AppDiagnostics.Write("Startup completed.");
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Startup failed.", ex);
                MessageBox.Show(ex.Message, "QuizScore Live could not start", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private static void BringExistingWindowToFront()
        {
            var windowHandle = FindWindow(null, "QuizScore Live");
            if (windowHandle == IntPtr.Zero) return;
            ShowWindow(windowHandle, RestoreWindow);
            SetForegroundWindow(windowHandle);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string className, string windowTitle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr windowHandle, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr windowHandle);

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                AppDiagnostics.Write("Application exiting with code " + e.ApplicationExitCode + ".");
                if (ownsInstanceMutex && instanceMutex != null) instanceMutex.ReleaseMutex();
                if (instanceMutex != null) instanceMutex.Dispose();
            }
            finally
            {
                base.OnExit(e);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppDiagnostics.Write("AppDomain unhandled exception: " + e.ExceptionObject);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppDiagnostics.WriteException("Dispatcher unhandled exception.", e.Exception);
            e.Handled = true;
            if (isHandlingFatalError) return;
            isHandlingFatalError = true;
            MessageBox.Show("QuizScore Live encountered an unexpected error and will close to protect the current session. Your saved scores remain on this computer.\n\nDiagnostic log:\n" + AppDiagnostics.LogFilePath, "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            AppDiagnostics.WriteException("Unobserved task exception.", e.Exception);
            e.SetObserved();
        }
    }
}
