using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Castle.Core.Internal;
using LayoutBrowser.Layout;
using MvvmHelpers;
using WpfAppCommon;
using WpfAppCommon.CollectionSegmenting;
using WpfAppCommon.Prompt;

namespace LayoutBrowser.Tab;

public interface IProfileListViewModelFactory
{
    public ProfileListViewModel ForOwnerTab(BrowserTabViewModel ownerTab);
}

public class ProfileListViewModel : ObservableObject
{
    private readonly BrowserTabViewModel ownerTab;
    private readonly ProfileManager profileManager;

    private readonly ConcurrentDictionary<ProfileItem, ProfileListItem> profileItemHash = new();
    private readonly CollectionManager<ProfileListItem, ProfileListItem> combiningCollectionManager;

    public ProfileListViewModel(BrowserTabViewModel ownerTab, ProfileManager profileManager)
    {
        this.ownerTab = ownerTab;
        this.profileManager = profileManager;

        CollectionManager<ProfileListItem, ProfileItem> normalProfileMutator = new(
            p =>
            {
                if (!profileItemHash.TryGetValue(p, out ProfileListItem? pl))
                {
                    pl = new ProfileListItem(p, ownerTab.Profile == p, OnProfileSelected);
                    profileItemHash[p] = pl;
                }

                return pl;
            }
        );
        normalProfileMutator.AddItemBlock(new SimpleItemBlock<ProfileItem>(profileManager.Profiles));

        combiningCollectionManager = new CollectionManager<ProfileListItem, ProfileListItem>(p => p);
        combiningCollectionManager.AddItemBlock(new SimpleItemBlock<ProfileListItem>(normalProfileMutator.Collection));
        combiningCollectionManager.AddItem(new ProfileListSeparator(), DefaultCollectionBlock.Back);
        combiningCollectionManager.AddItem(new ProfileListCreateNew(OnCreateNewProfile), DefaultCollectionBlock.Back);
    }

    public ObservableCollection<ProfileListItem> ListItems => combiningCollectionManager.Collection;

    private void OnCreateNewProfile()
    {
        string profileCaption = InputBox.Show("Please enter new profile name:", "Create profile");
        if (profileCaption.IsNullOrEmpty())
        {
            return;
        }

        string profileName = profileCaption.ToLowerInvariant().Replace(' ', '_');
        ProfileItem profile = profileManager.AddProfile(profileName, profileCaption);

        OnProfileSelected(profile);
    }

    private void OnProfileSelected(ProfileListItem pi) => OnProfileSelected(pi.Model);

    private void OnProfileSelected(ProfileItem? profileItem)
    {
        ArgumentNullException.ThrowIfNull(profileItem);

        ownerTab.OnNewProfileSelected(profileItem);
    }
}

public class DefaultItemContainerTemplateSelector : ItemContainerTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, ItemsControl parentItemsControl)
    {
        return parentItemsControl.FindResource(item.GetType()) as DataTemplate;
    }
}

public record ProfileListSeparator() : ProfileListItem(null, false, null);

public record ProfileListCreateNew(Action SimpleSelected) : ProfileListItem(null, false, _ => SimpleSelected());

public record ProfileListItem(ProfileItem? Model, bool IsChecked, Action<ProfileListItem>? Selected)
{
    public ICommand SelectedCommand => new WindowCommand(() => Selected?.Invoke(this));
}