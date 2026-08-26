using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.SlimeKin.Projectiles
{
    /// <summary>
    /// 死亡分裂的弹跳凝胶：自爆弹幕（非 NPC）。落地弹跳滚动，前 40 帧完全无害（死亡机制预告），
    /// 武装后脉动加速倒数，引信走完在小半径内爆裂——判定窗与可见溅胶窗完全一致。
    /// ai[0]=风味，ai[1]=凝胶色，ai[2]=体型；弹跳物理各端按同步地形确定性模拟
    /// </summary>
    internal class SlimeGelBomb : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>武装前无害帧（死亡分裂预告，公平契约 ≥30）</summary>
        private const int ArmFrames = 40;
        /// <summary>生成到起爆的总引信帧</summary>
        private const int FuseFrames = 120;
        /// <summary>爆裂判定窗帧数</summary>
        private const int BurstFrames = 8;
        /// <summary>爆裂半径（具名逃生阀门：武装期步行即可脱离）</summary>
        private const float BlastRadius = 66f;
        private const float Gravity = 0.28f;
        private const float MaxFall = 12f;
        private const float BounceDampY = 0.62f;
        private const float BounceDampX = 0.8f;

        private GooFlavor Flavor => (GooFlavor)(int)Projectile.ai[0];
        private Color Gel => SlimeKinFlavor.UnpackColor(Projectile.ai[1]);
        private float Scale => Projectile.ai[2] <= 0f ? 1f : Projectile.ai[2];

        private ref float Age => ref Projectile.localAI[0];

        private bool Armed => Age >= ArmFrames;
        private bool Bursting => Age >= FuseFrames;
        /// <summary>爆裂扩张进度 0→1（同时驱动判定半径与溅胶可视）</summary>
        private float BurstProgress => MathHelper.Clamp((Age - FuseFrames) / BurstFrames, 0f, 1f);

        /// <summary>落地压扁的回弹动画量，纯本地表现</summary>
        private float bounceSquash;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FuseFrames + BurstFrames + 20;
            Projectile.netImportant = true;
        }

        /// <summary>只有爆裂窗有伤害；触碰弹跳中的凝胶无事</summary>
        public override bool? CanDamage() => Bursting && BurstProgress < 1f ? null : false;

        public override void AI() {
            Age++;

            if (Age == 1f && !VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.t_Slime,
                        Main.rand.NextVector2Circular(2f, 2f), 120, Gel, 1.1f * Scale);
                    dust.noGravity = true;
                }
            }

            if (Bursting) {
                Projectile.hostile = BurstProgress < 1f;
                Projectile.velocity = Vector2.Zero;
                if (Age - FuseFrames == 1f && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.3f, Volume = 0.7f, MaxInstances = 5 }, Projectile.Center);
                }
                //爆裂窗溅胶（每帧 ≤6 粒）
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 5; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.t_Slime,
                            Main.rand.NextVector2CircularEdge(4f, 4f) * BurstProgress, 100, Gel, 1.4f * Scale);
                        dust.noGravity = true;
                    }
                }
                if (Age >= FuseFrames + BurstFrames + 4f) {
                    Projectile.Kill();
                }
                return;
            }

            //弹跳物理
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFall) {
                Projectile.velocity.Y = MaxFall;
            }
            if (Projectile.velocity.Y == 0f) {
                //贴地滚动摩擦
                Projectile.velocity.X *= 0.92f;
                Projectile.rotation += Projectile.velocity.X * 0.06f;
            }
            else {
                Projectile.rotation += 0.08f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);
            }
            bounceSquash *= 0.86f;

            //武装脉动渗胶
            if (!VaultUtils.isServer && Armed && Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f) * Scale,
                    DustID.t_Slime, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f), 130, Gel, 0.9f);
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, Gel.ToVector3() * (Armed ? 0.3f : 0.12f));
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //弹跳衰减，不销毁
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * BounceDampX;
            }
            if (Projectile.velocity.Y != oldVelocity.Y && oldVelocity.Y > 1f) {
                Projectile.velocity.Y = -oldVelocity.Y * BounceDampY;
                bounceSquash = 1f;
                if (!VaultUtils.isServer && oldVelocity.Y > 2.5f) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.2f, Volume = 0.35f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            return false;
        }

        /// <summary>爆裂判定：以中心为圆心的精确圆距测试，半径随可视溅胶同步扩张</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Bursting) {
                return false;
            }
            float radius = BlastRadius * Scale * MathHelper.Lerp(0.4f, 1f, BurstProgress);
            Vector2 center = Projectile.Center;
            Vector2 closest = new Vector2(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(center, closest) <= radius * radius;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            (int buffId, int frames) = SlimeKinFlavor.BurstDebuff(Flavor);
            target.AddBuff(buffId, frames);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.t_Slime,
                    Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY, 110, Gel, 1.2f * Scale);
                dust.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blob = TextureAssets.Projectile[Type].Value;
            Vector2 blobOrigin = blob.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color gel = Gel;

            //爆裂窗：可见溅胶环 = 判定圆（同一半径公式）
            if (Bursting) {
                float radius = BlastRadius * Scale * MathHelper.Lerp(0.4f, 1f, BurstProgress);
                float ringAlpha = 1f - BurstProgress * 0.6f;
                float ringScale = radius * 2f / blob.Width;
                Main.EntitySpriteDraw(blob, pos, null, gel * (0.5f * ringAlpha), 0f, blobOrigin,
                    ringScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(blob, pos, null, (Color.Lerp(gel, Color.White, 0.5f) with { A = 0 }) * (0.55f * ringAlpha),
                    0f, blobOrigin, ringScale * 0.7f, SpriteEffects.None, 0);
                return false;
            }

            //武装后脉动加速 = 可读倒数
            float pulse = 1f;
            if (Armed) {
                float fuseLeft = 1f - (Age - ArmFrames) / (FuseFrames - ArmFrames);
                pulse = 1f + 0.14f * MathF.Sin(Age * MathHelper.Lerp(0.55f, 0.16f, fuseLeft));
            }
            float dim = Armed ? 1f : 0.62f;
            float squash = bounceSquash * 0.3f;
            Vector2 scale = new Vector2(0.34f * (1f + squash), 0.34f * (1f - squash)) * Scale * pulse;

            //旧位残迹（同材质拖尾）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 ghost = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(blob, ghost, null, gel * (0.3f * t * dim), Projectile.rotation,
                    blobOrigin, scale * (0.55f + 0.3f * t), SpriteEffects.None, 0);
            }

            //本体：真 alpha 暗胶层 + 原版凝胶贴图细节 + 加色高光
            Main.EntitySpriteDraw(blob, pos, null, gel * (0.88f * dim), Projectile.rotation,
                blobOrigin, scale * 1.12f, SpriteEffects.None, 0);
            Main.instance.LoadItem(ItemID.Gel);
            Texture2D gelTex = TextureAssets.Item[ItemID.Gel].Value;
            Main.EntitySpriteDraw(gelTex, pos, null, Color.Lerp(gel, Color.White, 0.3f) * (0.85f * dim),
                Projectile.rotation, gelTex.Size() * 0.5f, 0.9f * Scale * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(blob, pos - new Vector2(0f, 3f), null,
                (Color.Lerp(gel, Color.White, 0.55f) with { A = 0 }) * (0.35f * dim * (Armed ? pulse : 1f)),
                Projectile.rotation, blobOrigin, scale * 0.4f, SpriteEffects.None, 0);
            return false;
        }
    }
}
