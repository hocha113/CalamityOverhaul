using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 压缩水弹:尾扇齐射用。原版水矢贴图作本体(有遮挡剪影),
    /// 拖尾=暗流管条带(TechCurrent,同材质、横径≥本体一半)+同素材递缩鬼影;
    /// 直飞段复利续压+轻微鱼摆尾(不匀速),后段带下坠成弧。
    /// 出膛水花、命中溅射、死后水滴余痕补齐四相
    /// </summary>
    internal class SeaShrimpWaterBolt : SeaShrimpModProjectile, IPrimitiveDrawable
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WaterBolt}";

        private const int TrailLen = 10;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            //出膛水花由发射状态在炮口统一给(齐射一轮一蓬,不逐弹重复)

            if (Projectile.timeLeft > 218) {
                //直飞段:复利续压 + 轻微鱼摆尾,消灭匀速直线
                if (Projectile.velocity.Length() < 15.5f) {
                    Projectile.velocity *= 1.008f;
                }
                Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                Projectile.velocity += perp
                    * (MathF.Sin(Projectile.localAI[0] * 0.37f + Projectile.identity) * 0.09f);
            }
            else {
                //后段轻微下坠成弧
                Projectile.velocity.Y += 0.09f;
                if (Projectile.velocity.Y > 14f) {
                    Projectile.velocity.Y = 14f;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.06f, 0.16f, 0.32f);

            if (!Main.dedServ) {
                if (Main.GameUpdateCount % 2 == 0) {
                    PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center,
                        -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                        Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.2f, 0.34f))?.Configure(10, 1.5f);
                }
                if (Main.rand.NextBool(9)) {
                    EverdeepVFX.ShedDroplet(Projectile.Center,
                        -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f), 0.8f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //命中溅射:水珠抛物线+泡沫+水压环,飞沫在弹体死后继续存在
            EverdeepVFX.SplashBurst(Projectile.Center, Projectile.velocity, 1f);
            //尾迹化滴:条带不许在死亡帧整段蒸发,沿旧位散成水团缓沉
            for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.oldPos[i] + Projectile.Size * 0.5f,
                    Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(10, 18), 1.2f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 3f) - Projectile.velocity * 0.1f,
                    SeaShrimpVFX.Glow, Main.rand.NextFloat(0.6f, 0.9f))?.Configure(10);
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            //暗流管拖尾:头宽 10px 半宽(全宽 20 ≥ 本体 18 的一半,契约5),同材质暗水
            Vector2[] path = new Vector2[TrailLen];
            int count = 0;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                path[count++] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            if (count < 2) {
                return;
            }
            path[count - 1] = Projectile.Center;
            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0.15f, 1f);
            AbyssrendFX.DrawPathStrip(path, count, i => {
                float t = i / (float)Math.Max(count - 1, 1);
                return MathHelper.Lerp(4.5f, 10f, t) * lifeFade;
            }, lifeFade);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            //鬼影:同素材递缩重绘(0.55×/0.35α 量级,契约5),色走深渊家族
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                float t = i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color col = Color.Lerp(SeaShrimpVFX.Body, SeaShrimpVFX.Glow, 1f - t) * (0.4f * (1f - t));
                Main.spriteBatch.Draw(tex, pos, null, col, Projectile.oldRot[i],
                    origin, MathHelper.Lerp(0.9f, 0.5f, t), SpriteEffects.None, 0f);
            }

            //本体:光照色主体 + 白亮芯
            Vector2 center = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, center, null, lightColor, Projectile.rotation,
                origin, 1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, center, null,
                new Color(200, 235, 255, 90) * 0.7f, Projectile.rotation,
                origin, 0.62f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
