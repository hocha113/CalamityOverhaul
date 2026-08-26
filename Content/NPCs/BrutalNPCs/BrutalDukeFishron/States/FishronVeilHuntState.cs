using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 雨隐猎杀（三阶段残血专属）：身体化进雨里彻底隐形，只剩一双电眼与雨中的凹痕。
    /// 每一击都先亮预告线（隐身的身体，可见的杀意），锁线即承诺；
    /// 突进期雨水裹身半显形，打完重新沉回雨幕。全程可被命中，眼睛就是弱点标记
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.VeilHunt, typeof(FishronStateContext))]
    internal class FishronVeilHuntState : FishronStateBase
    {
        public override string StateName => "VeilHunt";
        public override FishronStateIndex StateIndex => FishronStateIndex.VeilHunt;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        /// <summary>溶进雨里</summary>
        private const int FadeTime = 26;
        /// <summary>每轮潜行绕位</summary>
        private const int StalkTime = 36;
        /// <summary>隐身突击预告：比常规冲刺短，比风暴连突长</summary>
        internal const int StrikeTelegraph = 26;
        /// <summary>突进直线帧数</summary>
        private const int StrikeTime = 16;
        /// <summary>突进后回隐缓冲</summary>
        private const int SettleTime = 12;
        /// <summary>显形收势</summary>
        private const int RevealTime = 20;
        private const int StrikeLoops = 2;
        private const float StrikeSpeed = 56f;
        /// <summary>完全隐身阈值：alpha 高于此值时接触伤害关闭（公平阀）</summary>
        internal const int HiddenAlpha = 200;
        #endregion

        //相位 0溶隐 1潜行 2预告 3突进 4回隐 5显形收势
        private int phase;
        private int phaseStart;
        private int loopsDone;
        private int stalkSide;
        private Vector2 strikeDir;

        public FishronVeilHuntState() {
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            phase = 0;
            phaseStart = 0;
            loopsDone = 0;
            stalkSide = context.Npc.Center.X < context.Target.Center.X ? -1 : 1;
            //雨声被拉满，咆哮反而没有：安静本身是威胁
            SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.8f, Pitch = -0.35f, MaxInstances = 3 }, context.Npc.Center);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            int t = (int)Timer - phaseStart;

            //整段维持暴雨与半黑天：隐身依托的舞台
            context.StormBoost = 0.25f;
            FishronStormSky.PushRainBoost(0.45f);

            switch (phase) {
                case 0:
                    UpdateFade(context, npc, player, t);
                    break;
                case 1:
                    UpdateStalk(context, npc, player, t);
                    break;
                case 2:
                    UpdateTelegraph(context, npc, player, t);
                    break;
                case 3:
                    UpdateStrike(context, npc, player, t);
                    break;
                case 4:
                    UpdateSettle(context, npc, player, t);
                    break;
                default:
                    if (UpdateReveal(context, npc, t)) {
                        return new FishronHoverState();
                    }
                    break;
            }

            //公平阀：完全隐身期接触伤害关闭，不许无形撞人
            if (npc.alpha >= HiddenAlpha) {
                npc.damage = 0;
                npc.chaseable = false;
            }

            return null;
        }

        private void EnterPhase(int next) {
            phase = next;
            phaseStart = (int)Timer;
        }

        /// <summary>维持隐身：主控每帧衰减 12，这里必须压回去</summary>
        private static void HoldHidden(NPC npc) {
            npc.alpha = Math.Min(npc.alpha + 40, 255);
        }

        /// <summary>相位0：身体一段段化成雨，向上淋散</summary>
        private void UpdateFade(FishronStateContext context, NPC npc, Player player, int t) {
            HoldHidden(npc);
            npc.velocity *= 0.92f;
            FaceBody(npc, player.Center, 0.1f);
            context.FrameCommand = 1;

            if (!VaultUtils.isServer && t % 2 == 0) {
                //身上淌下的雨帘碎屑
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
                FishronMotionFX.SpawnMist(pos, new Vector2(0, Main.rand.NextFloat(0.5f, 1.5f)), 0.9f, 1);
            }

            if (t >= FadeTime) {
                EnterPhase(1);
            }
        }

        /// <summary>相位1：雨幕里绕到玩家侧后方压位，凹痕与眼芒是唯二踪迹</summary>
        private void UpdateStalk(FishronStateContext context, NPC npc, Player player, int t) {
            HoldHidden(npc);
            context.FrameCommand = 2;

            //侧后位：玩家背向侧上方
            int behind = player.direction != 0 ? -player.direction : stalkSide;
            Vector2 goal = player.Center + new Vector2(behind * 400f, -230f);
            Vector2 desired = (goal - npc.Center).SafeNormalize(Vector2.Zero) * 19f;
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.14f);
            FaceBody(npc, player.Center, 0.2f);

            //雨的凹痕：他所在处雨点被撞碎（本地视觉，追猎公平线索之一）
            if (!VaultUtils.isServer && t % 5 == 0) {
                FishronMotionFX.SpawnMist(npc.Center + Main.rand.NextVector2Circular(40f, 30f), Vector2.Zero, 0.7f, 1);
            }

            bool positioned = npc.WithinRange(goal, 150f) && t > 12;
            if (t >= StalkTime || positioned) {
                EnterPhase(2);
                strikeDir = Vector2.Zero;
                //预告线锚在隐身的身体上（服务端生成）
                if (!VaultUtils.isClient) {
                    Terraria.Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        (player.Center - npc.Center).SafeNormalize(Vector2.UnitX),
                        ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, player.whoAmI, FishronTelegraph.PackParams(0, StrikeTelegraph));
                }
                SoundEngine.PlaySound(SoundID.NPCHit14 with { Volume = 0.6f, Pitch = -0.55f, MaxInstances = 3 }, npc.Center);
            }
        }

        /// <summary>相位2：隐身蓄势，预告线追瞄至锁定帧冻结</summary>
        private void UpdateTelegraph(FishronStateContext context, NPC npc, Player player, int t) {
            HoldHidden(npc);
            float progress = Math.Min(t / (float)StrikeTelegraph, 1f);

            //锁定帧冻结方向：与预告线的 LockTime 同拍，锁线即承诺
            if (t < StrikeTelegraph - FishronTelegraph.LockTime || strikeDir == Vector2.Zero) {
                strikeDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            }
            context.SetChargeState(1, progress);
            context.DashDirection = strikeDir;
            context.FrameCommand = 1;

            //迟滞后撤蓄势
            float reel = (float)Math.Pow(progress, 8) * 18f;
            npc.velocity = Vector2.Lerp(npc.velocity, -strikeDir * (1f + reel), 0.24f);
            FaceBody(npc, npc.Center + strikeDir * 100f, 0.3f);

            if (t >= StrikeTelegraph) {
                EnterPhase(3);
                //一帧写满：雨水裹身半显形，冲线
                npc.velocity = strikeDir * StrikeSpeed;
                npc.alpha = 90;
                npc.damage = npc.defDamage;
                npc.netUpdate = true;
                FishronMotionFX.SpawnDashBurst(npc.Center, strikeDir, 1.05f);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.9f, Pitch = 0.35f, MaxInstances = 3 }, npc.Center);
            }
        }

        /// <summary>相位3：半显形直线突进，近零转向</summary>
        private void UpdateStrike(FishronStateContext context, NPC npc, Player player, int t) {
            //突进期不回隐：雨衣半透
            npc.alpha = Math.Max(npc.alpha, 90);
            AimBodyAlongVelocity(npc);
            context.FrameCommand = 2;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                FishronMotionFX.SpawnSprayCone(npc.Center, -npc.velocity.SafeNormalize(Vector2.UnitY), 1, 3f, 8f, 0.5f, 0.85f);
            }

            if (t >= StrikeTime) {
                EnterPhase(4);
            }
        }

        /// <summary>相位4：拖刹回隐，或耗尽轮次转显形</summary>
        private void UpdateSettle(FishronStateContext context, NPC npc, Player player, int t) {
            HoldHidden(npc);
            npc.velocity *= 0.86f;
            AimBodyAlongVelocity(npc);

            if (t >= SettleTime) {
                loopsDone++;
                if (loopsDone < StrikeLoops) {
                    stalkSide = -stalkSide;
                    EnterPhase(1);
                }
                else {
                    EnterPhase(5);
                    FishronStormSky.PushFlash(0.45f, npc.Center);
                    FishronMotionFX.SpawnSplashBurst(npc.Center, 1.4f, playSound: false);
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1f, Pitch = -0.15f, MaxInstances = 3 }, npc.Center);
                }
            }
        }

        /// <summary>相位5：从雨里重新凝出身体，收势交还悬停</summary>
        private bool UpdateReveal(FishronStateContext context, NPC npc, int t) {
            //不再补 alpha，主控逐帧衰减即显形
            npc.velocity *= 0.93f;
            context.FrameCommand = 1;
            if (!VaultUtils.isServer && t % 3 == 0) {
                FishronMotionFX.SpawnSprayCone(npc.Center + Main.rand.NextVector2Circular(40f, 30f),
                    Vector2.UnitY, 1, 1f, 3f, 0.4f, 0.7f);
            }
            return t >= RevealTime;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.alpha = 0;
            context.Npc.chaseable = true;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
