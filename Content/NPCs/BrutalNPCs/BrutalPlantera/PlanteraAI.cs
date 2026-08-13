using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera
{
    /// <summary>世纪之花主控：藤蔓悬吊运动+状态机，钩爪/触手/孢子为部件</summary>
    internal class PlanteraAI : CWRNPCOverride, ICWRLoader
    {
        #region Data
        public override int TargetID => NPCID.Plantera;

        private VaultStateMachine<PlanteraStateContext> stateMachine;
        private PlanteraStateContext stateContext;
        private Player targetPlayer;

        /// <summary>供部件/弹幕读主控状态索引</summary>
        internal static PlanteraStateIndex GetStateIndex(NPC plantera) => (PlanteraStateIndex)(int)plantera.ai[2];

        /// <summary>找活着的世纪之花本体</summary>
        internal static NPC FindBoss() {
            if (NPC.plantBoss >= 0 && NPC.plantBoss < Main.maxNPCs) {
                NPC boss = Main.npc[NPC.plantBoss];
                if (boss.active && boss.type == NPCID.Plantera) {
                    return boss;
                }
            }
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.Plantera) {
                    return n;
                }
            }
            return null;
        }
        #endregion

        #region 加载与初始化
        void ICWRLoader.UnLoadData() => PlanteraScreenFX.Clear();

        public override void SetProperty() {
            InitializeStateContext();
        }

        public override bool? CanCWROverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new PlanteraStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<PlanteraStateContext>(stateContext, aiSlot: 2);

            //客户端从ai[2]恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<PlanteraStateContext> syncedState = VaultStateRegistry<PlanteraStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new PlanteraIntroState());
            }
            else {
                stateMachine.SetInitialState(new PlanteraIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            //部件与原版画藤都依赖这个静态索引
            NPC.plantBoss = npc.whoAmI;

            FindTarget();
            UpdateStateContext();
            CheckPhaseTriggers();

            //每帧重声明的表现数据，未声明回落基线
            stateContext.GlowPulse = stateContext.IsPhase2 ? 0.32f : 0.22f;
            stateContext.BodyScalePulse = 0.012f * (float)Math.Sin(stateContext.SwayPhase * 1.7f);
            stateContext.RotationMode = 0;

            //每帧基线，状态在Update里覆盖
            ApplyBaselineStats();

            stateMachine?.Update();

            if (!stateContext.SkipDefaultMovement) {
                UpdateSuspension();
            }

            UpdateRotation();
            UpdateAmbientDressing();

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not PlanteraDespawnState and not PlanteraDeathState) {
                    stateMachine?.ChangeState(new PlanteraDespawnState());
                }
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            stateContext.IsLowLife = npc.life < npc.lifeMax * PlanteraDirector.NovaLifeRatio;

            //激怒：目标出丛林或上地表(原版规则)
            bool surface = targetPlayer.position.Y < Main.worldSurface * 16.0;
            bool underworld = targetPlayer.position.Y > Main.UnderworldLayer * 16;
            stateContext.IsEnraged = !CWRRef.GetBossRushActive() && (!targetPlayer.ZoneJungle || surface || underworld);

            //阶段标记：权威端 context→ai[3]；客户端从 ai[3] 单向收养(中途加入/重建也能对上阶段)
            if (VaultUtils.isClient) {
                if (npc.ai[3] > 0.5f) {
                    stateContext.IsPhase2 = true;
                }
            }
            else {
                npc.ai[3] = stateContext.IsPhase2 ? 1f : 0f;
            }

            if (Main.GameUpdateCount % 10 == 0) {
                stateContext.RefreshParts();
            }
        }

        /// <summary>转阶段/大招/死亡演出触发，权威端裁决</summary>
        private void CheckPhaseTriggers() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }

            bool inCinematic = stateMachine.CurrentState is PlanteraIntroState or PlanteraPhaseTransitionState
                or PlanteraBloomNovaState or PlanteraDeathState or PlanteraDespawnState;

            //死亡演出
            if (!stateContext.DeathPerformanceFinished
                && stateMachine.CurrentState is not PlanteraDeathState and not PlanteraDespawnState
                && npc.life <= PlanteraDirector.DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new PlanteraDeathState());
                return;
            }

            if (inCinematic) {
                return;
            }

            //蜕壳
            if (!stateContext.IsPhase2 && npc.life <= npc.lifeMax / 2) {
                stateMachine.ChangeState(new PlanteraPhaseTransitionState());
                return;
            }

            //凋零绽放
            if (stateContext.IsPhase2 && !stateContext.NovaUsed && stateContext.IsLowLife) {
                stateMachine.ChangeState(new PlanteraBloomNovaState());
            }
        }

        /// <summary>每帧基线伤害/防御，状态可覆盖</summary>
        private void ApplyBaselineStats() {
            stateContext.SkipDefaultMovement = false;
            npc.dontTakeDamage = false;

            float damageMult = stateContext.IsPhase2 ? 1.4f : 1f;
            if (stateContext.IsEnraged) {
                damageMult *= 2f;
            }
            npc.damage = (int)(npc.defDamage * damageMult);
            npc.defense = stateContext.IsPhase2 ? 10 : 36;
            if (stateContext.IsEnraged) {
                npc.defense *= stateContext.IsPhase2 ? 4 : 2;
            }
        }
        #endregion

        #region 悬吊运动
        /// <summary>藤蔓悬吊：身体被拉向钩爪质心+目标偏移，弹簧+摆动</summary>
        private void UpdateSuspension() {
            Vector2 centroid = stateContext.HookCentroid();
            Vector2 toPlayer = targetPlayer.Center - centroid;
            float leash = stateContext.IsPhase2 ? PlanteraDirector.LeashP2 : PlanteraDirector.LeashP1;

            if (toPlayer.Length() > leash) {
                toPlayer = toPlayer.SafeNormalize(Vector2.Zero) * leash;
            }

            Vector2 desired = centroid + toPlayer + stateContext.SuspendOffset;
            Vector2 delta = desired - npc.Center;
            float dist = delta.Length();

            float speed = stateContext.MoveSpeed;
            float accel = stateContext.AccelRate;
            if (stateContext.IsEnraged) {
                speed += PlanteraDirector.EnrageSpeedBonus;
                accel = Math.Max(accel, 0.12f);
            }
            if (stateContext.IsDeathMode) {
                speed *= 1.2f;
            }

            //远距追赶阀，防脱节
            if (dist > 900f) {
                speed = Math.Max(speed, Math.Min(dist / 26f, 34f));
                accel = Math.Max(accel, 0.08f);
            }

            //弹簧趋近
            Vector2 targetVel = dist > 4f
                ? delta.SafeNormalize(Vector2.Zero) * Math.Min(dist * 0.08f, speed)
                : Vector2.Zero;
            npc.velocity = Vector2.Lerp(npc.velocity, targetVel, accel);

            //悬吊摆动，慢速时明显
            stateContext.SwayPhase += 0.033f + npc.velocity.Length() * 0.0012f;
            float slack = MathHelper.Clamp(1f - npc.velocity.Length() / 8f, 0f, 1f);
            Vector2 vineDir = (npc.Center - centroid).SafeNormalize(Vector2.UnitY);
            Vector2 swayPerp = vineDir.RotatedBy(MathHelper.PiOver2);
            npc.velocity += swayPerp * (float)Math.Sin(stateContext.SwayPhase) * 0.3f * slack;
        }

        private void UpdateRotation() {
            switch (stateContext.RotationMode) {
                case 0:
                    FaceRotation((targetPlayer.Center - npc.Center).ToRotation() + MathHelper.PiOver2, 0.12f);
                    break;
                case 1:
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                    break;
                    //2 状态自行控制
            }
        }

        private void FaceRotation(float target, float lerp) {
            npc.rotation = npc.rotation.AngleLerp(target, lerp);
        }
        #endregion

        #region 环境装点
        private void UpdateAmbientDressing() {
            //生物荧光照明，二阶段偏品红
            Vector3 lightColor = stateContext.IsPhase2
                ? new Vector3(0.85f, 0.3f, 0.6f)
                : new Vector3(0.5f, 0.75f, 0.3f);
            Lighting.AddLight(npc.Center, lightColor * (0.6f + stateContext.GlowPulse));

            //孢子微光缓升，客户端稀疏
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                PlanteraRenderHelper.SpawnAmbientMote(npc.Center + Main.rand.NextVector2Circular(150f, 130f),
                    stateContext.IsPhase2);
            }
        }
        #endregion

        #region 帧动画
        public override bool FindFrame(int frameHeight) {
            if (stateContext == null) {
                return true;
            }

            //加特林/狂化嚼得快，死亡演出迟滞
            int interval = 6;
            PlanteraStateIndex idx = GetStateIndex(npc);
            if (idx == PlanteraStateIndex.SeedGatling || idx == PlanteraStateIndex.FrenzyPounce) {
                interval = 4;
            }
            else if (idx == PlanteraStateIndex.Death) {
                interval = 10;
            }

            npc.frameCounter++;
            if (npc.frameCounter >= interval) {
                npc.frameCounter = 0;
                npc.frame.Y += frameHeight;
            }

            int baseRow = stateContext.IsPhase2 ? 4 : 0;
            int minY = baseRow * frameHeight;
            int maxY = (baseRow + 3) * frameHeight;
            if (npc.frame.Y < minY || npc.frame.Y > maxY) {
                npc.frame.Y = minY;
            }

            return false;
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }

            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            int frameHeight = texture.Height / Main.npcFrameCount[npc.type];
            Rectangle frameRec = new(0, npc.frame.Y, texture.Width, frameHeight);
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 mainPos = npc.Center - screenPos;
            float scale = npc.scale * (1f + stateContext.BodyScalePulse);

            //蓄力特效画在本体后
            PlanteraRenderHelper.DrawChargeEffect(spriteBatch, stateContext);

            //高速冲撞残影(速度门控，只在猛扑时出现)
            float speedNow = npc.velocity.Length();
            if (speedNow > 20f && npc.Opacity > 0.5f) {
                Color ghostColor = PlanteraRenderHelper.GlowByPhase(stateContext.IsPhase2) with { A = 0 };
                Vector2 back = -npc.velocity.SafeNormalize(Vector2.Zero);
                for (int i = 1; i <= 3; i++) {
                    float fade = (1f - i / 4f) * 0.38f * MathHelper.Clamp((speedNow - 20f) / 26f, 0f, 1f);
                    spriteBatch.Draw(texture, mainPos + back * speedNow * i * 0.55f, frameRec,
                        ghostColor * fade, npc.rotation, origin, scale * (1f - i * 0.03f), SpriteEffects.None, 0f);
                }
            }

            //死亡演出枯萎：颜色抽干成灰褐
            Color bodyColor = drawColor;
            if (stateContext.DeathWilt > 0.01f) {
                Color wilt = new((int)(drawColor.R * 0.55f), (int)(drawColor.G * 0.48f), (int)(drawColor.B * 0.36f));
                bodyColor = Color.Lerp(drawColor, wilt, stateContext.DeathWilt);
            }
            //隐形期(入场地底/撤离)透明度
            bodyColor *= npc.Opacity;

            //本体
            spriteBatch.Draw(texture, mainPos, frameRec, bodyColor,
                npc.rotation, origin, scale, SpriteEffects.None, 0f);

            //生物荧光罩层，加色
            float glow = stateContext.GlowPulse * npc.Opacity;
            if (glow > 0.02f) {
                Color glowColor = stateContext.IsPhase2
                    ? new Color(255, 110, 190, 0)
                    : new Color(170, 255, 120, 0);
                spriteBatch.Draw(texture, mainPos, frameRec, glowColor * glow,
                    npc.rotation, origin, scale * 1.015f, SpriteEffects.None, 0f);
            }

            return false;
        }
        #endregion

        #region 生死钩子
        public override bool CheckActive() => false;

        /// <summary>演出中锁血，完后放行；秒杀也先走演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not PlanteraDeathState) {
                stateMachine.ChangeState(new PlanteraDeathState());
            }

            return false;
        }
        #endregion

        #region 部件管理静态助手
        /// <summary>生成一只钩爪，ordinal 写 ai[3]；服务端调用</summary>
        internal static int SpawnHook(NPC boss, int ordinal) {
            int index = NPC.NewNPC(boss.GetSource_FromAI(), (int)boss.Center.X, (int)boss.Center.Y,
                NPCID.PlanterasHook, boss.whoAmI, ai3: ordinal);
            if (index >= 0 && index < Main.maxNPCs) {
                Main.npc[index].netUpdate = true;
            }
            return index;
        }

        /// <summary>补齐缺失钩爪(演出外兜底)；服务端调用</summary>
        internal static void EnsureHooks(NPC boss) {
            Span<bool> present = stackalloc bool[3];
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.PlanterasHook) {
                    int ord = (int)n.ai[3];
                    if (ord >= 0 && ord < 3) {
                        present[ord] = true;
                    }
                }
            }
            for (int i = 0; i < 3; i++) {
                if (!present[i]) {
                    SpawnHook(boss, i);
                }
            }
        }

        /// <summary>清场所有部件；服务端调用</summary>
        internal static void DespawnParts(bool includeSpores = true) {
            foreach (var n in Main.ActiveNPCs) {
                bool isPart = n.type == NPCID.PlanterasHook || n.type == NPCID.PlanterasTentacle
                    || (includeSpores && n.type == NPCID.Spore);
                if (isPart) {
                    n.life = 0;
                    n.HitEffect();
                    n.active = false;
                    n.netUpdate = true;
                }
            }
        }
        #endregion
    }
}
