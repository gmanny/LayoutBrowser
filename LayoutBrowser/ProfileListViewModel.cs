using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MvvmHelpers;
using WpfAppCommon;
using WpfAppCommon.CollectionSegmenting;

namespace LayoutBrowser
{
    public interface IProfileListViewModelFactory
    {
        public ProfileListViewModel ForOwnerTab(BrowserTabViewModel ownerTab);
    }

    public class ProfileListViewModel : ObservableObject
    {
        private readonly BrowserTabViewModel ownerTab;
        private readonly ProfileManager profileManager;

        private readonly CollectionManager<ProfileListItem, ProfileItem> normalProfileMutator;
        private readonly ConcurrentDictionary<ProfileItem, ProfileListItem> profileItemHash = new ConcurrentDictionary<ProfileItem, ProfileListItem>();
        private readonly CollectionManager<ProfileListItem, ProfileListItem> combiningCollectionManager;

        public ProfileListViewModel(BrowserTabViewModel ownerTab, ProfileManager profileManager)
        {
            this.ownerTab = ownerTab;
            this.profileManager = profileManager;

            normalProfileMutator = new CollectionManager<ProfileListItem, ProfileItem>(
                p =>
                {
                    if (!profileItemHash.TryGetValue(p, out ProfileListItem pl))
                    {
                        pl = new ProfileListItem(p, ownerTab.Profile == p, OnProfileSelected);
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
            throw new NotImplementedException();
        }

        private void OnProfileSelected(ProfileListItem pi)
        {
            ownerTab.NewProfileSelected(pi.Model);
        }
    }

    public class DefaultItemContainerTemplateSelector : ItemContainerTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, ItemsControl parentItemsControl)
        {
            return parentItemsControl.FindResource(item.GetType()) as DataTemplate;
        }
    }

    public class ProfileListSeparator : ProfileListItem
    {
        public ProfileListSeparator() : base(null, false, null)
        { }
    }

    public class ProfileListCreateNew : ProfileListItem
    {
        public ProfileListCreateNew(Action selected) : base(null, false, _ => selected?.Invoke())
        { }
    }

    public class ProfileListItem : ObservableObject
    {
        private readonly ProfileItem model;
        private readonly bool isChecked;
        private readonly ICommand selectedCommand;

        public ProfileListItem(ProfileItem model, bool isChecked, Action<ProfileListItem> selected)
        {
            this.model = model;
            this.isChecked = isChecked;

            selectedCommand = new WindowCommand(() => selected?.Invoke(this));
        }

        public ProfileItem Model => model;
        
        public bool IsChecked => isChecked;

        public ICommand SelectedCommand => selectedCommand;
    }
}