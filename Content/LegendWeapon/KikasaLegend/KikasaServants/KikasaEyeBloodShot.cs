using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>
    /// 鬼奴克眼的血液炮弹：出膛平直，半秒后被重量拽出弧线（禁匀速直飞），
    /// 弹体速度拉伸成泪滴、沿途洒落血珠；命中爆血花，落空坠回血湖时
    /// 以一圈涟漪收尾——湖把自己的血收回去
    /// </summary>
    internal class KikasaEyeBloodShot : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>出膛后多少帧开始吃重力</summary>
        private const int GravityDelay = 16;

        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            //半拍后弧线下坠
            if (Projectile.timeLeft < 300 - GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.14f, 17f);
                Projectile.velocity.X *= 0.999f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //沿途甩落血珠
            if (!Main.dedServ && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodTint * 0.45f, Main.rand.NextFloat(0.25f, 0.45f))
                    ?.Configure(Main.rand.Next(8, 16), 0f);
            }

            Lighting.AddLight(Projectile.Center, 0.22f, 0.05f, 0.05f);

            //落空坠回血湖：湖收回自己的血
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.7f);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            //命中/触地/落湖共用的爆花收尾；OnKill 各端都跑，队友也看得见
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.35f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                float angle = -MathHelper.Pi * (0.1f + 0.8f * i / 7f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(1.5f, 3.6f)
                        + Projectile.velocity * 0.12f,
                    BloodTint * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 22), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.2f), MistBlood * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))
                ?.Configure(Main.rand.Next(30, 50));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Projectile.type]?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;

            //拖尾：旧位残珠渐细渐淡——血在空中留了一条线
            for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                if (oldCenter == Projectile.Size * 0.5f) {
                    continue;
                }
                float fall = 1f - k / (float)Projectile.oldPos.Length;
                Vector2 scale = new Vector2(0.16f, 0.34f) * fall;
                Main.spriteBatch.Draw(tex, oldCenter - Main.screenPosition, null,
                    BloodTint * (0.4f * fall), Projectile.oldRot[k], origin, scale, SpriteEffects.None, 0f);
            }

            //弹体：快成丝、慢成珠（与鬼雨滴同语法）
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0f, 1f);
            Vector2 body = new(0.24f * (1f - stretch * 0.3f), 0.5f * (1f + stretch * 1.8f));
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, pos, null, BloodTint * 0.9f,
                Projectile.rotation, origin, body, SpriteEffects.None, 0f);
            //高光芯
            Main.spriteBatch.Draw(tex, pos, null, FoamGlow * 0.5f,
                Projectile.rotation, origin, body * new Vector2(0.4f, 0.9f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
