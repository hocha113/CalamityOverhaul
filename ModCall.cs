using System;
using System.Collections.Generic;
using CalamityOverhaul.API;
using Terraria;
using static CalamityOverhaul.CWRMod;

namespace CalamityOverhaul
{
    internal static class ModCall
    {
        internal enum CallType
        {
            SupertableRecipeDate,
            SupertableSetItem,
            SetNoRecipeHasFrme,
            Config_ForceReplaceResetContent,
            Config_AddExtrasContent,
            SupertableRecipeDate_ZenithWorld,
            SetSupertableRecipeDate,
        }

        public static object Hander(params object[] args) {
            int contentCount = args.Length;
            if (contentCount <= 0) {
                Instance.Logger.Info("Call was made with no parameters.");
                return null;
            }

            if (args[0] is string command) {
                return HandleString(command, args);
            }

            CallType callType = default;
            //首参非 CallType 则退
            if (Enum.IsDefined(typeof(CallType), args[0])) {
                callType = (CallType)args[0];
            }
            else {
                Instance.Logger.Info("Call was made without the correct CallType.");
                return null;
            }

            //超工台已删，枚举留作兼容，no-op
            if (callType is CallType.SupertableRecipeDate
                or CallType.SupertableSetItem
                or CallType.SupertableRecipeDate_ZenithWorld
                or CallType.SetSupertableRecipeDate
                or CallType.SetNoRecipeHasFrme) {
                Instance.Logger.Info($"Call-{callType} is deprecated: the Supertable system has been removed. This call does nothing.");
                if (callType == CallType.SetNoRecipeHasFrme) {
                    return args.Length > 1 ? args[1] as Recipe : null;
                }
                return null;
            }
            //强制内容替换开关
            else if (callType == CallType.Config_ForceReplaceResetContent) {
                return true;
            }
            //弃用仍回 true，旧模组兼容
            else if (callType == CallType.Config_AddExtrasContent) {
                return true;
            }

            return null;
        }

        private static object HandleString(string command, object[] args) {
            try {
                switch (command) {
                    case QuestLogsAPI.SupportsExternal:
                    case EntrustAPI.SupportsExternal:
                        return true;

                    case QuestLogsAPI.RegisterNode:
                        return TryMap(args, out var nodeArgs, required: true) && QuestLogsAPI.TryRegisterNode(nodeArgs);

                    case QuestLogsAPI.AddReward:
                        return HandleAddReward(args);

                    case QuestLogsAPI.IsCompleted:
                        return HandleIsCompleted(args);

                    case QuestLogsAPI.SetObjectiveProgress:
                        return HandleSetObjectiveProgress(args);

                    case EntrustAPI.Register:
                        return TryMap(args, out var entrustArgs, required: true) && EntrustAPI.TryRegister(entrustArgs);

                    case EntrustAPI.SetProgress:
                        return HandleEntrustSetProgress(args);

                    case EntrustAPI.SetStatus:
                        return HandleEntrustSetStatus(args);

                    case EntrustAPI.Unregister:
                        return args.Length > 1 && EntrustAPI.TryUnregister(Convert.ToString(args[1]));

                    case EntrustAPI.Get:
                        return args.Length > 1 ? EntrustAPI.TryGet(Convert.ToString(args[1])) : null;

                    default:
                        Instance.Logger.Info($"Call unknown command `{command}`.");
                        return null;
                }
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(command, ex.Message);
                return false;
            }
        }

        private static bool TryMap(object[] args, out Dictionary<string, object> map, bool required = false) {
            map = null;
            if (args.Length >= 2 && ExternalCallUtil.TryAsMap(args[1], out map)) {
                return true;
            }
            if (required) {
                ExternalCallUtil.LogFailed(Convert.ToString(args[0]), "expected Dictionary<string, object>");
            }
            return false;
        }

