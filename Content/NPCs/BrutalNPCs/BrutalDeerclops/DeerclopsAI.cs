using CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Deerclops;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops
{
    /// <summary>独眼巨鹿主控：冰封领域与暗影凝视</summary>
    internal class DeerclopsAI : BrutalNPCOverride
    {
        #region 数据
        public override int TargetID => NPCID.Deerclops;

        /// <summary>npc.ai[0] 位标：第二阶段</summary>
        internal const int FlagPhase2 = 1;
        /// <summary>npc.ai[0] 位标：白澈已用</summary>
        internal const int FlagWhiteoutUsed = 2;

        /// <summary>life低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;
        /// <summary>暗影护盾触发距离</summary>
        internal const float ShieldFarDistance = 640f;

        //帧序(照抄原版 _deerclopsAttack*Frames)
        private static readonly int[] StompSeq = [12, 13, 14, 13, 14, 13, 14, 13, 14, 15, 16, 17];
        private static readonly int[] ScoopSeq = [12, 15, 16, 17, 17, 17, 17, 13, 18, 18, 18, 18, 12];
        private static readonly int[] RoarSeq = [19, 20, 21, 22, 21, 22, 21, 22, 23, 24, 23, 24, 23, 24, 20, 19];

        private VaultStateMachine<DeerclopsStateContext> stateMachine;
        private DeerclopsStateContext stateContext;
        private Player targetPlayer;
        /// <summary>暴风雪瞬步阀计时</summary>
        private int stepValveTimer;
        /// <summary>上帧中心，用于瞬步检测与FX</summary>
        private Vector2 prevCenter;

        internal DeerclopsStateContext StateContext => stateContext;
        internal IVaultState<DeerclopsStateContext> CurrentState => stateMachine?.CurrentState;
        #endregion

        #region 加载与初始化
        public override void SetProperty() {
            //冲撞残影用 oldPos
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 10;
            InitializeStateContext();
        }

        public override bool? CanBrutalOverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new DeerclopsStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<DeerclopsStateContext>(stateContext, aiSlot: 2);

            //客户端从ai[2]恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<DeerclopsStateContext> syncedState = VaultStateRegistry<DeerclopsStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new DeerclopsIntroState());
            }
            else {
                stateMachine.SetInitialState(new DeerclopsIntroState());
            }
            prevCenter = npc.Center;
        }
        #endregion

        #region 主AI
        public override bool AI() {
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            //保持原版全局标记(音乐/血条等系统消费)
            NPC.deerclopsBoss = npc.whoAmI;

            FindTarget();
            UpdateStateContext();
            CheckDeathPerformanceTrigger();

            //每帧重声明的命令，未声明回落默认
            stateContext.HaltMovement = false;
            stateContext.AnimMode = DeerAnimMode.Locomotion;
            stateContext.GazePhase = 0;
            stateContext.VeilTarget = stateContext.IsPhase2 ? 0.7f : 0.45f;
            stateContext.Whiteout = Math.Max(stateContext.Whiteout - 0.02f, 0f);
            stateContext.EyeGlow = Math.Max(stateContext.EyeGlow - 0.05f, stateContext.IsPhase2 ? 0.35f : 0.15f);
            stateContext.EyeHeat = MathHelper.Lerp(stateContext.EyeHeat, stateContext.IsPhase2 ? 0.65f : 0f, 0.04f);

            stateMachine?.Update();

            //物理(可跳过)
            if (!stateContext.SkipDefaultMovement) {
                DeerclopsMotion.Walk(npc, stateContext, stateContext.HaltMovement);
            }

            UpdateShadowShield();
            UpdateBlizzardStepValve();
            DetectStepFlashFx();
            UpdateVisuals();

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            prevCenter = npc.Center;
            return false;
        }
        #endregion

        #region 上下文与目标
        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();

            //阶段位标自 ai[0] 反解(各端一致)
            int flags = (int)npc.ai[0];
            stateContext.IsPhase2 = (flags & FlagPhase2) != 0;
            stateContext.PhaseRoarDone = stateContext.IsPhase2;
            stateContext.WhiteoutUsed = (flags & FlagWhiteoutUsed) != 0;
        }

        /// <summary>服务端置位阶段位标</summary>
        internal static void SetFlag(NPC npc, int flag) {
            npc.ai[0] = (int)npc.ai[0] | flag;
            npc.netUpdate = true;
        }

        /// <summary>清空本boss的敌对弹幕(转阶段/死亡公平阀，服务端)</summary>
        internal static void ClearHostileProjectiles() {
            if (VaultUtils.isClient) {
                return;
            }
            int spike = ModContent.ProjectileType<Projectiles.DeerIceSpikeProj>();
            int pulse = ModContent.ProjectileType<Projectiles.DeerFrostPulseProj>();
            int rubble = ModContent.ProjectileType<Projectiles.DeerRubbleProj>();
            int hand = ModContent.ProjectileType<Projectiles.DeerShadowHandProj>();
            int seize = ModContent.ProjectileType<Projectiles.DeerSeizeHandProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == spike || proj.type == pulse || proj.type == rubble || proj.type == hand || proj.type == seize) {
                    proj.Kill();
                }
            }
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not DeerclopsDespawnState and not DeerclopsDeathState) {
                    stateMachine?.ChangeState(new DeerclopsDespawnState());
                }
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
            if (stateMachine.CurrentState is DeerclopsDeathState or DeerclopsDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new DeerclopsDeathState());
            }
        }
        #endregion

        #region 暗影护盾（领域机制：远离者被暗影拒斥）
        private void UpdateShadowShield() {
            bool combatState = stateMachine?.CurrentState is DeerclopsStateBase state
                && state.StateIndex is not DeerclopsStateIndex.Intro
                    and not DeerclopsStateIndex.PhaseRoar
                    and not DeerclopsStateIndex.Despawn
                    and not DeerclopsStateIndex.Death;

            if (!combatState || !targetPlayer.Alives()) {
                stateContext.ShadowShield = Math.Max(stateContext.ShadowShield - 2f, 0f);
                return;
            }

            //白澈领域收紧安全圈
            float farDist = stateContext.Whiteout > 0.5f ? 520f : ShieldFarDistance;
            bool far = npc.Distance(targetPlayer.Center) >= farDist;
            stateContext.ShadowShield = MathHelper.Clamp(stateContext.ShadowShield + (far ? 1f : -1f), 0f, 30f);
            npc.dontTakeDamage = stateContext.ShadowShield >= 30f;

            //阴影侵蚀粒子(本端)
            if (!Main.dedServ && stateContext.ShadowShield > 0f) {
                float ramp = stateContext.ShadowShield / 30f;
                float amount = Main.rand.NextFloat() * ramp * 3f;
                while (amount > 0f) {
                    amount -= 1f;
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Shadowflame, 0f, -3f, 0, default, 1.4f);
                    dust.noGravity = true;
                }
            }

            //满盾时压边手把远离者赶回来(服务端)
            if (!VaultUtils.isClient && stateContext.ShadowShield >= 30f && Main.GameUpdateCount % 64 == 0) {
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.Alives()) {
                        continue;
                    }
                    float dist = player.Distance(npc.Center);
                    if (dist < farDist || dist > 2600f) {
                        continue;
                    }
                    Projectiles.DeerShadowHandProj.SpawnBorderHand(npc, player);
                }
            }
        }
        #endregion

        #region 暴风雪瞬步阀（仅潜行态卡地形/超距时回归）
        private void UpdateBlizzardStepValve() {
            if (stateMachine?.CurrentState is not DeerclopsStateBase state || !state.AllowBlizzardStep) {
                stepValveTimer = 0;
                return;
            }
            if (!targetPlayer.Alives()) {
                stepValveTimer = 0;
                return;
            }

            float dist = npc.Distance(targetPlayer.Center);
            bool stuck = Math.Abs(npc.Center.X - prevCenter.X) < 1.6f && dist > 780f
                && !stateContext.HaltMovement && !stateContext.SkipDefaultMovement;

            if (dist > 2400f) {
                stepValveTimer += 2;
            }
            else if (stuck) {
                stepValveTimer++;
            }
            else {
                stepValveTimer = Math.Max(stepValveTimer - 2, 0);
            }

            if (stepValveTimer < 110) {
                return;
            }
            stepValveTimer = 0;

            if (VaultUtils.isClient) {
                return;
            }

            //落点：目标移动来向一侧的地面(截断走位)
            int side = targetPlayer.velocity.X > 0.5f ? 1 : targetPlayer.velocity.X < -0.5f ? -1
                : (npc.Center.X < targetPlayer.Center.X ? -1 : 1);
            Vector2 ground = DeerclopsMotion.FindGroundBelow(targetPlayer.Center + new Vector2(side * 470f, -40f));
            npc.Bottom = ground - new Vector2(0f, 2f);
            npc.velocity = Vector2.Zero;
            npc.netUpdate = true;
        }

        /// <summary>单帧大位移=瞬步，两端各自补FX(位置包自然同步)</summary>
        private void DetectStepFlashFx() {
            float delta = Vector2.Distance(npc.Center, prevCenter);
            if (delta < 380f || Main.dedServ) {
                return;
            }
            if (stateMachine?.CurrentState is DeerclopsDeathState or DeerclopsIntroState) {
                return;
            }
            DeerclopsVeilFX.SpawnStepBurst(prevCenter);
            DeerclopsVeilFX.SpawnStepBurst(npc.Center);
        }
        #endregion

        #region 视觉推送
        private void UpdateVisuals() {
            //独眼冷光照明
            float glow = MathHelper.Clamp(stateContext.EyeGlow, 0f, 1f);
            Color eyeColor = Color.Lerp(DeerclopsMotion.ColdWhite, DeerclopsMotion.GazeRed, stateContext.EyeHeat);
            Lighting.AddLight(EyeWorldPos(), eyeColor.ToVector3() * (0.35f + glow * 0.8f));

            if (!Main.dedServ) {
                DeerclopsVeilFX.Push(npc, stateContext);
            }
        }

        /// <summary>独眼世界坐标(近似，头部随帧微移不追)</summary>
        internal Vector2 EyeWorldPos() {
            return npc.Bottom + new Vector2(npc.spriteDirection * 26f, -138f) * npc.scale;
        }

        /// <summary>本地玩家是否面向此NPC(凝视判定用，逐端结算)</summary>
        internal static bool LocalPlayerFacing(NPC npc, float maxDist) {
            Player lp = Main.LocalPlayer;
            if (!lp.active || lp.dead || lp.creativeGodMode) {
                return false;
            }
            float dx = npc.Center.X - lp.Center.X;
            if (Math.Abs(dx) <= 24f) {
                return false;
            }
            if (lp.Distance(npc.Center) > maxDist) {
                return false;
            }
            return Math.Sign(dx) == lp.direction;
        }
        #endregion

        #region 帧动画接管（原版FindFrame读ai[0]选帧，必须全接管）
        public override bool FindFrame(int frameHeight) {
            if (npc.IsABestiaryIconDummy) {
                return true;
            }
            if (stateContext == null) {
                return false;
            }

            int frameY = npc.frame.Y;
            switch (stateContext.AnimMode) {
                case DeerAnimMode.Stomp:
                    frameY = SeqFrame(StompSeq, stateContext.AnimTimer);
                    npc.spriteDirection = npc.direction;
                    break;
                case DeerAnimMode.Scoop:
                    frameY = SeqFrame(ScoopSeq, stateContext.AnimTimer);
                    npc.spriteDirection = npc.direction;
                    break;
                case DeerAnimMode.Roar:
                    frameY = SeqFrame(RoarSeq, stateContext.AnimTimer);
                    npc.spriteDirection = npc.direction;
                    break;
                case DeerAnimMode.Crouch:
                    frameY = 1;
                    npc.frameCounter = 0.0;
                    break;
                default: {
                    //原版步行逻辑
                    if (npc.velocity.Y == 0f) {
                        npc.spriteDirection = npc.direction;
                    }
                    if (npc.velocity.Y > 0f || npc.localAI[0] == 1f) {
                        npc.frameCounter = 0.0;
                        frameY = 1;
                    }
                    else if (npc.velocity.X == 0f) {
                        npc.frameCounter = 0.0;
                        frameY = 0;
                    }
                    else {
                        //步频封顶：冲刺速度下不至于腿部残影化
                        npc.frameCounter += Math.Min(Math.Abs(npc.velocity.X), 9f);
                        if (npc.frameCounter >= 150.0 || npc.frameCounter < 0.0) {
                            npc.frameCounter = 0.0;
                        }
                        int walkFrame = 2 + (int)(npc.frameCounter / 15.0);
                        if (walkFrame > 11) {
                            walkFrame = 11;
                        }
                        //落脚帧踏步声
                        if (walkFrame != frameY && (walkFrame == 4 || walkFrame == 9) && !Main.dedServ) {
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.DeerclopsStep, npc.Bottom);
                        }
                        frameY = walkFrame;
                    }
                    break;
                }
            }

            npc.frame.Y = frameY;
            return false;
        }

        private static int SeqFrame(int[] seq, int animTimer) {
            int idx = animTimer / 4;
            if (idx >= seq.Length) {
                idx = seq.Length - 1;
            }
            if (idx < 0) {
                idx = 0;
            }
            return seq[idx];
        }
        #endregion

        #region 绘制（原版为5x5网格特判绘制，全接管）
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (npc.IsABestiaryIconDummy || stateContext == null) {
                return null;
            }

            Main.instance.LoadNPC(npc.type);
            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Rectangle rect = tex.Frame(5, 5, npc.frame.Y / 5, npc.frame.Y % 5, 2, 2);
            Vector2 origin = rect.Size() * new Vector2(0.5f, 1f);
            origin.Y -= 4f;
            origin.X = npc.spriteDirection == 1 ? 106f : rect.Width - 106f;
            Vector2 drawPos = npc.Bottom - screenPos;
            SpriteEffects fx = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float rotation = npc.rotation + stateContext.BodyLean * npc.spriteDirection;
            float dissolve = MathHelper.Clamp(stateContext.Dissolve, 0f, 1f);

            //冲撞高速残影
            float speed = Math.Abs(npc.velocity.X);
            if (speed > 13f && npc.oldPos != null) {
                float ghostAlpha = MathHelper.Clamp((speed - 13f) / 14f, 0f, 1f) * 0.5f;
                for (int i = 2; i < 8 && i < npc.oldPos.Length; i += 2) {
                    Vector2 ghostPos = npc.oldPos[i] + new Vector2(npc.width * 0.5f, npc.height) - screenPos;
                    Color ghostColor = DeerclopsMotion.IceBlue with { A = 0 } * (ghostAlpha * (1f - i / 8f)) * (1f - dissolve);
                    spriteBatch.Draw(tex, ghostPos, rect, ghostColor, rotation, origin, npc.scale, fx, 0f);
                }
            }

            //暗影护盾环影(原版免伤视觉语言)
            float shieldRamp = stateContext.ShadowShield / 30f;
            Color bodyColor = npc.GetAlpha(drawColor);
            if (shieldRamp > 0f) {
                float wobble = shieldRamp * shieldRamp;
                Color ghost = new Color(80, 0, 0, 255) * 0.5f * wobble * (1f - dissolve);
                for (int i = 0; i < 2; i++) {
                    Vector2 orbit = new Vector2(0f, 1f).RotatedBy(i * MathHelper.Pi + Main.GlobalTimeWrappedHourly * 10f) * wobble * 20f;
                    spriteBatch.Draw(tex, drawPos + orbit, rect, ghost, rotation, origin, npc.scale, fx, 0f);
                }
                bodyColor = Color.Lerp(bodyColor, new Color(50, 0, 160), MathHelper.Clamp(shieldRamp * 1.5f, 0f, 1f));
                bodyColor *= 1f - wobble * 0.5f;
            }

            //第二阶段霜甲底衬(加法冷蓝，画在主体之下)
            if (stateContext.IsPhase2 && dissolve < 0.9f) {
                Color rim = DeerclopsMotion.DeepIce with { A = 0 } * (0.2f * (1f - dissolve));
                spriteBatch.Draw(tex, drawPos - new Vector2(0f, 2f), rect, rim, rotation, origin, npc.scale * 1.012f, fx, 0f);
            }

            //主体：溶解先压成暗影剪影再化透明(入场=雪幕中的黑影，退场=影散)
            Color silhouette = Color.Lerp(bodyColor, new Color(16, 20, 34), dissolve);
            float bodyAlpha = 1f - dissolve * dissolve;
            spriteBatch.Draw(tex, drawPos, rect, silhouette * bodyAlpha, rotation, origin, npc.scale, fx, 0f);

            //独眼辉光
            DrawEye(spriteBatch, screenPos, dissolve);

            return false;
        }

        private void DrawEye(SpriteBatch spriteBatch, Vector2 screenPos, float dissolve) {
            float glow = MathHelper.Clamp(stateContext.EyeGlow, 0f, 1f);
            if (glow <= 0.02f || dissolve >= 1f) {
                return;
            }
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D glintTex = CWRAsset.StarGlow01.Value;
            Vector2 eyePos = EyeWorldPos() - screenPos;
            Color eyeColor = Color.Lerp(DeerclopsMotion.ColdWhite, DeerclopsMotion.GazeRed, stateContext.EyeHeat) with { A = 0 };
            float pulse = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
            float fade = (1f - dissolve) * glow;

            spriteBatch.Draw(glowTex, eyePos, null, eyeColor * (0.85f * fade), 0f,
                glowTex.Size() / 2f, 0.5f * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(glowTex, eyePos, null, Color.White with { A = 0 } * (0.5f * fade), 0f,
                glowTex.Size() / 2f, 0.22f * pulse, SpriteEffects.None, 0f);
            //高热时十字glint
            if (glow > 0.55f) {
                float glint = (glow - 0.55f) / 0.45f;
                spriteBatch.Draw(glintTex, eyePos, null, eyeColor * (0.7f * glint * fade), 0f,
                    glintTex.Size() / 2f, 0.4f * glint * pulse, SpriteEffects.None, 0f);
            }
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
        #endregion

        #region 生死
        public override bool CheckActive() => false;

        /// <summary>演出中锁血，完后放行；秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not DeerclopsDeathState) {
                stateMachine.ChangeState(new DeerclopsDeathState());
            }

            return false;
        }

        /// <summary>残酷遗物掉落：独眼巨鹿无宝藏袋，本路径全难度通用，残酷世界必掉</summary>
        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.ByCondition(new DropInBrutalMode(),
                ModContent.ItemType<WhiteoutStormCore>()));
        }
        #endregion
    }
}
