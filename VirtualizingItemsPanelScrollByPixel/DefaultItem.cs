using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

namespace VirtualizingItemsPanelScrollByPixel
{
    internal class DefaultItem : Grid
    {
        public DefaultItem()
        {
            Background = new SolidColorBrush(new Color { A = 0, R = 0, G = 0, B = 0 });

            ContentPresenter contentPresenter = new ContentPresenter();
            TextBlock textBlock = new TextBlock();

            Children.Add(contentPresenter);
            contentPresenter.Content = textBlock;

            Binding c = new Binding();
            textBlock.SetBinding(TextBlock.TextProperty, c);
        }
    }
}
