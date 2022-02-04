using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using LayoutBrowser.Layout;
using Microsoft.Web.WebView2.Wpf;
using MvvmHelpers;
using WpfAppCommon;
using WpfAppCommon.CollectionSegmenting;

namespace LayoutBrowser.Tab;

public interface IElementBlockerViewModelFactory
{
    ElementBlockerViewModel ForModel(ElementBlockingSettings? model);
}

public class ElementBlockerViewModel : ObservableObject, ITabFeatureViewModel
{
    private const string StartMsgType = "elementBlockStart";
    private const string InitMsgType = "elementBlockInit";
    private const string RulesChangedMsgType = "elementBlockRulesChanged";

    private static readonly string BlockingUserScript;

    private readonly ObservableCollection<ElementBlockerRuleItemViewModel> rules = new();
    private readonly CollectionManager<IElementBlockerMenuItem, IElementBlockerMenuItem> uiMenuItems = new(i => i);

    private bool enabled;
    private bool hasRules;

    private WebView2? webView;
    private WebView2MessagingService? msgr;

    public ElementBlockerViewModel(ElementBlockingSettings? model)
    {
        enabled = model?.enabled ?? false;

        foreach (ElementBlockingRule rule in model?.rules ?? Enumerable.Empty<ElementBlockingRule>())
        {
            AddRule(rule);
        }

        uiMenuItems.AddItem(new ElementBlockerEnabledItemViewModel(this), DefaultCollectionBlock.Front);
        uiMenuItems.AddItem(new ElementBlockerSeparatorItem(), DefaultCollectionBlock.Front);
        uiMenuItems.AddItemBlock(new TypeConvertingItemBlock<ElementBlockerRuleItemViewModel, IElementBlockerMenuItem>(rules));
        uiMenuItems.AddItem(new ElementBlockerAddNewButtonItem(this), DefaultCollectionBlock.Back);
    }

    public bool Enabled
    {
        get => enabled;
        set
        {
            bool prev = enabled;

            SetProperty(ref enabled, value);

            if (prev == value)
            {
                return;
            }

            OnEnabledChanged(value);
        }
    }

    public bool HasRules
    {
        get => hasRules;
        set => SetProperty(ref hasRules, value);
    }

    private void UpdateHasRules() => HasRules = rules.Any(r => r.Enabled);

    public ObservableCollection<ElementBlockerRuleItemViewModel> Rules => rules;

    public ObservableCollection<IElementBlockerMenuItem> UiMenuItems => uiMenuItems.Collection;

    public ElementBlockingSettings ToModel() => new()
    {
        enabled = enabled,
        rules = rules.Select(r => new ElementBlockingRule
        {
            enabled = r.Enabled,
            selector = r.Selector
        }).ToList()
    };

    private void OnEnabledChanged(bool enbl)
    {
        if (msgr == null)
        {
            return;
        }

        if (enbl)
        {
            msgr.PostJsonMessage(new RulesChangedMessage
            {
                type = RulesChangedMsgType,
                addedRules = EffectiveRules
            });
        }
        else
        {
            msgr.PostJsonMessage(new RulesChangedMessage
            {
                type = RulesChangedMsgType,
                removedRules = EnabledRules
            });
        }
    }

    private List<string> EnabledRules => rules.Filter(r => r.Enabled).Select(r => r.Selector).ToList();
    private List<string> EffectiveRules => enabled ? EnabledRules : Enumerable.Empty<string>().ToList();

    public void AddRule(ElementBlockingRule rule)
    {
        ElementBlockerRuleItemViewModel ruleVm = new(rule.enabled, rule.selector, OnRuleEnabledChanged, OnRuleSelectorChanged, this);

        rules.Add(ruleVm);

        UpdateHasRules();

        if (webView != null)
        {
            OnRuleAdded(ruleVm);
        }
    }

    private void OnRuleAdded(ElementBlockerRuleItemViewModel ruleVm)
    {
        if (!enabled || !ruleVm.Enabled || msgr == null)
        {
            return;
        }

        msgr.PostJsonMessage(new RulesChangedMessage
        {
            type = RulesChangedMsgType,
            addedRules = new List<string> { ruleVm.Selector }
        });
    }

    public void RemoveRule(ElementBlockerRuleItemViewModel ruleVm)
    {
        ruleVm.Dispose();

        int idx = rules.IndexOf(ruleVm);
        if (idx >= 0)
        {
            rules.RemoveAt(idx);
        }

        UpdateHasRules();

        OnRuleRemoved(ruleVm);
    }

    private void OnRuleRemoved(ElementBlockerRuleItemViewModel ruleVm)
    {
        if (!enabled || !ruleVm.Enabled || msgr == null)
        {
            return;
        }

        msgr.PostJsonMessage(new RulesChangedMessage
        {
            type = RulesChangedMsgType,
            removedRules = new List<string> { ruleVm.Selector }
        });
    }

