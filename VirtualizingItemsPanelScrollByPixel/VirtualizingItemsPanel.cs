using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace VirtualizingItemsPanelScrollByPixel
{
    public class VirtualizingItemsPanel : UserControl
    {
        private readonly object RefreshLock = new object();

        private int TopItemIndex;
        private int ViewCount;
        private double ScrollViewCount;
        private double ScrollOffset;
        private double PanelOffset;
        private Timer RefreshDelay = new Timer(3);

        private Grid ControlGrid;
        private StackPanel ContentContainer;
        private StackPanel ItemsPanel;
        private Border ItemsBorder;
        private ScrollBar ItemsVSB;
        private ScrollBar ItemsHSB;

        private Orientation _orientation = Orientation.Vertical;
        private bool _scrollable = true;

        private DataTemplate _itemTemplate;

        private ViewCollection ItemsCollection;


        public event EventHandler ViewRefreshed;

        public event EventHandler ItemsSourceChanged;

        public event ScrollEventHandler VerticalScroll;

        public event ScrollEventHandler HorizontalScroll;

        public double VerticalScrollValue { get => ItemsVSB.Value; set { if (ItemsVSB.Value != value) { ItemsVSB.Value = value; ItemsVSB_Scroll(null, null); } } }

        public double HorizontalScrollValue { get => ItemsHSB.Value; set { if (ItemsHSB.Value != value) { ItemsHSB.Value = value; ItemsHSB_Scroll(null, null); } } }

        public Orientation Orientation { get => _orientation; set { if (value != _orientation) { _orientation = value; RefreshOrientation(Orientation, Scrollable); } } }

        public bool Scrollable { get => _scrollable; set { if (value != _scrollable) { _scrollable = value; RefreshOrientation(Orientation, Scrollable); } } }

        private void RefreshOrientation(Orientation orientation, bool scrollable)
        {
            ItemsPanel.Orientation = orientation == Orientation.Vertical ? Orientation.Vertical : Orientation.Horizontal;
            ScrollBar sb = Orientation == Orientation.Vertical ? ItemsHSB : ItemsVSB;
            sb.Visibility = scrollable ? Visibility.Visible : Visibility.Collapsed;
            ContentContainer.Orientation = orientation == Orientation.Vertical ? (scrollable ? Orientation.Horizontal : Orientation.Vertical) : (scrollable ? Orientation.Vertical : Orientation.Horizontal);
            Refresh();
        }

        public DataTemplate ItemTemplate { get => _itemTemplate; set => SetValue(ItemTemplateProperty, value); }

        public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(VirtualizingItemsPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => ((VirtualizingItemsPanel)o).ItemTemplateChange(e.NewValue as DataTemplate)));

        private void ItemTemplateChange(DataTemplate value)
        {
            _itemTemplate = value;
            ClearChild();
        }


        public object ItemsSource { get => ItemsCollection?.Source; set => SetValue(ItemsSourceProperty, value); }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(VirtualizingItemsPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => ((VirtualizingItemsPanel)o).ItemsSourceChange(e.NewValue)));

        private void ItemsSourceChange(object value)
        {
            if (ItemsCollection != null)
            {
                ItemsCollection.CollectionChanged -= ItemCollection_CollectionChanged;
                ClearChild();
                ItemsCollection.Dispose();
            }
            if (value != null)
            {
                ItemsCollection = new ViewCollection(value);
                ItemsCollection.CollectionChanged += ItemCollection_CollectionChanged;
            }
            else
                ItemsCollection = null;
            ItemsSourceChanged?.Invoke(this, null);
            Refresh();
        }


        internal void ScrollIntoView(object item)
        {
            if (item == null)
                return;
            ScrollIntoView(ItemsCollection.IndexOf(item));
        }

        public void ScrollIntoView(int index)
        {
            if (ItemsCollection == null)
                return;
            index = ItemsCollection.CheckIndex(index);
            int endIndex = TopItemIndex + ViewCount - 1;
            if (index <= TopItemIndex)
            {
                TopItemIndex = index;
                RefreshMethod(1);
            }
            else if (index >= endIndex)
            {
                TopItemIndex = index;
                RefreshMethod(2);
            }
        }


        public VirtualizingItemsPanel()
        {
            InitControls();
            SizeChanged += BigList_SizeChanged;
            RefreshDelay.AutoReset = false;
            RefreshDelay.Elapsed += RefreshTimer_Elapsed;
        }

        private void InitControls()
        {
            SnapsToDevicePixels = true;
            ClipToBounds = true;
            Focusable = true;
            FocusVisualStyle = null;
            //BorderThickness = new Thickness(1);
            //BorderBrush = Brushes.Blue;

            //сетка элемента
            ControlGrid = new Grid();
            ControlGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ControlGrid.RowDefinitions.Add(new RowDefinition());
            ControlGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ControlGrid.ColumnDefinitions.Add(new ColumnDefinition());
            ControlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Content = ControlGrid;

            //панель элементов
            ItemsPanel = new StackPanel();
            ContentContainer = new StackPanel() { ClipToBounds = true };
            ContentContainer.Children.Add(ItemsPanel);

            ItemsBorder = new Border() { AllowDrop = true, Background = new SolidColorBrush(new Color { A = 0, B = 0, R = 0, G = 0 }) };
            //ItemsBorder.BorderThickness = new Thickness(1);
            //ItemsBorder.BorderBrush = Brushes.Blue;
            ItemsBorder.MouseWheel += ItemsBorder_MouseWheel;
            ItemsBorder.Child = ContentContainer;
            Grid.SetRow(ItemsBorder, 1);
            ControlGrid.Children.Add(ItemsBorder);

            //скруллы
            ItemsVSB = new ScrollBar { Orientation = Orientation.Vertical, LargeChange = 100, SmallChange = 100 };
            Grid.SetRow(ItemsVSB, 1);
            Grid.SetColumn(ItemsVSB, 1);
            ItemsVSB.Scroll += ItemsVSB_Scroll;
            ControlGrid.Children.Add(ItemsVSB);

            ItemsHSB = new ScrollBar { Orientation = Orientation.Horizontal, LargeChange = 100, SmallChange = 100 };
            Grid.SetRow(ItemsHSB, 2);
            ItemsHSB.Scroll += ItemsHSB_Scroll;
            ControlGrid.Children.Add(ItemsHSB);

            RefreshOrientation(Orientation, Scrollable);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            this.Focus();
            e.Handled = true;
        }

        private void ItemsHSB_Scroll(object sender, ScrollEventArgs e)
        {
            if (Orientation == Orientation.Vertical)
                RefreshHScroll();
            else
                Refresh();
        }

        private void ItemsVSB_Scroll(object sender, ScrollEventArgs e)
        {
            if (Orientation == Orientation.Vertical)
                Refresh();
            else
                RefreshHScroll();
        }

        private void ItemsBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            ScrollBar sb = Orientation == Orientation.Vertical ? ItemsVSB : ItemsHSB;
            double newVal = sb.Value + (0 - e.Delta) / 120 * (ScrollViewCount * 0.3);
            if (newVal < 0) newVal = 0;
            if (newVal > sb.Maximum) newVal = sb.Maximum;
            sb.Value = newVal;
            Refresh();
        }

        private void BigList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Refresh();
        }

        internal virtual void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Refresh();
        }

        internal virtual void ItemCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Refresh();
        }

        internal virtual void Child_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        internal virtual void Child_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }


        private void RefreshHScroll()
        {
            ScrollBar sb = Orientation == Orientation.Vertical ? ItemsHSB : ItemsVSB;
            ItemsPanel.Margin = Orientation == Orientation.Vertical ? new Thickness(0 - Math.Round(sb.Value), PanelOffset, 0, 0) : new Thickness(PanelOffset, 0 - Math.Round(sb.Value), 0, 0);

            if (ItemsPanel.Children.Count == 0) return;
            double width = Orientation == Orientation.Vertical ? ItemsPanel.ActualWidth : ItemsPanel.ActualHeight;
            double pwidth = Orientation == Orientation.Vertical ? ItemsBorder.ActualWidth : ItemsBorder.ActualHeight;
            double vwidth = Math.Abs(Orientation == Orientation.Vertical ? ItemsPanel.Margin.Left : ItemsPanel.Margin.Top) + pwidth;
            if (width > vwidth) vwidth = width;
            double unvwidth = vwidth - pwidth;
            if (unvwidth > 0 && sb.Track != null)
            {
                double ThumbH = pwidth / vwidth * sb.Track.ActualWidth;
                if (ThumbH < 0) ThumbH = 0;
                sb.Maximum = unvwidth;
                sb.ViewportSize = ThumbH * (sb.Maximum - sb.Minimum) / (sb.Track.ActualWidth - ThumbH);
            }
            else
            {
                sb.ViewportSize = width;
                sb.Maximum = 0;
            }

            (Orientation == Orientation.Vertical ? HorizontalScroll : VerticalScroll)?.Invoke(this, new ScrollEventArgs(ScrollEventType.ThumbTrack, sb.Value));
        }

        internal void Refresh()
        {
            //Dispatcher.Invoke(new Action(() => RefreshMethod(0)));
            try
            {
                RefreshDelay.Enabled = false;
                RefreshDelay.Enabled = true;
            }
            catch (Exception) { }
        }

        private void RefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try { Dispatcher.Invoke(new Action(() => RefreshMethod(0))); } catch (Exception) { }
        }

        private void RefreshMethod(int typeLoad)
        {
            //System.Diagnostics.Debug.WriteLine("RefreshMethod");
            //typeLoad == 0 по значению скрулла
            //typeLoad == 1 от верхнего элемента
            //typeLoad == 2 от нижнего элемента
            ScrollBar sb = Orientation == Orientation.Vertical ? ItemsVSB : ItemsHSB;
            lock (RefreshLock)
            {
                if (ItemsCollection == null)
                {
                    ItemsPanel.Children.Clear();
                    return;
                }
                if (typeLoad == 0)
                {
                    double rv = sb.Value - ItemsCollection.CheckIndex(TopItemIndex);
                    if (rv > 0)
                    {
                        int irv = (int)Math.Abs(rv);
                        TopItemIndex = ItemsCollection.CheckIndex(TopItemIndex + irv);
                        ScrollOffset = rv - irv;
                    }
                    else
                    {
                        int irv = (int)Math.Ceiling(Math.Abs(rv));
                        TopItemIndex = ItemsCollection.CheckIndex(TopItemIndex - irv);
                        ScrollOffset = rv + irv;
                    }
                }
                else if (typeLoad > 0)
                    ScrollOffset = 0;

                //Очистка панели
                for (int i = 0; i < ItemsPanel.Children.Count; i++)
                    ((FrameworkElement)ItemsPanel.Children[i]).Visibility = Visibility.Collapsed;
                UpdateLayoutItemsPanel();

                //Добавление элементов
                ViewCount = 0;
                double height = 0;
                double maxHeight = Orientation == Orientation.Vertical ? ItemsBorder.ActualHeight : ItemsBorder.ActualWidth;
                int CollectionCount = ItemsCollection.Count;
                int index = TopItemIndex = ItemsCollection.CheckIndex(TopItemIndex);
                int findex = TopItemIndex - 1;
                double sizeViewTopChild = 0;
                double sizeViewLastChild = 0;
                double sizeTopChild = 0;
                while (height < maxHeight && index < CollectionCount)
                {
                    FrameworkElement child = GetChild(ViewCount, index);
                    UpdateLayoutItemsPanel();
                    double CHeight = Orientation == Orientation.Vertical ? child.ActualHeight : child.ActualWidth;
                    height += CHeight;
                    ViewCount++;
                    index++;
                    if (ViewCount == 1)
                    {
                        sizeTopChild = CHeight;
                        sizeViewTopChild = (CHeight - CHeight * ScrollOffset) / CHeight;
                        PanelOffset = 0 - CHeight * ScrollOffset;
                        height += PanelOffset;
                        if (typeLoad == 2)
                            break;
                    }
                }

                //Заполнение оставшегося пространства
                if (height < maxHeight)
                {
                    if (findex >= 0)
                    {
                        height -= PanelOffset;
                        while (findex >= 0)
                        {
                            FrameworkElement child = GetChild(-1, findex);
                            UpdateLayoutItemsPanel();
                            double CHeight = Orientation == Orientation.Vertical ? child.ActualHeight : child.ActualWidth;
                            height += CHeight;
                            TopItemIndex = findex;
                            ViewCount++;
                            findex--;
                            if (height > maxHeight)
                            {
                                ScrollOffset = (height - maxHeight) / CHeight;
                                sizeViewTopChild = 1 - ScrollOffset;
                                PanelOffset = 0 - CHeight * ScrollOffset;
                                height += PanelOffset;
                                break;
                            }
                        }
                    }
                    else if (PanelOffset < 0)
                    {
                        if (sizeTopChild == 0 || (height - PanelOffset - maxHeight) / sizeTopChild < 0)
                        {
                            ScrollOffset = 0;
                            PanelOffset = 0;
                            sizeViewTopChild = 1;
                        }
                        else
                        {
                            height -= PanelOffset;
                            ScrollOffset = (height - maxHeight) / sizeTopChild;
                            sizeViewTopChild = 1 - ScrollOffset;
                            PanelOffset = 0 - sizeTopChild * ScrollOffset;
                            height += PanelOffset;
                        }
                    }
                }

                //Обновление вертикального скрулла
                sizeViewTopChild = Double.IsNaN(sizeViewTopChild) || Double.IsInfinity(sizeViewTopChild) ? 0 : sizeViewTopChild;
                ScrollViewCount = ViewCount;
                if (PanelOffset <= 0)
                    ScrollViewCount--;
                if (height > maxHeight)
                    ScrollViewCount--;
                if (GetLastChild() is FrameworkElement lastChild && (Orientation == Orientation.Vertical ? lastChild.ActualHeight : lastChild.ActualWidth) is double LHeight)
                    sizeViewLastChild = height > maxHeight ? (LHeight - height + maxHeight) / LHeight : 0;
                ScrollViewCount += sizeViewLastChild + sizeViewTopChild;
                if (ScrollViewCount < 0)
                    ScrollViewCount = 0;
                sb.Maximum = CollectionCount - ScrollViewCount;
                sb.Value = TopItemIndex + ScrollOffset;
                if (sb.Track != null && sb.Track.ActualHeight > 0)
                {
                    double ThumbH = ScrollViewCount / (double)CollectionCount * sb.Track.ActualHeight;
                    if (Double.IsNaN(ThumbH) || Double.IsInfinity(ThumbH)) ThumbH = 0;
                    if (sb.Track.ActualHeight - ThumbH == 0)
                        sb.ViewportSize = CollectionCount;
                    else
                        sb.ViewportSize = ThumbH * (sb.Maximum - sb.Minimum) / (sb.Track.ActualHeight - ThumbH);
                }

                //Смещение панели
                ItemsPanel.Margin = Orientation == Orientation.Vertical ? new Thickness(0, PanelOffset, 0, 0) : new Thickness(PanelOffset, 0, 0, 0);

                //Обновление горизонтального скрулла
                RefreshHScroll();

                ViewRefreshed?.Invoke(this, new EventArgs());
                (Orientation == Orientation.Vertical ? VerticalScroll : HorizontalScroll)?.Invoke(this, new ScrollEventArgs(ScrollEventType.ThumbTrack, sb.Value));
            }
        }

        internal virtual FrameworkElement GetChild(int indexChild, int indexSource)
        {
            //Создание элемента
            FrameworkElement itemControl = null;
            if (indexChild == -1)
            {
                if (ItemsPanel.Children.Count > 0 && ItemsPanel.Children[ItemsPanel.Children.Count - 1].Visibility == Visibility.Collapsed)
                {
                    itemControl = ItemsPanel.Children[ItemsPanel.Children.Count - 1] as FrameworkElement;
                    itemControl.Visibility = Visibility.Visible;
                    ItemsPanel.Children.RemoveAt(ItemsPanel.Children.Count - 1);
                }
                else
                    itemControl = CreateChild();
                ItemsPanel.Children.Insert(0, itemControl);
            }
            else if (indexChild >= 0 && indexChild < ItemsPanel.Children.Count)
            {
                ItemsPanel.Children[indexChild].Visibility = Visibility.Visible;
                itemControl = ItemsPanel.Children[indexChild] as FrameworkElement;
            }
            else
            {
                itemControl = CreateChild();
                ItemsPanel.Children.Add(itemControl);
            }
            itemControl.UpdateLayout();

            //Задание контекста данных для представления контента
            if (itemControl is FrameworkElement presenther && ItemsCollection.GetItem(indexSource) is object item && presenther.DataContext != item)
            {
                if (presenther.DataContext is INotifyPropertyChanged oldChanged)
                    oldChanged.PropertyChanged -= Item_PropertyChanged;
                if (item is INotifyPropertyChanged newChanged)
                    newChanged.PropertyChanged += Item_PropertyChanged;
                presenther.DataContext = item;
            }

            return itemControl;
        }

        private FrameworkElement CreateChild()
        {
            FrameworkElement child = null;
            if (_itemTemplate != null)
                child = ItemTemplate.LoadContent() as FrameworkElement;
            if (child == null)
                child = new DefaultItem();
            child = new ContentPresenter() { Content = child };
            child.MouseDown += Child_MouseDown;
            child.MouseUp += Child_MouseUp;
            return child;
        }

        private FrameworkElement GetLastChild()
        {
            for (int i = ItemsPanel.Children.Count - 1; i >= 0; i--)
                if (ItemsPanel.Children[i].Visibility == Visibility.Visible)
                    return ItemsPanel.Children[i] as FrameworkElement;
            return null;
        }

        private void ClearChild()
        {
            for (int i = 0; i < ItemsPanel.Children.Count; i++)
                if (ItemsPanel.Children[i] is FrameworkElement itemControl)
                {
                    itemControl.MouseDown -= Child_MouseDown;
                    itemControl.MouseUp -= Child_MouseUp;
                    if (itemControl.DataContext is INotifyPropertyChanged viewChanged)
                        viewChanged.PropertyChanged -= Item_PropertyChanged;
                    itemControl.DataContext = null;
                    if (itemControl is FrameworkElement presenther)
                    {
                        if (presenther.DataContext is INotifyPropertyChanged presentherChanged)
                            presentherChanged.PropertyChanged -= Item_PropertyChanged;
                        presenther.DataContext = null;
                    }
                    itemControl.ContextMenu = null;
                }
            ItemsPanel.Children.Clear();
            Refresh();
        }

        private void UpdateLayoutItemsPanel()
        {
            Size size = new Size(ItemsBorder.ActualWidth, ItemsBorder.ActualHeight);
            ItemsBorder.Measure(size);
            //ItemsBorder.Arrange(new Rect(size));
            ItemsBorder.UpdateLayout();
        }
    }
}
