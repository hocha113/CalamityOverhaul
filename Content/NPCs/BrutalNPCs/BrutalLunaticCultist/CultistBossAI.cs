using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist
{
    /// <summary>拜月教邪教徒主控：元素仪式与真身博弈</summary>
    internal class CultistBossAI : CWRNPCOverride
    {
        #region Data
        public override int TargetID => NPCID.CultistBoss;

        /// <summary>life 低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;

        private VaultStateMachine<CultistStateContext> stateMachine;
        private CultistStateContext stateContext;
        private Player targetPlayer;

        internal CultistStateContext Context => stateContext;
        internal VaultStateMachine<CultistStateContext> Machine => stateMachine;
        #endregion

        #region 初始化
        public override void SetProperty() {
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 12;
            InitializeStateContext();
        }

        public override bool? CanCWROverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new CultistStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive(),
                LifeSnapshot = npc.lifeMax,
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
            CheckPhaseGates();
            CheckDeathPerformanceTrigger();

            //每帧重声明的表现量
            stateContext.CastPose = CultistPose.Float;
            stateContext.CastGlow = 0f;
            stateContext.ElementAura = 0.35f;
            stateContext.SkipDefaultHover = false;
            stateContext.StageSigilProgress = 0f;
            stateContext.StageSigilFlash = 0f;
            stateContext.StageSigilSpin += 0.02f;

            stateMachine?.Update();

            if (!stateContext.SkipDefaultHover) {
                UpdateHover();
            }

            //纯施法者永不接触伤害（公平阀：瞬移不追尊）
            npc.damage = 0;

            UpdateEnrage();

            //破绽硬直：防御归零，计时衰减
            if (!VaultUtils.isClient) {
                if (stateContext.StaggerTimer > 0) {
                    stateContext.StaggerTimer--;
                }
                MirrorAiSlots();
            }
            npc.defense = stateContext.StaggerTimer > 0 ? 0 : (stateContext.IsPhase2 ? (int)(npc.defDefense * 0.8f) : npc.defDefense);

            UpdateAmbientVisuals();

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        /// <summary>服务端把权威数据镜像进同步 ai 槽</summary>
        private void MirrorAiSlots() {
            ai[0] = (int)stateContext.Element;
            ai[1] = stateContext.IsPhase2 ? 1f : 0f;
            ai[2] = stateContext.RitualProgress;
            ai[3] = stateContext.StaggerTimer;
            ai[4] = stateContext.RitualCenter.X;
            ai[5] = stateContext.RitualCenter.Y;
            ai[6] = stateContext.Enraged ? 1f : 0f;
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            if (!VaultUtils.isClient) {
                stateContext.IsPhase2 = npc.life < npc.lifeMax * 0.5f || stateContext.IsPhase2;
            }
            else {
                //客户端在状态机更新前从同步槽回读，保证 OnEnter 读到新鲜值
                stateContext.Element = (CultistElement)(int)MathHelper.Clamp(ai[0], 0f, 2f);
                stateContext.IsPhase2 = ai[1] >= 1f;
                stateContext.RitualProgress = ai[2];
                stateContext.StaggerTimer = (int)ai[3];
                stateContext.RitualCenter = new Vector2(ai[4], ai[5]);
                stateContext.Enraged = ai[6] >= 1f;
            }
            if (Main.GameUpdateCount % 30 == 0) {
                stateContext.RefreshClones();
            }
        }

        /// <summary>阶段闸门：50% 转阶段、28% 大招，服务端一次性</summary>
        private void CheckPhaseGates() {
            if (VaultUtils.isClient || stateMachine == null) {
                return;
            }
            if (stateMachine.CurrentState is CultistDeathState or CultistDespawnState
                or CultistIntroState or CultistPhaseTransitionState or CultistCataclysmState) {
                return;
            }
            //破绽硬直是玩家挣来的输出窗口，不被阶段演出抢走
            if (stateContext.StaggerTimer > 0) {
                return;
            }

            if (!stateContext.PhaseTransitionDone && npc.life < npc.lifeMax * 0.5f) {
                stateMachine.ChangeState(new CultistPhaseTransitionState());
                return;
            }
            //大仪式吟唱中不抢大招（仪式自身承担压力节拍）
            if (stateMachine.CurrentState is CultistGrandRitualState) {
                return;
            }
            if (!stateContext.CataclysmUsed && stateContext.PhaseTransitionDone && npc.life < npc.lifeMax * 0.28f) {
                stateMachine.ChangeState(new CultistCataclysmState());
            }
        }

        /// <summary>life≤阈值切死亡演出，服务端驱动</summary>
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

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest(faceTarget: false);
            }
            targetPlayer = Main.player[npc.target];

            if (VaultUtils.isClient) {
                return;
            }

            //只有无有效目标（全员死亡/离线）才撤离；拉远脱战一律转狂暴
            if (!targetPlayer.Alives()
                && stateMachine?.CurrentState is not CultistDespawnState and not CultistDeathState) {
                stateMachine?.ChangeState(new CultistDespawnState());
                return;
            }

            //狂暴迟滞带：拉出 5600 触发，收回 4600 内解除（防边界抖动）
            float dist = targetPlayer.Alives() ? npc.Distance(targetPlayer.Center) : 0f;
            if (!stateContext.Enraged && dist > 5600f) {
                stateContext.Enraged = true;
                npc.netUpdate = true;
            }
            else if (stateContext.Enraged && dist < 4600f) {
                stateContext.Enraged = false;
                npc.netUpdate = true;
            }
        }

        /// <summary>悬浮：弹簧趋近锚点+呼吸浮沉</summary>
        private void UpdateHover() {
            Vector2 anchor = stateContext.HoverAnchor;
            if (anchor == Vector2.Zero) {
                anchor = npc.Center;
            }
            float bob = (float)Math.Sin(Main.GameUpdateCount * 0.045f + npc.whoAmI) * 14f;
            Vector2 goal = anchor + new Vector2(0f, bob);
            Vector2 toGoal = goal - npc.Center;
            Vector2 desired = toGoal * 0.055f;
            if (desired.Length() > 17f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 17f;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.12f);
            npc.rotation = npc.velocity.X * 0.012f;
        }

        private bool prevEnraged;

        /// <summary>脱战狂暴：免伤生效+各端转场演出（纯音画，无文本）</summary>
        private void UpdateEnrage() {
            //狂暴时置位免伤；撤离/死亡自管不抢写
            if (stateContext.Enraged
                && stateMachine?.CurrentState is not CultistDespawnState and not CultistDeathState) {
                npc.dontTakeDamage = true;
            }

            if (stateContext.Enraged != prevEnraged) {
                if (stateContext.Enraged && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1f, Pitch = 0.35f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 0.9f, Pitch = 0.3f }, npc.Center);
                    CultistScreenFX.PushFlash(0.35f, 14);
                }
                //解除沿边清除免伤：只在不自管无敌的状态里放行，避免覆盖 Intro/转阶段/死亡锁
                if (!stateContext.Enraged
                    && stateMachine?.CurrentState is not CultistDespawnState and not CultistDeathState
                        and not CultistIntroState and not CultistPhaseTransitionState) {
                    npc.dontTakeDamage = false;
                }
                prevEnraged = stateContext.Enraged;
            }

            //狂暴常驻音画：绯白怒焰环绕
            if (stateContext.Enraged && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(46f, 60f);
                PRTLoader.NewParticle<PRT_CultistEmber>(pos, -Vector2.UnitY * Main.rand.NextFloat(1.5f, 4f),
                    new Color(255, 90, 70), Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(14, 26));
            }
        }

        private void UpdateAmbientVisuals() {
            Color main = CultistPalette.Main(stateContext.Element);
            Lighting.AddLight(npc.Center, main.ToVector3() * (0.5f + stateContext.CastGlow * 0.8f));

            //常态低语（原版彩蛋保留）
            if (!VaultUtils.isServer && npc.alpha < 100 && Main.rand.NextBool(900)) {
                CultistRenderHelper.ChantVoice(npc.Center, 0.55f, -0.1f);
            }
        }
        #endregion

        #region 帧驱动
        public override bool FindFrame(int frameHeight) {
            if (npc.IsABestiaryIconDummy) {
                return true;
            }
            int pose = stateContext?.CastPose ?? CultistPose.Float;
            int counter = stateContext?.FrameCounter ?? 0;
            counter++;
            if (counter >= 15) {
                counter = 0;
            }
            if (stateContext != null) {
                stateContext.FrameCounter = counter;
            }

            int cell = counter / 5;
            int row = pose switch {
                CultistPose.CastForward => 10 + cell,
                CultistPose.CastUp => 7 + cell,
                CultistPose.Scream => 13 + cell,
                CultistPose.Stand => 0,
                _ => 4 + cell,
            };
            npc.frame.Y = row * frameHeight;
            return false;
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }

            //原版贴图懒加载守卫
            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = npc.frame;
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 drawPos = npc.Center - screenPos;
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float opacity = 1f - npc.alpha / 255f;

            //舞台法阵（入场/撤离/死亡演出的地面大阵）：先于透明度早退，隐身阶段也要画
            if (stateContext.StageSigilProgress > 0.01f) {
                CultistRenderHelper.DrawSigil(spriteBatch, stateContext.StageSigilPos,
                    stateContext.StageSigilRadius, stateContext.Element,
                    stateContext.StageSigilProgress, stateContext.StageSigilSpin,
                    stateContext.StageSigilFlash, stateContext.StageSigilBreak, 1f);
            }

            if (opacity <= 0.01f) {
                return false;
            }

            //博弈窗口真身基座阵（与分身同构；饱和主色 vs 分身灰调=可学习破绽的镜面）
            CultistStateIndex selfState = (CultistStateIndex)(int)npc.ai[2];
            if (selfState is CultistStateIndex.MirrorBlink or CultistStateIndex.GrandRitual) {
                float pedestalFlash = stateContext.StaggerTimer > 0 ? 0.6f : 0f;
                CultistRenderHelper.DrawSigil(spriteBatch, npc.Bottom + new Vector2(0f, 26f), 64f, stateContext.Element,
                    opacity, Main.GameUpdateCount * 0.03f, pedestalFlash, 0f, 0.7f * opacity);
            }

            Color main = CultistPalette.Main(stateContext.Element);
            Color bright = CultistPalette.Bright(stateContext.Element);

            //残影（施法辉光越高越明显）
            float ghostStrength = MathHelper.Clamp(stateContext.CastGlow + npc.velocity.Length() * 0.03f, 0f, 1f);
            if (ghostStrength > 0.08f) {
                for (int i = 1; i < npc.oldPos.Length; i += 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float fade = (1f - i / (float)npc.oldPos.Length) * 0.3f * ghostStrength * opacity;
                    Vector2 gp = npc.oldPos[i] + npc.Size / 2f - screenPos;
                    spriteBatch.Draw(texture, gp, frameRec, main with { A = 0 } * fade,
                        npc.rotation, origin, npc.scale, flip, 0f);
                }
            }

            //元素光环底衬
            if (stateContext.ElementAura > 0.02f && opacity > 0.2f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                spriteBatch.Draw(glow, drawPos, null, main with { A = 0 } * (0.35f * stateContext.ElementAura * opacity),
                    0f, glow.Size() / 2f, 1.6f, SpriteEffects.None, 0f);
            }

            //本体
            spriteBatch.Draw(texture, drawPos, frameRec, drawColor * opacity,
                npc.rotation, origin, npc.scale, flip, 0f);

            //施法辉光描边（叠加同贴图微放大）
            if (stateContext.CastGlow > 0.05f) {
                spriteBatch.Draw(texture, drawPos, frameRec, bright with { A = 0 } * (0.5f * stateContext.CastGlow * opacity),
                    npc.rotation, origin, npc.scale * 1.04f, flip, 0f);
            }

            //破绽硬直：金白呼吸描边，提示玩家输出窗口
            if (stateContext.StaggerTimer > 0) {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f);
                spriteBatch.Draw(texture, drawPos, frameRec, new Color(255, 235, 160, 0) * (0.45f * pulse * opacity),
                    npc.rotation, origin, npc.scale * 1.07f, flip, 0f);
            }

            //狂暴：绯红怒焰描边（免伤警示，快频呼吸）
            if (stateContext.Enraged) {
                float rage = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f);
                spriteBatch.Draw(texture, drawPos, frameRec, new Color(255, 60, 50, 0) * (0.55f * rage * opacity),
                    npc.rotation, origin, npc.scale * 1.09f, flip, 0f);
                spriteBatch.Draw(texture, drawPos, frameRec, new Color(255, 160, 90, 0) * (0.3f * rage * opacity),
                    npc.rotation, origin, npc.scale * 1.045f, flip, 0f);
            }

            return false;
        }
        #endregion

        #region 死亡与存活
        public override bool CheckActive() => false;

        /// <summary>演出中锁血；演出完放行真死（原版死亡事件触发四柱）</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
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

        #region 群体管理（服务端静态辅助）

        /// <summary>补齐分身编制，缺员在本体身后补位生成</summary>
        internal static void EnsureClones(CultistStateContext context, int count) {
            if (VaultUtils.isClient) {
                return;
            }
            context.RefreshClones();
            NPC boss = context.Npc;
            int need = count - context.Clones.Count;
            for (int i = 0; i < need; i++) {
                Vector2 pos = boss.Center + new Vector2(Main.rand.NextFloat(-120f, 120f), Main.rand.NextFloat(-80f, 20f));
                //阵位索引错开，护法编队不叠站（镜像/仪式态进场时会再重排）
                float slot = context.Clones.Count + i;
                int idx = NPC.NewNPC(boss.GetSource_FromAI(), (int)pos.X, (int)pos.Y,
                    NPCID.CultistBossClone, 0, ai0: slot, ai1: Main.rand.Next(1000), ai2: 0f, ai3: boss.whoAmI);
                if (idx >= 0 && idx < Main.maxNPCs) {
                    Main.npc[idx].netUpdate = true;
                }
            }
            context.RefreshClones();
        }

        /// <summary>清空分身（亮片破灭）</summary>
        internal static void DismissClones(CultistStateContext context) {
            if (VaultUtils.isClient) {
                return;
            }
            context.RefreshClones();
            foreach (var clone in context.Clones) {
                CultistCloneAI.MarkHarmlessDeath(clone);
            }
            context.Clones.Clear();
        }

        /// <summary>清扫事件仆从（死亡/撤离收尾），服务端</summary>
        internal static void CleanupMinions(bool includeDragons) {
            if (VaultUtils.isClient) {
                return;
            }
            foreach (var n in Main.ActiveNPCs) {
                bool isServant = n.type == NPCID.CultistBossClone
                    || n.type == NPCID.AncientLight || n.type == NPCID.AncientDoom;
                bool isDragon = n.type >= NPCID.CultistDragonHead && n.type <= NPCID.CultistDragonTail;
                bool isVision = n.type == NPCID.AncientCultistSquidhead;
                if (isServant || (includeDragons && (isDragon || isVision))) {
                    n.life = 0;
                    n.HitEffect();
                    n.active = false;
                    n.netUpdate = true;
                }
            }
        }

        /// <summary>清空本Boss的敌对弹幕（转阶段/死亡公平阀），服务端</summary>
        internal static void ClearHostileProjectiles() {
            if (VaultUtils.isClient) {
                return;
            }
            foreach (var p in Main.ActiveProjectiles) {
                if (!p.hostile) {
                    continue;
                }
                if (p.ModProjectile != null
                    && p.ModProjectile.Mod == CWRMod.Instance
                    && p.ModProjectile.GetType().Namespace?.Contains("BrutalLunaticCultist") == true) {
                    p.Kill();
                }
            }
        }

        /// <summary>服务端瞬移+各端表现由位置同步兜底</summary>
        internal static void BlinkTo(CultistStateContext context, Vector2 target) {
            NPC npc = context.Npc;
            CultistRenderHelper.BlinkOut(npc.Center, context.Element);
            npc.Center = target;
            npc.velocity = Vector2.Zero;
            npc.netUpdate = true;
            CultistRenderHelper.BlinkIn(npc.Center, context.Element);
        }

        #endregion
    }
}
