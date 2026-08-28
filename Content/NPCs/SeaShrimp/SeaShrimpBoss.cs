using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.NPCs.SeaShrimp.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>
    /// 渊晶海虾，石巨人后海洋召唤 boss。
    /// 单 NPC 多部件：脊链（头+3体节+尾扇）+ 双螯二骨 IK + 六足程序化步态 + verlet 触角，
    /// 全部部件由各端从已同步的头位与状态索引确定性重算（不发部件包）。
    /// 伤害不走部件贴图：接触伤仅状态窗内启用，螯击/空泡由权威端弹幕承载（伤害窗=视觉窗）。
    /// 联机契约：转场只在服务器裁决（NpcStateMachine 走 ai[3] 同步），
    /// 各端本地跑同一状态机做表现，节拍键控在本地 Timer 上，弹幕只在权威端生成，
    /// 粒子音效全走 !dedServ 门，表现层随机只用确定性种子
    /// </summary>
    internal class SeaShrimpBoss : SeaShrimpModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override string BossHeadTexture => CWRConstant.NPC + "SeaShrimp/SeaShrimpBoss_Head_Boss";

        private NpcStateMachine<SeaShrimpStateContext> stateMachine;
        internal SeaShrimpStateContext Context { get; private set; }
        internal ShrimpSkeleton Skeleton { get; } = new();
        internal ShrimpLocomotion Locomotion { get; } = new();
        /// <summary>残影位姿环（纯本地表现）</summary>
        internal ShrimpPoseTrail PoseTrail { get; } = new();
        private Player targetPlayer;

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        internal float Seed => NPC.whoAmI * 0.7391f;

        internal SeaShrimpStateIndex CurrentStateIndex => (SeaShrimpStateIndex)(int)NPC.ai[3];

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
            NPC.width = 150;
            NPC.height = 110;
            NPC.damage = SeaShrimpDirector.ContactDamage;
            NPC.defense = SeaShrimpDirector.BaseDefense;
            NPC.lifeMax = SeaShrimpDirector.BaseLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.npcSlots = 15f;
            NPC.value = Item.buyPrice(0, 15);
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicID.DukeFishron;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Ocean,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.SeaShrimpBoss.Bestiary"),
            ]);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            //专家：宝藏袋；普通：同池直掉（与袋共享一张掉落表，袋内量更足）
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<SeaShrimpTreasureBag>()));
            //大师：圣物直掉，照原版不进袋
            npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<SeaShrimpRelic>()));
            LeadingConditionRule notExpert = new(new Conditions.NotExpert());
            RegisterSharedLoot(rule => notExpert.OnSuccess(rule), expert: false);
            npcLoot.Add(notExpert);
        }

        /// <summary>
        /// 共享掉落池（普通直掉与专家袋同表）：签名深渊武器永渊/裂渊/钳渊/潮渊/镰渊五取一保底 +
        /// 渊晶虾壳（五武器配方的必需材料）+ 渊晶碎片；灾厄在场换深渊系材料
        /// （荧光藻/虚空石/深渊细胞——同为配方料，击杀反哺成套），缺席回退后石巨人期原版材料
        /// </summary>
        internal static void RegisterSharedLoot(Action<IItemDropRule> add, bool expert) {
            add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<Content.Items.Magic.Everdeeps.Everdeep>(),
                ModContent.ItemType<Content.Items.Melee.Abyssrends.Abyssrend>(),
                ModContent.ItemType<Content.Items.Summon.Deepclaws.Deepclaw>(),
                ModContent.ItemType<Content.Items.Ranged.Undertows.Undertow>(),
                ModContent.ItemType<Content.Items.Melee.TideReapers.TideReaper>()));
            add(ItemDropRule.Common(ModContent.ItemType<SeaShrimpShell>(), 1
                , expert ? 16 : 12, expert ? 24 : 18));
            add(ItemDropRule.Common(ItemID.CrystalShard, 1, expert ? 24 : 18, expert ? 40 : 30));
            if (CWRID.Item_Lumenyl > 0 && CWRID.Item_Voidstone > 0 && CWRID.Item_DepthCells > 0) {
                add(ItemDropRule.Common(CWRID.Item_Lumenyl, 1, expert ? 14 : 10, expert ? 22 : 16));
                add(ItemDropRule.Common(CWRID.Item_Voidstone, 1, expert ? 20 : 15, expert ? 34 : 25));
                add(ItemDropRule.Common(CWRID.Item_DepthCells, 1, expert ? 12 : 8, expert ? 20 : 14));
                return;
            }
            add(ItemDropRule.Common(ItemID.SoulofMight, 1, expert ? 12 : 8, expert ? 20 : 14));
            add(ItemDropRule.Common(ItemID.BeetleHusk, 1, expert ? 6 : 4, expert ? 10 : 7));
        }

        public override void OnKill() {
            //击杀旗标：SetEventFlagCleared 自动处理联机 WorldData 广播
            NPC.SetEventFlagCleared(ref SeaShrimpWorldFlag.DownedSeaShrimp, -1);
        }

        //==================== 状态机装配 ====================

        private void InitializeStateMachine() {
            Context = new SeaShrimpStateContext {
                Npc = NPC,
                Owner = this,
            };
            stateMachine = new NpcStateMachine<SeaShrimpStateContext>(Context);
            Skeleton.BindSeed(Seed);
            Locomotion.Bind(NPC);
            Locomotion.SnapHeading(NPC.Center.X < Main.maxTilesX * 8f ? 0f : MathHelper.Pi);

            if (Context.Phase < 1) {
                Context.Phase = 1;
            }

            //中途加入的客户端从 ai[3] 恢复状态；M3 起初始态换入场演出
            if (VaultUtils.isClient) {
                int syncedIndex = (int)NPC.ai[3];
                IVaultState<SeaShrimpStateContext> synced = VaultStateRegistry<SeaShrimpStateContext>.Create(syncedIndex);
                stateMachine.SetInitialState(synced ?? new SeaShrimpIntroState());
            }
            else {
                stateMachine.SetInitialState(new SeaShrimpIntroState());
            }
        }

        //==================== 主 AI ====================

        public override void AI() {
            if (stateMachine == null || Context == null) {
                InitializeStateMachine();
            }

            NPC.netOffset = Vector2.Zero;
            NPC.dontTakeDamage = false;
            //接触伤默认关，仅状态举旗的冲撞窗开（伤害窗=视觉窗）
            NPC.damage = 0;

            FindTarget();
            UpdateContextFacts();
            EvaluateGlobalTransitions();

            Context.BeginFrameDefaults();
            stateMachine.Update();

            Locomotion.Update();
            Skeleton.Update(Context, NPC.Center, Locomotion.Heading,
                Locomotion.TangentMove, NPC.velocity.Length(), Locomotion.Wet);

            //残影快照：爆发段（尾弹/出拳举旗）才捕获，渲染层按当前强度衰减重绘
            if (!Main.dedServ) {
                PoseTrail.Capture(Skeleton, Context.AfterimageStrength);
            }

            if (Context.AttackCooldown > 0) {
                Context.AttackCooldown--;
            }

            //晶簇常燃微光
            float glow = (0.35f + 0.65f * Context.CrystalGlow) * Context.BodyAlpha;
            Lighting.AddLight(NPC.Center, 0.1f * glow, 0.2f * glow, 0.42f * glow);

            if (!Main.dedServ) {
                //战场滤镜阶段深度：各端从同步的 ai[2] 自行推导
                SeaShrimpAbyssScreen.PushDepth((MathHelper.Clamp(Context.Phase, 1f, 3f) - 1f) * 0.5f);
                WatchPhaseFx();

                //深渊微生物光点：战场缓漂的氛围亮尘，P3 更密
                float moteChance = 0.05f + 0.05f * (Context.Phase - 1);
                if (Main.rand.NextFloat() < moteChance && Context.BodyAlpha > 0.5f) {
                    Vector2 pos = Main.LocalPlayer.Center + Main.rand.NextVector2Circular(900f, 520f);
                    PRTLoader.NewParticle<PRT_Light>(pos,
                        new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.05f, 0.3f)),
                        SeaShrimpRenderer.CrystalBlue * Main.rand.NextFloat(0.25f, 0.5f),
                        Main.rand.NextFloat(0.14f, 0.32f));
                }
            }
        }

        /// <summary>入 P2 的各端本地演出（蜕壳入 P3 的演出由蜕壳态自持）</summary>
        private int lastSeenPhase;

        private void WatchPhaseFx() {
            int phase = Context.Phase;
            if (phase == 2 && lastSeenPhase == 1) {
                //P2 涨压破甲拍：怒吼 + 甲壳缝隙高压水线喷射 + 冲击环 + 滤镜微脉冲
                SoundEngine.PlaySound(CWRSound.SendRoar with { Volume = 0.8f, Pitch = -0.1f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.1f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.35f }, NPC.Center);
                Context.CrystalGlow = 1f;
                SeaShrimpAbyssScreen.TriggerImpactFrame(0.18f);
                if (Main.LocalPlayer != null
                    && Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1500f) {
                    Main.LocalPlayer.CWR()?.GetScreenShake(5f);
                }
                Context.AddRing(NPC.Center, 320f, 30, 1f);
                //裂缝喷压：沿体节向外的十股高压水滴锥
                for (int i = 0; i < 10; i++) {
                    Vector2 seam = NPC.Center + Main.rand.NextVector2Circular(70f, 44f);
                    Vector2 dir = (seam - NPC.Center).SafeNormalize(Main.rand.NextVector2Unit());
                    for (int j = 0; j < 3; j++) {
                        Content.Items.Magic.Everdeeps.EverdeepVFX.ShedDroplet(seam,
                            dir.RotatedByRandom(0.3f) * Main.rand.NextFloat(4f, 9f), 1f);
                    }
                }
            }
            lastSeenPhase = phase;
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
                || Math.Abs(NPC.position.X - targetPlayer.position.X) > SeaShrimpDirector.MaxFindDistance
                || Math.Abs(NPC.position.Y - targetPlayer.position.Y) > SeaShrimpDirector.MaxFindDistance;
        }

        private void UpdateContextFacts() {
            Context.Npc = NPC;
            Context.Target = targetPlayer;
            Context.Owner = this;
            Context.MasterMode = Main.masterMode;
        }

        /// <summary>触发死亡演出的生命阈值</summary>
        internal const int DeathTriggerLife = 10;

        /// <summary>全局转移，仅服务端驱动；入场/离场/蜕壳/死亡演出中不打断</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient || stateMachine?.CurrentState == null) {
                return;
            }
            IVaultState<SeaShrimpStateContext> current = stateMachine.CurrentState;
            if (current is SeaShrimpIntroState or SeaShrimpDespawnState
                or SeaShrimpMoltTransitionState or SeaShrimpDeathState
                or SeaShrimpVortexWallState) {
                return;
            }

            //血线见底：进死亡演出（清弹、锁血、逐节熄灭）
            if (NPC.life <= DeathTriggerLife && !Context.DeathPerformanceFinished) {
                stateMachine.ChangeState(new SeaShrimpDeathState());
                return;
            }

            if (TargetInvalid()) {
                stateMachine.ChangeState(new SeaShrimpDespawnState());
                return;
            }

            //70%：入 P2 由双渊柱封场事件承接（状态内置 Phase 切换+清弹+封场召唤）
            if (Context.Phase == 1 && NPC.life <= NPC.lifeMax * 0.7f) {
                stateMachine.ChangeState(new SeaShrimpVortexWallState());
                return;
            }

            //40%：蜕壳转阶段大节拍
            if (Context.Phase == 2 && NPC.life <= NPC.lifeMax * 0.4f) {
                stateMachine.ChangeState(new SeaShrimpMoltTransitionState());
            }
        }

        /// <summary>锁血：死亡演出没放完不许真死，一击超杀也被拦回演出</summary>
        public override bool CheckDead() {
            if (Context != null && !Context.DeathPerformanceFinished) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not SeaShrimpDeathState) {
                    stateMachine?.ChangeState(new SeaShrimpDeathState());
                }
                return false;
            }
            return true;
        }

        /// <summary>清除本 boss 系的敌对弹幕（阶段转换/死亡演出的公平阀，权威端调用）</summary>
        internal static void ClearHostileProjectiles() {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.hostile && proj.ModProjectile is SeaShrimpModProjectile) {
                    proj.Kill();
                }
            }
        }

        //==================== 受击表现 ====================

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            //甲壳崩晶：受击溅蓝晶火花
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(46f, 34f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(1.5f, 4.5f), -Main.rand.NextFloat(0.5f, 2.5f)),
                    Color.Lerp(SeaShrimpRenderer.CrystalBlue, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.4f, 0.8f))?.Configure(true, Main.rand.Next(8, 14));
            }

            if (NPC.life <= 0) {
                //M1 简版谢幕：晶爆（M3 换成完整死亡演出）
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.9f, Pitch = -0.2f }, NPC.Center);
                for (int i = 0; i < 30; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(60f, 40f),
                        Main.rand.NextVector2Circular(8f, 8f),
                        Color.Lerp(SeaShrimpRenderer.CrystalBlue, Color.White, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.6f, 1.1f))?.Configure(true, Main.rand.Next(14, 26));
                }
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            SeaShrimpRenderer.Draw(spriteBatch, this);
            return false;
        }
    }
}
