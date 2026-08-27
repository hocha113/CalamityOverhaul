using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles
{
    /// <summary>
    /// 孢弹（发光蘑菇系通用弹体）。ai[0]=每帧重力 ai[1]=体型（0标准/1迷你）。
    /// 出膛淡入且淡入期无判定（公平阀）；暗青绿真 alpha 外壳+亮蓝加色芯双层实体
    /// （Extra_98 配方，镜像 VileLanceProj.DrawGlob），同材质拖尾横轴 ≥0.5 倍体宽
    /// </summary>
    internal class MushroomSporeBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>淡入帧数，可见度与判定同一时间轴</summary>
        private const int FadeInFrames = 10;
        private const float MaxFallSpeed = 9f;
        /// <summary>迷你体（孢囊破裂产物）的存续帧</summary>
        private const int MiniLifeFrames = 90;

        //==== 孢弹风味口径（孢雾喷吐与死亡孢爆共用；困难孢子系三型差异的唯一权威表） ====
        internal const int FlavorLadybug = 0;
        internal const int FlavorZombie = 1;
        internal const int FlavorBat = 2;
        internal const int FlavorSkeleton = 3;

        /// <summary>风味→(弹速, 每帧重力)：Zombie 带重力抛物 / Bat 更快无重力 / Skeleton 中速无重力（其差异在弹数与缺口）</summary>
        internal static (float Speed, float Gravity) FlavorShot(int flavor) => flavor switch {
            FlavorZombie => (5.0f, 0.09f),
            FlavorBat => (7.4f, 0f),
            FlavorSkeleton => (5.6f, 0f),
            _ => (4.4f, 0.02f),//瓢虫慢速孢弹
        };

        /// <summary>暗青绿外壳色（真 alpha，压得住亮背景）</summary>
        internal static readonly Color SporeDeep = new(16, 62, 54);
        /// <summary>亮蓝芯色（发光蘑菇青蓝，加色层用）</summary>
        internal static readonly Color SporeBright = new(96, 208, 255);

        private ref float Gravity => ref Projectile.ai[0];
        private bool Mini => Projectile.ai[1] == 1f;
        private ref float Age => ref Projectile.localAI[0];

        /// <summary>双层孢珠画笔（暗壳 ×1.18 全 alpha + 亮芯 A=0），弹体/虚影/鞭节/孢囊共用</summary>
        internal static void DrawGlobAt(Vector2 screenPos, float rotation, float alpha, Vector2 scale) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Main.EntitySpriteDraw(tex, screenPos, null, SporeDeep * (0.92f * alpha),
                rotation, origin, scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, screenPos, null, (SporeBright with { A = 0 }) * (0.85f * alpha),
                rotation, origin, scale * 0.78f, SpriteEffects.None, 0);
        }

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
            Projectile.penetrate = -1;
            Projectile.timeLeft = 220;
            Projectile.alpha = 255;
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            if (Age == 1f && Mini) {
                //体型由 ai[1] 决定，各端从同一生成包确定性收缩，无权威端事后改包问题
                Projectile.Resize(9, 9);
                Projectile.timeLeft = MiniLifeFrames;
            }
            Projectile.alpha = (int)MathHelper.Lerp(230f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            //无重力孢子的浮游漂摆（identity+龄期播种，各端确定性一致）
            if (Gravity == 0f) {
                Projectile.velocity = Projectile.velocity.RotatedBy(
                    MathF.Sin((Age + Projectile.identity * 13f) * 0.09f) * 0.012f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(7)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GlowingMushroom,
                    -Projectile.velocity * 0.1f, 150, default, 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, SporeBright.ToVector3() * (Mini ? 0.1f : 0.18f));
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = 1f - Projectile.alpha / 255f;
            float miniMul = Mini ? 0.62f : 1f;
            //快成线、慢成珠的孢囊拉伸
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.06f, 0f, 1f);
            Vector2 baseScale = new Vector2(0.30f * (1f - stretch * 0.35f), 0.40f * (1f + stretch * 1.4f)) * miniMul;

            //旧位残迹（同材质拖尾，横轴 ≥0.5 倍体宽）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                DrawGlobAt(oldDrawPos, Projectile.rotation, t * 0.35f * opacity, baseScale * (0.55f * t + 0.25f));
            }
            DrawGlobAt(Projectile.Center - Main.screenPosition, Projectile.rotation, opacity, baseScale);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GlowingMushroom,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.8f) * Main.rand.NextFloat(1f, 3f),
                    120, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.25f, Pitch = 0.35f, MaxInstances = 4 }, Projectile.Center);
        }
    }
}
