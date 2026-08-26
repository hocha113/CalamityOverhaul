using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist
{
    /// <summary>
    /// 拜月教邪教徒 AI 主控:星轨司祭,巨型天体是他的主星,浑天仪是他的法器,黄道环是他划下的界<br/>
    /// 合相充能是宏观压力钟:诸星连珠即大祭,蓄力窗可拆台<br/>
    /// 同步槽位:ai[0]=阶段 ai[1]=浑天仪形态 ai[2]=状态索引 ai[3]=合相充能
    /// </summary>
    internal class CultistBossAI : BrutalNPCOverride, ICWRLoader
    {
        #region 数据
        public override int TargetID => NPCID.CultistBoss;

        /// <summary>life 低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 40;

        private VaultStateMachine<CultistStateContext> stateMachine;
        private CultistStateContext stateContext;
        private Player targetPlayer;
        #endregion

        #region 加载与初始化
        void ICWRLoader.UnLoadData() {
            CultistScreenFX.Clear();
        }

        public override bool? CanBrutalOverride() {
            //跟随残酷模式世界旗标
            return null;
        }

        /// <summary>状态上下文只读口(星球/黄道环等场上实体取用)</summary>
        internal CultistStateContext Context => stateContext;

        public override void SetProperty() {
            //五阶段战线更长,血量上调
            npc.lifeMax = (int)(npc.lifeMax * 1.6f);
            npc.life = npc.lifeMax;
            npc.knockBackResist = 0f;
            npc.npcSlots = 20f;

            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new CultistStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive(),
            };
            stateMachine = new NpcStateMachine<CultistStateContext>(stateContext, aiSlot: 2);

            //客户端从 ai[2] 恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<CultistStateContext> syncedState = VaultStateRegistry<CultistStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new CultistIntroState());
            }
            else {
                stateMachine.SetInitialState(new CultistIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();
            CheckPhaseTransition();
            CheckDeathPerformanceTrigger();
            UpdateAlignEconomy();

            //司祭不近身:接触伤恒零
            npc.damage = 0;

            stateMachine?.Update();

            UpdateAmbientVisuals();
            ForcedNetUpdating(npc);

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            if (npc.target >= 0 && npc.target < 255) {
                targetPlayer = Main.player[npc.target];
            }

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient
                    && stateMachine?.CurrentState is not CultistDespawnState and not CultistDeathState) {
                    stateMachine?.ChangeState(new CultistDespawnState());
                }
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer ?? Main.player[Main.myPlayer];
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();

            //权威端写同步槽,客户端读回,晚入场/漂移都由 ai 槽兜底
            if (VaultUtils.isClient) {
                stateContext.Phase = (int)npc.ai[0];
                stateContext.OrreryMode = (int)npc.ai[1];
                stateContext.AlignCharge = npc.ai[3];
            }
            else {
                npc.ai[0] = stateContext.Phase;
                npc.ai[1] = stateContext.OrreryMode;
                npc.ai[3] = stateContext.AlignCharge;
            }

            //星球开火闸:他收手星球才出手,轮流施压(各端由同步的状态索引一致推导)
            stateContext.PlanetVolleyGate = stateMachine?.CurrentState
                is CultistCoilState or CultistStaggerState;

            //浑天仪显形兜底:晚入场端跳过了入场演出,战斗中环应恒在(入场/死亡/撤离自管)
            if (stateMachine?.CurrentState is not CultistIntroState and not CultistDeathState
                and not CultistDespawnState && stateContext.OrreryReveal < 3f) {
                stateContext.OrreryReveal = MathHelper.Min(3f, stateContext.OrreryReveal + 0.05f);
            }

            //场心跨端解析:黄道环弹幕 netImportant 同步,客户端从它反推(权威端 Intro 直写)
            if (!stateContext.ArenaSpawned || Main.GameUpdateCount % 12 == 0) {
                int arenaType = ModContent.ProjectileType<CultistZodiacRing>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type == arenaType && (int)proj.ai[0] == npc.whoAmI) {
                        stateContext.ArenaCenter = proj.Center;
                        stateContext.ArenaSpawned = true;
                        break;
                    }
                }
            }

            stateContext.DecayVisuals();
        }

        /// <summary>血量过阈转阶段,权威端;不打断入场/演出/大祭/失衡</summary>
        private void CheckPhaseTransition() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.IsInPhaseTransition) {
                return;
            }
            if (stateMachine.CurrentState is CultistIntroState or CultistDespawnState or CultistDeathState
                or CultistPhaseShiftState or CultistConjunctionState or CultistStaggerState) {
                return;
            }

            float ratio = stateContext.Phase < CultistStateContext.PhaseRatios.Length
                ? CultistStateContext.PhaseRatios[stateContext.Phase] : -1f;
            if (ratio > 0f && npc.life < npc.lifeMax * ratio) {
                stateMachine.ChangeState(new CultistPhaseShiftState());
            }
        }

        /// <summary>濒死切死亡演出,权威端</summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is CultistDeathState or CultistDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new CultistDeathState());
            }
        }

        /// <summary>
        /// 合相经济(权威端):出招间充能积累,盘转最快;<br/>
        /// 满格即大祭,蓄力窗内重创可打断(拆台还是躲避的抉择在 Conjunction 态内)
        /// </summary>
        private void UpdateAlignEconomy() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }

            float rate = stateMachine.CurrentState switch {
                CultistCoilState => 0.24f,
                CultistOrbitLanceState or CultistRingHurlState or CultistStarChartState
                    or CultistEclipseState or CultistGazeState or CultistPlanetHurlState
                    or CultistCometVolleyState or CultistZodiacSealState or CultistStasisMinesState => 0.11f,
                _ => 0f,
            };
            if (rate > 0f) {
                stateContext.AddAlign(rate * (stateContext.IsDeathMode ? 1.15f : 1f));
            }
        }

        /// <summary>常驻氛围:体光、连珠逼近预告与分相环境层</summary>
        private void UpdateAmbientVisuals() {
            Color core = CultistMotion.PhaseCore(stateContext.Phase);
            Lighting.AddLight(npc.Center, core.ToVector3() * (0.5f + stateContext.CastAura * 0.5f));

            //连珠逼近:浑天仪急促脉动+低鸣,玩家的"要来了"预告
            float fill = stateContext.AlignCharge / CultistStateContext.AlignMax;
            if (fill > 0.85f && Main.GameUpdateCount % 30 == 0) {
                stateContext.OrreryGlow = MathHelper.Max(stateContext.OrreryGlow, 0.8f);
                if (!VaultUtils.isServer && CultistMotion.OnScreen(npc.Center, 300f)) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.4f, Pitch = -0.5f }, npc.Center);
                }
            }

            UpdatePhaseAmbience();
        }

        /// <summary>
        /// 分相环境(各端本地演出量,无网络):<br/>
        /// 星旋=远雷白闪+雷声;星云=画面阴森去饱和;星尘=晶尘缓降;日耀=余烬上浮+暖幕;月明=冷暗低鸣
        /// </summary>
        private void UpdatePhaseAmbience() {
            if (VaultUtils.isServer || stateContext == null || !stateContext.ArenaSpawned) {
                return;
            }
            Vector2 arena = stateContext.ArenaCenter;
            switch (stateContext.Phase) {
                case 0: {
                    //远雷:不规则白闪+闷雷声
                    if (Main.rand.NextBool(240)) {
                        CultistScreenFX.PushFlash(0.10f + Main.rand.NextFloat(0.08f));
                        SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.35f, Pitch = -0.5f },
                            arena + Main.rand.NextVector2Circular(900f, 500f));
                    }
                    break;
                }
                case 1:
                    //阴森:轻度去饱和垫底
                    CultistScreenFX.BreakDesat = MathHelper.Max(CultistScreenFX.BreakDesat, 0.16f);
                    break;
                case 2: {
                    //晶尘缓降:落星阶段的天在飘星屑
                    if (Main.rand.NextBool(5)) {
                        Vector2 pos = Main.screenPosition + new Vector2(Main.rand.NextFloat(Main.screenWidth), -20f);
                        InnoVault.PRT.PRTLoader.NewParticle<Rendering.PRT_CultistFrostMote>(pos,
                            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(1.4f, 2.6f)),
                            Color.Lerp(CultistMotion.StardustCore, CultistMotion.StardustEdge, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.5f, 1.0f))?.Configure(Main.rand.Next(80, 140));
                    }
                    break;
                }
                case 3: {
                    //炙烤:全场余烬上浮+持续暖幕
                    CultistScreenFX.SetVeil(0.22f, arena, CultistMotion.SolarEdge, 1100f);
                    if (Main.rand.NextBool(4)) {
                        Vector2 pos = Main.screenPosition + new Vector2(Main.rand.NextFloat(Main.screenWidth),
                            Main.screenHeight + 16f);
                        InnoVault.PRT.PRTLoader.NewParticle<Rendering.PRT_CultistEmber>(pos,
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.6f, 3.4f)),
                            Color.Lerp(CultistMotion.SolarCore, CultistMotion.SolarEdge, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.7f, 1.4f))?.Configure(Main.rand.Next(60, 110), 0.02f);
                    }
                    break;
                }
                default: {
                    if (stateContext.Phase >= 4) {
                        //月明:冷暗压场+低鸣
                        CultistScreenFX.BreakDesat = MathHelper.Max(CultistScreenFX.BreakDesat, 0.10f);
                        CultistScreenFX.SetVeil(0.18f, arena, CultistMotion.MoonCore, 1200f);
                        if (Main.GameUpdateCount % 300 == 0) {
                            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.4f, Pitch = -0.9f }, arena);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>远端玩家周期性全量刷新,防长战漂移</summary>
        internal static void ForcedNetUpdating(NPC npc) {
            if (!VaultUtils.isServer || !npc.active || Main.GameUpdateCount % 80 != 0) {
                return;
            }
            foreach (var findPlayer in Main.ActivePlayers) {
                if (findPlayer.Distance(npc.position) < 1440) {
                    continue;
                }
                npc.SendNPCbasicData(findPlayer.whoAmI);
            }
        }

        /// <summary>清场:己方全部攻击性弹幕收势(权威端);星球与黄道环由调用方决定去留</summary>
        internal static void ClearHostileKit(NPC owner) {
            int bead = ModContent.ProjectileType<CultistStarBead>();
            int orbit = ModContent.ProjectileType<CultistOrbitPath>();
            int ring = ModContent.ProjectileType<CultistOrreryRingProj>();
            int shade = ModContent.ProjectileType<CultistUmbraShade>();
            int lance = ModContent.ProjectileType<CultistCoronaLance>();
            int chart = ModContent.ProjectileType<CultistStarChart>();
            int gaze = ModContent.ProjectileType<CultistGazeBeam>();
            int comet = ModContent.ProjectileType<CultistCometProj>();
            int spoke = ModContent.ProjectileType<CultistZodiacSpokeProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == bead || proj.type == orbit || proj.type == ring
                    || proj.type == shade || proj.type == lance || proj.type == chart
                    || proj.type == gaze || proj.type == comet || proj.type == spoke) {
                    proj.Kill();
                }
            }
        }
        #endregion

        #region 伤害与死亡
        /// <summary>失衡窗口受伤加深:拆台奖励,各端由同步的状态索引一致推导</summary>
        public override bool? On_ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (stateMachine?.CurrentState is CultistStaggerState) {
                modifiers.FinalDamage *= 1.25f;
            }
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>演出中锁血,完后放行;秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                npc.dontTakeDamage = false;
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not CultistDeathState) {
                stateMachine.ChangeState(new CultistDeathState());
            }

            return false;
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }
            Rendering.CultistOrreryRenderer.DrawBody(spriteBatch, npc, stateContext, screenPos, drawColor);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
        #endregion
    }
}
