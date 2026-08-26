using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 提灯巡守（L2 牢狱层，WAVE2-ENEMIES §3.1）：灯锥扫到你就举灯鸣警、全层怪向你涌来。
    /// 状态机：0 巡逻（灯锥常显，入锥累计 30f 宽限）→ 1 鸣警（75f，三环三响，
    /// 窗口内击杀=警报流产）→ 2 追缉（×1.6 追击 + 活警报器浓度 + 即时增援已在鸣警尾拍发出）
    /// → 3 熄灯撤防（180f 解除信号）→ 回巡逻（30s 冷却）。
    /// 联机：入锥裁决/鸣警计时/增援生成全服务器，ai[0..3] 过线，各端从 ai 重放灯锥/三环节拍；
    /// 出生 alpha 目标恒 0。材质=锈铁+灯油火：灯随步摆 / 锥内浮尘发亮 / 鸣警灯焰白热过曝一拍即回暖。
    /// 视觉复合：灯锥走 LanternWardenCone.fx（LanternWardenRender，实体层下体积光，触墙截断）；
    /// 灯具本体走 LanternWardenLamp.fx（金属/玻璃亮度域拆分+焰动+扫光）；警报环走 ShockRing；
    /// 余烬/烟/溅油火舌走 PRT_LWarden 三件套。着色器缺编各层均有 CPU 回退，灯锥承诺永不隐形。
    /// </summary>
    internal class LanternWarden : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.RustyArmoredBonesSword;

        //==================== 参数（建议值，验收再调）====================

        private const int StatePatrol = 0;
        private const int StateAlarm = 1;
        private const int StateChase = 2;
        private const int StateStanddown = 3;

        /// <summary>灯锥半径（px）</summary>
        internal const float ConeRange = 340f;
        /// <summary>灯锥半角余弦（半角 27°；上缘高出水平 ~10°，同层站立必入锥，
        /// 远端保留跳跃越锥窗口——判定与 LanternWardenCone.fx 的 tan27° 同一半角）</summary>
        private const float ConeHalfCos = 0.891f;
        /// <summary>灯锥轴线下俯角（弧度 ~17°；再陡则平地上锥轴过早触地，判定名不副实）</summary>
        private const float ConePitch = 0.30f;
        /// <summary>入锥宽限帧：满 30f 才鸣警，撤出即衰减</summary>
        private const int GraceFrames = 30;
        /// <summary>鸣警总帧</summary>
        private const int AlarmFrames = 75;
        /// <summary>追缉硬上限 / 断视脱战帧</summary>
        private const int ChaseFrames = 600;
        private const int LoseSightFrames = 360;
        /// <summary>撤防帧 / 警报冷却（30s）</summary>
        private const int StanddownFrames = 180;
        private const int AlarmCooldown = 1800;

        private const float PatrolSpeed = 1.3f;
        /// <summary>追缉速（头注承诺 ×1.6：1.3×1.6≈2.1）</summary>
        private const float ChaseSpeed = 2.1f;
        /// <summary>追缉铃：首响帧 / 周期（活警报器的听觉脉搏，灯焰烽燧脉冲同拍）</summary>
        private const int ChaseBellLead = 20;
        private const int ChaseBellPeriod = 56;

        /// <summary>灯油火主色（暖橙）</summary>
        internal static readonly Color LampWarm = new(255, 180, 90);
        private static readonly Color LampCore = new(255, 230, 170);
        private static readonly Color LampDeep = new(150, 60, 24);
        /// <summary>老铁锈褐（drawColor 乘色）</summary>
        private static readonly Color RustMul = new(205, 150, 105);

        //==================== 各端本地表现字段（不过线，纯演出）====================

        /// <summary>转身锁（防抖，各端本地步态用）</summary>
        private int turnLock;
        /// <summary>灯体受击/鸣拍抖动冲量（弧度级，指数衰减）</summary>
        private float lampJolt;
        /// <summary>灯具锚点平滑（举灯/放灯是重物挪动，不许瞬移）</summary>
        private Vector2 lampSmooth;
        private bool lampSmoothInit;
        /// <summary>灯锥触墙截断距离（px，低频探针+平滑；灯不许穿墙照人）</summary>
        private float coneReach = ConeRange + 45f;
        private float coneReachTarget = ConeRange + 45f;

        //==================== 渲染侧只读口（LanternWardenRender 消费）====================

        internal bool ConeVisible => (int)State == StatePatrol;
        internal float RenderFlameLevel => FlameLevel();
        internal float RenderAlert01 => (int)State == StatePatrol
            ? Math.Min(1f, StateParam / GraceFrames) : 0f;
        internal Vector2 RenderConeAxis => ConeAxis();
        internal Vector2 RenderLanternPos => lampSmoothInit ? lampSmooth : LanternPos();
        internal float RenderConeReach01 => coneReach / LanternWardenRender.ConeQuadLen;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.RustyArmoredBonesSword];
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 18;
            NPC.height = 40;
            NPC.damage = 34;
            NPC.defense = 12;
            NPC.lifeMax = 260;
            NPC.knockBackResist = 0.25f;
            NPC.aiStyle = -1;
            NPC.npcSlots = 1.5f;
            NPC.value = 40000f;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;
            AnimationType = NPCID.RustyArmoredBonesSword;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.LanternWarden.Bestiary"),
            ]);
        }

        //==================== 投放（§4：L2 主投 0.10；L6 二现已被裁决 §1-11 砍掉，本波 L6/L7 零投放）====================

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            if (!DungeonworldEliteDirector.CommonSpawnGate(spawnInfo, Type)) {
                return 0f;
            }
            return DungeonworldEliteDirector.BandIndexForRow(spawnInfo.SpawnTileY) == 1 ? 0.10f : 0f;
        }

        //==================== AI ====================

        public override void AI() {
            HealAlpha(0);
            AmbientClock++;
            //先取上帧观测态再让 StateEdge 覆写，撤防→巡逻的重燃灯芯声要认得来路
            int prevObserved = (int)NPC.localAI[2] - 1;
            if (StateEdge()) {
                PlayStateEdgeCue(prevObserved);
            }
            ServerSyncPacer();

            if (NPC.direction == 0) {
                NPC.direction = 1;
            }

            switch ((int)State) {
                case StatePatrol:
                    UpdatePatrol();
                    break;
                case StateAlarm:
                    UpdateAlarm();
                    break;
                case StateChase:
                    UpdateChase();
                    break;
                default:
                    UpdateStanddown();
                    break;
            }

            NPC.spriteDirection = NPC.direction;
            UpdateLampPresentation();
            DoLanternLight();
            DoConeDust();
        }

        private void PlayStateEdgeCue(int prevState) {
            switch ((int)State) {
                case StateAlarm:
                    //灯焰拔高的点火声 + 举灯前小蹲身（重物预备拍）
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, Pitch = 0.3f, MaxInstances = 3 }, NPC.Center);
                    lampJolt += 0.5f;
                    if (lampSmoothInit) {
                        lampSmooth += new Vector2(-NPC.direction * 2f, 6f);
                    }
                    break;
                case StateChase:
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, NPC.Center);
                    lampJolt += 0.4f;
                    break;
                case StateStanddown:
                    //解除的哑钟 + 掐灯芯头一缕烟
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = -0.6f, MaxInstances = 2 }, NPC.Center);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 3; i++) {
                            PRTLoader.NewParticle<PRT_LWardenSmoke>(FlamePos() + Main.rand.NextVector2Circular(4f, 4f),
                                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -0.7f), default, 1f)
                                ?.Configure(Main.rand.Next(42, 62), Main.rand.NextFloat(0.16f, 0.26f));
                        }
                    }
                    break;
                case StatePatrol:
                    if (prevState == StateStanddown) {
                        //撤防期满重燃灯芯：轻点火 + 两粒火星（冷却结束的可听信号）
                        SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.3f, Pitch = 0.5f, MaxInstances = 2 }, NPC.Center);
                        EmitEmbers(2, 1.1f);
                    }
                    break;
            }
        }

        //==================== 巡逻 ====================

        private void UpdatePatrol() {
            WalkGait(PatrolSpeed, 0.06f, turnAtLedge: true);

            //警报冷却记帧（服务器裁决用，表现不读它）
            if (!VaultUtils.isClient && StackCount > 0f) {
                StackCount--;
            }

            //入锥裁决只在服务器，每 10 tick 一次/每玩家：距离+夹角+通视
            if (VaultUtils.isClient || (int)AmbientClock % 10 != 0 || StackCount > 0f) {
                return;
            }
            bool seen = false;
            Vector2 axis = ConeAxis();
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                Vector2 toPlayer = player.Center - LanternPos();
                float dist = toPlayer.Length();
                if (dist > ConeRange || dist < 1f) {
                    continue;
                }
                if (Vector2.Dot(toPlayer / dist, axis) < ConeHalfCos) {
                    continue;
                }
                if (!Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                    player.position, player.width, player.height)) {
                    continue;
                }
                seen = true;
                break;
            }
            float old = StateParam;
            if (seen) {
                StateParam = Math.Min(GraceFrames + 10, StateParam + 10);
            }
            else {
                StateParam = Math.Max(0, StateParam - 10);
            }
            if (StateParam != old) {
                NPC.netUpdate = true;
            }
            if (StateParam >= GraceFrames) {
                ChangeState(StateAlarm);
            }
        }

        //==================== 鸣警（75f，窗口内被击杀=警报流产）====================

        private void UpdateAlarm() {
            StateTimer++;
            NPC.velocity.X *= 0.8f;
            int t = (int)StateTimer;

            //三响三环，节拍固定可背；严格前进沿防回卷重播。
            //每响附灯体抖动 + 火星迸出（钟声要有物理后果，不是干响）
            for (int i = 0; i < 3; i++) {
                if (t >= RingBeatFrame(i) && BeatForward(i + 1)) {
                    SoundEngine.PlaySound(SoundID.Item35 with {
                        Volume = 0.8f + i * 0.05f,
                        Pitch = -0.15f + i * 0.2f,
                        MaxInstances = 3
                    }, NPC.Center);
                    lampJolt += 0.35f;
                    EmitEmbers(6 + i * 2, 2.2f);
                }
            }

            if (VaultUtils.isClient || t < AlarmFrames) {
                return;
            }
            //鸣警成功当拍：即时增援保底通道（EditSpawnRate 只是浓度阀，可能来得慢）
            NPC.TargetClosest(faceTarget: true);
            if (NPC.HasValidTarget) {
                SpawnReinforcements(Main.player[NPC.target]);
            }
            ChangeState(StateChase);
        }

        /// <summary>三环节拍帧：12/37/62</summary>
        internal static int RingBeatFrame(int index) => 12 + index * 25;

        //==================== 追缉（活警报器）====================

        private void UpdateChase() {
            StateTimer++;

            //服务器每帧通报浓度（追缉结束后 Director 里自然残留 8s）
            if (!VaultUtils.isClient) {
                DungeonworldEliteDirector.ReportAlarmChase(NPC.whoAmI, NPC.Center);
            }

            //追缉铃（56f 周期）：活警报器持续可听，灯焰烽燧脉冲与它同拍；
            //BeatForward 单调线防 StateTimer 回卷重响，拍号 10+ 与鸣警三拍(1..3)不冲突
            int t = (int)StateTimer;
            if (t >= ChaseBellLead) {
                int bellIndex = 10 + (t - ChaseBellLead) / ChaseBellPeriod;
                if (BeatForward(bellIndex)) {
                    SoundEngine.PlaySound(SoundID.Item35 with {
                        Volume = 0.5f,
                        Pitch = bellIndex % 2 == 0 ? -0.1f : 0.05f,
                        MaxInstances = 3
                    }, NPC.Center);
                    lampJolt += 0.25f;
                }
            }

            NPC.TargetClosest(faceTarget: true);
            if (!NPC.HasValidTarget) {
                if (!VaultUtils.isClient) {
                    ChangeState(StateStanddown);
                }
                return;
            }
            Player target = Main.player[NPC.target];

            //追击步态 + 小跳
            float dx = target.Center.X - NPC.Center.X;
            if (Math.Abs(dx) > 8f) {
                NPC.direction = Math.Sign(dx);
            }
            WalkGait(ChaseSpeed, 0.12f, turnAtLedge: false);
            bool standing = NPC.velocity.Y == 0f;
            if (standing) {
                if (NPC.collideX || (target.Bottom.Y < NPC.Top.Y - 48f && Math.Abs(dx) < 140f)) {
                    NPC.velocity.Y = -7.6f;
                }
                else if ((int)StateTimer % 55 == 0) {
                    NPC.velocity.Y = -4.5f;
                }
            }

            //断视/超时脱战（服务器裁决）
            if (VaultUtils.isClient) {
                return;
            }
            if ((int)AmbientClock % 10 == 0) {
                bool sight = Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                    target.position, target.width, target.height);
                StateParam = sight ? 0f : StateParam + 10f;
            }
            if (StateParam >= LoseSightFrames || StateTimer >= ChaseFrames) {
                ChangeState(StateStanddown);
            }
        }

        //==================== 熄灯撤防 ====================

        private void UpdateStanddown() {
            StateTimer++;
            NPC.velocity.X *= 0.85f;
            if (!VaultUtils.isClient && StateTimer >= StanddownFrames) {
                ChangeState(StatePatrol);
                StackCount = AlarmCooldown;
            }
        }

        //==================== 步态（最笨 fighter：撞墙/临崖转身，可读性优先于聪明）====================

        private void WalkGait(float maxSpeed, float accel, bool turnAtLedge) {
            if (turnLock > 0) {
                turnLock--;
            }
            NPC.velocity.X += accel * NPC.direction;
            NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -maxSpeed, maxSpeed);

            bool standing = NPC.velocity.Y == 0f;
            if (!standing || turnLock > 0) {
                return;
            }
            bool turn = NPC.collideX;
            if (!turn && turnAtLedge) {
                int probeX = (int)((NPC.Center.X + NPC.direction * (NPC.width / 2 + 8)) / 16f);
                int probeY = (int)(NPC.Bottom.Y / 16f);
                turn = !StandableTile(probeX, probeY) && !StandableTile(probeX, probeY + 1);
            }
            if (turn) {
                NPC.direction = -NPC.direction;
                NPC.velocity.X = 0f;
                turnLock = 12;
            }
        }

        private static bool StandableTile(int x, int y) {
            if (!WorldGen.InWorld(x, y, 5)) {
                return false;
            }
            Tile tile = Main.tile[x, y];
            return tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        //==================== 增援（服务器）：屏幕外最近墙沿落 2 只本层原版怪 ====================

        private void SpawnReinforcements(Player target) {
            int[] pool = [NPCID.AngryBones, NPCID.AngryBonesBig, NPCID.AngryBonesBigMuscle, NPCID.AngryBonesBigHelmet];
            int spawned = 0;
            for (int side = -1; side <= 1; side += 2) {
                if (!TryFindPerch(target, side, out Point tile)) {
                    continue;
                }
                int type = pool[Main.rand.Next(pool.Length)];
                int idx = NPC.NewNPC(NPC.GetSource_FromAI(), tile.X * 16 + 8, tile.Y * 16, type);
                if (idx >= 0 && idx < Main.maxNPCs) {
                    spawned++;
                }
            }
            //两侧都找不到落点：退化为巡守脚边出（警报必须有后果）
            for (; spawned < 2; spawned++) {
                int type = pool[Main.rand.Next(pool.Length)];
                NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X + (spawned == 0 ? -32 : 32), (int)NPC.Bottom.Y, type);
            }
        }

        /// <summary>目标一侧约 62 格（略出屏）扫墙沿：脚下可站 + 上方 3 格净空</summary>
        private static bool TryFindPerch(Player target, int side, out Point tile) {
            int baseX = (int)(target.Center.X / 16f) + side * 62;
            int baseY = (int)(target.Center.Y / 16f);
            for (int step = 0; step < 8; step++) {
                int x = baseX + side * step * 2;
                if (x < 12 || x > Main.maxTilesX - 12) {
                    break;
                }
                for (int dy = -14; dy <= 14; dy++) {
                    int y = baseY + dy;
                    if (y < 12 || y >= Main.maxTilesY - 12) {
                        continue;
                    }
                    if (!WorldGen.SolidTile(x, y)) {
                        continue;
                    }
                    if (Collision.SolidTiles(x - 1, x + 1, y - 4, y - 1)) {
                        continue;
                    }
                    tile = new Point(x, y);
                    return true;
                }
            }
            tile = default;
            return false;
        }

        //==================== 表现：灯锚平滑 / 灯光 / 锥内浮尘 / 状态粒子 ====================

        /// <summary>灯锥轴线：行进方向前下 ~17°</summary>
        private Vector2 ConeAxis()
            => new Vector2(NPC.direction * MathF.Cos(ConePitch), MathF.Sin(ConePitch));

        private Vector2 LanternPos() {
            if ((int)State == StateAlarm) {
                return NPC.Center + new Vector2(NPC.direction * 2f, -24f);
            }
            return NPC.Center + new Vector2(NPC.direction * 9f, 1f);
        }

        /// <summary>灯焰实际位置（平滑锚 + 摆角下垂）</summary>
        private Vector2 FlamePos()
            => RenderLanternPos + new Vector2(0f, 14f).RotatedBy(LanternRotation());

        /// <summary>各端本地演出帧驱动：灯锚平滑（举灯是重物挪动）、抖动衰减、
        /// 触墙探针、追缉火星尾、撤防熄灯烟——全部纯表现，不碰裁决</summary>
        private void UpdateLampPresentation() {
            lampJolt *= 0.86f;
            Vector2 target = LanternPos();
            if (!lampSmoothInit || Vector2.DistanceSquared(lampSmooth, target) > 160f * 160f) {
                lampSmooth = target;
                lampSmoothInit = true;
            }
            else {
                lampSmooth = Vector2.Lerp(lampSmooth, target, 0.22f);
            }

            //灯锥触墙截断：低频水平探针 + 平滑跟随（灯不许穿墙照人，视觉与空间一致）
            if ((int)State == StatePatrol) {
                if ((int)AmbientClock % 3 == 0) {
                    coneReachTarget = ProbeWallReach();
                }
                coneReach += (coneReachTarget - coneReach) * 0.25f;
            }

            if (Main.dedServ) {
                return;
            }
            //追缉：狂奔的警报器沿途洒火星
            if ((int)State == StateChase && (int)AmbientClock % 6 == 0
                && Math.Abs(NPC.velocity.X) > 1.1f) {
                PRTLoader.NewParticle<PRT_LWardenEmber>(FlamePos() + Main.rand.NextVector2Circular(4f, 4f),
                    new Vector2(-NPC.velocity.X * 0.25f, -0.6f), default, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
            //撤防：灯焰渐熄，烟一缕一缕冒，越熄越稀
            if ((int)State == StateStanddown && (int)AmbientClock % 16 == 0
                && Main.rand.NextFloat() < 1.1f - StateTimer / (float)StanddownFrames) {
                PRTLoader.NewParticle<PRT_LWardenSmoke>(FlamePos(),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.5f), default, 1f)
                    ?.Configure(Main.rand.Next(38, 58), Main.rand.NextFloat(0.13f, 0.22f));
            }
        }

        /// <summary>灯口沿水平向前扫墙：连续两格实心视为墙面，返回截断距离；
        /// 无墙返回满 quad 长（shader 端 uReach 钳 0.93 收口）</summary>
        private float ProbeWallReach() {
            Vector2 from = LanternPos();
            int ty = (int)(from.Y / 16f);
            for (float d = 32f; d <= ConeRange; d += 16f) {
                int tx = (int)((from.X + NPC.direction * d) / 16f);
                if (!WorldGen.InWorld(tx, ty, 8)) {
                    return d;
                }
                if (WorldGen.SolidTile(tx, ty) && WorldGen.SolidTile(tx, ty - 1)) {
                    return d + 12f;
                }
            }
            return ConeRange + 45f;
        }

        /// <summary>灯焰强度：状态×计时的确定函数（各端一致）</summary>
        private float FlameLevel() {
            int t = (int)StateTimer;
            switch ((int)State) {
                case StateAlarm: {
                    float level = t < 4 ? 1.6f : MathHelper.Lerp(1.25f, 1.05f, Math.Min(1f, (t - 4) / 20f));
                    //三响每拍灯焰跟着鼓一下（声画同拍）
                    for (int i = 0; i < 3; i++) {
                        int dt = t - RingBeatFrame(i);
                        if (dt >= 0 && dt < 12) {
                            level += (1f - dt / 12f) * 0.4f;
                        }
                    }
                    return Math.Min(level, 1.65f);
                }
                case StateChase: {
                    //烽燧脉冲：与追缉铃（20+56k）同拍一鼓一收
                    float pulse = MathF.Pow(MathF.Abs(MathF.Cos((t - ChaseBellLead) * MathHelper.Pi / ChaseBellPeriod)), 3f);
                    return 0.85f + 0.35f * pulse;
                }
                case StateStanddown:
                    //熄灯要熄到位：0.12 只剩灯芯余烬，回巡逻边沿重燃
                    return MathHelper.Lerp(0.9f, 0.12f, Math.Min(1f, t / (float)StanddownFrames));
                default: {
                    float level = 0.62f + 0.05f * MathF.Sin(AmbientClock * 0.05f + Seed);
                    if (StateParam > 0f) {
                        //宽限期灯焰双闪（潜行玩家的撤出窗口提示）
                        level *= 0.72f + 0.28f * MathF.Sin(AmbientClock * 1.2f);
                    }
                    return level;
                }
            }
        }

        /// <summary>灯具 shader 的警报驱动量（uAlert）：巡逻=宽限充能，鸣警=满，追缉=半警</summary>
        private float LampAlert01() {
            return (int)State switch {
                StateAlarm => 1f,
                StateChase => 0.55f,
                StatePatrol => Math.Min(1f, StateParam / GraceFrames),
                _ => 0f,
            };
        }

        /// <summary>鸣警白热闪（uFlash）：首拍满闪 + 三响每拍短闪，timer 确定函数各端一致</summary>
        private float LampFlash01() {
            if ((int)State != StateAlarm) {
                return 0f;
            }
            int t = (int)StateTimer;
            float flash = t < 4 ? 1f - t / 4f * 0.3f : 0f;
            for (int i = 0; i < 3; i++) {
                int dt = t - RingBeatFrame(i);
                if (dt >= 0 && dt < 7) {
                    flash = Math.Max(flash, (1f - dt / 7f) * 0.85f);
                }
            }
            return flash;
        }

        private void DoLanternLight() {
            float level = FlameLevel();
            Vector2 pos = LanternPos();
            Lighting.AddLight(pos, 0.95f * level, 0.66f * level, 0.34f * level);
        }

        /// <summary>锥内浮尘发亮（各端本地，巡逻态专属；Dust 补真实体层深度，主体积感在 shader）</summary>
        private void DoConeDust() {
            if (Main.dedServ || (int)State != StatePatrol || (int)AmbientClock % 4 != 0) {
                return;
            }
            Vector2 axis = ConeAxis();
            Vector2 perp = new(-axis.Y, axis.X);
            float d = Main.rand.NextFloat(50f, Math.Min(coneReach, ConeRange) * 0.92f);
            Vector2 p = LanternPos() + axis * d + perp * Main.rand.NextFloat(-0.3f, 0.3f) * d;
            Dust dust = Dust.NewDustPerfect(p, DustID.Torch, axis * 0.4f, 150, default, Main.rand.NextFloat(0.6f, 1.0f));
            dust.noGravity = true;
        }

        /// <summary>灯焰火星迸发（各端本地表现）</summary>
        private void EmitEmbers(int count, float speed) {
            if (Main.dedServ) {
                return;
            }
            Vector2 from = FlamePos();
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(speed, speed * 0.8f) + new Vector2(0f, -speed * 0.5f);
                PRTLoader.NewParticle<PRT_LWardenEmber>(from + Main.rand.NextVector2Circular(3f, 3f),
                    vel, default, Main.rand.NextFloat(0.55f, 0.95f))
                    ?.Configure(Main.rand.Next(18, 32));
            }
        }

        //==================== 掉落 ====================

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(new CommonDrop(ItemID.GoldenKey, 100, 1, 1, 15));
            npcLoot.Add(new CommonDrop(ItemID.HunterPotion, 4));
            npcLoot.Add(new CommonDrop(ItemID.ChainLantern, 10));
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            if (NPC.life > 0) {
                //受击：骨屑 + 灯体一晃两粒火星（每次命中都有物理回应）
                for (int i = 0; i < 3; i++) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                        hit.HitDirection * 1.5f, -1f);
                }
                lampJolt += 0.3f;
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_LWardenEmber>(FlamePos(),
                        new Vector2(hit.HitDirection * Main.rand.NextFloat(0.6f, 1.8f), Main.rand.NextFloat(-1.4f, -0.4f)),
                        default, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(14, 24));
                }
                return;
            }
            //死亡余韵：灯摔碎——玻璃脆响、火星迸散、烟散、泼油贴地烧一阵
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.5f, Pitch = 0.15f }, NPC.Center);
            if ((int)State == StateAlarm) {
                //鸣警窗口内击杀=警报流产：被捂住的哑钟，奖励一耳朵可辨
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.55f, Pitch = -0.85f }, NPC.Center);
            }
            for (int i = 0; i < 12; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                    hit.HitDirection * 1.5f, -1f);
            }
            Vector2 lampAt = RenderLanternPos;
            for (int i = 0; i < 14; i++) {
                Vector2 vel = new Vector2(hit.HitDirection * Main.rand.NextFloat(0.5f, 2.6f), -1.4f)
                    + Main.rand.NextVector2Circular(2.6f, 2.2f);
                PRTLoader.NewParticle<PRT_LWardenEmber>(lampAt + Main.rand.NextVector2Circular(6f, 6f),
                    vel, default, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(20, 36));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_LWardenSmoke>(lampAt + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -0.8f), default, 1f)
                    ?.Configure(Main.rand.Next(40, 62), Main.rand.NextFloat(0.18f, 0.3f));
            }
            //泼油着火：向下找地，火舌根锚地面烧一阵（尸体消失了，事故现场还在）
            Vector2 ground = FindGroundBelow(NPC.Bottom);
            for (int i = 0; i < 5; i++) {
                Vector2 p = ground + new Vector2(Main.rand.NextFloat(-22f, 22f) + hit.HitDirection * 8f, 0f);
                PRTLoader.NewParticle<PRT_LWardenOilTongue>(p, Vector2.Zero, default, Main.rand.NextFloat(0.75f, 1.15f))
                    ?.Configure(new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -1f),
                        Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(28, 46));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_LWardenEmber>(ground + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.6f, -0.6f)),
                    default, Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(24, 40));
            }
        }

        private static Vector2 FindGroundBelow(Vector2 from) {
            int tx = (int)(from.X / 16f);
            int ty = (int)(from.Y / 16f);
            for (int dy = 0; dy < 6; dy++) {
                if (WorldGen.InWorld(tx, ty + dy, 8) && WorldGen.SolidTile(tx, ty + dy)) {
                    return new Vector2(from.X, (ty + dy) * 16f);
                }
            }
            return from + new Vector2(0f, 8f);
        }

        //==================== 绘制：本体走原版管线（GetAlpha 压锈色），灯/焰/环在 PostDraw，锥在 RenderHandle ====================

        public override Color? GetAlpha(Color drawColor)
            => drawColor.MultiplyRGB(RustMul) * NPC.Opacity;

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游实体批状态泄漏自愈（netcode 7.2）：以已知默认态重开一次
            BeginDefault(spriteBatch);
            return true;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DrawLanternBody(spriteBatch, screenPos, drawColor);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                BeginAdditive(spriteBatch);
                DrawFlameGlow(spriteBatch, glow);
                BeginDefault(spriteBatch);
            }
            //ShockRing 自管批切换，进出都是默认批（PostDraw 出口契约）
            if ((int)State == StateAlarm) {
                DrawAlarmRings(spriteBatch);
            }
            if ((int)State == StateChase) {
                DrawChasePulse(spriteBatch);
            }
        }

        /// <summary>吊灯本体：LanternWardenLamp.fx 做金属/玻璃拆分+焰动+扫光；
        /// 着色器缺编回退为锈色贴图直绘。随步伐摆锤式摆动，鸣警举过头，受击/鸣拍抖动</summary>
        private void DrawLanternBody(SpriteBatch sb, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadItem(ItemID.ChainLantern);
            Texture2D lantern = TextureAssets.Item[ItemID.ChainLantern]?.Value;
            if (lantern == null) {
                return;
            }
            float rot = LanternRotation();
            Vector2 anchor = RenderLanternPos;
            //origin 取贴图顶部中点，灯体绕提手摆动
            Vector2 origin = new(lantern.Width * 0.5f, 2f);
            Color bodyCol = drawColor.MultiplyRGB(new Color(220, 190, 150)) * NPC.Opacity;

            Effect fx = EffectLoader.LanternWardenLamp?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx != null && noise != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);
                fx.Parameters["uSeed"]?.SetValue(Seed);
                fx.Parameters["uLevel"]?.SetValue(FlameLevel());
                fx.Parameters["uAlert"]?.SetValue(LampAlert01());
                fx.Parameters["uFlash"]?.SetValue(LampFlash01());
                fx.Parameters["uTexSize"]?.SetValue(lantern.Size());
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(lantern, anchor - screenPos, null, bodyCol, rot, origin, 0.85f, SpriteEffects.None, 0f);
                BeginDefault(sb);
                gd.Textures[1] = null;
                return;
            }
            sb.Draw(lantern, anchor - screenPos, null, bodyCol, rot, origin, 0.85f, SpriteEffects.None, 0f);
        }

        private float LanternRotation() {
            //抖动冲量注入高频晃（鸣拍/受击的物理回应）
            float wobble = MathF.Sin(AmbientClock * 0.9f + Seed) * lampJolt * 0.35f;
            if ((int)State == StateAlarm) {
                return MathF.Sin(AmbientClock * 0.8f + Seed) * 0.06f + wobble;
            }
            float swing = 0.16f + 0.10f * Math.Min(1f, Math.Abs(NPC.velocity.X) / ChaseSpeed);
            return MathF.Sin(AmbientClock * 0.09f + Seed) * swing + wobble;
        }

        /// <summary>灯焰双层辉光（加色批：强度写进色值整体，A 随乘法收缩）</summary>
        private void DrawFlameGlow(SpriteBatch sb, Texture2D glow) {
            float level = FlameLevel();
            Vector2 flamePos = FlamePos();
            Vector2 gOrigin = glow.Size() * 0.5f;
            //鸣警首拍白热过曝，一拍即回暖（法 4：暖材质白只走短过曝）
            Color main = (int)State == StateAlarm && (int)StateTimer < 4
                ? Color.Lerp(LampWarm, Color.White, 0.75f) : LampWarm;
            sb.Draw(glow, flamePos - Main.screenPosition, null, main * (0.55f * level), 0f,
                gOrigin, new Vector2(26f * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, flamePos - Main.screenPosition, null, LampCore * (0.45f * level), 0f,
                gOrigin, new Vector2(11f * 2f / glow.Width), SpriteEffects.None, 0f);
        }

        /// <summary>警报三环：三响节拍展开的 ShockRing 冲击波（timer 确定函数，回卷不重播由绘制无副作用保证）。
        /// 波前 ease-out：出手快、末段滞空消散，环环相扣一环比一环远</summary>
        private void DrawAlarmRings(SpriteBatch sb) {
            int t = (int)StateTimer;
            Vector2 center = RenderLanternPos;
            for (int i = 0; i < 3; i++) {
                int start = RingBeatFrame(i);
                if (t < start || t > start + 44) {
                    continue;
                }
                float p = (t - start) / 44f;
                float ease = 1f - (1f - p) * (1f - p);
                float radius = 18f + ease * (205f + i * 28f);
                float alpha = (1f - p) * (1f - p) * (0.62f + i * 0.1f) * NPC.Opacity;
                ShockRingDraw.Draw(sb, center, radius, 10f - 5f * p,
                    LampCore, LampWarm, LampDeep, alpha,
                    innerGlow: 0.15f, timeSeed: i * 1.73f + Seed);
            }
        }

        /// <summary>追缉铃脉冲环：每响一圈小冲击波从灯口散出（活警报器的可视脉搏）</summary>
        private void DrawChasePulse(SpriteBatch sb) {
            int t = (int)StateTimer;
            if (t < ChaseBellLead) {
                return;
            }
            int dt = (t - ChaseBellLead) % ChaseBellPeriod;
            if (dt >= 22) {
                return;
            }
            float p = dt / 22f;
            float alpha = (1f - p) * (1f - p) * 0.4f * NPC.Opacity;
            ShockRingDraw.Draw(sb, RenderLanternPos, 14f + p * 92f, 7f - 4f * p,
                LampCore, LampWarm, LampDeep, alpha, timeSeed: Seed);
        }
    }
}
