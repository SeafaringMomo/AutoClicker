using System;
using System.Collections.Generic;
using System.Text;
using AutoClicker.Models;
using AutoClicker.Native;

namespace AutoClicker.Services
{
    public class WindowTreeService
    {
        public List<WindowTreeNode> BuildWindowTree(int maxDepth = 3)
        {
            var roots = new List<WindowTreeNode>();
            Win32.EnumWindows((hWnd, lParam) =>
            {
                if (Win32.IsWindowVisible(hWnd))
                {
                    var node = BuildNode(hWnd, 0, maxDepth);
                    if (node != null)
                        roots.Add(node);
                }
                return true;
            }, IntPtr.Zero);
            return roots;
        }

        public WindowTreeNode? BuildNode(IntPtr hWnd, int currentDepth = 0, int maxDepth = 3)
        {
            try
            {
                if (currentDepth > maxDepth)
                    return null;

                var node = new WindowTreeNode
                {
                    Handle = hWnd,
                    ClassName = GetClassName(hWnd),
                    Title = GetWindowTitle(hWnd),
                    ProcessId = GetProcessId(hWnd),
                    IsVisible = Win32.IsWindowVisible(hWnd),
                    IsEnabled = Win32.IsWindowEnabled(hWnd),
                    StyleInfo = GetStyleInfo(hWnd)
                };

                Win32.EnumChildWindows(hWnd, (childHwnd, lParam) =>
                {
                    var childNode = BuildNode(childHwnd, currentDepth + 1, maxDepth);
                    if (childNode != null)
                        node.Children.Add(childNode);
                    return true;
                }, IntPtr.Zero);

                return node;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "BuildNode");
                return null;
            }
        }

        private string GetClassName(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            Win32.GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            var length = Win32.GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;
            var sb = new StringBuilder(length + 1);
            Win32.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private uint GetProcessId(IntPtr hWnd)
        {
            Win32.GetWindowThreadProcessId(hWnd, out uint pid);
            return pid;
        }

        private string GetStyleInfo(IntPtr hWnd)
        {
            try
            {
                var style = Win32.GetWindowLong(hWnd, Win32.GWL_STYLE);
                var exStyle = Win32.GetWindowLong(hWnd, Win32.GWL_EXSTYLE);
                return $"Style:0x{style:X8} ExStyle:0x{exStyle:X8}";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 递归查找指定句柄节点，并回溯路径上的所有祖先节点 (用于自动展开路径)
        /// 返回 true 表示找到，并通过 expandAction 回调逐级展开父节点
        /// </summary>
        public bool ExpandPathRecursive(WindowTreeNode node, IntPtr targetHandle, Action<WindowTreeNode> expandAction)
        {
            if (node == null) return false;
            if (node.Handle == targetHandle) return true;

            foreach (var child in node.Children)
            {
                if (ExpandPathRecursive(child, targetHandle, expandAction))
                {
                    expandAction(node);
                    return true;
                }
            }
            return false;
        }
    }
}
