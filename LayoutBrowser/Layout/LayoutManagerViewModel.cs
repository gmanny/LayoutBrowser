using MvvmHelpers;

namespace LayoutBrowser.Layout;

public class LayoutManagerViewModel : ObservableObject
{
    private readonly LayoutManager layoutManager;

    public LayoutManagerViewModel(LayoutManager layoutManager)
    {
        this.layoutManager = layoutManager;
    }
        
    public bool LayoutLocked
    {
        get => layoutManager.LayoutLocked;
        set
        {
            layoutManager.LayoutLocked = value;

            OnPropertyChanged();
        }
    }

    public bool LayoutRestoreUsingToBack
    {
        get => layoutManager.LayoutRestoreUsingToBack;
        set
        {
            layoutManager.LayoutRestoreUsingToBack = value;

            OnPropertyChanged();
        }
    }

    public bool StoreClosedHistory
    {
        get => layoutManager.StoreClosedHistory;
        set
        {
            layoutManager.StoreClosedHistory = value;

            OnPropertyChanged();
        }
    }

    public bool DarkMode
    {
        get => layoutManager.DarkMode;
        set
        {
            layoutManager.DarkMode = value;

            OnPropertyChanged();
        }
    }
}