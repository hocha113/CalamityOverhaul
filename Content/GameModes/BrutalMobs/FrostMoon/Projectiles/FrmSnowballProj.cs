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
    /// 僵尸精灵齐投雪球：ai[0]=风味（0 标准/1 重雪球/2 快小球）。
    /// 抛物线全程可见，出膛淡入期无判定（公平阀）；重雪球命中挂短暂寒颤（受击方本机结算）。
    /// 原版雪球贴图实体层 + 同材质拖尾；风味几何（重力/体积）由两端从同步的 ai 值确定性展开
    /// </summary>
    internal class FrmSnowballProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SnowBallHostile;

        internal const int FlavorStandard = 0;
        internal const int FlavorHeavy = 1;
        internal const int FlavorSmall = 2;

        /// <summary>淡入帧数，判定开启与可见同门</summary>
        private const int FadeInFrames = 6;
        /// <summary>重雪球命中的寒颤时长（短暂，受击方本机 AddBuff）</summary>
        private const int HeavyChillTicks = 75;
        /// <summary>下坠终端速度</summary>
        private const float MaxFallSpeed = 14f;

        private int Flavor => Math.Clamp((int)Projectile.ai[0], 0, 2);
        private ref float Age => ref Projectile.localAI[0];

        /// <summary>风味重力（NPC 侧弹道解算与本体 AI 共用，两端一致）</summary>
        internal static float GravityFor(int flavor) => flavor switch {
            FlavorHeavy => 0.13f,
            FlavorSmall => 0.26f,
            _ => 0.2f,
        };

        /// <summary>风味体积比例（贴图与碰撞箱同步缩放）</summary>
        private static float ScaleFor(int flavor) => flavor switch {
            FlavorHeavy => 1.5f,
            FlavorSmall => 0.75f,
            _ => 1f,
        };

        /// <summary>风味主色（标准/重压灰蓝/亮快雪）</summary>
        private static readonly Color[] FlavorTints = [
            new Color(208, 224, 240),
            new Color(178, 200, 226),
            new Color(228, 242, 255),
        ];

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
            Projectile.timeLeft = 180;
            Projectile.coldDamage = true;
        }

        public override void AI() {
            if (Age == 0f) {
                //首帧按同步的风味值定形（各端从同一 ai 确定性 Resize，不依赖追加同步包）
                int size = Flavor switch { FlavorHeavy => 20, FlavorSmall => 10, _ => 14 };
                Projectile.Resize(size, size);
                Projectile.scale = ScaleFor(Flavor);
            }
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            Projectile.velocity.Y += GravityFor(Flavor);
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            //雪球翻滚
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            //沿途雪屑（低频）
            if (!Main.dedServ && Main.rand.NextBool(Flavor == FlavorHeavy ? 4 : 7)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    -Projectile.velocity * 0.08f, 140, default, Flavor == FlavorHeavy ? 1.1f : 0.8f);
                dust.noGravity = true;
            }
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > 5 ? null : false;

        /// <summary>重雪球风味差异：命中挂短暂寒颤（受击方本机结算，减益原生同步）</summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Flavor == FlavorHeavy) {
                target.AddBuff(BuffID.Chilled, HeavyChillTicks);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SnowBallHostile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SnowBallHostile].Value;
            int frames = Main.projFrames[ProjectileID.SnowBallHostile] > 0 ? Main.projFrames[ProjectileID.SnowBallHostile] : 1;
            Rectangle rect = tex.Frame(1, frames, 0, 0);
            Vector2 orig = rect.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;
            Color body = Color.Lerp(lightColor, FlavorTints[Flavor], 0.55f) * opacity;

            //同材质拖尾（横轴粗细 ≥ 弹体一半）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, drawPos, rect, body * (0.38f * t), Projectile.rotation - i * 0.05f,
                    orig, Projectile.scale * (0.55f + 0.3f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, rect, body,
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.32f, Pitch = 0.25f, MaxInstances = 6 }, Projectile.Center);
            int burst = Flavor == FlavorHeavy ? 8 : 5;
            for (int i = 0; i < burst; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    Main.rand.NextVector2Circular(2.4f, 2.4f) * Projectile.scale, 100, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = Main.rand.NextBool();
            }
        }
    }
}
