using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostLegion.Projectiles
{
    /// <summary>
    /// 落点迸裂小雪片：方向由落点环按固定角度表给出（非追踪保证），本体只走重力弧线。
    /// 原版雪球贴图小型化实体层 + 同材质拖尾；出膛淡入期无判定（公平阀），触地即碎
    /// </summary>
    internal class FlgSnowShardProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SnowBallHostile;

        /// <summary>每帧重力</summary>
        private const float Gravity = 0.24f;
        private const float MaxFallSpeed = 11f;
        /// <summary>出膛淡入帧：判定随可见度同门开启（公平阀）</summary>
        private const int FadeInFrames = 4;
        /// <summary>雪片绘制比例</summary>
        private const float ShardScale = 0.62f;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.coldDamage = true;
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(190f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            Projectile.rotation += Projectile.velocity.X * 0.06f;

            if (!Main.dedServ && Main.rand.NextBool(7)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    -Projectile.velocity * 0.08f, 150, default, 0.7f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.25f, Pitch = 0.35f, MaxInstances = 6 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    Main.rand.NextVector2Circular(1.8f, 1.8f), 110, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SnowBallHostile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SnowBallHostile].Value;
            int frames = Main.projFrames[ProjectileID.SnowBallHostile] > 0 ? Main.projFrames[ProjectileID.SnowBallHostile] : 1;
            Rectangle rect = tex.Frame(1, frames, 0, 0);
            Vector2 orig = rect.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;
            Color body = Color.Lerp(lightColor, new Color(228, 242, 255), 0.55f) * opacity;

            //同材质拖尾（横轴 ≥0.5×弹体）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, rect, body * (0.36f * t), Projectile.rotation - i * 0.06f,
                    orig, ShardScale * (0.55f + 0.3f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, rect, body,
                Projectile.rotation, orig, ShardScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
