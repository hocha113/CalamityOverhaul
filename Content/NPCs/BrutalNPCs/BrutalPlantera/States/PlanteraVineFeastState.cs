using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>
    /// 绞藤飨宴(二阶段投技)：缠卷蓄势→缠足藤索射出→命中则把玩家
    /// 沿荆棘拖回巨口，三拍咀嚼+孢子毒雾喷面，压缩静默后连壳吐飞；
    /// 空挥则软垂硬直。与猛扑(本体飞向玩家)方向相反：藤把玩家拽向本体。
    /// 子相位走 npc.ai[0]、被抓者 whoAmI+1 走 npc.ai[1]，权威端推进各端跟随；
    /// 被抓玩家的位移/锁控/结算伤害全部由其本人客户端在 PlanteraGrabPlayer 施加
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.VineFeast, typeof(PlanteraStateContext))]
    internal class PlanteraVineFeastState : PlanteraStateBase
    {
        public override string StateName => "VineFeast";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.VineFeast;

        #region 子相位与节拍常量(运镜/玩家侧共用)
        internal const int SubCoil = 0;
        internal const int SubLash = 1;
        internal const int SubDrag = 2;
        internal const int SubChew = 3;
        internal const int SubSpit = 4;
        internal const int SubWhiff = 5;
        internal const int SubRecover = 6;

        /// <summary>缠卷蓄势时长(前摇，满足≥40tick公平阀)</summary>
        internal const int CoilTime = 48;
        /// <summary>蓄势中锁定瞄准+预警线的时刻</summary>
        internal const int AimLockTick = 30;
        /// <summary>藤索飞行窗口上限</summary>
        internal const int LashMaxTime = 40;
        /// <summary>藤索射速</summary>
        internal const float LashSpeed = 30f;
        /// <summary>藤索射程</summary>
        internal const float LashRange = 880f;
        /// <summary>缠中顿帧</summary>
        internal const int HitStopTime = 6;
        /// <summary>拖拽时长(含顿帧)</summary>
        internal const int DragTime = 58;
        /// <summary>单拍咀嚼周期</summary>
        internal const int BitePeriod = 42;
        /// <summary>拍内咬合帧</summary>
        internal const int BiteSnapTick = 22;
        /// <summary>咀嚼总长(三拍)</summary>
        internal const int ChewTime = BitePeriod * 3;
        /// <summary>吐飞段时长</summary>
        internal const int SpitTime = 30;
        /// <summary>吐飞段内的弹射帧(之前是压缩静默)</summary>
        internal const int SpitYeetTick = 10;
        /// <summary>空挥软垂时长(惩罚窗)</summary>
        internal const int WhiffTime = 45;
        /// <summary>收势回摆时长</summary>
        internal const int RecoverTime = 40;
        /// <summary>咀嚼锚距(巨口离本体中心)</summary>
        internal const float MawHoldDist = 54f;
        /// <summary>拖拽途中荆棘刮擦的两拍</summary>
        internal const int ScrapeTickA = 24;
        internal const int ScrapeTickB = 44;
        /// <summary>拖拽结束仍离口过远则脱手(卡地形公平放生)</summary>
        internal const float DragGiveUpDist = 210f;
        /// <summary>救援阀：抓取期间本体掉血超此比例提前吐人</summary>
        internal const float RescueDamageRatio = 0.04f;
        /// <summary>全状态保底超时</summary>
        private const int HardTimeout = 480;
        #endregion

        private int lastSubPhase = -1;
        private int subTimer;
        private Vector2 aimDir = Vector2.UnitY;
        private bool aimLocked;
        /// <summary>抓取起始时本体血量(救援阀基准，服务端)</summary>
        private int lifeAtGrab;

        public PlanteraVineFeastState() {
        }

        #region 共享静态助手(玩家侧/藤索/运镜复用)
        /// <summary>正在演投技且接管在场的世纪之花，无则null</summary>
        internal static NPC FindFeastBoss() {
            foreach (var n in Main.ActiveNPCs) {
                if (n.type != NPCID.Plantera) {
                    continue;
                }
                if (PlanteraAI.GetStateIndex(n) != PlanteraStateIndex.VineFeast) {
                    continue;
                }
                //确认CWR接管在场，防原版ai[2]撞值
                if (!n.TryGetOverride(out PlanteraAI ov) || ov == null) {
                    continue;
                }
                return n;
            }
            return null;
        }

        /// <summary>投技子相位，仅在VineFeast状态下有意义</summary>
        internal static int GrabSubPhase(NPC boss) => (int)boss.ai[0];

        /// <summary>被抓玩家whoAmI，无人为-1</summary>
        internal static int GrabVictim(NPC boss) => (int)boss.ai[1] - 1;

        /// <summary>巨口朝向(本体贴图口面向rotation-π/2方向)</summary>
        internal static Vector2 MawDir(NPC boss) => (boss.rotation - MathHelper.PiOver2).ToRotationVector2();

        /// <summary>巨口世界坐标</summary>
        internal static Vector2 MawWorld(NPC boss) => boss.Center + MawDir(boss) * MawHoldDist;

        /// <summary>可被缠住的玩家</summary>
        internal static bool VictimEligible(Player player)
            => player.Alives() && !player.ghost && !player.shimmering;
        #endregion

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            lastSubPhase = -1;
            subTimer = 0;
            aimLocked = false;
            lifeAtGrab = 0;
            aimDir = context.Target.Alives()
                ? (context.Target.Center - context.Npc.Center).SafeNormalize(Vector2.UnitY)
                : Vector2.UnitY;

            NPC npc = context.Npc;
            if (!VaultUtils.isClient) {
                npc.ai[0] = SubCoil;
                npc.ai[1] = 0f;
                npc.netUpdate = true;
                //两只钩爪撤到身后撑地(拔河桩)，与猛扑的前置锚形成方向差异
                Vector2 back = -aimDir;
                for (int i = 0; i < context.Hooks.Count && i < 2; i++) {
                    Vector2 wish = npc.Center + back * (380f + i * 200f)
                        + back.RotatedBy(MathHelper.PiOver2) * (i == 0 ? 120f : -120f);
                    PlanteraHookAI.Command(context.Hooks[i], PlanteraHookAI.FindAnchorNear(wish, 8f, Vector2.Zero));
                }
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.7f, Pitch = -0.6f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = -0.7f }, npc.Center);
            }
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;
            context.SkipDefaultMovement = true;
            Timer++;

            //子相位跟随：权威端写ai[0]，各端在此统一检测切换并重置本地节拍
            int sub = (int)npc.ai[0];
            if (sub != lastSubPhase) {
                lastSubPhase = sub;
                subTimer = 0;
                EnterSubPhase(context, sub);
            }
            else {
                subTimer++;
            }

            //保底超时：任何异常都能回到连接态
            if (Timer > HardTimeout && !VaultUtils.isClient) {
                return new PlanteraCanopyState();
            }

            switch (sub) {
                case SubCoil:
                    UpdateCoil(context);
                    break;
                case SubLash:
                    UpdateLash(context);
                    break;
                case SubDrag:
                    UpdateDrag(context);
                    break;
                case SubChew:
                    UpdateChew(context);
                    break;
                case SubSpit:
                    UpdateSpit(context);
                    break;
                case SubWhiff:
                    if (UpdateWhiff(context)) {
                        return new PlanteraCanopyState();
                    }
                    break;
                default:
                    if (UpdateRecover(context)) {
                        return new PlanteraCanopyState();
                    }
                    break;
            }

            return null;
        }

        /// <summary>子相位进入拍(各端一次)：一次性音效/权威侧一次性动作</summary>
        private void EnterSubPhase(PlanteraStateContext context, int sub) {
            NPC npc = context.Npc;
            switch (sub) {
                case SubLash:
                    //藤索出鞘：权威端生成，方向已在蓄势期锁定
                    if (!VaultUtils.isClient) {
                        PlanteraSnareVine.Spawn(npc, MawWorld(npc), aimDir * LashSpeed);
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item32 with { Volume = 1f, Pitch = -0.15f }, npc.Center);
                        SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 0.55f, Pitch = 0.5f }, npc.Center);
                        PlanteraRenderHelper.SpawnPetalBurst(MawWorld(npc), 6, 5f, true);
                    }
                    break;
                case SubDrag:
                    //缠中：救援阀基准记账
                    if (!VaultUtils.isClient) {
                        lifeAtGrab = npc.life;
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 1f, Pitch = -0.65f }, VictimCenterOrMaw(npc));
                        SoundEngine.PlaySound(SoundID.Grass with { Volume = 1f, Pitch = -0.3f }, VictimCenterOrMaw(npc));
                        PlanteraScreenFX.CameraPunch(VictimCenterOrMaw(npc), 5f, 12, "PlanteraFeastCatch");
                    }
                    break;
                case SubChew:
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Zombie32 with { Volume = 0.8f, Pitch = -0.35f, MaxInstances = 3 }, npc.Center);
                    }
                    break;
                case SubWhiff:
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = -0.55f }, npc.Center);
                    }
                    break;
            }
        }

        /// <summary>被抓者中心，无效回退巨口(音效/粒子定位用)</summary>
        private static Vector2 VictimCenterOrMaw(NPC boss) {
            int v = GrabVictim(boss);
            if (v >= 0 && v < Main.maxPlayers && Main.player[v].active) {
                return Main.player[v].Center;
            }
            return MawWorld(boss);
        }

        #region 各子相位逐帧
        /// <summary>缠卷蓄势：后仰拉弓+张力上行波，末段锁向+预警线</summary>
        private void UpdateCoil(PlanteraStateContext context) {
            NPC npc = context.Npc;
            float t = MathHelper.Clamp(subTimer / (float)CoilTime, 0f, 1f);

            npc.damage = 0;
            context.RotationMode = 2;
            context.SetChargeState(5, t);
            context.GlowPulse = 0.3f + t * 0.5f;

            //锁向前跟瞄，锁向后定格
            if (!aimLocked && context.Target.Alives()) {
                aimDir = (context.Target.Center + context.Target.velocity * 10f - npc.Center)
                    .SafeNormalize(Vector2.UnitY);
            }
            npc.rotation = npc.rotation.AngleLerp(aimDir.ToRotation() + MathHelper.PiOver2, 0.22f);

            //反向缩身蓄势，pow(t,5)末段猛吸
            float reel = (float)Math.Pow(t, 5) * 6.5f;
            npc.velocity = Vector2.Lerp(npc.velocity, -aimDir * (1f + reel), 0.22f);
            context.BodyScalePulse = -0.04f * t;

            //钩爪弦紧+吸入粒子
            if (!VaultUtils.isServer) {
                for (int i = 0; i < context.Hooks.Count && i < 2; i++) {
                    PlanteraVineRenderer.PushPulse(context.Hooks[i].whoAmI, 0.25f + t * 0.6f);
                }
                PlanteraRenderHelper.SpawnChargeIntake(context, t);
            }

            //锁定拍：预警线+咬合静默音
            if (subTimer == AimLockTick) {
                aimLocked = true;
                if (!VaultUtils.isClient) {
                    PlanteraTelegraphLine.Spawn(npc, npc.Center, aimDir.ToRotation(),
                        CoilTime - AimLockTick, LashRange + 90f);
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.25f, Volume = 0.75f }, npc.Center);
                }
            }

            if (subTimer >= CoilTime && !VaultUtils.isClient) {
                Advance(npc, SubLash);
            }
        }

        /// <summary>藤索飞行：本体后坐持稳；命中/超射程由藤索弹幕在服务端裁决</summary>
        private void UpdateLash(PlanteraStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.RotationMode = 2;
            npc.rotation = npc.rotation.AngleLerp(aimDir.ToRotation() + MathHelper.PiOver2, 0.15f);
            npc.velocity *= 0.88f;
            context.GlowPulse = 0.7f;

            //藤索丢失/超窗兜底(正常miss由弹幕写SubWhiff)
            if (subTimer > LashMaxTime + 8 && !VaultUtils.isClient) {
                Advance(npc, SubWhiff);
            }
        }

        /// <summary>拖拽：本体小幅迎身，荆棘刮擦节拍；结束时距离检查裁决入口/脱手</summary>
        private void UpdateDrag(PlanteraStateContext context) {
            NPC npc = context.Npc;
            int victim = GrabVictim(npc);

            //被抓者失效(死亡/掉线/传送豁免)→立即脱手收势
            if (!VaultUtils.isClient && (victim < 0 || !VictimEligible(Main.player[victim]))) {
                ReleaseVictim(npc);
                Advance(npc, SubRecover);
                return;
            }

            npc.damage = npc.defDamage;
            context.GlowPulse = 0.6f;
            context.RotationMode = 2;

            if (victim >= 0 && victim < Main.maxPlayers) {
                Player prey = Main.player[victim];
                npc.rotation = npc.rotation.AngleLerp(
                    (prey.Center - npc.Center).ToRotation() + MathHelper.PiOver2, 0.16f);
                //小幅迎身收线，与拖拽相向而行
                Vector2 to = prey.Center - npc.Center;
                npc.velocity = Vector2.Lerp(npc.velocity, to.SafeNormalize(Vector2.Zero) * 6.5f, 0.07f);

                //刮擦节拍的叶屑(伤害由被抓者客户端结算，这里只做各端可见的表现)
                if ((subTimer == ScrapeTickA || subTimer == ScrapeTickB) && !VaultUtils.isServer) {
                    PlanteraRenderHelper.SpawnAnchorImpact(prey.Center, to.SafeNormalize(Vector2.UnitY));
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = 0.15f, MaxInstances = 4 }, prey.Center);
                }
            }

            if (subTimer >= DragTime && !VaultUtils.isClient) {
                Player prey = victim >= 0 ? Main.player[victim] : null;
                //卡地形拖不到嘴边→公平放生，不硬拽穿墙
                if (prey == null || npc.Distance(prey.Center) > DragGiveUpDist) {
                    ReleaseVictim(npc);
                    Advance(npc, SubRecover);
                }
                else {
                    Advance(npc, SubChew);
                }
            }
        }

        /// <summary>咀嚼三拍：每拍张口蓄-咬合-余韵；第三拍咬合喷孢子雾</summary>
        private void UpdateChew(PlanteraStateContext context) {
            NPC npc = context.Npc;
            int victim = GrabVictim(npc);

            if (!VaultUtils.isClient && (victim < 0 || !VictimEligible(Main.player[victim]))) {
                ReleaseVictim(npc);
                Advance(npc, SubRecover);
                return;
            }

            //救援阀：队友集火本体到位就提前吐人
            if (!VaultUtils.isClient && lifeAtGrab > 0
                && lifeAtGrab - npc.life > npc.lifeMax * RescueDamageRatio) {
                Advance(npc, SubSpit);
                return;
            }

            npc.damage = npc.defDamage;
            context.RotationMode = 2;
            npc.velocity *= 0.8f;

            int beatTick = subTimer % BitePeriod;
            float beatT = beatTick / (float)BitePeriod;

            //拍内节奏：张口蓄势(鼓胀)→咬合(压缩)→余韵回摆
            if (beatTick < BiteSnapTick) {
                float open = beatTick / (float)BiteSnapTick;
                context.BodyScalePulse = 0.05f * open;
                context.GlowPulse = 0.45f + open * 0.45f;
                //咬合前4帧收声收粒子(临爆静默)
                if (!VaultUtils.isServer && beatTick < BiteSnapTick - 4 && Main.rand.NextBool(3)) {
                    PlanteraRenderHelper.SpawnChargeIntake(context, open * 0.7f);
                }
            }
            else {
                context.BodyScalePulse = MathHelper.Lerp(-0.07f, 0f,
                    (beatTick - BiteSnapTick) / (float)(BitePeriod - BiteSnapTick));
                context.GlowPulse = 0.5f;
            }

            //咬合帧：各端可见的爆点(伤害在被抓者客户端)
            if (beatTick == BiteSnapTick) {
                int biteIndex = subTimer / BitePeriod;
                if (!VaultUtils.isServer) {
                    Vector2 maw = MawWorld(npc);
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 1f, Pitch = -0.55f }, maw);
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.7f, Pitch = -0.5f }, maw);
                    PlanteraRenderHelper.SpawnPetalBurst(maw, 9 + biteIndex * 3, 5.5f + biteIndex, true);
                    PlanteraScreenFX.CameraPunch(maw, 4.5f + biteIndex * 1.5f, 10, "PlanteraFeastBite");
                }
                //第三口：孢子毒雾喷面
                if (biteIndex == 2) {
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), MawWorld(npc), Vector2.Zero,
                            ModContent.ProjectileType<PlanteraSporeCloud>(),
                            Math.Max(npc.defDamage / 3, 10), 0f, Main.myPlayer, 0.85f, 1f);
                    }
                    if (!VaultUtils.isServer) {
                        PlanteraRenderHelper.SpawnSporePuff(MawWorld(npc), 1.6f);
                    }
                }
            }

            if (subTimer >= ChewTime && !VaultUtils.isClient) {
                Advance(npc, SubSpit);
            }
        }

        /// <summary>吐飞：压缩静默→弹射帧连壳吐出+反冲</summary>
        private void UpdateSpit(PlanteraStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            context.RotationMode = 2;
            npc.velocity *= 0.85f;

            if (subTimer < SpitYeetTick) {
                //压缩静默：吸气缩身，一切粒子停
                float t = subTimer / (float)SpitYeetTick;
                context.BodyScalePulse = -0.1f * t;
                context.GlowPulse = MathHelper.Lerp(0.6f, 0.15f, t);
            }
            else if (subTimer == SpitYeetTick) {
                //弹射帧：壳瓣爆+反冲+背向种子屑(不追打被吐者)
                context.BodyScalePulse = 0.08f;
                context.GlowPulse = 1f;
                int victim = GrabVictim(npc);
                Vector2 spitDir = victim >= 0 && Main.player[victim].active
                    ? new Vector2(Math.Sign(Main.player[victim].Center.X - npc.Center.X), -1f).SafeNormalize(Vector2.UnitY)
                    : -MawDir(npc);
                npc.velocity = -spitDir * 7.5f;

                //注意：此处不清ai[1]——被抓者客户端节拍略滞后，
                //需保留标记直到进入收势段，其本地弹射帧才能可靠执行
                if (!VaultUtils.isClient) {
                    for (int i = -1; i <= 1; i += 2) {
                        Vector2 vel = (-spitDir).RotatedBy(i * 0.5f) * 15f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), MawWorld(npc), vel,
                            ModContent.ProjectileType<PlanteraSeed>(), PlanteraSeed.GetDamage(npc), 0f, Main.myPlayer);
                    }
                }
                if (!VaultUtils.isServer) {
                    Vector2 maw = MawWorld(npc);
                    SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 1f, Pitch = -0.2f }, maw);
                    SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.7f, Pitch = 0.1f }, maw);
                    PlanteraRenderHelper.SpawnPetalBurst(maw, 22, 9f, true);
                    PlanteraRenderHelper.SpawnSporePuff(maw, 1.2f);
                    PlanteraScreenFX.CameraPunch(maw, 8f, 16, "PlanteraFeastSpit", spitDir);
                }
            }
            else {
                context.GlowPulse = MathHelper.Lerp(1f, 0.4f, (subTimer - SpitYeetTick) / (float)(SpitTime - SpitYeetTick));
            }

            if (subTimer >= SpitTime && !VaultUtils.isClient) {
                Advance(npc, SubRecover);
            }
        }

        /// <summary>空挥软垂：藤索缩回，本体低垂硬直(惩罚窗)</summary>
        private bool UpdateWhiff(PlanteraStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.GlowPulse = 0.15f;
            context.RotationMode = 0;
            //松劲下沉
            context.SkipDefaultMovement = false;
            SetSuspension(context, new Vector2(0f, 70f), PlanteraDirector.DriftSpeedP1 * 0.5f, 0.03f);

            return subTimer >= WhiffTime && !VaultUtils.isClient;
        }

        /// <summary>收势回摆：回到悬吊呼吸</summary>
        private bool UpdateRecover(PlanteraStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.GlowPulse = 0.3f;
            context.RotationMode = 0;
            context.SkipDefaultMovement = false;
            SetSuspension(context, Vector2.Zero, PlanteraDirector.DriftSpeedP2 * 0.7f, 0.05f);

            return subTimer >= RecoverTime && !VaultUtils.isClient;
        }
        #endregion

        /// <summary>权威端推进子相位并广播</summary>
        private static void Advance(NPC npc, int sub) {
            npc.ai[0] = sub;
            npc.netUpdate = true;
        }

        /// <summary>权威端清空被抓者标记</summary>
        private static void ReleaseVictim(NPC npc) {
            npc.ai[1] = 0f;
            npc.netUpdate = true;
        }

        public override void OnExit(PlanteraStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            //任何出口(含死亡/大招打断)都清抓取标记+还钩爪+上冷却
            if (!VaultUtils.isClient) {
                npc.ai[0] = 0f;
                npc.ai[1] = 0f;
                npc.netUpdate = true;
                context.VineFeastCooldown = PlanteraDirector.FeastCooldownTicks;
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.Release(hook);
                }
            }
        }
    }
}
