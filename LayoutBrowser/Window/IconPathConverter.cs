using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using MonitorCommon;

namespace LayoutBrowser.Window
{
    public class IconPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !(value is string iconPath) || iconPath.IsNullOrEmpty())
            {
                return null;
            }

            return BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}