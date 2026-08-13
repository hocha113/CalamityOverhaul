using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron
{
    /// <summary>猪龙鱼公爵主控：风暴海啸领域，InnoVault 状态机驱动，ai[2] 同步</summary>
    internal class DukeFishronAI : CWRNPCOverride, ICWRLoader, ILocalizedModType
    {
        #region 数据
        public override int TargetID => NPCID.DukeFishron;

        public string LocalizationCategory => "BrutalNPCs";
        public static LocalizedText DukeFishron_Frenzy_Text { get; private set; }
        public static LocalizedText DukeFishron_Nightfall_Text { get; private set; }
        public static LocalizedText DukeFishron_Maelstrom_Text { get; private set; }
        public static LocalizedText DukeFishron_Despawn_Text { get; private set; }

        /// <summary>life 低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;
        /// <summary>目标失效判定距离，镜像原版 5600</summary>
        private const float MaxFindDistance = 5600f;

        /// <summary>死亡演出中的本体 whoAmI，无则 -1（运镜观察用）</summary>
        internal static int ActivePerformanceBoss = -1;

        private VaultStateMachine<FishronStateContext> stateMachine;
        private FishronStateContext stateContext;
        private Player targetPlayer;
        /// <summary>远距滞留帧，达上限触发回归瞬移</summary>
        private int farTimer;

        /// <summary>是否处于死亡演出（运镜观察用）</summary>
        internal bool InDeathPerformance => stateMachine?.CurrentState is FishronDeathState;
        #endregion

        #region 加载与初始化
        public override void SetStaticDefaults() {
            DukeFishron_Frenzy_Text = this.GetLocalization(nameof(DukeFishron_Frenzy_Text),
                () => "海在他身后立起来了。");
            DukeFishron_Nightfall_Text = this.GetLocalization(nameof(DukeFishron_Nightfall_Text),
                () => "天黑了。雨里有什么在动。");
            DukeFishron_Maelstrom_Text = this.GetLocalization(nameof(DukeFishron_Maelstrom_Text),
                () => "他把整场风暴攥进了鳍里。");
            DukeFishron_Despawn_Text = this.GetLocalization(nameof(DukeFishron_Despawn_Text),
                () => "浪退了，海面合拢。");
        }

        void ICWRLoader.UnLoadData() {
            FishronTideTrailProj.UnloadTrails();
            FishronStormSky.Clear();
            ActivePerformanceBoss = -1;
        }

        public override void SetProperty() {
            //oldPos 残影缓存
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 14;
            npc.knockBackResist = 0f;
            InitializeStateContext();
        }

        public override bool? CanCWROverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new FishronStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<FishronStateContext>(stateContext, aiSlot: 2);

            //客户端从 ai[2] 恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<FishronStateContext> syncedState = VaultStateRegistry<FishronStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new FishronIntroState());
            }
            else {
                stateMachine.SetInitialState(new FishronIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
            npc.netOffset = Vector2.Zero;
            npc.dontTakeDamage = false;
            npc.chaseable = true;

            FindTarget();
            UpdateStateContext();
            UpdatePhaseStats();
            CheckDeathPerformanceTrigger();

            //每帧重声明，未声明回落默认
            stateContext.FrameCommand = 0;
            stateContext.StormBoost = 0f;
            //隐身声明衰减：状态不再维持时自动显形
            if (npc.alpha > 0) {
                npc.alpha = Math.Max(0, npc.alpha - 12);
            }

            stateMachine?.Update();

            //常规悬停物理（状态可跳过）
            if (!stateContext.SkipDefaultMovement) {
                UpdateMovement();
            }

            UpdateFarReturnValve();
            UpdateAmbientVisuals();

            //风暴等级上报天空（各端本地）
            FishronStormSky.Report(npc,
                MathHelper.Clamp(CurrentStormGrade(), 0f, 1f));

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        private float CurrentStormGrade() {
            //死亡/退场状态自带覆盖：用 StormBoost 承载相对偏移
            return stateContext.PhaseStormGrade + stateContext.StormBoost;
        }
        #endregion

        #region 上下文与数值
        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();

            //离开海域/太空的原版式激怒
            Player p = targetPlayer;
            stateContext.IsLandEnraged = p != null
                && (p.position.Y < 800f || p.position.Y > Main.worldSurface * 16.0
                || (p.position.X > 6400f && p.position.X < Main.maxTilesX * 16 - 6400));

            //阶段旗标兜底：仅客户端且留滞回余量——正常对局由转阶段演出（ai[2] 同步）落旗，
            //这里只回填中途加入/漏拍的客户端视觉基准；服务端旗标只能由演出状态设置，
            //否则演出会被抢跑成永不触发的死代码
            if (VaultUtils.isClient) {
                float ratio = stateContext.LifeRatio;
                if (ratio < 0.60f) {
                    stateContext.PhaseTwoStarted = true;
                }
                if (ratio < 0.27f) {
                    stateContext.PhaseThreeStarted = true;
                }
            }
        }

        /// <summary>阶段基线数值：接触伤害与防御</summary>
        private void UpdatePhaseStats() {
            float dmgMult = stateContext.Phase == 3 ? 1.1f : stateContext.Phase == 2 ? 1.2f : 1f;
            int defense = stateContext.Phase == 3 ? 0 : stateContext.Phase == 2
                ? (int)(npc.defDefense * 0.8f) : npc.defDefense;
            if (stateContext.IsLandEnraged) {
                dmgMult *= 1.5f;
                defense = npc.defDefense * 2;
            }
            npc.damage = (int)(npc.defDamage * dmgMult);
            npc.defense = defense;
        }

        /// <summary>life≤阈值切死亡演出，服务端驱动</summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is FishronDeathState or FishronDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new FishronDeathState());
            }
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()
                || Vector2.Distance(targetPlayer.Center, npc.Center) > MaxFindDistance) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives() || Vector2.Distance(targetPlayer.Center, npc.Center) > MaxFindDistance) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not FishronDespawnState and not FishronDeathState) {
                    stateMachine?.ChangeState(new FishronDespawnState());
                }
            }
        }
        #endregion

        #region 运动
        /// <summary>默认悬停：SimpleFly 追赶目标点 + 面向玩家</summary>
        private void UpdateMovement() {
            Vector2 toGoal = stateContext.TargetPosition - npc.Center - npc.velocity;
            Vector2 desired = toGoal.SafeNormalize(Vector2.Zero) * stateContext.MoveSpeed;
            npc.SimpleFlyMovement(desired, stateContext.Accel);

            if (targetPlayer.Alives()) {
                FaceBodyDefault(targetPlayer.Center);
            }
        }

        /// <summary>主控层的朝向副本，供默认运动用</summary>
        private void FaceBodyDefault(Vector2 focus) {
            int dir = Math.Sign(focus.X - npc.Center.X);
            if (dir != 0) {
                npc.direction = dir;
                if (npc.spriteDirection != -npc.direction) {
                    npc.rotation += MathHelper.Pi;
                    npc.spriteDirection = -npc.direction;
                }
            }
            float targetRot = (focus - npc.Center).ToRotation();
            if (npc.spriteDirection == 1) {
                targetRot += MathHelper.Pi;
            }
            npc.rotation = npc.rotation.AngleTowards(targetRot, 0.055f);
        }

        /// <summary>远距瞬移回归阀，防止脱战绕圈</summary>
        private void UpdateFarReturnValve() {
            if (stateMachine?.CurrentState is not FishronStateBase state || !state.AllowFarSnap) {
                farTimer = 0;
                return;
            }
            if (!targetPlayer.Alives()) {
                farTimer = 0;
                return;
            }

            float dist = npc.Distance(targetPlayer.Center);
            if (dist <= 3000f) {
                farTimer = 0;
                return;
            }

            if (++farTimer < 40) {
                return;
            }
            farTimer = 0;

            //雨雾中回到视野边
            Vector2 dir = (npc.Center - targetPlayer.Center).SafeNormalize(-Vector2.UnitY);
            FishronMotionFX.SpawnMist(npc.Center, Vector2.Zero, 1.2f, 4);
            npc.Center = targetPlayer.Center + dir * 1150f;
            float speed = Math.Max(npc.velocity.Length(), 18f);
            npc.velocity = -dir * speed;
            FishronMotionFX.SpawnMist(npc.Center, Vector2.Zero, 1.2f, 4);
            npc.netUpdate = true;
        }
        #endregion

        #region 帧与环境表现
        public override bool FindFrame(int frameHeight) {
            if (stateContext == null) {
                return true;
            }
            int count = Main.npcFrameCount[npc.type];
            int cycleFrames = Math.Max(count - 1, 1);

            if (stateContext.FrameCommand == 1) {
                //咆哮/蓄力定帧
                npc.frameCounter = 0;
                npc.frame.Y = frameHeight * Math.Min(2, cycleFrames - 1);
                return false;
            }

            //速度越快摆尾越急
            double cadence = stateContext.FrameCommand == 2
                ? 3.0
                : Math.Max(2.5, 6.0 - npc.velocity.Length() * 0.07);
            npc.frameCounter += 1.0;
            if (npc.frameCounter >= cadence) {
                npc.frameCounter = 0.0;
                npc.frame.Y += frameHeight;
                if (npc.frame.Y >= frameHeight * cycleFrames) {
                    npc.frame.Y = 0;
                }
            }
            return false;
        }

        /// <summary>常驻环境光与三阶段雷附体电弧</summary>
        private void UpdateAmbientVisuals() {
            if (VaultUtils.isServer) {
                return;
            }
            float visible = 1f - npc.alpha / 255f;
            Lighting.AddLight(npc.Center, FishronMotionFX.SeaGreen.ToVector3() * 0.7f * visible);

            //三阶段：风暴附体，体表电弧滋滋作响
            if (stateContext != null && stateContext.PhaseThreeStarted && Main.rand.NextBool(9) && visible > 0.3f) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.42f, npc.height * 0.42f);
                InnoVault.PRT.PRTLoader.NewParticle<CalamityOverhaul.Content.PRTTypes.PRT_Spark>(pos,
                    Main.rand.NextVector2Circular(2.5f, 2.5f) + npc.velocity * 0.2f,
                    FishronMotionFX.StormBolt, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, 10, npc);
            }
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }

            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = npc.frame;
            Vector2 origin = frameRec.Size() / 2f;
            SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float visible = 1f - npc.alpha / 255f;

            //高速残影：速度门控，只有冲刺时刻亮起
            float speed = npc.velocity.Length();
            float ghostIntensity = MathHelper.Clamp((speed - 17f) / 34f, 0f, 1f) * visible;
            if (ghostIntensity > 0.05f) {
                for (int i = 1; i < npc.oldPos.Length; i += 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        break;
                    }
                    float fade = 1f - i / (float)npc.oldPos.Length;
                    Vector2 ghostPos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                    Color ghostColor = new Color(
                        FishronMotionFX.SeaGreen.R, FishronMotionFX.SeaGreen.G, FishronMotionFX.SeaGreen.B, 0)
                        * (ghostIntensity * fade * 0.5f);
                    spriteBatch.Draw(texture, ghostPos, frameRec, ghostColor,
                        npc.rotation, origin, npc.scale, effects, 0f);
                }
            }

            //本体
            Color bodyColor = npc.GetAlpha(drawColor);
            //雨幕隐身期压成剪影
            if (npc.alpha > 40) {
                float veil = npc.alpha / 255f;
                bodyColor = Color.Lerp(bodyColor, new Color(10, 18, 26, bodyColor.A), veil * 0.85f);
            }
            spriteBatch.Draw(texture, npc.Center - screenPos, frameRec, bodyColor,
                npc.rotation, origin, npc.scale, effects, 0f);

            //蓄力/雷附体辉光层
            float glow = 0f;
            Color glowColor = FishronMotionFX.SeaGreen;
            if (stateContext.IsCharging) {
                glow = stateContext.ChargeProgress * 0.65f;
                glowColor = stateContext.ChargeType == 3 ? FishronMotionFX.StormBolt : FishronMotionFX.SeaGreen;
            }
            else if (stateContext.PhaseThreeStarted) {
                glow = 0.3f + 0.14f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7f);
                glowColor = FishronMotionFX.StormBolt;
            }
            if (glow > 0.02f && visible > 0.1f) {
                Color add = new Color(glowColor.R, glowColor.G, glowColor.B, 0) * (glow * visible);
                spriteBatch.Draw(texture, npc.Center - screenPos, frameRec, add,
                    npc.rotation, origin, npc.scale * 1.03f, effects, 0f);
            }

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
        #endregion

        #region 生死与收尾
        public override bool CheckActive() => false;

        /// <summary>演出中锁血，完后放行；秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not FishronDeathState) {
                stateMachine.ChangeState(new FishronDeathState());
            }

            return false;
        }

        /// <summary>
        /// 清场（服务端）：气泡/鲨鱼龙必清；fullSweep 时连同龙卷、水迹、
        /// 海啸、间歇泉与预告线一并撤走——死亡/退场/入夜演出期间不留残余判定
        /// </summary>
        internal static void ClearMinions(bool alsoTornado) {
            if (VaultUtils.isClient) {
                return;
            }
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.DetonatingBubble
                    || n.type == NPCID.Sharkron || n.type == NPCID.Sharkron2) {
                    n.life = 0;
                    n.HitEffect();
                    n.active = false;
                    n.netUpdate = true;
                }
            }
            if (!alsoTornado) {
                return;
            }
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == ModContent.ProjectileType<FishronSharkTornadoProj>()
                    || proj.type == ModContent.ProjectileType<FishronTideTrailProj>()
                    || proj.type == ModContent.ProjectileType<FishronTsunamiWallProj>()
                    || proj.type == ModContent.ProjectileType<FishronGeyserProj>()
                    || proj.type == ModContent.ProjectileType<FishronTelegraph>()) {
                    proj.Kill();
                }
            }
        }
        #endregion
    }
}
