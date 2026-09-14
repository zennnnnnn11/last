using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace last.Helpers;

/// <summary>
///     支持单次重置通知批量追加的 ObservableCollection，彻底避免逐条 Add 触发虚拟化列表反复测量重排。
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    /// <summary>
    ///     批量追加元素，仅在全部元素插入完成后触发单次 Reset 通知。
    /// </summary>
    public void AddRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var list = collection as IList<T> ?? new List<T>(collection);
        if (list.Count == 0)
            return;

        CheckReentrancy();

        _suppressNotification = true;
        try
        {
            foreach (var item in list)
                Items.Add(item);
        }
        finally
        {
            _suppressNotification = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnPropertyChanged(e);
    }
}