using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>寒雾带：漂移的迟滞区域，入内减速（本地玩家自判），无直接伤害</summary>
    internal class CultistFrostMistZone : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float Radius = 150f;
        private const int LifeTime = 260;
        private const int FadeTime = 40;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
        }

        private float Density {
            get {
                float fadeIn = MathHelper.Clamp((LifeTime - Projectile.timeLeft) / 30f, 0f, 1f);
                float fadeOut = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);
                return fadeIn * fadeOut;
            }
        }

        public override void AI() {
            Projectile.velocity *= 0.985f;

            //迟滞判定：各端只处理本地玩家（移动权威在本机）
            if (!Main.dedServ) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && lp.Distance(Projectile.Center) < Radius * Density + 40f) {
                    lp.AddBuff(BuffID.Chilled, 4);
                }
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_CultistFrost>(
                    Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.8f, Radius * 0.8f),
                    Main.rand.NextVector2Circular(0.4f, 0.4f) - Vector2.UnitY * 0.2f,
                    CultistPalette.IceBright, Main.rand.NextFloat(0.4f, 0.8f) * Density)?.Configure(Main.rand.Next(20, 34));
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.IceMain.ToVector3() * (0.4f * Density));
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D fog = CWRAsset.Fog.Value;
            float density = Density;

            //雾体走 AlphaBlend 染色（Fog 白RGB+真alpha），三层镜像错相
            for (int i = 0; i < 3; i++) {
                float phase = Main.GlobalTimeWrappedHourly * 0.5f + i * 2.1f + Projectile.whoAmI * 0.7f;
                Vector2 off = new((float)Math.Sin(phase) * 26f, (float)Math.Cos(phase * 0.8f) * 18f);
                SpriteEffects fx = (i + Projectile.whoAmI) % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                Color tint = Color.Lerp(CultistPalette.IceDeep, CultistPalette.IceMain, i / 2f) * (0.24f * density);
                sb.Draw(fog, drawPos + off, null, tint, phase * 0.14f, fog.Size() / 2f,
                    (Radius / 110f) * (0.9f + i * 0.2f), fx, 0f);
            }
            return false;
        }
    }
}
