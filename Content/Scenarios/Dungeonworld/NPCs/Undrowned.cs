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
    /// 不溺者：Dungeonworld 水牢层专属小 Boss，泄洪堂里淹不死的沉锚刑死囚。
    /// 与深牢怨灵成镜像：物理泡胀巨汉对无形怨魂、贴地贴水对悬浮、
    /// 重量语言（拖、拽、抡、砸）对灵质语言。水位即相位表：
    /// P1 陆刑（踝水）掷锚/砸地起浪/拖锚突进 → 70% 涨水至刻度一 →
    /// P2 半淹（水下是他的领域）破水突袭/布水雷/水面砸浪 → 35% 涨水至刻度二 →
    /// P3 深水 锚涡/上掷锚压柱顶。死亡演出=格栅锈裂整槽泄洪，他被水流拽走。
    /// 形体全借原版（日食鱼人躯体 + 原版锚贴图 + Chain22 锚链），重染尸青与锈橙。
    /// 联机契约照怨灵先例：转场只在服务器裁决盖 netUpdate 章，ai[0..3] 乘 SyncNPC 过线，
    /// 各端本地跑同一状态机做表现，节拍闩防快照回卷，敌对弹幕只在权威端生成（owner=255），
    /// 房间坐标经 SendExtraAI 过线，水位/封门 tile 事务只在服务器执行后整块回播。
    /// NPC.damage 每帧归零，仅破水滞空/拽行/突进达速窗内由各端同逻辑抬起。
    /// </summary>
    internal class Undrowned : UndrownedModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 数值（占位初值，验收再调；锚定怨灵 6800/12/36 的 L4 深度档）====================

        /// <summary>基础生命（普通模式，专家/大师原版自乘）：怨灵 ×1.44，对应两层深度差</summary>
        internal const int BaseLife = 9800;
        internal const int BaseDefense = 14;
        /// <summary>接触基伤，仅破水滞空/收锚拽行/拖锚突进达速窗内启用</summary>
        internal const int ContactDamage = 44;

        //弹幕基伤（normal/expert，走 GetAttackDamage_ForProjectiles）
        internal static (float Normal, float Expert) AnchorDamage => (46f, 38f);
        internal static (float Normal, float Expert) WaveDamage => (36f, 30f);
        internal static (float Normal, float Expert) MineDamage => (32f, 26f);
        internal static (float Normal, float Expert) WhirlAnchorDamage => (40f, 34f);

        internal int ScaleDamage((float Normal, float Expert) baseDamage)
            => (int)NPC.GetAttackDamage_ForProjectiles(baseDamage.Normal, baseDamage.Expert);

        //==================== 状态 ====================

        internal const int StateEmerge = 0;
        internal const int StateStalk = 1;
        internal const int StateAnchorThrow = 2;
        internal const int StateTideSlam = 3;
        internal const int StateDragLunge = 4;
        internal const int StateBreach = 5;
        internal const int StateDepthMines = 6;
        internal const int StateWhirl = 7;
        internal const int StateFloodRite = 8;
        internal const int StateDespawn = 9;
        internal const int StateDeath = 10;

        internal int State { get => (int)NPC.ai[0]; private set => NPC.ai[0] = value; }
        private ref float StateTimer => ref NPC.ai[1];
        /// <summary>状态内子参数。掷锚=相位+变体×16；破水=相位+轮次×16+总轮×64；
        /// 涨水仪式=第几次转阶段（1/2）；入场=变体标记</summary>
        private ref float StateParam => ref NPC.ai[2];
        /// <summary>出招轮转计数，分相位手排环用</summary>
        private ref float AttackIndex => ref NPC.ai[3];

        /// <summary>相位：1 陆刑 / 2 半淹 / 3 深水。直接从同步生命值推导，各端一致</summary>
        internal int PhaseIndex
            => NPC.life > NPC.lifeMax * 0.70f ? 1 : NPC.life > NPC.lifeMax * 0.35f ? 2 : 3;

        /// <summary>王座换体入场变体（UndrownedThrone 经 NewNPC ai2 传入）</summary>
        internal const int EmergeVariantThrone = 1;

        //==================== 房间坐标（服务器落场时写入，SendExtraAI 过线；无房=野外测试降级）====================

        /// <summary>房间左上角 tile X；&lt;0 = 无房（野外召唤，跳过一切水位/封门演出）</summary>
        internal int roomOriginX = -1;
        internal int roomOriginY = -1;

        internal bool HasRoom => roomOriginX >= 0;
        internal Point RoomOrigin => new(roomOriginX, roomOriginY);

        public override void SendExtraAI(BinaryWriter writer) {
            //恒定长写入，杜绝流错位
            writer.Write(roomOriginX);
            writer.Write(roomOriginY);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            roomOriginX = reader.ReadInt32();
            roomOriginY = reader.ReadInt32();
        }

        //==================== 时序（全部具名常量）====================

        //王座换体入场：链爆断→首拽纹丝不动→再拽锚离地→直身→面向玩家水面震纹→入战
        private const int EmergeTugFailAt = 24;
        private const int EmergeTugFreeAt = 44;
        private const int EmergeStandAt = 60;
        private const int EmergeFaceAt = 76;
        private const int EmergeTotal = 90;
        //野外测试召唤降级入场（无房）：起身 60f
        private const int EmergePlainTotal = 60;

        //掷锚：过顶蓄力(末段 pow6 猛吸)→锁定拍→出手→嵌墙绷线→收锚自拽→踉跄
        private const int ThrowWindupP1 = 38;
        private const int ThrowWindupFast = 30;
        /// <summary>锁定拍提前量：windup-8 帧起不再追瞄（预告即承诺）</summary>
        private const int ThrowLockLead = 8;
        private const int ThrowFlightMax = 46;
        private const int ThrowEmbedFrames = 40;
        private const int ThrowReelMax = 60;
        private const int ThrowStaggerFrames = 20;
        internal const float MinThrowDistance = 140f;
        /// <summary>链线判定宽=可见链宽（gap 视觉同一性）</summary>
        internal const float ChainLineWidth = 14f;
        //上掷锚（P3 变体）：水下蓄力+气泡柱预告→抛物线压柱顶→锚坠回水底
        private const int UpThrowWindup = 30;
        private const int UpThrowRecover = 24;

        //砸地起浪：双手过顶蓄力(72% 后静默吸气)→砸地→锚拔不出来(最大惩罚窗)→回身
        private const int SlamWindup = 30;
        private const int SlamStuckFrames = 26;
        private const int SlamRecoverFrames = 14;
        internal const float WaveSpeed = 9f;
        internal const float WaveHeight = 48f;

        //拖锚突进：压低身位拖锚拉火→定向直线→尽头刹车
        private const int LungeWindup = 24;
        private const int LungeDashFrames = 10;
        private const int LungeBrakeFrames = 18;
        private const float LungeSpeed = 16f;
        internal const float LungeMinRange = 180f;
        private const float LungeMaxRange = 620f;

        //破水突袭：水下巡游(背鳍波纹=telegraph 实体)→侧翼隆起→抛物线破水→落水僵直
        private const int BreachCruiseMin = 40;
        private const int BreachCruiseTimeout = 120;
        private const int BreachTelegraphFrames = 24;
        private const int BreachRecoverFrames = 22;
        internal const float BreachMinDistance = 120f;
        private const float BreachLaunchVy = -15f;
        private const float BreachGravity = 0.6f;

        //布水雷：定身撒网姿势→雷阵入水→全程定身(自由输出窗)
        private const int MinesPoseFrames = 20;
        private const int MinesStateTotal = 34;

        //锚涡：沉底居中收锚绕头→旋涡 120f→收势眩晕(全场最大输出窗)
        private const int WhirlWindupFrames = 26;
        private const int WhirlSpinFrames = 120;
        private const int WhirlDownFrames = 30;
        internal const float WhirlPull = 0.18f;
        internal const float WhirlPullRadius = 520f;
        internal const float WhirlOrbitRadius = 180f;

        //涨水仪式：立管喷雾警报先行→咆哮→四步阶梯涨水恰停在刻度线→缓升出场
        private const int RiteWarnFrames = 40;
        private const int RiteRoarAt = 44;
        private static readonly int[] RiteStepBeats = [48, 68, 88, 108];
        private const int RiteTotal = 130;
        /// <summary>转阶段后的超长首冷却（招速缓升，防转场即贴脸暴击）</summary>
        private const int PostRiteCooldown = 150;

        //死亡演出：锚脱手插格栅→格栅锈裂(全场唯一大拍)→阶梯泄洪+人被拽向格栅
        //→抓格栅边缘定格→指滑没入→空槽滴水锚立格栅→解封+战利品
        private const int DeathAnchorDropAt = 20;
        private const int DeathCrackAt = 40;
        private static readonly int[] DeathDrainBeats = [44, 58, 72, 86, 100];
        private const int DeathGrabAt = 96;
        private const int DeathSlipAt = 108;
        private const int DeathLootCueAt = 136;
        private const int DeathTotal = 150;

        private const int DespawnTotal = 170;

        /// <summary>感知与脱战距离（房间绑定半径同看守口径）</summary>
        private const float MaxFindDistance = 2600f;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int attackCooldown;
        private Player targetPlayer;
        private int lastSeenState = -1;

        //入场演出闩
        private bool tugFailDone;
        private bool tugFreeDone;
        private bool standDone;
        private bool faceDone;
        //掷锚闩
        private bool throwLockPlayed;
        private bool throwLaunched;
        private Vector2 throwAim;
        //砸地闩
        private bool slamHit;
        //突进闩
        private bool lungeStarted;
        private float lungeDir;
        //破水闩
        private bool breachLeapSet;
        private bool breachSplashed;
        private Vector2 breachAim;
        //水雷闩
        private bool minesLaid;
        //锚涡闩
        private bool whirlCued;
        //仪式闩（涨水步进在服务器逐步闩，客户端音效同拍各自闩）
        private int lastRiteStep = -1;
        private bool riteRoared;
        //死亡闩
        private bool anchorDropped;
        private bool grateCracked;
        private int lastDrainStep = -1;
        private bool slipped;
        private bool lootCued;
        private bool deathDone;
        private Vector2 deathStartPos;
        private int deathSurfaceFrom;
        //脱战闩
        private bool despawnSat;

        /// <summary>本地锚视觉位（入场拖锚/死亡插栅用；战斗中持锚位由状态推导）</summary>
        private Vector2 anchorVisualPos;
        private bool anchorVisualInit;

        //拖影环形缓冲（本地表现）
        private const int TrailLen = 10;
        private readonly Vector2[] trailPos = new Vector2[TrailLen];
        private int trailHead;
        private bool trailInit;

        //躯体帧动画（原版日食鱼人帧序，本地推进；行窗常量待游戏内校正）
        private int bodyFrameTick;
        private int bodyFrameIndex;
        /// <summary>陆相帧窗宽（自帧 0 起循环）</summary>
        private const int WalkFrameSpan = 8;
        /// <summary>水相帧窗宽（自帧表尾部倒数循环）</summary>
        private const int SwimFrameSpan = 5;

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        internal float Seed => NPC.whoAmI * 0.7391f;

        //==================== 色板（尸青 + 锈橙 + 沼靛狱水，对位水牢层绿砖）====================

        internal static readonly Color CorpseTeal = new(102, 128, 120);
        internal static readonly Color CorpseDeep = new(46, 66, 64);
        internal static readonly Color RustOrange = new(170, 120, 86);
        internal static readonly Color RustDeep = new(84, 56, 40);
        internal static readonly Color BogWater = new(58, 96, 104);
        internal static readonly Color BogDeep = new(24, 44, 52);
        internal static readonly Color FoamWhite = new(214, 236, 230);
        internal static readonly Color EyePale = new(172, 220, 202);

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
            NPC.width = 54;
            NPC.height = 92;
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
            NPC.value = Item.buyPrice(0, 8);
            NPC.HitSound = SoundID.NPCHit1 with { Pitch = -0.5f };
            NPC.DeathSound = SoundID.NPCDeath5 with { Pitch = -0.6f };
            //水战曲占位，正式配乐另议
            Music = MusicID.DukeFishron;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.Undrowned.Bestiary"),
            ]);
        }

        public override void BossHeadSlot(ref int index) {
            //暂借猪龙鱼公爵的地图头像（水系狂徒）
            index = NPCID.Sets.BossHeadTextures[NPCID.DukeFishron];
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            //沉锚镣环走 OnKill 服务器逐人定向结算（首杀必掉/复杀 25%），不进公共规则表
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SumpPearl>(), 1, 3, 6));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GaolAnchorWeapon>(), 3));
            npcLoot.Add(ItemDropRule.Common(ItemID.HealingPotion, 1, 5, 10));
        }

        //==================== 全局转移与锁血 ====================

        /// <summary>服务器专属的转阶段闩（客户端相位直接由生命值推导，无需同步）</summary>
        private bool rite1Done;
        private bool rite2Done;

        /// <summary>全局转移，仅服务端驱动；入场/仪式/死亡/脱战中不打断</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient) {
                return;
            }
            if (State is StateEmerge or StateFloodRite or StateDeath or StateDespawn) {
                return;
            }

            //目标失效：他回去坐着
            if (TargetInvalid()) {
                ChangeState(StateDespawn);
                return;
            }

            //70%/35%：清弹涨水仪式（水位=相位表的两次翻页）
            if (!rite1Done && PhaseIndex >= 2) {
                rite1Done = true;
                KillOwnedProjectiles();
                ChangeState(StateFloodRite);
                StateParam = 1;
                return;
            }
            if (!rite2Done && PhaseIndex >= 3) {
                rite2Done = true;
                KillOwnedProjectiles();
                ChangeState(StateFloodRite);
                StateParam = 2;
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
                ChangeState(StateStalk);
            }
        }

        /// <summary>清自家在场弹幕（转阶段/死亡公平阀），仅服务端</summary>
        private void KillOwnedProjectiles() {
            if (VaultUtils.isClient) {
                return;
            }
            int t1 = ModContent.ProjectileType<UndrownedAnchor>();
            int t2 = ModContent.ProjectileType<UndrownedTideWave>();
            int t3 = ModContent.ProjectileType<UndrownedDepthMine>();
            int t4 = ModContent.ProjectileType<UndrownedMineShard>();
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
            //上一场残闩会吞掉新场节拍（锁定拍、砸地拍、泄洪步）
            if (State != lastSeenState) {
                lastSeenState = State;
                throwLockPlayed = false;
                throwLaunched = false;
                slamHit = false;
                lungeStarted = false;
                breachLeapSet = false;
                breachSplashed = false;
                minesLaid = false;
                whirlCued = false;
                lastRiteStep = -1;
                riteRoared = false;
                despawnSat = false;
                if (State == StateDeath) {
                    anchorDropped = false;
                    grateCracked = false;
                    lastDrainStep = -1;
                    slipped = false;
                    lootCued = false;
                    deathStartPos = NPC.Center;
                    deathSurfaceFrom = -1;
                }
            }

            if (!anchorVisualInit) {
                anchorVisualInit = true;
                anchorVisualPos = NPC.Center + new Vector2(40f, 30f);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(); break;
                case StateStalk: UpdateStalk(); break;
                case StateAnchorThrow: UpdateAnchorThrow(); break;
                case StateTideSlam: UpdateTideSlam(); break;
                case StateDragLunge: UpdateDragLunge(); break;
                case StateBreach: UpdateBreach(); break;
                case StateDepthMines: UpdateDepthMines(); break;
                case StateWhirl: UpdateWhirl(); break;
                case StateFloodRite: UpdateFloodRite(); break;
                case StateDespawn: UpdateDespawn(); break;
                case StateDeath: UpdateDeath(); break;
            }

            PushTrail();
            UpdateBodyFrame();
            UpdateAmbientDrip();

            //战斗期压住 Boss 房环境粒子（IMPL-E 客户端演出口，慢节拍续票）
            if (!Main.dedServ && HasRoom && State != StateDespawn && (int)StateTimer % 8 == 0) {
                Rectangle bounds = FloodGalleryRoom.Bounds(RoomOrigin);
                Ambience.AmbientQuiet.Request(new Rectangle(
                    bounds.X * 16, bounds.Y * 16, bounds.Width * 16, bounds.Height * 16), 12);
            }

            if (attackCooldown > 0) {
                attackCooldown--;
            }

            float glow = BodyAlpha() * 0.35f;
            if (glow > 0.02f) {
                Lighting.AddLight(NPC.Center, 0.1f * glow, 0.24f * glow, 0.2f * glow);
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

        /// <summary>目标预判位</summary>
        private Vector2 AimPos(float lead = 6f)
            => targetPlayer == null ? NPC.Center + new Vector2(0f, 300f)
                : targetPlayer.Center + targetPlayer.velocity * lead;

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

        //==================== 水位几何（相位表→世界坐标；无房降级为纯陆战）====================

        /// <summary>当前相位的目标水面世界 Y；无房时返回极大值（永远视为陆上）</summary>
        internal float WaterSurfaceY() {
            if (!HasRoom) {
                return float.MaxValue;
            }
            int rel = PhaseIndex switch {
                1 => FloodGalleryRoom.AnkleSurfaceRel,
                2 => FloodGalleryRoom.Scale1SurfaceRel,
                _ => FloodGalleryRoom.Scale2SurfaceRel,
            };
            return FloodGalleryRoom.SurfaceWorldY(RoomOrigin, rel);
        }

        /// <summary>躯体是否没在水面之下（水相手感/画面判据）</summary>
        internal bool Submerged => NPC.Center.Y > WaterSurfaceY() + 8f;

        private float RoomCenterX => HasRoom
            ? (roomOriginX + FloodGalleryRoom.Width * 0.5f) * 16f
            : NPC.Center.X;

        private float RoomFloorY => HasRoom
            ? (roomOriginY + FloodGalleryRoom.FloorRel) * 16f
            : FindGroundY(NPC.Center);

        //==================== 入场：链爆断，两次拽锚 ====================

        private void UpdateEmerge() {
            int t = (int)StateTimer;
            bool fromThrone = (int)StateParam == EmergeVariantThrone;
            int total = fromThrone ? EmergeTotal : EmergePlainTotal;
            NPC.dontTakeDamage = t < (fromThrone ? EmergeStandAt : 20);

            if (t == 1) {
                NPC.velocity = Vector2.Zero;
                anchorVisualPos = NPC.Center + new Vector2(-FacingSign * 46f, 34f);
            }

            if (fromThrone) {
                //0~24f：链一节节爆断（密度递增的铁响+火花），身体前倾离座
                if (!Main.dedServ && t < EmergeTugFailAt && t % 6 == 1) {
                    SoundEngine.PlaySound(SoundID.Item37 with {
                        Volume = 0.35f + t * 0.01f,
                        Pitch = -0.6f + t * 0.02f,
                        MaxInstances = 3
                    }, NPC.Center);
                    PRTLoader.NewParticle<PRT_Spark>(
                        NPC.Center + Main.rand.NextVector2Circular(20f, 26f),
                        Main.rand.NextVector2Circular(1.8f, 1.8f),
                        Color.Lerp(RustOrange, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(8, 14));
                }

                if (!tugFailDone && t >= EmergeTugFailAt) {
                    //第一次拽：锚纹丝不动（重量叙事），闷响一声
                    tugFailDone = true;
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, anchorVisualPos);
                    NPC.velocity.X = -FacingSign * 1.2f;
                }

                if (!tugFreeDone && t >= EmergeTugFreeAt) {
                    //第二次全身后仰猛拽：锚离地拖出石沟，尘+火花
                    tugFreeDone = true;
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, anchorVisualPos);
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.5f, MaxInstances = 2 }, anchorVisualPos);
                    ShakeNearby(2f);
                    if (!Main.dedServ) {
                        for (int k = 0; k < 8; k++) {
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(anchorVisualPos + Main.rand.NextVector2Circular(14f, 8f),
                                new Vector2(-FacingSign * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(1f, 3f)),
                                Color.Lerp(RustDeep, CorpseDeep, Main.rand.NextFloat(0.5f)),
                                Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 28), 0f);
                        }
                    }
                }

                if (tugFreeDone) {
                    //锚被拖到脚边
                    anchorVisualPos = Vector2.Lerp(anchorVisualPos,
                        NPC.Center + new Vector2(-FacingSign * 40f, NPC.height * 0.5f - 8f), 0.12f);
                }

                if (!standDone && t >= EmergeStandAt) {
                    //直起身：颈椎逐段咔（帧抖动在绘制层）
                    standDone = true;
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 2 }, NPC.Center);
                }

                if (!faceDone && t >= EmergeFaceAt) {
                    //面向最近玩家，踝水整面同心震纹（水是他的），低吼
                    faceDone = true;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.6f, Pitch = -0.55f, MaxInstances = 1 }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 1 }, NPC.Center);
                    //入场重音压声床（IMPL-E 客户端演出口，距离门自守）
                    if (!Main.dedServ && Main.LocalPlayer.Alives()
                        && Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1500f) {
                        Ambience.DungeonworldAmbience.PushStinger(
                            SoundID.Drown with { Volume = 0.6f, Pitch = -0.85f }, 0.4f);
                    }
                    ShakeNearby(2.5f);
                    if (!Main.dedServ) {
                        float waterY = HasRoom
                            ? FloodGalleryRoom.SurfaceWorldY(RoomOrigin, FloodGalleryRoom.AnkleSurfaceRel)
                            : NPC.Center.Y + NPC.height * 0.5f;
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_DWave>(new Vector2(NPC.Center.X, waterY + 4f),
                                Vector2.Zero, BogWater, 0.05f + k * 0.02f)
                                ?.Configure(new Vector2(1f, 0.24f), 0f, 0.3f + k * 0.06f, 10 + k * 3);
                        }
                    }
                }

                //身体角度：前倾→猛拽后仰→回正
                float wantRot = t < EmergeTugFailAt ? 0.2f
                    : t < EmergeTugFreeAt ? 0.12f
                    : t < EmergeStandAt ? -0.22f : 0f;
                NPC.rotation = NPC.rotation.AngleLerp(wantRot * FacingSign, 0.12f);
                NPC.velocity *= 0.9f;
            }
            else {
                //野外降级入场：原地起身 + 尘雾
                NPC.velocity *= 0.9f;
                NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);
                if (!Main.dedServ && t % 4 == 1 && t < 40) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        NPC.Center + Main.rand.NextVector2Circular(26f, 34f),
                        new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                        BogDeep * 0.7f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(30, 50));
                }
            }

            if (t >= total) {
                attackCooldown = 45;
                if (!VaultUtils.isClient) {
                    ChangeState(StateStalk);
                }
            }
        }

        //==================== 连接态：拖锚喘息 / 水下巡游 + 选招 ====================

        private void UpdateStalk() {
            if (targetPlayer == null) {
                NPC.velocity *= 0.95f;
                return;
            }

            float surfaceY = WaterSurfaceY();
            bool waterPhase = PhaseIndex >= 2 && HasRoom;

            if (!waterPhase) {
                //陆刑：拖锚缓行（3px/f 的重物），贴地
                float groundY = FindGroundY(NPC.Center - new Vector2(0f, 40f));
                float wantX = MathHelper.Clamp((targetPlayer.Center.X - NPC.Center.X) * 0.03f, -3f, 3f);
                NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, wantX, 0.08f);
                float feetTarget = groundY - NPC.height * 0.5f + 4f;
                NPC.velocity.Y = MathHelper.Clamp((feetTarget - NPC.Center.Y) * 0.2f, -4f, 6f);
                NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.02f, 0.1f);
                //拖锚火花：走动时锚在地上刮
                if (!Main.dedServ && Math.Abs(NPC.velocity.X) > 0.8f && (int)StateTimer % 9 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        NPC.Center + new Vector2(-FacingSign * 44f, NPC.height * 0.5f - 4f),
                        new Vector2(-NPC.velocity.X * 0.4f, -Main.rand.NextFloat(0.4f, 1.2f)),
                        Color.Lerp(RustOrange, Color.White, 0.3f),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(8, 14));
                }
            }
            else {
                //半淹/深水：水下巡游（22px/f 的鲨鱼），锚收在背后
                Vector2 anchor = targetPlayer.Center + new Vector2(0f, 90f);
                anchor.Y = MathHelper.Clamp(anchor.Y, surfaceY + 70f, RoomFloorY - 50f);
                Vector2 desired = (anchor - NPC.Center) * 0.06f;
                if (desired.Length() > 22f) {
                    desired = desired.SafeNormalize(Vector2.Zero) * 22f;
                }
                NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.09f);
                NPC.rotation = NPC.rotation.AngleLerp(
                    MathHelper.Clamp(NPC.velocity.X * 0.03f, -0.4f, 0.4f), 0.12f);
                SpawnWakeRipples(surfaceY);
            }

            //出招裁决只在服务器，结果乘 ai 槽 + netUpdate 过线
            if (VaultUtils.isClient || attackCooldown > 0 || StateTimer <= 24 || TargetInvalid()) {
                return;
            }
            PickNextAttack();
        }

        /// <summary>分相位手排环 + 招前公平阀（距离不合的招跳位不硬发）</summary>
        private void PickNextAttack() {
            float dist = Vector2.Distance(NPC.Center, targetPlayer.Center);
            for (int guard = 0; guard < 4; guard++) {
                AttackIndex++;
                int idx = (int)AttackIndex;
                int pick;
                int param = 0;
                if (PhaseIndex == 1) {
                    //P1 陆刑：掷锚 → 砸地起浪 → 拖锚突进
                    pick = (idx % 3) switch {
                        0 => StateAnchorThrow,
                        1 => StateTideSlam,
                        _ => StateDragLunge,
                    };
                }
                else if (PhaseIndex == 2) {
                    //P2 半淹：破水×2 → 掷锚(快) → 布雷(4) → 破水×2 → 水面砸浪 → 掷锚(快)
                    switch (idx % 6) {
                        case 0: pick = StateBreach; param = 2 * 64; break;
                        case 1: pick = StateAnchorThrow; break;
                        case 2: pick = StateDepthMines; break;
                        case 3: pick = StateBreach; param = 2 * 64; break;
                        case 4: pick = StateTideSlam; param = 1; break;
                        default: pick = StateAnchorThrow; break;
                    }
                }
                else {
                    //P3 深水：破水×3 → 布雷(6) → 锚涡 → 上掷锚 → 破水×2 → 布雷(6)
                    switch (idx % 6) {
                        case 0: pick = StateBreach; param = 3 * 64; break;
                        case 1: pick = StateDepthMines; break;
                        case 2: pick = StateWhirl; break;
                        case 3: pick = StateAnchorThrow; param = 16; break;
                        case 4: pick = StateBreach; param = 2 * 64; break;
                        default: pick = StateDepthMines; break;
                    }
                }

                //公平阀：贴脸不掷锚、距离不合不突进（跳位到环上下一招）
                if (pick == StateAnchorThrow && param < 16 && dist < MinThrowDistance) {
                    continue;
                }
                if (pick == StateDragLunge && (dist < LungeMinRange || dist > LungeMaxRange)) {
                    continue;
                }
                ChangeState(pick);
                StateParam = param;
                return;
            }
            //四连跳位不中：拖一拍再选（不空转出招）
            attackCooldown = 30;
        }

        private int PhaseCooldown() => PhaseIndex switch {
            1 => 90 + (int)(Seed * 7f) % 20,
            2 => 75,
            _ => 60,
        };

        /// <summary>水下巡游的背鳍波纹（表面 V 形波+气泡列=telegraph 实体，客户端）</summary>
        private void SpawnWakeRipples(float surfaceY) {
            if (Main.dedServ || !Submerged || (int)StateTimer % 4 != 0) {
                return;
            }
            Vector2 at = new(NPC.Center.X, surfaceY + 2f);
            PRTLoader.NewParticle<PRT_SumpSpray>(at,
                new Vector2(NPC.velocity.X * 0.16f + Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.6f)),
                Color.Lerp(BogWater, FoamWhite, Main.rand.NextFloat(0.3f, 0.7f)),
                Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
        }

        //==================== 掷锚（含 P3 上掷锚变体）====================

        private int ThrowPhase => (int)StateParam % 16;
        private bool IsUpThrow => (int)StateParam / 16 % 4 == 1;
        private int ThrowWindup => IsUpThrow ? UpThrowWindup
            : PhaseIndex >= 2 ? ThrowWindupFast : ThrowWindupP1;

        private void NextThrowPhase(int next) {
            StateParam = next + (IsUpThrow ? 16 : 0);
            StateTimer = 0;
            NPC.netUpdate = !VaultUtils.isClient;
        }

        private void UpdateAnchorThrow() {
            int t = (int)StateTimer;
            int phase = ThrowPhase;
            int windup = ThrowWindup;
            float surfaceY = WaterSurfaceY();

            if (phase == 0) {
                //蓄力：陆相定身高举，水相浮到水面亮出上身；末段 pow6 猛吸
                if (targetPlayer == null) {
                    EndAttack(45);
                    return;
                }
                if (PhaseIndex >= 2 && HasRoom && !IsUpThrow) {
                    float wantY = surfaceY - 20f;
                    NPC.velocity.Y = MathHelper.Clamp((wantY - NPC.Center.Y) * 0.12f, -8f, 8f);
                    NPC.velocity.X *= 0.85f;
                }
                else {
                    NPC.velocity *= 0.82f;
                }
                NPC.rotation = NPC.rotation.AngleLerp(-FacingSign * 0.1f, 0.15f);

                //锁定拍：链绷直一响，此后不再追瞄（预告即承诺）
                if (t >= windup - ThrowLockLead) {
                    if (!throwLockPlayed) {
                        throwLockPlayed = true;
                        throwAim = IsUpThrow ? PickPillarTop() : AimPos(8f);
                        SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.15f, MaxInstances = 2 }, NPC.Center);
                    }
                }
                else if (!Main.dedServ && t % 3 == 0) {
                    //蓄力期水花向心汇聚
                    Vector2 hand = NPC.Center + new Vector2(0f, -46f);
                    Vector2 from = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 70f);
                    PRTLoader.NewParticle<PRT_SumpSpray>(from, (hand - from) * 0.12f,
                        BogWater * 0.8f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
                }
                //上掷锚：目标柱下方水面气泡柱预告（预告柱=承诺柱）
                if (IsUpThrow && throwLockPlayed && !Main.dedServ && t % 2 == 0 && HasRoom) {
                    PRTLoader.NewParticle<PRT_SumpSpray>(
                        new Vector2(throwAim.X + Main.rand.NextFloat(-24f, 24f), surfaceY + Main.rand.NextFloat(0f, 30f)),
                        new Vector2(0f, -Main.rand.NextFloat(2f, 4f)),
                        FoamWhite * 0.8f, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 20));
                }

                if (t >= windup) {
                    //出手拍闩：快照回卷不重放出手音效/反冲（弹幕生成本就只在服务器）
                    if (!throwLaunched) {
                        throwLaunched = true;
                        LaunchAnchor();
                    }
                    NextThrowPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //飞行/嵌墙绷线：锚已锁点，链线是静态几何，站离线即安全
                NPC.velocity *= 0.9f;
                Projectile anchor = FindMyAnchor();
                bool embedded = anchor != null && (int)anchor.ai[0] == 1;
                if (IsUpThrow) {
                    //上掷锚不收拽：锚坠回即收招
                    if (anchor == null || t > ThrowFlightMax + 60) {
                        NextThrowPhase(3);
                    }
                    return;
                }
                if (embedded && t > ThrowFlightMax) {
                    //嵌墙后绷线计时（伤害线窗口在锚弹幕侧门控）
                    if (t > ThrowFlightMax + ThrowEmbedFrames) {
                        NextThrowPhase(2);
                    }
                }
                else if (anchor == null) {
                    //锚意外没了（清弹/越界）：直接踉跄收招
                    NextThrowPhase(3);
                }
                return;
            }

            if (phase == 2) {
                //收锚自拽：全身后倾 12f，然后沿链直线拽行（接触窗 |v|>14 抬起）
                Projectile anchor = FindMyAnchor();
                if (anchor == null || t > ThrowReelMax) {
                    NextThrowPhase(3);
                    return;
                }
                if (t < 12) {
                    NPC.velocity *= 0.85f;
                    NPC.rotation = NPC.rotation.AngleLerp(FacingSign * 0.16f, 0.2f);
                    if (t == 4) {
                        SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.35f, MaxInstances = 2 }, NPC.Center);
                    }
                    return;
                }
                Vector2 to = anchor.Center - NPC.Center;
                if (to.Length() < 48f) {
                    NextThrowPhase(3);
                    return;
                }
                Vector2 want = to.SafeNormalize(Vector2.UnitX) * 18f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.16f);
                if (NPC.velocity.Length() > 14f) {
                    NPC.damage = ContactDamage;
                }
                NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.02f, 0.15f);
                return;
            }

            //踉跄收招（重量在刹车里）
            NPC.velocity *= 0.82f;
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.1f);
            int recover = IsUpThrow ? UpThrowRecover : ThrowStaggerFrames;
            if (t >= recover) {
                EndAttack(PhaseCooldown());
            }
        }

        /// <summary>服务器出锚：模式/伤害随 spawn 包原子过线，事后不补参数</summary>
        private void LaunchAnchor() {
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
            NPC.velocity -= (throwAim - NPC.Center).SafeNormalize(Vector2.UnitX) * 3f;
            ShakeNearby(1.2f);

            if (VaultUtils.isClient) {
                return;
            }
            Vector2 hand = NPC.Center + new Vector2(FacingSign * 10f, -40f);
            Vector2 vel;
            float damage;
            if (IsUpThrow) {
                //抛物线压柱顶：飞行 36f 的定解（弹幕侧同重力常数复算轨迹）
                const float flightT = 36f;
                Vector2 d = throwAim - hand;
                vel = new Vector2(d.X / flightT, d.Y / flightT - 0.5f * BreachGravity * flightT);
                damage = ScaleDamage(AnchorDamage);
            }
            else {
                vel = (throwAim - hand).SafeNormalize(Vector2.UnitX) * 22f;
                vel.Y -= 1.6f;
                damage = ScaleDamage(AnchorDamage);
            }
            Projectile.NewProjectile(NPC.GetSource_FromAI(), hand, vel,
                ModContent.ProjectileType<UndrownedAnchor>(), (int)damage, 4f,
                Main.myPlayer, 0f, NPC.whoAmI, IsUpThrow ? 1f : 0f);
        }

        /// <summary>找自己的掷出锚（排除涡轨锚；各端对同步弹幕数组扫描，结果一致）</summary>
        internal Projectile FindMyAnchor() => FindAnchorCore(includeOrbit: false);

        /// <summary>找任意在场自锚（含涡轨，绘制持锚判定用）</summary>
        internal Projectile FindMyAnchorAny() => FindAnchorCore(includeOrbit: true);

        private Projectile FindAnchorCore(bool includeOrbit) {
            int type = ModContent.ProjectileType<UndrownedAnchor>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[1] == NPC.whoAmI
                    && (includeOrbit || (int)p.ai[2] != UndrownedAnchor.ModeOrbit)) {
                    return p;
                }
            }
            return null;
        }

        /// <summary>P3 上掷锚锁柱：锁离目标玩家最近的柱顶（预告柱=承诺柱，单发只锁一座）</summary>
        private Vector2 PickPillarTop() {
            if (!HasRoom || targetPlayer == null) {
                return AimPos();
            }
            float leftX = (roomOriginX + FloodGalleryRoom.PillarLeftCol + FloodGalleryRoom.PillarWidth * 0.5f) * 16f;
            float rightX = (roomOriginX + FloodGalleryRoom.PillarRightCol + FloodGalleryRoom.PillarWidth * 0.5f) * 16f;
            float topY = (roomOriginY + FloodGalleryRoom.PillarTopRel) * 16f - 10f;
            bool left = Math.Abs(targetPlayer.Center.X - leftX) < Math.Abs(targetPlayer.Center.X - rightX);
            return new Vector2(left ? leftX : rightX, topY);
        }

        //==================== 砸地起浪 ====================

        private bool SlamOnSurface => (int)StateParam == 1;

        private void UpdateTideSlam() {
            int t = (int)StateTimer;
            float surfaceY = WaterSurfaceY();

            if (t <= SlamWindup) {
                //蓄力：双手过顶，水花向心汇聚，72% 后静默（吸气拍）
                if (SlamOnSurface && HasRoom) {
                    float wantY = surfaceY - 14f;
                    NPC.velocity.Y = MathHelper.Clamp((wantY - NPC.Center.Y) * 0.12f, -8f, 8f);
                    NPC.velocity.X *= 0.85f;
                }
                else {
                    NPC.velocity *= 0.8f;
                }
                NPC.rotation = NPC.rotation.AngleLerp(0f, 0.15f);
                float k = t / (float)SlamWindup;
                if (!Main.dedServ && k < 0.72f && t % 3 == 0) {
                    Vector2 hands = NPC.Center + new Vector2(0f, -54f);
                    Vector2 from = hands + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 80f);
                    PRTLoader.NewParticle<PRT_SumpSpray>(from, (hands - from) * 0.13f,
                        Color.Lerp(BogWater, FoamWhite, 0.4f), Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(10, 18));
                }
                if (t == (int)(SlamWindup * 0.72f)) {
                    SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 2 }, NPC.Center);
                }
                return;
            }

            if (!slamHit) {
                //砸地拍：锚砸进地板/水面，双向各一道浪
                slamHit = true;
                float lineY = SlamOnSurface && HasRoom ? surfaceY : FindGroundY(NPC.Center) - 6f;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.8f, Pitch = -0.4f, MaxInstances = 2 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, Pitch = -0.3f, MaxInstances = 2 }, NPC.Center);
                ShakeNearby(3f);
                if (!Main.dedServ) {
                    for (int k = 0; k < 10; k++) {
                        PRTLoader.NewParticle<PRT_SumpSpray>(
                            new Vector2(NPC.Center.X + Main.rand.NextFloat(-20f, 20f), lineY - 4f),
                            new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(2f, 6f)),
                            Color.Lerp(BogWater, FoamWhite, Main.rand.NextFloat(0.6f)),
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 28));
                    }
                    PRTLoader.NewParticle<PRT_DWave>(new Vector2(NPC.Center.X, lineY), Vector2.Zero,
                        BogWater, 0.07f)?.Configure(new Vector2(1f, 0.3f), 0f, 0.26f, 10);
                }
                if (!VaultUtils.isClient) {
                    int damage = ScaleDamage(WaveDamage);
                    for (int dir = -1; dir <= 1; dir += 2) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(),
                            new Vector2(NPC.Center.X + dir * 30f, lineY - WaveHeight * 0.5f),
                            new Vector2(dir * WaveSpeed, 0f),
                            ModContent.ProjectileType<UndrownedTideWave>(), damage, 3f,
                            Main.myPlayer, dir, NPC.whoAmI, lineY);
                    }
                }
            }

            //锚砸进地板拔不出来（最大惩罚窗），随后回身
            NPC.velocity *= 0.85f;
            NPC.rotation = NPC.rotation.AngleLerp(FacingSign * 0.18f, 0.1f);
            if (t >= SlamWindup + SlamStuckFrames + SlamRecoverFrames) {
                EndAttack(PhaseCooldown());
            }
        }

        //==================== 拖锚突进 ====================

        private void UpdateDragLunge() {
            int t = (int)StateTimer;

            if (t == 1) {
                //发动即定向不转向（承诺锁定）
                lungeDir = FacingSign;
            }

            if (t <= LungeWindup) {
                //压低身位，锚拖后拉出火花沟
                NPC.velocity *= 0.8f;
                NPC.rotation = NPC.rotation.AngleLerp(lungeDir * 0.12f, 0.2f);
                if (!Main.dedServ && t % 3 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        NPC.Center + new Vector2(-lungeDir * 48f, NPC.height * 0.5f - 4f),
                        new Vector2(-lungeDir * Main.rand.NextFloat(1f, 2.4f), -Main.rand.NextFloat(0.5f, 1.6f)),
                        Color.Lerp(RustOrange, Color.White, 0.4f),
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(8, 14));
                }
                if (t == LungeWindup) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 2 }, NPC.Center);
                }
                return;
            }

            if (t <= LungeWindup + LungeDashFrames) {
                //定向直线：达速窗内接触伤害
                if (!lungeStarted) {
                    lungeStarted = true;
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 2 }, NPC.Center);
                }
                NPC.velocity.X = lungeDir * LungeSpeed;
                NPC.velocity.Y *= 0.8f;
                if (Math.Abs(NPC.velocity.X) > 14f) {
                    NPC.damage = ContactDamage;
                }
                NPC.rotation = lungeDir * 0.1f;
                return;
            }

            //尽头刹车（重量拍）
            NPC.velocity.X *= 0.8f;
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.12f);
            if (t >= LungeWindup + LungeDashFrames + LungeBrakeFrames) {
                EndAttack(PhaseCooldown());
            }
        }

        //==================== 破水突袭 ====================

        private int BreachPhase => (int)StateParam % 16;
        private int BreachRound => (int)StateParam / 16 % 4;
        private int BreachTotalRounds => Math.Max(1, (int)StateParam / 64);

        private void NextBreachPhase(int phase, int round) {
            StateParam = phase + round * 16 + BreachTotalRounds * 64;
            StateTimer = 0;
            NPC.netUpdate = !VaultUtils.isClient;
        }

        private void UpdateBreach() {
            int t = (int)StateTimer;
            float surfaceY = WaterSurfaceY();
            if (!HasRoom || PhaseIndex < 2 || targetPlayer == null) {
                //无水降级：本招直接让位（不做无水鲨鱼）
                EndAttack(45);
                return;
            }
            int phase = BreachPhase;

            if (phase == 0) {
                //水下巡游到侧翼（背鳍波纹全程可见=telegraph 实体）
                float flankX = targetPlayer.Center.X
                    + MathF.Sign(NPC.Center.X - targetPlayer.Center.X + 0.01f) * 240f;
                flankX = MathHelper.Clamp(flankX, RoomCenterX - 480f, RoomCenterX + 480f);
                Vector2 want = new(flankX, MathHelper.Clamp(surfaceY + 90f, surfaceY + 60f, RoomFloorY - 40f));
                Vector2 desired = (want - NPC.Center) * 0.08f;
                if (desired.Length() > 22f) {
                    desired = desired.SafeNormalize(Vector2.Zero) * 22f;
                }
                NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.12f);
                NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.03f, 0.12f);
                SpawnWakeRipples(surfaceY);

                bool flanked = Math.Abs(NPC.Center.X - targetPlayer.Center.X) >= BreachMinDistance
                    && Math.Abs(NPC.Center.X - want.X) < 60f;
                if ((t >= BreachCruiseMin && flanked) || t >= BreachCruiseTimeout) {
                    NextBreachPhase(1, BreachRound);
                }
                return;
            }

            if (phase == 1) {
                //隆起预告：出现即瞄准锁死（承诺）；不在玩家脚下 120px 内起跳
                if (t == 1) {
                    breachAim = AimPos(10f);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 2 },
                        new Vector2(NPC.Center.X, surfaceY));
                }
                NPC.velocity *= 0.86f;
                NPC.velocity.Y += 0.06f;
                if (!Main.dedServ && t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_SumpSpray>(
                        new Vector2(NPC.Center.X + Main.rand.NextFloat(-26f, 26f), surfaceY + Main.rand.NextFloat(-2f, 6f)),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.5f, 3.5f)),
                        FoamWhite * 0.85f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(10, 18));
                }
                if (t >= BreachTelegraphFrames) {
                    NextBreachPhase(2, BreachRound);
                }
                return;
            }

            if (phase == 2) {
                //破水跃出：固定抛物线（起跳点可读后弧顶必然可算），滞空=接触窗
                if (!breachLeapSet) {
                    breachLeapSet = true;
                    float dx = breachAim.X - NPC.Center.X;
                    float flightT = MathF.Abs(BreachLaunchVy) * 2f / BreachGravity;
                    NPC.velocity = new Vector2(MathHelper.Clamp(dx / flightT, -14f, 14f), BreachLaunchVy);
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.9f, Pitch = 0.1f, MaxInstances = 2 }, NPC.Center);
                    ShakeNearby(1.6f);
                    if (!Main.dedServ) {
                        for (int k = 0; k < 12; k++) {
                            PRTLoader.NewParticle<PRT_SumpSpray>(
                                new Vector2(NPC.Center.X + Main.rand.NextFloat(-20f, 20f), surfaceY),
                                new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 8f)),
                                Color.Lerp(BogWater, FoamWhite, Main.rand.NextFloat(0.7f)),
                                Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(18, 30));
                        }
                    }
                }
                NPC.velocity.Y += BreachGravity;
                if (NPC.velocity.Length() > 10f) {
                    NPC.damage = ContactDamage;
                }
                NPC.rotation = NPC.velocity.ToRotation() * 0.35f * MathF.Sign(NPC.velocity.X + 0.01f);
                //回落穿过水面：入水收招
                if (NPC.velocity.Y > 0f && NPC.Center.Y > surfaceY + 30f) {
                    NextBreachPhase(3, BreachRound);
                }
                //保底出口：超时也收
                if (t > 120) {
                    NextBreachPhase(3, BreachRound);
                }
                return;
            }

            //落水僵直（水面大水花=收招拍），连段间隔 ≥40f 巡游
            if (!breachSplashed) {
                breachSplashed = true;
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, Pitch = -0.5f, MaxInstances = 2 },
                    new Vector2(NPC.Center.X, surfaceY));
                if (!Main.dedServ) {
                    for (int k = 0; k < 8; k++) {
                        PRTLoader.NewParticle<PRT_SumpSpray>(
                            new Vector2(NPC.Center.X + Main.rand.NextFloat(-18f, 18f), surfaceY),
                            new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 5f)),
                            FoamWhite * 0.8f, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(14, 24));
                    }
                }
            }
            NPC.velocity *= 0.88f;
            if (t >= BreachRecoverFrames) {
                breachLeapSet = false;
                breachSplashed = false;
                int round = BreachRound + 1;
                if (round < BreachTotalRounds) {
                    NextBreachPhase(0, round);
                }
                else {
                    EndAttack(PhaseCooldown());
                }
            }
        }

        //==================== 布水雷 ====================

        private void UpdateDepthMines() {
            int t = (int)StateTimer;
            float surfaceY = WaterSurfaceY();
            if (!HasRoom || PhaseIndex < 2) {
                EndAttack(45);
                return;
            }

            //定身摆撒网姿势（全程定身=自由输出窗）
            NPC.velocity *= 0.85f;
            NPC.rotation = NPC.rotation.AngleLerp(MathF.Sin(t * 0.3f + Seed) * 0.06f, 0.2f);

            if (t == 2) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
            }

            if (!minesLaid && t >= MinesPoseFrames) {
                minesLaid = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.6f, MaxInstances = 2 }, NPC.Center);
                if (!VaultUtils.isClient) {
                    int count = PhaseIndex >= 3 ? 6 : 4;
                    int damage = ScaleDamage(MineDamage);
                    for (int i = 0; i < count; i++) {
                        //横向扇布：错相引爆拍随 spawn 包原子过线（出厂后不补参数）
                        float dx = (i - (count - 1) * 0.5f) * 130f + Main.rand.NextFloat(-18f, 18f);
                        float x = MathHelper.Clamp(NPC.Center.X + dx, RoomCenterX - 500f, RoomCenterX + 500f);
                        float y = surfaceY + 90f + i % 2 * 56f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, y),
                            new Vector2(0f, -0.4f),
                            ModContent.ProjectileType<UndrownedDepthMine>(), damage, 2f,
                            Main.myPlayer, i, NPC.whoAmI);
                    }
                }
            }

            if (t >= MinesStateTotal) {
                EndAttack(PhaseCooldown());
            }
        }

        //==================== 锚涡（P3）====================

        /// <summary>涡轨锚的出手拍（锚弹幕以 boss 同步计时推导轨道，时间线必须连续不重置）</summary>
        internal const int WhirlAnchorSpawnAt = 18;

        private void UpdateWhirl() {
            //连续单时间线（不重置 StateTimer：涡轨锚的角度/半径由 ai[1] 推导，
            //重置会让远端轨道回卷跳位）：0~26 沉底收锚 / ~146 旋涡 / ~176 收势眩晕
            int t = (int)StateTimer;
            float surfaceY = WaterSurfaceY();
            if (!HasRoom || PhaseIndex < 3) {
                EndAttack(45);
                return;
            }

            if (t <= WhirlWindupFrames) {
                //沉底居中收锚绕头，水面出现旋纹
                Vector2 want = new(RoomCenterX, RoomFloorY - 70f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (want - NPC.Center) * 0.1f, 0.14f);
                if (!whirlCued && t > 4) {
                    whirlCued = true;
                    SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.7f, Pitch = -0.7f, MaxInstances = 1 }, NPC.Center);
                }
                if (t == WhirlAnchorSpawnAt && !VaultUtils.isClient) {
                    //涡轨锚：服务器生成，轨道参数随 spawn 包过线
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<UndrownedAnchor>(), ScaleDamage(WhirlAnchorDamage), 3f,
                        Main.myPlayer, 0f, NPC.whoAmI, 2f);
                }
                return;
            }

            if (t <= WhirlWindupFrames + WhirlSpinFrames) {
                //旋涡：拉力只作用于水中玩家且只推本机玩家（受害端本地裁决，服务器不推人）
                NPC.velocity *= 0.9f;
                NPC.rotation += 0.05f;
                if (!Main.dedServ) {
                    Player lp = Main.LocalPlayer;
                    if (lp.active && !lp.dead && lp.wet
                        && Vector2.Distance(lp.Center, NPC.Center) < WhirlPullRadius) {
                        Vector2 pull = (NPC.Center - lp.Center).SafeNormalize(Vector2.Zero) * WhirlPull;
                        lp.velocity += pull;
                    }
                    //水面旋纹（表现）
                    if (t % 5 == 0) {
                        float ang = t * 0.23f + Seed;
                        PRTLoader.NewParticle<PRT_SumpSpray>(
                            new Vector2(NPC.Center.X + MathF.Cos(ang) * 120f, surfaceY + 2f),
                            new Vector2(-MathF.Sin(ang) * 2.4f, -0.6f),
                            BogWater * 0.9f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 20));
                    }
                }
                return;
            }

            //收势眩晕（全场最大输出窗）
            NPC.velocity *= 0.9f;
            NPC.rotation = NPC.rotation.AngleLerp(0f, 0.08f);
            if (t >= WhirlWindupFrames + WhirlSpinFrames + WhirlDownFrames) {
                EndAttack(PhaseCooldown());
            }
        }

        //==================== 涨水仪式（转阶段）====================

        /// <summary>本次仪式的阶梯水面行（四步恰停在下一道刻度线：刻度=承诺）</summary>
        private static int RiteStepSurface(int riteIndex, int step) {
            return riteIndex == 1
                ? step switch { 0 => 38, 1 => 35, 2 => 32, _ => FloodGalleryRoom.Scale1SurfaceRel }
                : step switch { 0 => 26, 1 => 22, 2 => 19, _ => FloodGalleryRoom.Scale2SurfaceRel };
        }

        private void UpdateFloodRite() {
            int t = (int)StateTimer;
            int rite = Math.Max(1, (int)StateParam);
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.9f;

            //立管喷雾+警报先行 40f（预告在前）
            if (t < RiteWarnFrames) {
                if (t == 6 || t == 22 || t == 38) {
                    SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.7f, Pitch = -0.6f + t * 0.008f, MaxInstances = 2 }, NPC.Center);
                }
                if (!Main.dedServ && HasRoom && t % 3 == 0) {
                    SpawnPipeSpray();
                }
                return;
            }

            if (!riteRoared && t >= RiteRoarAt) {
                //咆哮定身（撕开立管封链），第二次变调
                riteRoared = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.75f, Pitch = rite == 1 ? -0.45f : -0.2f, MaxInstances = 1 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = 0.3f, MaxInstances = 2 }, NPC.Center);
                //相位切换的短屏幕色倾（IMPL-E 客户端演出口，距离门自守）
                if (!Main.dedServ && Main.LocalPlayer.Alives()
                    && Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1500f) {
                    Ambience.DungeonworldAmbience.PushGradePulse(BogWater, 0.35f, 40);
                }
                ShakeNearby(2.5f);
            }

            //阶梯涨水：服务器一次性 tile 事务 ×4（无逐帧写入），客户端同拍演水声
            for (int step = 0; step < RiteStepBeats.Length; step++) {
                if (t == RiteStepBeats[step] && lastRiteStep < step) {
                    lastRiteStep = step;
                    if (!VaultUtils.isClient && HasRoom) {
                        FloodGalleryWatcher.ApplyWater(RoomOrigin, RiteStepSurface(rite, step));
                    }
                    SoundEngine.PlaySound(SoundID.Splash with {
                        Volume = 0.6f + step * 0.08f,
                        Pitch = -0.5f + step * 0.12f,
                        MaxInstances = 2
                    }, NPC.Center);
                    ShakeNearby(1f + step * 0.3f);
                }
            }
            if (!Main.dedServ && HasRoom && t % 2 == 0) {
                SpawnPipeSpray();
            }

            //涨水期玩家浮力自然抬升、全程零伤害；仪式尾声回到连接态并给超长首冷却
            if (t >= RiteTotal) {
                attackCooldown = PostRiteCooldown;
                if (!VaultUtils.isClient) {
                    ChangeState(StateStalk);
                }
            }
        }

        /// <summary>立管喷雾（两根立管的墙面水口，客户端表现）</summary>
        private void SpawnPipeSpray() {
            for (int side = 0; side < 2; side++) {
                int col = side == 0 ? FloodGalleryRoom.PipeLeftCol : FloodGalleryRoom.PipeRightCol;
                float x = (roomOriginX + col + 1f) * 16f;
                float y = (roomOriginY + FloodGalleryRoom.PipeBottomRel - Main.rand.Next(0, 18)) * 16f;
                PRTLoader.NewParticle<PRT_SumpSpray>(new Vector2(x, y),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(1f, 3f)),
                    Color.Lerp(FoamWhite, BogWater, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(12, 22));
            }
        }

        //==================== 脱战撤离：涉水回龛坐下 ====================

        private void UpdateDespawn() {
            int t = (int)StateTimer;
            NPC.dontTakeDamage = t > 20;

            if (HasRoom && !despawnSat) {
                //收锚上肩，走回王座
                Vector2 throne = FloodGalleryRoom.ThroneWorldPos(RoomOrigin) + new Vector2(0f, -20f);
                Vector2 to = throne - NPC.Center;
                if (to.Length() < 30f || t > 100) {
                    despawnSat = true;
                    NPC.velocity = Vector2.Zero;
                }
                else {
                    NPC.velocity = Vector2.Lerp(NPC.velocity, to.SafeNormalize(Vector2.Zero) * 6f, 0.1f);
                }
            }
            else {
                NPC.velocity *= 0.92f;
            }

            if (!Main.dedServ && t % 3 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + Main.rand.NextVector2Circular(24f, 34f),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                    BogDeep * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(30, 50));
            }

            if (t > DespawnTotal - 20) {
                NPC.EncourageDespawn(10);
            }
            //目标回场就收势归队
            if (!VaultUtils.isClient && !TargetInvalid() && t < 50) {
                attackCooldown = 60;
                ChangeState(StateStalk);
            }
        }

        //==================== 死亡演出：格栅锈裂，整槽泄洪 ====================

        private void UpdateDeath() {
            int t = (int)StateTimer;
            NPC.dontTakeDamage = true;
            Vector2 grate = HasRoom
                ? FloodGalleryRoom.GrateWorldPos(RoomOrigin) + new Vector2(0f, -30f)
                : deathStartPos + new Vector2(0f, 40f);

            if (t <= DeathAnchorDropAt) {
                //锚脱手坠向格栅
                NPC.velocity *= 0.85f;
                anchorVisualPos = Vector2.Lerp(anchorVisualPos, grate + new Vector2(0f, 14f), 0.2f);
                if (!anchorDropped && t == DeathAnchorDropAt) {
                    anchorDropped = true;
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = -0.6f, MaxInstances = 2 }, grate);
                    ShakeNearby(1.5f);
                }
                return;
            }

            //踉跄扶膝，全身滴水加速
            if (!Main.dedServ && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    NPC.Center + Main.rand.NextVector2Circular(22f, 36f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(1f, 3f)),
                    BogWater * 0.8f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 26), 0.1f);
            }

            if (!grateCracked && t >= DeathCrackAt) {
                //格栅锈裂：全场唯一大拍
                grateCracked = true;
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1f, Pitch = -0.5f, MaxInstances = 1 }, grate);
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.8f, Pitch = -0.8f, MaxInstances = 2 }, grate);
                //死亡大拍的短屏幕色倾（IMPL-E 客户端演出口，距离门自守）
                if (!Main.dedServ && Main.LocalPlayer.Alives()
                    && Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1500f) {
                    Ambience.DungeonworldAmbience.PushGradePulse(BogDeep, 0.3f, 30);
                }
                ShakeNearby(4f, 1400f);
                if (!VaultUtils.isClient && HasRoom) {
                    FloodGalleryWatcher.PaintGrateCracked(RoomOrigin);
                    //死亡起点的水面行（供泄洪阶梯从当前水位出发）
                    deathSurfaceFrom = FloodGalleryWatcher.GetRoomSurfaceRel(RoomOrigin);
                }
                if (!Main.dedServ) {
                    for (int k = 0; k < 8; k++) {
                        PRTLoader.NewParticle<PRT_Spark>(grate + new Vector2(Main.rand.NextFloat(-56f, 56f), 8f),
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)),
                            Color.Lerp(RustOrange, Color.White, Main.rand.NextFloat(0.5f)),
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(10, 16));
                    }
                }
            }

            //阶梯泄洪：五步一次性事务；他被水流拽向格栅
            if (t >= DeathDrainBeats[0] && t < DeathGrabAt) {
                float k = (t - DeathDrainBeats[0]) / (float)(DeathGrabAt - DeathDrainBeats[0]);
                NPC.Center = Vector2.Lerp(deathStartPos, grate + new Vector2(-26f, -10f), MathF.Pow(k, 1.3f));
                NPC.velocity = Vector2.Zero;
                NPC.rotation = NPC.rotation.AngleLerp(0.5f, 0.06f);
            }
            for (int step = 0; step < DeathDrainBeats.Length; step++) {
                if (t == DeathDrainBeats[step] && lastDrainStep < step) {
                    lastDrainStep = step;
                    if (!VaultUtils.isClient && HasRoom) {
                        int from = deathSurfaceFrom > 0 ? deathSurfaceFrom : FloodGalleryRoom.Scale2SurfaceRel;
                        int rel = from + (FloodGalleryRoom.FloorRel - from) * (step + 1) / DeathDrainBeats.Length;
                        FloodGalleryWatcher.ApplyWater(RoomOrigin, rel);
                    }
                    SoundEngine.PlaySound(SoundID.Drown with {
                        Volume = 0.8f,
                        Pitch = -0.2f - step * 0.12f,
                        MaxInstances = 2
                    }, grate);
                    if (!Main.dedServ) {
                        //漩涡 PRT 汇于格栅
                        for (int k = 0; k < 6; k++) {
                            Vector2 from = grate + new Vector2(Main.rand.NextFloat(-160f, 160f), -Main.rand.NextFloat(20f, 120f));
                            PRTLoader.NewParticle<PRT_SumpSpray>(from, (grate - from) * 0.06f,
                                BogWater * 0.9f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
                        }
                    }
                }
            }

            //抓住格栅边缘定格一拍，指滑，没入
            if (t >= DeathGrabAt && t < DeathSlipAt) {
                NPC.Center = grate + new Vector2(-26f, -10f);
                NPC.velocity = Vector2.Zero;
                //定格中的微颤（挣扎）
                NPC.netOffset = new Vector2(MathF.Sin(t * 2.7f) * 1.4f, 0f);
            }
            if (!slipped && t >= DeathSlipAt) {
                slipped = true;
                SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.7f, Pitch = 0.25f, MaxInstances = 1 }, grate);
                NPC.Center = grate + new Vector2(0f, 6f);
            }

            if (!lootCued && t >= DeathLootCueAt) {
                //空槽滴水，锚立格栅；解封双门 + 战利品涌出预告
                lootCued = true;
                if (!VaultUtils.isClient && HasRoom) {
                    FloodGalleryWatcher.SealDoors(RoomOrigin, false);
                }
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 2 }, grate);
                if (!Main.dedServ) {
                    for (int k = 0; k < 6; k++) {
                        PRTLoader.NewParticle<PRT_SumpSpray>(grate + new Vector2(Main.rand.NextFloat(-40f, 40f), 4f),
                            new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(2f, 4f)),
                            FoamWhite, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                    }
                }
            }

            if (t >= DeathTotal) {
                //放行真死：掉落全出在格栅上
                deathDone = true;
                NPC.dontTakeDamage = false;
                NPC.Center = grate + new Vector2(0f, -8f);
                if (!VaultUtils.isClient) {
                    NPC.StrikeInstantKill();
                }
            }
        }

        //==================== 击杀通报与结算 ====================

        /// <summary>服务器钩子：通报看守熄灯 + 逐人结算沉锚镣环（野外测试召唤找不到房间则只结算掉落）</summary>
        public override void OnKill() {
            FloodGalleryWatcher.NotifyUndrownedDefeated(NPC.Center);
            DungeonworldBossRecords.ServerSettleKill(DungeonworldBossRecords.BossIdUndrowned,
                NPC, NPC.Center, ModContent.ItemType<UndrownedShackleCharm>());
        }

        //==================== 受击 ====================

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            //受击喷水不喷血（泡胀尸肉的材质回答）
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(NPC.Center + Main.rand.NextVector2Circular(20f, 30f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(0.8f, 2.2f), -Main.rand.NextFloat(0.5f, 2f)),
                    BogWater * 0.85f, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(16, 28), 0.12f);
            }
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(16f, 16f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(1f, 2.4f), -Main.rand.NextFloat(0.5f, 1.5f)),
                    Color.Lerp(RustOrange, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        //==================== 表现参数与杂项 ====================

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

        /// <summary>躯体透明度：入场渐显、水下压暗、死亡指滑后没入</summary>
        private float BodyAlpha() {
            int t = (int)StateTimer;
            switch (State) {
                case StateEmerge:
                    return MathHelper.Clamp(t / 16f, 0f, 1f);
                case StateDeath:
                    if (t >= DeathSlipAt) {
                        return MathHelper.Clamp(1f - (t - DeathSlipAt) / 10f, 0f, 1f);
                    }
                    return 1f;
                case StateDespawn:
                    return despawnSat ? MathHelper.Clamp(1f - (t - 90) / 60f, 0f, 1f) : 1f;
                default:
                    return 1f;
            }
        }

        private void PushTrail() {
            if (!trailInit) {
                trailInit = true;
                for (int i = 0; i < TrailLen; i++) {
                    trailPos[i] = NPC.Center;
                }
            }
            trailHead = (trailHead + 1) % TrailLen;
            trailPos[trailHead] = NPC.Center;
        }

        private void UpdateBodyFrame() {
            int speed = Submerged ? 4 : Math.Abs(NPC.velocity.X) > 6f ? 3 : 6;
            if (++bodyFrameTick >= speed) {
                bodyFrameTick = 0;
                bodyFrameIndex++;
            }
        }

        /// <summary>常态底噪：泡胀躯体的缘滴下坠（客户端）</summary>
        private void UpdateAmbientDrip() {
            if (Main.dedServ || Submerged || BodyAlpha() < 0.5f) {
                return;
            }
            if (Main.rand.NextBool(18)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), Main.rand.NextFloat(-30f, 20f)),
                    new Vector2(0f, Main.rand.NextFloat(0.8f, 1.8f)),
                    BogWater * 0.7f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(18, 30), 0.1f);
            }
        }

        //==================== 绘制：拖影 → 锚链 → 锚 → 躯体 → 加色眼芯 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.CreatureFromTheDeep);
            Main.instance.LoadItem(ItemID.Anchor);
            Texture2D bodyTex = TextureAssets.Npc[NPCID.CreatureFromTheDeep]?.Value;
            Texture2D anchorTex = TextureAssets.Item[ItemID.Anchor]?.Value;
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            if (bodyTex == null || anchorTex == null || chainTex == null) {
                return false;
            }

            float alpha = BodyAlpha();
            if (alpha <= 0.01f) {
                //没入后只剩立在格栅上的锚
                if (State == StateDeath) {
                    DrawAnchor(spriteBatch, anchorTex, anchorVisualPos, 0f, drawColor, 1f);
                }
                return false;
            }

            Rectangle frame = BodyFrame(bodyTex);
            bool submerged = Submerged;
            float bodyFade = submerged ? 0.4f : 1f;

            //速度残影（破水/拽行/突进的速度装饰，速度门控）
            if (NPC.velocity.Length() > 10f) {
                for (int k = 4; k >= 1; k--) {
                    int idx = (trailHead - k * 2 + TrailLen * 2) % TrailLen;
                    float fall = 1f - k / 5f;
                    DrawBodyAt(spriteBatch, bodyTex, frame, trailPos[idx],
                        CorpseDeep * (0.16f * fall * alpha * bodyFade), 1f);
                }
            }

            //锚与锚链：掷出期锚由弹幕自绘（涡轨锚连链一起自绘），这里只画链或持锚
            Projectile anchorProj = FindMyAnchorAny();
            Vector2 handPos = NPC.Center + new Vector2(FacingSign * 14f, -20f);
            if (anchorProj != null) {
                if ((int)anchorProj.ai[2] != UndrownedAnchor.ModeOrbit) {
                    DrawChainLine(spriteBatch, chainTex, handPos, anchorProj.Center, drawColor, alpha);
                }
            }
            else {
                Vector2 anchorPos = HeldAnchorPos();
                float anchorRot = HeldAnchorRot();
                DrawChainLine(spriteBatch, chainTex, handPos, anchorPos, drawColor, alpha * bodyFade);
                DrawAnchor(spriteBatch, anchorTex, anchorPos, anchorRot, drawColor, alpha * bodyFade);
            }

            //躯体：暗缘压边 + 尸青主体（借光照色保持房内明暗）
            Color rim = CorpseDeep * (0.75f * alpha * bodyFade);
            DrawBodyAt(spriteBatch, bodyTex, frame, NPC.Center + new Vector2(0f, 2f), rim, 1.07f);
            Color body = Color.Lerp(drawColor, CorpseTeal, 0.55f) * (alpha * bodyFade);
            DrawBodyAt(spriteBatch, bodyTex, frame, NPC.Center, body, 1f);

            DrawEyeGlow(spriteBatch, alpha);
            return false;
        }

        private Rectangle BodyFrame(Texture2D tex) {
            int count = Math.Max(1, Main.npcFrameCount[NPCID.CreatureFromTheDeep]);
            int frameH = tex.Height / count;
            int idx;
            if (Submerged) {
                //水相帧窗：帧表尾部（原版泳姿段，行窗待游戏内校正）
                int span = Math.Min(SwimFrameSpan, count);
                idx = count - span + bodyFrameIndex % span;
            }
            else if (Math.Abs(NPC.velocity.X) > 0.6f) {
                int span = Math.Min(WalkFrameSpan, count);
                idx = bodyFrameIndex % span;
            }
            else {
                idx = 0;
            }
            return new Rectangle(0, idx * frameH, tex.Width, frameH);
        }

        private void DrawBodyAt(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 pos, Color color, float scaleMul) {
            const float scale = 1.85f;
            SpriteEffects fx = FacingSign >= 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //死亡演出的颈椎抖动/入场帧抖动
            Vector2 jitter = Vector2.Zero;
            int t = (int)StateTimer;
            if (State == StateEmerge && (int)StateParam == EmergeVariantThrone && t >= EmergeStandAt && t < EmergeFaceAt) {
                jitter = new Vector2(MathF.Sin(t * 3.1f) * 1.4f, 0f);
            }
            sb.Draw(tex, pos + jitter - Main.screenPosition, frame, color, NPC.rotation,
                new Vector2(frame.Width * 0.5f, frame.Height * 0.5f), scale * scaleMul, fx, 0f);
        }

        /// <summary>持锚位：状态推导（蓄力过顶/拖行在后/涡轨绕头由弹幕接管）</summary>
        private Vector2 HeldAnchorPos() {
            int t = (int)StateTimer;
            if (State == StateEmerge || State == StateDeath) {
                return anchorVisualPos;
            }
            if (State == StateAnchorThrow && ThrowPhase == 0) {
                //过顶高举，末段 pow6 向后猛吸
                float k = MathF.Pow(MathHelper.Clamp(t / (float)ThrowWindup, 0f, 1f), 6f);
                return NPC.Center + new Vector2(-FacingSign * (10f + k * 30f), -58f - k * 14f);
            }
            if (State == StateTideSlam && t <= SlamWindup) {
                return NPC.Center + new Vector2(0f, -60f);
            }
            if (State == StateTideSlam) {
                //砸进地里拔不出来
                return NPC.Center + new Vector2(FacingSign * 26f, NPC.height * 0.5f + 2f);
            }
            //常态拖在身后
            return NPC.Center + new Vector2(-FacingSign * 44f, NPC.height * 0.5f - 6f);
        }

        private float HeldAnchorRot() {
            if (State == StateAnchorThrow && ThrowPhase == 0) {
                return FacingSign * -0.5f;
            }
            if (State == StateTideSlam && (int)StateTimer > SlamWindup) {
                return 0f;
            }
            return FacingSign * 0.4f;
        }

        /// <summary>锈锚：暗缘 + 锈橙乘法调色（材质=锈锚铁）</summary>
        internal static void DrawAnchor(SpriteBatch sb, Texture2D tex, Vector2 pos, float rot, Color lightColor, float alpha) {
            Vector2 origin = tex.Size() * 0.5f;
            const float scale = 1.35f;
            sb.Draw(tex, pos - Main.screenPosition, null, RustDeep * (0.7f * alpha), rot,
                origin, scale * 1.08f, SpriteEffects.None, 0f);
            sb.Draw(tex, pos - Main.screenPosition, null,
                lightColor.MultiplyRGB(RustOrange) * alpha, rot, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>锚链：悬链下垂步进铺 Chain22，重染水藻绿灰</summary>
        internal static void DrawChainLine(SpriteBatch sb, Texture2D chainTex, Vector2 from, Vector2 to, Color lightColor, float alpha) {
            Vector2 origin = chainTex.Size() * 0.5f;
            float dist = Vector2.Distance(from, to);
            int links = Math.Clamp((int)(dist / 12f), 2, 90);
            //下垂量随链长（悬链弧近似：中点下坠）
            float sag = MathHelper.Clamp(dist * 0.14f, 4f, 46f);
            Color tint = lightColor.MultiplyRGB(new Color(120, 142, 128)) * (0.9f * alpha);
            Vector2 prev = from;
            for (int i = 1; i <= links; i++) {
                float k = i / (float)links;
                Vector2 p = Vector2.Lerp(from, to, k);
                p.Y += MathF.Sin(k * MathHelper.Pi) * sag;
                sb.Draw(chainTex, (prev + p) * 0.5f - Main.screenPosition, null, tint,
                    (p - prev).ToRotation() + MathHelper.PiOver2, origin, 0.9f, SpriteEffects.None, 0f);
                prev = p;
            }
        }

        /// <summary>眼芯加色层：尸白眼缝，蓄力/仪式期抬亮（Additive 批内强度写进色乘）</summary>
        private void DrawEyeGlow(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || alpha < 0.05f) {
                return;
            }
            float level = 0.45f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + Seed);
            if (State is StateFloodRite or StateAnchorThrow) {
                level += 0.25f;
            }
            if (Submerged) {
                level += 0.2f;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 gOrigin = glow.Size() * 0.5f;
            Vector2 eye = NPC.Center + new Vector2(FacingSign * 15f, -30f).RotatedBy(NPC.rotation);
            sb.Draw(glow, eye - Main.screenPosition, null, EyePale * (0.6f * level * alpha), 0f,
                gOrigin, new Vector2(8f * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
