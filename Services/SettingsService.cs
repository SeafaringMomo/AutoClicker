using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoClicker.Models;

namespace AutoClicker.Services
{
    /// <summary>
    /// 配置持久化服务 - JSON 文件存储
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsFilePath;
        private static readonly JsonSerializerOptions JsonOptions;
        private static readonly object FileLock = new();

        static SettingsService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            SettingsFilePath = Path.Combine(baseDir, "AutoClicker.settings.json");

            JsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        /// <summary>
        /// 加载配置，失败时返回默认配置
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                lock (FileLock)
                {
                    if (!File.Exists(SettingsFilePath))
                        return new AppSettings();

                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SettingsService.Load");
                return new AppSettings();
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                lock (FileLock)
                {
                    var json = JsonSerializer.Serialize(settings, JsonOptions);
                    File.WriteAllText(SettingsFilePath, json);
                }
                Logger.Log($"配置已保存: {SettingsFilePath}", LogLevel.Debug, "Settings");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SettingsService.Save");
            }
        }

       /// <summary>
       /// 重置为默认配置
       /// </summary>
       public static void Reset()
       {
           try
           {
               lock (FileLock)
               {
                   if (File.Exists(SettingsFilePath))
                       File.Delete(SettingsFilePath);
               }
               Logger.Log("配置已重置为默认值", LogLevel.Info, "Settings");
           }
           catch (Exception ex)
           {
               Logger.LogException(ex, "SettingsService.Reset");
           }
       }
        /// <summary>
        /// 导出配置到指定文件
        /// </summary>
        public static void Export(string filePath)
        {
            try
            {
                var settings = Load();
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(filePath, json);
                Logger.Log($"配置已导出: {filePath}", LogLevel.Info, "Settings");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SettingsService.Export");
                throw;
            }
        }

        /// <summary>
        /// 从指定文件导入配置
        /// </summary>
        public static void Import(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    Save(settings);
                    Logger.Log($"配置已导入: {filePath}", LogLevel.Info, "Settings");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SettingsService.Import");
                throw;
            }
        }
   }
}
