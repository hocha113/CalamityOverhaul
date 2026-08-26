using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles
{
    /// <summary>
    /// 邪液溅矛:死亡定向溅射的实弹。淡入完成才有杀伤(伤害窗口=可见窗口),
    /// 飞行后段吃重力坠成溅洒弧线。ai[0]=风味 ai[2]=出生档位
    /// </summary>
    internal class VileLanceProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>淡入帧数,淡入期无判定(公平阀)</summary>
        private const int FadeInFrames = 12;
        /// <summary>重力介入延迟与每帧坠速</summary>
        private const int GravityDelayFrames = 16;
        private const float GravityPerFrame = 0.13f;
        private const float MaxFallSpeed = 11f;

        private int Flavor => (int)Projectile.ai[0];
        private int Tier => System.Math.Clamp((int)Projectile.ai[2], 1, 3);
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 190;
            Projectile.alpha = 255;
        }

        /// <summary>淡入完成才有杀伤</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;

            //出膛淡入(可见度与判定同一时间轴)
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / (float)FadeInFrames, 0f, 1f));

            //后段吃重力,溅洒弧线
            if (Age > GravityDelayFrames) {
                Projectile.velocity.Y += GravityPerFrame;
                if (Projectile.velocity.Y > MaxFallSpeed) {
                    Projectile.velocity.Y = MaxFallSpeed;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, EvilBiomeFX.DustFor(Flavor),
                    -Projectile.velocity * 0.15f, 140, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, EvilBiomeFX.Bright(Flavor).ToVector3() * 0.25f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //风味减益,时长随档位(6/8/10 秒)
            target.AddBuff(EvilBiomeFX.BuffFor(Flavor), (4 + 2 * Tier) * 60);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, EvilBiomeFX.DustFor(Flavor),
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4f),
                    120, default, 1.1f);
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.35f, Pitch = -0.25f, MaxInstances = 4 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float opacity = 1f - Projectile.alpha / 255f;

            //旧位残迹(同材质拖尾,横轴 ≥0.5 倍体宽)
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                DrawGlob(tex, origin, oldDrawPos, t * 0.35f * opacity, 0.55f * t);
            }
            DrawGlob(tex, origin, pos, opacity, 1f);
            return false;
        }

        private void DrawGlob(Texture2D tex, Vector2 origin, Vector2 drawPos, float alpha, float scaleMul) {
            //快成线、慢成珠的液体拉伸
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0f, 1f);
            Vector2 scale = new Vector2(0.34f * (1f - stretch * 0.4f), 0.48f * (1f + stretch * 1.6f)) * scaleMul;
            Color dark = EvilBiomeFX.Deep(Flavor) * (0.92f * alpha);
            Color core = EvilBiomeFX.Bright(Flavor) with { A = 0 } * (0.85f * alpha);
            Main.EntitySpriteDraw(tex, drawPos, null, dark, Projectile.rotation, origin, scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation, origin, scale * 0.78f, SpriteEffects.None, 0);
        }
    }
}
