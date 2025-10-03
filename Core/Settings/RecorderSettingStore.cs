using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using NLog;
using SharpConfig;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public enum RecorderSettingKeys
    {
        ServerIp,
        ServerPort,
        RecordingFile
    }

    public class RecorderSettingsStore
    {
        private static readonly string CFG_FILE_NAME = "recorder.cfg";
        private static readonly object _lock = new();
        private static RecorderSettingsStore _instance;
        private readonly Configuration _configuration;
        private readonly ConcurrentDictionary<string, object> _settingsCache = new();
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, string> defaultRecorderSettings = new()
        {
            { RecorderSettingKeys.ServerIp.ToString(), "127.0.0.1" },
            { RecorderSettingKeys.ServerPort.ToString(), "5002" },
            { RecorderSettingKeys.RecordingFile.ToString(), "recorded_audio.raw" }
        };

        public string ConfigFileName { get; } = CFG_FILE_NAME;
        public static string Path { get; set; } = "";

        public static RecorderSettingsStore Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new RecorderSettingsStore();
                return _instance;
            }
        }

        private RecorderSettingsStore()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
                if (arg.Trim().StartsWith("-recordercfg="))
                {
                    Path = arg.Trim().Replace("-recordercfg=", "").Trim();
                    if (!Path.EndsWith("\\")) Path = Path + "\\";
                    Logger.Info($"Found -recordercfg loading: {Path + ConfigFileName}");
                }

            try
            {
                var count = 0;
                while (IsFileLocked(new FileInfo(Path + ConfigFileName)) && count < 10)
                {
                    Thread.Sleep(200);
                    count++;
                }

                _configuration = Configuration.LoadFromFile(Path + ConfigFileName);
            }
            catch (FileNotFoundException)
            {
                Logger.Info($"Did not find recorder config file at path {Path}{ConfigFileName}, initializing with default config");
                _configuration = new Configuration
                {
                    new Section("Recorder Settings")
                };
                SetRecorderSetting(RecorderSettingKeys.ServerIp, defaultRecorderSettings[RecorderSettingKeys.ServerIp.ToString()]);
                SetRecorderSetting(RecorderSettingKeys.ServerPort, int.Parse(defaultRecorderSettings[RecorderSettingKeys.ServerPort.ToString()]));
                SetRecorderSetting(RecorderSettingKeys.RecordingFile, defaultRecorderSettings[RecorderSettingKeys.RecordingFile.ToString()]);
                Save();
            }
            catch (ParserException ex)
            {
                Logger.Error(ex, "Failed to parse recorder config, potentially corrupted. Creating backup and re-initializing with default config");
                try
                {
                    File.Copy(Path + ConfigFileName, Path + ConfigFileName + ".bak", true);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to create backup of corrupted config file, ignoring");
                }

                _configuration = new Configuration
                {
                    new Section("Recorder Settings")
                };
                SetRecorderSetting(RecorderSettingKeys.ServerIp, defaultRecorderSettings[RecorderSettingKeys.ServerIp.ToString()]);
                SetRecorderSetting(RecorderSettingKeys.ServerPort, int.Parse(defaultRecorderSettings[RecorderSettingKeys.ServerPort.ToString()]));
                SetRecorderSetting(RecorderSettingKeys.RecordingFile, defaultRecorderSettings[RecorderSettingKeys.RecordingFile.ToString()]);
                Save();
            }
        }

        public static bool IsFileLocked(FileInfo file)
        {
            if (!file.Exists) return false;
            try
            {
                using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }

        private SharpConfig.Setting GetSetting(string section, string setting)
        {
            if (!_configuration.Contains(section)) _configuration.Add(section);

            if (!_configuration[section].Contains(setting))
            {
                if (defaultRecorderSettings.ContainsKey(setting))
                {
                    _configuration[section].Add(new SharpConfig.Setting(setting, defaultRecorderSettings[setting]));
                    Save();
                }
                else
                {
                    _configuration[section].Add(new SharpConfig.Setting(setting, ""));
                    Save();
                }
            }
            return _configuration[section][setting];
        }

        public int GetRecorderSettingInt(RecorderSettingKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (int)val;
            var setting = GetSetting("Recorder Settings", key.ToString());
            if (setting.RawValue.Length == 0) return 0;
            _settingsCache[key.ToString()] = setting.IntValue;
            return setting.IntValue;
        }

        public double GetRecorderSettingDouble(RecorderSettingKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (double)val;
            var setting = GetSetting("Recorder Settings", key.ToString());
            if (setting.RawValue.Length == 0) return 0D;
            _settingsCache[key.ToString()] = setting.DoubleValue;
            return setting.DoubleValue;
        }

        public bool GetRecorderSettingBool(RecorderSettingKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (bool)val;
            var setting = GetSetting("Recorder Settings", key.ToString());
            if (setting.RawValue.Length == 0) return false;
            _settingsCache[key.ToString()] = setting.BoolValue;
            return setting.BoolValue;
        }

        public string GetRecorderSettingString(RecorderSettingKeys key)
        {
            if (_settingsCache.TryGetValue(key.ToString(), out var val)) return (string)val;
            var setting = GetSetting("Recorder Settings", key.ToString());
            if (setting.RawValue.Length == 0) return "";
            _settingsCache[key.ToString()] = setting.StringValue;
            return setting.StringValue;
        }

        public void SetRecorderSetting(RecorderSettingKeys key, string value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Recorder Settings", key.ToString(), value);
        }

        public void SetRecorderSetting(RecorderSettingKeys key, int value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Recorder Settings", key.ToString(), value);
        }

        public void SetRecorderSetting(RecorderSettingKeys key, double value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Recorder Settings", key.ToString(), value);
        }

        public void SetRecorderSetting(RecorderSettingKeys key, bool value)
        {
            _settingsCache.TryRemove(key.ToString(), out _);
            SetSetting("Recorder Settings", key.ToString(), value);
        }

        private void SetSetting(string section, string key, object setting)
        {
            if (setting == null) setting = "";
            if (!_configuration.Contains(section)) _configuration.Add(section);

            if (!_configuration[section].Contains(key))
                _configuration[section].Add(new SharpConfig.Setting(key, setting));
            else
            {
                if (setting is bool)
                    _configuration[section][key].BoolValue = (bool)setting;
                else if (setting is string)
                    _configuration[section][key].StringValue = (string)setting;
                else if (setting is int)
                    _configuration[section][key].IntValue = (int)setting;
                else if (setting is double)
                    _configuration[section][key].DoubleValue = (double)setting;
                else
                    Logger.Error("Unknown Setting Type - Not Saved ");
            }
            Save();
        }

        private void Save()
        {
            lock (_lock)
            {
                try
                {
                    _configuration.SaveToFile(Path + ConfigFileName);
                }
                catch (Exception)
                {
                    Logger.Error("Unable to save recorder settings!");
                }
            }
        }
    }
}