using System;
using System.Linq;
using System.Threading.Tasks;
using LayoutBrowser.Tab;
using LayoutBrowser.Window;
using Microsoft.Extensions.Logging;
using Monitor.ServiceCommon.Services;

namespace LayoutBrowser.Layout;

public class ConsoleCommandProvider
{
    private readonly LayoutManager layoutMgr;
    private readonly ILogger logger;

    public ConsoleCommandProvider(ConsoleCommandService cmdSvc, LayoutManager layoutMgr, ILogger logger)
    {
        this.layoutMgr = layoutMgr;
        this.logger = logger;

        cmdSvc.AddCommand("save", DoSaveCmd, "Save current layout");
        cmdSvc.AddCommand("fquit", DoForceQuit, "Force crash the application");
        cmdSvc.AddCommand("find", DoFind, "Find window id by a fragment of URL or title. (Usage: find [url/title substring])");
        cmdSvc.AddCommand("wnd", DoWndInfo, "Print window info by its index. (Usage: wnd [index])");
#pragma warning disable 4014
        cmdSvc.AddCommand("refresh", p => DoRefresh(p), "Refresh all windows' native coordinates");
        cmdSvc.AddCommand("rsave", p => DoRefreshSave(p), "Save current layout after updating windows' native coordinates");
#pragma warning restore 4014
    }

    private void DoWndInfo(string pars)
    {
        WindowItem wnd = FindByIndex(pars);
        if (wnd == null)
        {
            return;
        }

        LayoutBrowserWindowViewModel vm = wnd.ViewModel;
        BrowserTabViewModel curTabVm = wnd.ViewModel.CurrentTab.ViewModel;
        logger.LogInformation($"Window #{wnd.ViewModel.Index} {wnd.ViewModel.Id}:\r\n" +
                              $"title = {curTabVm.Title}{(curTabVm.Title != curTabVm.BrowserTitle ? $" / {curTabVm.BrowserTitle}" : "")}\r\n" +
                              $"url = {curTabVm.UrlVm.Url}\r\n" +
                              $"native coords: left = {(int) vm.LeftNative}, top = {(int) vm.TopNative}, width = {(int) vm.WidthNative}, height = {(int) vm.HeightNative}\r\n" +
                              $"initial native coords: left = {(int) vm.LeftNativeInit}, top = {(int) vm.TopNativeInit}, width = {(int) vm.WidthNativeInit}, height = {(int) vm.HeightNativeInit}");
    }

    private void DoFind(string pars)
    {
        foreach (WindowItem wnd in layoutMgr.Windows)
        {
            if (wnd.ViewModel.CurrentTab == null)
            {
                continue;
            }

            BrowserTabViewModel vm = wnd.ViewModel.CurrentTab.ViewModel;
            if (vm.Title != null && vm.Title.Contains(pars) || 
                vm.BrowserTitle != null && vm.BrowserTitle.Contains(pars) || 
                vm.UrlVm?.Url != null && vm.UrlVm.Url.Contains(pars) ||
                vm.UrlVm?.BrowserSource != null && vm.UrlVm.BrowserSource.ToString().Contains(pars))
            {
                logger.LogInformation($"Window #{wnd.ViewModel.Index} {wnd.ViewModel.Id}:\r\n" +
                                      $"title = {vm.Title}{(vm.Title != vm.BrowserTitle ? $" / {vm.BrowserTitle}" : "")}\r\n" +
                                      $"url = {vm.UrlVm.Url}");
            }
        }
    }

    private void DoForceQuit(string pars = null)
    {
        Environment.FailFast("Force quit triggered by console command");
    }

    private WindowItem FindByIndex(string indexStr)
    {
        if (!Int32.TryParse(indexStr, out int windowIndex))
        {
            logger.LogInformation($"Failed to parse window index {windowIndex}");
            return null;
        }

        WindowItem wnd = layoutMgr.Windows.FirstOrDefault(w => w.ViewModel.Index == windowIndex);
        if (wnd == null)
        {
            logger.LogInformation($"Couldn't find window with index {windowIndex}");
            return null;
        }

        return wnd;
    }

    private async Task DoRefreshSave(string pars = null)
    {
        await DoRefresh();

        DoSaveCmd();
    }

    private void DoSaveCmd(string pars = null)
    {
        layoutMgr.SaveLayout();
            
        logger.LogInformation("Layout saved");
    }

    private async Task DoRefresh(string pars = null)
    {
        await layoutMgr.UpdateNativeSizes();

        logger.LogInformation("Native sizes refreshed");
    }
}