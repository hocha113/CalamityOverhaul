using CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EmpressOfLight;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight
{
    /// <summary>
    /// 光之女皇主控：InnoVault状态机全接管。
    /// npc.ai[0]/ai[1]=姿态通道（原版绘制语义） npc.ai[2]=状态机 npc.ai[3]=形态位（1 二阶段 2 真昼 4 三阶段）；
    /// NPCOverride.ai[0..2]=竞技场半径/圆心 ai[3]=终章可击杀
    /// </summary>
    internal class EmpressOfLightAI : BrutalNPCOverride, ILocalizedModType
    {
        #region 数据
        public override int TargetID => NPCID.HallowBoss;
        public string LocalizationCategory => "BrutalNPCs";

        /// <summary>life低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;
        /// <summary>终章缩圈未满时的血量地板（占最大生命）</summary>
        internal const float FinaleLifeFloor = 0.02f;
        private const int SlotKillable = 3;

        internal static readonly Color BossTextColor = new(255, 231, 160);
        private static LocalizedText[] ascensionLines;
        private static LocalizedText[] farewellLines;
        private static LocalizedText whiteoutDeath;

        private VaultStateMachine<EmpressStateContext> stateMachine;
        private EmpressStateContext stateContext;
        private Player targetPlayer;

        /// <summary>弹幕侧读节拍/形态用（各端本地）</summary>
        internal EmpressStateContext Context => stateContext;
        /// <summary>上一帧昼形态标志，检测破晓/入夜的换形瞬间</summary>
        private bool lastDayEmpowered;
        #endregion

        #region 加载与初始化
        public override void SetStaticDefaults() {
            ascensionLines = new LocalizedText[7];
            string[] ascensionDefaults = [
                "……哈？", "这怎么可能？", "我已将我的力量迫至极限，你又是怎么活下来的？", "……不。",
                "我不会就这样向你低头。", "你想赢下这一场，就要接住我的每一分光。", "现在，到你了。",
            ];
            for (int i = 0; i < ascensionLines.Length; i++) {
                int idx = i;
                ascensionLines[i] = this.GetLocalization($"Ascension_{i}", () => ascensionDefaults[idx]);
            }
            farewellLines = new LocalizedText[4];
            string[] farewellDefaults = [
                "……够了。", "你远比看上去更强。", "那些蝴蝶对我意义非凡，别去惊扰它们，这片天空就还是我们共有的。", "光会记得你。",
            ];
            for (int i = 0; i < farewellLines.Length; i++) {
                int idx = i;
                farewellLines[i] = this.GetLocalization($"Farewell_{i}", () => farewellDefaults[idx]);
            }
            whiteoutDeath = this.GetLocalization("WhiteoutDeath", () => "{0}在她的光里蒸发了");
        }

        /// <summary>月影蒸发的死亡原因文本</summary>
        internal static string WhiteoutDeathText(string playerName) => whiteoutDeath?.Format(playerName) ?? playerName;

        /// <summary>三阶段台词（权威端广播，客户端忽略）</summary>
        internal static void SayAscension(int index) => Say(ascensionLines, index);
        /// <summary>认输独白</summary>
        internal static void SayFarewell(int index) => Say(farewellLines, index);

        private static void Say(LocalizedText[] lines, int index) {
            if (VaultUtils.isClient || lines == null || index < 0 || index >= lines.Length || lines[index] == null) {
                return;
            }
            VaultUtils.Text(lines[index].Value, BossTextColor);
        }

        public override void SetProperty() {
            //oldPos 供原版冲刺彩虹残影使用
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 24;
            InitializeStateContext();
        }

        protected override bool CarryStateTiming => true;
        protected override IBossNetTiming TimedState => stateMachine?.CurrentState as IBossNetTiming;

        private void InitializeStateContext() {
            stateContext = new EmpressStateContext {
                Npc = npc,
                IsAsuraMode = CWRWorld.Asura
            };
            stateMachine = new NpcStateMachine<EmpressStateContext>(stateContext, aiSlot: 2);

            //换态包带的是新态的计时：客户端在框架换态（新实例 OnEnter 刚清零）之后立刻收养
            stateMachine.OnStateChanged += (_, next, _) => AdoptTimingOnSwap(next);

            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<EmpressStateContext> syncedState = VaultStateRegistry<EmpressStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new EmpressIntroState());
                //中途加入的端上 SetDefaults 留下 Opacity=0/dontTakeDamage=true，恢复到非入场态时补回战斗常态
                if (syncedState is not null and not EmpressIntroState) {
                    npc.Opacity = 1f;
                    npc.dontTakeDamage = false;
                }
            }
            else {
                stateMachine.SetInitialState(new EmpressIntroState());
            }
            lastDayEmpowered = NPC.ShouldEmpressBeEnraged();
        }
        #endregion

        #region 主AI
        public override bool AI() {
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            //全舰队唯一一只从来没做过平滑决策的：HallowBoss 不在原版豁免表里，
            //而她的滑翔/贴身追击速度上限 26 px/f 起，命中驱动的高频快照下原版平滑必然锯齿
            BeginNetFrame();

            FindTarget();
            UpdateStateContext();
            CheckDeathPerformanceTrigger();

            //原版契约：满血白昼升格真昼形态（ai[3]|2），Terraprisma掉落条件依赖此位
            if (!VaultUtils.isClient && npc.life == npc.lifeMax
                && NPC.ShouldEmpressBeEnraged() && ((int)npc.ai[3] & 2) == 0) {
                npc.ai[3] = (int)npc.ai[3] | 2;
                npc.netUpdate = true;
            }

            UpdateDayNightForm();

            //姿态/蓄力每帧重声明，未声明回落
            stateContext.Pose = EmpressPose.Idle;
            stateContext.PoseTimer = 0f;
            //竞技场默认请求：昼且在战斗态时按阶段给半径，状态可覆盖（终章缩圈）
            stateContext.ArenaRadiusRequest = DefaultArenaRadius();
            stateContext.ArenaFollowSpeed = 12f;

            //节拍先走一步，让状态读到本帧的拍位；演出态不放拍点提示
            EmpressTempo.Update(this, stateContext);
            stateContext.TempoCueEnabled = stateMachine?.CurrentState is not (EmpressIntroState or EmpressDeathState or EmpressDespawnState);
            EmpressTempo.DownbeatCue(stateContext);
            EmpressTempo.WeakbeatCue(stateContext);

            npc.damage = 0;

            stateMachine?.Update();

            npc.ai[0] = (float)stateContext.Pose;
            npc.ai[1] = stateContext.PoseTimer;

            npc.rotation = npc.velocity.X * 0.005f;
            if ((npc.localAI[0] += 1f) >= 44f) {
                npc.localAI[0] = 0f;
            }

            npc.defense = stateContext.IsThirdPhase ? (int)(npc.defDefense * 1.35f)
                : (stateContext.IsSecondPhase ? (int)(npc.defDefense * 1.2f) : npc.defDefense);

            //终章缩圈未满：血量地板，剧本一定被看到
            if (stateContext.IsThirdPhase && !stateContext.FinaleKillable && stateMachine.CurrentState is EmpressFinaleState) {
                int floor = Math.Max((int)(npc.lifeMax * FinaleLifeFloor), DeathPerformanceTriggerLife + 1);
                if (npc.life < floor) {
                    npc.life = floor;
                }
            }

            Lighting.AddLight(npc.Center, Vector3.One * npc.Opacity * (0.9f + stateContext.DayFormBlend * 0.4f));
            UpdateAmbientVisuals();

            if (!VaultUtils.isClient) {
                if (stateContext.GrabCooldown > 0) {
                    stateContext.GrabCooldown--;
                }
                EmpressArena.ServerUpdate(this, stateContext);
                ai[SlotKillable] = stateContext.FinaleKillable ? 1f : 0f;
            }
            else {
                stateContext.FinaleKillable = ai[SlotKillable] > 0.5f;
            }

            //决策点（换态/棱彩闪现/竞技场缩圈/命中）各自 netUpdate，兜底心跳与客户端预测都在基类
            EndNetFrame();

            return false;
        }

        /// <summary>昼形态各阶段的竞技场半径；夜与非战斗态关闭</summary>
        private float DefaultArenaRadius() {
            if (!stateContext.DayEmpowered) {
                return 0f;
            }
            if (stateMachine?.CurrentState is EmpressIntroState or EmpressDeathState or EmpressDespawnState
                or EmpressPhaseTransitionState or EmpressAscensionState or EmpressLightBindWaltzState) {
                return 0f;
            }
            return stateContext.IsSecondPhase ? 2600f : 2400f;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not EmpressDespawnState and not EmpressDeathState) {
                    stateMachine?.ChangeState(new EmpressDespawnState());
                }
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            int form = (int)npc.ai[3];
            stateContext.IsSecondPhase = (form & 1) != 0;
            stateContext.IsThirdPhase = (form & 4) != 0;
            stateContext.IsAsuraMode = CWRWorld.Asura;
            stateContext.DayEmpowered = NPC.ShouldEmpressBeEnraged();
            float blendTarget = stateContext.DayEmpowered ? 1f : 0f;
            stateContext.DayFormBlend = MathHelper.Lerp(stateContext.DayFormBlend, blendTarget, 0.012f);
            if (Math.Abs(stateContext.DayFormBlend - blendTarget) < 0.004f) {
                stateContext.DayFormBlend = blendTarget;
            }
        }

        /// <summary>昼夜换形瞬间：破晓日辉强拍，入夜柔光敛息</summary>
        private void UpdateDayNightForm() {
            bool now = stateContext.DayEmpowered;
            if (now == lastDayEmpowered) {
                return;
            }
            lastDayEmpowered = now;

            if (!VaultUtils.isClient && now && npc.life == npc.lifeMax && ((int)npc.ai[3] & 2) == 0) {
                npc.ai[3] = (int)npc.ai[3] | 2;
                npc.netUpdate = true;
            }

            if (stateMachine?.CurrentState is EmpressDeathState or EmpressDespawnState) {
                return;
            }

            if (!VaultUtils.isServer) {
                if (now) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 0.85f, 40);
                    EmpressScreenFX.PushImpactDim(0.4f);
                    SoundEngine.PlaySound(SoundID.Item161 with { Volume = 0.9f, Pitch = 0.25f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item163 with { Volume = 0.7f, Pitch = 0.4f }, npc.Center);
                    EmpressMotion.SparkBurst(npc.Center, Vector2.UnitY, 20, 3f, 10f, 1f, MathHelper.Pi);
                }
                else {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 0.35f, 30);
                    SoundEngine.PlaySound(SoundID.Item165 with { Volume = 0.75f, Pitch = -0.3f }, npc.Center);
                    PRTLoader.NewParticle<PRT_EmpressRipple>(npc.Center, Vector2.Zero, new Color(190, 160, 255), 0.7f)?.Configure(16, 0.72f);
                }
                EmpressMotion.Shake(npc.Center, 4f, 20);
            }
        }

        /// <summary>life≤阈值切死亡演出，服务端驱动；终章缩圈未满不放行</summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is EmpressDeathState or EmpressDespawnState) {
                return;
            }
            if (stateContext.IsThirdPhase && !stateContext.FinaleKillable && stateMachine.CurrentState is EmpressFinaleState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new EmpressDeathState());
            }
        }

        /// <summary>环境层：昼形态世界压暗续租 + 屏幕描边（客户端）</summary>
        private void UpdateAmbientVisuals() {
            if (VaultUtils.isServer) {
                return;
            }
            float blend = stateContext.DayFormBlend;
            if (blend <= 0.02f) {
                return;
            }
            EmpressScreenFX.DeclareAmbient(blend * 0.3f);

            float drive = blend;
            float flicker = blend;
            if (stateMachine?.CurrentState is EmpressIntroState intro) {
                float p = MathHelper.Clamp(intro.Timer / 192f, 0f, 1f);
                drive = blend * p;
                //她来之前灯先疯
                flicker = blend * MathF.Sqrt(p) * 3.5f;
            }
            else if (stateMachine?.CurrentState is EmpressDeathState death) {
                drive = blend * MathHelper.Clamp(1f - death.Timer / (float)EmpressDeathState.TotalTime, 0f, 1f);
            }
            EmpressDayDrive.Report(drive, flicker);
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            EmpressRenderHelper.DrawUnderGlow(spriteBatch, npc, stateContext);
            return null;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            EmpressRenderHelper.DrawOverGlow(spriteBatch, npc, stateContext);
            //竞技场边界环（实体批，世界坐标）
            if (ai[EmpressArena.SlotRadius] > 1f) {
                Vector2 center = new(ai[EmpressArena.SlotCenterX], ai[EmpressArena.SlotCenterY]);
                EmpressArena.DrawBoundary(spriteBatch, center, ai[EmpressArena.SlotRadius], npc.Opacity);
            }
            return true;
        }
        #endregion

        #region 杂项覆写
        /// <summary>死亡演出进行中（运镜侧查询）</summary>
        internal bool InDeathPerformance => stateMachine?.CurrentState is EmpressDeathState;

        /// <summary>死亡演出计时（运镜对表）</summary>
        internal int DeathTimer => stateMachine?.CurrentState is EmpressDeathState death ? death.Timer : 0;

        public override bool CheckActive() => false;

        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.ByCondition(new DropInBrutalWorld(), ModContent.ItemType<WingsOfInterference>()));
        }

        /// <summary>演出中锁血，完后放行；终章缩圈未满也锁</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            if (stateContext.IsThirdPhase && !stateContext.FinaleKillable && stateMachine?.CurrentState is EmpressFinaleState) {
                npc.life = Math.Max((int)(npc.lifeMax * FinaleLifeFloor), DeathPerformanceTriggerLife + 1);
                return false;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not EmpressDeathState) {
                stateMachine.ChangeState(new EmpressDeathState());
            }

            return false;
        }
        #endregion
    }
}
