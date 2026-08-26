using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 深牢怨灵：Dungeonworld 牢狱层专属小 Boss。形体承袭骷髅王鬼奴三件套
    /// 单 NPC 内部模拟"鬼躯 + 左右铁铐"，躯位服务器权威同步，双铐各端本地
    /// 弹簧摆模拟（挥击窗内换运动学摆位），铐到躯体之间用原版 Chain22 沿悬链弧铺链。
    /// 贴图全借原版（Wraith 躯 + Shackle 铐），材质身份：青灰灵质 + 锈铁链具 + 冷粉狱火。
    /// 战斗循环 P1：铐击 → 狱火 → 横贯拉锁 → 狱火 → 链旋 → 狱火；
    /// 55% 转阶段（清弹嘶吼）后 P2 加入穿墙隐袭与囚笼合围。
    /// 联机契约照 ScrapCommander 先例：转场只在服务器裁决并盖 netUpdate 章，
    /// ai[0..3] 乘 SyncNPC 过线，各端本地跑同一状态机做表现，节拍闩防快照回卷，
    /// 弹幕只在权威端生成，铐位命中走贴合各端本地模拟的 GaolCuffHitbox（公平阀）
    /// </summary>
    internal class DeepGaolWraith : GaolModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 数值（占位初值，验收再调）====================

        /// <summary>基础生命（普通模式，专家/大师原版自乘）</summary>
        internal const int BaseLife = 6800;
        internal const int BaseDefense = 12;
        /// <summary>接触基伤，仅隐袭冲刺与链旋达速窗内启用</summary>
        internal const int ContactDamage = 36;

        //弹幕基伤（normal/expert，走 GetAttackDamage_ForProjectiles）
        internal static (float Normal, float Expert) SwipeDamage => (38f, 32f);
        internal static (float Normal, float Expert) FireBoltDamage => (30f, 26f);
        internal static (float Normal, float Expert) CrossChainDamage => (34f, 28f);
        internal static (float Normal, float Expert) CageBarDamage => (26f, 22f);

        internal int ScaleDamage((float Normal, float Expert) baseDamage)
            => (int)NPC.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);

        //==================== 状态 ====================

        internal const int StateEmerge = 0;
        internal const int StateFollow = 1;
        internal const int StateSwipe = 2;
        internal const int StateVolley = 3;
        internal const int StateFlail = 4;
        internal const int StateCrossChains = 5;
        internal const int StateCage = 6;
        internal const int StateAmbush = 7;
        internal const int StateRoar = 8;
        internal const int StateDeath = 9;
        internal const int StateDespawn = 10;

        internal int State { get => (int)NPC.ai[0]; private set => NPC.ai[0] = value; }
        private ref float StateTimer => ref NPC.ai[1];
        /// <summary>状态内子参数。铐击编码为 首铐位 + 已击次数×2（bit0=首铐）；
        /// 狱火/链旋为相位号；隐袭编码为 相位 + 轮次×16</summary>
        private ref float StateParam => ref NPC.ai[2];
        /// <summary>出招轮转计数，选招表用</summary>
        private ref float AttackIndex => ref NPC.ai[3];

        /// <summary>55% 以下进二阶段，直接从同步的生命值推导，各端一致</summary>
        internal bool Phase2 => NPC.life <= NPC.lifeMax * 0.55f;

        /// <summary>骷髅头入场变体：Emerge 期 ai[2] 复用为变体标记（该状态不用它做别的编码）</summary>
        private bool IsSkullEmerge => State == StateEmerge && (int)StateParam == EmergeVariantSkull;

        //==================== 时序 ====================

        //出场：地下链声预兆→双铐破土→鬼躯升起→落定→觉醒点火
        private const int CuffsBreachFrame = 30;
        private const int BodyRiseFrame = 44;
        private const int RiseEnd = 78;
        private const int AwakenFrame = 88;
        private const int EmergeTotal = 112;

        //骷髅头入场变体（蛰伏枯颅换体时经 NewNPC 的 ai2 传入）：
        //枯颅升起+双铐破墙+怨雾凝躯，跳过破土时间线，复用觉醒收尾拍。
        //预告缓冲由枯颅侧的 46 帧激活演出承担，此处节奏可比破土版更紧
        internal const int EmergeVariantSkull = 1;
        private const int VariantBurstFrame = 2;
        private const int VariantSettleFrame = 34;
        private const int VariantAwakenFrame = 44;
        private const int VariantEmergeTotal = 66;

        //铐击：交替挥抡，第二记更快（敌对版比鬼奴慢一档）
        private static readonly int[] SwipeWindups = [32, 20, 26, 22];
        private const int StrikeFrames = 8;
        /// <summary>冲击拍在挥击第几帧落地</summary>
        private const int ImpactAt = 6;
        private const int SwipeRecover = 16;
        private int SwipeCount => Phase2 ? 4 : 3;

        //链旋：收铐→原地加速→达速漂移逼近（伤害窗）→踉跄→回正
        private const int FlailTuckFrames = 14;
        private const int FlailSpinupFrames = 26;
        private const int FlailChaseFrames = 80;
        private const int FlailSpindownFrames = 24;
        private const int FlailRecoverFrames = 16;
        private const float FlailMaxOmega = 0.34f;
        private const float FlailMaxRadius = 120f;

        //狱火连弹：定身昂首→灯架蓄力（72% 后静默）→连发→回摆
        private const int VolleyAimFrames = 12;
        private const int VolleyChargeFrames = 18;
        private const int VolleyGap = 11;
        private const int VolleyRecoverFrames = 14;
        private int VolleyCount => Phase2 ? 4 : 3;

        //横贯拉锁：施法拉杆姿势，锁链弹幕自走预览→绷直时间线
        private const int ChainsPoseEnd = 18;
        private const int ChainsStateTotal = 78;

        //囚笼合围：过顶合击预告→静止链栏圈场→笼内三连狱火
        private const int CagePoseFrames = 30;
        private const int CageStateTotal = 200;
        private static readonly int[] CageShotBeats = [70, 95, 120];

        //穿墙隐袭：链缠成雾→侧翼先亮预告→凝形→锁定后仰→直线突刺→急刹
        private const int AmbushVeilFrames = 26;
        private const int AmbushReveilFrames = 16;
        private const int AmbushWarnFrames = 12;
        private const int AmbushFormFrames = 10;
        private const int AmbushAimFrames = 10;
        private const int AmbushLungeFrames = 12;
        private const int AmbushBrakeFrames = 22;
        private const float AmbushLungeSpeed = 26f;

        private const int RoarFrames = 45;
        private const int DeathTotal = 130;
        private const int DeathCuffOpenAt = 38;
        private const int DeathPopAt = 118;

        /// <summary>感知与脱战距离</summary>
        private const float MaxFindDistance = 4600f;

        //==================== 双铐（各端本地重建，不入同步；命中走 GaolCuffHitbox）====================

        private const float BodyDrawScale = 1.3f;
        private const float CuffDrawScale = 1.5f;

        /// <summary>0=左铐（原贴图向），1=右铐（水平翻转）</summary>
        private readonly Vector2[] cuffPos = new Vector2[2];
        /// <summary>本帧位移差，喂拖影拉伸与挥击扫掠碰撞</summary>
        private readonly Vector2[] cuffVel = new Vector2[2];
        private readonly float[] cuffRot = new float[2];
        private bool cuffsInit;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int attackCooldown;
        private Player targetPlayer;
        private int lastSeenState = -1;

        //出场演出闩
        private bool cuffsBreached;
        private bool bodyRisen;
        private bool settleDipped;
        private bool awakenDone;
        private float emergeGroundY;
        private bool emergeGroundLatched;

        //铐击节拍闩
        private int lastSwipeLaunched = -1;
        private int lastSwipeImpacted = -1;
        //狱火与链旋节拍闩
        private int lastBoltFired = -1;
        private bool flailRoared;
        private float spinRot;
        //拉锁与囚笼节拍闩
        private int lastChainCalled = -1;
        private bool cageBarsCalled;
        //隐袭节拍闩
        private bool ambushBlinked;
        private bool ambushLungeSet;
        private Vector2 ambushAim;
        //转阶段与死亡（服务器转场闩 + 各端演出闩）
        private bool roarDone;
        private bool roarCuePlayed;
        private bool deathDone;
        private bool cuffsOpened;
        private bool firePopped;
        private readonly bool[] cuffLanded = new bool[2];
        /// <summary>觉醒/转阶段的握拢脉冲余帧，纯绘制</summary>
        private int clenchTimer;
        /// <summary>全链战栗余帧，纯绘制</summary>
        private int chainShiver;

        //本次挥击的弧线参数（launch 闩帧从当前铐位/目标定参，各端自算，远端仅演出）
        private float swipeStartAng;
        private float swipeEndDelta;
        private float swipeR0;
        private float swipeR1;

        //拖影环形缓冲（本地表现）
        private const int TrailLen = 12;
        private readonly Vector2[] trailPos = new Vector2[TrailLen];
        private readonly float[] trailRot = new float[TrailLen];
        private int trailHead;
        private bool trailInit;

        //铐位轨迹环形缓冲：挥击/链旋/突刺的链风条带用（本地表现）
        internal const int CuffTrailLen = 10;
        private readonly Vector2[,] cuffTrail = new Vector2[2, CuffTrailLen];
        private int cuffTrailHead;
        private bool cuffTrailInit;
        /// <summary>本记挥击的链风条带是否已交给余辉（冲击拍一次）</summary>
        private int lastSwipeRibbonPushed = -1;

        //躯体帧动画（原版 Wraith 帧序，本地推进）
        private int bodyFrameTick;
        private int bodyFrameIndex;

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        internal float Seed => NPC.whoAmI * 0.7391f;

        //==================== 色板（灵质青灰 + 锈铁 + 冷粉狱火，对位牢狱层粉砖）====================

        internal static readonly Color EctoBody = new(172, 200, 208);
        internal static readonly Color EctoDeep = new(54, 78, 92);
        /// <summary>灵质苍白高光（怨魂/魂缘）</summary>
        internal static readonly Color EctoPale = new(223, 238, 241);
        internal static readonly Color GaolPink = new(236, 116, 156);
        internal static readonly Color GaolPinkDeep = new(118, 34, 66);
        /// <summary>二阶段狱火白热芯</summary>
        internal static readonly Color GaolWhiteHot = new(255, 214, 228);
        /// <summary>铁具乘法调色：原版灰铁压成冷紫锈</summary>
        internal static readonly Color IronMul = new(168, 158, 170);
        internal static readonly Color IronDeep = new(60, 54, 66);
        internal static readonly Color MistTint = new(96, 116, 128);

        /// <summary>狱火当前色，二阶段偏白热</summary>
        internal Color FireColor => Phase2 ? Color.Lerp(GaolPink, GaolWhiteHot, 0.55f) : GaolPink;

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
            NPC.width = 62;
            NPC.height = 88;
            NPC.damage = ContactDamage;
            NPC.defense = BaseDefense;
            NPC.lifeMax = BaseLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.npcSlots = 8f;
            NPC.value = Item.buyPrice(0, 5);
            NPC.HitSound = SoundID.NPCHit36;
            NPC.DeathSound = SoundID.NPCDeath39;
            Music = MusicID.Deerclops;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.DeepGaolWraith.Bestiary"),
            ]);
        }

        public override void BossHeadSlot(ref int index) {
            //暂借原版黑法师的地图头像（兜帽施法者）
            index = NPCID.Sets.BossHeadTextures[NPCID.DD2DarkMageT1];
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            //专属饰品不走掉落表：OnKill 经共用记录表逐人结算（首杀必掉/复杀 25%），与另两座 Boss 同权
            npcLoot.Add(ItemDropRule.Common(ItemID.Bone, 1, 20, 40));
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldenKey, 1, 1, 1));
            npcLoot.Add(ItemDropRule.Common(ItemID.HealingPotion, 1, 5, 10));
        }

        //==================== 全局转移与锁血 ====================

        /// <summary>全局转移，仅服务端驱动；出场/转阶段/死亡/脱战中不打断</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient) {
                return;
            }
            if (State is StateEmerge or StateRoar or StateDeath or StateDespawn) {
                return;
            }

            //目标失效：怨灵回到自己的班次
            if (TargetInvalid()) {
                ChangeState(StateDespawn);
                return;
            }

            //55%：清弹嘶吼，进二阶段
            if (!roarDone && Phase2) {
                roarDone = true;
                KillOwnedProjectiles();
                ChangeState(StateRoar);
            }
        }

        /// <summary>锁血：死亡演出没放完不许真死，一击超杀也拦回演出</summary>
        public override bool CheckDead() {
            if (!deathDone) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                if (!VaultUtils.isClient && State != StateDeath) {
                    KillOwnedProjectiles();
                    ChangeState(StateDeath);
                }
                return false;
            }
            return true;
        }

        /// <summary>服务端转场统一入口，盖 netUpdate 章</summary>
        private void ChangeState(int state) {
            State = state;
            StateTimer = 0;
            StateParam = 0;
            NPC.netUpdate = !VaultUtils.isClient;
        }

        private void EndAttack(int cooldown) {
            attackCooldown = cooldown;
            if (!VaultUtils.isClient) {
                ChangeState(StateFollow);
            }
        }

        /// <summary>清自家在场弹幕（转阶段/死亡公平阀），仅服务端</summary>
        private void KillOwnedProjectiles() {
            if (VaultUtils.isClient) {
                return;
            }
            int t1 = ModContent.ProjectileType<GaolFireBolt>();
            int t2 = ModContent.ProjectileType<GaolCrossChain>();
            int t3 = ModContent.ProjectileType<GaolCageBar>();
            int t4 = ModContent.ProjectileType<GaolCuffHitbox>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && (p.type == t1 || p.type == t2 || p.type == t3 || p.type == t4)) {
                    p.Kill();
                    p.netUpdate = true;
                }
            }
        }

        //==================== 主 AI ====================

        public override void AI() {
            NPC.netOffset = Vector2.Zero;
            NPC.dontTakeDamage = false;
            NPC.damage = 0;

            FindTarget();
            EvaluateGlobalTransitions();

            //换场清闩：远端可能靠收包切状态而非本地同拍转场，
            //上一场残闩会吞掉新场节拍（挥击弧、吼声、开火拍）
            if (State != lastSeenState) {
                lastSeenState = State;
                lastSwipeLaunched = -1;
                lastSwipeImpacted = -1;
                lastSwipeRibbonPushed = -1;
                lastBoltFired = -1;
                lastChainCalled = -1;
                flailRoared = false;
                cageBarsCalled = false;
                ambushBlinked = false;
                ambushLungeSet = false;
                roarCuePlayed = false;
                spinRot = 0f;
                if (State == StateDeath) {
                    cuffsOpened = false;
                    firePopped = false;
                    cuffLanded[0] = cuffLanded[1] = false;
                }
            }

            if (!cuffsInit) {
                RebuildCuffs();
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(); break;
                case StateFollow: UpdateFollow(); break;
                case StateSwipe: UpdateSwipe(); break;
                case StateVolley: UpdateVolley(); break;
                case StateFlail: UpdateFlail(); break;
                case StateCrossChains: UpdateCrossChains(); break;
                case StateCage: UpdateCage(); break;
                case StateAmbush: UpdateAmbush(); break;
                case StateRoar: UpdateRoar(); break;
                case StateDeath: UpdateDeath(); break;
                case StateDespawn: UpdateDespawn(); break;
            }

            UpdateCuffs();
            PushTrail();
            PushCuffTrails();
            UpdateBodyFrame();
            UpdateAmbientWisp();
            RequestScreenPresence();
            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (clenchTimer > 0) {
                clenchTimer--;
            }
            if (chainShiver > 0) {
                chainShiver--;
            }

            float glow = BodyAlpha() * (0.4f + 0.25f * HeartFireLevel());
            if (glow > 0.02f) {
                Lighting.AddLight(NPC.Center, 0.34f * glow, 0.12f * glow, 0.2f * glow);
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

        private bool TargetInvalid() {
            return targetPlayer == null || targetPlayer.dead || !targetPlayer.active
                || Math.Abs(NPC.position.X - targetPlayer.position.X) > MaxFindDistance
                || Math.Abs(NPC.position.Y - targetPlayer.position.Y) > MaxFindDistance;
        }

        /// <summary>目标预判位（弹幕/挥击共用的瞄准点）</summary>
        private Vector2 AimPos(float lead = 6f)
            => targetPlayer == null ? NPC.Center + new Vector2(0f, 300f)
                : targetPlayer.Center + targetPlayer.velocity * lead;

        /// <summary>朝向符号：目标在右为 +1</summary>
        private float FacingSign
            => targetPlayer != null && targetPlayer.Center.X >= NPC.Center.X ? 1f : -1f;

        /// <summary>自某点向下找地表 Y（世界像素），各端对同一世界数据确定性一致</summary>
        private static float FindGroundY(Vector2 from) {
            int tx = (int)(from.X / 16f);
            int ty = Math.Max(4, (int)(from.Y / 16f));
            for (int k = 0; k < 80; k++) {
                int y = ty + k;
                if (y >= Main.maxTilesY - 20) {
                    break;
                }
                if (WorldGen.SolidTile(tx, y)) {
                    return y * 16f;
                }
            }
            return from.Y + 96f;
        }

        //==================== 出场：铐先破土，怨灵后至 ====================

        private void UpdateEmerge() {
            if (IsSkullEmerge) {
                UpdateEmergeFromSkull();
                return;
            }
            int t = (int)StateTimer;

            //首拍服务器落位：贴到目标脚下地里，破土点从这里算
            if (t == 1 && !VaultUtils.isClient && targetPlayer != null) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 anchor = targetPlayer.Center + new Vector2(side * 130f, 0f);
                float ground = FindGroundY(anchor - new Vector2(0f, 60f));
                NPC.Center = new Vector2(anchor.X, ground + 52f);
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
            }

            //破土前逐帧对当前位置取地线（等服务器落位包），破土后闩死
            if (!emergeGroundLatched) {
                emergeGroundY = FindGroundY(NPC.Center - new Vector2(0f, 60f));
                if (t >= CuffsBreachFrame) {
                    emergeGroundLatched = true;
                }
            }
            float groundY = emergeGroundY;
            NPC.dontTakeDamage = t < BodyRiseFrame;

            if (t < CuffsBreachFrame) {
                //预兆：地下锁链声闷响，地表两点粉光收拢（绘制层），碎土先跳
                NPC.velocity = Vector2.Zero;
                if (t == 8 || t == 20) {
                    SoundEngine.PlaySound(SoundID.Item37 with {
                        Volume = 0.4f,
                        Pitch = t == 8 ? -0.8f : -0.55f,
                        MaxInstances = 2
                    }, new Vector2(NPC.Center.X, groundY));
                }
                if (!Main.dedServ && t > 6 && t % 4 == 1) {
                    float converge = 1f - t / (float)CuffsBreachFrame;
                    float side = t / 4 % 2 == 0 ? 1f : -1f;
                    Vector2 pos = new(NPC.Center.X + side * (30f + converge * 40f), groundY - 4f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.5f, 3f)),
                        IronDeep * 0.8f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(14, 24), 0f);
                }
                return;
            }

            if (!cuffsBreached) {
                //第二拍：双铐破土，两声铁响错半拍，链头甩出土瀑
                cuffsBreached = true;
                for (int i = 0; i < 2; i++) {
                    float side = CuffDir(i);
                    cuffPos[i] = new Vector2(NPC.Center.X + side * 56f, groundY + 2f);
                    cuffVel[i] = new Vector2(side * 0.6f, -8.5f);
                    SoundEngine.PlaySound(SoundID.NPCHit4 with {
                        Volume = 0.6f,
                        Pitch = -0.4f + i * 0.14f,
                        MaxInstances = 2
                    }, cuffPos[i]);
                    if (!Main.dedServ) {
                        BreachDust(new Vector2(cuffPos[i].X, groundY), 9);
                        GaolWraithScreenFX.PushRing(new Vector2(cuffPos[i].X, groundY), 0.4f, 220f, 18);
                    }
                }
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, NPC.Center);
                ShakeNearby(2.5f);
            }

            if (t < BodyRiseFrame) {
                return;
            }

            if (!bodyRisen) {
                //第三拍：鬼躯自砖缝间渗出，一帧起速 + 闷吼，雾幕遮住破土线
                bodyRisen = true;
                NPC.velocity = new Vector2(0f, -9.2f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.7f, MaxInstances = 2 }, NPC.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(
                            new Vector2(NPC.Center.X + Main.rand.NextFloat(-40f, 40f), groundY - 8f),
                            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 0.9f)),
                            MistTint * 0.9f, Main.rand.NextFloat(0.8f, 1.1f))
                            ?.Configure(Main.rand.Next(70, 110));
                    }
                }
            }

            //升起：起速后指数衰减，前快后慢，禁匀速
            NPC.velocity.Y *= 0.95f;
            NPC.velocity.X = 0f;

            if (!Main.dedServ && t < RiseEnd && t % 3 == 0) {
                //躯缘怨魂雾滴上升（灵质不坠反升）
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(10f, 40f)),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.5f, 1f)),
                    MistTint * 0.7f, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(40, 70));
            }

            if (!settleDipped && t >= RiseEnd + 2) {
                //落定拍：下沉半口再顶住，重量先答话
                settleDipped = true;
                NPC.velocity.Y = 1.3f;
            }

            if (!awakenDone && t >= AwakenFrame) {
                //觉醒拍：狱火点燃、双铐同时握拢脉冲
                awakenDone = true;
                clenchTimer = 14;
                SoundEngine.PlaySound(SoundID.DD2_DarkMageAttack with { Volume = 0.55f, Pitch = -0.35f, MaxInstances = 2 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.45f, Pitch = -0.7f, MaxInstances = 2 }, NPC.Center);
                ShakeNearby(1.5f);
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_DWave>(HeartPos(), Vector2.Zero, FireColor, 0.06f)
                        ?.Configure(new Vector2(0.85f, 1f), 0f, 0.24f, 10);
                    for (int k = 0; k < 8; k++) {
                        SpawnFireWisp(HeartPos(), Main.rand.NextVector2Circular(1.6f, 1.6f) - new Vector2(0f, 1.2f), 0.5f);
                    }
                }
            }

            //升起期微仰，觉醒后回正
            NPC.rotation = NPC.rotation.AngleLerp(t < AwakenFrame ? -0.07f : 0f, 0.15f);

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；服务器盖章纠偏
                attackCooldown = 45;
                if (!VaultUtils.isClient) {
                    ChangeState(StateFollow);
                }
            }
        }

        //==================== 出场变体：枯颅升起，双铐破墙 ====================

        /// <summary>自躯心向 side 侧扫第一堵实心墙，返回墙面朝房内一侧的世界像素 X；
        /// 找不到（野外测试召唤）给固定退避距离。各端对同一世界数据确定性一致</summary>
        private float FindWallX(float side) {
            int tx = (int)(NPC.Center.X / 16f);
            int ty = (int)(NPC.Center.Y / 16f);
            for (int k = 2; k < 56; k++) {
                int x = tx + (int)side * k;
                if (x < 4 || x >= Main.maxTilesX - 4) {
                    break;
                }
                if (WorldGen.SolidTile(x, ty)) {
                    return x * 16f + (side < 0f ? 16f : 0f);
                }
            }
            return NPC.Center.X + side * 430f;
        }

        private void UpdateEmergeFromSkull() {
            int t = (int)StateTimer;
            NPC.dontTakeDamage = t < 20;

            if (t == 1) {
                //枯颅位即出生位，不做破土落位；地线远置，DrawBody 的地线裁显自然失效
                emergeGroundLatched = true;
                emergeGroundY = NPC.Center.Y + 600f;
                NPC.velocity = new Vector2(0f, -2.8f);
            }

            if (t >= VariantBurstFrame && !cuffsBreached) {
                //双铐自两侧墙壁挣脱：墙点起步扑向躯侧，铁响错半拍 + 破墙尘瀑
                cuffsBreached = true;
                for (int i = 0; i < 2; i++) {
                    float side = CuffDir(i);
                    float wallX = FindWallX(side);
                    cuffPos[i] = new Vector2(wallX, NPC.Center.Y + Main.rand.NextFloat(-30f, 10f));
                    cuffVel[i] = new Vector2(-side * 7.5f, -1.2f);
                    SoundEngine.PlaySound(SoundID.NPCHit4 with {
                        Volume = 0.6f,
                        Pitch = -0.4f + i * 0.14f,
                        MaxInstances = 2
                    }, cuffPos[i]);
                    if (!Main.dedServ) {
                        WallBurstDust(cuffPos[i], side);
                    }
                }
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.5f, Pitch = -0.55f, MaxInstances = 2 }, NPC.Center);
                ShakeNearby(2.5f);
            }

            //升起：起速指数衰减，前快后慢，禁匀速
            NPC.velocity.Y *= 0.94f;
            NPC.velocity.X = 0f;

            //凝形雾：怨雾向枯颅收拢，躯体在雾里长出来
            if (!Main.dedServ && t > VariantBurstFrame && t < VariantAwakenFrame && t % 2 == 0) {
                Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 90f);
                PRTLoader.NewParticle<PRT_GhostRainMist>(from, (NPC.Center - from) * 0.06f,
                    MistTint * 0.8f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(20, 34));
            }

            if (!settleDipped && t >= VariantSettleFrame) {
                //落定拍：下沉半口再顶住，重量先答话
                settleDipped = true;
                NPC.velocity.Y = 1.1f;
            }

            if (!awakenDone && t >= VariantAwakenFrame) {
                //觉醒收尾拍：与破土版同一拍（狱火点燃、双铐同时握拢脉冲）
                awakenDone = true;
                clenchTimer = 14;
                SoundEngine.PlaySound(SoundID.DD2_DarkMageAttack with { Volume = 0.55f, Pitch = -0.35f, MaxInstances = 2 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.45f, Pitch = -0.7f, MaxInstances = 2 }, NPC.Center);
                ShakeNearby(1.5f);
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_DWave>(HeartPos(), Vector2.Zero, FireColor, 0.06f)
                        ?.Configure(new Vector2(0.85f, 1f), 0f, 0.24f, 10);
                    for (int k = 0; k < 8; k++) {
                        SpawnFireWisp(HeartPos(), Main.rand.NextVector2Circular(1.6f, 1.6f) - new Vector2(0f, 1.2f), 0.5f);
                    }
                }
            }

            //升起期微仰，觉醒后回正
            NPC.rotation = NPC.rotation.AngleLerp(t < VariantAwakenFrame ? -0.05f : 0f, 0.15f);

            if (t >= VariantEmergeTotal) {
                attackCooldown = 45;
                if (!VaultUtils.isClient) {
                    ChangeState(StateFollow);
                }
            }
        }

        /// <summary>破墙尘瀑：向房内横喷的碎屑 + 铁火花（side=铐所在侧符号）</summary>
        private void WallBurstDust(Vector2 hit, float side) {
            for (int k = 0; k < 8; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(hit + new Vector2(0f, Main.rand.NextFloat(-10f, 10f)),
                    new Vector2(-side * Main.rand.NextFloat(2f, 5.5f), Main.rand.NextFloat(-2.4f, 1.4f)),
                    Color.Lerp(IronDeep, MistTint, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(18, 30), 0f);
            }
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_Spark>(hit,
                    new Vector2(-side * Main.rand.NextFloat(1.5f, 3f), Main.rand.NextFloat(-1.6f, 1.6f)),
                    Color.Lerp(GaolPink, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>破土尘瀑：碎土珠 + 铁屑火花</summary>
        private void BreachDust(Vector2 hit, int count) {
            for (int k = 0; k < count; k++) {
                float ang = -MathHelper.Pi * (0.2f + 0.6f * k / (count - 1f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -2f),
                    ang.ToRotationVector2() * Main.rand.NextFloat(2f, 5.5f),
                    Color.Lerp(IronDeep, MistTint, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(18, 30), 0f);
            }
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_Spark>(hit, Main.rand.NextVector2Circular(2.2f, 2.2f) - new Vector2(0f, 1.5f),
                    Color.Lerp(GaolPink, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        //==================== 跟随与选招 ====================

        private void UpdateFollow() {
            if (targetPlayer == null) {
                NPC.velocity *= 0.95f;
                return;
            }
            //悬在目标侧上方，呼吸浮动；侧位带迟滞防抖
            float side = FacingSign >= 0f ? -1f : 1f;
            Vector2 anchor = targetPlayer.Center + new Vector2(side * 186f, -128f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 2.0f + Seed) * 8f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.2f + Seed * 2f) * 6f;

            Vector2 to = anchor - NPC.Center;
            if (to.Length() > 2400f) {
                //跟丢硬贴回来，双铐一并重建防抽搐
                NPC.Center = anchor;
                NPC.velocity = Vector2.Zero;
                RebuildCuffs();
                NPC.netUpdate = !VaultUtils.isClient;
                return;
            }
            Vector2 desired = to * 0.075f;
            const float maxSpeed = 14f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.12f);
            NPC.rotation = NPC.rotation.AngleLerp(
                MathHelper.Clamp(NPC.velocity.X * 0.04f, -0.26f, 0.26f), 0.12f);

            //出招裁决只在服务器，结果乘 ai 槽 + netUpdate 过线
            if (VaultUtils.isClient || attackCooldown > 0 || StateTimer <= 24 || TargetInvalid()) {
                return;
            }
            AttackIndex++;
            int idx = (int)AttackIndex;
            int pick;
            if (!Phase2) {
                //P1：铐击 → 狱火 → 拉锁 → 狱火 → 链旋 → 狱火
                pick = (idx % 6) switch {
                    1 => StateSwipe,
                    3 => StateCrossChains,
                    5 => StateFlail,
                    _ => StateVolley,
                };
            }
            else {
                //P2：隐袭 → 狱火 → 铐击 → 囚笼 → 狱火 → 拉锁 → 链旋 → 狱火
                pick = (idx % 8) switch {
                    1 => StateAmbush,
                    3 => StateSwipe,
                    4 => StateCage,
                    6 => StateCrossChains,
                    7 => StateFlail,
                    _ => StateVolley,
                };
            }
            ChangeState(pick);
            if (pick == StateSwipe) {
                //首铐＝目标所在侧，掌风顺着劈过去
                StateParam = targetPlayer.Center.X < NPC.Center.X ? 0 : 1;
                EnsureCuffHitboxes();
            }
            else if (pick == StateFlail) {
                EnsureCuffHitboxes();
            }
        }

        //==================== 铐击编码 ====================

        private int SwipeIndex => (int)StateParam / 2;
        private int SwipeFirstCuff => (int)StateParam % 2;
        /// <summary>本记的出手铐：首铐起、左右交替</summary>
        internal int ActiveCuff => (SwipeFirstCuff + SwipeIndex) % 2;

        /// <summary>铐的横向符号：左=-1 右=+1</summary>
        private static float CuffDir(int i) => i == 0 ? -1f : 1f;

        /// <summary>铐击挥击窗（GaolCuffHitbox 的 CanDamage 门）</summary>
        internal bool InSwipeStrikeWindow(int cuffIndex) {
            if (State != StateSwipe || SwipeIndex >= SwipeCount || cuffIndex != ActiveCuff) {
                return false;
            }
            int windup = SwipeWindups[Math.Min(SwipeIndex, SwipeWindups.Length - 1)];
            int t = (int)StateTimer;
            return t > windup && t <= windup + StrikeFrames + 4;
        }

        /// <summary>链旋达速窗（GaolCuffHitbox 与接触伤害共用）</summary>
        internal bool InFlailDamageWindow => State == StateFlail && (int)StateParam == 2;

        //==================== 铐击 ====================

        private void UpdateSwipe() {
            int swipeIdx = SwipeIndex;
            if (swipeIdx >= SwipeCount) {
                EndAttack(100);
                return;
            }
            int windup = SwipeWindups[Math.Min(swipeIdx, SwipeWindups.Length - 1)];
            int t = (int)StateTimer;

            if (targetPlayer == null) {
                EndAttack(45);
                return;
            }
            Vector2 aimPos = AimPos();

            //身位押阵：比跟随更贴近目标，躯体先探过去
            Vector2 lean = (aimPos - NPC.Center).SafeNormalize(Vector2.UnitX) * 30f;
            Vector2 anchor = targetPlayer.Center + new Vector2(-FacingSign * 150f, -120f) + lean;
            Vector2 desired = (anchor - NPC.Center) * 0.06f;
            if (desired.Length() > 9f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 9f;
            }
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.1f);
            NPC.rotation = NPC.rotation.AngleLerp(
                MathHelper.Clamp((aimPos.X - NPC.Center.X) * 0.0004f, -0.14f, 0.14f), 0.1f);

            if (t <= windup) {
                //蓄力：身后链条绷直战栗，72% 后静默吸气
                if (!Main.dedServ && t < windup * 0.72f && t % 3 == 1) {
                    Vector2 palm = cuffPos[ActiveCuff];
                    Vector2 from = palm + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 70f);
                    SpawnFireWisp(from, (palm - from) * 0.16f, 0.32f);
                }
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.35f, Pitch = -0.7f, MaxInstances = 3 }, cuffPos[ActiveCuff]);
                }
                return;
            }

            if (lastSwipeLaunched < swipeIdx) {
                //launch 一帧定弧：从举起位劈向目标并跟出半程；躯体吃反冲后仰
                lastSwipeLaunched = swipeIdx;
                ComputeSwipeArc(aimPos);
                Vector2 aimDir = (aimPos - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity -= aimDir * 2.4f;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.8f, Pitch = -0.35f + swipeIdx * 0.08f, MaxInstances = 3 }, cuffPos[ActiveCuff]);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.35f, Pitch = -0.5f, MaxInstances = 3 }, cuffPos[ActiveCuff]);
                ShakeNearby(1.2f);
            }

            //挥击窗内的链风铁屑：沿铐甩出速度拉伸
            if (!Main.dedServ && t <= windup + StrikeFrames) {
                int cuff = ActiveCuff;
                for (int k = 0; k < 2; k++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        cuffPos[cuff] + Main.rand.NextVector2Circular(14f, 14f),
                        cuffVel[cuff] * 0.26f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                        Color.Lerp(IronDeep, GaolPink, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.34f, 0.55f))?.Configure(Main.rand.Next(14, 24), 0f);
                }
            }

            if (t >= windup + ImpactAt && lastSwipeImpacted < swipeIdx) {
                //冲击拍：震屏 + 铁鞭双声 + 沿弧火花帘
                lastSwipeImpacted = swipeIdx;
                SwipeImpact();
            }

            if (t >= windup + StrikeFrames + SwipeRecover) {
                //本记结束，换铐
                StateParam += 2;
                StateTimer = 0;
                if (SwipeIndex >= SwipeCount) {
                    EndAttack(100);
                }
                else {
                    NPC.netUpdate = !VaultUtils.isClient;
                }
            }
        }

        /// <summary>launch 帧定弧线：起角=当前铐位，终角=目标向再跟出 0.55rad 的顺劈</summary>
        private void ComputeSwipeArc(Vector2 aimPos) {
            int cuff = ActiveCuff;
            Vector2 body = NPC.Center;
            swipeStartAng = (cuffPos[cuff] - body).ToRotation();
            float aimAng = (aimPos - body).ToRotation();
            float delta = MathHelper.WrapAngle(aimAng - swipeStartAng);
            float side = MathF.Sign(delta);
            if (side == 0f) {
                side = CuffDir(cuff);
            }
            swipeEndDelta = delta + side * 0.55f;
            swipeR0 = MathF.Max(Vector2.Distance(cuffPos[cuff], body), 110f);
            swipeR1 = MathHelper.Clamp(Vector2.Distance(aimPos, body), 160f, 320f);
        }

        /// <summary>冲击拍分层：震屏 + 重响双声 + 弧形火花帘幕</summary>
        private void SwipeImpact() {
            int cuff = ActiveCuff;
            Vector2 palm = cuffPos[cuff];
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 2 }, palm);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.65f, Pitch = -0.25f, MaxInstances = 3 }, palm);
            ShakeNearby(3f);

            if (Main.dedServ) {
                return;
            }
            Vector2 body = NPC.Center;
            for (int k = 0; k < 9; k++) {
                float ang = swipeStartAng + swipeEndDelta * (0.35f + 0.65f * k / 8f);
                float r = MathHelper.Lerp(swipeR0, swipeR1, 0.4f + 0.6f * k / 8f);
                Vector2 pos = body + ang.ToRotationVector2() * r;
                Vector2 fling = ang.ToRotationVector2() * Main.rand.NextFloat(1.4f, 3.2f);
                PRTLoader.NewParticle<PRT_Spark>(pos, fling,
                    Color.Lerp(GaolPink, Color.White, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 18));
            }
            PRTLoader.NewParticle<PRT_DWave>(palm, Vector2.Zero, GaolPinkDeep, 0.07f)
                ?.Configure(new Vector2(0.6f, 1f), cuffVel[cuff].ToRotation(), 0.24f, 9);

            //链风余韵：本记挥击的铐轨迹交给余辉缓冲，活过挥击本身
            if (lastSwipeRibbonPushed < SwipeIndex) {
                lastSwipeRibbonPushed = SwipeIndex;
                GaolWraithScreenFX.PushAfterglow(this, cuff, 30f, ironWind: true);
            }
        }

        //==================== 狱火连弹 ====================

        /// <summary>灯架口位：双铐合拢时狱火汇聚点</summary>
        internal Vector2 HeartPos()
            => NPC.Center + new Vector2(0f, -6f).RotatedBy(NPC.rotation) * BodyDrawScale;

        private Vector2 LanternPos()
            => NPC.Center + new Vector2(FacingSign * 34f, -22f);

        private void UpdateVolley() {
            int t = (int)StateTimer;
            int phase = (int)StateParam;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                NPC.netUpdate = !VaultUtils.isClient;
            }

            Vector2 aim = (AimPos(8f) - LanternPos()).SafeNormalize(Vector2.UnitY);

            if (phase == 0) {
                //定身昂首
                if (targetPlayer == null) {
                    EndAttack(45);
                    return;
                }
                NPC.velocity *= 0.85f;
                NPC.rotation = NPC.rotation.AngleLerp(
                    MathHelper.Clamp(aim.X * 0.2f, -0.28f, 0.28f) - 0.08f, 0.2f);
                if (t >= VolleyAimFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //蓄力：狱火向灯架汇聚，72% 静默截断
                NPC.velocity *= 0.9f;
                float charge = t / (float)VolleyChargeFrames;
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
                }
                if (!Main.dedServ && charge < 0.72f && t % 2 == 0) {
                    Vector2 mouth = LanternPos();
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(44f, 90f);
                    SpawnFireWisp(from, (mouth - from) * 0.14f, 0.3f + charge * 0.2f);
                }
                if (t >= VolleyChargeFrames) {
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                //连发；窗口闩出手，远端迟到换场也补得上节拍
                int shotIndex = (t - 1) / VolleyGap;
                if (shotIndex < VolleyCount && lastBoltFired < shotIndex) {
                    lastBoltFired = shotIndex;
                    FireGaolBolt(aim, shotIndex);
                }
                NPC.velocity *= 0.9f;
                if (t >= VolleyGap * VolleyCount) {
                    NextPhase(3);
                }
                return;
            }

            //回摆
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.15f);
            NPC.velocity *= 0.92f;
            if (t >= VolleyRecoverFrames) {
                EndAttack(70);
            }
        }

        /// <summary>放一发追踪狱火：后坐上仰各端同拍，弹体只在服务器生成</summary>
        private void FireGaolBolt(Vector2 aim, int shotIndex) {
            NPC.velocity -= aim * 2.6f;
            NPC.velocity.Y -= 0.9f;
            NPC.rotation -= 0.06f;

            Vector2 mouth = LanternPos();
            SoundEngine.PlaySound(SoundID.DD2_DarkMageAttack with { Volume = 0.5f, Pitch = -0.15f, MaxInstances = 3 }, mouth);
            if (!Main.dedServ) {
                for (int i = 0; i < 5; i++) {
                    SpawnFireWisp(mouth + Main.rand.NextVector2Circular(3f, 3f),
                        aim.RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 6f), 0.42f);
                }
                PRTLoader.NewParticle<PRT_DWave>(mouth + aim * 8f, Vector2.Zero, FireColor, 0.06f)
                    ?.Configure(new Vector2(0.55f, 1f), aim.ToRotation(), 0.2f, 8);
            }
            ShakeNearby(0.8f);

            if (!VaultUtils.isClient) {
                Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-0.07f, 0.07f)) * 10.5f;
                //吐是抛出去的：上抛偏置配合弹体前段微重力走弧线
                vel.Y -= 1.4f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), mouth, vel,
                    ModContent.ProjectileType<GaolFireBolt>(), ScaleDamage(FireBoltDamage), 2f,
                    Main.myPlayer, NPC.target, shotIndex % 2 == 0 ? 1f : -1f);
            }
        }

        //==================== 链旋 ====================

        /// <summary>链旋角速度：加速平方爬升、达速恒定、收势指数衰减，全由相位计时确定</summary>
        private float FlailOmega() {
            if (State != StateFlail) {
                return 0f;
            }
            int t = (int)StateTimer;
            return (int)StateParam switch {
                1 => FlailMaxOmega * MathF.Pow(MathHelper.Clamp(t / (float)FlailSpinupFrames, 0f, 1f), 2f),
                2 => FlailMaxOmega * (Phase2 ? 1.15f : 1f),
                3 => FlailMaxOmega * MathF.Pow(0.88f, t),
                _ => 0f,
            };
        }

        /// <summary>链旋当前甩链半径：起旋展开、收势缠回</summary>
        internal float FlailRadius() {
            if (State != StateFlail) {
                return 0f;
            }
            int t = (int)StateTimer;
            return (int)StateParam switch {
                1 => MathHelper.Lerp(46f, FlailMaxRadius, MathF.Sqrt(MathHelper.Clamp(t / (float)FlailSpinupFrames, 0f, 1f))),
                2 => FlailMaxRadius,
                3 => MathHelper.Lerp(FlailMaxRadius, 40f, MathHelper.Clamp(t / (float)FlailSpindownFrames, 0f, 1f)),
                _ => 0f,
            };
        }

        private void UpdateFlail() {
            int t = (int)StateTimer;
            int phase = (int)StateParam;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                NPC.netUpdate = !VaultUtils.isClient;
            }

            spinRot += FlailOmega();

            if (phase == 0) {
                //收铐护体，躯体后拉半步蓄势
                Vector2 back = targetPlayer != null
                    ? (NPC.Center - targetPlayer.Center).SafeNormalize(-Vector2.UnitY)
                    : -Vector2.UnitY;
                float k = MathF.Pow(t / (float)FlailTuckFrames, 3f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, back * (1.4f + 4.5f * k), 0.2f);
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 }, NPC.Center);
                }
                if (t >= FlailTuckFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //原地加速自旋：链条哗啦声随转速爬调，72% 后粒子静默
                NPC.velocity *= 0.86f;
                float charge = t / (float)FlailSpinupFrames;
                if (t % 6 == 0) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with {
                        Volume = 0.26f,
                        Pitch = -0.8f + charge * 0.75f,
                        MaxInstances = 3
                    }, NPC.Center);
                }
                if (!Main.dedServ && charge < 0.72f && t % 2 == 0) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(50f, 120f);
                    SpawnFireWisp(from, (NPC.Center - from) * 0.12f, 0.3f);
                }
                if (t >= FlailSpinupFrames) {
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                //达速漂移逼近：伤害窗开启，威压来自躲不开的慢
                NPC.damage = ContactDamage;
                if (!flailRoared) {
                    flailRoared = true;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 2 }, NPC.Center);
                    ShakeNearby(2f);
                }
                if (targetPlayer != null && !TargetInvalid()) {
                    Vector2 aim = (targetPlayer.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, aim * 7.4f, 0.05f);
                }
                else {
                    NPC.velocity *= 0.97f;
                    if (t > 30) {
                        NextPhase(3);
                        return;
                    }
                }
                //离心火花：铐口沿切线甩出
                if (!Main.dedServ && t % 3 == 0) {
                    int i = t / 3 % 2;
                    PRTLoader.NewParticle<PRT_Spark>(cuffPos[i],
                        (spinRot + i * MathHelper.Pi + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(2.5f, 5f),
                        Color.Lerp(GaolPink, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(8, 14));
                }
                if (t % 14 == 0) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.3f, Pitch = 0.15f, MaxInstances = 2 }, NPC.Center);
                }
                if (t >= FlailChaseFrames) {
                    NextPhase(3);
                }
                return;
            }

            if (phase == 3) {
                //收势踉跄：链缠回身上，身位左摇右晃再下沉半口
                float dir = (int)(Seed * 13f) % 2 == 0 ? 1f : -1f;
                if (t == 2) {
                    NPC.velocity.X += dir * 3f;
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
                    ShakeNearby(2f);
                }
                if (t == 10) {
                    NPC.velocity.X -= dir * 2f;
                    NPC.velocity.Y += 1.6f;
                }
                NPC.velocity *= 0.9f;
                if (t >= FlailSpindownFrames) {
                    NextPhase(4);
                }
                return;
            }

            //回正
            NPC.rotation = MathHelper.WrapAngle(NPC.rotation).AngleLerp(0f, 0.14f);
            NPC.velocity *= 0.92f;
            if (t >= FlailRecoverFrames) {
                EndAttack(120);
            }
        }

        //==================== 横贯拉锁 ====================

        private void UpdateCrossChains() {
            int t = (int)StateTimer;
            if (targetPlayer == null) {
                EndAttack(45);
                return;
            }

            //拉杆姿势：定身，双铐高举，每道锁链落下时向下猛拽（表现在 UpdateCuffs）
            NPC.velocity *= 0.9f;
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.12f);

            //布链拍：P1 上下两道夹击，P2 加一道斜穿；只在服务器生成
            int chainBeat = t switch {
                ChainsPoseEnd => 0,
                ChainsPoseEnd + 12 => 1,
                ChainsPoseEnd + 24 when Phase2 => 2,
                _ => -1,
            };
            if (chainBeat >= 0 && lastChainCalled < chainBeat) {
                lastChainCalled = chainBeat;
                clenchTimer = 10;
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.45f + chainBeat * 0.12f, MaxInstances = 3 }, NPC.Center);
                if (!VaultUtils.isClient) {
                    Vector2 mid;
                    float rot;
                    if (chainBeat == 0) {
                        mid = new Vector2(targetPlayer.Center.X, targetPlayer.Center.Y - 116f);
                        rot = 0f;
                    }
                    else if (chainBeat == 1) {
                        mid = new Vector2(targetPlayer.Center.X, targetPlayer.Center.Y + 84f);
                        rot = 0f;
                    }
                    else {
                        mid = targetPlayer.Center;
                        rot = Main.rand.NextBool() ? 0.21f : -0.21f;
                    }
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), mid, Vector2.Zero,
                        ModContent.ProjectileType<GaolCrossChain>(), ScaleDamage(CrossChainDamage), 3f,
                        Main.myPlayer, rot, 620f);
                }
            }

            if (t >= ChainsStateTotal) {
                EndAttack(110);
            }
        }

        //==================== 囚笼合围（P2）====================

        private void UpdateCage() {
            int t = (int)StateTimer;
            if (targetPlayer == null) {
                EndAttack(45);
                return;
            }

            if (t <= CagePoseFrames) {
                //过顶合击预告：双铐举顶收拢，狱火胀大
                NPC.velocity *= 0.88f;
                if (t == 4) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.5f, Pitch = -0.7f, MaxInstances = 2 }, NPC.Center);
                }
                if (!Main.dedServ && t % 3 == 0 && t < CagePoseFrames * 0.72f) {
                    Vector2 top = NPC.Center + new Vector2(0f, -60f);
                    Vector2 from = top + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 90f);
                    SpawnFireWisp(from, (top - from) * 0.15f, 0.36f);
                }
                return;
            }

            if (!cageBarsCalled) {
                //圈笼拍：以目标为心布 12 槽静止链栏，留对置双缺口
                cageBarsCalled = true;
                clenchTimer = 12;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
                ShakeNearby(2.5f);
                if (!VaultUtils.isClient) {
                    const int slots = 12;
                    int gap = Main.rand.Next(slots);
                    Vector2 center = targetPlayer.Center;
                    for (int k = 0; k < slots; k++) {
                        if (k == gap || k == (gap + slots / 2) % slots) {
                            continue;
                        }
                        float ang = MathHelper.TwoPi * k / slots;
                        Vector2 pos = center + ang.ToRotationVector2() * 260f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero,
                            ModContent.ProjectileType<GaolCageBar>(), ScaleDamage(CageBarDamage), 2f,
                            Main.myPlayer, ang + MathHelper.PiOver2, 0f);
                    }
                }
            }

            //笼内压迫：飘向目标侧，隔一拍打一发狱火
            Vector2 anchor = targetPlayer.Center + new Vector2(-FacingSign * 170f, -90f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.05f, 0.1f);

            int shotBeat = Array.IndexOf(CageShotBeats, t);
            if (shotBeat >= 0 && lastBoltFired < shotBeat) {
                lastBoltFired = shotBeat;
                FireGaolBolt((AimPos(6f) - LanternPos()).SafeNormalize(Vector2.UnitY), shotBeat);
            }

            if (t >= CageStateTotal) {
                EndAttack(140);
            }
        }

        //==================== 穿墙隐袭（P2）====================

        private int AmbushPhase => (int)StateParam % 16;
        private int AmbushRound => (int)StateParam / 16;

        private void UpdateAmbush() {
            int t = (int)StateTimer;
            int phase = AmbushPhase;
            int round = AmbushRound;

            void NextPhase(int next, bool nextRound = false) {
                StateParam = next + (nextRound ? round + 1 : round) * 16;
                StateTimer = 0;
                NPC.netUpdate = !VaultUtils.isClient;
            }

            if (targetPlayer == null) {
                EndAttack(45);
                return;
            }

            if (phase == 0) {
                //链缠成雾：躯体塌缩、雾向心内吸
                int veil = round == 0 ? AmbushVeilFrames : AmbushReveilFrames;
                NPC.velocity *= 0.88f;
                NPC.dontTakeDamage = t > veil / 2;
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.NPCHit36 with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, NPC.Center);
                    //全屏薄雾拍：雾里藏着要来的东西（全屏效果只给近处的人）
                    if (!Main.dedServ && Main.LocalPlayer != null
                        && Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1800f) {
                        GaolWraithScreenFX.PushMist(0.8f);
                    }
                }
                if (!Main.dedServ && t % 2 == 0) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 90f);
                    PRTLoader.NewParticle<PRT_GhostRainMist>(from, (NPC.Center - from) * 0.06f,
                        MistTint * 0.8f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(20, 34));
                }
                if (t >= veil) {
                    //消隐末帧服务器闪现到侧翼，位置乘同步包过线
                    if (!VaultUtils.isClient && !ambushBlinked) {
                        float side = Main.rand.NextBool() ? 1f : -1f;
                        NPC.Center = targetPlayer.Center + new Vector2(side * 260f, -46f);
                        NPC.velocity = Vector2.Zero;
                        NPC.netUpdate = true;
                    }
                    ambushBlinked = true;
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //侧翼预告：人未至光先到，狱火光晕 + 雾旋 + 链声（各端按同步后的位置画）
                NPC.dontTakeDamage = true;
                NPC.velocity = Vector2.Zero;
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 2 }, NPC.Center);
                    //侧翼预告环：人未至，先在雾里敲一记位置
                    if (!Main.dedServ) {
                        GaolWraithScreenFX.PushRing(NPC.Center, 0.5f, 210f, 18);
                    }
                }
                if (!Main.dedServ && t % 2 == 0) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 70f);
                    PRTLoader.NewParticle<PRT_GhostRainMist>(from, (NPC.Center - from) * 0.08f,
                        Color.Lerp(MistTint, GaolPink, 0.4f) * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(16, 26));
                }
                if (t >= AmbushWarnFrames) {
                    NextPhase(2);
                }
                return;
            }

            if (phase == 2) {
                //凝形：透明度爬回，凝定即可受击
                NPC.dontTakeDamage = t < AmbushFormFrames / 2;
                NPC.velocity = Vector2.Zero;
                if (t >= AmbushFormFrames) {
                    NextPhase(3);
                }
                return;
            }

            if (phase == 3) {
                //锁定后仰：pow(8) 末段猛收一口气，视线咬死目标
                if (t == 1) {
                    ambushAim = (targetPlayer.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    ambushLungeSet = false;
                }
                float k = MathF.Pow(t / (float)AmbushAimFrames, 8f);
                NPC.velocity = -ambushAim * (k * 6.5f);
                NPC.rotation = NPC.rotation.AngleLerp(ambushAim.X * 0.2f, 0.3f);
                if (t >= AmbushAimFrames) {
                    NextPhase(4);
                }
                return;
            }

            if (phase == 4) {
                //直线突刺：一帧定速，链在身后拉直；速度门槛对齐可见冲势
                if (!ambushLungeSet) {
                    ambushLungeSet = true;
                    NPC.velocity = ambushAim * AmbushLungeSpeed;
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
                    ShakeNearby(2f);
                }
                if (NPC.velocity.Length() > 18f) {
                    NPC.damage = ContactDamage;
                }
                if (t >= AmbushLungeFrames) {
                    NextPhase(5);
                }
                return;
            }

            //急刹踉跄
            NPC.velocity *= 0.72f;
            if (t == 4) {
                NPC.velocity += new Vector2(-ambushAim.Y, ambushAim.X) * 1.8f;
            }
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.12f);
            if (t >= AmbushBrakeFrames) {
                if (round == 0) {
                    //再隐一次，第二段突刺
                    ambushBlinked = false;
                    NextPhase(0, nextRound: true);
                }
                else {
                    EndAttack(110);
                }
            }
        }

        //==================== 转阶段嘶吼 ====================

        private void UpdateRoar() {
            int t = (int)StateTimer;
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;
            if (!roarCuePlayed && t >= 4) {
                roarCuePlayed = true;
                chainShiver = 34;
                clenchTimer = 16;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 1 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCHit36 with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 1 }, NPC.Center);
                ShakeNearby(3f);
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_DWave>(HeartPos(), Vector2.Zero, GaolWhiteHot, 0.08f)
                        ?.Configure(new Vector2(0.9f, 1f), 0f, 0.3f, 12);
                    for (int k = 0; k < 10; k++) {
                        SpawnFireWisp(HeartPos(), Main.rand.NextVector2Circular(2.4f, 2.4f) - new Vector2(0f, 1f), 0.55f);
                    }
                    //转阶段拍：屏幕层冲击环，狱火白热化的宣告
                    GaolWraithScreenFX.PushRing(HeartPos(), 0.9f, 470f, 26);
                }
            }
            //后仰嘶吼姿态
            NPC.rotation = NPC.rotation.AngleLerp(t < RoarFrames / 2 ? -0.18f : 0f, 0.12f);
            if (t >= RoarFrames) {
                attackCooldown = 60;
                if (!VaultUtils.isClient) {
                    ChangeState(StateFollow);
                }
            }
        }

        //==================== 死亡演出：铐开人散 ====================

        private void UpdateDeath() {
            int t = (int)StateTimer;
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.92f;
            NPC.velocity.Y += 0.02f;
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.06f);

            if (!cuffsOpened && t >= DeathCuffOpenAt) {
                //铐开拍：两只铁铐弹开，囚徒散监
                cuffsOpened = true;
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.9f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 2 }, NPC.Center);
                for (int i = 0; i < 2; i++) {
                    cuffVel[i] = new Vector2(CuffDir(i) * Main.rand.NextFloat(2.4f, 3.4f), -Main.rand.NextFloat(2f, 3f));
                }
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        for (int k = 0; k < 4; k++) {
                            PRTLoader.NewParticle<PRT_Spark>(cuffPos[i], Main.rand.NextVector2Circular(2f, 2f),
                                Color.Lerp(GaolPink, Color.White, 0.4f), Main.rand.NextFloat(0.4f, 0.6f))
                                ?.Configure(true, Main.rand.Next(8, 14));
                        }
                        //链节脱落：主链沿弧逐节崩解成带重力的铁节
                        Vector2 shoulder = NPC.Center + new Vector2(CuffDir(i) * 16f, -6f) * BodyDrawScale;
                        Vector2 mid = (shoulder + cuffPos[i]) * 0.5f + new Vector2(0f, 26f);
                        for (int k = 0; k < 6; k++) {
                            Vector2 p = Bezier(shoulder, mid, cuffPos[i], (k + 0.5f) / 6f);
                            PRTLoader.NewParticle<PRT_GaolChainLink>(p,
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.4f, 1.6f)),
                                Color.White, Main.rand.NextFloat(0.85f, 1.05f))
                                ?.Configure(Main.rand.Next(50, 80), Main.rand.NextFloat(-0.2f, 0.2f));
                        }
                    }
                }
            }

            //怨魂上升：链节化雾、躯体自下而上散去，苍白囚魂逐个脱监
            if (!Main.dedServ && t > DeathCuffOpenAt && t % 2 == 0) {
                Vector2 pos = NPC.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(-10f, 44f));
                PRTLoader.NewParticle<PRT_GhostRainMist>(pos,
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.7f, 1.4f)),
                    Color.Lerp(MistTint, EctoBody, 0.5f) * 0.7f, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(40, 70));
            }
            if (!Main.dedServ && t > DeathCuffOpenAt + 6 && t < DeathPopAt && t % 9 == 0) {
                //散监的怨魂：自散躯前沿升起，摆着走远（活得比演出久，谢幕后仍在飘）
                float dissolveY = NPC.Center.Y + 44f - DeathDissolveT() * 96f;
                PRTLoader.NewParticle<PRT_GaolSoulShade>(
                    new Vector2(NPC.Center.X + Main.rand.NextFloat(-26f, 26f), dissolveY),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 1.7f)),
                    Color.Lerp(EctoBody, EctoPale, Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(70, 110));
            }

            if (!firePopped && t >= DeathPopAt) {
                //火芯谢幕：全场唯一的大拍
                firePopped = true;
                SoundEngine.PlaySound(SoundID.NPCDeath39 with { Volume = 1f, Pitch = -0.3f, MaxInstances = 1 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.DD2_DarkMageDeath with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 1 }, NPC.Center);
                ShakeNearby(4f, 1400f);
                if (!Main.dedServ) {
                    //全场唯一的冲击帧 + 大环：狱火谢幕（冲击帧是全屏效果，只给看得见的人）
                    if (Main.LocalPlayer != null && Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1800f) {
                        GaolWraithScreenFX.PushFlash(1f, 26);
                    }
                    GaolWraithScreenFX.PushRing(HeartPos(), 1.1f, 560f, 28);
                    PRTLoader.NewParticle<PRT_DWave>(HeartPos(), Vector2.Zero, FireColor, 0.1f)
                        ?.Configure(new Vector2(1f, 1f), 0f, 0.42f, 14);
                    for (int k = 0; k < 14; k++) {
                        SpawnFireWisp(HeartPos(), Main.rand.NextVector2Circular(3.2f, 3.2f) - new Vector2(0f, 1.4f), 0.6f);
                    }
                    for (int k = 0; k < 4; k++) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(HeartPos() + Main.rand.NextVector2Circular(20f, 20f),
                            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1f)),
                            MistTint * 0.8f, Main.rand.NextFloat(0.8f, 1.1f))?.Configure(Main.rand.Next(60, 100));
                    }
                }
            }

            if (t >= DeathTotal) {
                //放行真死：战利品照常掉落
                deathDone = true;
                NPC.dontTakeDamage = false;
                if (!VaultUtils.isClient) {
                    NPC.StrikeInstantKill();
                }
            }
        }

        //==================== 脱战撤离 ====================

        private void UpdateDespawn() {
            int t = (int)StateTimer;
            NPC.velocity.X *= 0.96f;
            NPC.velocity.Y = MathF.Max(NPC.velocity.Y - 0.12f, -6f);
            NPC.dontTakeDamage = t > 20;
            if (!Main.dedServ && t % 3 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + Main.rand.NextVector2Circular(24f, 30f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f)),
                    MistTint * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(30, 50));
            }
            if (t > 60) {
                NPC.EncourageDespawn(10);
            }
            //目标回场就收势归队
            if (!VaultUtils.isClient && !TargetInvalid() && t < 50) {
                attackCooldown = 60;
                ChangeState(StateFollow);
            }
        }

        //==================== 双铐推进（各端本地重建）====================

        internal Vector2 GetCuffPos(int i) => cuffPos[i];
        internal Vector2 GetCuffVel(int i) => cuffVel[i];

        private void RebuildCuffs() {
            cuffsInit = true;
            for (int i = 0; i < 2; i++) {
                float side = CuffDir(i);
                if (State == StateEmerge && StateTimer < CuffsBreachFrame) {
                    cuffPos[i] = new Vector2(NPC.Center.X + side * 56f, NPC.Center.Y + 40f);
                }
                else {
                    cuffPos[i] = HoverPost(i);
                }
                cuffVel[i] = Vector2.Zero;
                cuffRot[i] = 0f;
            }
        }

        /// <summary>跟随态铐位锚：躯侧偏下，呼吸浮动错相位</summary>
        private Vector2 HoverPost(int i) {
            float side = CuffDir(i);
            Vector2 post = NPC.Center + NPC.velocity + new Vector2(side * 74f, 34f);
            post.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + Seed + i * 2.1f) * 6f;
            post.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.5f + Seed * 2f + i * 1.7f) * 4f;
            return post;
        }

        /// <summary>铐口指向 dir 时的贴图旋转（Shackle 铐口朝上画）</summary>
        private static float CuffAimRot(Vector2 dir) => dir.ToRotation() + MathHelper.PiOver2;

        private void UpdateCuffs() {
            //本帧渲染位 = Center + velocity（AI 在位移积分前跑）
            Vector2 body = NPC.Center + NPC.velocity;
            int t = (int)StateTimer;

            //硬纠检测：同步包把躯体拽走半屏（隐袭闪现属正常路径），双铐直接归位
            if (Vector2.Distance(cuffPos[0], body) > 620f || Vector2.Distance(cuffPos[1], body) > 620f) {
                RebuildCuffs();
                return;
            }

            Vector2 aimPos = AimPos();

            for (int i = 0; i < 2; i++) {
                Vector2 prev = cuffPos[i];
                float side = CuffDir(i);
                Vector2 anchor;
                float wantRot;
                float chase = 0.14f;
                float lerpV = 0.3f;
                float maxSpd = 24f;
                bool kinematic = false;

                switch (State) {
                    case StateEmerge: {
                        if (IsSkullEmerge) {
                            //变体：破墙前双铐无形钉在躯侧；破墙后（位置已被 UpdateEmergeFromSkull
                            //设到墙点）弹簧扑回悬停位，觉醒后铐口咬住目标
                            if (t < VariantBurstFrame) {
                                cuffPos[i] = new Vector2(body.X + side * 56f, body.Y + 10f);
                                cuffVel[i] = Vector2.Zero;
                                cuffRot[i] = 0f;
                                continue;
                            }
                            anchor = HoverPost(i);
                            wantRot = t >= VariantAwakenFrame
                                ? CuffAimRot((aimPos - cuffPos[i]).SafeNormalize(Vector2.UnitY))
                                : CuffAimRot((body - cuffPos[i]).SafeNormalize(Vector2.UnitY));
                            chase = 0.16f;
                            lerpV = 0.3f;
                            break;
                        }
                        if (t < CuffsBreachFrame) {
                            //地下待命：钉住不动
                            cuffPos[i] = new Vector2(body.X + side * 56f, emergeGroundY + 18f);
                            cuffVel[i] = Vector2.Zero;
                            cuffRot[i] = 0f;
                            continue;
                        }
                        //破土后先扑到地表上方的临时位，躯体出来了再退让到侧位
                        anchor = t < BodyRiseFrame
                            ? new Vector2(body.X + side * 62f, emergeGroundY - 52f)
                            : HoverPost(i);
                        wantRot = 0f;
                        if (t >= AwakenFrame) {
                            wantRot = CuffAimRot((aimPos - cuffPos[i]).SafeNormalize(Vector2.UnitY));
                        }
                        chase = 0.2f;
                        lerpV = 0.36f;
                        break;
                    }
                    case StateSwipe: {
                        int swipeIdx = SwipeIndex;
                        int windup = swipeIdx < SwipeCount ? SwipeWindups[Math.Min(swipeIdx, SwipeWindups.Length - 1)] : 20;
                        bool isActive = i == ActiveCuff && swipeIdx < SwipeCount;
                        if (isActive && t <= windup) {
                            //蓄力举铐：高举过顶并向背离目标侧后拉，pow(6) 憋到最后猛吸一口气
                            Vector2 awayDir = (cuffPos[i] - aimPos).SafeNormalize(-Vector2.UnitY);
                            float k = MathF.Pow(t / (float)windup, 6f);
                            anchor = body + new Vector2(side * 42f, -150f) + awayDir * (k * 44f);
                            wantRot = CuffAimRot((aimPos - cuffPos[i]).SafeNormalize(Vector2.UnitY));
                            chase = 0.16f;
                            lerpV = 0.34f;
                        }
                        else if (isActive && t <= windup + StrikeFrames) {
                            //挥击窗：沿定参弧线运动学摆位，几乎全部角程压在前几帧
                            float k = (t - windup) / (float)StrikeFrames;
                            float ease = 1f - MathF.Pow(1f - k, 9f);
                            float ang = swipeStartAng + swipeEndDelta * ease;
                            float r = MathHelper.Lerp(swipeR0, swipeR1, MathF.Min(1f, ease * 1.2f));
                            cuffPos[i] = body + ang.ToRotationVector2() * r;
                            cuffVel[i] = cuffPos[i] - prev;
                            cuffRot[i] = cuffRot[i].AngleLerp(
                                CuffAimRot(cuffVel[i].SafeNormalize(Vector2.UnitY)), 0.7f);
                            kinematic = true;
                            anchor = cuffPos[i];
                            wantRot = cuffRot[i];
                        }
                        else if (isActive) {
                            //收势：先硬刹掉挥速再弹回悬停位
                            cuffVel[i] *= t <= windup + StrikeFrames + 4 ? 0.68f : 1f;
                            anchor = HoverPost(i);
                            wantRot = CuffAimRot((aimPos - cuffPos[i]).SafeNormalize(Vector2.UnitY));
                            chase = 0.1f;
                            lerpV = 0.22f;
                        }
                        else {
                            //闲铐压低撑场，铐口咬住目标
                            anchor = HoverPost(i) + new Vector2(side * 8f, 14f);
                            wantRot = CuffAimRot((aimPos - cuffPos[i]).SafeNormalize(Vector2.UnitY));
                        }
                        break;
                    }
                    case StateFlail: {
                        int phase = (int)StateParam;
                        if (phase == 0) {
                            //收拢护体
                            anchor = body + new Vector2(side * 30f, 8f);
                            wantRot = CuffAimRot((body - cuffPos[i]).SafeNormalize(-Vector2.UnitY));
                            chase = 0.2f;
                        }
                        else if (phase <= 3) {
                            //链锤轨道：贴着甩链半径转，铐口朝外读出离心
                            float orbit = spinRot + (i == 0 ? MathHelper.Pi : 0f);
                            cuffPos[i] = body + orbit.ToRotationVector2() * FlailRadius();
                            cuffVel[i] = cuffPos[i] - prev;
                            cuffRot[i] = cuffRot[i].AngleLerp(CuffAimRot(orbit.ToRotationVector2()), 0.5f);
                            kinematic = true;
                            anchor = cuffPos[i];
                            wantRot = cuffRot[i];
                        }
                        else {
                            anchor = HoverPost(i);
                            wantRot = MathHelper.Pi;
                        }
                        break;
                    }
                    case StateVolley: {
                        int phase = (int)StateParam;
                        if (phase is 1 or 2) {
                            //灯架：双铐收拢到胸前托住狱火
                            Vector2 mouth = LanternPos();
                            anchor = mouth + new Vector2(side * 26f, 12f);
                            wantRot = CuffAimRot((mouth - cuffPos[i]).SafeNormalize(-Vector2.UnitY));
                            chase = 0.2f;
                        }
                        else {
                            anchor = HoverPost(i);
                            wantRot = CuffAimRot((aimPos - cuffPos[i]).SafeNormalize(Vector2.UnitY));
                        }
                        break;
                    }
                    case StateCrossChains: {
                        //拉杆姿势：双铐高举，clench 脉冲期向下猛拽
                        anchor = body + new Vector2(side * 52f, clenchTimer > 0 ? -66f : -108f);
                        wantRot = MathHelper.Pi;
                        chase = clenchTimer > 0 ? 0.34f : 0.16f;
                        break;
                    }
                    case StateCage: {
                        if (t <= CagePoseFrames) {
                            //过顶合拢
                            anchor = body + new Vector2(side * 16f, -74f);
                            wantRot = 0f;
                            chase = 0.2f;
                        }
                        else {
                            anchor = HoverPost(i);
                            wantRot = CuffAimRot((aimPos - cuffPos[i]).SafeNormalize(Vector2.UnitY));
                        }
                        break;
                    }
                    case StateAmbush: {
                        int phase = AmbushPhase;
                        if (phase is 0 or 1 or 2) {
                            //缠身/隐没：铐贴紧躯体
                            anchor = body + new Vector2(side * 18f, 4f);
                            wantRot = 0f;
                            chase = 0.3f;
                        }
                        else if (phase == 4) {
                            //突刺：双铐甩在身后，链拉直
                            anchor = body - ambushAim * 74f + new Vector2(side * 20f, 0f);
                            wantRot = CuffAimRot(ambushAim);
                            chase = 0.4f;
                            lerpV = 0.5f;
                            maxSpd = 40f;
                        }
                        else {
                            anchor = HoverPost(i);
                            wantRot = CuffAimRot(ambushAim);
                        }
                        break;
                    }
                    case StateDeath: {
                        if (cuffsOpened) {
                            //铐开坠地：真重力，落上地面各响一声
                            cuffVel[i].X *= 0.99f;
                            cuffVel[i].Y = MathF.Min(cuffVel[i].Y + 0.42f, 14f);
                            cuffPos[i] += cuffVel[i];
                            if (!cuffLanded[i]) {
                                float ground = FindGroundY(cuffPos[i] - new Vector2(0f, 8f));
                                if (cuffPos[i].Y >= ground - 8f) {
                                    cuffLanded[i] = true;
                                    cuffPos[i].Y = ground - 8f;
                                    cuffVel[i] = Vector2.Zero;
                                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.55f, Pitch = -0.55f + i * 0.15f, MaxInstances = 2 }, cuffPos[i]);
                                    if (!Main.dedServ) {
                                        BreachDust(cuffPos[i] + new Vector2(0f, 8f), 4);
                                    }
                                }
                            }
                            else {
                                cuffVel[i] = Vector2.Zero;
                            }
                            cuffRot[i] = cuffRot[i].AngleLerp(MathHelper.PiOver2 * CuffDir(i), 0.1f);
                            continue;
                        }
                        //火熄前松劲下垂
                        anchor = cuffPos[i] + new Vector2(0f, 1.8f);
                        wantRot = 0f;
                        chase = 0.06f;
                        lerpV = 0.12f;
                        break;
                    }
                    default: {
                        //跟随：呼吸浮动 + 间歇铐链空响挑衅
                        anchor = HoverPost(i);
                        wantRot = targetPlayer != null && !TargetInvalid()
                            ? CuffAimRot((aimPos - cuffPos[i]).SafeNormalize(Vector2.UnitY))
                            : MathHelper.Pi;
                        int cyc = (int)StateTimer % 120;
                        if (cyc >= 70 && cyc < 96 && i == (int)StateTimer / 120 % 2) {
                            //抬铐虚点猎物，链梢勾两下
                            anchor = body + new Vector2(side * 88f, -18f);
                            wantRot += MathF.Sin((cyc - 70) * 0.5f) * 0.18f;
                            if (cyc == 70) {
                                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.2f, Pitch = -0.6f, MaxInstances = 2 }, cuffPos[i]);
                            }
                        }
                        break;
                    }
                }

                if (!kinematic) {
                    Vector2 want = (anchor - cuffPos[i]) * chase;
                    if (want.Length() > maxSpd) {
                        want = want.SafeNormalize(Vector2.Zero) * maxSpd;
                    }
                    cuffVel[i] = Vector2.Lerp(cuffVel[i], want, lerpV);
                    cuffPos[i] += cuffVel[i];
                    cuffRot[i] = cuffRot[i].AngleLerp(wantRot, State == StateFlail ? 0.5f : 0.22f);
                }
            }
        }

        //==================== 公共小件 ====================

        /// <summary>确保双铐判定弹幕在场（服务端一次性生成，各端本地贴铐位）</summary>
        private void EnsureCuffHitboxes() {
            if (VaultUtils.isClient) {
                return;
            }
            int type = ModContent.ProjectileType<GaolCuffHitbox>();
            for (int cuff = 0; cuff < 2; cuff++) {
                bool exists = false;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == type && (int)p.ai[0] == NPC.whoAmI && (int)p.ai[1] == cuff) {
                        exists = true;
                        break;
                    }
                }
                if (!exists) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), cuffPos[cuff], Vector2.Zero,
                        type, ScaleDamage(SwipeDamage), 3f, Main.myPlayer, NPC.whoAmI, cuff);
                }
            }
        }

        /// <summary>狱火余烬（客户端表现）</summary>
        internal void SpawnFireWisp(Vector2 pos, Vector2 vel, float scale) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_GaolFireWisp>(pos, vel,
                Main.rand.NextBool(3) ? GaolPinkDeep : FireColor, scale * Main.rand.NextFloat(0.85f, 1.15f))
                ?.Configure(Main.rand.Next(16, 30));
        }

        /// <summary>铐位轨迹推进（链风条带的路径源）</summary>
        private void PushCuffTrails() {
            if (!cuffTrailInit) {
                cuffTrailInit = true;
                for (int i = 0; i < 2; i++) {
                    for (int k = 0; k < CuffTrailLen; k++) {
                        cuffTrail[i, k] = cuffPos[i];
                    }
                }
            }
            cuffTrailHead = (cuffTrailHead + 1) % CuffTrailLen;
            for (int i = 0; i < 2; i++) {
                cuffTrail[i, cuffTrailHead] = cuffPos[i];
            }
        }

        /// <summary>把某铐的轨迹按新→旧展开成点列（头在前）</summary>
        internal int CopyCuffTrail(int cuff, Vector2[] into) {
            int n = Math.Min(into.Length, CuffTrailLen);
            for (int k = 0; k < n; k++) {
                into[k] = cuffTrail[cuff, ((cuffTrailHead - k) % CuffTrailLen + CuffTrailLen) % CuffTrailLen];
            }
            return n;
        }

        /// <summary>向屏幕层申报本帧狱压（各端本地，按状态与距离衰减）</summary>
        private void RequestScreenPresence() {
            if (Main.dedServ || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                return;
            }
            float baseLevel = State switch {
                StateEmerge => 0.45f,
                StateRoar => 0.95f,
                StateDeath => 0.75f,
                StateDespawn => 0.2f,
                StateAmbush => 0.85f,
                _ => Phase2 ? 0.62f : 0.45f,
            };
            float dist = Vector2.Distance(Main.LocalPlayer.Center, NPC.Center);
            float atten = MathHelper.Clamp(1f - dist / 2400f, 0f, 1f);
            GaolWraithScreenFX.RequestDomain(baseLevel * atten);
        }

        /// <summary>拖影缓冲推进</summary>
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

        private void UpdateBodyFrame() {
            if (++bodyFrameTick >= 5) {
                bodyFrameTick = 0;
                bodyFrameIndex++;
            }
        }

        /// <summary>常态底噪：怨魂缘滴上升、链节偶响，牢狱的呼吸</summary>
        private void UpdateAmbientWisp() {
            if (Main.dedServ || BodyAlpha() < 0.5f) {
                return;
            }
            if (Main.rand.NextBool(16)) {
                Vector2 pos = NPC.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(16f, 46f));
                PRTLoader.NewParticle<PRT_GhostRainMist>(pos,
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                    MistTint * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(26, 44));
            }
        }

        /// <summary>只震看得见的人：本地玩家超出范围不吃震屏</summary>
        private void ShakeNearby(float amount, float range = 1200f) {
            if (Main.dedServ || Main.LocalPlayer == null) {
                return;
            }
            if (Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) > range) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(amount);
        }

        //==================== 表现参数 ====================

        /// <summary>躯体透明度：出场渐显、隐袭消隐、死亡与撤离渐散</summary>
        private float BodyAlpha() {
            int t = (int)StateTimer;
            switch (State) {
                case StateEmerge:
                    if (IsSkullEmerge) {
                        //变体：怨雾凝躯，自枯颅周身长出
                        return MathHelper.Clamp((t - 4) / 24f, 0f, 1f);
                    }
                    return t < BodyRiseFrame ? 0f : MathHelper.Clamp((t - BodyRiseFrame) / 10f, 0f, 1f);
                case StateAmbush: {
                    int phase = AmbushPhase;
                    int veil = AmbushRound == 0 ? AmbushVeilFrames : AmbushReveilFrames;
                    return phase switch {
                        0 => 1f - MathHelper.Clamp(t / (float)veil, 0f, 0.95f),
                        1 => 0.05f,
                        2 => MathHelper.Clamp(t / (float)AmbushFormFrames, 0.05f, 1f),
                        _ => 1f,
                    };
                }
                case StateDeath:
                    return MathHelper.Clamp((DeathTotal - t) / 40f, 0f, 1f);
                case StateDespawn:
                    return MathHelper.Clamp(1f - t / 70f, 0f, 1f);
                default:
                    return 1f;
            }
        }

        /// <summary>死亡散躯进度：源矩形自下而上收</summary>
        private float DeathDissolveT()
            => State == StateDeath
                ? MathF.Pow(MathHelper.Clamp((StateTimer - DeathCuffOpenAt) / (float)(DeathPopAt - DeathCuffOpenAt), 0f, 1f), 1.2f)
                : 0f;

        /// <summary>狱火亮度：蓄力与威压期抬升，死亡期忽明忽暗地熄</summary>
        internal float HeartFireLevel() {
            int t = (int)StateTimer;
            float baseLevel = 0.55f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + Seed);
            switch (State) {
                case StateEmerge:
                    if (IsSkullEmerge) {
                        //变体：眼窝火不熄，自枯颅余烬渐旺成胸腔狱火
                        return baseLevel * MathHelper.Clamp(0.3f + t / 30f, 0f, 1f);
                    }
                    return t < AwakenFrame ? 0f : baseLevel * MathHelper.Clamp((t - AwakenFrame) / 8f, 0f, 1f);
                case StateVolley: {
                    int phase = (int)StateParam;
                    if (phase == 1) {
                        return baseLevel + 0.45f * MathHelper.Clamp(t / (float)VolleyChargeFrames, 0f, 1f);
                    }
                    return phase == 2 ? baseLevel + 0.3f : baseLevel;
                }
                case StateCage:
                    return t <= CagePoseFrames ? baseLevel + 0.4f * (t / (float)CagePoseFrames) : baseLevel + 0.2f;
                case StateRoar:
                    return baseLevel + 0.5f;
                case StateAmbush:
                    return AmbushPhase == 1 ? 0.9f : baseLevel * BodyAlpha();
                case StateDeath: {
                    if (t >= DeathPopAt) {
                        return 0f;
                    }
                    //忽明忽暗地熄：哈希闪烁叠衰减
                    float decay = 1f - t / (float)DeathPopAt;
                    float flick = 0.6f + 0.4f * MathF.Sin(t * 0.9f + Hash01((int)(t * 0.23f)) * 9f);
                    return baseLevel * decay * flick + 0.08f;
                }
                default:
                    return baseLevel;
            }
        }

        /// <summary>确定性 0~1 散列，链节缺珠与死亡闪烁用</summary>
        private static float Hash01(int n) {
            float v = MathF.Sin(n * 127.1f) * 43758.5453f;
            return v - MathF.Floor(v);
        }

        //==================== 绘制：拖影 → 主链 → 腕链 → 铐 → 躯体 → 加色层 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!cuffsInit) {
                return false;
            }
            Main.instance.LoadNPC(NPCID.Wraith);
            Main.instance.LoadItem(ItemID.Shackle);
            Texture2D bodyTex = TextureAssets.Npc[NPCID.Wraith]?.Value;
            Texture2D cuffTex = TextureAssets.Item[ItemID.Shackle]?.Value;
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            if (bodyTex == null || cuffTex == null || chainTex == null) {
                return false;
            }

            int frameCount = Math.Max(1, Main.npcFrameCount[NPCID.Wraith]);
            int frameH = bodyTex.Height / frameCount;
            Rectangle bodyFrame = new(0, frameH * (bodyFrameIndex % frameCount), bodyTex.Width, frameH);

            DrawDashGhosts(spriteBatch, bodyTex, bodyFrame);
            DrawCuffWindRibbons();
            for (int i = 0; i < 2; i++) {
                DrawArmChain(spriteBatch, chainTex, i, drawColor);
                DrawWristChain(spriteBatch, chainTex, i, drawColor);
            }
            DrawCuffSmears(spriteBatch, cuffTex);
            DrawCuffs(spriteBatch, cuffTex, drawColor);
            if (IsSkullEmerge) {
                DrawSkullRelic(spriteBatch, drawColor);
            }
            DrawBody(spriteBatch, bodyTex, bodyFrame, drawColor);
            DrawGlowLayer(spriteBatch);
            return false;
        }

        /// <summary>变体入场：枯颅随躯体升起、被凝形的怨躯吸收（与蛰伏枯颅同贴图同刻度，
        /// 换体帧视觉不换皮，无缝的关键一笔）</summary>
        private void DrawSkullRelic(SpriteBatch sb, Color drawColor) {
            int t = (int)StateTimer;
            float fade = 1f - MathHelper.Clamp((t - 8) / 22f, 0f, 1f);
            if (fade < 0.03f) {
                return;
            }
            Main.instance.LoadItem(ItemID.Skull);
            Texture2D skullTex = TextureAssets.Item[ItemID.Skull]?.Value;
            if (skullTex == null) {
                return;
            }
            Vector2 origin = skullTex.Size() * 0.5f;
            float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + Seed) * 3f;
            Vector2 pos = NPC.Center + new Vector2(0f, bob) - Main.screenPosition;
            const float scale = 1.7f;
            sb.Draw(skullTex, pos, null, IronDeep * (0.8f * fade), 0f, origin, scale * 1.1f, SpriteEffects.None, 0f);
            sb.Draw(skullTex, pos, null, Color.Lerp(drawColor, new Color(198, 204, 202), 0.45f) * fade,
                0f, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>突刺/链旋拖影：速度门控，免得常开成噪声</summary>
        private void DrawDashGhosts(SpriteBatch sb, Texture2D bodyTex, Rectangle frame) {
            bool flailing = State == StateFlail && (int)StateParam == 2;
            if (!trailInit || BodyAlpha() < 0.2f || (NPC.velocity.Length() < 15f && !flailing)) {
                return;
            }
            Vector2 origin = frame.Size() * 0.5f;
            SpriteEffects fx = FacingSign > 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            for (int j = 1; j <= 4; j++) {
                int idx = ((trailHead - j * 2) % TrailLen + TrailLen) % TrailLen;
                float fade = (1f - j / 5f) * 0.3f * BodyAlpha();
                sb.Draw(bodyTex, trailPos[idx] - Main.screenPosition, frame,
                    (GaolPink with { A = 0 }) * fade, trailRot[idx], origin,
                    BodyDrawScale * (1f - j * 0.04f), fx, 0f);
            }
        }

        /// <summary>躯体到铐的主链：Chain22 沿悬链弧逐节铺，松则垂、绷则直，战栗期高频颤</summary>
        private void DrawArmChain(SpriteBatch sb, Texture2D chainTex, int i, Color lightColor) {
            float alpha = CuffAlpha(i);
            if (alpha < 0.03f) {
                return;
            }
            Vector2 shoulder = NPC.Center + NPC.velocity
                + new Vector2(CuffDir(i) * 16f, -6f).RotatedBy(NPC.rotation) * BodyDrawScale;
            Vector2 hand = cuffPos[i];
            float dist = Vector2.Distance(shoulder, hand);
            //松弛垂度：链越松垂越深，甩直时自然归零
            float slack = MathHelper.Clamp(150f - dist, 0f, 120f) * 0.5f;
            Vector2 mid = (shoulder + hand) * 0.5f + new Vector2(0f, 8f + slack);
            if (chainShiver > 0) {
                Vector2 perp = (hand - shoulder).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                mid += perp * MathF.Sin(Main.GlobalTimeWrappedHourly * 60f + Seed + i * 2f) * (chainShiver / 34f) * 6f;
            }

            //死亡铐开后主链随躯体一起散雾，不再画向坠地的铐
            if (State == StateDeath && cuffsOpened) {
                return;
            }

            Color tint = lightColor.MultiplyRGB(IronMul) * (alpha * 0.95f);
            Vector2 origin = chainTex.Size() * 0.5f;
            float linkStep = MathF.Max(10f, chainTex.Height - 2f);
            //贝塞尔弧上按链节步进
            Vector2 prev = shoulder;
            int links = (int)MathF.Ceiling((dist + slack * 1.6f) / linkStep) + 1;
            links = Math.Min(links, 26);
            for (int k = 1; k <= links; k++) {
                float tt = k / (float)links;
                Vector2 p = Bezier(shoulder, mid, hand, tt);
                Vector2 dir = p - prev;
                if (dir.Length() < 2f) {
                    continue;
                }
                float rot = dir.ToRotation() + MathHelper.PiOver2;
                sb.Draw(chainTex, (prev + p) * 0.5f - Main.screenPosition, null, tint, rot,
                    origin, 1f, SpriteEffects.None, 0f);
                prev = p;
            }
        }

        /// <summary>铐口断链：三四节残链垂着晃，高速时甩直在身后（被扯断的过去）</summary>
        private void DrawWristChain(SpriteBatch sb, Texture2D chainTex, int i, Color lightColor) {
            float alpha = CuffAlpha(i);
            if (alpha < 0.03f) {
                return;
            }
            Vector2 origin = chainTex.Size() * 0.5f;
            Color tint = lightColor.MultiplyRGB(IronMul) * (alpha * 0.8f);
            bool fast = cuffVel[i].Length() > 7f;
            Vector2 fastDir = -cuffVel[i].SafeNormalize(Vector2.UnitY);
            //断链根挂在铐口反侧
            Vector2 mouth = (cuffRot[i] - MathHelper.PiOver2).ToRotationVector2();
            Vector2 root = cuffPos[i] - mouth * 12f;
            int flickerBeat = (int)(Main.GlobalTimeWrappedHourly * 2f);

            Vector2 prev = root;
            for (int k = 0; k < 4; k++) {
                //偶发缺节：断口本来就不齐
                if (Hash01(NPC.whoAmI * 31 + i * 97 + k * 7 + flickerBeat) < 0.16f && k == 3) {
                    break;
                }
                Vector2 p = fast
                    ? root + fastDir * ((k + 1) * 12f)
                    : root + new Vector2(
                        MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + k * 1.4f + Seed + i * 2f) * (3f + k * 1.4f),
                        (k + 1) * 12f);
                Vector2 dir = p - prev;
                if (dir.Length() >= 2f) {
                    sb.Draw(chainTex, (prev + p) * 0.5f - Main.screenPosition, null,
                        tint * (1f - k * 0.16f), dir.ToRotation() + MathHelper.PiOver2,
                        origin, 0.9f, SpriteEffects.None, 0f);
                }
                prev = p;
            }
        }

        /// <summary>链风条带：铐高速运动时沿轨迹拉出的灵质风带（速度门控，
        /// 挥击/链旋/突刺自动生效；金属拖影仍由 DrawCuffSmears 的残铐承担旋转涂抹）</summary>
        private void DrawCuffWindRibbons() {
            Vector2[] pts = GaolWraithScreenFX.SharedRibbonBuffer;
            for (int i = 0; i < 2; i++) {
                float speed = cuffVel[i].Length();
                float alpha = CuffAlpha(i);
                if (speed < 10f || alpha < 0.1f || !cuffTrailInit) {
                    continue;
                }
                int n = CopyCuffTrail(i, pts);
                float fade = alpha * MathHelper.Clamp((speed - 10f) / 14f, 0f, 1f) * 0.85f;
                GaolWraithDraw.DrawRibbon(pts, n, 30f, fade,
                    hot: 0.25f, decay: 0f, seed: Seed + i * 1.7f, ironWind: true);
            }
        }

        /// <summary>挥击拖影：按铐速在身后铺残铐，速度门控</summary>
        private void DrawCuffSmears(SpriteBatch sb, Texture2D cuffTex) {
            Vector2 origin = cuffTex.Size() * 0.5f;
            for (int i = 0; i < 2; i++) {
                float speed = cuffVel[i].Length();
                float alpha = CuffAlpha(i);
                if (speed < 11f || alpha < 0.1f) {
                    continue;
                }
                SpriteEffects fx = i == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                for (int k = 1; k <= 3; k++) {
                    sb.Draw(cuffTex, cuffPos[i] - cuffVel[i] * (k * 0.55f) - Main.screenPosition, null,
                        (GaolPink with { A = 0 }) * (0.28f * alpha * (1f - k * 0.28f)), cuffRot[i],
                        origin, CuffDrawScale * (1f - k * 0.05f), fx, 0f);
                }
            }
        }

        private void DrawCuffs(SpriteBatch sb, Texture2D cuffTex, Color lightColor) {
            Vector2 origin = cuffTex.Size() * 0.5f;
            for (int i = 0; i < 2; i++) {
                float alpha = CuffAlpha(i);
                if (alpha < 0.03f) {
                    continue;
                }
                SpriteEffects fx = i == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                float scale = CuffDrawScale;
                if (clenchTimer > 0) {
                    scale *= 1f + 0.12f * MathF.Sin(clenchTimer / 14f * MathHelper.Pi);
                }
                Vector2 pos = cuffPos[i] - Main.screenPosition;
                //暗缘压边给体积
                sb.Draw(cuffTex, pos, null, IronDeep * (0.75f * alpha), cuffRot[i], origin, scale * 1.12f, fx, 0f);
                sb.Draw(cuffTex, pos, null, lightColor.MultiplyRGB(IronMul) * alpha, cuffRot[i], origin, scale, fx, 0f);
            }
        }

        /// <summary>隐袭雾化进度（喂 Ecto shader 的 uVeil，几何碎解代替纯透明度）</summary>
        private float AmbushVeilT() {
            if (State != StateAmbush) {
                return 0f;
            }
            int t = (int)StateTimer;
            int veil = AmbushRound == 0 ? AmbushVeilFrames : AmbushReveilFrames;
            return AmbushPhase switch {
                0 => MathHelper.Clamp(t / (float)veil, 0f, 0.97f),
                1 => 0.97f,
                2 => MathHelper.Clamp(0.97f - t / (float)AmbushFormFrames * 0.97f, 0f, 0.97f),
                _ => 0f,
            };
        }

        /// <summary>躯体：GaolWraithEcto 灵质材质重绘（体内灵流+下摆撕散+缘光+狱火透光；
        /// 出场地线渗出/死亡蚀散/隐袭雾化全走噪声前沿，无一处平切）。缺 shader 走 CPU 回退</summary>
        private void DrawBody(SpriteBatch sb, Texture2D bodyTex, Rectangle frame, Color lightColor) {
            Effect ecto = EffectLoader.GaolWraithEcto?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            float veilT = AmbushVeilT();
            //隐袭期几何碎解由 shader 承担，顶点透明度只留少量呼吸
            float alpha = State == StateAmbush && ecto != null ? 0.92f : BodyAlpha();
            if (alpha < 0.02f) {
                return;
            }
            if (ecto == null || noise == null) {
                DrawBodyFallback(sb, bodyTex, frame, lightColor, BodyAlpha());
                return;
            }

            //帧上下各内缩 1px：双通道防帧表渗色（shader 侧另有半像素钳制）
            Rectangle src = frame;
            src.Y += 1;
            src.Height = Math.Max(2, src.Height - 2);
            float texW = bodyTex.Width;
            float texH = bodyTex.Height;
            Vector2 drawSize = new(src.Width * BodyDrawScale, src.Height * BodyDrawScale);
            Vector2 topLeft = NPC.Center - drawSize * 0.5f;

            //出场地线折算到帧内 v（未出场/变体地线远置时 >1.5 自动关断）
            float groundV = 2f;
            if (State == StateEmerge && emergeGroundLatched) {
                groundV = MathHelper.Clamp((emergeGroundY - topLeft.Y) / drawSize.Y, 0f, 2f);
            }
            Vector2 heartUv = (HeartPos() - topLeft) / drawSize;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //共享 uniform 全参数重设 + 噪声显式绑 s1（Draw 会覆写 s0）
            ecto.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            ecto.Parameters["uSeed"]?.SetValue(Seed % 1f);
            ecto.Parameters["uUvRect"]?.SetValue(new Vector4(src.X / texW, src.Y / texH, src.Width / texW, src.Height / texH));
            ecto.Parameters["uTexel"]?.SetValue(new Vector2(1f / texW, 1f / texH));
            ecto.Parameters["uAspect"]?.SetValue(src.Width / (float)src.Height);
            ecto.Parameters["uFlipH"]?.SetValue(FacingSign > 0f ? 1f : 0f);
            ecto.Parameters["uGroundV"]?.SetValue(groundV);
            ecto.Parameters["uDissolve"]?.SetValue(DeathDissolveT());
            ecto.Parameters["uVeil"]?.SetValue(veilT);
            ecto.Parameters["uFireLevel"]?.SetValue(HeartFireLevel());
            ecto.Parameters["uHeartUv"]?.SetValue(heartUv);
            ecto.Parameters["uFireColor"]?.SetValue(FireColor.ToVector3());
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            ecto.CurrentTechnique.Passes[0].Apply();

            Color tint = Color.Lerp(lightColor, Color.White, 0.55f);
            tint.A = (byte)(alpha * 255f);
            sb.Draw(bodyTex, NPC.Center - Main.screenPosition, src, tint, NPC.rotation,
                new Vector2(src.Width * 0.5f, src.Height * 0.5f), BodyDrawScale, SpriteEffects.None, 0f);

            sb.End();
            Main.graphics.GraphicsDevice.Textures[1] = null;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>CPU 回退：暗缘 + 灵质主体三层重染，出场/死亡用源矩形裁切</summary>
        private void DrawBodyFallback(SpriteBatch sb, Texture2D bodyTex, Rectangle frame, Color lightColor, float alpha) {
            if (alpha < 0.02f) {
                return;
            }
            SpriteEffects fx = FacingSign > 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Rectangle src = frame;
            float drawH = frame.Height * BodyDrawScale;
            float topY = NPC.Center.Y - drawH * 0.5f;

            //出场期：只画地线以上的部分（破土的躯体被地面咬住）
            if (State == StateEmerge && emergeGroundLatched) {
                float visible = MathHelper.Clamp((emergeGroundY - topY) / drawH, 0f, 1f);
                src.Height = Math.Max(2, (int)(frame.Height * visible));
            }
            //死亡期：自下而上散去
            float dissolve = DeathDissolveT();
            if (dissolve > 0f) {
                src.Height = Math.Max(2, (int)(frame.Height * (1f - dissolve * 0.85f)));
            }

            Vector2 pos = new Vector2(NPC.Center.X, topY) - Main.screenPosition;
            Vector2 origin = new(frame.Width * 0.5f, 0f);
            Color deep = Color.Lerp(lightColor, EctoDeep, 0.7f) * (alpha * 0.8f);
            Color bodyCol = Color.Lerp(lightColor, EctoBody, 0.55f) * alpha;
            sb.Draw(bodyTex, pos, src, deep, NPC.rotation, origin, BodyDrawScale * 1.07f, fx, 0f);
            sb.Draw(bodyTex, pos, src, bodyCol, NPC.rotation, origin, BodyDrawScale, fx, 0f);
            //灵质内芯淡淡透光（AlphaBlend 批内 A=0 加色技法）
            sb.Draw(bodyTex, pos, src, (EctoBody with { A = 0 }) * (0.22f * alpha), NPC.rotation, origin,
                BodyDrawScale * 0.94f, fx, 0f);

            //裁切线亮边：出场与死亡的凝散边界
            if (src.Height < frame.Height - 2) {
                Texture2D glowTex = CWRAsset.SoftGlow?.Value;
                if (glowTex != null) {
                    float cutY = topY + src.Height * BodyDrawScale;
                    sb.Draw(glowTex, new Vector2(NPC.Center.X, cutY) - Main.screenPosition, null,
                        (FireColor with { A = 0 }) * (0.5f * alpha), 0f, glowTex.Size() * 0.5f,
                        new Vector2(frame.Width * BodyDrawScale * 1.2f / glowTex.Width, 10f / glowTex.Height),
                        SpriteEffects.None, 0f);
                }
            }
        }

        private float CuffAlpha(int i) {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge when IsSkullEmerge => t < VariantBurstFrame
                    ? 0f : MathHelper.Clamp((t - VariantBurstFrame) / 4f, 0f, 1f),
                StateEmerge => t < CuffsBreachFrame ? 0f : MathHelper.Clamp((t - CuffsBreachFrame) / 4f, 0f, 1f),
                StateDeath => MathHelper.Clamp((DeathTotal - t) / 20f, 0f, 1f),
                StateDespawn => BodyAlpha(),
                //隐袭消隐期铐随躯体一起没入雾里
                StateAmbush => MathF.Max(BodyAlpha(), 0.05f),
                _ => 1f,
            };
        }

        /// <summary>加色装饰：预兆地光、胸腔狱火、眼点、蓄力汇聚流线、隐袭预告光</summary>
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

            int t = (int)StateTimer;
            //Additive 批内强度必须写进 A：Color*float 同乘四通道，禁 A=0 染色

            //出场预兆：地表两点粉光收拢 + 中央深光憋压（骷髅头变体没有破土预兆，跳过）
            if (State == StateEmerge && !IsSkullEmerge && t < BodyRiseFrame) {
                float ot = MathHelper.Clamp(t / (float)BodyRiseFrame, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                for (int side = -1; side <= 1; side += 2) {
                    if (t < CuffsBreachFrame) {
                        Vector2 pos = new(NPC.Center.X + side * 56f, emergeGroundY - 2f);
                        float r = 16f + 12f * ease;
                        sb.Draw(glow, pos - Main.screenPosition, null, GaolPink * (0.4f * ease), 0f,
                            gOrigin, new Vector2(r * 2.2f / glow.Width, r * 0.9f / glow.Height), SpriteEffects.None, 0f);
                    }
                }
                Vector2 centerPos = new(NPC.Center.X, emergeGroundY - 4f);
                sb.Draw(glow, centerPos - Main.screenPosition, null, GaolPinkDeep * (0.35f * ease), 0f,
                    gOrigin, new Vector2(70f * 2f / glow.Width, 22f / glow.Height), SpriteEffects.None, 0f);
            }

            //胸腔狱火 + 眼点
            float fire = HeartFireLevel();
            float bodyAlpha = BodyAlpha();
            if (fire > 0.04f && (bodyAlpha > 0.1f || State == StateAmbush)) {
                EnsureBegin();
                Vector2 heart = HeartPos();
                float rr = 12f + 16f * fire;
                sb.Draw(glow, heart - Main.screenPosition, null, FireColor * (0.55f * fire), 0f,
                    gOrigin, new Vector2(rr * 2f / glow.Width), SpriteEffects.None, 0f);
                sb.Draw(glow, heart - Main.screenPosition, null, GaolWhiteHot * (0.3f * fire), 0f,
                    gOrigin, new Vector2(rr * 0.9f / glow.Width), SpriteEffects.None, 0f);
                //兜帽下双眼
                if (bodyAlpha > 0.3f) {
                    for (int side = -1; side <= 1; side += 2) {
                        Vector2 eye = NPC.Center + new Vector2(FacingSign * 8f + side * 7f, -26f).RotatedBy(NPC.rotation) * BodyDrawScale;
                        sb.Draw(glow, eye - Main.screenPosition, null, GaolPink * (0.5f * fire * bodyAlpha), 0f,
                            gOrigin, new Vector2(7f * 2f / glow.Width), SpriteEffects.None, 0f);
                    }
                }
            }

            //蓄力汇聚流线（狱火/囚笼共用；确定性相位，各端一致；72% 后静默）
            float charge = 0f;
            Vector2 chargeAt = LanternPos();
            if (State == StateVolley && (int)StateParam == 1) {
                charge = MathHelper.Clamp(StateTimer / VolleyChargeFrames, 0f, 1f);
            }
            else if (State == StateCage && t <= CagePoseFrames) {
                charge = MathHelper.Clamp(t / (float)CagePoseFrames, 0f, 1f);
                chargeAt = NPC.Center + new Vector2(0f, -60f);
            }
            if (charge > 0.03f && charge < 0.72f) {
                EnsureBegin();
                const int streaks = 6;
                for (int i = 0; i < streaks; i++) {
                    float phase = (Main.GlobalTimeWrappedHourly * 0.9f + i / (float)streaks + Seed * 0.13f) % 1f;
                    float ang = Seed + i * MathHelper.TwoPi / streaks + MathF.Sin(Seed * 3f + i) * 0.7f;
                    float dist = MathHelper.Lerp(84f, 14f, phase);
                    Vector2 pos = chargeAt + ang.ToRotationVector2() * dist;
                    float a = charge * 0.4f * MathF.Sin(phase * MathHelper.Pi);
                    sb.Draw(glow, pos - Main.screenPosition, null, FireColor * a, ang,
                        gOrigin, new Vector2(26f * 2.2f / glow.Width, 6f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //铐击蓄力：出手铐口积光
            if (State == StateSwipe && SwipeIndex < SwipeCount) {
                int windup = SwipeWindups[Math.Min(SwipeIndex, SwipeWindups.Length - 1)];
                if (t <= windup && CuffAlpha(ActiveCuff) > 0.1f) {
                    float k = t / (float)windup;
                    EnsureBegin();
                    float r = 6f + 14f * k;
                    sb.Draw(glow, cuffPos[ActiveCuff] - Main.screenPosition, null, GaolPink * (0.5f * k), 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //隐袭预告：人未至光先到
            if (State == StateAmbush && AmbushPhase == 1) {
                EnsureBegin();
                float k = MathHelper.Clamp(t / (float)AmbushWarnFrames, 0f, 1f);
                float r = 20f + 34f * k;
                sb.Draw(glow, NPC.Center - Main.screenPosition, null, FireColor * (0.55f * k), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
            => Vector2.Lerp(Vector2.Lerp(a, c, t), Vector2.Lerp(c, b, t), t);

        //==================== 击杀通报 ====================

        /// <summary>服务器钩子：通报禁室看守本次进入熄灯（野外测试召唤找不到房间则无事发生），
        /// 并经共用记录表逐人结算印信饰品（首杀必掉/复杀 25%）</summary>
        public override void OnKill() {
            GaolBossRoomWatcher.NotifyWraithDefeated(NPC.Center);
            DungeonworldBossRecords.ServerSettleKill(DungeonworldBossRecords.BossIdWraith, NPC, NPC.Center, ModContent.ItemType<RustedGaolIrons>());
        }

        //==================== 受击 ====================

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            //受击：灵质雾瓣 + 偶发铁屑
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center + Main.rand.NextVector2Circular(20f, 26f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(0.5f, 1.4f), -Main.rand.NextFloat(0.3f, 0.8f)),
                    MistTint * 0.7f, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(20, 34));
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(16f, 16f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(1.5f, 3f), -Main.rand.NextFloat(0.5f, 2f)),
                    Color.Lerp(GaolPink, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(8, 14));
            }

            if (NPC.life <= 0 && deathDone) {
                //真死谢幕残雾（大拍已在死亡演出里放完）
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center + Main.rand.NextVector2Circular(26f, 30f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.2f)),
                        MistTint * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))?.Configure(Main.rand.Next(50, 80));
                }
            }
        }
    }

    /// <summary>
    /// 怨灵条带绘制辅助：GaolWraithFire TechTrail 的统一入口。
    /// 世界坐标顶点直喂 GetTransfromMatrix（矩阵内已含屏移，勿再减 screenPosition），
    /// 噪声显式绑 s1，uniform 全参数重设；缺 shader 静默跳过（金属拖影仍由残铐 sprite 承担）
    /// </summary>
    internal static class GaolWraithDraw
    {
        private const int MaxPts = 24;
        private static readonly VertexPositionColorTexture[] verts = new VertexPositionColorTexture[MaxPts * 2];
        private static readonly Vector2[] compact = new Vector2[MaxPts];

        /// <summary>
        /// 画一条链风/狱火条带（pts[0]=头，向尾展开；世界坐标）。
        /// ironWind=true 走铁风色板（挥击/链旋），false 走狱火色板（弹尾/余韵）
        /// </summary>
        public static void DrawRibbon(Vector2[] pts, int count, float widthPx, float fade,
            float hot, float decay, float seed, bool ironWind) {
            if (fade <= 0.02f || count < 3) {
                return;
            }
            Effect effect = EffectLoader.GaolWraithFire?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            //压缩重复点：点距过近的顶点会让切向退化打结
            int n = 0;
            for (int i = 0; i < count && n < MaxPts; i++) {
                if (n == 0 || Vector2.DistanceSquared(compact[n - 1], pts[i]) > 9f) {
                    compact[n++] = pts[i];
                }
            }
            if (n < 3) {
                return;
            }

            for (int i = 0; i < n; i++) {
                float t = i / (float)(n - 1);
                Vector2 dir = (i == n - 1 ? compact[i] - compact[i - 1] : compact[i + 1] - compact[i]);
                dir = dir.SafeNormalize(Vector2.UnitX);
                Vector2 side = dir.RotatedBy(MathHelper.PiOver2) * widthPx * 0.5f;
                Color col = Color.White;
                verts[i * 2] = new VertexPositionColorTexture(new Vector3(compact[i].X + side.X, compact[i].Y + side.Y, 0f), col, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(compact[i].X - side.X, compact[i].Y - side.Y, 0f), col, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique = effect.Techniques["TechTrail"];
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(seed % 1f);
            effect.Parameters["uFade"]?.SetValue(MathHelper.Clamp(fade, 0f, 1f));
            effect.Parameters["uHot"]?.SetValue(hot);
            effect.Parameters["uDecay"]?.SetValue(MathHelper.Clamp(decay, 0f, 1f));
            if (ironWind) {
                effect.Parameters["uColDeep"]?.SetValue(DeepGaolWraith.IronDeep.ToVector3());
                effect.Parameters["uColBody"]?.SetValue(Color.Lerp(DeepGaolWraith.MistTint, DeepGaolWraith.GaolPink, 0.45f).ToVector3());
                effect.Parameters["uColCore"]?.SetValue(DeepGaolWraith.GaolWhiteHot.ToVector3());
            }
            else {
                effect.Parameters["uColDeep"]?.SetValue(DeepGaolWraith.GaolPinkDeep.ToVector3());
                effect.Parameters["uColBody"]?.SetValue(DeepGaolWraith.GaolPink.ToVector3());
                effect.Parameters["uColCore"]?.SetValue(DeepGaolWraith.GaolWhiteHot.ToVector3());
            }
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, (n - 1) * 2);
            }

            device.Textures[1] = null;
            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        private static readonly VertexPositionColorTexture[] bindVerts = new VertexPositionColorTexture[4];

        /// <summary>
        /// 画一段鬼链束缚场（GaolWraithChain TechBind 的单 quad 入口，世界坐标 A→B）。
        /// 铁链 sprite 在其上层承担结构；本层负责行波灵流/锚结收口/绷直白闪/锈解蚀散
        /// </summary>
        public static void DrawBindStrip(Vector2 a, Vector2 b, float widthPx, float alpha,
            float taut, float snap, float snapT, float decay, float seed) {
            if (alpha <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.GaolWraithChain?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }
            Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
            Vector2 side = dir.RotatedBy(MathHelper.PiOver2) * widthPx * 0.5f;
            Color col = Color.White * MathHelper.Clamp(alpha, 0f, 1f);
            bindVerts[0] = new VertexPositionColorTexture(new Vector3(a.X + side.X, a.Y + side.Y, 0f), col, new Vector2(0f, 0f));
            bindVerts[1] = new VertexPositionColorTexture(new Vector3(a.X - side.X, a.Y - side.Y, 0f), col, new Vector2(0f, 1f));
            bindVerts[2] = new VertexPositionColorTexture(new Vector3(b.X + side.X, b.Y + side.Y, 0f), col, new Vector2(1f, 0f));
            bindVerts[3] = new VertexPositionColorTexture(new Vector3(b.X - side.X, b.Y - side.Y, 0f), col, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique = effect.Techniques["TechBind"];
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(seed % 1f);
            effect.Parameters["uTaut"]?.SetValue(MathHelper.Clamp(taut, 0f, 1f));
            effect.Parameters["uSnap"]?.SetValue(MathHelper.Clamp(snap, 0f, 1f));
            effect.Parameters["uSnapT"]?.SetValue(snapT);
            effect.Parameters["uDecay"]?.SetValue(MathHelper.Clamp(decay, 0f, 1f));
            effect.Parameters["uColBody"]?.SetValue(DeepGaolWraith.MistTint.ToVector3());
            effect.Parameters["uColGlow"]?.SetValue(DeepGaolWraith.GaolPink.ToVector3());
            effect.Parameters["uColHot"]?.SetValue(DeepGaolWraith.GaolWhiteHot.ToVector3());
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bindVerts, 0, 2);
            }

            device.Textures[1] = null;
            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>
    /// 怨灵屏幕层通道（客户端，Push* 写入，渲染句柄逐帧收敛）：
    /// 狱压强度 / 冲击环 ×2 / 冷粉冲击帧（死亡一次）/ 隐袭雾拍 / 链风余辉条带。
    /// 结构承 SkeletronScreenEffects，材质换深牢冷粉
    /// </summary>
    internal static class GaolWraithScreenFX
    {
        internal const int MaxRings = 2;
        private const int MaxAfterglows = 8;
        internal const int AfterglowPts = 12;

        internal struct RingInstance
        {
            public Vector2 WorldCenter;
            public float Intensity;
            public float MaxRadiusPx;
            public int Age;
            public int Life;
            public bool Active;
        }

        internal struct AfterglowRibbon
        {
            public Vector2[] Pts;
            public int Count;
            public float Width;
            public float Seed;
            public float Hot;
            public bool IronWind;
            public int Age;
            public int Life;
            public bool Active;
        }

        internal static readonly RingInstance[] Rings = new RingInstance[MaxRings];
        internal static readonly AfterglowRibbon[] Afterglows = new AfterglowRibbon[MaxAfterglows];

        /// <summary>条带临时点列（每帧栈式使用，勿跨帧持有）</summary>
        internal static readonly Vector2[] SharedRibbonBuffer = new Vector2[DeepGaolWraith.CuffTrailLen];

        internal static float DomainIntensity { get; private set; }
        private static float domainTarget;

        internal static float MistLevel { get; private set; }

        internal static float FlashIntensity { get; private set; }
        internal static int FlashAge { get; private set; }
        internal static int FlashLife { get; private set; }
        internal static bool FlashActive => FlashAge < FlashLife && FlashIntensity > 0.01f;

        public static bool HasAny {
            get {
                if (DomainIntensity > 0.012f || FlashActive || MistLevel > 0.015f) {
                    return true;
                }
                for (int i = 0; i < MaxRings; i++) {
                    if (Rings[i].Active) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>本帧期望狱压（观察者取最大）</summary>
        public static void RequestDomain(float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            if (intensity > domainTarget) {
                domainTarget = MathHelper.Clamp(intensity, 0f, 1f);
            }
        }

        /// <summary>隐袭雾拍：置起然后自然衰减</summary>
        public static void PushMist(float level) {
            if (VaultUtils.isServer) {
                return;
            }
            MistLevel = MathF.Max(MistLevel, MathHelper.Clamp(level, 0f, 1f));
        }

        /// <summary>冲击环，超限顶替最老</summary>
        public static void PushRing(Vector2 worldCenter, float intensity, float maxRadiusPx, int lifeFrames) {
            if (VaultUtils.isServer) {
                return;
            }
            int slot = -1;
            int oldestAge = -1;
            for (int i = 0; i < MaxRings; i++) {
                if (!Rings[i].Active) {
                    slot = i;
                    break;
                }
                if (Rings[i].Age > oldestAge) {
                    oldestAge = Rings[i].Age;
                    slot = i;
                }
            }
            Rings[slot] = new RingInstance {
                WorldCenter = worldCenter,
                Intensity = MathHelper.Clamp(intensity, 0f, 1.2f),
                MaxRadiusPx = maxRadiusPx,
                Age = 0,
                Life = Math.Max(lifeFrames, 8),
                Active = true,
            };
        }

        /// <summary>冷粉冲击帧，死亡终爆一次</summary>
        public static void PushFlash(float intensity, int lifeFrames) {
            if (VaultUtils.isServer) {
                return;
            }
            FlashIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            FlashAge = 0;
            FlashLife = Math.Max(lifeFrames, 8);
        }

        /// <summary>把怨灵某铐的当前轨迹交给余辉（挥击冲击拍：风带活过挥击）</summary>
        public static void PushAfterglow(DeepGaolWraith boss, int cuff, float width, bool ironWind) {
            if (VaultUtils.isServer) {
                return;
            }
            int slot = FindAfterglowSlot();
            Afterglows[slot].Pts ??= new Vector2[AfterglowPts];
            int n = boss.CopyCuffTrail(cuff, Afterglows[slot].Pts);
            SetAfterglow(slot, n, width, ironWind, 0.35f, 16);
        }

        /// <summary>把一段现成路径交给余辉（狱火弹谢幕：拖尾不随弹亡消失）</summary>
        public static void PushAfterglowPath(Vector2[] src, int count, float width, bool ironWind, float hot, int life) {
            if (VaultUtils.isServer) {
                return;
            }
            int slot = FindAfterglowSlot();
            Afterglows[slot].Pts ??= new Vector2[AfterglowPts];
            int n = Math.Min(count, AfterglowPts);
            for (int i = 0; i < n; i++) {
                Afterglows[slot].Pts[i] = src[i];
            }
            SetAfterglow(slot, n, width, ironWind, hot, life);
        }

        private static int FindAfterglowSlot() {
            int slot = 0;
            int oldestAge = -1;
            for (int i = 0; i < MaxAfterglows; i++) {
                if (!Afterglows[i].Active) {
                    return i;
                }
                if (Afterglows[i].Age > oldestAge) {
                    oldestAge = Afterglows[i].Age;
                    slot = i;
                }
            }
            return slot;
        }

        private static void SetAfterglow(int slot, int count, float width, bool ironWind, float hot, int life) {
            Afterglows[slot].Count = count;
            Afterglows[slot].Width = width;
            Afterglows[slot].Seed = slot * 0.31f + 0.07f;
            Afterglows[slot].Hot = hot;
            Afterglows[slot].IronWind = ironWind;
            Afterglows[slot].Age = 0;
            Afterglows[slot].Life = Math.Max(life, 8);
            Afterglows[slot].Active = count >= 3;
        }

        /// <summary>每帧收敛（渲染句柄驱动，仅客户端）</summary>
        public static void Update() {
            DomainIntensity = MathHelper.Lerp(DomainIntensity, domainTarget, domainTarget > DomainIntensity ? 0.08f : 0.05f);
            if (DomainIntensity < 0.012f && domainTarget <= 0f) {
                DomainIntensity = 0f;
            }
            domainTarget = 0f;

            MistLevel = MathF.Max(MistLevel - 0.022f, 0f);

            for (int i = 0; i < MaxRings; i++) {
                if (Rings[i].Active && ++Rings[i].Age >= Rings[i].Life) {
                    Rings[i].Active = false;
                }
            }
            for (int i = 0; i < MaxAfterglows; i++) {
                if (Afterglows[i].Active && ++Afterglows[i].Age >= Afterglows[i].Life) {
                    Afterglows[i].Active = false;
                }
            }

            if (FlashAge < FlashLife) {
                FlashAge++;
            }
        }
    }

    /// <summary>
    /// 怨灵渲染句柄：实体层画链风余辉条带（EndEntityDraw，无活动批），
    /// 拷屏层做狱压/冲击环/冲击帧全屏后效（EndCaptureDraw，镜像 SkeletronScreenRender）。
    /// 随门禁一起进出游戏
    /// </summary>
    internal class GaolWraithScreenRender : RenderHandle
    {
        public override bool CanLoad() => DeepGaolWraithGate.Enabled;

        /// <summary>A1 频段 1.610–1.619，取 1.612</summary>
        public override float Weight => 1.612f;

        private static readonly Vector4[] ringBuffer = new Vector4[GaolWraithScreenFX.MaxRings];

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            GaolWraithScreenFX.Update();
            for (int i = 0; i < GaolWraithScreenFX.Afterglows.Length; i++) {
                ref readonly var glow = ref GaolWraithScreenFX.Afterglows[i];
                if (!glow.Active) {
                    continue;
                }
                float t = glow.Age / (float)glow.Life;
                GaolWraithDraw.DrawRibbon(glow.Pts, glow.Count, glow.Width,
                    fade: (1f - t * 0.55f), hot: glow.Hot, decay: t, seed: glow.Seed, ironWind: glow.IronWind);
            }
        }

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            if (!GaolWraithScreenFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.GaolWraithVeil?.Value;
            if (shader == null || CWRAsset.PerlinNoise?.Value == null) {
                return;
            }

            for (int i = 0; i < GaolWraithScreenFX.MaxRings; i++) {
                ref readonly var ring = ref GaolWraithScreenFX.Rings[i];
                if (!ring.Active) {
                    ringBuffer[i] = Vector4.Zero;
                    continue;
                }
                float t = ring.Age / (float)ring.Life;
                float radiusPx = ring.MaxRadiusPx * VaultUtils.EaseOutCubic(t);
                float strength = ring.Intensity * (1f - t) * (1f - t);
                Vector2 uv = WorldToScreenUV(ring.WorldCenter);
                ringBuffer[i] = new Vector4(uv.X, uv.Y, PixelsToHeightNorm(radiusPx), strength);
            }

            float flashProgress = GaolWraithScreenFX.FlashLife > 0
                ? MathHelper.Clamp(GaolWraithScreenFX.FlashAge / (float)GaolWraithScreenFX.FlashLife, 0f, 1f)
                : 1f;
            float flash = GaolWraithScreenFX.FlashActive ? GaolWraithScreenFX.FlashIntensity : 0f;
            //环辉平时冷粉，冲击帧期偏白热
            Vector3 ringColor = Vector3.Lerp(DeepGaolWraith.GaolPink.ToVector3(),
                DeepGaolWraith.GaolWhiteHot.ToVector3(), flash);

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["uDomain"]?.SetValue(GaolWraithScreenFX.DomainIntensity);
            shader.Parameters["uMist"]?.SetValue(GaolWraithScreenFX.MistLevel);
            shader.Parameters["uFlash"]?.SetValue(flash);
            shader.Parameters["uFlashProgress"]?.SetValue(flashProgress);
            shader.Parameters["ringData"]?.SetValue(ringBuffer);
            shader.Parameters["uRingColor"]?.SetValue(ringColor);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
            gd.Textures[1] = null;
        }

        private static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }

        private static float PixelsToHeightNorm(float pixels) {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            if (zoomY <= 0f) {
                zoomY = 1f;
            }
            return pixels * zoomY / Main.screenHeight;
        }
    }

    /// <summary>苍白怨魂：散监的囚魂缓缓上升，左右摆着走远，先显后隐（Fog 真 alpha 底）</summary>
    internal class PRT_GaolSoulShade : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private float swayPhase;
        private bool mirror;

        public PRT_GaolSoulShade Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 80;
            }
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            mirror = Main.rand.NextBool();
            Rotation = Main.rand.NextFloat(-0.3f, 0.3f);
        }

        public override void Reset() {
            base.Reset();
            swayPhase = 0f;
            mirror = false;
        }

        public override void AI() {
            //魂性上浮 + 左右摆游（出生横速衰减后交给摆动，不吞初速）
            Velocity.Y = MathF.Max(Velocity.Y - 0.02f, -1.9f);
            Velocity.X = Velocity.X * 0.95f + MathF.Sin(Time * 0.07f + swayPhase) * 0.055f;
            Rotation += 0.004f * (mirror ? -1f : 1f);
            Scale *= 1.0035f;
            float t = LifetimeCompletion;
            //先显后隐的钟形包络
            Opacity = MathF.Pow(MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi), 0.8f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            SpriteEffects fx = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //魂体（雾底染灵质色）+ 内芯苍白微光（A=0 加色）
            spriteBatch.Draw(tex, pos, null, Color * (0.55f * Opacity), Rotation, origin, Scale * 0.5f, fx, 0f);
            spriteBatch.Draw(tex, pos, null, (DeepGaolWraith.EctoPale with { A = 0 }) * (0.3f * Opacity),
                Rotation, origin, Scale * 0.26f, fx, 0f);
            return false;
        }
    }

    /// <summary>脱落的链节：带重力翻滚的铁节，落地一声轻响后锈灭（原版 Chain22 单节）</summary>
    internal class PRT_GaolChainLink : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private float spin;
        private bool landed;

        public PRT_GaolChainLink Configure(int lifetime, float spinRate) {
            Lifetime = lifetime;
            spin = spinRate;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 60;
            }
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            landed = false;
        }

        public override void AI() {
            if (!landed) {
                Velocity.X *= 0.99f;
                Velocity.Y = MathF.Min(Velocity.Y + 0.34f, 12f);
                Rotation += spin;
                if (Collision.SolidCollision(Position - new Vector2(4f, 4f), 8, 8)) {
                    landed = true;
                    Velocity = Vector2.Zero;
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.14f, Pitch = -0.3f, MaxInstances = 3 }, Position);
                }
            }
            //落地后原地锈灭，未落地也随寿命淡出
            float t = LifetimeCompletion;
            Opacity = 1f - MathF.Pow(t, 3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Chain22?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            Color light = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            spriteBatch.Draw(tex, pos, null, DeepGaolWraith.IronDeep * (0.6f * Opacity), Rotation, origin, Scale * 1.08f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, light.MultiplyRGB(DeepGaolWraith.IronMul) * Opacity, Rotation, origin, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
