using System.Windows.Controls;

namespace IEDCollector
{
    internal class ListBoxAsQueue
    {
        private ListBox listBox;
        public ListBoxAsQueue(ListBox listBox)
        {
            this.listBox = listBox;
        }

        public ListBoxItem addLast(ListBoxItem item)
        {
            listBox.Items.Add(item);
            return item;
        }

        public ListBoxItem removeFirst()
        {
            if (listBox.Items.Count == 0)
            {
                return null;
            }


            ListBoxItem first = (ListBoxItem)(this.listBox.Items[0]);
            this.listBox.Items.RemoveAt(0);
            return first;
        }
    }
}
