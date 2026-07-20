using System;
using System.Collections.ObjectModel;
using AutoClicker.Models;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// TreeView 节点包装器，支持 IsExpanded/IsVisible 绑定
    /// </summary>
    public class WindowTreeNodeWrapper : ViewModelBase
    {
        public WindowTreeNode Node { get; }
        public ObservableCollection<WindowTreeNodeWrapper> Children { get; } = new();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public WindowTreeNodeWrapper(WindowTreeNode node)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            foreach (var child in node.Children)
                Children.Add(new WindowTreeNodeWrapper(child));
        }
    }
}
