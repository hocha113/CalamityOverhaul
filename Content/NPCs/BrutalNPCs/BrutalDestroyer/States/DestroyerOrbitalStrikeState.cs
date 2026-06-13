using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.Destroyer;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>低血量大招「轨道绞杀」：撤离→交叉俯冲2趟→垂直终结贯穿→回场散热</summary>
    /// <para>普攻俯冲见 <see cref="DestroyerDiveStrikeState"/>；全难度完整演出，Death 只调数值</para>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.OrbitalStrike, typeof(DestroyerStateContext))]
    internal class DestroyerOrbitalStrikeState : DestroyerStateBase
    {
        public override string StateName => "OrbitalStrike";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.OrbitalStrike;
        /// <summary>大招自带高空/地下走位，回归瞬移阀不介入</summary>
        public override bool AllowFarSnap => false;

        #region 演出节奏常量
        private const int AscendEnd = 60;
        private const int SilenceEnd = 100;
        private const int TelegraphTime = 42;
        private const int DiveTime = 54;
        private const int GapTime = 16;
        private const int PassLength = TelegraphTime + DiveTime + GapTime;
        private const int FinalTelegraphTime = 58;
        private const int FinalDiveMax = 90;
        private const int FinalHold = 36;
        private const int ReturnTime = 150;
        #endregion

        private int PassCount(DestroyerStateContext ctx) => 2;
        private float DiveSpeed(DestroyerStateContext ctx)
            => 82f + (ctx.IsDeathMode ? 10f : 0f);

        //以下字段均由 Timer 与同步的 npc.ai[3] 确定性推导，各端独立计算
        private Vector2 lineCenter;
        private Vector2 diveDir;
        private bool passBoomFired;
        private int currentPass = -1;
        private bool finalDiveStarted;
        private bool impactFired;
        private bool emergeFired;
        private Vector2 impactPoint;
        private float lockedX;

        public DestroyerOrbitalStrikeState() {
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 1;
            currentPass = -1;
            finalDiveStarted = false;
            impactFired = false;
            emergeFired = false;

            //服务端决定首趟俯冲方位并经 ai[3] 同步，后续趟次由此确定性推导
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.Next(2);
                context.Npc.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f, Volume = 1.2f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int passCount = PassCount(context);
            int divesEnd = SilenceEnd + passCount * PassLength;
            int finalDiveStart = divesEnd + FinalTelegraphTime;
            int returnStart = finalDiveStart + FinalDiveMax + FinalHold;

            Timer++;

            //幕一：蓄能撤离
            if (Timer <= AscendEnd) {
                UpdateAscend(context);
                return null;
            }

            //幕一后段：高空静默蓄势
            if (Timer <= SilenceEnd) {
                UpdateSilence(context);
                return null;
            }

            //幕二：交叉俯冲
            if (Timer <= divesEnd) {
                UpdateCrossDives(context, Timer - SilenceEnd - 1);
                return null;
            }

            //幕三：终结贯穿，垂直预警
            if (Timer <= finalDiveStart) {
                UpdateFinalTelegraph(context, Timer - divesEnd);
                return null;
            }

            //幕三：垂直俯冲与冲击
            if (Timer <= returnStart) {
                UpdateFinalDive(context);
                return null;
            }

            //破土回场：散热惩罚窗口
            if (Timer <= returnStart + ReturnTime) {
                UpdateReturn(context, (Timer - returnStart) / (float)ReturnTime);
                return null;
            }

            return new DestroyerPatrolState();
        }

        #region 幕一：撤离与静默

        private void UpdateAscend(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            float progress = Timer / (float)AscendEnd;

            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 1;
            npc.damage = 0;

            //撤离不伤人，目标点在玩家正上方极高处，速度随充能急剧攀升
            float speed = MathHelper.Lerp(16f, 64f, progress * progress);
            SetMovement(context, player.Center + new Vector2(0, -2400f), speed, 1.2f);
            context.AccelRate = 0.09f;

            //撤离加速段挂上热浪尾流
            if (Timer == 20 && !VaultUtils.isClient) {
                DestroyerHeatWakeProj.EnsureForHead(npc);
            }

            //全身充能波循环加速：能量一圈圈涌向头部
            float wavePhase = 1f - (Timer * (0.012f + progress * 0.05f)) % 1f;
            DestroyerChargeWave.Push(npc.whoAmI, wavePhase, 0.28f, 0.4f + 0.6f * progress);

            //尾烟（仅客户端）
            if (!VaultUtils.isServer && Timer % 3 == 0 && DestroyerMotionFX.OnScreen(npc.Center)) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.Smoke, 0, 0, 140, default, Main.rand.NextFloat(1.4f, 2.2f));
                dust.noGravity = true;
                dust.velocity = -npc.velocity * 0.1f;
            }
        }

        private void UpdateSilence(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 1;
            npc.damage = 0;

            //保持在极高空盘旋待命
            SetMovement(context, player.Center + new Vector2(0, -2600f), 30f, 0.8f);

            //远处轰鸣 + 持续低强度震动，制造"暴风雨前"的压迫感
            if (Timer == SilenceEnd - 30) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.8f, Pitch = -0.7f }, player.Center);
            }
            if (Timer % 24 == 0) {
                DestroyerMotionFX.CameraPunch(player.Center, 2f, 20, "DestroyerOrbitalRumble");
            }
        }

        #endregion

        #region 幕二：交叉俯冲

        private void UpdateCrossDives(DestroyerStateContext context, int diveTimer) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int passIndex = Math.Min(diveTimer / PassLength, PassCount(context) - 1);
            int t = diveTimer - passIndex * PassLength;

            //新一趟开始：确定本趟贯穿线（方位由同步的 ai[3] 推导，左右交替）
            if (passIndex != currentPass) {
                currentPass = passIndex;
                passBoomFired = false;

                int side = ((int)npc.ai[3] + passIndex) % 2 == 0 ? 1 : -1;
                float angleFromVertical = MathHelper.ToRadians(38f + passIndex * 7f) * side;
                diveDir = Vector2.UnitY.RotatedBy(angleFromVertical);
                lineCenter = player.Center + player.velocity * 24f;

                //预警线（服务端生成同步弹幕，所有玩家看到一致的警告）
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        lineCenter - diveDir * 2400f, diveDir,
                        ModContent.ProjectileType<DestroyerStrikeTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, DestroyerStrikeTelegraph.PackParams(0, TelegraphTime));
                }
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.2f, Volume = 0.85f }, player.Center);
            }

            //预警阶段：蠕虫仍在高空，等待线锁定
            if (t < TelegraphTime) {
                context.SkipDefaultMovement = false;
                context.OrbitalVisual = 1;
                npc.damage = 0;
                SetMovement(context, player.Center + new Vector2(0, -2600f), 30f, 0.8f);
                DestroyerChargeWave.Push(npc.whoAmI, 1f - t / (float)TelegraphTime, 0.25f, 0.8f);
                return;
            }

            //俯冲释放帧：瞬移到线外起点并全速贯入（轨迹与预警线一致，公平可躲）
            if (t == TelegraphTime) {
                npc.Center = lineCenter - diveDir * 2700f;
                npc.velocity = diveDir * DiveSpeed(context);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                //ForceRoar：避免被上一声未播完的Roar按IgnoreNew上限吞掉（详见DiveStrike同处注释）
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.35f, Volume = 1f }, player.Center);
                //俯冲瞬间天空闪雷
                MachineEffect.TriggerSkyFlash(lineCenter, 1f);
                if (!VaultUtils.isClient) {
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
            }

            //俯冲阶段：直线贯穿，接触伤害开启
            if (t <= TelegraphTime + DiveTime) {
                context.SkipDefaultMovement = true;
                context.OrbitalVisual = 2;
                npc.damage = npc.defDamage;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                //贴近战场中心时引爆音爆扭曲环
                if (!passBoomFired && npc.Distance(lineCenter) < 340f) {
                    passBoomFired = true;
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), lineCenter, Vector2.Zero,
                            ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 1);
                    }
                    DestroyerMotionFX.CameraPunch(lineCenter, 7f, 16, "DestroyerOrbitalPass", diveDir);
                }
                return;
            }

            //间隙：冲出屏幕后略微减速，准备下一趟
            context.SkipDefaultMovement = true;
            context.OrbitalVisual = 1;
            npc.damage = 0;
            npc.velocity *= 0.97f;
        }

        #endregion

        #region 幕三：终结贯穿

        private void UpdateFinalTelegraph(DestroyerStateContext context, int t) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 1;
            npc.damage = 0;
            SetMovement(context, player.Center + new Vector2(0, -2800f), 34f, 0.9f);

            //垂直警告光柱：横向跟随玩家，锁定窗口后定格
            if (t == 1) {
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        new Vector2(player.Center.X, player.Center.Y - 1600f), Vector2.UnitY,
                        ModContent.ProjectileType<DestroyerStrikeTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, npc.target, DestroyerStrikeTelegraph.PackParams(2, FinalTelegraphTime));
                }
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.5f, Volume = 1.1f }, player.Center);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.7f, Pitch = -0.5f }, player.Center);
            }

            //与预警线锁定时机一致地捕获X坐标
            if (t == FinalTelegraphTime - DestroyerStrikeTelegraph.LockTime) {
                lockedX = player.Center.X + player.velocity.X * 10f;
            }

            DestroyerChargeWave.Push(npc.whoAmI, 1f - t / (float)FinalTelegraphTime, 0.3f, 1f);
        }

        private void UpdateFinalDive(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = true;

            //俯冲起始：瞬移到锁定X的高空，垂直全速向下
            if (!finalDiveStarted) {
                finalDiveStarted = true;
                if (lockedX == 0f) {
                    lockedX = player.Center.X;
                }
                impactPoint = DestroyerMotionFX.FindGroundBelow(new Vector2(lockedX, player.Center.Y));
                npc.Center = new Vector2(lockedX, player.Center.Y - 2700f);
                npc.velocity = Vector2.UnitY * (DiveSpeed(context) + 14f);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                //ForceRoar：终结贯穿是全场最重的一声，绝不能被实例上限吞掉
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.5f, Volume = 1.1f }, player.Center);
                //终结贯穿释放：最强一道闪雷劈向冲击点
                MachineEffect.TriggerSkyFlash(impactPoint, 1f);
                if (!VaultUtils.isClient) {
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
            }

            context.OrbitalVisual = impactFired ? 1 : 2;
            npc.damage = impactFired ? 0 : npc.defDamage;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //砸入大地：巨型冲击 + 碎屑喷泉 + 全身闪烁
            if (!impactFired && npc.Center.Y >= impactPoint.Y) {
                impactFired = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), impactPoint, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 2);
                }
                DestroyerMotionFX.SpawnImpactBlast(impactPoint, 1.6f);
                DestroyerMotionFX.CameraPunch(impactPoint, 16f, 32, "DestroyerOrbitalImpact", Vector2.UnitY);
                DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, 1f, fullBody: true);
            }

            //冲击后继续钻深，逐渐减速
            if (impactFired) {
                npc.velocity *= 0.965f;
            }
        }

        private void UpdateReturn(DestroyerStateContext context, float progress) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //破土回场：散热冒烟的惩罚窗口，不开火、不造成接触伤害
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 3;
            context.SlitherStrength = 0.6f;
            npc.damage = 0;

            float side = ((int)npc.ai[3] % 2 == 0) ? 1f : -1f;
            SetMovement(context, player.Center + new Vector2(side * 620f, -430f), 24f, 0.9f);
            context.AccelRate = 0.05f;

            //破土瞬间：从地下回到玩家水平线以上时炸开一圈尘土
            if (!emergeFired && npc.Center.Y < player.Bottom.Y) {
                emergeFired = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 0);
                }
                DestroyerMotionFX.CameraPunch(npc.Center, 6f, 14, "DestroyerOrbitalEmerge", -Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1f, Pitch = -0.2f }, npc.Center);
            }

            //散热浓烟从可见体节冒出（仅客户端，概率+剔除）
            if (!VaultUtils.isServer && Main.GameUpdateCount % 4 == 0) {
                foreach (var seg in context.BodySegments) {
                    if (!seg.Alives() || !Main.rand.NextBool(14) || !DestroyerMotionFX.OnScreen(seg.Center)) {
                        continue;
                    }
                    PRTTypes.PRT_Smoke smoke = InnoVault.PRT.PRTLoader.NewParticle<PRTTypes.PRT_Smoke>(
                        seg.Center, -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2f),
                        new Color(66, 60, 56), Main.rand.NextFloat(0.5f, 0.9f));
                    smoke?.Configure(Main.rand.Next(35, 60), 0.55f * (1f - progress), Main.rand.NextFloat(-0.04f, 0.04f));
                }
            }
        }

        #endregion

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 0;
            context.AccelRate = 0.055f;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
