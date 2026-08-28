using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>BossChecklist 弱引用注册：石巨人后档位（17.5，介于石巨人与猪龙鱼之间）</summary>
    internal class SeaShrimpChecklist : SeaShrimpModSystem
    {
        public override void PostSetupContent() {
            if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist)) {
                return;
            }
            try {
                checklist.Call("LogBoss", Mod, nameof(SeaShrimpBoss), 17.5f,
                    () => SeaShrimpWorldFlag.DownedSeaShrimp,
                    ModContent.NPCType<SeaShrimpBoss>(),
                    new Dictionary<string, object> {
                        ["spawnItems"] = ModContent.ItemType<SeaShrimpSummonItem>(),
                        ["spawnInfo"] = Language.GetText("Mods.CalamityOverhaul.NPCs.SeaShrimpBoss.ChecklistSpawnInfo"),
                    });
            }
            catch (Exception e) {
                //弱引用容错：BossChecklist 签名漂移只记日志，不拦加载
                Mod.Logger.Warn($"BossChecklist 注册失败: {e.Message}");
            }
        }
    }
}
