using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.BossChecklist
{
    /// <summary>LogBoss 弱引用登记公共入口（容错：接口签名漂移只记日志，不拦加载）</summary>
    internal static class BossLogRegistry
    {
        public static void Register(Mod host, string internalName, float progression,
            Func<bool> downed, object npcTypes, Dictionary<string, object> extra) {
            if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist)) {
                return;
            }
            try {
                checklist.Call("LogBoss", host, internalName, progression, downed, npcTypes, extra);
            } catch (Exception e) {
                host.Logger.Warn($"BossChecklist 注册失败({internalName}): {e.Message}");
            }
        }
    }
}
