using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AutoClicker.Models;
using AutoClicker.Native;

namespace AutoClicker.Services
{
    /// <summary>
    /// 窗口树服务 — 枚举所有顶层窗口及其子窗口
    /// 用于模式2 (窗口树定位), 用户可以在树中选择目标控件
    /// </summary>
    public class WindowTreeService
    {
        /// <summary>
        /// 获取所有可见顶层窗口
        /// </summary>
        public List<WindowTreeNode> GetTopLevelWindows()
        {
            var windows = new List<WindowTreeNode>();
            Win32.EnumWindows((hWnd, lParam) =>
            {
                if (Win32.IsWindowVisible(hWnd))
                {
                    var node = BuildNode(hWnd);
                    windows.Add(node);
                }
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        /// <summary>
        /// 递归获取指定窗口的所有子窗口
        /// </summary>
        public List<WindowTreeNode> GetChildWindows(IntPtr parentHwnd, int maxDepth = 10)
        {
            var children = new List<WindowTreeNode>();
            CollectChildren(parentHwnd, children, 0, maxDepth);
            return children;
        }

        /// <summary>
        /// 构建完整的窗口树 (顶层 + 递归子窗口)
        /// 注意: 只展开2层, 避免太深
        /// </summary>
        public List<WindowTreeNode> BuildWindowTree(int maxDepth = 3)
        {
            var result = new List<WindowTreeNode>();
            var topWindows = GetTopLevelWindows();

            foreach (var top in topWindows)
            {
                // 只展开有标题的或重要的窗口
                if (!string.IsNullOrEmpty(top.Title) ||
                    top.ClassName.Contains("Button", StringComparison.OrdinalIgnoreCase) ||
                    top.ClassName == "Shell_TrayWnd") // 任务栏
                {
                    BuildTreeRecursive(top, 0, maxDepth);
                    result.Add(top);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取鼠标下的窗口及父窗口链
        /// </summary>
        public WindowTreeNode? GetWindowInfoUnderCursor()
        {
            Win32.GetCursorPos(out var pt);
            IntPtr hwnd = Win32.WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) return null;

            // 获取顶层窗口
            IntPtr rootHwnd = Win32.GetAncestor(hwnd, Win32.GA_ROOT);
            var root = BuildNode(rootHwnd);

            // 从 root 向下构建到当前句柄的路径
            var path = new List<IntPtr>();
            IntPtr current = hwnd;
            while (current != IntPtr.Zero && current != rootHwnd)
            {
                path.Add(current);
                current = Win32.GetParent(current);
            }
            path.Reverse();

            var node = root;
            foreach (var h in path)
            {
                var child = BuildNode(h);
                child.Children = GetImmediateChildren(h);
                node.Children.Add(child);
                node = child;
            }

            return root;
        }

        /// <summary>
        /// 根据句柄构建单棵节点
        /// </summary>
        public WindowTreeNode BuildNode(IntPtr hWnd)
        {
            Win32.GetWindowThreadProcessId(hWnd, out uint pid);
            uint style = Win32.GetWindowLong(hWnd, Win32.GWL_STYLE);

            return new WindowTreeNode
            {
                Handle = hWnd,
                ClassName = Win32.GetClassNameStr(hWnd),
                Title = Win32.GetWindowTextStr(hWnd),
                ProcessId = pid,
                IsVisible = Win32.IsWindowVisible(hWnd),
                IsEnabled = Win32.IsWindowEnabled(hWnd),
                StyleInfo = $"0x{style:X8}",
            };
        }

        // ===== 内部方法 =====

        private void BuildTreeRecursive(WindowTreeNode parent, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;

            var children = GetImmediateChildren(parent.Handle);
            parent.Children = children;

            foreach (var child in children)
            {
                BuildTreeRecursive(child, depth + 1, maxDepth);
            }
        }

        private List<WindowTreeNode> GetImmediateChildren(IntPtr parentHwnd)
        {
            var children = new List<WindowTreeNode>();
            Win32.EnumChildWindows(parentHwnd, (hWnd, lParam) =>
            {
                children.Add(BuildNode(hWnd));
                return true;
            }, IntPtr.Zero);
            return children;
        }

        private void CollectChildren(IntPtr parentHwnd, List<WindowTreeNode> result, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;

            Win32.EnumChildWindows(parentHwnd, (hWnd, lParam) =>
            {
                var node = BuildNode(hWnd);
                result.Add(node);
                CollectChildren(hWnd, result, depth + 1, maxDepth);
                return true;
            }, IntPtr.Zero);
        }
    }
}
