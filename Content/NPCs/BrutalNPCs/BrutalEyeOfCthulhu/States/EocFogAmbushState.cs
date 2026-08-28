using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 血雾伏击：绕玩家播撒遮蔽雾团→潜入→雾间隐行→择雾红脉冲预警→破雾扑杀<br/>
    /// 出击雾团由权威端掷骰，其弹幕索引写入 npc.ai[3]，脉冲预警走雾团弹幕自身 ai[1] 同步
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.FogAmbush, typeof(EocStateContext))]
    internal class EocFogAmbushState : EocStateBase
    {
        public override string StateName => "EocFogAmbush";
        public override EocStateIndex StateIndex => EocStateIndex.FogAmbush;
        public override bool AllowFogStep => false;

        private enum AmbushPhase
        {
            Seed,       //播雾
            DiveIn,     //潜入最近雾团
            Lurk,       //雾间隐行至出击雾团
            PulseWait,  //红脉冲预警
            Emerge,     //破雾扑杀
        }

        private const int SeedTime = 44;
        private const int DiveTime = 50;
        private const int LurkTime = 64;
        private const int PulseTime = 42;
        private const int EmergeFlight = 30;
        private const int EmergeBrake = 14;

        private float EmergeSpeed => Context.IsAsuraMode ? 52f : 47f;
        private int MaxAmbushes => Context.IsSecondPhase ? 2 : 1;

        private EocStateContext Context;
        private AmbushPhase phase;
        private int ambushCount;
        private bool launched;
        private bool finished;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            phase = AmbushPhase.Seed;
            ambushCount = 0;
            launched = false;
            finished = false;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            switch (phase) {
                case AmbushPhase.Seed:
                    UpdateSeed(npc, player, context);
                    break;
                case AmbushPhase.DiveIn:
                    UpdateDiveIn(npc, context);
                    break;
                case AmbushPhase.Lurk:
                    UpdateLurk(npc, context);
                    break;
                case AmbushPhase.PulseWait:
                    UpdatePulseWait(npc, player, context);
                    break;
                case AmbushPhase.Emerge:
                    UpdateEmerge(npc, player, context);
                    break;
            }

            //收招决策仅权威端
            if (finished && !VaultUtils.isClient) {
                return new EocVeilHoverState(context.IsAsuraMode ? 42 : 58);
            }

            return null;
        }

        private void SwitchPhase(AmbushPhase next) {
            phase = next;
            Timer = 0;
        }

        #region 播雾
        private void UpdateSeed(NPC npc, Player player, EocStateContext context) {
            //上仰后撤，喉音起手
            Vector2 rearPoint = player.Center + new Vector2(0f, -430f);
            EocMotion.SpringHover(npc, rearPoint, 0.02f, 0.1f, 24f);
            FaceTarget(npc, player.Center, 0.3f);
            context.SetChargeState(2, Timer / (float)SeedTime);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 1f, Pitch = -0.7f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.8f, Pitch = -0.5f }, npc.Center);
            }

            //权威端一次性播撒雾团
            if (Timer == 12 && !VaultUtils.isClient) {
                int cloudCount = Context.IsSecondPhase ? 6 : 5;
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < cloudCount; i++) {
                    float angle = baseAngle + MathHelper.TwoPi * i / cloudCount
                        + Main.rand.NextFloat(-0.3f, 0.3f);
                    float radius = Main.rand.NextFloat(430f, 590f);
                    Vector2 pos = player.Center + angle.ToRotationVector2() * radius;
                    Vector2 drift = angle.ToRotationVector2() * 0.8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, drift,
                        ModContent.ProjectileType<EocFogCloud>(), 0, 0f, Main.myPlayer);
                }
            }

            //播撒时本体也喷吐雾息
            if (Timer > 10 && Timer % 4 == 0) {
                EocMotion.MistPuff(npc.Center + Main.rand.NextVector2Circular(50f, 50f), 1, 1.1f, 0.4f);
            }
            EocScreenFX.PushVignette(0.25f);

            Timer++;
            if (Timer >= SeedTime) {
                launched = false;
                SwitchPhase(AmbushPhase.DiveIn);
            }
        }
        #endregion

        #region 潜入
        private void UpdateDiveIn(NPC npc, EocStateContext context) {
            Projectile nearest = FindNearestCloud(npc.Center, ignoreIndex: -1);
            if (nearest == null) {
                //雾没了(被清或极端情况)，直接转出击收尾
                if (!VaultUtils.isClient) {
                    npc.ai[3] = -1f;
                }
                launched = false;
                SwitchPhase(AmbushPhase.Emerge);
                return;
            }

            if (!launched) {
                launched = true;
                Vector2 dir = (nearest.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                //坦率的潜入冲刺，可读可躲
                EocMotion.DashLaunch(npc, context, dir, 37f, 0.85f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }

            FaceVelocity(npc);
            EnableContactDamageIfFast(npc, 24f, 1f);
            context.PushDashVisuals(0.85f, 0.85f);

            Timer++;
            //抵达雾团或超时
            if (npc.Distance(nearest.Center) < nearest.width * 1.4f + 60f || Timer >= DiveTime) {
                context.FogHideGoal = 1f;
                EocMotion.MistPuff(npc.Center, 5, 1.5f, 0.5f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.6f }, npc.Center);
                }
                //权威端择出击雾团，索引写 ai[3]
                if (!VaultUtils.isClient) {
                    Projectile chosen = PickAmbushCloud(npc, nearest.whoAmI);
                    npc.ai[3] = chosen != null ? chosen.whoAmI : -1f;
                    npc.netUpdate = true;
                }
                SwitchPhase(AmbushPhase.Lurk);
            }
        }
        #endregion

        #region 隐行
        private void UpdateLurk(NPC npc, EocStateContext context) {
            context.FogHideGoal = 1f;
            EocScreenFX.PushVignette(0.42f);
            DisableContactDamage(npc);   //隐行不撞人，公平阀

            Projectile targetCloud = GetCloudByIndex((int)npc.ai[3]);
            if (targetCloud == null) {
                //目标雾团失效→直接进入出击
                SwitchPhase(AmbushPhase.PulseWait);
                return;
            }

            //雾间隐行：前段悠游，后段快速转移
            float t = Timer / (float)LurkTime;
            Vector2 toTarget = targetCloud.Center - npc.Center;
            if (t < 0.35f) {
                npc.velocity *= 0.92f;
                npc.velocity += Main.rand.NextVector2Circular(0.4f, 0.4f);
            }
            else {
                float speed = MathHelper.Lerp(10f, 33f, VaultUtils.EaseInQuad((t - 0.35f) / 0.65f));
                npc.velocity = Vector2.Lerp(npc.velocity, toTarget.SafeNormalize(Vector2.Zero) * speed, 0.2f);
            }
            FaceVelocity(npc);

            //移动时拖出雾丝，位置的公平线索
            if (Timer % 5 == 0) {
                EocMotion.MistPuff(npc.Center, 1, 0.9f, 0.28f);
            }
            //湿滑喉音间歇暴露位置
            if (Timer % 26 == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.45f, Pitch = -0.7f }, npc.Center);
            }

            Timer++;
            if (Timer >= LurkTime || npc.Distance(targetCloud.Center) < 50f) {
                //到位，点亮红脉冲预警（权威端置位）
                if (!VaultUtils.isClient && targetCloud.ModProjectile is EocFogCloud) {
                    targetCloud.ai[1] = 1f;
                    targetCloud.netUpdate = true;
                }
                SwitchPhase(AmbushPhase.PulseWait);
            }
        }
        #endregion

        #region 脉冲预警
        private void UpdatePulseWait(NPC npc, Player player, EocStateContext context) {
            context.FogHideGoal = 1f;
            EocScreenFX.PushVignette(0.48f);
            EocScreenFX.PushPulse(0.5f + 0.3f * (Timer / (float)PulseTime));
            DisableContactDamage(npc);

            Projectile cloud = GetCloudByIndex((int)npc.ai[3]);
            if (cloud != null) {
                //贴附雾团中心微颤
                npc.velocity = (cloud.Center - npc.Center) * 0.15f;
                if (!VaultUtils.isServer) {
                    npc.position += Main.rand.NextVector2Circular(1.2f, 1.2f);
                }
            }
            else {
                npc.velocity *= 0.9f;
            }
            FaceTarget(npc, player.Center, 0.35f);
            context.SetChargeState(2, Timer / (float)PulseTime);
            context.PushIris(Timer / (float)PulseTime, EocMotion.IrisRed);

            Timer++;
            if (Timer >= PulseTime) {
                launched = false;
                SwitchPhase(AmbushPhase.Emerge);
            }
        }
        #endregion

        #region 破雾
        private void UpdateEmerge(NPC npc, Player player, EocStateContext context) {
            if (!launched) {
                launched = true;
                context.FogHideGoal = 0f;
                context.FogHide = 0.25f;   //破雾瞬间快速显形
                Vector2 predicted = EocMotion.PredictTarget(player, npc.Center, EmergeSpeed, 0.6f);
                Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                if (!VaultUtils.isClient) {
                    EocMotion.DashLaunch(npc, context, dir, EmergeSpeed, 1.25f);
                    //清出击旗，雾团转入快速消散
                    Projectile cloud = GetCloudByIndex((int)npc.ai[3]);
                    if (cloud != null) {
                        cloud.ai[1] = 0f;
                        cloud.timeLeft = Math.Min(cloud.timeLeft, 240);
                        cloud.netUpdate = true;
                    }
                    npc.netUpdate = true;
                }
                else {
                    EocMotion.DashLaunch(npc, context, dir, EmergeSpeed, 1.25f);
                }
                EocMotion.BloodBurst(npc.Center, 1.35f);
                EocMotion.Shake(npc.Center, 6f, 12, dir);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.95f, Pitch = 0.12f }, npc.Center);
                }
            }

            FaceVelocity(npc);
            EnableContactDamageIfFast(npc, 26f, Context.IsSecondPhase ? 1.3f : 1.15f);
            context.PushDashVisuals(1f, 1f);

            Timer++;
            if (Timer > EmergeFlight) {
                npc.velocity *= 0.72f;
                EocMotion.BrakeDroplets(npc);
            }

            if (Timer >= EmergeFlight + EmergeBrake) {
                ambushCount++;
                if (ambushCount < MaxAmbushes && HasAnyCloud()) {
                    //二阶段再潜一轮
                    launched = false;
                    SwitchPhase(AmbushPhase.DiveIn);
                    return;
                }
                if (!VaultUtils.isClient) {
                    npc.ai[3] = 0f;
                    npc.netUpdate = true;
                }
                finished = true;
            }
        }
        #endregion

        #region 雾团检索
        private static Projectile GetCloudByIndex(int index) {
            if (index < 0 || index >= Main.maxProjectiles) {
                return null;
            }
            Projectile proj = Main.projectile[index];
            if (!proj.active || proj.ModProjectile is not EocFogCloud) {
                return null;
            }
            return proj;
        }

        private static Projectile FindNearestCloud(Vector2 from, int ignoreIndex) {
            Projectile best = null;
            float bestDist = float.MaxValue;
            int fogType = ModContent.ProjectileType<EocFogCloud>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != fogType || proj.whoAmI == ignoreIndex) {
                    continue;
                }
                float dist = Vector2.DistanceSquared(from, proj.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = proj;
                }
            }
            return best;
        }

        /// <summary>掷骰选出击雾团，避开当前所在，仅权威端</summary>
        private static Projectile PickAmbushCloud(NPC npc, int currentIndex) {
            List<Projectile> candidates = [];
            int fogType = ModContent.ProjectileType<EocFogCloud>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != fogType || proj.whoAmI == currentIndex) {
                    continue;
                }
                if (proj.timeLeft < 200) {
                    continue;
                }
                candidates.Add(proj);
            }
            if (candidates.Count == 0) {
                return GetCloudByIndex(currentIndex);
            }
            return candidates[Main.rand.Next(candidates.Count)];
        }

        private static bool HasAnyCloud() {
            int fogType = ModContent.ProjectileType<EocFogCloud>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == fogType && proj.timeLeft > 260) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        public override void OnExit(EocStateContext context) {
            base.OnExit(context);
            context.FogHideGoal = 0f;
        }
    }
}
