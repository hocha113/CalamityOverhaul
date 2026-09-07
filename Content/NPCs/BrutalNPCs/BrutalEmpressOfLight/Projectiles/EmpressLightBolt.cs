using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>光球行为</summary>
    internal enum EmpressBoltMode : int
    {
        /// <summary>直飞，轻微加速</summary>
        Straight = 0,
        /// <summary>沿初始朝向恒加速 0.5/f，出生带 2800px 预告线</summary>
        Accelerating = 1,
        /// <summary>30f 后追玩家（ai[1]=玩家索引），同类互斥 60px</summary>
        Chase = 2,
        /// <summary>速度每帧旋 ai[1] 弧度并被宿主微推离，寿命×2</summary>
        Spinning = 3,
        /// <summary>向宿主汇聚，正弦扭旋成漩涡臂，225px 内爆散</summary>
        Vortex = 4,
    }

    /// <summary>
    /// 聚集之光·光球：本体=原版 873 棱彩弹贴图（真 alpha 占遮挡），暗边+金晕叠层，拖尾同材质 0.55×；
    /// ai[0]=模式 ai[1]=模式参数 ai[2]=宿主 whoAmI。扫掠圆碰撞防高速穿人
    /// </summary>
    internal class EmpressLightBolt : ModProjectile, IEmpressAttack, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HallowBossRainbowStreak;

        public EmpressScorchTier ScorchTier => EmpressScorchTier.Medium;
        public float FeedbackIntensity => 0.8f;

        private const int MaxLife = 120;
        private const float HitRadius = 20f;

        private EmpressBoltMode Mode => (EmpressBoltMode)(int)Projectile.ai[0];
        private ref float Param => ref Projectile.ai[1];
        private NPC Host => ((int)Projectile.ai[2]).TryGetNPC(out NPC n) ? n : null;
        private ref float Timer => ref Projectile.localAI[0];
        private float Hue => (Projectile.identity * 0.137f + Projectile.ai[0] * 0.21f) % 1f;

        /// <summary>本帧追踪光球，PostUpdate 时互斥</summary>
        private static readonly List<Projectile> chasing = new();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.scale = 0.1f;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;

        public override void AI() {
            if (Timer == 0f) {
                Projectile.localAI[1] = Projectile.velocity.ToRotation();
                if (Mode == EmpressBoltMode.Spinning) {
                    Projectile.timeLeft = MaxLife * 2;
                }
            }
            Timer++;
            Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.2f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            switch (Mode) {
                case EmpressBoltMode.Straight:
                    Projectile.velocity *= 1.012f;
                    break;
                case EmpressBoltMode.Accelerating:
                    Projectile.velocity += Projectile.localAI[1].ToRotationVector2() * 0.5f;
                    if (!VaultUtils.isServer && Projectile.Distance(Main.LocalPlayer.Center) < 1800f) {
                        EmpressDayDrive.AddBlackout(0.0002f);
                    }
                    break;
                case EmpressBoltMode.Chase:
                    ChaseUpdate();
                    break;
                case EmpressBoltMode.Spinning:
                    Projectile.velocity = Projectile.velocity.RotatedBy(Param);
                    if (Host is NPC host) {
                        Projectile.velocity += Projectile.DirectionFrom(host.Center) * 0.025f;
                    }
                    break;
                case EmpressBoltMode.Vortex:
                    VortexUpdate();
                    break;
            }

            Color light = EmpressMotion.FormColor(Hue, DayBlend, 0.62f);
            Lighting.AddLight(Projectile.Center, light.ToVector3() * 0.6f * Projectile.scale);

            //飞行火花：速度越快撒得越多
            if (!VaultUtils.isServer && Main.rand.NextFloat() < 0.12f + Projectile.velocity.Length() * 0.006f) {
                EmpressMotion.SparkBurst(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY), 1, 0.5f, 2f, DayBlend, 0.9f);
            }
        }

        private void ChaseUpdate() {
            if (Timer < 30f) {
                return;
            }
            chasing.Add(Projectile);
            int idx = (int)Param;
            if (idx < 0 || idx >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[idx];
            if (!player.Alives()) {
                return;
            }
            bool phase3 = Host is NPC host && ((int)host.ai[3] & 4) != 0;
            float accel = phase3 ? 0.75f : 0.32f;
            Vector2 desired = Projectile.DirectionTo(player.Center) * 20f;
            if (phase3) {
                desired += player.velocity * 0.15f;
            }
            Vector2 diff = desired - Projectile.velocity;
            Projectile.velocity += diff.SafeNormalize(Vector2.Zero) * Math.Min(diff.Length(), accel);
        }

        private void VortexUpdate() {
            if (Host is not NPC host) {
                Projectile.velocity *= 0.96f;
                return;
            }
            float dist = Projectile.Distance(host.Center);
            Vector2 dir = Projectile.DirectionTo(host.Center).RotatedBy(0.5 * Math.Sin(Timer * 0.015));
            Projectile.velocity = Projectile.velocity * 0.98f + dir * Math.Min(0.2f + Timer / 3000f, 0.3f);
            if (dist > 225f) {
                Projectile.timeLeft = Math.Max(Projectile.timeLeft, 100);
                return;
            }
            //抵达宿主：向外甩出并爆散
            Projectile.velocity = Projectile.DirectionFrom(host.Center) * 40f;
            Projectile.Kill();
        }

        /// <summary>追踪光球互斥：60px 内按 (1-t²)×2 推开，防聚成一个点</summary>
        internal static void RepelChasers() {
            for (int i = 0; i < chasing.Count; i++) {
                for (int j = 0; j < chasing.Count; j++) {
                    if (i == j) {
                        continue;
                    }
                    Vector2 d = chasing[i].position - chasing[j].position;
                    float len = d.Length();
                    if (len > 1e-4f && len < 60f) {
                        float t = len / 60f;
                        chasing[i].velocity += d / len * (1f - t * t) * 2f;
                    }
                }
            }
            chasing.Clear();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = HitRadius * Projectile.scale;
            Vector2 from = Projectile.oldPosition + Projectile.Size / 2f;
            Vector2 to = Projectile.Center;
            float travel = Vector2.Distance(from, to);
            if (travel < 1f) {
                return Vector2.Distance(to, targetHitbox.ClosestPointInRect(to)) <= r;
            }
            //扫掠：沿位移步进，步长=到目标最近距离减半径
            Vector2 dir = (to - from) / travel;
            Vector2 p = from;
            float left = travel;
            for (int i = 0; i < 40 && left > 0f; i++) {
                float gap = Vector2.Distance(p, targetHitbox.ClosestPointInRect(p)) - r;
                if (gap < 1f) {
                    return true;
                }
                p += dir * gap;
                left -= gap;
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            int count = timeLeft > 4 ? 3 : 7;
            EmpressMotion.SparkBurst(Projectile.Center, Projectile.velocity.SafeNormalize(Vector2.UnitY), count, 1f, 5f, DayBlend, 1.2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D body = TextureAssets.Projectile[Type].Value;
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float day = DayBlend;
            Color main = EmpressMotion.FormColor(Hue, day, 0.64f);
            Color rim = Color.Lerp(new Color(60, 20, 90), EmpressMotion.SunDark, day);
            Vector2 origin = body.Size() / 2f;
            float scale = Projectile.scale;

            //拖尾：同材质本体按 0.55× 与 0.35α 重画（不是另一张更细的东西）
            Vector2[] old = Projectile.oldPos;
            for (int i = old.Length - 1; i >= 1; i--) {
                if (old[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)old.Length;
                Vector2 pos = old[i] + Projectile.Size / 2f - Main.screenPosition;
                float s = scale * MathHelper.Lerp(0.35f, 0.75f, t);
                Main.spriteBatch.Draw(star, pos, null, rim * (0.35f * t), Projectile.oldRot[i], star.Size() / 2f, s * 0.13f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(body, pos, null, main * (0.35f * t), Projectile.oldRot[i], origin, s, SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //底层金晕（加色，≤30% 视觉体量）
            Main.spriteBatch.Draw(glow, drawPos, null, (main with { A = 0 }) * 0.5f, 0f, glow.Size() / 2f, scale * 1.4f, SpriteEffects.None, 0f);
            //暗边（真 alpha，A=255，速度拉伸）
            float speed = Projectile.velocity.Length();
            Vector2 stretch = new(scale * 0.15f * (1f + speed * 0.02f), scale * 0.13f);
            Main.spriteBatch.Draw(star, drawPos, null, rim, Projectile.rotation - MathHelper.PiOver2, star.Size() / 2f, stretch, SpriteEffects.None, 0f);
            //本体：原版贴图，形态染色，饱和实体
            Main.spriteBatch.Draw(body, drawPos, null, main, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            //热芯：小、短命感靠闪烁
            float flicker = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f + Projectile.identity);
            Main.spriteBatch.Draw(body, drawPos, null, (Color.White with { A = 0 }) * (0.45f * flicker), Projectile.rotation, origin, scale * 0.55f, SpriteEffects.None, 0f);
            return false;
        }

        /// <summary>加速光球的预告线：出生 25f 渐显，60f 后随速度起来渐隐（线告诉你它会去哪）</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Mode != EmpressBoltMode.Accelerating || VaultUtils.isServer) {
                return;
            }
            float fadeIn = MathHelper.Clamp(Timer / 25f, 0f, 1f);
            float fadeOut = 1f - MathHelper.Clamp((Timer - 60f) / 40f, 0f, 1f);
            float opacity = fadeIn * fadeOut * 0.55f;
            if (opacity <= 0.01f) {
                return;
            }
            Vector2 dir = Projectile.localAI[1].ToRotationVector2();
            Color c = EmpressMotion.FormColor(Hue, DayBlend, 0.7f);
            EmpressBeamDraw.DrawMarker(Projectile.Center, dir, 2800f, 14f, c, opacity, 1f, 0f, 0.3f, 0.5f);
        }
    }

    /// <summary>女皇弹幕的帧末结算：追踪光球互斥、同帧多根只播最近一声的音效队列</summary>
    internal class EmpressProjectileSystem : ModSystem
    {
        private static Vector2? closestShot;
        private static Vector2? closestBuildup;
        private static Terraria.Audio.SoundStyle shotStyle;
        private static Terraria.Audio.SoundStyle buildupStyle;

        /// <summary>登记一声发射音，只保留离屏幕中心最近的一处</summary>
        public static void EnqueueShot(Vector2 pos, Terraria.Audio.SoundStyle style) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 center = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            if (Vector2.Distance(pos, center) > 3200f) {
                return;
            }
            if (!closestShot.HasValue || Vector2.Distance(pos, center) < Vector2.Distance(closestShot.Value, center)) {
                closestShot = pos;
                shotStyle = style;
            }
        }

        public static void EnqueueBuildup(Vector2 pos, Terraria.Audio.SoundStyle style) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 center = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            if (Vector2.Distance(pos, center) > 2600f) {
                return;
            }
            if (!closestBuildup.HasValue || Vector2.Distance(pos, center) < Vector2.Distance(closestBuildup.Value, center)) {
                closestBuildup = pos;
                buildupStyle = style;
            }
        }

        public override void PostUpdateProjectiles() {
            EmpressLightBolt.RepelChasers();
            if (VaultUtils.isServer) {
                return;
            }
            EmpressMeltingFan.ResolveAttention();
            if (closestShot.HasValue) {
                Terraria.Audio.SoundEngine.PlaySound(shotStyle, closestShot.Value);
                closestShot = null;
            }
            if (closestBuildup.HasValue) {
                Terraria.Audio.SoundEngine.PlaySound(buildupStyle, closestBuildup.Value);
                closestBuildup = null;
            }
        }
    }
}
