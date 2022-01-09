using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace LayoutBrowser.RuntimeInstall
{
    public interface IRuntimeInstallWindowFactory
    {
        public RuntimeInstallWindow ForViewModel(RuntimeInstallWindowViewModel viewModel);
    }

    /// <summary>
    /// Interaction logic for RuntimeInstallWindow.xaml
    /// </summary>
    public partial class RuntimeInstallWindow
    {
        private readonly RuntimeInstallWindowViewModel viewModel;
        private readonly ILogger logger;

        public RuntimeInstallWindow(RuntimeInstallWindowViewModel viewModel, ILogger logger)
        {
            this.viewModel = viewModel;
            this.logger = logger;

            InitializeComponent();
        }

        public RuntimeInstallWindowViewModel ViewModel => viewModel;

        private void QuitButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static void OpenInBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        private void HyperlinkUrlNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenInBrowser(e.Uri.ToString());
        }

        private void HyperlinkRunProgram(object sender, RequestNavigateEventArgs e)
        {
            OpenInBrowser(e.Uri.PathAndQuery);
        }

        private async void InstallRuntimeClick(object sender, RoutedEventArgs e)
        {
            viewModel.StartInstall();

            string programPath = Assembly.GetEntryAssembly()?.Location;
            if (programPath == null)
            {
                throw new Exception("Program location not found");
            }

            string programDir = Path.GetDirectoryName(Path.GetFullPath(programPath));
            if (programDir == null)
            {
                throw new Exception("Program directory not found");
            }

            using Process process = Process.Start(programDir + "/WvSetup.exe", "/install");
            if (process == null)
            {
                throw new Exception("Couldn't run the installer");
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            process.EnableRaisingEvents = true;
            process.Exited += (_1, _2) => tcs.TrySetResult(process.ExitCode);

            if (process.HasExited)
            {
                tcs.TrySetResult(process.ExitCode);
            }

            int exitCode = await tcs.Task;
            logger.LogDebug($"WV2 installer exit code = {exitCode}");

            if (exitCode == 0 && RuntimeInstallWindowViewModel.IsRuntimeAvailable())
            {
                DialogResult = true;
                Close();
            }
            else
            {
                viewModel.InstallFailed();
            }
        }
    }
}
