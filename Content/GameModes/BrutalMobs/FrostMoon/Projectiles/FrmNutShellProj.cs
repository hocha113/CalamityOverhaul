using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 硬壳跳弹（胡桃夹子弹跳弹道）：沿已锁定的瞄准线发出，触物块反弹最多
    /// <see cref="MaxBounces"/> 次后碎裂。微重力弧线，弹速中庸全程可见可躲
    /// </summary>
    internal class FrmNutShellProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SnowBallHostile;

        /// <summary>最大反弹次数（反弹循环读取）</summary>
        internal const int MaxBounces = 3;
        /// <summary>每帧微重力（弹跳弹道的弧感）</summary>
        private const float Gravity = 0.1f;
        /// <summary>反弹速度保留系数</summary>
        private const float BounceKeep = 0.9f;

        private int Bounces {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > 15f) {
                Projectile.velocity.Y = 15f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.06f;

            if (!Main.dedServ && Main.rand.NextBool(7)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    -Projectile.velocity * 0.08f, 130, default, 0.8f);
                dust.noGravity = true;
            }
        }

        /// <summary>物块反弹：逐轴反射，超出上限即碎裂（物块碰撞各端确定性一致）</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Bounces >= MaxBounces) {
                return true;
            }
            Bounces++;
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * BounceKeep;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * BounceKeep;
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 6 }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                        Main.rand.NextVector2Circular(2f, 2f), 110, default, 0.9f);
                    dust.noGravity = true;
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = -0.1f, MaxInstances = 6 }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SnowBallHostile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SnowBallHostile].Value;
            int frames = Main.projFrames[ProjectileID.SnowBallHostile] > 0 ? Main.projFrames[ProjectileID.SnowBallHostile] : 1;
            Rectangle rect = tex.Frame(1, frames, 0, 0);
            Vector2 orig = rect.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //同材质拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, rect, new Color(200, 176, 130) * (0.35f * t),
                    Projectile.rotation - i * 0.06f, orig, 0.85f * t + 0.15f, SpriteEffects.None, 0);
            }

            //壳体：原版贴图实体层，硬壳染色
            Color body = Color.Lerp(lightColor, new Color(196, 148, 84), 0.45f);
            Main.EntitySpriteDraw(tex, pos, rect, body, Projectile.rotation, orig, 1.05f, SpriteEffects.None, 0);
            float glint = 0.55f + 0.45f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, pos, null, new Color(255, 214, 140, 0) * (0.25f * glint), 0f,
                glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            return false;
        }
    }
}
