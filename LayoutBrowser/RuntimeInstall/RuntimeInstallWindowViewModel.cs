using System;
using Microsoft.Web.WebView2.Core;
using MonitorCommon;
using MvvmHelpers;

namespace LayoutBrowser.RuntimeInstall
{
    public class RuntimeInstallWindowViewModel : ObservableObject
    {
        private bool isInstalling;
        private bool isInstallFailed;

        public bool IsInstalling
        {
            get => isInstalling;
            set => SetProperty(ref isInstalling, value);
        }

        public bool IsInstallFailed
        {
            get => isInstallFailed;
            set => SetProperty(ref isInstallFailed, value);
        }

        public bool IsWin10 => Environment.OSVersion.Version.Major >= 10;

        public void StartInstall()
        {
            IsInstallFailed = false;
            IsInstalling = true;
        }

        public void InstallFailed()
        {
            IsInstalling = false;
            IsInstallFailed = true;
        }

        public static bool IsRuntimeAvailable()
        {
            string ver = null;
            try
            {
                ver = CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                // ignored
            }

            return !ver.IsNullOrEmpty();
        }
    }
}