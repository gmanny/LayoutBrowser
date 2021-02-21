using MvvmHelpers;

namespace LayoutBrowser.Layout
{
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
    }
}