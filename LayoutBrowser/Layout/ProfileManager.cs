using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Monitor.ServiceCommon.Services;
using Monitor.ServiceCommon.Util;
using MonitorCommon;
using Newtonsoft.Json;

namespace LayoutBrowser.Layout;

// don't know, if such a fancy class is needed for essentially managing a list of strings, but
// it can be useful for cleanup in future
public class ProfileManager
{
    public const string DefaultProfile = "default";

    private readonly JsonSerializer ser;

    private readonly ObservableCollection<ProfileItem> profiles = new();

    public ProfileManager(JsonSerializerSvc serSvc, ProcessLifetimeSvc lifetimeSvc)
    {
        ser = serSvc.Serializer;

        LoadProfiles();

        lifetimeSvc.ApplicationStop += OnAppStop;
    }

    private Task OnAppStop()
    {
        SaveProfiles();

        return Task.CompletedTask;
    }

    public ProfileItem AddProfile(string name, string caption)
    {
        ProfileItem item = new(name, caption);

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
            items = ser.Deserialize<List<ProfileItem>>(Settings.Default.Profiles) ?? new List<ProfileItem>();
        }

        if (items.IsEmpty())
        {
            items.Add(new ProfileItem(DefaultProfile, "Default"));
        }

        return items;
    }

    private void SaveProfiles()
    {
        Settings.Default.Profiles = ser.Serialize(profiles);
        Settings.Default.Save();
    }
}

public record ProfileItem(string Name, string Caption);