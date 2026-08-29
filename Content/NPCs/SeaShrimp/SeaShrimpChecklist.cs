using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.OtherMods.BossChecklist;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>
    /// BossChecklist 图鉴登记：石巨人后档位（17.5，介于石巨人与猪龙鱼之间）。
    /// 头像不用静态图，走 <see cref="SeaShrimpPortraitActor"/> 实时沙盒绘制
    /// </summary>
    internal class SeaShrimpChecklist : SeaShrimpModSystem
    {
        public override void PostSetupContent() {
            BossLogRegistry.Register(Mod, nameof(SeaShrimpBoss), 17.5f,
                () => SeaShrimpWorldFlag.DownedSeaShrimp,
                ModContent.NPCType<SeaShrimpBoss>(),
                new Dictionary<string, object> {
                    ["spawnItems"] = ModContent.ItemType<SeaShrimpSummonItem>(),
                    ["spawnInfo"] = Language.GetText("Mods.CalamityOverhaul.NPCs.SeaShrimpBoss.ChecklistSpawnInfo"),
                    ["collectibles"] = new List<int> { ModContent.ItemType<SeaShrimpRelic>() },
                    ["customPortrait"] = (Action<SpriteBatch, Rectangle, Color>)((sb, rect, color)
                        => BossPortraitStage.Draw(sb, rect, color, SeaShrimpPortraitActor.Instance)),
                });
        }
    }
}
