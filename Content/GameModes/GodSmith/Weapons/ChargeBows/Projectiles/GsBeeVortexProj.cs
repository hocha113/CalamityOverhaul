using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows.Projectiles
{
    /// <summary>
    /// 蜂膝弓 T3 蜂后之怒：命中点悬置 1.5 秒的蜂涡（金雾光核，自身无伤），
    /// 每 20 帧放出 1 只蜂共 4 只（owner 端生成，respect 蜂巢背包与 12 只蜂池上限）
    /// </summary>
    internal class GsBeeVortexProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //不注册新键，显示名指向原版物品键
        public override LocalizedText DisplayName => Language.GetText("ItemName.BeesKnees");

        private static readonly Color HoneyGold = new(255, 200, 70);
        private static readonly Color HoneyBright = new(255, 236, 150);

        private const int LifeFrames = 90;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            //懒浮：identity 定相的微幅漂移
            Projectile.velocity = new Vector2(
                MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + Projectile.identity) * 0.22f,
                MathF.Cos(Main.GlobalTimeWrappedHourly * 1.7f + Projectile.identity * 0.6f) * 0.18f);

            int elapsed = LifeFrames - Projectile.timeLeft;
            //每 20 帧放 1 蜂共 4 只（owner 端权威）
            if (Projectile.IsOwnedByLocalPlayer() && elapsed > 0 && elapsed % 20 == 0 && elapsed <= 80) {
                GsBeesKnees.SpawnBee(Main.player[Projectile.owner], Projectile.GetSource_FromThis(),
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), 12);
            }

            if (!VaultUtils.isServer) {
                Lighting.AddLight(Projectile.Center, HoneyGold.ToVector3() * 0.22f);
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_Sparkle>(
                        Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                        Main.rand.NextVector2Circular(0.8f, 0.8f), HoneyBright, Main.rand.NextFloat(0.25f, 0.4f))
                        ?.Configure(HoneyGold, Main.rand.Next(12, 20), 0.06f, 0.7f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float life = Projectile.timeLeft / (float)LifeFrames;
            float env = MathHelper.Clamp(MathF.Sin(life * MathHelper.Pi) * 1.6f, 0f, 1f);
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.identity * 0.77f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //金雾双层（SoftGlow 黑底加色，A=0）
            Color outer = HoneyGold * (0.4f * env * pulse);
            outer.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, outer, 0f, glow.Size() / 2f, 1.15f * pulse, SpriteEffects.None);
            Color inner = HoneyBright * (0.55f * env);
            inner.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, inner, 0f, glow.Size() / 2f, 0.6f * pulse, SpriteEffects.None);
            return false;
        }
    }
}
