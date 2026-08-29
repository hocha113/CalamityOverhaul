using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.BrainOfCthulhu
{
    /// <summary>
    /// 换位心跳脉冲环：从换位端点炸开的血色环形判定，命中施加困惑。
    /// ai[0]=本地推进的年龄(各端同速自增)，ai[1]=1 为主拍(承担第二记心音)。
    /// 命中判定按环带做圆环相交，每个目标全程只吃一次
    /// </summary>
    internal class MirrorheartPulse : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>扩张时长(帧)</summary>
        private const int ExpandTime = 24;
        /// <summary>总寿命(帧)，含收尾淡出</summary>
        private const int LifeTime = 34;
        /// <summary>环最大半径(px)</summary>
        private const float MaxRadius = 400f;
        /// <summary>判定带内缘余量(px)</summary>
        private const float BandInner = 70f;
        /// <summary>判定带外缘余量(px)</summary>
        private const float BandOuter = 26f;
        /// <summary>困惑时长(帧)，2.5秒</summary>
        private const int ConfusedTime = 150;

        private ref float Age => ref Projectile.ai[0];
        private bool IsPrimary => Projectile.ai[1] == 1f;

        private float CurrentRadius => BrainMotion.SharpOut(Age / ExpandTime, 4) * MaxRadius;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeTime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center,
                BrainMotion.BloodBright.ToVector3() * (0.9f * (1f - Age / LifeTime)));

            if (VaultUtils.isServer) {
                return;
            }

            //第二记心音(dub)：主拍环在第9帧补半拍
            if (IsPrimary && Age == 9f) {
                BrainHeartbeat.Thump(0.6f);
                BrainHeartbeat.PlayThumpSound(Projectile.Center, 0.6f, 0.15f);
            }

            if (!BrainMotion.OnScreen(Projectile.Center, 700f)) {
                return;
            }

            //扩张期波前甩血珠
            float radius = CurrentRadius;
            if (Age <= ExpandTime && Age % 3 == 0) {
                for (int i = 0; i < 2; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 rim = Projectile.Center + angle.ToRotationVector2() * radius;
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(rim,
                        angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5.5f),
                        Color.Lerp(BrainMotion.BloodBright, BrainMotion.BloodDark, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(18, 30), 0.34f);
                }
            }

            //被困惑敌人头顶的幻象粒子(困惑经原版 buff 同步，各端都看得见)
            if (Age % 10 == 0) {
                int shown = 0;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.friendly || npc.dontTakeDamage || !npc.HasBuff(BuffID.Confused)) {
                        continue;
                    }
                    if (npc.Distance(Projectile.Center) > radius + 90f) {
                        continue;
                    }
                    Vector2 head = npc.Top - Vector2.UnitY * 14f;
                    PRTLoader.NewParticle<PRT_Spark>(head + Main.rand.NextVector2Circular(10f, 6f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f),
                        Color.Lerp(BrainMotion.MirrorCold, Color.White, Main.rand.NextFloat(0.35f)),
                        Main.rand.NextFloat(0.5f, 0.8f))?.Configure(true, Main.rand.Next(10, 18));
                    PRTLoader.NewParticle<PRT_BrainBloodMist>(head, -Vector2.UnitY * 0.4f,
                        BrainMotion.MirrorCold * 0.55f, 0.3f)?.Configure(Main.rand.Next(14, 22));
                    if (++shown >= 8) {
                        break;
                    }
                }
            }
        }

        /// <summary>环带命中：目标矩形与环带 [r-内缘, r+外缘] 相交才算</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = CurrentRadius;
            if (radius < 4f) {
                return false;
            }
            Vector2 center = Projectile.Center;
            Vector2 nearest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            float dNear = Vector2.Distance(center, nearest);
            float dx = Math.Max(Math.Abs(center.X - targetHitbox.Left), Math.Abs(center.X - targetHitbox.Right));
            float dy = Math.Max(Math.Abs(center.Y - targetHitbox.Top), Math.Abs(center.Y - targetHitbox.Bottom));
            float dFar = MathF.Sqrt(dx * dx + dy * dy);
            return dFar >= radius - BandInner && dNear <= radius + BandOuter;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //AddBuff 客户端自动上报服务端(原版包53)，跨端同步
            target.AddBuff(BuffID.Confused, ConfusedTime);
            if (!VaultUtils.isServer) {
                BrainMotion.BloodMistBurst(target.Center, 0.5f, 3, 4f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float age = Age;
            float fade = MathHelper.Clamp((LifeTime - age) / 10f, 0f, 1f);
            float radius = CurrentRadius;

            //主环：波前亮缘+血色环带+暗血尾波
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius,
                36f * (0.6f + 0.4f * fade),
                new Color(255, 118, 100), BrainMotion.BloodBright, BrainMotion.BloodDark,
                fade, -1f, 1f, 0.28f * fade, Projectile.identity * 0.31f);

            //滞后半拍的第二环(lub-dub 的 dub)
            if (age > 9f) {
                float lagRadius = BrainMotion.SharpOut((age - 9f) / ExpandTime, 4) * MaxRadius * 0.82f;
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, lagRadius, 20f,
                    BrainMotion.HeartGlow, BrainMotion.BloodDark, BrainMotion.BloodDark,
                    fade * 0.55f, -1f, 1f, 0f, Projectile.identity * 0.77f);
            }

            //起爆心光：前10帧的心脏闪(黑底贴图走 A=0 加色)
            if (age < 10f) {
                float k = 1f - age / 10f;
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Texture2D flare = CWRAsset.StarFlare01.Value;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Main.spriteBatch.Draw(glow, drawPos, null, new Color(255, 96, 84, 0) * (0.85f * k),
                    0f, glow.Size() * 0.5f, 2.6f * (1f + age * 0.08f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(flare, drawPos, null, new Color(255, 150, 140, 0) * (0.7f * k),
                    age * 0.06f, flare.Size() * 0.5f, 0.5f * k + 0.2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
