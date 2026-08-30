using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaSeaShrimp
{
    /// <summary>
    /// 鬼奴海虾的血水弹：尾扇齐射用。原版水矢贴图作本体、血水衣着色，
    /// 拖尾为同素材递缩鬼影；直飞段复利续压 + 轻微鱼摆尾（不匀速），
    /// 后段带下坠成弧。沿途掉血滴，命中溅血花
    /// </summary>
    internal class KikasaShrimpWaterBolt : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WaterBolt}";

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 220;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.timeLeft > 198) {
                //直飞段：复利续压 + 轻微鱼摆尾，消灭匀速直线
                if (Projectile.velocity.Length() < 16f) {
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
            Lighting.AddLight(Projectile.Center, 0.2f, 0.06f, 0.09f);

            //沿途掉血滴
            if (!Main.dedServ && Main.GameUpdateCount % 2 == 0) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(Projectile.Center,
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Color.Lerp(BloodDeep, BloodMain, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(10, 1.3f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //命中溅血：血珠抛物线，尾迹化滴不许整段蒸发
            Vector2 normal = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    normal.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.32f, 0.55f))?.Configure(Main.rand.Next(14, 26));
            }
            for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(Projectile.oldPos[i] + Projectile.Size * 0.5f,
                    Main.rand.NextVector2Circular(0.8f, 0.8f),
                    BloodDeep * 0.6f, Main.rand.NextFloat(0.2f, 0.36f))?.Configure(Main.rand.Next(10, 16), 1.1f);
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = 0.15f, MaxInstances = 3 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            //鬼影：同素材递缩重绘，色走血水家族
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color col = Color.Lerp(BloodMain, BloodBright, 1f - t) * (0.38f * (1f - t));
                Main.spriteBatch.Draw(tex, pos, null, col, Projectile.oldRot[i],
                    origin, MathHelper.Lerp(0.9f, 0.5f, t), SpriteEffects.None, 0f);
            }

            //本体：血染主体 + 亮芯
            Vector2 center = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, center, null,
                Color.Lerp(lightColor, BloodMain, 0.6f), Projectile.rotation,
                origin, 1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, center, null,
                (BloodBright with { A = 60 }) * 0.7f, Projectile.rotation,
                origin, 0.62f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
