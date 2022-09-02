using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace VirtualizingItemsPanelScrollByPixel
{
    internal class ViewCollection
    {
        private object LockChange = new object();

        private MethodInfo Counter;
        private MethodInfo Indexer;
        private IList SourceList;
        public object Source;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public object this[int index] => SourceList != null ? SourceList[index] : Indexer.Invoke(Source, new object[] { index });

        public int Count => SourceList != null ? SourceList.Count : (int)Counter.Invoke(Source, null);

        public ViewCollection(object collection)
        {
            if (collection == null)
                throw new NullReferenceException();
            if (collection is IList list)
                SourceList = list;
            else
            {
                Type t = collection.GetType();
                Indexer = t.GetMethod("get_Item");
                if (Indexer == null)
                    Indexer = t.GetMethod("Get");
                Counter = t.GetMethod("get_Count");
                if (Counter == null)
                    Counter = t.GetMethod("get_Length");
                if (Counter == null || Indexer == null)
                    throw new Exception("Объект не является коллекцией с поддержкой индексатора");
            }
            Source = collection;

            if (Source is INotifyCollectionChanged sCollectionChanged)
                sCollectionChanged.CollectionChanged += CollectionChanged_CollectionChanged;
        }

        public object GetItem(int index)
        {
            object r = null;
            lock (LockChange)
                r = this[index];
            return r;
        }

        public int CheckIndex(int index)
        {
            int r = 0;
            lock (LockChange)
            {
                int count = this.Count;
                r = index < 0 ? 0 : index < count ? index : count - 1;
            }
            return r;
        }

        public void CollectionChanged_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(sender, e);
        }

        public int IndexOf(object item)
        {
            if (SourceList != null)
                return SourceList.IndexOf(item);
            else if (Indexer != null)
            {
                int count = Count;
                for (int i = 0; i < count; i++)
                    if (Indexer.Invoke(Source, new object[] { i }) == item)
                        return i;
            }
            return -1;
        }

        public void Dispose()
        {
            Counter = null;
            Indexer = null;
            SourceList = null;
            Source = null;

            if (Source is INotifyCollectionChanged sCollectionChanged)
                sCollectionChanged.CollectionChanged -= CollectionChanged_CollectionChanged;
        }
    }
}
