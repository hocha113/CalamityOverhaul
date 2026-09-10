using System;
using System.Collections;
using System.Collections.Generic;
using CalamityOverhaul.Content.EntrustManager;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.API
{
    /// <summary>
    /// 委托卷宗对外门面。只做展示、进度、状态；不发奖、不接管贡献度网络。
    /// 听服 no-op。出世界 <c>ClearAll</c>，调用方进世界后必须自己再 Register。
    /// Key 请带模组名前缀，例如 <c>CalamityEntropy.Cruiser</c>
    /// </summary>
    public static class EntrustAPI
    {
        public const string SupportsExternal = "Entrust.SupportsExternal";
        public const string Register = "Entrust.Register";
        public const string SetProgress = "Entrust.SetProgress";
        public const string SetStatus = "Entrust.SetStatus";
        public const string Unregister = "Entrust.Unregister";
        public const string Get = "Entrust.Get";

        public static bool RegisterEntrust(string key, LocalizedText title, LocalizedText summary, LocalizedText category,
            float progress01 = 0f, string status = null, LocalizedText providerName = null, Color? providerColor = null, int priority = 0) {
            var args = new Dictionary<string, object> {
                ["key"] = key,
                ["title"] = title,
                ["summary"] = summary,
                ["category"] = category,
                ["progress"] = progress01,
                ["priority"] = priority
            };
            if (!string.IsNullOrWhiteSpace(status)) {
                args["status"] = status;
            }
            if (providerName != null) {
                args["provider"] = providerName;
            }
            if (providerColor.HasValue) {
                args["providerColor"] = providerColor.Value;
            }
            return TryRegister(args);
        }

        public static bool SetEntrustProgress(string key, float progress01, string status = null)
            => TrySetProgress(key, progress01, status);

        public static bool UnregisterEntrust(string key) => TryUnregister(key);

        public static IDictionary<string, object> GetEntrust(string key) => TryGet(key);

        public static bool TryRegister(IDictionary<string, object> args) {
            try {
                if (!TryGetClientUI(Register, out var ui)) {
                    return false;
                }
                if (args == null) {
                    ExternalCallUtil.LogFailed(Register, "args null");
                    return false;
                }

                string key = ExternalCallUtil.ReadString(args, "key", "id");
                if (string.IsNullOrWhiteSpace(key)) {
                    ExternalCallUtil.LogFailed(Register, "key empty");
                    return false;
                }
                if (ui.GetEntry(key) != null) {
                    ExternalCallUtil.LogFailed(Register, $"duplicate key `{key}`");
                    return false;
                }

                ExternalCallUtil.TryGet(args, out object titleRaw, "title");
                ExternalCallUtil.TryGet(args, out object summaryRaw, "summary", "description");
                ExternalCallUtil.TryGet(args, out object categoryRaw, "category");
                var entry = new EntrustEntryData(
                    key,
                    ExternalCallUtil.CoerceText(titleRaw, key, "Title"),
                    ExternalCallUtil.CoerceText(summaryRaw, key, "Summary"),
                    ExternalCallUtil.CoerceText(categoryRaw, key, "Category"));

                if (ExternalCallUtil.TryGet(args, out object progressRaw, "progress")
                    && ExternalCallUtil.TryReadFloat(progressRaw, out float progress)) {
                    entry.Progress = MathHelper.Clamp(progress, 0f, 1f);
                }
                if (ExternalCallUtil.TryGet(args, out object statusRaw, "status")
                    && TryParseStatus(statusRaw, out var status)) {
                    entry.Status = status;
                }
                if (ExternalCallUtil.TryGet(args, out object priorityRaw, "priority")
                    && ExternalCallUtil.TryReadInt(priorityRaw, out int priority)) {
                    entry.Priority = priority;
                }

                LocalizedText providerName = null;
                if (ExternalCallUtil.TryGet(args, out object providerNameRaw, "provider", "providerName")) {
                    providerName = ExternalCallUtil.CoerceText(providerNameRaw, key, "Provider");
                }
                Color? accent = null;
                if (ExternalCallUtil.TryGet(args, out object colorRaw, "providerColor", "color", "accent")
                    && TryReadColor(colorRaw, out Color color)) {
                    accent = color;
                }
                if (providerName != null && providerName != LocalizedText.Empty) {
                    entry.Provider = new EntrustProvider {
                        Name = providerName,
                        Accent = accent ?? Color.White,
                        GlyphD = string.Empty
                    };
                }

                ui.RegisterQuest(entry);
                return ui.GetEntry(key) != null;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(Register, ex.Message);
                return false;
            }
        }

        public static bool TrySetProgress(string key, float progress01, string status = null) {
            try {
                if (!TryGetClientUI(SetProgress, out var ui)) {
                    return false;
                }
                var entry = ui.GetEntry(key);
                if (entry == null) {
                    ExternalCallUtil.LogFailed(SetProgress, $"missing `{key}`");
                    return false;
                }
                entry.Progress = MathHelper.Clamp(progress01, 0f, 1f);
                if (!string.IsNullOrWhiteSpace(status) && TryParseStatus(status, out var st) && entry.Status != st) {
                    ui.SetEntryStatus(key, st, entry.Progress);
                }
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(SetProgress, ex.Message);
                return false;
            }
        }

        public static bool TrySetStatus(string key, string status, float? progress01 = null) {
            try {
                if (!TryGetClientUI(SetStatus, out var ui)) {
                    return false;
                }
                var entry = ui.GetEntry(key);
                if (entry == null || !TryParseStatus(status, out var st)) {
                    ExternalCallUtil.LogFailed(SetStatus, $"missing/invalid `{key}` / `{status}`");
                    return false;
                }
                if (progress01.HasValue) {
                    entry.Progress = MathHelper.Clamp(progress01.Value, 0f, 1f);
                }
                if (entry.Status == st) {
                    return true;
                }
                return ui.SetEntryStatus(key, st, entry.Progress);
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(SetStatus, ex.Message);
                return false;
            }
        }

        public static bool TryUnregister(string key) {
            try {
                if (!TryGetClientUI(Unregister, out var ui)) {
                    return false;
                }
                if (ui.GetEntry(key) == null) {
                    return false;
                }
                ui.UnregisterQuest(key);
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(Unregister, ex.Message);
                return false;
            }
        }

        /// <summary>只读快照，键：key / title / summary / category / progress / status</summary>
        public static IDictionary<string, object> TryGet(string key) {
            try {
                if (!TryGetClientUI(Get, out var ui, silent: true)) {
                    return null;
                }
                var entry = ui.GetEntry(key);
                if (entry == null) {
                    return null;
                }
                return new Dictionary<string, object> {
                    ["key"] = entry.Key,
                    ["title"] = entry.Title,
                    ["summary"] = entry.Summary,
                    ["category"] = entry.Category,
                    ["progress"] = entry.Progress,
                    ["status"] = entry.Status.ToString()
                };
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(Get, ex.Message);
                return null;
            }
        }

        private static bool TryGetClientUI(string command, out QuestManagerUI ui, bool silent = false) {
            ui = null;
            if (Main.dedServ) {
                return false;
            }
            if (!QuestManagerUI.TryGetInstance(out ui)) {
                if (!silent) {
                    ExternalCallUtil.LogFailed(command, "QuestManagerUI not ready");
                }
                return false;
            }
            return true;
        }

        private static bool TryParseStatus(object raw, out QuestEntryStatus status) {
            status = QuestEntryStatus.Active;
            if (raw is QuestEntryStatus direct) {
                status = direct;
                return true;
            }
            string name = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture);
            return !string.IsNullOrWhiteSpace(name) && Enum.TryParse(name, true, out status);
        }

        private static bool TryReadColor(object raw, out Color color) {
            color = Color.White;
            if (raw is Color c) {
                color = c;
                return true;
            }
            if (raw is IList list && list.Count >= 3) {
                if (ExternalCallUtil.TryReadInt(list[0], out int r)
                    && ExternalCallUtil.TryReadInt(list[1], out int g)
                    && ExternalCallUtil.TryReadInt(list[2], out int b)) {
                    int a = 255;
                    if (list.Count >= 4) {
                        ExternalCallUtil.TryReadInt(list[3], out a);
                    }
                    color = new Color(r, g, b, a);
                    return true;
                }
            }
            return false;
        }
    }
}
