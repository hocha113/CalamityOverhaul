using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh
{
    /// <summary>
    /// 血肉墙主控(口部)。推进死线与器官协同：
    /// 复用原版 wofNPCIndex/wofDrawArea 契约(墙体绘制/舌头惩罚/越狱语义全保留)，
    /// 接管推进速度、全部攻击编排与部件指挥。状态索引写 npc.ai[2]，阶段写 npc.ai[1]
    /// </summary>
    internal class WallOfFleshAI : CWRNPCOverride, ICWRLoader, ILocalizedModType
    {
        #region 数据与资源
        public override int TargetID => NPCID.WallofFlesh;

        public string LocalizationCategory => "BrutalNPCs";
        public static LocalizedText CurtainDeathReason { get; private set; }
        public static LocalizedText NetDeathReason { get; private set; }

        /// <summary>屏幕滤镜名</summary>
        internal const string FilterName = "CWRWofBloodline";

        private VaultStateMachine<WofStateContext> stateMachine;
        private WofStateContext stateContext;
        private Player targetPlayer;
        /// <summary>咀嚼帧计数(本地动画)</summary>
        private float chewCounter;
        /// <summary>环境低吼计时(本地)</summary>
        private int roarTimer;
        /// <summary>滤镜当前强度(本地渐变)</summary>
        private static float filterIntensity;
        #endregion

        #region 加载
        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //血线滤镜：大迁徙/死亡演出的全屏压迫感，客户端由状态观察驱动
            Filters.Scene[FilterName] = new Filter(
                new ScreenShaderData("FilterMiniTower").UseColor(0.42f, 0.02f, 0.05f).UseOpacity(0f),
                EffectPriority.High);
        }

        void ICWRLoader.UnLoadData() {
            filterIntensity = 0f;
        }

        /// <summary>强制关闭血线滤镜(墙移除前由终态调用，防止残留染色)</summary>
        internal static void ShutdownFilter() {
            if (Main.dedServ) {
                return;
            }
            filterIntensity = 0f;
            if (Filters.Scene[FilterName] != null && Filters.Scene[FilterName].IsActive()) {
                Filters.Scene.Deactivate(FilterName);
            }
        }

        public override void SetStaticDefaults() {
            CurtainDeathReason = this.GetLocalization(nameof(CurtainDeathReason),
                () => "{0}被血潮吞没了");
            NetDeathReason = this.GetLocalization(nameof(NetDeathReason),
                () => "{0}被饥饿者的肉网绞碎了");
        }

        public override bool? CanCWROverride() {
            return null;
        }
        #endregion

        #region 初始化
        public override void SetProperty() {
            //血幕/覆膜延伸到口器屏外很远处也能绘制：原版已将 113/114/115 列入 MustAlwaysDraw，无需额外处理
            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new WofStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<WofStateContext>(stateContext, aiSlot: 2);

            //客户端从ai[2]恢复状态(中途加入)
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<WofStateContext> syncedState = VaultStateRegistry<WofStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new WofIntroState());
            }
            else {
                stateMachine.SetInitialState(new WofIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            //原版语义：到达世界边缘直接消失
            if (npc.position.X < 160f || npc.position.X > (Main.maxTilesX - 10) * 16) {
                ShutdownFilter();
                npc.active = false;
                return false;
            }

            //首帧重置墙域(原版语义)
            if (npc.localAI[0] == 0f) {
                npc.localAI[0] = 1f;
                Main.wofDrawAreaBottom = -1;
                Main.wofDrawAreaTop = -1;
            }

            FindTarget();
            UpdateStateContext();
            EvaluateGlobalTransitions();

            //每帧重声明，状态内覆盖
            npc.damage = npc.defDamage;
            npc.dontTakeDamage = false;
            npc.netOffset = Vector2.Zero;
            stateContext.AdvanceFactor = 1f;
            stateContext.SpeedOverride = -1f;
            stateContext.SuppressYAnchor = false;
            stateContext.MouthCommand = 0;
            WofWallField.CinematicAreaLock = 0;

            stateMachine?.Update();

            //维护原版契约：wofNPCIndex + 墙域扫描 + Y锚定
            WofWallField.MaintainWallArea(npc);
            if (!stateContext.SuppressYAnchor) {
                float anchorY = WofWallField.MiddleY - npc.height / 2;
                npc.velocity.Y = 0f;
                npc.position.Y = anchorY;
            }

            //推进(死线本体)
            UpdateAdvance();
            UpdateFarEnrage();
            UpdateMouthRotation();

            //环境音与常态渗血
            UpdateAmbience();

            //部件视觉通道
            float baseFlush = stateContext.WallFlush;
            float eyeCharge = stateContext.ChargeType == 4 ? stateContext.ChargeProgress : 0f;
            WofWallField.PushVisual(npc.whoAmI, baseFlush, eyeCharge);

            //滤镜强度(纯客户端表现)
            UpdateScreenFilter();

            //周期性同步
            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= Main.maxPlayers || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            //优先咬住已被恐惧的玩家(原版语义)
            if (Main.player[npc.target].dead || !Main.player[npc.target].gross) {
                npc.TargetClosest_WOF();
            }
            targetPlayer = Main.player[npc.target];

            //全灭进入撤离(服务端决策)
            if (!VaultUtils.isClient && !targetPlayer.Alives()
                && stateMachine?.CurrentState is not WofDespawnState and not WofDeathState) {
                stateMachine?.ChangeState(new WofDespawnState());
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            stateContext.MasterMode = Main.masterMode;
            stateContext.Phase = (int)npc.ai[1] > 0 ? (int)npc.ai[1] : 1;

            //常态潮红：随失血加深的心跳
            float lifeRatio = MathHelper.Clamp(npc.life / (float)npc.lifeMax, 0f, 1f);
            float heartRate = MathHelper.Lerp(2.2f, 5.4f, 1f - lifeRatio);
            float heartbeat = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * heartRate);
            stateContext.WallFlush = MathHelper.Clamp((0.16f + (1f - lifeRatio) * 0.34f) * (0.6f + 0.4f * heartbeat), 0f, 1f);
        }

        /// <summary>全局转移，服务端驱动，优先级：死亡>转阶段>大迁徙</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient || stateMachine?.CurrentState == null) {
                return;
            }

            IVaultState<WofStateContext> current = stateMachine.CurrentState;

            //死亡演出(任何状态可切，除已在死亡/撤离)
            if (npc.life <= WofDirector.DeathTriggerLife && !stateContext.DeathPerformanceFinished
                && current is not WofDeathState and not WofDespawnState and not WofIntroState) {
                stateMachine.ChangeState(new WofDeathState());
                return;
            }
            if (current is not WofAdvanceState) {
                return;
            }

            float lifeRatio = npc.life / (float)npc.lifeMax;
            int phase = (int)npc.ai[1];

            //转阶段演出(66%，仅从推进态干净切入；阶段位写ai[1]同步、断线重入安全)
            if (phase < 2 && lifeRatio <= WofDirector.Phase2LifeRatio) {
                stateMachine.ChangeState(new WofPhaseTransitionState());
                return;
            }

            //低血大招(33%，一次性，结束后ai[1]=3)
            if (phase == 2 && lifeRatio <= WofDirector.ExodusLifeRatio) {
                stateMachine.ChangeState(new WofCrimsonExodusState());
            }
        }

        /// <summary>
        /// 推进：死线本体。基础曲线随失血提速；远拉追赶、贴脸缓和；
        /// 状态可乘系数或直接覆盖速度；方向一经锁定永不回头(原版语义)
        /// </summary>
        private void UpdateAdvance() {
            //初始方向锁定：只在从未定向时执行一次(镜像原版 velocity.X==0 分支)；
            //演出把速度压到0不会触发重新定向——方向一经锁定永不回头
            if (npc.velocity.X == 0f && npc.direction == 0) {
                npc.TargetClosest();
                if (Main.player[npc.target].dead) {
                    float bestDist = float.PositiveInfinity;
                    int dir = 1;
                    foreach (var player in Main.ActivePlayers) {
                        float d = npc.Distance(player.Center);
                        if (d < bestDist) {
                            bestDist = d;
                            dir = npc.Center.X < player.Center.X ? 1 : -1;
                        }
                    }
                    npc.direction = dir;
                }
                else {
                    npc.direction = npc.Center.X < Main.player[npc.target].Center.X ? 1 : -1;
                }
                npc.velocity.X = npc.direction != 0 ? npc.direction : 1;
            }

            float lifeRatio = MathHelper.Clamp(npc.life / (float)npc.lifeMax, 0f, 1f);
            float speed;

            if (stateContext.SpeedOverride >= 0f) {
                speed = stateContext.SpeedOverride;
            }
            else {
                speed = WofDirector.BaseAdvanceSpeed + WofDirector.LifeAdvanceBonus * (1f - lifeRatio);
                if (Main.expertMode) {
                    speed *= 1.18f;
                }
                if (stateContext.IsDeathMode) {
                    speed *= 1.12f;
                }
                if (stateContext.Phase >= 3) {
                    speed += 0.7f;
                }

                //距离调制：领先太多追赶，贴脸缓和
                if (targetPlayer.Alives()) {
                    float lead = (targetPlayer.Center.X - npc.Center.X) * npc.direction;
                    if (lead > WofDirector.CatchUpDistance) {
                        speed += (lead - WofDirector.CatchUpDistance) * WofDirector.CatchUpPerPixel * speed;
                    }
                    else if (lead > 0f && lead < WofDirector.CloseEaseDistance) {
                        float ease = MathHelper.Lerp(WofDirector.CloseEaseFloor, 1f,
                            lead / WofDirector.CloseEaseDistance);
                        speed *= ease;
                    }
                }

                if (stateContext.FarEnraged) {
                    speed *= WofDirector.FarEnrageMultiplier;
                }
                speed *= stateContext.AdvanceFactor;
            }

            //方向源：速度符号优先，速度被演出压到0时沿用已锁方向
            int moveDir = npc.velocity.X != 0f
                ? Math.Sign(npc.velocity.X)
                : (npc.direction != 0 ? npc.direction : 1);
            npc.velocity.X = moveDir * speed;
            npc.direction = npc.spriteDirection = moveDir;
        }

        /// <summary>反风筝阀：目标长期领先过远则激怒狂奔，追近解除</summary>
        private void UpdateFarEnrage() {
            if (!targetPlayer.Alives()) {
                stateContext.FarTimer = 0;
                stateContext.FarEnraged = false;
                return;
            }
            float lead = (targetPlayer.Center.X - npc.Center.X) * npc.direction;

            if (stateContext.FarEnraged) {
                if (lead < WofDirector.CloseEaseDistance) {
                    stateContext.FarEnraged = false;
                    stateContext.FarTimer = 0;
                }
                return;
            }

            if (lead > WofDirector.FarEnrageDistance) {
                stateContext.FarTimer++;
                if (stateContext.FarTimer >= WofDirector.FarEnrageFrames) {
                    stateContext.FarEnraged = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = -0.3f }, npc.Center);
                    }
                }
            }
            else if (stateContext.FarTimer > 0) {
                stateContext.FarTimer--;
            }
        }

        /// <summary>口器朝向：目标在推进前方时咬向目标(原版语义)</summary>
        private void UpdateMouthRotation() {
            if (!targetPlayer.Alives()) {
                npc.rotation = npc.rotation.AngleLerp(0f, 0.1f);
                return;
            }
            Vector2 toTarget = targetPlayer.Center - npc.Center;
            if (npc.direction > 0) {
                npc.rotation = targetPlayer.Center.X > npc.Center.X
                    ? (float)Math.Atan2(-toTarget.Y, -toTarget.X) + MathHelper.Pi
                    : 0f;
            }
            else {
                npc.rotation = targetPlayer.Center.X < npc.Center.X
                    ? (float)Math.Atan2(toTarget.Y, toTarget.X) + MathHelper.Pi
                    : 0f;
            }
        }

        private void UpdateAmbience() {
            if (VaultUtils.isServer) {
                return;
            }
            //随机低吼(原版口癖)
            roarTimer++;
            if (roarTimer >= 600 + Main.rand.Next(1000)) {
                roarTimer = -Main.rand.Next(200);
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.8f }, npc.Center);
            }
            //面缘常态渗血，密度随潮红
            WofMotionFX.SpawnWallSeep(npc, 0.6f + stateContext.WallFlush * 1.4f);
            //面缘血光
            Lighting.AddLight(new Vector2(WofWallField.WallFaceX(npc), npc.Center.Y),
                WofMotionFX.BloodHot.ToVector3() * (0.5f + stateContext.WallFlush * 0.6f));
        }

        /// <summary>血线滤镜：大迁徙/死亡演出渐入，其余渐出(客户端本地)</summary>
        private void UpdateScreenFilter() {
            if (Main.dedServ) {
                return;
            }
            float goal = 0f;
            var current = stateMachine?.CurrentState;
            if (current is WofCrimsonExodusState) {
                goal = 0.42f;
            }
            else if (current is WofDeathState) {
                goal = 0.3f;
            }
            else if (stateContext.FarEnraged) {
                goal = 0.2f;
            }
            filterIntensity = MathHelper.Lerp(filterIntensity, goal, 0.04f);

            if (filterIntensity > 0.012f) {
                if (!Filters.Scene[FilterName].IsActive()) {
                    Filters.Scene.Activate(FilterName, npc.Center);
                }
                Filters.Scene[FilterName].GetShader().UseOpacity(filterIntensity)
                    .UseTargetPosition(npc.Center);
            }
            else if (Filters.Scene[FilterName].IsActive()) {
                Filters.Scene.Deactivate(FilterName);
            }
        }
        #endregion

        #region 死亡与掉落
        public override bool CheckActive() => false;

        /// <summary>演出中锁血；登场期免死；演出完放行真死(触发困难模式等原版死亡路径)</summary>
        public override bool? CheckDead() {
            if (stateContext == null) {
                return true;
            }
            if (stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null
                && stateMachine.CurrentState is not WofDeathState and not WofIntroState) {
                stateMachine.ChangeState(new WofDeathState());
            }
            return false;
        }
        #endregion

        #region 动画与绘制
        public override bool FindFrame(int frameHeight) {
            //咀嚼节奏随推进速度与嘴部指令
            float rate = 1f + Math.Abs(npc.velocity.X) * 0.10f;
            if (stateContext != null) {
                if (stateContext.MouthCommand == 1) {
                    rate = 3.2f; //狂乱磨牙
                }
                else if (stateContext.MouthCommand == 2) {
                    //紧咬定格
                    npc.frame.Y = 0;
                    return false;
                }
            }
            chewCounter += rate;
            if (chewCounter >= 12f) {
                chewCounter = 0f;
                npc.frame.Y += frameHeight;
                if (npc.frame.Y >= frameHeight * Main.npcFrameCount[npc.type]) {
                    npc.frame.Y = 0;
                }
            }
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }

            //血肉覆膜(墙条带+面缘+拖尾肉髓)
            WofRenderHelper.DrawWallOverlay(spriteBatch, npc, stateContext);

            //口部漩涡(漩涡态由状态推进度)
            if (stateContext.ChargeType == 2 && stateContext.ChargeProgress > 0.01f) {
                WofRenderHelper.DrawMawVortex(spriteBatch, npc, stateContext.ChargeProgress, 0.5f + stateContext.ChargeProgress * 0.5f);
            }

            //口器本体：埋入地下时(入场隆起前)不绘制，破土才现身
            bool mouthBuried = npc.Center.Y > WofWallField.Bottom + 60f;
            if (!mouthBuried) {
                Texture2D tex = TextureAssets.Npc[npc.type].Value;
                Vector2 origin = npc.frame.Size() / 2f;
                SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                spriteBatch.Draw(tex, npc.Center - screenPos + new Vector2(0, npc.gfxOffY), npc.frame,
                    drawColor, npc.rotation, origin, npc.scale, effects, 0f);
            }

            //蓄能光晕(突进=白热 漩涡=猩红 大迁徙=暗红 舌鞭=血红)；
            //漩涡期压低口器光球，主体让给漩涡本身(拒绝常驻亮球)
            if (stateContext.ChargeType > 0 && stateContext.ChargeProgress > 0.02f) {
                Color theme = stateContext.ChargeType switch {
                    1 => new Color(255, 190, 130),
                    2 => WofMotionFX.BloodHot,
                    3 => new Color(150, 20, 30),
                    _ => WofMotionFX.BloodMid,
                };
                float glowStrength = stateContext.ChargeType == 2
                    ? stateContext.ChargeProgress * 0.4f
                    : stateContext.ChargeProgress;
                WofRenderHelper.DrawMouthCharge(npc, glowStrength, theme);
            }

            //舌鞭预告瞄准线：预告后半段自口器指向预测落点(分段端部包络，根/尾无平切)
            if (stateContext.ChargeType == 5 && stateContext.ChargeProgress > 0.55f && targetPlayer.Alives()) {
                Vector2 aim = (targetPlayer.Center + targetPlayer.velocity * 12f - npc.Center)
                    .SafeNormalize(Vector2.UnitX * npc.direction);
                float lineAlpha = (stateContext.ChargeProgress - 0.55f) / 0.45f * 0.55f;
                WofMotionFX.DrawAimLine(spriteBatch, npc.Center, aim, 980f, 9f,
                    new Color(255, 70, 50, 0) * lineAlpha);
            }

            //后方血幕(大迁徙)
            if (stateContext.RearCurtainOpacity > 0.01f && stateContext.RearCurtainX != 0f) {
                WofRenderHelper.DrawBloodCurtain(spriteBatch, stateContext.RearCurtainX,
                    npc.direction, stateContext.RearCurtainOpacity);
            }

            return false;
        }
        #endregion

        #region 对外契约(部件读取)

        /// <summary>读取墙当前同步状态索引</summary>
        internal static WofStateIndex GetStateIndex(NPC wall) {
            return (WofStateIndex)(int)wall.ai[2];
        }

        /// <summary>尝试获取有效的墙主体</summary>
        internal static bool TryGetWall(out NPC wall) {
            wall = null;
            if (Main.wofNPCIndex < 0 || Main.wofNPCIndex >= Main.maxNPCs) {
                return false;
            }
            NPC candidate = Main.npc[Main.wofNPCIndex];
            if (!candidate.active || candidate.type != NPCID.WallofFlesh) {
                return false;
            }
            wall = candidate;
            return true;
        }

        /// <summary>部件伤害基准(随难度缩放走原版口径)</summary>
        internal static int ScaleDamage(NPC wall, int baseDamage) {
            return wall.GetAttackDamage_ScaledByStrength(baseDamage);
        }

        #endregion
    }
}
