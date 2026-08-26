using CalamityOverhaul.Content.Industrials.ElectricPowers.ShieldGenerators;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    /// <summary>
    /// 能量护盾:护盾发生器光环内获得,维持护盾池充能,受击时吸收部分伤害。
    /// 各端本地挂,吸收结算在受击玩家自己的端上(见 <see cref="ShieldGeneratorPlayer"/>)。
    /// 图标为程序化护盾池量表:填充高度=当前池量,吸收瞬间白闪
    /// </summary>
    internal class IndustrialShieldBuff : ModBuff
    {
        //程序绘制接管,占位图只作兜底
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.GetModPlayer<ShieldGeneratorPlayer>().ShieldAuraActive = true;
        }

        /// <summary>
        /// 程序化图标:暗紫底板+护盾池纵向填充量表+池面亮线,
        /// 吸收瞬间整格白闪。图标反馈直连本地池量,不是静态贴图
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, int buffIndex, ref BuffDrawParams drawParams) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return true;
            }
            var sp = Main.LocalPlayer.GetModPlayer<ShieldGeneratorPlayer>();
            float fill = MathHelper.Clamp(sp.ShieldCharge / ShieldGeneratorPlayer.ShieldMax, 0f, 1f);
            float alpha = drawParams.DrawColor.A / 255f;
            Point pos = drawParams.Position.ToPoint();
            const int Size = 32;

            //暗紫底板+边框
            spriteBatch.Draw(px, new Rectangle(pos.X, pos.Y, Size, Size), new Color(16, 13, 34) * alpha);
            spriteBatch.Draw(px, new Rectangle(pos.X, pos.Y, Size, 1), new Color(90, 80, 160) * alpha);
            spriteBatch.Draw(px, new Rectangle(pos.X, pos.Y + Size - 1, Size, 1), new Color(90, 80, 160) * alpha);
            spriteBatch.Draw(px, new Rectangle(pos.X, pos.Y, 1, Size), new Color(90, 80, 160) * alpha);
            spriteBatch.Draw(px, new Rectangle(pos.X + Size - 1, pos.Y, 1, Size), new Color(90, 80, 160) * alpha);

            //护盾池填充:自底而上,呼吸微光
            int fillH = (int)(fill * (Size - 4));
            if (fillH > 0) {
                float breath = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.8f);
                Color body = ShieldGenerator.Tint * (0.55f * breath * alpha);
                body.A = (byte)(180 * alpha);
                spriteBatch.Draw(px, new Rectangle(pos.X + 2, pos.Y + Size - 2 - fillH, Size - 4, fillH), body);
                //池面亮线:能量膜的水位
                Color rim = new Color(225, 218, 255) * (0.9f * breath * alpha);
                spriteBatch.Draw(px, new Rectangle(pos.X + 2, pos.Y + Size - 3 - fillH, Size - 4, 2), rim);
            }

            //吸收闪:整格白闪快退
            if (sp.AbsorbFlash > 0.02f) {
                spriteBatch.Draw(px, new Rectangle(pos.X + 1, pos.Y + 1, Size - 2, Size - 2),
                    Color.White * (0.55f * sp.AbsorbFlash * alpha));
            }
            return false;
        }
    }
}
