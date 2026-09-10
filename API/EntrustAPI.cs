using System;
using System.Collections;
using System.Collections.Generic;
using CalamityOverhaul.Content.EntrustManager;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.API
{
    /// <summary>
    /// 委托卷宗对外门面。只做展示、进度、状态；不发奖、不接管贡献度网络。<br/>
    /// 听服 no-op。出世界 <c>ClearAll</c>，调用方进世界后必须自己再 Register。
    /// Register 是 upsert：Key 已存在时只更新进度 / 状态 / 优先级并返回 true，不再弹"新委托"，
    /// 所以调用方可以每隔若干帧无脑 Register 做 Ensure。<br/>
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
            float progress01 = 0f, string status = null, LocalizedText providerName = null, Color? providerColor = null,
            int priority = 0, bool notify = true) {
            var args = new Dictionary<string, object> {
                ["key"] = key,
                ["title"] = title,
                ["summary"] = summary,
                ["category"] = category,
                ["progress"] = progress01,
                ["priority"] = priority,
                ["notify"] = notify
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

        /// <summary>注册或更新一条委托。新建时若 status 缺省 / Active 且 notify 不为 false，会弹"新委托"并自动转为关注</summary>
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

                float progress = 0f;
                bool hasProgress = ExternalCallUtil.TryGet(args, out object progressRaw, "progress")
                    && ExternalCallUtil.TryReadFloat(progressRaw, out progress);
                QuestEntryStatus status = QuestEntryStatus.Active;
                bool hasStatus = ExternalCallUtil.TryGet(args, out object statusRaw, "status")
                    && TryParseStatus(statusRaw, out status);
                int priority = 0;
                bool hasPriority = ExternalCallUtil.TryGet(args, out object priorityRaw, "priority")
                    && ExternalCallUtil.TryReadInt(priorityRaw, out priority);

                var existing = ui.GetEntry(key);
                if (existing != null) {
                    //upsert：Ensure 循环会反复打进来，不算错，也不重弹通知
                    if (hasProgress) {
                        existing.Progress = MathHelper.Clamp(progress, 0f, 1f);
                    }
                    if (hasPriority) {
                        existing.Priority = priority;
                        ui.MarkFilterDirty();
                    }
                    if (hasStatus) {
                        ApplyStatus(ui, existing, status);
                    }
                    return true;
                }

                ExternalCallUtil.TryGet(args, out object titleRaw, "title");
                ExternalCallUtil.TryGet(args, out object summaryRaw, "summary", "description");
                ExternalCallUtil.TryGet(args, out object categoryRaw, "category");
                var entry = new EntrustEntryData(
                    key,
                    ExternalCallUtil.CoerceText(titleRaw),
                    ExternalCallUtil.CoerceText(summaryRaw),
                    ExternalCallUtil.CoerceText(categoryRaw));

                if (hasProgress) {
                    entry.Progress = MathHelper.Clamp(progress, 0f, 1f);
                }
                if (hasStatus) {
                    entry.Status = status;
                }
                if (hasPriority) {
                    entry.Priority = priority;
                }
                if (ExternalCallUtil.TryGet(args, out object progressLabelRaw, "progressLabel", "progressText")) {
                    entry.ProgressLabel = ExternalCallUtil.CoerceText(progressLabelRaw);
                }

                //notify=false：跨会话早已首发过的委托，直接落关注态，不再弹"新委托"。
                //与本模组各委托线的做法一致（Fishoil / OldNet 预置 Tracked）
                ExternalCallUtil.TryReadBool(args, true, out bool notify, "notify", "announce");
                if (!notify && entry.Status == QuestEntryStatus.Active) {
                    entry.Status = QuestEntryStatus.Tracked;
                    entry.IsNew = false;
                }

                entry.Provider = BuildProvider(args);

                ui.RegisterQuest(entry);
                return ui.GetEntry(key) != null;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(Register, ex.Message);
                return false;
            }
        }

        /// <summary>Key 不存在返回 false 且不记日志：出世界被清空后再进世界前，这是预期状态</summary>
        public static bool TrySetProgress(string key, float progress01, string status = null) {
            try {
                if (!TryGetClientUI(SetProgress, out var ui)) {
                    return false;
                }
                var entry = ui.GetEntry(key);
                if (entry == null) {
                    return false;
                }
                entry.Progress = MathHelper.Clamp(progress01, 0f, 1f);
                if (!string.IsNullOrWhiteSpace(status) && TryParseStatus(status, out var st)) {
                    ApplyStatus(ui, entry, st);
                }
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(SetProgress, ex.Message);
                return false;
            }
        }

        /// <summary>Key 不存在静默 false；status 拼错才记日志</summary>
        public static bool TrySetStatus(string key, string status, float? progress01 = null) {
            try {
                if (!TryGetClientUI(SetStatus, out var ui)) {
                    return false;
                }
                if (!TryParseStatus(status, out var st)) {
                    ExternalCallUtil.LogFailed(SetStatus, $"invalid status `{status}` for `{key}`");
                    return false;
                }
                var entry = ui.GetEntry(key);
                if (entry == null) {
                    return false;
                }
                if (progress01.HasValue) {
                    entry.Progress = MathHelper.Clamp(progress01.Value, 0f, 1f);
                }
                ApplyStatus(ui, entry, st);
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(SetStatus, ex.Message);
                return false;
            }
        }

        private static bool IsInProgress(QuestEntryStatus status)
            => status is QuestEntryStatus.Active or QuestEntryStatus.Tracked or QuestEntryStatus.Suspended;

        /// <summary>
        /// 进行中三态（Active / Tracked / Suspended）由玩家在卷宗里右键、中键切换，外模只该驱动结果态。
        /// 外模每帧 Ensure 传 Active 时，若条目已处于进行中的任一态就保持玩家的选择，
        /// 否则会把玩家关注的条目打回 Active 并弹「取消关注」。显式传 Tracked / Suspended 仍照办
        /// </summary>
        private static void ApplyStatus(QuestManagerUI ui, EntrustEntryData entry, QuestEntryStatus requested) {
            if (entry.Status == requested) {
                return;
            }
            if (requested == QuestEntryStatus.Active && IsInProgress(entry.Status)) {
                return;
            }
            ui.SetEntryStatus(entry.Key, requested, entry.Progress);
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

        /// <summary>只读快照，键：key / title / summary / category / progress / status / priority。缺席回 null</summary>
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
                    ["status"] = entry.Status.ToString(),
                    ["priority"] = entry.Priority
                };
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(Get, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 一次性委托人，不进官方七人表。只给名字时是空戳；
        /// 可再给 providerItem（物品头像）/ providerTexture（贴图头像）/ providerGlyph（归一 [-1,1] 的 SVG 纹样，M/L/H/V/C/Q，禁 A 弧）
        /// </summary>
        private static EntrustProvider BuildProvider(IDictionary<string, object> args) {
            LocalizedText providerName = null;
            if (ExternalCallUtil.TryGet(args, out object providerNameRaw, "provider", "providerName")) {
                providerName = ExternalCallUtil.CoerceText(providerNameRaw);
            }
            if (providerName == null || providerName == LocalizedText.Empty) {
                return null;
            }

            Color accent = Color.White;
            if (ExternalCallUtil.TryGet(args, out object colorRaw, "providerColor", "color", "accent")
                && TryReadColor(colorRaw, out Color color)) {
                accent = color;
            }
            int avatarItem = 0;
            if (ExternalCallUtil.TryGet(args, out object avatarItemRaw, "providerItem", "providerAvatarItem")) {
                ExternalCallUtil.TryReadInt(avatarItemRaw, out avatarItem);
            }
            string avatarTexture = ExternalCallUtil.ReadString(args, "providerTexture", "providerAvatarTexture");
            string glyph = ExternalCallUtil.ReadString(args, "providerGlyph", "glyph");

            return new EntrustProvider {
                Name = providerName,
                Accent = accent,
                GlyphD = glyph ?? string.Empty,
                AvatarItemType = Math.Max(avatarItem, 0),
                AvatarTexturePath = string.IsNullOrWhiteSpace(avatarTexture) ? null : avatarTexture
            };
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
