using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
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
    /// 铸造监工：Dungeonworld 铸造机关层专属小 Boss，挂在验收堂天轨上的验收机件。
    /// 三只 Boss 三种运动平面：怨灵悬浮 / 不溺者水陆 / 监工一维轨巡→P3 断轨钟摆。
    /// P1 验收（轨巡+压印+浇渣）→ P2 加压（快压印/镖阵异色空窗巷/齿轮滚碾）→
    /// 30% 断轨演出（全场唯一大拍）→ P3 摆刑（弧端落锤、弧中恒安全的几何空窗）。
    /// 环境反杀全场独一份：玩家站上检修位触发板且毂心恰好过顶 → 对冲活塞砸个正着，
    /// 硬直 90f + 防御归零，每场限 2 次（余次灯挂在检修位上方）。
    /// 形体全借原版（Cog×3 异径叠转毂 + 石巨人拳压锤 + 地牢火轮滚碾形态 + Chain22 链柱），
    /// 锈橙铸铁重染。联机契约同前两案：转场只在服务器裁决盖 netUpdate 章，
    /// ai[0..3] 乘 SyncNPC 过线，房间坐标与反杀余次经 SendExtraAI 过线，
    /// 各端本地跑同一状态机做表现，节拍闩防快照回卷；轨位/摆角为确定性运动学
    /// （由同步计时推导，P3 钟摆走连续单时间线绝不重置——锚涡教训的直接移植）。
    /// NPC.damage 每帧归零，仅滚碾/摆锤达速窗内抬起。
    /// </summary>
    internal class FoundryOverseer : OverseerModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 数值（占位初值，验收再调；L6 深度档=怨灵×1.84）====================

        internal const int BaseLife = 12500;
        internal const int BaseDefense = 18;
        /// <summary>接触基伤，仅滚碾/摆锤达速窗内启用</summary>
        internal const int ContactDamage = 48;

        internal static (float Normal, float Expert) PressDamage => (52f, 44f);
        internal static (float Normal, float Expert) SlagDamage => (40f, 34f);
        internal static (float Normal, float Expert) DartDamage => (34f, 28f);

        internal int ScaleDamage((float Normal, float Expert) baseDamage)
            => (int)NPC.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);

        //==================== 状态 ====================

        internal const int StateEmerge = 0;
        internal const int StateRailPatrol = 1;
        internal const int StatePress = 2;
        internal const int StateSlagPour = 3;
        internal const int StateDartVolley = 4;
        internal const int StateWheelRoll = 5;
        internal const int StateBreakRail = 6;
        internal const int StatePendulum = 7;
        internal const int StateStunned = 8;
        internal const int StateDespawn = 9;
        internal const int StateDeath = 10;

        internal int State { get => (int)NPC.ai[0]; private set => NPC.ai[0] = value; }
        private ref float StateTimer => ref NPC.ai[1];
        /// <summary>状态内子参数：镖阵=空窗巷道号；滚碾=相位+回合×16；入场=变体标记</summary>
        private ref float StateParam => ref NPC.ai[2];
        private ref float AttackIndex => ref NPC.ai[3];

        /// <summary>相位：1 验收 / 2 加压 / 3 摆刑。直接从同步生命值推导，各端一致</summary>
        internal int PhaseIndex
            => NPC.life > NPC.lifeMax * 0.65f ? 1 : NPC.life > NPC.lifeMax * 0.30f ? 2 : 3;

        /// <summary>吊臂换体入场变体（OverseerDormantRig 经 NewNPC ai2 传入）</summary>
        internal const int EmergeVariantRig = 1;

        //==================== 房间坐标与反杀余次（SendExtraAI 过线）====================

        internal int roomOriginX = -1;
        internal int roomOriginY = -1;
        /// <summary>对冲活塞反杀余次（服务器裁决消耗，随每次 SyncNPC 过线供余次灯显示）</summary>
        internal int counterUses = CounterUsesMax;

        internal const int CounterUsesMax = 2;

        internal bool HasRoom => roomOriginX >= 0;
        internal Point RoomOrigin => new(roomOriginX, roomOriginY);

        public override void SendExtraAI(BinaryWriter writer) {
            //恒定长写入，杜绝流错位
            writer.Write(roomOriginX);
            writer.Write(roomOriginY);
            writer.Write((byte)counterUses);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            roomOriginX = reader.ReadInt32();
            roomOriginY = reader.ReadInt32();
            counterUses = reader.ReadByte();
        }

        //==================== 时序（全部具名常量）====================

        //入场（换体变体）：齿轮由慢到快咬合 → 教学空锤（落点=自位无人区）→ 入战
        private const int EmergeGearAt = 8;
        private const int EmergeTestPressAt = 30;
        private const int EmergeTotal = 70;
        private const int EmergePlainTotal = 40;

        //压印：光柱预告（弹幕侧承载）→ 快照式下落 → 触底停 → 收回
        private const int PressWindupP1 = 24;
        private const int PressWindupFast = 18;
        private const int PressStateTotal = 84;
        /// <summary>二连不锁同点的最小横距（服务器出锤时应用）</summary>
        internal const float PressMinGapX = 64f;

        //浇渣：浇包倾斜 → 5 槽扇形（中槽恒空=声明的跳位）→ 回正
        private const int SlagTiltFrames = 30;
        private const int SlagStateTotal = 56;
        internal const int SlagSkipIndex = 2;

        //镖阵：镖口逐亮 20f（空窗巷道异色）→ 齐射 → 全口冷却
        private const int DartWarnFrames = 20;
        private const int DartStateTotal = 62;
        internal const float DartSpeed = 9f;

        //齿轮滚碾：毂卸链坠地 → 地滚 16px/f 两个来回（可跳越）→ 链拉回上挂硬直
        private const int RollDetachFrames = 20;
        private const int RollRetractFrames = 26;
        private const float RollSpeed = 16f;
        private const int RollTurns = 2;

        //断轨演出（30%，全场唯一大拍）：火花瀑 → 轨段坠落 → 挂链成摆
        private const int BreakSparkAt = 8;
        private const int BreakFallAt = 40;
        private const int BreakCatchAt = 70;
        private const int BreakTotal = 90;

        //钟摆（连续单时间线，绝不重置 StateTimer）：θ=θmax·sin(ωt)
        internal const float PendMaxAngle = 1.13f;
        internal const float PendOmega = 0.035f;
        internal const float PendLength = 224f;
        private const int PendVolleyCycle = 480;
        private const int PendVolleyWarnAt = 220;
        private const int PendVolleyFireAt = 240;

        //反杀硬直：防御归零的全场输出窗
        private const int StunFrames = 90;
        /// <summary>毂心过顶判定横距</summary>
        private const float CounterHubGapX = 48f;

        private const int DeathTotal = 150;
        private const int DespawnTotal = 150;

        private const float MaxFindDistance = 2600f;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int attackCooldown;
        private Player targetPlayer;
        private int lastSeenState = -1;

        //入场闩
        private bool gearsCued;
        private bool testPressCued;
        //压印/浇渣/镖阵闩
        private bool pressSpawned;
        private bool slagPoured;
        private bool dartsFired;
        //滚碾闩
        private bool rollLanded;
        private float rollDir;
        //断轨闩
        private bool breakSparked;
        private bool breakFell;
        //钟摆节拍闩（本状态时间线连续，闩只涨不落）
        private int lastPendExtreme = -1;
        private int lastPendCross = -1;
        private int lastPendVolley = -1;
        //反杀硬直闩
        private bool stunCued;
        //死亡演出闩
        private bool deathLanded;
        private int deathLandT = -1;
        private bool deathSteamed;
        private bool deathUnsealed;
        private bool deathDone;
        //死亡齿轮碎片（纯本地物理表现，允许端间发散）
        private readonly Vector2[] gearPos = new Vector2[3];
        private readonly Vector2[] gearVel = new Vector2[3];
        private readonly float[] gearRot = new float[3];
        private bool gearsBurst;
        //服务器侧：上一锤落点（PressMinGapX 用，仅权威端有意义）
        private float lastPressX = float.MinValue;
        //齿轮咬合的连续转角（纯绘制）
        private float cogSpin;

        /// <summary>连续量抖动的确定性相位，各端一致</summary>
        internal float Seed => NPC.whoAmI * 0.7391f;

        //==================== 色板（铸铁灰蓝 + 炉锈橙 + 熔渣热红，对位 L6 蓝砖锈橙层染）====================

        internal static readonly Color IronMul = new(150, 152, 164);
        internal static readonly Color IronDeep = new(52, 54, 64);
        internal static readonly Color FurnaceOrange = new(222, 138, 58);
        internal static readonly Color SlagHot = new(238, 120, 40);
        internal static readonly Color SlagDark = new(96, 40, 28);
        internal static readonly Color SteamWhite = new(226, 230, 232);
        internal static readonly Color LampGreen = new(110, 220, 140);
        internal static readonly Color LampRed = new(224, 92, 64);

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.DontDoHardmodeScaling[Type] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.OnFire] = true;
        }

        public override void SetDefaults() {
            NPC.width = 60;
            NPC.height = 60;
            NPC.damage = ContactDamage;
            NPC.defense = BaseDefense;
            NPC.lifeMax = BaseLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.npcSlots = 10f;
            NPC.value = Item.buyPrice(0, 10);
            NPC.HitSound = SoundID.NPCHit4 with { Pitch = -0.2f };
            NPC.DeathSound = SoundID.NPCDeath14 with { Volume = 0.6f };
            //机械戏占位曲（计划的 MusicID.Golem 并不存在，编译实证后退 Boss3），正式配乐另议
            Music = MusicID.Boss3;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.FoundryOverseer.Bestiary"),
            ]);
        }

        public override void BossHeadSlot(ref int index) {
            //暂借石巨人的地图头像（机件方脸）
            index = NPCID.Sets.BossHeadTextures[NPCID.Golem];
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            //验工印章走 OnKill 服务器逐人定向结算（首杀必掉/复杀 25%），不进公共规则表
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<OverseerLogger>(), 3));
            npcLoot.Add(ItemDropRule.Common(ItemID.Cog, 1, 6, 12));
            npcLoot.Add(ItemDropRule.Common(ItemID.IronBar, 1, 8, 15));
            npcLoot.Add(ItemDropRule.Common(ItemID.HealingPotion, 1, 5, 10));
        }

        //==================== 全局转移与锁血 ====================

        /// <summary>断轨转相的服务器闩</summary>
        private bool breakDone;

        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient) {
                return;
            }
            if (State is StateEmerge or StateBreakRail or StateStunned or StateDeath or StateDespawn) {
                return;
            }

            if (TargetInvalid()) {
                ChangeState(StateDespawn);
                return;
            }

            //30%：断轨演出（清弹公平阀），此后永驻钟摆
            if (!breakDone && PhaseIndex >= 3) {
                breakDone = true;
                KillOwnedProjectiles();
                ChangeState(StateBreakRail);
                return;
            }

            //对冲活塞反杀（服务器裁决合取：站板 + 毂心过顶 + 余次未尽 + 轨巡族状态）
            if (counterUses > 0 && HasRoom && State is StateRailPatrol or StatePress or StateSlagPour or StateDartVolley) {
                for (int side = 0; side < 2; side++) {
                    Rectangle zone = ProofingHallRoom.BayZoneWorld(RoomOrigin, side == 0);
                    float bayX = zone.Center.X;
                    if (Math.Abs(NPC.Center.X - bayX) > CounterHubGapX) {
                        continue;
                    }
                    bool standing = false;
                    foreach (Player player in Main.ActivePlayers) {
                        if (!player.dead && zone.Contains(player.Center.ToPoint())) {
                            standing = true;
                            break;
                        }
                    }
                    if (standing) {
                        counterUses--;
                        KillOwnedProjectiles();
                        ChangeState(StateStunned);
                        return;
                    }
                }
            }
        }

        /// <summary>锁血：死亡演出没放完不许真死</summary>
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

        private void ChangeState(int state) {
            State = state;
            StateTimer = 0;
            StateParam = 0;
            NPC.netUpdate = !VaultUtils.isClient;
        }

        private void EndAttack(int cooldown) {
            attackCooldown = cooldown;
            if (!VaultUtils.isClient) {
                //P3 的收招回钟摆而非轨巡（轨已经断了）
                ChangeState(breakDone ? StatePendulum : StateRailPatrol);
            }
        }

        private void KillOwnedProjectiles() {
            if (VaultUtils.isClient) {
                return;
            }
            int t1 = ModContent.ProjectileType<OverseerPressStrike>();
            int t2 = ModContent.ProjectileType<OverseerSlagGlob>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && (p.type == t1 || p.type == t2)) {
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
            //防御每帧回基线：硬直窗内由 UpdateStunned 压到 0（镜像 damage 归零惯例）
            NPC.defense = BaseDefense;

            FindTarget();
            EvaluateGlobalTransitions();

            if (State != lastSeenState) {
                lastSeenState = State;
                pressSpawned = false;
                slagPoured = false;
                dartsFired = false;
                rollLanded = false;
                breakSparked = false;
                breakFell = false;
                stunCued = false;
                if (State == StatePendulum) {
                    lastPendExtreme = -1;
                    lastPendCross = -1;
                    lastPendVolley = -1;
                }
                if (State == StateDeath) {
                    deathLanded = false;
                    deathLandT = -1;
                    deathSteamed = false;
                    deathUnsealed = false;
                    gearsBurst = false;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(); break;
                case StateRailPatrol: UpdateRailPatrol(); break;
                case StatePress: UpdatePress(); break;
                case StateSlagPour: UpdateSlagPour(); break;
                case StateDartVolley: UpdateDartVolley(); break;
                case StateWheelRoll: UpdateWheelRoll(); break;
                case StateBreakRail: UpdateBreakRail(); break;
                case StatePendulum: UpdatePendulum(); break;
                case StateStunned: UpdateStunned(); break;
                case StateDespawn: UpdateDespawn(); break;
                case StateDeath: UpdateDeath(); break;
            }

            //齿轮咬合转角（转速=状态活跃度，硬直期卡顿）
            float spinRate = State switch {
                StateStunned => 0.01f + 0.06f * MathF.Max(0f, MathF.Sin((int)StateTimer * 0.7f)),
                StateWheelRoll => 0.3f,
                StateDeath => MathF.Max(0f, 0.2f - (int)StateTimer * 0.002f),
                _ => 0.1f + MathF.Min(0.1f, NPC.velocity.Length() * 0.01f),
            };
            cogSpin += spinRate;

            //战斗期压住 Boss 房环境粒子（IMPL-E 客户端演出口，慢节拍续票）
            if (!Main.dedServ && HasRoom && State != StateDespawn && (int)StateTimer % 8 == 0) {
                Rectangle bounds = ProofingHallRoom.Bounds(RoomOrigin);
                Ambience.AmbientQuiet.Request(new Rectangle(
                    bounds.X * 16, bounds.Y * 16, bounds.Width * 16, bounds.Height * 16), 12);
            }

            if (attackCooldown > 0) {
                attackCooldown--;
            }

            float glow = 0.3f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + Seed);
            Lighting.AddLight(NPC.Center, 0.3f * glow, 0.18f * glow, 0.06f * glow);
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
            return from.Y + 200f;
        }

        //==================== 轨道几何（无房降级：以出生点虚拟一条轨）====================

        /// <summary>毂心悬挂高（一维舞台的 Y）；无房时以出生位为轨</summary>
        private float virtualRailY = float.MinValue;

        internal float HubY() {
            if (HasRoom) {
                return ProofingHallRoom.HubWorldY(RoomOrigin);
            }
            if (virtualRailY == float.MinValue) {
                virtualRailY = NPC.Center.Y;
            }
            return virtualRailY;
        }

        internal float RailLeftX => HasRoom
            ? (roomOriginX + ProofingHallRoom.InteriorLeft + 3) * 16f
            : NPC.Center.X - 500f;
        internal float RailRightX => HasRoom
            ? (roomOriginX + ProofingHallRoom.InteriorRight - 3) * 16f
            : NPC.Center.X + 500f;

        internal Vector2 PendAnchor() => HasRoom
            ? ProofingHallRoom.BreakAnchorWorld(RoomOrigin)
            : new Vector2(NPC.Center.X, HubY() - 20f);

        /// <summary>钟摆确定性位形（θ 由连续同步计时推导，各端一致）</summary>
        internal Vector2 PendBobPos(float t) {
            float theta = PendMaxAngle * MathF.Sin(t * PendOmega);
            Vector2 anchor = PendAnchor();
            return anchor + new Vector2(MathF.Sin(theta) * PendLength, MathF.Cos(theta) * PendLength);
        }

        //==================== 入场：三枚齿轮由慢到快咬合，教学空锤 ====================

        private void UpdateEmerge() {
            int t = (int)StateTimer;
            bool fromRig = (int)StateParam == EmergeVariantRig;
            int total = fromRig ? EmergeTotal : EmergePlainTotal;
            NPC.dontTakeDamage = t < total - 10;

            //吊在轨下，轻微荡定
            NPC.velocity *= 0.9f;
            NPC.velocity.Y += MathHelper.Clamp((HubY() - NPC.Center.Y) * 0.08f, -3f, 3f);

            if (!gearsCued && t >= EmergeGearAt) {
                gearsCued = true;
                SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, NPC.Center);
                //入场重音：压声床（客户端演出口，距离门自守）
                if (!Main.dedServ && Main.LocalPlayer.Alives()
                    && Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1500f) {
                    Ambience.DungeonworldAmbience.PushStinger(
                        SoundID.Roar with { Volume = 0.5f, Pitch = -0.9f }, 0.4f);
                }
                ShakeNearby(2f);
            }

            if (fromRig && !testPressCued && t >= EmergeTestPressAt) {
                //教学空锤：落点=自位（无人区），让玩家白看一次完整压印时序
                testPressCued = true;
                if (!VaultUtils.isClient) {
                    SpawnPress(NPC.Center.X, PressWindupP1);
                }
            }

            if (t >= total) {
                attackCooldown = 40;
                if (!VaultUtils.isClient) {
                    ChangeState(StateRailPatrol);
                }
            }
        }

        //==================== 轨巡（连接态：迟滞+过冲的机件追踪）+ 选招 ====================

        private void UpdateRailPatrol() {
            if (targetPlayer == null) {
                NPC.velocity *= 0.94f;
                return;
            }
            //一维追踪：迟滞 lerp + 惯性过冲（机件的"笨"就是它的性格）
            float targetX = MathHelper.Clamp(targetPlayer.Center.X, RailLeftX, RailRightX);
            NPC.velocity.X += (targetX - NPC.Center.X) * 0.006f;
            NPC.velocity.X *= 0.93f;
            NPC.velocity.Y = MathHelper.Clamp((HubY() - NPC.Center.Y) * 0.15f, -6f, 6f);

            //换向打滑拍（速度足够且方向翻转，客户端表现）
            if (!Main.dedServ && Math.Abs(NPC.velocity.X) > 3f && (int)StateTimer % 14 == 0
                && MathF.Sign(NPC.velocity.X) != MathF.Sign(targetX - NPC.Center.X)) {
                SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 2 }, NPC.Center);
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center + new Vector2(0f, -34f),
                    new Vector2(-NPC.velocity.X * 0.2f, Main.rand.NextFloat(0.5f, 1.5f)),
                    Color.Lerp(FurnaceOrange, Color.White, 0.4f),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(8, 14));
            }

            if (VaultUtils.isClient || attackCooldown > 0 || StateTimer <= 20 || TargetInvalid()) {
                return;
            }
            PickNextAttack();
        }

        /// <summary>分相位手排环 + 公平阀（压印要求毂已巡到目标近旁）</summary>
        private void PickNextAttack() {
            float dx = Math.Abs(NPC.Center.X - targetPlayer.Center.X);
            for (int guard = 0; guard < 4; guard++) {
                AttackIndex++;
                int idx = (int)AttackIndex;
                int pick;
                int param = 0;
                if (PhaseIndex <= 1) {
                    //P1 验收：压印 → 浇渣 交替
                    pick = idx % 2 == 0 ? StatePress : StateSlagPour;
                }
                else {
                    //P2 加压：压印(快) → 镖阵 → 齿轮滚碾 → 浇渣 → 压印(快) → 镖阵
                    switch (idx % 6) {
                        case 0: pick = StatePress; break;
                        case 1: pick = StateDartVolley; break;
                        case 2: pick = StateWheelRoll; break;
                        case 3: pick = StateSlagPour; break;
                        case 4: pick = StatePress; break;
                        default: pick = StateDartVolley; break;
                    }
                }
                //公平阀：没巡到目标近旁不落锤（位置即预告，锤永远从头顶来）
                if (pick == StatePress && dx > 220f) {
                    continue;
                }
                if (pick == StateDartVolley) {
                    //空窗巷道：确定性选巷（无掷随机），乘 StateParam 过线供各端灯色一致
                    param = (idx + NPC.whoAmI) % ProofingHallRoom.DartLaneRows.Length;
                }
                ChangeState(pick);
                StateParam = param;
                return;
            }
            attackCooldown = 30;
        }

        private int PhaseCooldown() => PhaseIndex switch {
            1 => 90,
            2 => 70,
            _ => 60,
        };

        //==================== 压印 ====================

        private int PressWindup => PhaseIndex >= 2 ? PressWindupFast : PressWindupP1;

        private void UpdatePress() {
            int t = (int)StateTimer;
            //定住轨位（光柱已锁 x，承诺不追瞄）
            NPC.velocity.X *= 0.8f;
            NPC.velocity.Y = MathHelper.Clamp((HubY() - NPC.Center.Y) * 0.15f, -6f, 6f);

            if (!pressSpawned && t >= 2) {
                pressSpawned = true;
                if (!VaultUtils.isClient) {
                    //二连不锁同点（公平阀，服务器出锤时应用）
                    float lockX = NPC.Center.X;
                    if (Math.Abs(lockX - lastPressX) < PressMinGapX) {
                        lockX = lastPressX + PressMinGapX * MathF.Sign(lockX - lastPressX + 0.01f);
                    }
                    lastPressX = lockX;
                    SpawnPress(lockX, PressWindup);
                }
                SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
            }

            if (t >= PressStateTotal) {
                EndAttack(PhaseCooldown());
            }
        }

        /// <summary>服务器出锤：锁定 x/预告帧/伤害随 spawn 包原子过线</summary>
        private void SpawnPress(float lockX, int windup) {
            Projectile.NewProjectile(NPC.GetSource_FromAI(),
                new Vector2(lockX, NPC.Center.Y + 28f), Vector2.Zero,
                ModContent.ProjectileType<OverseerPressStrike>(), ScaleDamage(PressDamage), 5f,
                Main.myPlayer, windup, NPC.whoAmI);
        }

        //==================== 浇渣 ====================

        private void UpdateSlagPour() {
            int t = (int)StateTimer;
            NPC.velocity.X *= 0.86f;
            NPC.velocity.Y = MathHelper.Clamp((HubY() - NPC.Center.Y) * 0.15f, -6f, 6f);

            if (t == 4) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
            }
            //包沿发亮渣珠（客户端，倾斜期）
            if (!Main.dedServ && t < SlagTiltFrames && t % 4 == 0) {
                PRTLoader.NewParticle<PRT_SlagBead>(LadlePos() + Main.rand.NextVector2Circular(6f, 4f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.2f, 0.8f)),
                    SlagHot, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
            }

            if (!slagPoured && t >= SlagTiltFrames) {
                slagPoured = true;
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, NPC.Center);
                if (!VaultUtils.isClient) {
                    int damage = ScaleDamage(SlagDamage);
                    for (int i = 0; i < 5; i++) {
                        if (i == SlagSkipIndex) {
                            //第 3 槽恒空：发射循环实读的声明跳位
                            continue;
                        }
                        float ang = MathHelper.Lerp(-0.85f, 0.85f, i / 4f) + MathHelper.PiOver2;
                        Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(6.5f, 8f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), LadlePos(), vel,
                            ModContent.ProjectileType<OverseerSlagGlob>(), damage, 2f,
                            Main.myPlayer, i, NPC.whoAmI);
                    }
                }
            }

            if (t >= SlagStateTotal) {
                EndAttack(PhaseCooldown());
            }
        }

        internal Vector2 LadlePos()
            => NPC.Center + new Vector2(26f, 14f);

        //==================== 镖阵 ====================

        internal int DartGapLane => (int)StateParam % ProofingHallRoom.DartLaneRows.Length;

        private void UpdateDartVolley() {
            int t = (int)StateTimer;
            NPC.velocity.X *= 0.88f;
            NPC.velocity.Y = MathHelper.Clamp((HubY() - NPC.Center.Y) * 0.15f, -6f, 6f);
            if (!HasRoom) {
                //无房降级：镖口不存在，本招让位
                EndAttack(40);
                return;
            }

            if (t == 2) {
                SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.6f, Pitch = 0.1f, MaxInstances = 2 }, NPC.Center);
            }

            if (!dartsFired && t >= DartWarnFrames) {
                dartsFired = true;
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
                if (!VaultUtils.isClient) {
                    FireDartLanes(DartGapLane);
                }
            }

            if (t >= DartStateTotal) {
                EndAttack(PhaseCooldown());
            }
        }

        /// <summary>齐射：除空窗巷道外每巷双向对射（原版机关镖，伤害走生成参数覆盖）</summary>
        private void FireDartLanes(int gapLane) {
            int damage = ScaleDamage(DartDamage);
            for (int lane = 0; lane < ProofingHallRoom.DartLaneRows.Length; lane++) {
                if (lane == gapLane) {
                    continue;
                }
                float y = ProofingHallRoom.DartLaneWorldY(RoomOrigin, lane);
                float lx = ProofingHallRoom.DartPortWorldX(RoomOrigin, left: true);
                float rx = ProofingHallRoom.DartPortWorldX(RoomOrigin, left: false);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(lx, y),
                    new Vector2(DartSpeed, 0f), ProjectileID.PoisonDartTrap, damage, 1f, Main.myPlayer);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(rx, y),
                    new Vector2(-DartSpeed, 0f), ProjectileID.PoisonDartTrap, damage, 1f, Main.myPlayer);
            }
        }

        //==================== 齿轮滚碾 ====================

        private int RollPhase => (int)StateParam % 16;
        private int RollTurnCount => (int)StateParam / 16;

        private void NextRollPhase(int phase, int turns) {
            StateParam = phase + turns * 16;
            StateTimer = 0;
            NPC.netUpdate = !VaultUtils.isClient;
        }

        private void UpdateWheelRoll() {
            int t = (int)StateTimer;
            int phase = RollPhase;
            float groundY = FindGroundY(NPC.Center);

            if (phase == 0) {
                //毂卸链坠地：弹跳一次即预告拍
                if (t == 1) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 2 }, NPC.Center);
                }
                NPC.velocity.X *= 0.8f;
                NPC.velocity.Y = MathF.Min(NPC.velocity.Y + 0.8f, 16f);
                if (!rollLanded && NPC.Center.Y > groundY - 34f) {
                    rollLanded = true;
                    NPC.velocity.Y = -4.5f;
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
                    ShakeNearby(2f);
                    //落地即定滚向（发动即定向）
                    rollDir = targetPlayer != null && targetPlayer.Center.X >= NPC.Center.X ? 1f : -1f;
                }
                if (t >= RollDetachFrames && rollLanded) {
                    NextRollPhase(1, 0);
                }
                return;
            }

            if (phase == 1) {
                //地滚：恒速 16px/f、高 3 格可跳越；滚碾期天上无攻击（单层压力纪律）
                NPC.velocity.X = rollDir * RollSpeed;
                float feet = groundY - 30f;
                NPC.velocity.Y = MathHelper.Clamp((feet - NPC.Center.Y) * 0.25f, -6f, 8f);
                if (Math.Abs(NPC.velocity.X) > 10f) {
                    NPC.damage = ContactDamage;
                }
                if (!Main.dedServ && t % 3 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(NPC.Center + new Vector2(-rollDir * 20f, 26f),
                        new Vector2(-rollDir * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.5f, 2f)),
                        Color.Lerp(FurnaceOrange, Color.White, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(8, 14));
                }
                //到达轨端/超时=一个来回
                bool atEdge = NPC.Center.X < RailLeftX + 40f || NPC.Center.X > RailRightX - 40f;
                if (atEdge || t > 110) {
                    int turns = RollTurnCount + 1;
                    if (turns >= RollTurns) {
                        //回收预告：链先绷直一响
                        SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.25f, MaxInstances = 2 }, NPC.Center);
                        NextRollPhase(2, turns);
                    }
                    else {
                        rollDir = -rollDir;
                        NextRollPhase(1, turns);
                    }
                }
                return;
            }

            //回收上挂：26f 全身硬直（贴身输出窗）
            NPC.velocity.X *= 0.8f;
            NPC.velocity.Y = MathHelper.Clamp((HubY() - NPC.Center.Y) * 0.12f, -12f, 4f);
            if (t >= RollRetractFrames + 14) {
                EndAttack(PhaseCooldown());
            }
        }

        //==================== 断轨演出（30%，全场唯一大拍）====================

        private void UpdateBreakRail() {
            int t = (int)StateTimer;
            NPC.dontTakeDamage = true;
            Vector2 anchor = PendAnchor();

            //火花瀑：断轨点持续迸溅
            if (!breakSparked && t >= BreakSparkAt) {
                breakSparked = true;
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = 0.4f, MaxInstances = 2 }, anchor);
            }
            if (!Main.dedServ && t >= BreakSparkAt && t < BreakFallAt && t % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(anchor + new Vector2(Main.rand.NextFloat(-20f, 20f), 4f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3.5f)),
                    Color.Lerp(FurnaceOrange, Color.White, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 18));
            }

            if (!breakFell && t >= BreakFallAt) {
                //轨段坠落：全场唯一大拍
                breakFell = true;
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1f, Pitch = -0.6f, MaxInstances = 1 }, anchor);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = -0.7f, MaxInstances = 2 }, anchor);
                ShakeNearby(4f, 1400f);
                if (!Main.dedServ && Main.LocalPlayer.Alives()
                    && Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1500f) {
                    Ambience.DungeonworldAmbience.PushGradePulse(FurnaceOrange, 0.35f, 40);
                }
            }

            //移到断轨点下方接住链（钟摆起点=摆底）
            Vector2 want = t < BreakCatchAt
                ? new Vector2(anchor.X, HubY())
                : anchor + new Vector2(0f, PendLength);
            NPC.velocity = (want - NPC.Center) * 0.12f;

            if (t >= BreakTotal) {
                attackCooldown = 60;
                if (!VaultUtils.isClient) {
                    ChangeState(StatePendulum);
                }
            }
        }

        //==================== 钟摆（P3 常驻，连续单时间线绝不重置）====================

        private void UpdatePendulum() {
            float t = StateTimer;
            Vector2 bob = PendBobPos(t);
            NPC.velocity = bob - NPC.Center;

            //摆速达速窗=接触伤害（弧中快、弧端慢：弧端安全但吃锤，弧中危险但无锤）
            if (NPC.velocity.Length() > 10f) {
                NPC.damage = ContactDamage;
            }

            float phaseArg = t * PendOmega;
            float sin = MathF.Sin(phaseArg);

            //弧端落锤：每个极值一锤（闩只涨不落，快照回卷不重放）
            int extremeIndex = (int)MathF.Floor((phaseArg + MathHelper.PiOver2) / MathHelper.Pi);
            if (extremeIndex > lastPendExtreme && MathF.Abs(sin) > 0.985f) {
                lastPendExtreme = extremeIndex;
                SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, NPC.Center);
                if (!VaultUtils.isClient) {
                    SpawnPress(NPC.Center.X, 14);
                }
            }

            //过中浇渣：每隔一次过中点撒一把（弧中央站位=声明的几何空窗，渣从头顶来提醒别常驻）
            int crossIndex = (int)MathF.Floor(phaseArg / MathHelper.Pi + 0.5f);
            if (crossIndex > lastPendCross && MathF.Abs(sin) < 0.08f && t > 30f) {
                lastPendCross = crossIndex;
                if (crossIndex % 2 == 0) {
                    SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
                    if (!VaultUtils.isClient) {
                        int damage = ScaleDamage(SlagDamage);
                        for (int i = 0; i < 3; i++) {
                            Vector2 vel = new(NPC.velocity.X * 0.3f + (i - 1) * 1.6f, 2f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), LadlePos(), vel,
                                ModContent.ProjectileType<OverseerSlagGlob>(), damage, 2f,
                                Main.myPlayer, i, NPC.whoAmI);
                        }
                    }
                }
            }

            //周期镖阵（双高度）：巷道号由周期序确定性推导，预告窗各端同拍同色
            int volleyIndex = (int)t / PendVolleyCycle;
            int volleyPhase = (int)t % PendVolleyCycle;
            if (HasRoom && volleyPhase == PendVolleyFireAt && volleyIndex >= lastPendVolley + 1) {
                lastPendVolley = volleyIndex;
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
                if (!VaultUtils.isClient) {
                    FireDartLanes(PendVolleyGapLane(volleyIndex));
                }
            }

            //摆行风声（速度门控的呼啸）
            if (!Main.dedServ && NPC.velocity.Length() > 12f && (int)t % 16 == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.25f, Pitch = -0.6f, MaxInstances = 2 }, NPC.Center);
            }
        }

        /// <summary>钟摆期镖阵空窗巷道（确定性推导，客户端预告灯与服务器齐射同源）</summary>
        internal int PendVolleyGapLane(int volleyIndex)
            => (volleyIndex + NPC.whoAmI) % ProofingHallRoom.DartLaneRows.Length;

        /// <summary>钟摆期镖阵预告窗（绘制层读：预警亮灯中/空窗巷道号）</summary>
        internal bool PendVolleyWarning(out int gapLane) {
            gapLane = 0;
            if (State != StatePendulum || !HasRoom) {
                return false;
            }
            int volleyPhase = (int)StateTimer % PendVolleyCycle;
            if (volleyPhase < PendVolleyWarnAt || volleyPhase >= PendVolleyFireAt) {
                return false;
            }
            gapLane = PendVolleyGapLane((int)StateTimer / PendVolleyCycle);
            return true;
        }

        //==================== 反杀硬直（防御归零的全场输出窗）====================

        private void UpdateStunned() {
            int t = (int)StateTimer;
            NPC.defense = 0;
            if (!stunCued && t >= 1) {
                stunCued = true;
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.8f, MaxInstances = 1 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 1 }, NPC.Center);
                ShakeNearby(3f);
                if (!Main.dedServ) {
                    for (int k = 0; k < 12; k++) {
                        PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(26f, 26f),
                            Main.rand.NextVector2Circular(3f, 3f),
                            Color.Lerp(FurnaceOrange, Color.White, Main.rand.NextFloat(0.5f)),
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 18));
                    }
                }
            }
            //被砸得下坠半截，链拽着晃
            float sagY = HubY() + 46f;
            NPC.velocity.X *= 0.9f;
            NPC.velocity.Y = MathHelper.Clamp((sagY - NPC.Center.Y) * 0.1f, -4f, 4f);
            NPC.netOffset = new Vector2(MathF.Sin(t * 1.9f) * 2f, 0f);
            //泄压白汽
            if (!Main.dedServ && t % 5 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center + Main.rand.NextVector2Circular(20f, 20f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.4f)),
                    SteamWhite * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(20, 34));
            }

            if (t >= StunFrames) {
                EndAttack(60);
            }
        }

        //==================== 脱战与死亡 ====================

        private void UpdateDespawn() {
            int t = (int)StateTimer;
            NPC.dontTakeDamage = t > 20;
            //沿轨退回停靠位挂起熄灯
            float homeX = HasRoom
                ? ProofingHallRoom.RigWorldPos(RoomOrigin).X
                : NPC.Center.X;
            NPC.velocity.X = MathHelper.Clamp((homeX - NPC.Center.X) * 0.04f, -8f, 8f);
            NPC.velocity.Y = MathHelper.Clamp((HubY() - NPC.Center.Y) * 0.1f, -6f, 6f);

            if (t > DespawnTotal - 20) {
                NPC.EncourageDespawn(10);
            }
            if (!VaultUtils.isClient && !TargetInvalid() && t < 50) {
                attackCooldown = 60;
                ChangeState(breakDone ? StatePendulum : StateRailPatrol);
            }
        }

        private void UpdateDeath() {
            int t = (int)StateTimer;
            NPC.dontTakeDamage = true;

            if (t == 2) {
                //断链完全崩断
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.9f, Pitch = 0.5f, MaxInstances = 1 }, NPC.Center);
            }

            if (!deathLanded) {
                //整机坠地
                NPC.velocity.X *= 0.95f;
                NPC.velocity.Y = MathF.Min(NPC.velocity.Y + 0.5f, 18f);
                float groundY = FindGroundY(NPC.Center);
                if (NPC.Center.Y > groundY - 32f) {
                    deathLanded = true;
                    deathLandT = t;
                    NPC.Center = new Vector2(NPC.Center.X, groundY - 32f);
                    NPC.velocity = Vector2.Zero;
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.6f, MaxInstances = 1 }, NPC.Center);
                    ShakeNearby(3f, 1400f);
                    //三枚齿轮作为物理碎片弹跳滚散（纯本地表现，允许端间发散）
                    if (!Main.dedServ) {
                        gearsBurst = true;
                        for (int i = 0; i < 3; i++) {
                            gearPos[i] = NPC.Center + new Vector2((i - 1) * 8f, -10f);
                            gearVel[i] = new Vector2((i - 1) * 2.6f + Main.rand.NextFloat(-1f, 1f),
                                -Main.rand.NextFloat(3f, 6f));
                            gearRot[i] = 0f;
                        }
                    }
                }
            }
            else {
                NPC.velocity = Vector2.Zero;
                int since = t - deathLandT;
                //机体抽搐两拍、蒸汽逐处泄压
                if (since is 18 or 40) {
                    NPC.netOffset = new Vector2(0f, -3f);
                    SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 2 }, NPC.Center);
                }
                if (!Main.dedServ && since % 6 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        NPC.Center + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(-16f, 10f)),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.8f, 1.8f)),
                        SteamWhite * 0.55f, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(24, 40));
                }
                if (!deathSteamed && since >= 60) {
                    //蒸汽柱冲顶（全场第二大拍，衰减版）
                    deathSteamed = true;
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.9f, Pitch = -0.6f, MaxInstances = 1 }, NPC.Center);
                    ShakeNearby(2.5f);
                    if (!Main.dedServ) {
                        for (int k = 0; k < 14; k++) {
                            PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), -8f),
                                new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(2.5f, 6f)),
                                SteamWhite * 0.7f, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(40, 70));
                        }
                    }
                }
                if (!deathUnsealed && since >= 84) {
                    //熄火：闸门升起
                    deathUnsealed = true;
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
                    if (!VaultUtils.isClient && HasRoom) {
                        ProofingHallWatcher.SealDoors(RoomOrigin, false);
                    }
                }
            }

            //齿轮碎片本地物理（弹跳+摩擦）
            if (gearsBurst) {
                for (int i = 0; i < 3; i++) {
                    gearVel[i].Y = MathF.Min(gearVel[i].Y + 0.35f, 10f);
                    gearVel[i].X *= 0.99f;
                    gearPos[i] += gearVel[i];
                    gearRot[i] += gearVel[i].X * 0.08f;
                    float g = FindGroundY(gearPos[i] - new Vector2(0f, 12f));
                    if (gearPos[i].Y > g - 10f && gearVel[i].Y > 0f) {
                        gearPos[i].Y = g - 10f;
                        gearVel[i].Y *= -0.45f;
                        gearVel[i].X *= 0.8f;
                    }
                }
            }

            if (t >= DeathTotal) {
                deathDone = true;
                NPC.dontTakeDamage = false;
                if (!VaultUtils.isClient) {
                    NPC.StrikeInstantKill();
                }
            }
        }

        //==================== 击杀通报与结算 ====================

        public override void OnKill() {
            ProofingHallWatcher.NotifyOverseerDefeated(NPC.Center);
            DungeonworldBossRecords.ServerSettleKill(DungeonworldBossRecords.BossIdOverseer,
                NPC, NPC.Center, ModContent.ItemType<ProofSealCharm>());
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            //受击迸铁屑与泄压小汽（铸铁的材质回答）
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(22f, 22f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(1f, 2.6f), -Main.rand.NextFloat(0.5f, 2f)),
                    Color.Lerp(FurnaceOrange, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(8, 14));
            }
            if (Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center + Main.rand.NextVector2Circular(18f, 18f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    SteamWhite * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(16, 28));
            }
        }

        private void ShakeNearby(float amount, float range = 1200f) {
            if (Main.dedServ || Main.LocalPlayer == null) {
                return;
            }
            if (Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) > range) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(amount);
        }

        //==================== 绘制：轨灯 → 链柱 → 毂体/滚碾形态 → 浇包 → 余次灯 → 炉芯 ====================

        private float BodyAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => MathHelper.Clamp(t / 14f, 0f, 1f),
                StateDespawn => MathHelper.Clamp(1f - (t - 90) / 50f, 0f, 1f),
                _ => 1f,
            };
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadItem(ItemID.Cog);
            Main.instance.LoadItem(ItemID.CookingPot);
            Main.instance.LoadNPC(NPCID.BlazingWheel);
            Texture2D cogTex = TextureAssets.Item[ItemID.Cog]?.Value;
            Texture2D potTex = TextureAssets.Item[ItemID.CookingPot]?.Value;
            Texture2D wheelTex = TextureAssets.Npc[NPCID.BlazingWheel]?.Value;
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            if (cogTex == null || potTex == null || wheelTex == null || chainTex == null) {
                return false;
            }

            float alpha = BodyAlpha();
            if (alpha <= 0.01f) {
                return false;
            }

            //死亡碎片齿轮
            if (gearsBurst) {
                for (int i = 0; i < 3; i++) {
                    float s = 0.9f - i * 0.2f;
                    spriteBatch.Draw(cogTex, gearPos[i] - Main.screenPosition, null,
                        drawColor.MultiplyRGB(IronMul), gearRot[i], cogTex.Size() * 0.5f, s, SpriteEffects.None, 0f);
                }
            }

            //吊链：轨巡=竖直链柱；钟摆=锚到摆锤的斜链；硬直=多一段下垂
            Vector2 chainTop = State == StatePendulum || State == StateBreakRail && (int)StateTimer >= BreakCatchAt
                ? PendAnchor()
                : new Vector2(NPC.Center.X, HasRoom ? (roomOriginY + ProofingHallRoom.RailRel) * 16f + 8f : HubY() - 40f);
            Undrowned.DrawChainLine(spriteBatch, chainTex, chainTop, NPC.Center + new Vector2(0f, -18f), drawColor, alpha);

            bool wheelForm = State == StateWheelRoll && RollPhase == 1;
            if (wheelForm) {
                //滚碾形态：去焰重染纯锈铁的火轮
                int count = Math.Max(1, Main.npcFrameCount[NPCID.BlazingWheel]);
                Rectangle frame = new(0, 0, wheelTex.Width, wheelTex.Height / count);
                Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.5f);
                spriteBatch.Draw(wheelTex, NPC.Center - Main.screenPosition, frame,
                    IronDeep * (0.8f * alpha), cogSpin * 2f, origin, 1.35f, SpriteEffects.None, 0f);
                spriteBatch.Draw(wheelTex, NPC.Center - Main.screenPosition, frame,
                    drawColor.MultiplyRGB(IronMul) * alpha, cogSpin * 2f, origin, 1.25f, SpriteEffects.None, 0f);
            }
            else {
                //毂体：大中小三枚齿轮同轴反向咬合（暗缘 + 铸铁乘色）
                Vector2 cogOrigin = cogTex.Size() * 0.5f;
                (float scale, float dir)[] cogs = [(2.1f, 1f), (1.45f, -1.6f), (0.85f, 2.4f)];
                foreach ((float scale, float dir) in cogs) {
                    spriteBatch.Draw(cogTex, NPC.Center - Main.screenPosition, null,
                        IronDeep * (0.75f * alpha), cogSpin * dir, cogOrigin, scale * 1.08f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(cogTex, NPC.Center - Main.screenPosition, null,
                        drawColor.MultiplyRGB(IronMul) * alpha, cogSpin * dir, cogOrigin, scale, SpriteEffects.None, 0f);
                }
                //浇包侧挂（浇渣期倾倒）
                float tilt = State == StateSlagPour
                    ? MathHelper.Clamp((int)StateTimer / (float)SlagTiltFrames, 0f, 1f) * 1.1f
                    : 0.15f;
                spriteBatch.Draw(potTex, LadlePos() - Main.screenPosition, null,
                    drawColor.MultiplyRGB(IronMul) * alpha, tilt, potTex.Size() * 0.5f, 0.9f, SpriteEffects.None, 0f);
            }

            DrawGlowLayers(spriteBatch, alpha);
            return false;
        }

        /// <summary>加色层：炉芯 + 轨灯 + 检修位余次灯 + 镖口预警灯（强度写进色乘，永不 A=0 染色）</summary>
        private void DrawGlowLayers(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 gOrigin = glow.Size() * 0.5f;

            //炉芯：毂心一粒炉橙，硬直期明灭挣扎
            float core = State == StateStunned
                ? 0.25f + 0.35f * MathF.Max(0f, MathF.Sin((int)StateTimer * 0.6f))
                : 0.55f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.2f + Seed);
            sb.Draw(glow, NPC.Center - Main.screenPosition, null, FurnaceOrange * (core * alpha), 0f,
                gOrigin, new Vector2(16f * 2f / glow.Width), SpriteEffects.None, 0f);

            if (HasRoom) {
                //轨灯 8 盏（战斗期常亮微光）
                float railY = (roomOriginY + ProofingHallRoom.RailRel) * 16f + 8f;
                for (int i = 0; i < 8; i++) {
                    float x = (roomOriginX + 8 + i * 9) * 16f;
                    sb.Draw(glow, new Vector2(x, railY) - Main.screenPosition, null,
                        FurnaceOrange * (0.28f * alpha), 0f, gOrigin,
                        new Vector2(5f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
                //检修位余次灯：绿=可用，暗=已耗
                for (int side = 0; side < 2; side++) {
                    Rectangle zone = ProofingHallRoom.BayZoneWorld(RoomOrigin, side == 0);
                    Vector2 baseAt = new(zone.Center.X, zone.Top - 10);
                    for (int u = 0; u < CounterUsesMax; u++) {
                        bool lit = u < counterUses;
                        sb.Draw(glow, baseAt + new Vector2((u - 0.5f) * 14f, 0f) - Main.screenPosition, null,
                            (lit ? LampGreen : IronDeep) * ((lit ? 0.55f : 0.2f) * alpha), 0f, gOrigin,
                            new Vector2(5f * 2f / glow.Width), SpriteEffects.None, 0f);
                    }
                }
                //镖口预警灯：齐射前逐口亮，空窗巷道恒以绿灯声明（gap 视觉同一性金标准）
                bool warn = State == StateDartVolley && (int)StateTimer < DartWarnFrames && (int)StateTimer > 2;
                int gapLane = DartGapLane;
                if (!warn) {
                    warn = PendVolleyWarning(out gapLane);
                }
                if (warn) {
                    for (int lane = 0; lane < ProofingHallRoom.DartLaneRows.Length; lane++) {
                        float y = ProofingHallRoom.DartLaneWorldY(RoomOrigin, lane);
                        Color lamp = lane == gapLane ? LampGreen : LampRed;
                        for (int side = 0; side < 2; side++) {
                            float x = ProofingHallRoom.DartPortWorldX(RoomOrigin, side == 0);
                            sb.Draw(glow, new Vector2(x, y) - Main.screenPosition, null,
                                lamp * (0.6f * alpha), 0f, gOrigin,
                                new Vector2(6f * 2f / glow.Width), SpriteEffects.None, 0f);
                        }
                    }
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
