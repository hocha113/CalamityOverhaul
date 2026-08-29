using CalamityOverhaul.Content.NPCs.FestersandSerpents.Rendering;
using CalamityOverhaul.OtherMods.BossChecklist;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>
    /// BossChecklist 图鉴登记：肉山后早期档位（7.5）。
    /// 头像走 <see cref="FssPortraitActor"/> 实时沙盒绘制
    /// </summary>
    internal class FssChecklist : FssModSystem
    {
        public override void PostSetupContent() {
            BossLogRegistry.Register(Mod, nameof(FssHead), 7.5f,
                () => FssWorldFlag.DownedFesterSerpent,
                new List<int> {
                    ModContent.NPCType<FssHead>(),
                    ModContent.NPCType<FssBody>(),
                    ModContent.NPCType<FssTail>(),
                },
                new Dictionary<string, object> {
                    ["spawnItems"] = ModContent.ItemType<FssFesterBud>(),
                    ["spawnInfo"] = Language.GetText("Mods.CalamityOverhaul.NPCs.FssHead.ChecklistSpawnInfo"),
                    ["customPortrait"] = (Action<SpriteBatch, Rectangle, Color>)((sb, rect, color)
                        => BossPortraitStage.Draw(sb, rect, color, FssPortraitActor.Instance)),
                });
        }
    }
}
