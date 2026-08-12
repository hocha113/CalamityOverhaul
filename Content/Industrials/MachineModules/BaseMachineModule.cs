using CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.MachineModules
{
    /// <summary>
    /// 机器升级模块的物品基类:插入对应机器的模块槽生效,可拆卸转移。<br/>
    /// 图标由 <see cref="MiningModuleGlyph"/> 三层配方逐帧合成(切角钢牌+功能纹+巡行亮笔),
    /// 不占贴图资产;tooltip 自动带"适用机器"行(<see cref="MachineModuleText.DescribeTargets"/>)
    /// </summary>
    internal abstract class BaseMachineModule : ModItem, IMachineModule
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>本模块能装进哪些机器</summary>
        public abstract MachineModuleTarget ModuleTargets { get; }

        /// <summary>功能纹登记名,默认物品类名</summary>
        protected virtual string GlyphKey => GetType().Name;
        /// <summary>功能纹 SVG d 串;不给则退回通用钻齿纹</summary>
        protected virtual string GlyphPath => null;
        /// <summary>功能纹主色</summary>
        internal abstract Color Accent { get; }

        public override void SetStaticDefaults() {
            if (!string.IsNullOrEmpty(GlyphPath)) {
                MiningModuleGlyph.Register(GlyphKey, GlyphPath);
            }
        }

        public sealed override void SetDefaults() {
            Item.width = 30;
            Item.height = 30;
            //模块是设备不是耗材:一枚一件,槽位互斥由插座逻辑保证
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 1);
            SetModuleDefaults();
        }

        protected virtual void SetModuleDefaults() { }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            int index = tooltips.FindIndex(line => line.Name == "ItemName");
            if (index != -1) {
                tooltips.Insert(index + 1,
                    new TooltipLine(Mod, "MachineModuleTag", MachineModuleText.TagText.Value) {
                        OverrideColor = Accent,
                    });
                tooltips.Insert(index + 2,
                    new TooltipLine(Mod, "MachineModuleTargets",
                        MachineModuleText.TargetsLine.Format(MachineModuleText.DescribeTargets(ModuleTargets))) {
                        OverrideColor = new Color(190, 175, 155),
                    });
            }
            tooltips.Add(new TooltipLine(Mod, "MachineModuleHowTo", MachineModuleText.HowToText.Value) {
                OverrideColor = new Color(168, 152, 132),
            });
        }

        /// <summary>供机器面板插座直接绘制模块图标</summary>
        internal void DrawIcon(SpriteBatch spriteBatch, Vector2 center, float half, float alpha) {
            MiningModuleGlyph.Draw(spriteBatch, GlyphKey, center, half, alpha, Accent,
                0f, Main.GameUpdateCount * 0.02f);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            MiningModuleGlyph.Draw(spriteBatch, GlyphKey, position, 13f * scale,
                drawColor.A / 255f, Accent, 0f, Main.GameUpdateCount * 0.02f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor
            , ref float rotation, ref float scale, int whoAmI) {
            Vector2 center = Item.Center - Main.screenPosition;
            //暗处可寻:alpha 兜底 + 一点暖背光
            float a = MathHelper.Max(lightColor.A / 255f, 0.35f);
            MiningModuleGlyph.DrawBacklight(spriteBatch, center, 13f * scale, Accent, a * 0.24f);
            MiningModuleGlyph.Draw(spriteBatch, GlyphKey, center, 13f * scale, a, Accent,
                rotation, Main.GameUpdateCount * 0.02f);
            return false;
        }
    }
}
