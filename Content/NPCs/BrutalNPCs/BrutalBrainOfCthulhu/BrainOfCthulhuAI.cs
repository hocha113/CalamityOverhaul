using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Accessories.BrutalRelics.BrainOfCthulhu;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu
{
    /// <summary>克脑主控：镜像瞬移心理战，状态机契约见 BrainStateIndex、npc.ai[2]</summary>
    internal class BrainOfCthulhuAI : BrutalNPCOverride, ICWRLoader
    {
        #region 数据
        public override int TargetID => NPCID.BrainofCthulhu;

        /// <summary>life 低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;
        /// <summary>二阶段血量阈值</summary>
        internal const float Phase2LifeRatio = 0.55f;
        /// <summary>低血狂化阈值（解锁心搏骤停）</summary>
        internal const float LowLifeRatio = 0.28f;
        /// <summary>目标脱离猩红进入狂暴前的宽限帧(允许短暂追出边界)</summary>
        internal const int OutOfZoneEnrageDelay = 120;
        /// <summary>override ai 槽位：出猩红狂暴强度0~1(权威端写入，各端回读；0/1=编队锚点 2=矛浪旋向)</summary>
        internal const int SlotEnrageRamp = 3;

        private VaultStateMachine<BrainStateContext> stateMachine;
        private BrainStateContext stateContext;
        private Player targetPlayer;
        /// <summary>远距滞留帧，达上限触发回归瞬移</summary>
        private int farTimer;
        /// <summary>目标脱离猩红累计帧，过宽限进入狂暴</summary>
        private int outOfZoneTimer;
        /// <summary>入怒吼声已播(本地防重播)</summary>
        private bool enrageCuePlayed;
        /// <summary>乘算记忆：上帧原始接触伤(-1=无效)，防状态未逐帧重声明时复利爆炸</summary>
        private int lastRawDamage = -1;
        /// <summary>乘算记忆：上帧放大后的输出值</summary>
        private int lastEnragedOutput = -1;
        /// <summary>客户端瞬移检测：上一帧位置</summary>
        private Vector2 lastFramePos;
        private bool lastPosValid;
        #endregion

        #region 加载与初始化
        void ICWRLoader.LoadData() { }

        void ICWRLoader.UnLoadData() {
            BrainHeartbeat.Clear();
        }

        public override void SetProperty() {
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 12;
            InitializeStateContext();
        }

        public override bool? CanBrutalOverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new BrainStateContext {
                Npc = npc,
                Master = this,
                IsAsuraMode = CWRWorld.Asura
            };
            stateMachine = new NpcStateMachine<BrainStateContext>(stateContext, aiSlot: 2);

            //客户端从ai[2]恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<BrainStateContext> syncedState = VaultStateRegistry<BrainStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new BrainIntroState());
            }
            else {
                stateMachine.SetInitialState(new BrainIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            //飞眼与镜像依赖此索引
            NPC.crimsonBoss = npc.whoAmI;

            FindTarget();
            UpdateStateContext();
            CheckDeathPerformanceTrigger();

            //客户端瞬移检测：大位移自播撕裂演出（先于状态机，让状态对 GhostFade 拥有最终话语权）
            DetectTeleportVisual();

            //每帧重声明，未声明归零
            stateContext.ResetTelegraph();
            stateContext.BeatPeriod = stateContext.IsPhase2 ? 40 : 54;
            stateContext.BeatIntensity = stateContext.IsPhase2 ? 0.62f : 0.45f;
            stateContext.BeatSilenced = false;

            //状态机
            stateMachine?.Update();

            //无敌统一落位（每帧声明制；死亡演出锁血由 CheckDead 兜底）
            npc.dontTakeDamage = stateContext.Invulnerable;

            //出猩红狂暴：接触伤放大+心跳加重，AI 与招式不动
            UpdateEnragePresentation();

            //心跳时钟（各端同步递增，netUpdate 纠偏）
            npc.ai[3] += 1f;
            DispatchBeat();

            //远距回归瞬移阀
            UpdateFarReturnValve();

            //屏效推送与灯光
            PushScreenState();

            //力竭窗口
            if (stateContext.FalterTimer > 0) {
                stateContext.FalterTimer--;
            }
            if (stateContext.HeartAttackCooldown > 0) {
                stateContext.HeartAttackCooldown--;
            }
            if (stateContext.MindSeizeCooldown > 0) {
                stateContext.MindSeizeCooldown--;
            }

            //防御档位：壳存期+飞眼护佑更硬，露心归零
            UpdateDefense();

            npc.knockBackResist = 0f;
            npc.chaseable = !stateContext.HideFromMinions;

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }
            ForcedNetUpdating(npc);

            return false;
        }
        #endregion

        #region 上下文与节拍

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.LifeRatio = npc.life / (float)npc.lifeMax;
            stateContext.IsPhase2 = npc.ai[0] < 0f;
            stateContext.IsLowLife = stateContext.LifeRatio <= LowLifeRatio;
            stateContext.IsAsuraMode = CWRWorld.Asura;

            //客户端从同步槽回读狂暴强度
            if (VaultUtils.isClient) {
                stateContext.EnrageRamp = MathHelper.Clamp(ai[SlotEnrageRamp], 0f, 1f);
            }

            if (Main.GameUpdateCount % 30 == 0) {
                stateContext.RefreshCreepers();
            }
        }

        /// <summary>狂暴表现与增伤统一落位：接触伤放大、心跳加重、入怒瞬间吼声与红光</summary>
        private void UpdateEnragePresentation() {
            float ramp = stateContext.EnrageRamp;
            if (ramp <= 0.01f) {
                enrageCuePlayed = false;
                lastRawDamage = -1;
                lastEnragedOutput = -1;
                return;
            }

            //带记忆的乘算：与上帧输出相同说明本帧未被状态重新声明，先还原原始值再乘，防逐帧复利
            if (npc.damage > 0) {
                if (npc.damage == lastEnragedOutput && lastRawDamage >= 0) {
                    npc.damage = lastRawDamage;
                }
                lastRawDamage = npc.damage;
                npc.damage = (int)(npc.damage * (1f + 0.8f * ramp));
                lastEnragedOutput = npc.damage;
            }
            else {
                lastRawDamage = -1;
                lastEnragedOutput = -1;
            }

            //心跳只上抬力度不动周期：心音=判定拍（裂隙真假、整拍出击、环笼收缩都读它），
            //压缩周期会让心音脱离各状态的判定窗，教玩家错误节拍
            stateContext.BeatIntensity += 0.35f * ramp;

            Lighting.AddLight(npc.Center, BrainMotion.BloodBright.ToVector3() * 0.7f * ramp);

            if (!enrageCuePlayed) {
                enrageCuePlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 1f, Pitch = -0.6f }, npc.Center);
                }
            }
        }

        /// <summary>
        /// 整拍事件：屏冲+心音
        /// 判拍用与各状态一致的模运算（ai[3] % 周期 == 0），对周期切换鲁棒；
        /// 用“上次触发的时钟戳”去重，防快照回卷复播（严格前进才响）
        /// </summary>
        private void DispatchBeat() {
            if (stateContext.BeatSilenced || stateContext.BeatPeriod <= 0) {
                return;
            }
            long clock = (long)npc.ai[3];
            if (clock % stateContext.BeatPeriod != 0 || clock <= stateContext.LastPlayedBeat) {
                return;
            }
            stateContext.LastPlayedBeat = clock;

            if (VaultUtils.isServer) {
                return;
            }

            float intensity = stateContext.BeatIntensity;
            //距离衰减：越近越压迫
            float dist = Main.LocalPlayer.Distance(npc.Center);
            float proximity = MathHelper.Clamp(1f - dist / 2400f, 0.15f, 1f);

            BrainHeartbeat.Thump(intensity * (0.55f + proximity * 0.45f));
            BrainHeartbeat.PlayThumpSound(npc.Center, intensity * proximity,
                stateContext.IsPhase2 ? 0.12f : 0f);
        }

        /// <summary>life≤阈值切死亡演出，服务端驱动</summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is BrainDeathState or BrainDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new BrainDeathState());
            }
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (VaultUtils.isClient) {
                return;
            }

            //撤离/死亡演出期间：狂暴消退，不再裁决
            if (stateMachine?.CurrentState is BrainDespawnState or BrainDeathState) {
                outOfZoneTimer = 0;
                stateContext.EnrageRamp = MathHelper.Clamp(stateContext.EnrageRamp - 1f / 60f, 0f, 1f);
                ai[SlotEnrageRamp] = stateContext.EnrageRamp;
                return;
            }

            if (!targetPlayer.Alives()) {
                stateMachine?.ChangeState(new BrainDespawnState());
                return;
            }

            //目标持续脱离猩红不再脱战：宽限后进入狂暴（AI 不变，免伤+增伤；BossRush 豁免），权威端裁决
            //入场演出期不累计（防开幕即怒吼撞演出）
            if (stateMachine?.CurrentState is BrainIntroState) {
                outOfZoneTimer = 0;
            }
            else if (!targetPlayer.ZoneCrimson && !CWRRef.GetBossRushActive()) {
                outOfZoneTimer++;
            }
            else if (outOfZoneTimer > 0) {
                outOfZoneTimer = Math.Max(outOfZoneTimer - 2, 0);
            }
            float step = outOfZoneTimer > OutOfZoneEnrageDelay ? 1f / 60f : -1f / 60f;
            stateContext.EnrageRamp = MathHelper.Clamp(stateContext.EnrageRamp + step, 0f, 1f);
            ai[SlotEnrageRamp] = stateContext.EnrageRamp;
        }

        private void UpdateDefense() {
            if (stateContext.HeartExposed) {
                npc.defense = 0;
                return;
            }
            if (!stateContext.IsPhase2) {
                bool anyCreeper = stateContext.Creepers.Count > 0;
                npc.defense = npc.defDefense + (anyCreeper ? 10 : 2);
                return;
            }
            npc.defense = Math.Max(npc.defDefense - 2, 0);
        }

        /// <summary>远距瞬移回归，AllowFarSnap 可关</summary>
        private void UpdateFarReturnValve() {
            if (stateMachine?.CurrentState is not BrainStateBase state || !state.AllowFarSnap) {
                farTimer = 0;
                return;
            }
            if (!targetPlayer.Alives()) {
                farTimer = 0;
                return;
            }

            float dist = npc.Distance(targetPlayer.Center);
            if (dist <= 2400f) {
                farTimer = 0;
                return;
            }

            if (++farTimer < 30 || VaultUtils.isClient) {
                return;
            }
            farTimer = 0;

            //瞬移到玩家侧上方视野边
            Vector2 dir = (npc.Center - targetPlayer.Center).SafeNormalize(-Vector2.UnitY);
            BrainMotion.ServerTeleport(npc, targetPlayer.Center + dir * 860f, dir * -6f);
        }

        /// <summary>客户端大位移检测，撕裂演出各端自播（无包自愈）</summary>
        private void DetectTeleportVisual() {
            if (VaultUtils.isServer) {
                lastPosValid = false;
                return;
            }
            if (lastPosValid) {
                float jump = Vector2.Distance(lastFramePos, npc.Center);
                if (jump > 240f) {
                    BrainMotion.TeleportBurst(lastFramePos, 1.1f, false);
                    BrainMotion.TeleportBurst(npc.Center, 1.25f, true);
                    //瞬移落地实体化爬升
                    stateContext.GhostFade = Math.Min(stateContext.GhostFade, 0.15f);
                }
            }
            lastFramePos = npc.Center;
            lastPosValid = true;

            //实体化爬升
            if (stateContext.GhostFade < 1f) {
                stateContext.GhostFade = Math.Min(stateContext.GhostFade + 0.07f, 1f);
            }
        }

        private void PushScreenState() {
            if (VaultUtils.isServer) {
                return;
            }

            Lighting.AddLight(npc.Center, BrainMotion.BloodBright.ToVector3() *
                (stateContext.IsPhase2 ? 0.95f : 0.55f) * stateContext.GhostFade);

            float dist = Main.LocalPlayer.Distance(npc.Center);
            float proximity = MathHelper.Clamp(1f - dist / 3200f, 0f, 1f);

            float veil = stateContext.IsPhase2 ? 0.5f + (stateContext.IsLowLife ? 0.22f : 0f) : 0.12f;
            BrainHeartbeat.Push(npc.Center, stateContext.BeatIntensity * proximity,
                veil * proximity, stateContext.BlackoutTarget * MathHelper.Clamp(proximity * 1.5f, 0f, 1f));
        }

        /// <summary>瞬移频繁，远端玩家周期性强推基础数据</summary>
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

        #endregion

        #region 判定与掉落

        /// <summary>力竭窗口受伤加深；出猩红狂暴免伤（无尽伤害类不受抑制）</summary>
        public override bool? On_ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (stateContext != null && stateContext.FalterTimer > 0) {
                modifiers.FinalDamage *= 1.3f;
            }
            if (stateContext != null && stateContext.EnrageRamp > 0f
                && modifiers.DamageType != EndlessDamageClass.Instance) {
                modifiers.FinalDamage *= 1f - 0.9f * stateContext.EnrageRamp;
            }
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>残酷遗物「镜心悖论」：残酷世界击杀必掉(条件类自带门禁)</summary>
        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.ByCondition(new DropInBrutalWorld(),
                ModContent.ItemType<MirrorheartParadox>()));
        }

        /// <summary>演出中锁血，完后放行；秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not BrainDeathState) {
                stateMachine.ChangeState(new BrainDeathState());
            }

            return false;
        }

        #endregion

        #region 帧与绘制

        public override bool FindFrame(int frameHeight) {
            //露心命令：一阶段强制开壳帧
            if (stateContext != null && stateContext.FrameCommand == 1 && !stateContext.IsPhase2) {
                npc.frameCounter += 1.0;
                if (npc.frameCounter > 6.0) {
                    npc.frameCounter = 0.0;
                    npc.frame.Y += frameHeight;
                }
                if (npc.frame.Y < frameHeight * 4 || npc.frame.Y > frameHeight * 7) {
                    npc.frame.Y = frameHeight * 4;
                }
                return false;
            }
            return true;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }
            //出猩红狂暴体色：血光灼热
            if (stateContext.EnrageRamp > 0.01f) {
                drawColor = Color.Lerp(drawColor, new Color(255, 60, 50), stateContext.EnrageRamp * 0.45f);
            }
            BrainRenderHelper.DrawBrain(spriteBatch, npc, stateContext, screenPos, drawColor);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        #endregion
    }
}
