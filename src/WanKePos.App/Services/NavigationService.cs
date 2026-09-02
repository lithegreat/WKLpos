using System;
using System.Windows.Controls;

namespace WanKePos.App.Services
{
    public class NavigationService
    {
        private Frame _frame;

        public void Initialize(Frame frame)
        {
            _frame = frame;
        }

        public void Navigate(Type pageType)
        {
            if (_frame != null && pageType != null)
            {
                var page = App.Services.GetService(pageType);
                if (page != null)
                {
                    _frame.Navigate(page);
                }
            }
        }
    }
}
