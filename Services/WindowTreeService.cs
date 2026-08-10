using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AutoClicker.Models;
using AutoClicker.Native;

namespace AutoClicker.Services
{
    public class WindowTreeService
    {
        /// <summary>
        /// v1.5.0 新增: 按条件查找顶层窗口
        /// 所有条件都为空时返回 IntPtr.Zero
        /// </summary>
        /// <param name="titlePattern">标题通配符模式 (支持 *)，空字符串表示不限制</param>
        /// <param name="className">类名精确匹配，空字符串表示不限制</param>
        /// <param name="processName">进程名匹配 (如 notepad)，空字符串表示不限制</param>
        /// <returns>第一个匹配窗口的句柄；未找到返回 IntPtr.Zero</returns>
        public IntPtr FindWindow(string titlePattern, string className, string processName)
        {
            // 三条件都为空 — 无意义
            if (string.IsNullOrEmpty(titlePattern) 
                && string.IsNullOrEmpty(className) 
                && string.IsNullOrEmpty(processName))
            {
                return IntPtr.Zero;
            }

            IntPtr found = IntPtr.Zero;
            Win32.EnumWindows((h, _) =>
            {
                if (!Win32.IsWindowVisible(h)) return true;

                var title = GetWindowTitle(h);
                var cls = GetClassName(h);

                // 标题通配符匹配 (* 匹配任意字符)
                if (!string.IsNullOrEmpty(titlePattern) && !MatchWildcard(title, titlePattern))
                    return true;
                // 类名精确匹配
                if (!string.IsNullOrEmpty(className) && cls != className)
                    return true;
                // 进程名匹配
                if (!string.IsNullOrEmpty(processName))
                {
                    Win32.GetWindowThreadProcessId(h, out uint pid);
                    try
                    {
                        var proc = Process.GetProcessById((int)pid);
                        if (!proc.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch
                    {
                        return true;
                    }
                }

                found = h;
                return false;  // 停止枚举
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>
        /// v1.5.0 新增: 通配符匹配 (* 匹配任意字符，? 匹配单字符)
        /// 例如 MatchWildcard("订单详情 - #ORD123", "订单*") = true
        /// </summary>
        public static bool MatchWildcard(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(input)) return false;

            // 将通配符转换为正则: * → .*, ? → ., 其他字符转义
            var regex = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// v1.5.0 新增: 枚举指定父窗口下指定类名的所有子控件
        /// </summary>
        public List<IntPtr> FindChildControls(IntPtr parentHwnd, string className)
        {
            var result = new List<IntPtr>();
            if (parentHwnd == IntPtr.Zero) return result;

            Win32.EnumChildWindows(parentHwnd, (h, _) =>
            {
                if (!string.IsNullOrEmpty(className))
                {
                    var cls = GetClassName(h);
                    if (cls != className) return true;
                }
                result.Add(h);
                return true;
            }, IntPtr.Zero);
            return result;
        }

        /// <summary>
        /// v1.5.0 新增: 获取子控件文本 (WM_GETTEXT)
        /// </summary>
        public string GetControlText(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return string.Empty;
            var sb = new StringBuilder(4096);
            // WM_GETTEXT = 0x000D
            Win32.SendMessage(hwnd, 0x000D, (IntPtr)4096, sb);
            return sb.ToString();
        }

        /// <summary>
        /// v1.5.0 新增: 拼接所有子控件文本
        /// </summary>
        public string GetAllChildrenText(IntPtr parentHwnd)
        {
            if (parentHwnd == IntPtr.Zero) return string.Empty;
            var sb = new StringBuilder();

            Win32.EnumChildWindows(parentHwnd, (h, _) =>
            {
                var text = GetControlText(h);
                if (!string.IsNullOrEmpty(text))
                {
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(text);
                }
                return true;
            }, IntPtr.Zero);

            return sb.ToString();
        }

        /// <summary>
        /// v1.5.0 新增: 获取指定类名子控件序号对应的文本
        /// </summary>
        public string GetChildTextByIndex(IntPtr parentHwnd, string className, int index)
        {
            var children = FindChildControls(parentHwnd, className);
            if (index < 0 || index >= children.Count) return string.Empty;
            return GetControlText(children[index]);
        }

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

        /// <summary>v1.5.0: 改为 public 供 WorkflowPlayer 使用</summary>
        public string GetWindowTitle(IntPtr hWnd)
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
