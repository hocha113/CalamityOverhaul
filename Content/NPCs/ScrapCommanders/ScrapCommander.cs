using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders
{
    /// <summary>
    /// 废钢统帅——机械三王战争残料拼成的刑架工头。
    /// 形体承袭鬼奴四械刑架：单 NPC 内部模拟"头 + 四条工具臂"，
    /// 头位服务器权威同步，四臂在各端按肩锚 + 指令做弹簧摆模拟（吊链有重量），
    /// 臂链用原版 Chain22 重链沿悬链弧铺出，贴图全部借用原版（Prime 头与四工具）。
    /// 材质身份：锈红底 + 油黑渍 + 焊橙热光。
    /// 联机契约：转场只在服务器裁决并盖 netUpdate 章（AiSlotNetSync 自动处理），
    /// 各端本地跑同一状态机做表现，节拍全部键控在本地 Timer 上（单调，不回卷），
    /// 弹幕只在权威端生成，粒子音效全走 !dedServ 门。
    /// </summary>
    internal class ScrapCommander : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 臂编制 ====================

        public const int ArmCount = 4;
        public const int ArmSaw = 0;
        public const int ArmVice = 1;
        public const int ArmCannon = 2;
        public const int ArmLaser = 3;

        /// <summary>悬挂队形：锯/钳贴身低垂，炮/镭射外侧高挂</summary>
        internal static readonly Vector2[] RestOffset = {
            new(-78f, 106f),
            new(78f, 106f),
            new(-186f, 24f),
            new(186f, 24f),
        };

        internal static int ArmNpcType(int i) => i switch {
            ArmSaw => NPCID.PrimeSaw,
            ArmVice => NPCID.PrimeVice,
            ArmCannon => NPCID.PrimeCannon,
            _ => NPCID.PrimeLaser,
        };

        //==================== 配色（锈红底 + 油黑渍 + 焊橙热光）====================

        /// <summary>锈色乘法调色：把原版灰机械压成废钢暖锈</summary>
        internal static readonly Color RustMul = new(214, 158, 118);
        /// <summary>链条更深一档的锈</summary>
        internal static readonly Color ChainRustMul = new(188, 132, 96);
        /// <summary>焊缝热橙，加色层专用</summary>
        internal static readonly Color WeldOrange = new(255, 150, 58);
        /// <summary>目镜红</summary>
        internal static readonly Color EyeRed = new(255, 64, 46);
        /// <summary>油渍深色</summary>
        internal static readonly Color OilDark = new(34, 30, 26);
        /// <summary>烟雾灰</summary>
        internal static readonly Color SmokeGray = new(52, 48, 44);

        //==================== 状态机 ====================

        private NpcStateMachine<ScrapStateContext> stateMachine;
        internal ScrapStateContext Context { get; private set; }
        private Player targetPlayer;

        //==================== 臂模拟数据（各端本地重建，头位由同步纠偏）====================

        private readonly Vector2[] armPos = new Vector2[ArmCount];
        private readonly Vector2[] armVel = new Vector2[ArmCount];
        /// <summary>工具旋转（贴图约定：rotation=0 工具口朝下，瞄准 = 方向角 - PiOver2）</summary>
        private readonly float[] armRot = new float[ArmCount];
        private bool armsInit;
        private Vector2 headSim;

        //==================== 本地表现量（不入同步）====================

        private int headFrameTick;
        private int headFrameIndex;
        private int sawFrameTick;
        private int sawFrameIndex;
        /// <summary>突刺链条绷直颤动余帧</summary>
        internal int TautVibe;
        /// <summary>本次突刺的链长收口</summary>
        internal float DartReach = ScrapDirector.DartMaxReach;
        private bool dartThunked;
        /// <summary>炮口余温计时，绘制层热光衰减用</summary>
        internal int CannonHeat;
        /// <summary>锯轮是否高速旋转（状态每帧举旗）</summary>
        internal bool SawSpinning;
        /// <summary>钳爪咬合帧余量（&gt;0 时钳画咬合帧）</summary>
        internal int ViceClampFrames;
        /// <summary>镭射出弹前积光余量（&gt;0 时画枪口预闪与开火帧）</summary>
        internal int LaserFlash;

        //==================== 残影环形缓冲（本地表现）====================

        private const int TrailLen = 14;
        private readonly Vector2[] trailPos = new Vector2[TrailLen];
        private readonly float[] trailRot = new float[TrailLen];
        private int trailHead;
        private bool trailInit;

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        internal float Seed => NPC.whoAmI * 0.7391f;

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.DontDoHardmodeScaling[Type] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
        }

        public override void SetDefaults() {
            NPC.width = 92;
            NPC.height = 92;
            NPC.damage = ScrapDirector.ContactDamage;
            NPC.defense = ScrapDirector.BaseDefense;
            NPC.lifeMax = ScrapDirector.BaseLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.npcSlots = 15f;
            NPC.value = Item.buyPrice(0, 12);
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicID.Boss3;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.ScrapCommander.Bestiary"),
            ]);
        }

        public override void BossHeadSlot(ref int index) {
            //暂借原版机械骷髅王的地图头像
            index = NPCID.Sets.BossHeadTextures[NPCID.SkeletronPrime];
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            //占位掉落：三王残料主题，专属掉落后续另定
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofMight, 1, 12, 20));
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofSight, 1, 12, 20));
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofFright, 1, 12, 20));
            npcLoot.Add(ItemDropRule.Common(ItemID.HallowedBar, 1, 10, 18));
        }

        //==================== 状态机装配 ====================

        private void InitializeStateMachine() {
            Context = new ScrapStateContext {
                Npc = NPC,
                Owner = this,
            };
            stateMachine = new NpcStateMachine<ScrapStateContext>(Context);

            //中途加入的客户端从 ai[3] 恢复状态
            if (VaultUtils.isClient) {
                int syncedIndex = (int)NPC.ai[3];
                IVaultState<ScrapStateContext> synced = VaultStateRegistry<ScrapStateContext>.Create(syncedIndex);
                stateMachine.SetInitialState(synced ?? new ScrapIntroState());
            }
            else {
                stateMachine.SetInitialState(new ScrapIntroState());
            }
        }

        internal ScrapStateIndex CurrentStateIndex => (ScrapStateIndex)(int)NPC.ai[3];

        //==================== 主 AI ====================

        public override void AI() {
            if (stateMachine == null || Context == null) {
                InitializeStateMachine();
            }

            NPC.netOffset = Vector2.Zero;
            NPC.dontTakeDamage = false;
            NPC.damage = 0;

            FindTarget();
            UpdateContextFacts();
            EvaluateGlobalTransitions();

            Context.BeginFrameDefaults();
            stateMachine.Update();

            UpdateArms();
            UpdateFrames();
            UpdateAmbientWear();
            PushTrail();

            //冲势速度线：残影激活期身后甩曳光帧
            if (!Main.dedServ && Context.AfterimageStrength > 0.3f
                && NPC.velocity.Length() > 18f && Main.GameUpdateCount % 2 == 0) {
                ScrapVfx.SpeedStreak(NPC.Center, NPC.velocity);
            }

            if (Context.AttackCooldown > 0) {
                Context.AttackCooldown--;
            }
            if (CannonHeat > 0) {
                CannonHeat--;
            }
            if (TautVibe > 0) {
                TautVibe--;
            }
            if (ViceClampFrames > 0) {
                ViceClampFrames--;
            }
            if (LaserFlash > 0) {
                LaserFlash--;
            }

            float glow = Context.HeadAlpha * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(NPC.Center, 0.32f * glow, 0.12f * glow, 0.05f * glow);
            }

            //过载热度喂给屏幕边缘泛橙（客户端表现通道）
            if (!Main.dedServ && Context.WeldHeat > 0.1f) {
                ScrapSiegeScreen.PushOverloadHeat(Context.WeldHeat * 0.85f);
            }
        }

        private void FindTarget() {
            if (NPC.target < 0 || NPC.target >= 255 || !Main.player[NPC.target].Alives()) {
                NPC.TargetClosest();
            }
            targetPlayer = Main.player[NPC.target];
            if (TargetInvalid()) {
                NPC.TargetClosest();
                targetPlayer = Main.player[NPC.target];
            }
        }

        internal bool TargetInvalid() {
            return targetPlayer == null || targetPlayer.dead || !targetPlayer.active
                || Math.Abs(NPC.position.X - targetPlayer.position.X) > ScrapDirector.MaxFindDistance
                || Math.Abs(NPC.position.Y - targetPlayer.position.Y) > ScrapDirector.MaxFindDistance;
        }

        private void UpdateContextFacts() {
            Context.Npc = NPC;
            Context.Target = targetPlayer;
            Context.Owner = this;
            Context.MasterMode = Main.masterMode;
        }

        /// <summary>触发死亡演出的生命阈值</summary>
        internal const int DeathTriggerLife = 10;

        /// <summary>全局转移，仅服务端驱动；进场/离场/转阶段/死亡演出中不打断</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient || stateMachine?.CurrentState == null) {
                return;
            }
            IVaultState<ScrapStateContext> current = stateMachine.CurrentState;
            if (current is ScrapIntroState or ScrapDespawnState or ScrapPhaseTransitionState or ScrapDeathState) {
                return;
            }

            //血线见底：进死亡演出（清弹、锁血、逐件散架）
            if (Context.Phase >= 1 && NPC.life <= DeathTriggerLife && !Context.DeathPerformanceFinished) {
                stateMachine.ChangeState(new ScrapDeathState());
                return;
            }

            //目标失效或天亮：机械造物循例撤离
            if (TargetInvalid() || Main.IsItDay()) {
                stateMachine.ChangeState(new ScrapDespawnState());
                return;
            }

            //55%：甩壳重构，进入统帅模式
            if (Context.Phase == 1 && NPC.life <= NPC.lifeMax * 0.55f) {
                stateMachine.ChangeState(new ScrapPhaseTransitionState());
                return;
            }

            //20%：过载熔断
            if (Context.Phase == 2 && NPC.life <= NPC.lifeMax * 0.2f
                && current is not ScrapOverloadConnectorState) {
                stateMachine.ChangeState(new ScrapOverloadConnectorState());
            }
        }

        /// <summary>锁血：死亡演出没放完不许真死，一击超杀也会被拦回演出</summary>
        public override bool CheckDead() {
            if (Context != null && !Context.DeathPerformanceFinished) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not ScrapDeathState) {
                    stateMachine?.ChangeState(new ScrapDeathState());
                }
                return false;
            }
            return true;
        }

        /// <summary>甩壳裸奔窗受击加深：压血奖励</summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (Context != null && Context.BareWindow) {
                modifiers.FinalDamage *= 1.25f;
            }
        }

        //==================== 四臂模拟（指令积分器，链条重量的来源）====================

        internal Vector2 GetArmPos(int i) => armPos[i];
        internal float GetArmRot(int i) => armRot[i];

        internal void RebuildArms(Vector2 head) {
            armsInit = true;
            headSim = head;
            for (int i = 0; i < ArmCount; i++) {
                armPos[i] = head + RestOffset[i];
                armVel[i] = Vector2.Zero;
                armRot[i] = 0f;
            }
        }

        /// <summary>肩锚点：左臂挂左肩、右臂挂右肩</summary>
        internal Vector2 ShoulderWorld(int i)
            => headSim + new Vector2(RestOffset[i].X < 0f ? -34f : 34f, 20f);

        /// <summary>悬挂队形目标：呼吸摆动 + 头移动时的滞后拖行（吊链的重量读数）</summary>
        internal Vector2 RestTarget(int i) {
            float time = Main.GlobalTimeWrappedHourly;
            float ph = Seed + i * 1.917f;
            Vector2 sway = new(MathF.Sin(time * 1.3f + ph) * 13f, MathF.Sin(time * 2.0f + ph * 1.31f) * 8f);
            return headSim + RestOffset[i] + sway - NPC.velocity * new Vector2(2.8f, 1.5f);
        }

        /// <summary>突刺弹出：一帧定初速 + 链长收口</summary>
        internal void BeginDart(int arm, Vector2 aim, float reach) {
            armVel[arm] = aim * ScrapDirector.DartLaunchSpeed;
            DartReach = reach;
            dartThunked = false;
        }

        /// <summary>给臂一记冲量（后坐/觉醒绷正等）</summary>
        internal void ImpulseArm(int arm, Vector2 impulse) => armVel[arm] += impulse;

        /// <summary>把臂直接摆到某点并清速（重构演出的散点起位）</summary>
        internal void PlaceArm(int arm, Vector2 pos) {
            armPos[arm] = pos;
            armVel[arm] = Vector2.Zero;
        }

        /// <summary>残影缓冲推进</summary>
        private void PushTrail() {
            if (!trailInit) {
                trailInit = true;
                for (int i = 0; i < TrailLen; i++) {
                    trailPos[i] = NPC.Center;
                    trailRot[i] = NPC.rotation;
                }
            }
            trailHead = (trailHead + 1) % TrailLen;
            trailPos[trailHead] = NPC.Center;
            trailRot[trailHead] = NPC.rotation;
        }

        /// <summary>确保磁场表现体在场（服务端一次性生成，各端本地读强度）</summary>
        internal void EnsureMagnetFieldProj() {
            if (VaultUtils.isClient) {
                return;
            }
            int type = ModContent.ProjectileType<Projectiles.ScrapMagnetFieldProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == NPC.whoAmI) {
                    return;
                }
            }
            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                type, 0, 0f, Main.myPlayer, NPC.whoAmI);
        }

        private void UpdateArms() {
            Vector2 head = NPC.Center + NPC.velocity;
            //硬纠：同步包把头拽走半屏，臂直接重建防抽搐
            if (!armsInit || Vector2.Distance(headSim, head) > 320f) {
                RebuildArms(head);
                return;
            }
            headSim = head;

            for (int i = 0; i < ArmCount; i++) {
                ArmDirective d = Context.Arms[i];
                float wantRot = d.UseRot
                    ? d.WantRot
                    : MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f + Seed + i * 1.917f) * 0.07f;
                float rotRate = d.RotRate > 0f ? d.RotRate : 0.14f;

                switch (d.Mode) {
                    case ArmMode.Hang: {
                        Vector2 target = RestTarget(i);
                        armVel[i] = (armVel[i] + (target - armPos[i]) * d.Spring) * d.Damping;
                        armPos[i] += armVel[i];
                        break;
                    }
                    case ArmMode.Hold: {
                        armVel[i] = (armVel[i] + (d.Target - armPos[i]) * d.Spring) * d.Damping;
                        armPos[i] += armVel[i];
                        break;
                    }
                    case ArmMode.Snap: {
                        armPos[i] = d.Target;
                        armVel[i] = Vector2.Zero;
                        break;
                    }
                    case ArmMode.Ballistic: {
                        //弹出段只管飞，链长放到本次伸展量就哐当勒停
                        armVel[i] *= 0.995f;
                        armPos[i] += armVel[i];
                        Vector2 fromShoulder = armPos[i] - ShoulderWorld(i);
                        float reach = fromShoulder.Length();
                        if (reach > DartReach) {
                            armPos[i] = ShoulderWorld(i) + fromShoulder.SafeNormalize(Vector2.UnitY) * DartReach;
                            armVel[i] *= 0.2f;
                            if (!dartThunked) {
                                dartThunked = true;
                                TautVibe = 12;
                                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 2 }, armPos[i]);
                            }
                        }
                        break;
                    }
                    case ArmMode.Fall: {
                        armVel[i].X *= 0.98f;
                        armVel[i].Y = MathF.Min(armVel[i].Y + 1.05f, 27f);
                        armPos[i] += armVel[i];
                        if (armPos[i].Y >= d.Target.Y) {
                            //嵌入地面
                            armPos[i].Y = d.Target.Y;
                            armVel[i] = Vector2.Zero;
                        }
                        break;
                    }
                }

                armRot[i] = armRot[i].AngleLerp(wantRot, rotRate);
            }
        }

        //==================== 帧与常态损耗 ====================

        private void UpdateFrames() {
            //头：常态 0/1 慢速交替，出手窗亮狰狞面
            bool rage = CurrentStateIndex is ScrapStateIndex.SawLaunch or ScrapStateIndex.ViceSnatch
                or ScrapStateIndex.HeadSwing or ScrapStateIndex.MagnetStorm;
            if (rage) {
                headFrameIndex = 2;
            }
            else {
                if (headFrameIndex > 1) {
                    headFrameIndex = 0;
                }
                if (++headFrameTick >= 12) {
                    headFrameTick = 0;
                    headFrameIndex = (headFrameIndex + 1) % 2;
                }
            }

            //锯：出手期高速旋转，闲时缓转
            if (++sawFrameTick >= (SawSpinning ? 2 : 14)) {
                sawFrameTick = 0;
                sawFrameIndex = (sawFrameIndex + 1) % 2;
            }
            SawSpinning = false;
        }

        /// <summary>常态损耗底噪：关节渗油、偶发接触不良的火星——废钢的呼吸</summary>
        private void UpdateAmbientWear() {
            if (Main.dedServ || !armsInit || Context.HeadAlpha < 0.4f) {
                return;
            }
            int budget = 2;
            for (int k = 0; k < 3 && budget > 0; k++) {
                int i = Main.rand.Next(ArmCount);
                if (Context.ToolAlpha[i] < 0.4f) {
                    continue;
                }
                if (Main.rand.NextFloat() > 0.16f) {
                    continue;
                }
                budget--;
                //滴点在链条中段或工具关节缝
                Vector2 pos = Main.rand.NextBool()
                    ? Vector2.Lerp(ShoulderWorld(i), armPos[i], Main.rand.NextFloat(0.3f, 0.8f))
                    : armPos[i] + Main.rand.NextVector2Circular(14f, 12f);
                if (Main.rand.NextBool(5)) {
                    //接触不良火星，稀有
                    PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2Circular(1.6f, 1.6f),
                        Color.Lerp(WeldOrange, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(8, 14));
                }
                else {
                    //油滴
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                        new Vector2(NPC.velocity.X * 0.05f, Main.rand.NextFloat(0.7f, 1.5f)),
                        OilDark * Main.rand.NextFloat(0.5f, 0.7f),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(18, 32), 0f);
                }
            }
        }

        //==================== 受击与谢幕（M1 简版，死亡演出在 M3 接管）====================

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            //受击火星 + 掉漆尘
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(30f, 30f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(1f, 3f)),
                    Color.Lerp(WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(10, 16));
            }

            if (NPC.life <= 0) {
                //M1 简版谢幕：金属爆散 + 烟柱（M3 换成逐件散架死亡演出）
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.9f, Pitch = -0.4f }, NPC.Center);
                for (int i = 0; i < 26; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(40f, 40f),
                        Main.rand.NextVector2Circular(7f, 7f),
                        Color.Lerp(WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.7f, 1.2f))?.Configure(true, Main.rand.Next(14, 26));
                }
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center + Main.rand.NextVector2Circular(36f, 36f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.5f, 1.1f)),
                        SmokeGray, Main.rand.NextFloat(0.8f, 1.2f))?.Configure(Main.rand.Next(60, 100));
                }
            }
        }

        //==================== 绘制：链 → 工具 → 头 → 加色层 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!armsInit) {
                return false;
            }

            //废钢材质着色器：锈蚀斑块 + 油渍流层 + 焊缝热光；缺资产时回退纯 tint
            Effect form = EffectLoader.ScrapForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;
            if (shaderOk) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            }

            //链在最底
            for (int i = 0; i < ArmCount; i++) {
                float alpha = Context.ToolAlpha[i];
                if (alpha > 0.02f) {
                    DrawChainArm(spriteBatch, form, shaderOk, i, alpha, drawColor);
                }
            }
            //工具压链上
            for (int i = 0; i < ArmCount; i++) {
                float alpha = Context.ToolAlpha[i];
                if (alpha > 0.02f) {
                    DrawTool(spriteBatch, form, shaderOk, i, alpha, drawColor);
                }
            }
            //头压顶
            if (Context.HeadAlpha > 0.02f) {
                DrawHead(spriteBatch, form, shaderOk, Context.HeadAlpha, drawColor);
            }

            if (shaderOk) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            DrawGlowLayer(spriteBatch);
            return false;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
            => Vector2.Lerp(Vector2.Lerp(a, c, t), Vector2.Lerp(c, b, t), t);

        /// <summary>体表锈化基线：机体一路打一路烂</summary>
        private float RustBase()
            => 0.55f + (1f - NPC.life / (float)NPC.lifeMax) * 0.35f;

        /// <summary>逐件套废钢材质参数（Immediate 批内每次 Draw 前调用）</summary>
        private void ApplyScrapForm(Effect form, float seed, float rust, float sheen, float heat,
            Rectangle frame, Texture2D tex) {
            form.Parameters["uSeed"]?.SetValue(seed);
            form.Parameters["uRust"]?.SetValue(MathHelper.Clamp(rust, 0f, 1f));
            form.Parameters["uSheen"]?.SetValue(sheen);
            form.Parameters["uHeat"]?.SetValue(MathHelper.Clamp(heat, 0f, 1f));
            form.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
            form.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>吊链：Chain22 重链沿悬链弧逐环铺出；突刺绷直时高频颤动</summary>
        private void DrawChainArm(SpriteBatch sb, Effect form, bool shaderOk, int i, float alpha, Color lightColor) {
            Texture2D chain = TextureAssets.Chain22?.Value;
            if (chain == null) {
                return;
            }
            Vector2 s = ShoulderWorld(i);
            Vector2 a = armPos[i];
            float dist = Vector2.Distance(s, a);
            //悬链弧：链越松垂度越大，突刺伸展时自然绷直
            float restLen = RestOffset[i].Length() * 1.18f;
            float sag = 10f + MathHelper.Clamp(restLen - dist, 0f, 120f) * 0.55f;
            Vector2 mid = (s + a) * 0.5f + new Vector2(0f, sag);
            //绷直颤动：拉满后链条打的那个战栗
            if (TautVibe > 0 && (i == ArmSaw || i == ArmVice)) {
                Vector2 perp = (a - s).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                mid += perp * MathF.Sin(Main.GlobalTimeWrappedHourly * 62f + Seed) * (TautVibe / 12f) * 6f;
            }

            Rectangle chainFrame = new(0, 0, chain.Width, chain.Height);
            Color tint = shaderOk
                ? new Color(lightColor.R, lightColor.G, lightColor.B, (byte)(alpha * 255f))
                : lightColor.MultiplyRGB(ChainRustMul) * alpha;
            if (shaderOk) {
                //参数每臂只设一次（链条锈得更透、缝隙渗油最重），链节共享同一材质态
                ApplyScrapForm(form, Seed + i * 2.3f,
                    RustBase() + 0.15f, 0.5f, Context.WeldHeat * 0.5f, chainFrame, chain);
            }
            float linkLen = chain.Height * 0.92f;
            int segs = Math.Max(3, (int)((dist + sag * 2f) / linkLen));
            Vector2 prev = s;
            for (int k = 1; k <= segs; k++) {
                Vector2 p = Bezier(s, mid, a, k / (float)segs);
                Vector2 dir = p - prev;
                float len = dir.Length();
                if (len < 2f) {
                    prev = p;
                    continue;
                }
                Vector2 c = (prev + p) * 0.5f;
                float rot = dir.ToRotation() + MathHelper.PiOver2;
                Vector2 scale = new(0.9f, len / chain.Height * 1.06f);
                sb.Draw(chain, c - Main.screenPosition, null, tint, rot,
                    chain.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                prev = p;
            }
        }

        private void DrawTool(SpriteBatch sb, Effect form, bool shaderOk, int i, float alpha, Color lightColor) {
            int npcType = ArmNpcType(i);
            Main.instance.LoadNPC(npcType);
            Texture2D tex = TextureAssets.Npc[npcType]?.Value;
            if (tex == null) {
                return;
            }
            int frameCount = Main.npcFrameCount[npcType];
            int frameIndex = i switch {
                ArmSaw => sawFrameIndex % frameCount,
                ArmVice when ViceClampFrames > 0 => 1 % frameCount,
                ArmLaser when LaserFlash > 0 => 1 % frameCount,
                _ => 0,
            };
            int frameH = tex.Height / frameCount;
            Rectangle frame = new(0, frameH * frameIndex, tex.Width, frameH);
            //左侧臂镜像（原版同款按侧翻面）
            SpriteEffects flip = RestOffset[i].X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Color tint;
            if (shaderOk) {
                //炮管带余温热光，其余件只吃过载焊光
                float heat = Context.WeldHeat + (i == ArmCannon ? CannonHeat / 30f * 0.8f : 0f);
                ApplyScrapForm(form, Seed + i * 3.1f, RustBase(), 0.35f, heat, frame, tex);
                tint = new Color(lightColor.R, lightColor.G, lightColor.B, (byte)(alpha * 255f));
            }
            else {
                tint = lightColor.MultiplyRGB(RustMul) * alpha;
            }
            sb.Draw(tex, armPos[i] - Main.screenPosition, frame, tint, armRot[i],
                frame.Size() * 0.5f, 1f, flip, 0f);
        }

        private void DrawHead(SpriteBatch sb, Effect form, bool shaderOk, float alpha, Color lightColor) {
            Main.instance.LoadNPC(NPCID.SkeletronPrime);
            Texture2D tex = TextureAssets.Npc[NPCID.SkeletronPrime]?.Value;
            if (tex == null) {
                return;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.SkeletronPrime];
            Rectangle frame = new(0, frameH * headFrameIndex, tex.Width, frameH);

            Color tint;
            if (shaderOk) {
                ApplyScrapForm(form, Seed, RustBase() * 0.9f, 0.3f, Context.WeldHeat * 0.7f, frame, tex);
                tint = new Color(lightColor.R, lightColor.G, lightColor.B, (byte)(alpha * 255f));
            }
            else {
                tint = lightColor.MultiplyRGB(RustMul) * alpha;
            }
            sb.Draw(tex, NPC.Center - Main.screenPosition, frame, tint, NPC.rotation,
                frame.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
        }

        /// <summary>加色层：磁力吊线、目镜红点与扫光、炮口余温、焊缝热光</summary>
        private void DrawGlowLayer(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            bool begun = false;
            Vector2 gOrigin = glow.Size() * 0.5f;
            void EnsureBegin() {
                if (!begun) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
            }

            //磁力吊线：锚点到四件工具的焊橙光带，进场拼装与磁暴时亮起
            if (Context.MagnetGlow > 0.05f) {
                EnsureBegin();
                Vector2 anchor = Context.HeadAlpha > 0.5f ? NPC.Center : Context.IntroAnchor;
                float flicker = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 26f + Seed);
                for (int i = 0; i < ArmCount; i++) {
                    if (Context.ToolAlpha[i] < 0.1f) {
                        continue;
                    }
                    Vector2 to = armPos[i] - anchor;
                    float len = to.Length();
                    if (len < 8f) {
                        continue;
                    }
                    float rot = to.ToRotation();
                    sb.Draw(glow, anchor + to * 0.5f - Main.screenPosition, null,
                        WeldOrange * (0.34f * Context.MagnetGlow * flicker), rot, gOrigin,
                        new Vector2(len * 2f / glow.Width, 9f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //热残影：冲撞/突刺身后的灰烬鬼影（画在其余光效之下）
            if (Context.AfterimageStrength > 0.05f && Context.HeadAlpha > 0.5f && trailInit) {
                Main.instance.LoadNPC(NPCID.SkeletronPrime);
                Texture2D headTex = TextureAssets.Npc[NPCID.SkeletronPrime]?.Value;
                if (headTex != null) {
                    EnsureBegin();
                    int frameH2 = headTex.Height / Main.npcFrameCount[NPCID.SkeletronPrime];
                    Rectangle ghostFrame = new(0, frameH2 * headFrameIndex, headTex.Width, frameH2);
                    for (int j = 1; j <= 5; j++) {
                        int idx = ((trailHead - j * 2) % TrailLen + TrailLen) % TrailLen;
                        float fade = Context.AfterimageStrength * (1f - j / 6f) * 0.4f;
                        sb.Draw(headTex, trailPos[idx] - Main.screenPosition, ghostFrame,
                            WeldOrange * fade, trailRot[idx], ghostFrame.Size() * 0.5f,
                            1f - j * 0.03f, SpriteEffects.None, 0f);
                    }
                }
            }

            //镭射出弹前积光
            if (LaserFlash > 0 && Context.ToolAlpha[ArmLaser] > 0.5f) {
                EnsureBegin();
                float k = LaserFlash / 5f;
                Vector2 muzzle = armPos[ArmLaser] + (armRot[ArmLaser] + MathHelper.PiOver2).ToRotationVector2() * 24f;
                sb.Draw(glow, muzzle - Main.screenPosition, null, WeldOrange * (0.5f * k), 0f,
                    gOrigin, new Vector2((6f + 10f * (1f - k)) * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //目镜红点脉冲（扫光束移到 BeamLine 层）
            if (Context.EyeScan >= 0f && Context.HeadAlpha > 0.5f) {
                EnsureBegin();
                float bright = MathF.Sin(Context.EyeScan * MathHelper.Pi);
                sb.Draw(glow, NPC.Center + new Vector2(0f, 8f) - Main.screenPosition, null,
                    EyeRed * (0.5f * bright), 0f, gOrigin,
                    new Vector2(20f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //常燃目镜红点
            if (Context.HeadAlpha > 0.3f) {
                EnsureBegin();
                float pulse = 0.28f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f + Seed);
                sb.Draw(glow, NPC.Center + new Vector2(0f, 8f) - Main.screenPosition, null,
                    EyeRed * (pulse * Context.HeadAlpha), 0f, gOrigin,
                    new Vector2(13f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //炮口余温衰减
            if (CannonHeat > 0 && Context.ToolAlpha[ArmCannon] > 0.5f) {
                EnsureBegin();
                float heat = CannonHeat / 30f;
                Vector2 muzzle = armPos[ArmCannon] + (armRot[ArmCannon] + MathHelper.PiOver2).ToRotationVector2() * 26f;
                sb.Draw(glow, muzzle - Main.screenPosition, null, WeldOrange * (0.45f * heat), 0f,
                    gOrigin, new Vector2(16f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //焊缝热光：过载阶段全身关节泛橙（M2 起用）
            if (Context.WeldHeat > 0.05f) {
                EnsureBegin();
                for (int i = 0; i < ArmCount; i++) {
                    if (Context.ToolAlpha[i] < 0.3f) {
                        continue;
                    }
                    sb.Draw(glow, ShoulderWorld(i) - Main.screenPosition, null,
                        WeldOrange * (0.3f * Context.WeldHeat), 0f, gOrigin,
                        new Vector2(10f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            DrawBeamAccents(sb);
        }

        /// <summary>BeamLine 层：状态登记的全部射线标记 + 目镜扫光束（着色器线材质）</summary>
        private void DrawBeamAccents(SpriteBatch sb) {
            bool wantScan = Context.EyeScan >= 0f && Context.HeadAlpha > 0.5f;
            if (Context.Beams.Count == 0 && !wantScan) {
                return;
            }

            ScrapVfx.BeginBeamBatch(sb);

            //状态登记的射线：突刺预警/矩阵网格/瀑布柱/指挥线全走这一条通道
            for (int i = 0; i < Context.Beams.Count; i++) {
                ScrapStateContext.BeamMark mark = Context.Beams[i];
                if (mark.Alpha < 0.03f) {
                    continue;
                }
                ScrapVfx.DrawBeam(sb, mark.From, mark.From + mark.Dir * mark.Length,
                    26f, mark.Hot, mark.Dash, Seed + i * 1.31f,
                    ScrapVfx.BeamCoreWarm, mark.Dash > 0.5f ? ScrapVfx.BeamEdgeRed : ScrapVfx.BeamEdgeRust,
                    0.02f, 0.25f, mark.Alpha);
            }

            //目镜扫光束：从目镜口扫出的细光锥
            if (wantScan) {
                float ang = MathHelper.PiOver2 + MathHelper.Lerp(-0.62f, 0.62f, Context.EyeScan);
                float bright = MathF.Sin(Context.EyeScan * MathHelper.Pi);
                Vector2 eye = NPC.Center + new Vector2(0f, 8f);
                ScrapVfx.DrawBeam(sb, eye, eye + ang.ToRotationVector2() * 200f,
                    16f, bright * 0.75f, 0f, Seed + 3.1f,
                    ScrapVfx.BeamCoreWarm, ScrapVfx.BeamEdgeRed,
                    0.02f, 0.55f, bright);
            }

            ScrapVfx.EndBeamBatch(sb);
        }
    }
}
