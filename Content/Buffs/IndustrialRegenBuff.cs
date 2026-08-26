using CalamityOverhaul.Content.Industrials.ElectricPowers.HealingStations;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    /// <summary>
    /// 治疗光环:治疗站光环内获得,提高生命再生。
    /// 各端本地挂,回血走原版 lifeRegen 本地结算,零同步
    /// </summary>
    internal class IndustrialRegenBuff : ModBuff
    {
        //程序绘制接管,占位图只作兜底
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>lifeRegen 加值,原版单位2=每秒1点,8即每秒4点</summary>
        internal const int RegenBonus = 8;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        /// <summary>程序化图标:暖暗底板+治疗十字,随再生节律呼吸,上浮微光点</summary>
        public override bool PreDraw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
            int buffIndex, ref Terraria.DataStructures.BuffDrawParams drawParams) {
            var px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return true;
            }
            float alpha = drawParams.DrawColor.A / 255f;
            Point pos = drawParams.Position.ToPoint();
            const int Size = 32;
            float time = Main.GlobalTimeWrappedHourly;
            float breath = 0.78f + 0.22f * System.MathF.Sin(time * 3.2f);

            //暖暗底板+边框
            spriteBatch.Draw(px, new Rectangle(pos.X, pos.Y, Size, Size), new Color(34, 16, 22) * alpha);
            Color frame = new Color(170, 100, 122) * alpha;
            spriteBatch.Draw(px, new Rectangle(pos.X, pos.Y, Size, 1), frame);
            spriteBatch.Draw(px, new Rectangle(pos.X, pos.Y + Size - 1, Size, 1), frame);
            spriteBatch.Draw(px, new Rectangle(pos.X, pos.Y, 1, Size), frame);
            spriteBatch.Draw(px, new Rectangle(pos.X + Size - 1, pos.Y, 1, Size), frame);

            //治疗十字:随再生节律呼吸
            Color heal = HealStation.Tint;
            Color cross = heal * (0.75f * breath * alpha);
            cross.A = (byte)(200 * alpha);
            spriteBatch.Draw(px, new Rectangle(pos.X + 13, pos.Y + 7, 6, 18), cross);
            spriteBatch.Draw(px, new Rectangle(pos.X + 7, pos.Y + 13, 18, 6), cross);
            //十字芯提亮
            Color core = new Color(255, 235, 242) * (0.5f * breath * alpha);
            spriteBatch.Draw(px, new Rectangle(pos.X + 15, pos.Y + 9, 2, 14), core);
            spriteBatch.Draw(px, new Rectangle(pos.X + 9, pos.Y + 15, 14, 2), core);

            //两粒上浮微光点:循环爬升
            for (int i = 0; i < 2; i++) {
                float cycle = (time * 0.45f + i * 0.5f) % 1f;
                int dotY = pos.Y + Size - 5 - (int)(cycle * (Size - 10));
                int dotX = pos.X + 6 + i * 19;
                spriteBatch.Draw(px, new Rectangle(dotX, dotY, 2, 2),
                    heal * ((1f - cycle) * 0.8f * alpha));
            }
            return false;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.lifeRegen += RegenBonus;

            //受益可见性:周期性柔和光晕脉冲+两粒治愈光点,纯客户端表现
            //miscCounter逐玩家自然错拍;buff各端本地挂,远端玩家一样有
            if (Main.dedServ || player.dead || player.miscCounter % 54 != 0) {
                return;
            }
            if (!VaultUtils.IsPointOnScreen(player.Center - Main.screenPosition, 150)) {
                return;
            }
            PRTLoader.NewParticle<PRT_DefHealPulse>(player.MountedCenter, Vector2.Zero,
                HealStation.Tint, 1.35f)?.Configure(26, player.whoAmI);
            for (int i = 0; i < 2; i++) {
                Vector2 pos = player.MountedCenter + Main.rand.NextVector2Circular(16f, 22f);
                PRTLoader.NewParticle<PRT_DefHealMote>(pos, new Vector2(0, -0.3f),
                    HealStation.Tint, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(36, 54));
            }
        }
    }
}
