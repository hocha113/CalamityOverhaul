using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>
    /// 水晶囚舞(投技)：御晶吊灯压中玩家→封入棱晶茧→皇后携茧华尔兹对旋三踢→终结贯茧旋踢掷飞。
    /// 相位走 npc.ai[0]，相位内时钟走 npc.ai[1](各端本地自增+SyncNPC校正)，被抓者 npc.ai[3]=whoAmI+1。
    /// 服务端只判定，被抓玩家位移由其本人客户端 QueenSlimePerformancePlayer 施加。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.CrystalPrisonWaltz, typeof(QueenSlimeStateContext))]
    internal class QueenCrystalPrisonWaltzState : QueenSlimeStateBase
    {
        public override string StateName => "CrystalPrisonWaltz";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.CrystalPrisonWaltz;

        #region 节奏与参数常量
        //相位值(ai[0])
        internal const int PhaseSummon = 0;
        internal const int PhaseWaltz = 2;
        internal const int PhaseRecover = 3;

        //召唤段：吊灯在 t=24 挂出，自带物化30+跟随44+锁闪14+坠落的完整前摇
        private const int ChandelierSpawnTick = 24;
        private const int SummonMaxTime = 300;

        //囚舞段(catch 后 ai[1] 时间线)
        internal const int CocoonTime = 16;
        internal static readonly int[] KickTicks = [52, 96, 140];
        internal const int KickWindup = 14;
        internal const int KickSnap = 4;
        internal const int FinisherChargeTick = 158;
        internal const int FinisherTick = 176;
        internal const int WaltzEndTick = 190;
        private const int RecoverTime = 42;
        //整状态保底超时
        private const int HardTimeout = 640;

        //连段伤害基础值(经 GetAttackDamage_ScaledByStrength 缩放；受害者端有免死阀)
        internal const int KickDamageBase = 30;
        internal const int FinisherDamageBase = 55;

        //舞轨半径
        private const float QueenOrbitR = 150f;
        private const float PrisonOrbitR = 118f;
        //异常释放：被抓者离开茧位过远(被外力传送)
        private const float BreakDistance = 380f;
        //释放后冷却
        private const int CooldownAfter = 1500;
        private const int CooldownAfterAsuraMode = 1140;

        //御晶吊灯金色相种子(PrismHue 金段)
        internal const float RoyalHue = 0.72f;
        #endregion

        /// <summary>服务端持有的吊灯槽位，客户端恒-1</summary>
        private int chandelierIndex = -1;
        /// <summary>本端演出节拍高水位，防时钟回卷重放</summary>
        private int lastVfxBeat = -1;

        #region 同步字段读写
        private static int Phase(NPC npc) => (int)npc.ai[0];
        /// <summary>相位内时钟(各端本地自增)</summary>
        internal static int GrabTick(NPC npc) => (int)npc.ai[1];
        /// <summary>被抓玩家下标，-1=无</summary>
        internal static int VictimIndex(NPC npc) => (int)npc.ai[3] - 1;
        /// <summary>投技是否正抓着指定玩家(囚舞相位)</summary>
        internal static bool IsGrabbing(NPC npc, int playerIndex)
            => (int)npc.ai[2] == (int)QueenSlimeStateIndex.CrystalPrisonWaltz
            && Phase(npc) == PhaseWaltz && VictimIndex(npc) == playerIndex;
        #endregion

        #region 舞轨公式(纯时间函数，各端一致)
        /// <summary>舞心，晶茧出生包携带</summary>
        internal static Vector2 WaltzCenter(Projectile prison) => new(prison.ai[1], prison.ai[2]);

        /// <summary>对旋角：三拍渐快，终结前收速凝滞</summary>
        internal static float OrbitTheta(int t) {
            const float ThetaStart = -MathHelper.PiOver2;
            const float W0 = 0.028f;
            const float W1 = 0.052f;
            if (t <= FinisherChargeTick) {
                //角速 W0→W1 线性爬升的闭式积分
                return ThetaStart + W0 * t + (W1 - W0) / (2f * FinisherChargeTick) * t * t;
            }
            float baseTheta = OrbitTheta(FinisherChargeTick);
            float dt = Math.Min(t - FinisherChargeTick, FinisherTick - FinisherChargeTick);
            //凝滞：角速 W1→0 线性衰减
            return baseTheta + W1 * dt - W1 * dt * dt / (2f * (FinisherTick - FinisherChargeTick));
        }

        /// <summary>共同沉浮</summary>
        private static float OrbitBob(int t) => (float)Math.Sin(OrbitTheta(t) * 2f) * 14f;

        /// <summary>晶茧轨道位(踢击受击外摆+终结前拉近)</summary>
        internal static Vector2 PrisonSocket(Vector2 center, int t) {
            float r = PrisonOrbitR;
            foreach (int k in KickTicks) {
                if (t >= k && t <= k + 10) {
                    r += 30f * QueenMotion.Bump((t - k) / 10f);
                }
            }
            if (t > FinisherChargeTick) {
                float p = MathHelper.Clamp((t - FinisherChargeTick) / (float)(FinisherTick - FinisherChargeTick), 0f, 1f);
                r = MathHelper.Lerp(r, 96f, p);
            }
            return center + OrbitTheta(t).ToRotationVector2() * r + new Vector2(0f, OrbitBob(t));
        }

        /// <summary>皇后轨道位(对位差半圈；踢前外倾、踢中内切、终结外拉蓄势)</summary>
        internal static Vector2 QueenSocket(Vector2 center, int t) {
            float r = QueenOrbitR;
            foreach (int k in KickTicks) {
                if (t >= k - KickWindup && t < k) {
                    //外倾蓄力，末段猛收
                    r += 26f * QueenMotion.LateSnap((t - (k - KickWindup)) / (float)KickWindup, 3);
                }
                else if (t >= k && t <= k + KickSnap) {
                    //内切踢击，高次幂到位
                    r = MathHelper.Lerp(176f, 96f, QueenMotion.SnapOut((t - k) / (float)KickSnap, 8));
                }
                else if (t > k + KickSnap && t <= k + 18) {
                    //收腿回位
                    r = MathHelper.Lerp(96f, QueenOrbitR, (t - k - KickSnap) / 14f);
                }
            }
            if (t > FinisherChargeTick && t < FinisherTick) {
                float p = (t - FinisherChargeTick) / (float)(FinisherTick - FinisherChargeTick);
                r = MathHelper.Lerp(r, 196f, QueenMotion.LateSnap(p, 2));
            }
            return center + (OrbitTheta(t) + MathHelper.Pi).ToRotationVector2() * r + new Vector2(0f, OrbitBob(t));
        }

        /// <summary>找本皇后麾下的晶茧弹幕</summary>
        internal static Projectile FindPrison(NPC queen) {
            int type = ModContent.ProjectileType<QueenCrystalPrisonProj>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == type && (int)p.ai[0] == queen.whoAmI) {
                    return p;
                }
            }
            return null;
        }
        #endregion

        #region 触发闸门(服务端)
        /// <summary>投技触发判定：二阶段+冷却完+目标有效+非时停+未临死</summary>
        internal static bool CanTrigger(QueenSlimeStateContext ctx) {
            if (VaultUtils.isClient || ctx == null) {
                return false;
            }
            if (!ctx.Phase2Unfolded || ctx.GrabCooldown > 0) {
                return false;
            }
            if (!ctx.Target.Alives() || ctx.Npc.Distance(ctx.Target.Center) > 1500f) {
                return false;
            }
            //临死(≤1/12血)不再起舞，留给死亡演出
            if (ctx.Npc.life * 12 <= ctx.Npc.lifeMax) {
                return false;
            }
            if (WorldFreezeSystem.IsActive) {
                return false;
            }
            return true;
        }
        #endregion

        public QueenCrystalPrisonWaltzState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            chandelierIndex = -1;
            lastVfxBeat = -1;
            if (!VaultUtils.isClient) {
                npc.ai[0] = PhaseSummon;
                npc.ai[1] = 0f;
                npc.ai[3] = 0f;
                npc.netUpdate = true;
            }
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;

            Timer++;
            DisableContactDamage(npc);
            //相位内时钟各端本地自增，服务端周期 SyncNPC 校正
            npc.ai[1] += 1f;

            IQueenSlimeState next = Phase(npc) switch {
                PhaseWaltz => UpdateWaltz(context),
                PhaseRecover => UpdateRecover(context),
                _ => UpdateSummon(context),
            };

            //保底超时：无论卡在哪一相位都强制收场
            if (Timer > HardTimeout && !VaultUtils.isClient) {
                ReleaseVictim(context);
                return new QueenAerialBalletState();
            }
            return next;
        }

        #region 召唤段
        private IQueenSlimeState UpdateSummon(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int t = GrabTick(npc);

            //侧上位悬停，注视目标
            int side = npc.Center.X < player.Center.X ? -1 : 1;
            Vector2 anchor = player.Center + new Vector2(side * 300f, -330f);
            QueenMotion.SpringHover(npc, anchor, 0.018f, 0.11f, 22f);
            QueenMotion.FlightLean(npc);
            context.PoseCommand = 5;
            FaceTarget(npc, player.Center);

            //王冠蓄能(专属前摇视觉)
            context.SetChargeState(1, MathHelper.Clamp(t / 90f, 0f, 1f));
            context.PrismShimmer = Math.Max(context.PrismShimmer, 0.5f);
            if (!VaultUtils.isServer && t > 8 && t % 3 == 0) {
                QueenMotion.ChargeGatherFX(QueenSlimeRenderHelper.CrownAnchor(npc), t / 90f, 130f, RoyalHue);
            }

            //专属和弦前摇音
            if (t == 2) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1f, Pitch = -0.35f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = 0.5f }, npc.Center);
            }

            //挂出御晶吊灯(服务端)
            if (t == ChandelierSpawnTick && !VaultUtils.isClient) {
                Vector2 pos = player.Center + new Vector2(0f, -330f);
                chandelierIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<QueenRoyalChandelierProj>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI, 0f, RoyalHue);
            }

            //服务端逐帧监视吊灯：坠落段压中任意玩家→抓取；吊灯落空死亡→收势
            if (!VaultUtils.isClient && t > ChandelierSpawnTick) {
                Projectile chandelier = ResolveChandelier();
                if (chandelier != null) {
                    if (chandelier.ModProjectile is QueenRoyalChandelierProj royal && royal.IsFalling) {
                        Rectangle press = chandelier.Hitbox;
                        press.Inflate(10, 10);
                        foreach (var candidate in Main.ActivePlayers) {
                            if (!candidate.Alives() || candidate.ghost || !press.Intersects(candidate.Hitbox)) {
                                continue;
                            }
                            Catch(context, candidate, chandelier);
                            return null;
                        }
                    }
                }
                else {
                    //吊灯已消亡(落地/被清)→未压中，进恢复拍
                    BeginRecover(npc);
                }
            }

            //召唤段保底
            if (t > SummonMaxTime && !VaultUtils.isClient) {
                BeginRecover(npc);
            }
            return null;
        }

        /// <summary>按槽位取回吊灯，验证类型与属主</summary>
        private Projectile ResolveChandelier() {
            if (chandelierIndex < 0 || chandelierIndex >= Main.maxProjectiles) {
                return null;
            }
            Projectile p = Main.projectile[chandelierIndex];
            if (!p.active || p.type != ModContent.ProjectileType<QueenRoyalChandelierProj>()) {
                return null;
            }
            return p;
        }

        /// <summary>抓取成立(服务端)：写同步字段、收编吊灯、生成晶茧</summary>
        private void Catch(QueenSlimeStateContext context, Player victim, Projectile chandelier) {
            NPC npc = context.Npc;

            //吊灯标记为"被收编"后消亡，OnKill 走成茧闪光而非落地碎裂；
            //击杀包不携带 ai，Kill 前显式同步一次钉住包序，客户端才能读到收编标记
            chandelier.ai[1] = 1f;
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, chandelier.whoAmI);
            }
            chandelier.Kill();

            //舞心：压点上方一点，并向地面上方钳制，防低轨拖玩家进地
            Vector2 center = victim.Center + new Vector2(0f, -40f);
            float groundY = QueenMotion.FindGroundBelow(victim.Center).Y;
            center.Y = Math.Min(center.Y, groundY - 200f);

            npc.ai[0] = PhaseWaltz;
            npc.ai[1] = 0f;
            npc.ai[3] = victim.whoAmI + 1;
            npc.netUpdate = true;

            Projectile.NewProjectile(npc.GetSource_FromAI(), victim.Center, Vector2.Zero,
                ModContent.ProjectileType<QueenCrystalPrisonProj>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, center.X, center.Y);
        }
        #endregion

        #region 囚舞段
        private IQueenSlimeState UpdateWaltz(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            int t = GrabTick(npc);
            int victimIndex = VictimIndex(npc);
            Projectile prison = FindPrison(npc);

            //服务端逐帧验人：死亡/掉线/幽灵/被外力传走 → 提前释放
            if (!VaultUtils.isClient) {
                bool victimInvalid = victimIndex < 0 || victimIndex >= Main.maxPlayers
                    || !Main.player[victimIndex].Alives() || Main.player[victimIndex].ghost;
                bool teleportedAway = !victimInvalid && prison != null
                    && Main.player[victimIndex].Distance(prison.Center) > BreakDistance;
                if (victimInvalid || teleportedAway || prison == null) {
                    ReleaseVictim(context);
                    BeginRecover(npc);
                    return null;
                }
            }

            Vector2 center = prison != null ? WaltzCenter(prison) : npc.Center;

            //皇后运动
            if (t < CocoonTime) {
                //急冲入位：一帧发力的舞伴就位
                Vector2 socket = QueenSocket(center, t);
                npc.velocity = (socket - npc.Center) * 0.34f;
                if (t == 1) {
                    context.PushSquash(0.55f);
                    context.AfterimageBoost = 1f;
                }
            }
            else if (t < FinisherTick) {
                //比例控制器贴轨对旋，速度留给倾斜与翼拍读数
                Vector2 socket = QueenSocket(center, t);
                Vector2 toSocket = socket - npc.Center;
                npc.velocity = toSocket.Length() > 40f
                    ? toSocket.SafeNormalize(Vector2.Zero) * 40f
                    : toSocket * 0.55f;
                QueenMotion.FlightLean(npc, 0.07f, 0.55f);
            }
            else if (t == FinisherTick) {
                //贯茧旋踢：穿过舞心的一帧发力
                Vector2 through = (center - npc.Center).SafeNormalize(Vector2.UnitX) * 26f;
                npc.velocity = through;
                context.PushSquash(0.65f);
                context.AfterimageBoost = 1f;
            }
            else {
                //反冲刹车余韵
                npc.velocity *= 0.82f;
                context.PoseCommand = 3;
            }

            //姿态：踢击瞬间用升姿卖踢腿，其余飞行巡航
            bool kicking = false;
            foreach (int k in KickTicks) {
                if (t >= k && t <= k + KickSnap + 2) {
                    kicking = true;
                    break;
                }
            }
            if (t < FinisherTick) {
                context.PoseCommand = kicking ? 1 : 5;
            }
            if (prison != null) {
                FaceTarget(npc, prison.Center);
            }

            //全程虹彩与翼拍
            context.PrismShimmer = 1f;
            context.WingFlapBoost = MathHelper.Clamp(npc.velocity.Length() / 14f, 0.4f, 1.5f);
            if (t > FinisherChargeTick && t < FinisherTick) {
                context.SetChargeState(3, (t - FinisherChargeTick) / (float)(FinisherTick - FinisherChargeTick));
            }

            //本端演出节拍(高水位防重放)
            FireWaltzBeats(context, prison, t);

            //服务端推进：终结拍杀茧放人，余韵尽头转恢复
            if (!VaultUtils.isClient) {
                if (t == FinisherTick && prison != null) {
                    prison.Kill();
                    npc.ai[3] = 0f;
                    npc.netUpdate = true;
                }
                if (t >= WaltzEndTick) {
                    BeginRecover(npc);
                }
            }
            return null;
        }

        /// <summary>囚舞演出节拍：成茧/三踢/静默蓄力/终结，各端本地触发</summary>
        private void FireWaltzBeats(QueenSlimeStateContext context, Projectile prison, int t) {
            NPC npc = context.Npc;
            Vector2 prisonPos = prison?.Center ?? npc.Center;

            //beat 0 成茧
            if (t >= 2 && lastVfxBeat < 0) {
                lastVfxBeat = 0;
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.9f, Pitch = 0.15f }, prisonPos);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = -0.5f }, prisonPos);
                if (!VaultUtils.isServer) {
                    QueenMotion.CrystalShatterBurst(prisonPos, 0.8f, RoyalHue, playSound: false);
                    QueenMotion.Shake(prisonPos, 4f, 12, "QueenGrabCocoon");
                }
            }

            //beat 1..3 三踢
            for (int i = 0; i < KickTicks.Length; i++) {
                int beatId = i + 1;
                if (t >= KickTicks[i] && lastVfxBeat < beatId) {
                    lastVfxBeat = beatId;
                    context.PushSquash(0.5f);
                    SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.85f, Pitch = 0.15f + i * 0.15f, MaxInstances = 3 }, prisonPos);
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 3 }, prisonPos);
                    if (!VaultUtils.isServer) {
                        QueenMotion.CrystalShatterBurst(prisonPos, 0.9f, RoyalHue + i * 0.08f, playSound: false);
                        PRTLoader.NewParticle<PRT_DWave>(prisonPos, Vector2.Zero,
                            QueenMotion.PrismHue(RoyalHue + i * 0.1f) * 0.85f, 0.28f)?
                            .Configure(new Vector2(1f, 1f), 0f, 1.2f, 16);
                        QueenMotion.Shake(prisonPos, 3f, 10, "QueenGrabKick");
                    }
                }
            }

            //beat 4 终结蓄力(静默拍：只有一声渐起，粒子收声)
            if (t >= FinisherChargeTick && lastVfxBeat < 4) {
                lastVfxBeat = 4;
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.75f }, npc.Center);
            }

            //beat 5 终结旋踢(碎茧主爆由晶茧 OnKill 演出，这里补皇后侧反冲)
            if (t >= FinisherTick && lastVfxBeat < 5) {
                lastVfxBeat = 5;
                SoundEngine.PlaySound(SoundID.Item167 with { Volume = 1f, Pitch = -0.05f }, prisonPos);
                if (!VaultUtils.isServer) {
                    QueenMotion.Shake(prisonPos, 6f, 16, "QueenGrabFinisher");
                }
            }
        }
        #endregion

        #region 恢复段
        private IQueenSlimeState UpdateRecover(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            int t = GrabTick(npc);

            //缓浮+屈膝礼收势，无接触伤；侧倾归零
            npc.velocity *= 0.92f;
            npc.velocity.Y -= 0.02f;
            npc.rotation *= 0.85f;
            context.PoseCommand = t < 24 ? 3 : 5;
            context.ResetChargeState();

            if (t >= RecoverTime && !VaultUtils.isClient) {
                return new QueenAerialBalletState();
            }
            return null;
        }

        /// <summary>切恢复相位(服务端)</summary>
        private static void BeginRecover(NPC npc) {
            npc.ai[0] = PhaseRecover;
            npc.ai[1] = 0f;
            npc.ai[3] = 0f;
            npc.netUpdate = true;
        }

        /// <summary>异常提前释放(服务端)：杀茧清目标，受害者端自会解锁</summary>
        private static void ReleaseVictim(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Projectile prison = FindPrison(npc);
            prison?.Kill();
            npc.ai[3] = 0f;
            npc.netUpdate = true;
        }
        #endregion

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.rotation = 0f;
            npc.noGravity = true;
            npc.noTileCollide = true;

            //被打断(大招/死亡/撤离)也必须放人清场
            if (!VaultUtils.isClient) {
                ReleaseVictim(context);
                Projectile chandelier = ResolveChandelier();
                chandelier?.Kill();
                npc.ai[0] = 0f;
                npc.ai[1] = 0f;
                context.GrabCooldown = context.IsAsuraMode ? CooldownAfterAsuraMode : CooldownAfter;
            }
        }
    }
}
