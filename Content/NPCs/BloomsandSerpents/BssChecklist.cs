using CalamityOverhaul.OtherMods.BossChecklist;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// BossChecklist 图鉴登记：克眼后沙漠档位（2.5）。
    /// 头像走 <see cref="BssPortraitActor"/> 实时沙盒绘制
    /// </summary>
    internal class BssChecklist : BssModSystem
    {
        public override void PostSetupContent() {
            BossLogRegistry.Register(Mod, nameof(BssHead), 2.5f,
                () => BssWorldFlag.DownedBloomSerpent,
                new List<int> {
                    ModContent.NPCType<BssHead>(),
                    ModContent.NPCType<BssBody>(),
                    ModContent.NPCType<BssTail>(),
                },
                new Dictionary<string, object> {
                    ["spawnItems"] = ModContent.ItemType<BssBloomBud>(),
                    ["spawnInfo"] = Language.GetText("Mods.CalamityOverhaul.NPCs.BssHead.ChecklistSpawnInfo"),
                    ["customPortrait"] = (Action<SpriteBatch, Rectangle, Color>)((sb, rect, color)
                        => BossPortraitStage.Draw(sb, rect, color, BssPortraitActor.Instance)),
                });
        }
    }
}
