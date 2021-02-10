using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Monitor.ServiceCommon.Services;
using Monitor.ServiceCommon.Util;
using MonitorCommon;
using MvvmHelpers;
using Newtonsoft.Json;

namespace LayoutBrowser.Layout
{
    // don't know, if such a fancy class is needed for essentially managing a list of strings, but
    // it can be useful for cleanup in future
    public class ProfileManager
    {
        public const string DefaultProfile = "default";

        private readonly JsonSerializer ser;
        private readonly ILogger logger;

        private readonly ObservableCollection<ProfileItem> profiles = new ObservableCollection<ProfileItem>();

        public ProfileManager(JsonSerializerSvc serSvc, ProcessLifetimeSvc lifetimeSvc, ILogger logger)
        {
            this.logger = logger;

            ser = serSvc.Serializer;

            LoadProfiles();
            lifetimeSvc.ApplicationStop += OnAppStop;
        }

        private async Task OnAppStop()
        {
            SaveProfiles();
        }

        public ProfileItem AddProfile(string name, string caption)
        {
            ProfileItem item = new ProfileItem
            {
                Name = name,
                Caption = caption
            };

            profiles.Add(item);

            return item;
        }

        private void LoadProfiles()
        {
            profiles.Clear();
            foreach (ProfileItem item in FromSettings())
            {
                profiles.Add(item);
            }
        }

        public ObservableCollection<ProfileItem> Profiles => profiles;

        private List<ProfileItem> FromSettings()
        {
            List<ProfileItem> items;

            if (Settings.Default.Profiles.IsNullOrEmpty())
            {
                items = new List<ProfileItem>();
            }
            else
            {
                items = ser.Deserialize<List<ProfileItem>>(Settings.Default.Profiles);
            }

            if (items.IsEmpty())
            {
                items.Add(new ProfileItem
                {
                    Name = DefaultProfile,
                    Caption = "Default"
                });
            }

            return items;
        }

        private void SaveProfiles()
        {
            Settings.Default.Profiles = ser.Serialize(profiles);
            Settings.Default.Save();
        }
    }

    public class ProfileItem : ObservableObject
    {
        private string name;
        private string caption;

        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        // friendly name
        public string Caption
        {
            get => caption;
            set => SetProperty(ref caption, value);
        }
    }
}