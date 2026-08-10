using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AutoClicker.Models;

namespace AutoClicker.Services
{
    /// <summary>
    /// 流程持久化服务 — workflows.json 存取 + 自动备份 + 单流程导出/导入
    /// 文件位置: 程序目录/workflows.json
    /// 备份策略: 每次保存前备份为 workflows.json.bak (只保留最近一份)
    /// </summary>
    public class WorkflowStorageService : IWorkflowStorage
    {
        private static readonly string StoragePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "workflows.json");

        private static readonly string BackupPath = StoragePath + ".bak";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public WorkflowLibrary LoadAll()
        {
            try
            {
                if (!File.Exists(StoragePath))
                    return new WorkflowLibrary();

                var json = File.ReadAllText(StoragePath);
                var lib = JsonSerializer.Deserialize<WorkflowLibrary>(json, JsonOptions);
                return lib ?? new WorkflowLibrary();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "LoadAll");
                Logger.Log("尝试加载备份文件", LogLevel.Warning, "WorkflowStorage");
                return TryLoadBackup() ?? new WorkflowLibrary();
            }
        }

        private WorkflowLibrary? TryLoadBackup()
        {
            try
            {
                if (!File.Exists(BackupPath)) return null;
                var json = File.ReadAllText(BackupPath);
                return JsonSerializer.Deserialize<WorkflowLibrary>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public void SaveAll(WorkflowLibrary library)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));

            try
            {
                // 备份现有文件
                if (File.Exists(StoragePath))
                {
                    File.Copy(StoragePath, BackupPath, overwrite: true);
                }

                library.Version = 1;
                var json = JsonSerializer.Serialize(library, JsonOptions);
                File.WriteAllText(StoragePath, json);

                Logger.Log($"已保存流程库: {library.Workflows.Count} 个流程", LogLevel.Debug, "WorkflowStorage");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SaveAll");
                throw;
            }
        }

        public void SaveWorkflow(Workflow workflow)
        {
            if (workflow == null) throw new ArgumentNullException(nameof(workflow));
            if (string.IsNullOrEmpty(workflow.Id))
                workflow.Id = Guid.NewGuid().ToString("N");

            workflow.UpdatedAt = DateTime.Now;

            var lib = LoadAll();
            var existingIdx = lib.Workflows.FindIndex(w => w.Id == workflow.Id);
            if (existingIdx >= 0)
            {
                lib.Workflows[existingIdx] = workflow;
            }
            else
            {
                if (workflow.CreatedAt == default)
                    workflow.CreatedAt = DateTime.Now;
                lib.Workflows.Add(workflow);
            }

            SaveAll(lib);

            Logger.Log($"流程已保存: {workflow.Name} ({workflow.Actions.Count} 步)",
                LogLevel.Info, "WorkflowStorage");
        }

        public bool DeleteWorkflow(string workflowId)
        {
            if (string.IsNullOrEmpty(workflowId)) return false;

            var lib = LoadAll();
            var removed = lib.Workflows.RemoveAll(w => w.Id == workflowId);

            if (removed > 0)
            {
                SaveAll(lib);
                Logger.Log($"流程已删除: id={workflowId}", LogLevel.Info, "WorkflowStorage");
                return true;
            }
            return false;
        }

        public void Export(Workflow workflow, string filePath)
        {
            if (workflow == null) throw new ArgumentNullException(nameof(workflow));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("文件路径不能为空", nameof(filePath));

            try
            {
                // 单流程导出为单元素 Workflows 数组，便于以后扩展为多流程导出
                var lib = new WorkflowLibrary
                {
                    Version = 1,
                    Workflows = { workflow }
                };
                var json = JsonSerializer.Serialize(lib, JsonOptions);
                File.WriteAllText(filePath, json);

                Logger.Log($"流程已导出: {workflow.Name} -> {filePath}",
                    LogLevel.Info, "WorkflowStorage");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Export");
                throw;
            }
        }

        public Workflow Import(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);

            try
            {
                var json = File.ReadAllText(filePath);
                var lib = JsonSerializer.Deserialize<WorkflowLibrary>(json, JsonOptions)
                    ?? throw new InvalidDataException("文件内容无效");

                if (lib.Workflows.Count == 0)
                    throw new InvalidDataException("文件中未找到任何流程");

                var workflow = lib.Workflows[0];
                // 导入时重新生成 ID 避免冲突，更新时间戳
                workflow.Id = Guid.NewGuid().ToString("N");
                workflow.CreatedAt = DateTime.Now;
                workflow.UpdatedAt = DateTime.Now;

                // 加入到现有库
                var existingLib = LoadAll();
                existingLib.Workflows.Add(workflow);
                SaveAll(existingLib);

                Logger.Log($"流程已导入: {workflow.Name}", LogLevel.Info, "WorkflowStorage");
                return workflow;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Import");
                throw;
            }
        }

        /// <summary>
        /// 获取存储文件路径 (用于UI显示)
        /// </summary>
        public static string GetStoragePath() => StoragePath;
    }
}