        private static object HandleAddReward(object[] args) {
            if (TryMap(args, out var map)) {
                string id = ExternalCallUtil.ReadString(map, "id");
                ExternalCallUtil.TryGet(map, out object itemRaw, "item", "itemId", "type");
                ExternalCallUtil.TryReadInt(itemRaw, out int item);
                int stack = 1;
                if (ExternalCallUtil.TryGet(map, out object stackRaw, "stack", "amount")) {
                    ExternalCallUtil.TryReadInt(stackRaw, out stack);
                }
                return QuestLogsAPI.TryAddReward(id, item, stack);
            }
            if (args.Length >= 3) {
                string id = Convert.ToString(args[1]);
                ExternalCallUtil.TryReadInt(args[2], out int item);
                int stack = 1;
                if (args.Length >= 4) {
                    ExternalCallUtil.TryReadInt(args[3], out stack);
                }
                return QuestLogsAPI.TryAddReward(id, item, stack);
            }
            ExternalCallUtil.LogFailed(QuestLogsAPI.AddReward, "bad args");
            return false;
        }

        private static object HandleIsCompleted(object[] args) {
            Player player = null;
            string id = null;
            if (TryMap(args, out var map)) {
                if (ExternalCallUtil.TryGet(map, out object playerRaw, "player") && playerRaw is Player p) {
                    player = p;
                }
                id = ExternalCallUtil.ReadString(map, "id");
            }
            else if (args.Length >= 3 && args[1] is Player fromArgs) {
                player = fromArgs;
                id = Convert.ToString(args[2]);
            }
            else if (args.Length >= 2) {
                player = Main.LocalPlayer;
                id = Convert.ToString(args[1]);
            }
            return QuestLogsAPI.TryIsCompleted(player ?? Main.LocalPlayer, id);
        }

        private static object HandleSetObjectiveProgress(object[] args) {
            Player player = Main.LocalPlayer;
            string id = null;
            float progress = 0f;
            if (TryMap(args, out var map)) {
                if (ExternalCallUtil.TryGet(map, out object playerRaw, "player") && playerRaw is Player p) {
                    player = p;
                }
                id = ExternalCallUtil.ReadString(map, "id");
                if (ExternalCallUtil.TryGet(map, out object progressRaw, "progress")) {
                    ExternalCallUtil.TryReadFloat(progressRaw, out progress);
                }
            }
            else if (args.Length >= 4 && args[1] is Player fromArgs) {
                player = fromArgs;
                id = Convert.ToString(args[2]);
                ExternalCallUtil.TryReadFloat(args[3], out progress);
            }
            else if (args.Length >= 3) {
                id = Convert.ToString(args[1]);
                ExternalCallUtil.TryReadFloat(args[2], out progress);
            }
            return QuestLogsAPI.TrySetObjectiveProgress(player, id, progress);
        }

        private static object HandleEntrustSetProgress(object[] args) {
            if (TryMap(args, out var map)) {
                string key = ExternalCallUtil.ReadString(map, "key", "id");
                float progress = 0f;
                if (ExternalCallUtil.TryGet(map, out object progressRaw, "progress")) {
                    ExternalCallUtil.TryReadFloat(progressRaw, out progress);
                }
                string status = ExternalCallUtil.ReadString(map, "status");
                return EntrustAPI.TrySetProgress(key, progress, status);
            }
            if (args.Length >= 3) {
                string status = args.Length >= 4 ? Convert.ToString(args[3]) : null;
                ExternalCallUtil.TryReadFloat(args[2], out float progress);
                return EntrustAPI.TrySetProgress(Convert.ToString(args[1]), progress, status);
            }
            ExternalCallUtil.LogFailed(EntrustAPI.SetProgress, "bad args");
            return false;
        }

        private static object HandleEntrustSetStatus(object[] args) {
            if (TryMap(args, out var map)) {
                string key = ExternalCallUtil.ReadString(map, "key", "id");
                string status = ExternalCallUtil.ReadString(map, "status");
                float? progress = null;
                if (ExternalCallUtil.TryGet(map, out object progressRaw, "progress")
                    && ExternalCallUtil.TryReadFloat(progressRaw, out float p)) {
                    progress = p;
                }
                return EntrustAPI.TrySetStatus(key, status, progress);
            }
            if (args.Length >= 3) {
                float? progress = null;
                if (args.Length >= 4 && ExternalCallUtil.TryReadFloat(args[3], out float p)) {
                    progress = p;
                }
                return EntrustAPI.TrySetStatus(Convert.ToString(args[1]), Convert.ToString(args[2]), progress);
            }
            ExternalCallUtil.LogFailed(EntrustAPI.SetStatus, "bad args");
            return false;
        }
    }
}
