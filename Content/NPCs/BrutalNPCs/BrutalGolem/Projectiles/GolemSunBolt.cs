using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>太阳能量弹：直线快弹，速度拉伸体 + 三层拖尾</summary>
    internal class GolemSunBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            //出膛闪光：首帧各端本地（OnSpawn 不在远端客户端执行）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = -0.3f, Volume = 0.6f }, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                            Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1.5f, 1.5f), 0, default, 1.3f);
                        dust.noGravity = true;
                    }
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            //飞行中缓慢增速，不是匀速贴图平移
            if (Projectile.velocity.Length() < 22f) {
                Projectile.velocity *= 1.012f;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.5f, 0.16f));

            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                    -Projectile.velocity * 0.06f, 0, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f, Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                    Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.4f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(0.5f + speed * 0.045f, 0.6f, 1.7f);

            //拖尾三层：暗橙外→饱和中→白热芯
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(streak, oldPos, null, new Color(190, 80, 20, 0) * (0.3f * fade),
                    Projectile.rotation, new Vector2(streak.Width * 0.8f, streak.Height / 2f),
                    new Vector2(0.3f * stretch * fade, 0.1f), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(streak, drawPos, null, new Color(255, 170, 60, 0) * 0.95f,
                Projectile.rotation, new Vector2(streak.Width * 0.8f, streak.Height / 2f),
                new Vector2(0.42f * stretch, 0.13f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(streak, drawPos, null, new Color(255, 240, 190, 0),
                Projectile.rotation, new Vector2(streak.Width * 0.8f, streak.Height / 2f),
                new Vector2(0.3f * stretch, 0.07f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 190, 90, 0) * 0.8f,
                0f, glow.Size() / 2f, 0.38f, SpriteEffects.None, 0);
            return false;
        }
    }
}
