using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell.Projectiles
{
    /// <summary>
    /// 小鬼焰弹：开窗结束后沿锁定角发射的火束弹体，穿墙（小鬼火球的本行）。ai[0]=档位。<br/>
    /// 淡入完成才有杀伤
    /// </summary>
    internal class JhImpFireBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        private const int FadeInFrames = 8;
        private const int BurnTicksBase = 90;
        private const int BurnTicksPerTier = 30;

        private int Tier => (int)Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));
            Projectile.rotation += 0.21f;

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.15f, 130, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.42f, 0.2f, 0.06f);
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, BurnTicksBase + BurnTicksPerTier * Tier);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 90, default, Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质火尾
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, new Color(255, 140, 50, 60) * (0.4f * fade * opacity),
                    Projectile.rotation - i * 0.21f, origin, Projectile.scale * (0.65f + 0.35f * fade), SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, Color.White * opacity,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(255, 170, 70, 0) * (0.5f * opacity),
                Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0);
            return false;
        }
    }
}
