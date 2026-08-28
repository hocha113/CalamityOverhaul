using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 超空泡终拳（P3 独有）：三记速拳（双螯交替，各留一颗小空泡）→
    /// 66f 双螯合拢巨蓄（75% 静默拍）→ 合击巨拳 → 巨型空泡 42f 后爆缩，
    /// 并炸出十二向水弹环。声明式缺口：环上朝主体一侧空出三席（贴身即安全，
    /// 鼓励压进；由跳位循环保证而非注释）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.SuperCavitation, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpSuperCavitationState : SeaShrimpStateBase
    {
        public override string StateName => "SuperCavitation";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.SuperCavitation;

        private const int QuickCycle = 34;
        private const int QuickCount = 3;
        private const int QuickEnd = QuickCycle * QuickCount;
        private const int GrandChargeEnd = QuickEnd + 66;
        private const int GrandLockFrame = QuickEnd + 38;
        private const int GrandQuietFrame = QuickEnd + 50;
        private const int RingFrame = GrandChargeEnd + 4 + 42;
        private const int Total = RingFrame + 40;

        /// <summary>环弹总数与主体侧空席数（贴身安全弧 ≈ 90°）</summary>
        private const int RingCount = 12;
        private const int RingSafeSlots = 3;

        private Vector2 quickLock = Vector2.UnitX;
        private Vector2 grandLock = Vector2.UnitX;
        private Vector2 grandPoint;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);

            if (t < QuickEnd) {
                UpdateQuickPunches(ctx, npc, t);
                return null;
            }

            if (t < GrandChargeEnd) {
                UpdateGrandCharge(ctx, npc, t);
                return null;
            }

            if (t == GrandChargeEnd) {
                //合击帧：双螯同点轰出
                for (int a = 0; a < 2; a++) {
                    ctx.Owner.Skeleton.Arms[a].Impulse(grandLock * 54f);
                }
                grandPoint = ctx.Owner.Skeleton.ShoulderWorld(0) + grandLock * 300f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 1f, Pitch = -0.3f }, npc.Center);
                    ShakeNearby(npc.Center, 9f);
                }
                if (!VaultUtils.isClient) {
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.ClawStrikeDamage);
                    for (int a = 0; a < 2; a++) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                            ModContent.ProjectileType<SeaShrimpClawHitbox>(), damage, 4f, Main.myPlayer,
                            npc.whoAmI, a);
                    }
                }
            }

            if (t == GrandChargeEnd + 4 && !VaultUtils.isClient) {
                //巨泡落位：42f 后爆缩
                int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.CavitationDamage);
                Projectile.NewProjectile(npc.GetSource_FromAI(), grandPoint, Vector2.Zero,
                    ModContent.ProjectileType<SeaShrimpCavitationBubble>(), damage, 3f,
                    Main.myPlayer, 42f, 205f);
            }

            if (t <= GrandChargeEnd + 10) {
                //合击持位与伤害窗
                for (int a = 0; a < 2; a++) {
                    ctx.Claws[a] = new ClawDirective {
                        Mode = ClawMode.Strike,
                        Target = ctx.Owner.Skeleton.ShoulderWorld(a) + grandLock * 290f,
                        Spring = 0.6f,
                        Damping = 0.85f,
                        ClawOpen = 0f,
                    };
                }
                ctx.ClawDamageWindow = t >= GrandChargeEnd + 2 && t <= GrandChargeEnd + 9;
                ctx.SpineCurl = 0.26f;
                ctx.AfterimageStrength = 0.7f;
            }

            if (t == RingFrame && !VaultUtils.isClient) {
                //爆缩环：十二向水弹，主体侧三席空出（贴身安全弧，跳位保证）
                int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.WaterBoltDamage);
                float baseAng = grandLock.ToRotation();
                float backAng = baseAng + MathHelper.Pi;
                for (int i = 0; i < RingCount; i++) {
                    float ang = baseAng + MathHelper.TwoPi * i / RingCount;
                    //空席判定：与"朝向主体"夹角最近的 RingSafeSlots 个位跳过
                    float delta = MathF.Abs(MathHelper.WrapAngle(ang - backAng));
                    if (delta < MathHelper.TwoPi / RingCount * (RingSafeSlots * 0.5f)) {
                        continue;
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(), grandPoint,
                        ang.ToRotationVector2() * 9f,
                        ModContent.ProjectileType<SeaShrimpWaterBolt>(), damage, 1.5f, Main.myPlayer);
                }
            }

            if (t >= Total) {
                return EndAttack(ctx, 72);
            }
            return null;
        }

        /// <summary>三记速拳：双螯交替，锁向-出拳-小空泡，节拍紧凑</summary>
        private void UpdateQuickPunches(SeaShrimpStateContext ctx, NPC npc, int t) {
            int cycle = t / QuickCycle;
            int ct = t % QuickCycle;
            int arm = cycle % 2;
            Vector2 shoulder = ctx.Owner.Skeleton.ShoulderWorld(arm);

            if (ct < 14) {
                quickLock = (PredictTarget(ctx, 8f) - shoulder).SafeNormalize(Vector2.UnitX);
            }

            if (ct < 22) {
                float w = ct / 22f;
                ctx.Claws[arm] = new ClawDirective {
                    Mode = ClawMode.Hold,
                    Target = shoulder - quickLock * (10f + MathF.Pow(w, 6f) * 46f),
                    Spring = 0.34f,
                    Damping = 0.7f,
                    ClawOpen = 0f,
                };
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, 0.5f + w * 0.4f);
                float aimAlpha = MathHelper.Clamp((ct - 6) / 10f, 0f, 1f) * (ct >= 14 ? 0.5f : 0.25f);
                ctx.AddSolidBeam(shoulder + quickLock * 24f, quickLock, 300f, aimAlpha, 0.8f);
                return;
            }

            if (ct == 22) {
                ctx.Owner.Skeleton.Arms[arm].Impulse(quickLock * 42f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.7f, Pitch = 0.15f, MaxInstances = 3 }, shoulder);
                    ShakeNearby(npc.Center, 4f);
                }
                if (!VaultUtils.isClient) {
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.ClawStrikeDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), shoulder, Vector2.Zero,
                        ModContent.ProjectileType<SeaShrimpClawHitbox>(), damage, 3f, Main.myPlayer,
                        npc.whoAmI, arm);
                }
            }

            if (ct == 26 && !VaultUtils.isClient) {
                int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.CavitationDamage);
                Projectile.NewProjectile(npc.GetSource_FromAI(), shoulder + quickLock * 230f,
                    Vector2.Zero, ModContent.ProjectileType<SeaShrimpCavitationBubble>(), damage, 2f,
                    Main.myPlayer, 20f, 78f);
            }

            ctx.Claws[arm] = new ClawDirective {
                Mode = ClawMode.Strike,
                Target = shoulder + quickLock * 230f,
                Spring = 0.55f,
                Damping = 0.82f,
                ClawOpen = 0f,
            };
            ctx.ClawDamageWindow = ct >= 23 && ct <= 28;
            ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, 0.5f);
        }

        /// <summary>巨蓄段：双螯合拢巨型后拉，向心晶光双流，75% 截停静默</summary>
        private void UpdateGrandCharge(SeaShrimpStateContext ctx, NPC npc, int t) {
            int ct = t - QuickEnd;
            float w = ct / 66f;

            if (t < GrandLockFrame) {
                grandLock = (PredictTarget(ctx, 12f) - npc.Center).SafeNormalize(Vector2.UnitX);
            }

            float reel = MathF.Pow(w, 8f) * 72f;
            for (int a = 0; a < 2; a++) {
                Vector2 shoulder = ctx.Owner.Skeleton.ShoulderWorld(a);
                ctx.Claws[a] = new ClawDirective {
                    Mode = ClawMode.Hold,
                    Target = shoulder - grandLock * (8f + reel),
                    Spring = 0.32f,
                    Damping = 0.66f,
                    ClawOpen = 0f,
                };
            }
            ctx.CrystalGlow = 1f;
            ctx.SpineCurl = -0.2f * w;

            if (!Main.dedServ && t < GrandQuietFrame && t % 2 == 0) {
                //双螯各一股向心晶光
                for (int a = 0; a < 2; a++) {
                    Vector2 claw = ctx.Owner.Skeleton.ClawTip(a);
                    Vector2 from = claw + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(60f, 140f);
                    PRTLoader.NewParticle<PRT_Spark>(from, (claw - from) * 0.12f,
                        Color.Lerp(SeaShrimpRenderer.CrystalBlue, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.45f, 0.85f))?.Configure(false, Main.rand.Next(9, 15));
                }
            }
            if (t == QuickEnd + 6 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.55f }, npc.Center);
            }

            float aimAlpha = MathHelper.Clamp((ct - 16) / 26f, 0f, 1f) * (t >= GrandLockFrame ? 0.7f : 0.32f);
            ctx.AddSolidBeam(npc.Center + grandLock * 40f, grandLock, 420f, aimAlpha,
                t >= GrandLockFrame ? 1f : 0.55f);
        }
    }
}
