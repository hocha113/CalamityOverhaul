using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
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
                bool inside = lp.active && !lp.dead && lp.Distance(Projectile.Center) < Radius * Density + 40f;
                if (inside) {
                    lp.AddBuff(BuffID.Chilled, 4);
                    //入雾扰动：玩家身周雾旋涡（过冷雾对闯入者的反应）
                    if (Main.rand.NextBool(3)) {
                        Vector2 swirl = (lp.Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                            .RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(1f, 2.4f);
                        PRTLoader.NewParticle<PRT_CultistFrost>(lp.Center + Main.rand.NextVector2Circular(30f, 30f),
                            swirl + lp.velocity * 0.2f, CultistPalette.IceBright,
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24));
                    }
                }
            }

            if (!VaultUtils.isServer) {
                //雾中悬晶：内部漂浮微晶闪点
                if (Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_CultistFrost>(
                        Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.8f, Radius * 0.8f),
                        Main.rand.NextVector2Circular(0.4f, 0.4f) - Vector2.UnitY * 0.2f,
                        CultistPalette.IceBright, Main.rand.NextFloat(0.4f, 0.8f) * Density)?.Configure(Main.rand.Next(20, 34));
                }
                //缘部霜花：雾缘偶发凝出细小晶棱，刹那即化
                if (Main.rand.NextBool(7)) {
                    Vector2 rim = Projectile.Center + Main.rand.NextVector2CircularEdge(Radius * Density, Radius * Density * 0.9f);
                    PRTLoader.NewParticle<PRT_CultistShard>(rim, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f),
                        CultistPalette.IceBright, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
                }
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.IceMain.ToVector3() * (0.4f * Density));
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D fog = CWRAsset.Fog.Value;
            float density = Density;

            //雾体走 AlphaBlend 染色（Fog 白RGB+真alpha），四层镜像错相（外暗内亮的分层流动）
            for (int i = 0; i < 4; i++) {
                float phase = Main.GlobalTimeWrappedHourly * (0.4f + i * 0.12f) + i * 2.1f + Projectile.whoAmI * 0.7f;
                Vector2 off = new((float)Math.Sin(phase) * (30f - i * 5f), (float)Math.Cos(phase * 0.8f) * (20f - i * 3f));
                SpriteEffects fx = (i + Projectile.whoAmI) % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                Color tint = Color.Lerp(CultistPalette.IceDeep, CultistPalette.IceMain, i / 3f) * (0.2f * density);
                sb.Draw(fog, drawPos + off, null, tint, phase * (i % 2 == 0 ? 0.14f : -0.11f), fog.Size() / 2f,
                    (Radius / 110f) * (1.15f - i * 0.18f), fx, 0f);
            }

            //雾核基底：原版信徒冰雾464真实贴图，五团错相环游（雾里真正的"雾"）
            Main.instance.LoadProjectile(ProjectileID.CultistBossIceMist);
            Texture2D mist = TextureAssets.Projectile[ProjectileID.CultistBossIceMist].Value;
            for (int i = 0; i < 5; i++) {
                float phase = Main.GlobalTimeWrappedHourly * (0.22f + i * 0.05f) + i * 1.256f + Projectile.whoAmI;
                Vector2 off = new((float)Math.Sin(phase) * Radius * 0.5f, (float)Math.Cos(phase * 1.3f) * Radius * 0.34f);
                float wobble = 1f + 0.14f * (float)Math.Sin(phase * 2.2f);
                sb.Draw(mist, drawPos + off, null, new Color(255, 255, 255, 255) * (0.5f * density),
                    phase * 0.4f, mist.Size() / 2f, 1.15f * wobble, SpriteEffects.None, 0f);
            }

            //雾心悬晶硬闪：确定性hash频闪的星点（雾里有东西在结晶）
            CultistRenderHelper.BeginAdditive(sb);
            Texture2D star = CWRAsset.StarGlow01.Value;
            for (int i = 0; i < 5; i++) {
                float seedI = Projectile.whoAmI * 3.7f + i * 17.31f;
                //悬晶慢漂位置（确定性，各端一致）
                float px = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.3f + seedI) * Radius * 0.55f;
                float py = (float)Math.Cos(Main.GlobalTimeWrappedHourly * 0.23f + seedI * 1.7f) * Radius * 0.4f;
                //2~3帧硬频闪
                float blink = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f + seedI * 2.3f);
                if (blink < 0.55f) {
                    continue;
                }
                float lum = (blink - 0.55f) / 0.45f;
                sb.Draw(star, drawPos + new Vector2(px, py), null, CultistPalette.IceBright * (0.7f * lum * density),
                    seedI, star.Size() / 2f, new Vector2(0.34f, 0.1f) * (0.6f + lum * 0.5f), SpriteEffects.None, 0f);
                sb.Draw(star, drawPos + new Vector2(px, py), null, CultistPalette.IceBright * (0.5f * lum * density),
                    seedI + MathHelper.PiOver2, star.Size() / 2f, new Vector2(0.26f, 0.08f) * (0.6f + lum * 0.5f), SpriteEffects.None, 0f);
            }
            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