    private void OnRuleSelectorChanged(string prevSelector, string curSelector, ElementBlockerRuleItemViewModel model)
    {
        if (!enabled || !model.Enabled || prevSelector == curSelector || msgr == null)
        {
            return;
        }

        msgr.PostJsonMessage(new RulesChangedMessage
        {
            type = RulesChangedMsgType,
            removedRules = new List<string> { prevSelector },
            addedRules = new List<string> { curSelector }
        });
    }

    private void OnRuleEnabledChanged(bool enbl, ElementBlockerRuleItemViewModel model)
    {
        UpdateHasRules();

        if (!enabled || msgr == null)
        {
            return;
        }

        if (enbl)
        {
            msgr.PostJsonMessage(new RulesChangedMessage
            {
                type = RulesChangedMsgType,
                addedRules = new List<string> { model.Selector }
            });
        }
        else
        {
            msgr.PostJsonMessage(new RulesChangedMessage
            {
                type = RulesChangedMsgType,
                removedRules = new List<string> { model.Selector }
            });
        }
    }

    static ElementBlockerViewModel()
    {
        using Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("LayoutBrowser.Tab.ElementBlockerUserScript.js") ?? throw new Exception("Couldn't find an element blocker user-script");
        using StreamReader reader = new(resource);

        BlockingUserScript = reader.ReadToEnd();
    }

    public async Task PlugIntoWebView(WebView2 wv, WebView2MessagingService messenger)
    {
        webView = wv;
        msgr = messenger;

        messenger.AddMessageHandler<EmptyMessage>(StartMsgType, OnElementBlockerScriptStart);

        await wv.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BlockingUserScript);
    }

    private void OnElementBlockerScriptStart(EmptyMessage msg)
    {
        if (msgr == null)
        {
            return;
        }

        msgr.PostJsonMessage(new RulesChangedMessage
        {
            type = InitMsgType,
            addedRules = EffectiveRules
        });
    }
}

public interface IElementBlockerMenuItem { }

public class ElementBlockerEnabledItemViewModel : IElementBlockerMenuItem
{
    private readonly ElementBlockerViewModel parent;

    public ElementBlockerEnabledItemViewModel(ElementBlockerViewModel parent)
    {
        this.parent = parent;
    }

    public ElementBlockerViewModel Parent => parent;
}

public class ElementBlockerSeparatorItem : IElementBlockerMenuItem { }

public class ElementBlockerAddNewButtonItem : IElementBlockerMenuItem
{
    private readonly ICommand addNewCommand;

    public ElementBlockerAddNewButtonItem(ElementBlockerViewModel parent)
    {
        addNewCommand = new WindowCommand(() => parent.AddRule(new ElementBlockingRule
        {
            enabled = false,
            selector = parent.Rules.Count < 1 ? "css.selector" : ""
        }));
    }

    public ICommand AddNewCommand => addNewCommand;
}

public class ElementBlockerRuleItemViewModel : ObservableObject, IDisposable, IElementBlockerMenuItem
{
    public delegate void EnabledHandler(bool enabled, ElementBlockerRuleItemViewModel model);
    public delegate void SelectorChangedHandler(string prevSelector, string curSelector, ElementBlockerRuleItemViewModel model);

    private readonly EnabledHandler enabledHandler;
    private readonly SelectorChangedHandler selectorChangedHandler;

    private readonly ICommand removeCommand;

    private bool enabled;
    private string selector;

    private bool disposed;

    public ElementBlockerRuleItemViewModel(bool enabled, string selector, EnabledHandler enabledHandler, SelectorChangedHandler selectorChangedHandler, ElementBlockerViewModel parent)
    {
        this.enabled = enabled;
        this.selector = selector;
        this.enabledHandler = enabledHandler;
        this.selectorChangedHandler = selectorChangedHandler;

        removeCommand = new WindowCommand(() => parent.RemoveRule(this));
    }

    public ICommand RemoveCommand => removeCommand;

    public bool Enabled
    {
        get => enabled;
        set
        {
            bool prev = enabled;

            SetProperty(ref enabled, value);

            if (prev == value || disposed)
            {
                return;
            }

            enabledHandler(value, this);
        }
    }

    public string Selector
    {
        get => selector;
        set
        {
            string prev = selector;

            SetProperty(ref selector, value);

            if (!enabled || prev == value || disposed)
            {
                return;
            }

            selectorChangedHandler(prev, value, this);
        }
    }

    public void Dispose()
    {
        disposed = true;

        GC.SuppressFinalize(this);
    }
}

public class EmptyMessage { }

public class RulesChangedMessage
{
    public string? type;
    public List<string>? addedRules;
    public List<string>? removedRules;
}