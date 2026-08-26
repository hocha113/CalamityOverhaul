using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 沉波狱吏（L4 水牢湿舱段，WAVE2-ENEMIES §3.3）：水面下只有一道涟漪跟着你，
    /// 靠近水缘就被它暴起拽向水里；上岸的它锈重迟缓、甲缝大开。
    /// 状态机：0 潜航（舱段内贴水游弋，alpha 200 半隐，涟漪常显）→ 1 暴起（30f 气泡柱+
    /// 三环收束 telegraph，末 6f 静默拍，开锁判决音即跃出承诺 → 抛物线跃出扑抱，
    /// 净空不足改水线横扑）→ 2 压制（玩家同水体：42f 冲程节奏划水、接触 44→52）→
    /// 3 搁浅（登干地 240f 累计：移速减半、防御 16→8、寻路回水，120s 无水自毁散架，
    /// 钥匙串崩飞）。
    /// 联机：状态/暴起时机/舱段绑定服务器权威；跃出初速一次性写 velocity 乘 SyncNPC，
    /// 客户端本地积分不回卷；涟漪行各端本地扫水面推导（联机客户端无 Compartments 数据）；
    /// 击退拽向水体在受害端本地改向（ModifyHitPlayer 原版路径）；钥匙串摆动/材质包络
    /// 纯本地视觉（由已同步的 velocity/ai 推导，不入包）。
    /// 材质=浸水锈甲+污水（TurnkeyBody.fx 帧后处理）：水下焦散+沼暗 / 出水淌水线+轮廓
    /// 湿缘 / 搁浅锈斑蔓延+甲缝渗水；水面层（TurnkeyRipple.fx，TurnkeyWaterlineRender
    /// Weight 1.690）：潜航尾流白沫+水下暗影透镜 / 暴起隆起沸腾。
    /// 公平阀：暴起 = 三环收束 30f + 开锁判决音 + 末 6f 静默拍（预告即承诺，锚定 ai[2]
    /// 出水列不追踪）；压制冲程 12f 突进 + 30f 滑行（追击有节奏空窗）；搁浅 = 玩家的
    /// 惩罚窗口（防御减半 + 甲缝渗水视觉声明）。
    /// </summary>
    internal class DrownedTurnkey : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.HellArmoredBones;

        //==================== 参数（建议值，验收再调）====================

        private const int StateLurk = 0;
        private const int StateBurst = 1;
        private const int StateSuppress = 2;
        private const int StateBeached = 3;

        /// <summary>暴起 telegraph / 跃出帧</summary>
        private const int BurstTelegraph = 30;
        private const int BurstAirCap = 150;
        /// <summary>暴起触发：水平距 / 与水面垂距（px）</summary>
        private const float BurstTriggerX = 180f;
        private const float BurstTriggerY = 64f;
        /// <summary>登干地累计 → 搁浅 / 搁浅自毁（2min）</summary>
        private const int BeachAfterFrames = 240;
        private const int BeachSelfDestruct = 7200;
        /// <summary>压制冲程周期 / 突进段帧数（12f 突进 + 30f 滑行）</summary>
        private const int StrokePeriod = 42;
        private const int StrokeSurge = 12;

        private const float SwimSpeed = 4.5f;
        private const float SuppressSpeed = 7.6f;
        private const int DefenseWet = 16;
        private const int DefenseBeached = 8;

        /// <summary>沼绿蓝浸水甲（drawColor 乘色）</summary>
        private static readonly Color SwampMul = new(95, 135, 125);
        /// <summary>白沫涟漪</summary>
        private static readonly Color FoamPale = new(200, 228, 222);
        /// <summary>冲击环主体（沼水搅白）/ 内侧深水</summary>
        private static readonly Color RingMain = new(120, 180, 168);
        private static readonly Color RingDeep = new(30, 60, 54);

        /// <summary>绑定舱段索引（服务器权威决策用；客户端恒 -1 属预期）</summary>
        private int boundIdx = -1;
        /// <summary>涟漪水面行（各端本地扫水面缓存，tile 行；-1=无）</summary>
        private int rippleRow = -1;
        /// <summary>最后有效水面行（离水后水面层按 rippleEnv 淡出的锚，防瞬灭弹掉）</summary>
        private int lastSurfaceRow = -1;

        //==================== 视觉包络（纯本地，由已同步的 wet/ai 推导）====================

        private bool wasWet;
        /// <summary>0~1 浸没（TurnkeyBody uWet）</summary>
        private float wetEnv;
        /// <summary>0~1 出水淌水（uDrip；离水置 1 缓慢晾干 ≈4.3s）</summary>
        private float dripEnv;
        /// <summary>0~1 搁浅锈化（uBeach；回水洗掉）</summary>
        private float beachEnv;
        /// <summary>0~1 甲缝渗水（uSeep；搁浅前期涨满、晾干段归零）</summary>
        private float seepEnv;
        /// <summary>水面层包络：在场 / 威胁 / 沸腾 / 静默</summary>
        private float rippleEnv, threatEnv, boilEnv, quietEnv;
        /// <summary>PreDraw 置位、PostDraw 消费：本体批当前套着 TurnkeyBody</summary>
        private bool bodyShaderActive;

        //==================== 钥匙串（纯本地摆动物理，三枚异长异相）====================

        private readonly float[] keyAngle = new float[3];
        private readonly float[] keyAngVel = new float[3];
        private static readonly float[] KeyLen = [11f, 15f, 19f];
        private Vector2 prevVelocity;
        private int jangleCooldown;

        //==================== 渲染层出口（TurnkeyWaterlineRender 消费）====================

        /// <summary>渲染用水面行：当前行优先，离水后回落到记忆行让尾流淡完</summary>
        internal int RenderRippleRow => rippleRow > 0 ? rippleRow : lastSurfaceRow;
        /// <summary>水面层锚定 X：暴起态锚死 ai[2] 出水列（沸腾柱不跟身走），其余跟本体</summary>
        internal float RenderCenterX => (int)State == StateBurst && StateTimer <= BurstTelegraph + 6
            ? StateParam * 16f + 8f : NPC.Center.X;
        internal float RenderEnv => rippleEnv;
        internal float RenderThreat => threatEnv;
        internal float RenderBoil => boilEnv;
        internal float RenderQuiet => quietEnv;
        internal float RenderSeed => Seed;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.HellArmoredBones];
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 22;
            NPC.height = 42;
            NPC.damage = 44;
            NPC.defense = DefenseWet;
            NPC.lifeMax = 420;
            NPC.knockBackResist = 0.1f;
            NPC.aiStyle = -1;
            //手动重力：水中浮控，干地下坠
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.npcSlots = 2f;
            NPC.value = 50000f;
            NPC.alpha = 200;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath2;
            AnimationType = NPCID.HellArmoredBones;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.DrownedTurnkey.Bestiary"),
            ]);
        }

        //==================== 投放（§4：仅 L4 湿舱段 0.12，每舱段 ≤1）====================

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            if (!DungeonworldEliteDirector.CommonSpawnGate(spawnInfo, Type)) {
                return 0f;
            }
            if (DungeonworldEliteDirector.BandIndexForRow(spawnInfo.SpawnTileY) != 3 || !spawnInfo.Water) {
                return 0f;
            }
            if (!DungeonworldEliteDirector.InWetCompartment(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY,
                out L4WaterWorks.Compartment compartment)) {
                return 0f;
            }
            //每舱段同时 ≤1
            int myType = Type;
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.type != myType) {
                    continue;
                }
                if (compartment.Area.Contains((int)(other.Center.X / 16f), (int)(other.Center.Y / 16f))) {
                    return 0f;
                }
            }
            return 0.12f;
        }

        /// <summary>出生绑定所在舱段（服务器；spawn 包时序无关：boundIdx 只被服务器决策消费）</summary>
        public override void OnSpawn(IEntitySource source) {
            if (VaultUtils.isClient) {
                return;
            }
            RebindCompartment();
        }

        private void RebindCompartment() {
            L4WaterWorks.Compartment c = DungeonworldEliteDirector.CompartmentContaining(
                (int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f));
            if (c != null) {
                boundIdx = L4WaterWorks.Compartments.IndexOf(c);
            }
        }

        private L4WaterWorks.Compartment Bound
            => boundIdx >= 0 && boundIdx < L4WaterWorks.Compartments.Count
                ? L4WaterWorks.Compartments[boundIdx] : null;

        //==================== AI ====================

        /// <summary>alpha 目标=状态函数：潜航 200（水下只余轮廓），其余 0</summary>
        private int AlphaTarget() => (int)State == StateLurk && NPC.wet ? 200 : 0;

        public override void AI() {
            HealAlpha(AlphaTarget(), 10);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateEdgeCue();
            }
            ServerSyncPacer();
            RefreshRippleRow();

            //防御=状态确定函数（各端一致：判伤发生在攻击端本地，运行时改防御必须走 ai 推导）
            NPC.defense = (int)State == StateBeached ? DefenseBeached : DefenseWet;

            //手动重力（干地/空中）
            if (!NPC.wet) {
                NPC.velocity.Y = Math.Min(NPC.velocity.Y + 0.35f, 10f);
            }

            NPC.TargetClosest(faceTarget: false);
            Player target = NPC.HasValidTarget ? Main.player[NPC.target] : null;

            UpdateVisualEnvelopes(target);

            switch ((int)State) {
                case StateLurk:
                    UpdateLurk(target);
                    break;
                case StateBurst:
                    UpdateBurst(target);
                    break;
                case StateSuppress:
                    UpdateSuppress(target);
                    break;
                default:
                    UpdateBeached();
                    break;
            }

            if (NPC.velocity.X != 0f) {
                NPC.direction = Math.Sign(NPC.velocity.X);
                NPC.spriteDirection = NPC.direction;
            }
            //暴起空中段前倾入速度向（出水的扑抱要有身体语言），其余回正
            if ((int)State == StateBurst && !NPC.wet) {
                NPC.rotation = MathHelper.Lerp(NPC.rotation,
                    MathHelper.Clamp(NPC.velocity.X * 0.05f, -0.5f, 0.5f), 0.2f);
            }
            else {
                NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.3f);
            }

            if (!Main.dedServ) {
                UpdateKeychain();
                DoAmbientWaterFx();
            }
        }

        private void PlayStateEdgeCue() {
            switch ((int)State) {
                case StateBurst:
                    //开锁判决音：钥吏下达了溺刑判决——这声即跃出承诺（公平阀：音画双通道预告）
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.9f, Pitch = -0.15f, MaxInstances = 2 },
                        new Vector2(StateParam * 16f + 8f, NPC.Center.Y));
                    break;
                case StateSuppress:
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, NPC.Center);
                    break;
                case StateBeached:
                    //搁浅：锈住的铁罐子落地闷响
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.55f, MaxInstances = 2 }, NPC.Center);
                    break;
            }
        }

        //==================== 视觉包络（各端本地，由已同步的 wet/ai 推导，不入包）====================

        private void UpdateVisualEnvelopes(Player target) {
            bool wetNow = NPC.wet;
            if (wasWet && !wetNow) {
                dripEnv = 1f;
                SpawnExitDripBurst();
            }
            wasWet = wetNow;
            wetEnv = MathHelper.Lerp(wetEnv, wetNow ? 1f : 0f, wetNow ? 0.10f : 0.06f);
            dripEnv = Math.Max(0f, dripEnv - 1f / 260f);

            int st = (int)State;
            if (st == StateBeached) {
                //锈斑 70s 涨满；渗水 10s 涨满、末 30s（晾干段）收干
                beachEnv = Math.Min(1f, beachEnv + 1f / 4200f);
                float rise = Math.Min(1f, StateTimer / 600f);
                float dryKill = 1f - MathHelper.Clamp((StateTimer - 5400f) / 900f, 0f, 1f);
                seepEnv = rise * dryKill;
            }
            else {
                beachEnv = Math.Max(0f, beachEnv - (wetNow ? 0.012f : 0.002f));
                seepEnv = Math.Max(0f, seepEnv - 0.02f);
            }

            //水面层：在场（湿身且有水面行）/ 威胁（压制>暴起>近身>巡游）/ 沸腾 / 静默拍
            float envT = wetNow && rippleRow > 0 && st != StateBeached ? 1f : 0f;
            rippleEnv = MathHelper.Lerp(rippleEnv, envT, envT > rippleEnv ? 0.07f : 0.035f);
            float threatT = st == StateSuppress ? 1f
                : st == StateBurst ? 0.75f
                : target != null && !target.dead && Vector2.Distance(target.Center, NPC.Center) < 280f ? 0.55f
                : 0.15f;
            threatEnv = MathHelper.Lerp(threatEnv, threatT, 0.05f);
            float boilT = st == StateBurst && StateTimer <= BurstTelegraph ? StateTimer / BurstTelegraph : 0f;
            boilEnv = MathHelper.Lerp(boilEnv, boilT, boilT > boilEnv ? 0.25f : 0.12f);
            float quietT = st == StateBurst && StateTimer > BurstTelegraph - 6 && StateTimer <= BurstTelegraph ? 1f : 0f;
            quietEnv = MathHelper.Lerp(quietEnv, quietT, 0.45f);
        }

        /// <summary>出水沿：满身挂水一次性甩落（客户端表现）</summary>
        private void SpawnExitDripBurst() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_TurnkeyDrip>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-18f, 14f)),
                    NPC.velocity * 0.3f + new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-0.5f, 1f)),
                    FoamPale * 0.8f, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(24, 40));
            }
        }

        //==================== 钥匙串摆动物理（纯本地：加速度驱动的三摆，异长异相自然错拍）====================

        private void UpdateKeychain() {
            Vector2 accel = NPC.velocity - prevVelocity;
            prevVelocity = NPC.velocity;
            bool wet = NPC.wet;
            //水下：阻尼重、重力轻（悬摆迟缓）；干地：轻阻尼强响应（叮当乱撞）
            float g = wet ? 0.014f : 0.030f;
            float damp = wet ? 0.90f : 0.965f;
            float drive = wet ? 0.012f : 0.024f;
            for (int i = 0; i < 3; i++) {
                float phase = 1f + i * 0.14f;
                keyAngVel[i] += -MathF.Sin(keyAngle[i]) * g * phase;
                keyAngVel[i] -= accel.X * drive * MathF.Cos(keyAngle[i]);
                keyAngVel[i] += accel.Y * drive * 0.5f * MathF.Sin(keyAngle[i] + 0.35f * i);
                keyAngVel[i] = MathHelper.Clamp(keyAngVel[i] * damp, -0.45f, 0.45f);
                keyAngle[i] += keyAngVel[i];
            }

            //金属碰响：急动量超阈值时叮当一声（水下闷、干地脆）；本地音效允许 Main.rand
            if (jangleCooldown > 0) {
                jangleCooldown--;
            }
            if (accel.Length() > 1.05f && jangleCooldown <= 0) {
                jangleCooldown = 15;
                SoundEngine.PlaySound(SoundID.CoinPickup with {
                    Volume = wet ? 0.16f : 0.30f,
                    Pitch = wet ? -0.45f : Main.rand.NextFloat(-0.15f, 0.25f),
                    MaxInstances = 3,
                }, NPC.Center);
            }
        }

        //==================== 潜航 ====================

        private void UpdateLurk(Player target) {
            StateTimer++;

            if (NPC.wet) {
                //湿身：干地累计快速消退
                if (!VaultUtils.isClient && StackCount > 0f) {
                    StackCount = Math.Max(0f, StackCount - 5f);
                }
                SwimSteer(target, SwimSpeed);
                ClampInsideBound();
            }
            else {
                //落在干地：走向水（全速），累计搁浅计时
                SeekWaterGait(2.2f);
                if (!VaultUtils.isClient) {
                    if (NPC.velocity.Y == 0f) {
                        StackCount++;
                    }
                    if (StackCount >= BeachAfterFrames) {
                        ChangeState(StateBeached);
                        return;
                    }
                }
            }

            //暴起/压制裁决（服务器；20f 驻留防水缘往复抖动刷包）
            if (VaultUtils.isClient || target == null || target.dead || !NPC.wet || StateTimer < 20f) {
                return;
            }
            if (PlayerInBoundWater(target)) {
                ChangeState(StateSuppress);
                return;
            }
            if (rippleRow > 0) {
                float surfaceY = rippleRow * 16f;
                if (Math.Abs(target.Center.X - NPC.Center.X) < BurstTriggerX
                    && Math.Abs(target.Center.Y - surfaceY) < BurstTriggerY
                    && StateTimer > 90f) {
                    //ai[2]=出水点列（客户端画三环收束的锚；发令后锁死不追踪 = 预告即承诺）
                    float exitX = MathHelper.Clamp(target.Center.X / 16f,
                        BoundLeftTile() + 2, BoundRightTile() - 2);
                    ChangeState(StateBurst, (int)exitX);
                }
            }
        }

        /// <summary>舱段内贴水游弋：跟随玩家水平位、保持其下方（限速 4.5）</summary>
        private void SwimSteer(Player target, float maxSpeed) {
            Vector2 desired;
            if (target != null && !target.dead) {
                float surfaceY = rippleRow > 0 ? rippleRow * 16f : NPC.Center.Y - 48f;
                float wantY = Math.Max(surfaceY + 56f, target.Center.Y + 48f);
                desired = new Vector2(target.Center.X, wantY) - NPC.Center;
            }
            else {
                desired = new Vector2(MathF.Sin(AmbientClock * 0.02f + Seed) * 40f, 0f);
            }
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.08f);
        }

        /// <summary>活动范围钳在舱段矩形内（服务器权威；客户端无表跳过，靠周期锚校正）</summary>
        private void ClampInsideBound() {
            if (VaultUtils.isClient) {
                return;
            }
            L4WaterWorks.Compartment c = Bound;
            if (c == null) {
                RebindCompartment();
                return;
            }
            float left = c.Area.Left * 16f + 8f;
            float right = c.Area.Right * 16f - 8f - NPC.width;
            float bottom = c.Area.Bottom * 16f - 8f - NPC.height;
            NPC.position.X = MathHelper.Clamp(NPC.position.X, left, right);
            NPC.position.Y = Math.Min(NPC.position.Y, bottom);
        }

        private int BoundLeftTile() => Bound?.Area.Left ?? (int)(NPC.Center.X / 16f) - 10;
        private int BoundRightTile() => Bound?.Area.Right ?? (int)(NPC.Center.X / 16f) + 10;

        /// <summary>玩家是否与它同水体（服务器：湿 + 在绑定舱段矩形内）</summary>
        private bool PlayerInBoundWater(Player player) {
            if (!player.wet) {
                return false;
            }
            L4WaterWorks.Compartment c = Bound;
            return c != null && c.Area.Contains((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
        }

        //==================== 暴起 ====================

        private void UpdateBurst(Player target) {
            StateTimer++;
            int t = (int)StateTimer;
            float exitXpx = StateParam * 16f + 8f;

            if (t <= BurstTelegraph) {
                //蓄势：潜到出水点正下方；末 8f 反向下沉蓄力（跃出前的一口深吸气）
                if (NPC.wet) {
                    float sinkBias = t > BurstTelegraph - 8 ? 26f : 0f;
                    Vector2 aim = new(exitXpx, (rippleRow > 0 ? rippleRow * 16f : NPC.Center.Y) + 40f + sinkBias);
                    Vector2 desired = aim - NPC.Center;
                    if (desired.Length() > SwimSpeed * 1.4f) {
                        desired = desired.SafeNormalize(Vector2.Zero) * SwimSpeed * 1.4f;
                    }
                    NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.12f);
                }
                //气泡柱变密（各端本地，锚在 ai[2] 出水列）；末 6f 静默拍停手——水面憋住一口气
                bool quietBeat = t > BurstTelegraph - 6;
                if (!Main.dedServ && !quietBeat && rippleRow > 0 && (int)AmbientClock % 2 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        new Vector2(exitXpx + Main.rand.NextFloat(-6f, 6f), rippleRow * 16f + Main.rand.NextFloat(0f, 20f)),
                        new Vector2(0f, -Main.rand.NextFloat(1.5f, 3f)),
                        FoamPale * 0.5f, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(10, 18));
                }
                //跃出发令（服务器）：净空 ≥5 格抛物线跃出，不足改水线横扑
                if (!VaultUtils.isClient && t == BurstTelegraph && target != null) {
                    int exitTileX = (int)StateParam;
                    int surface = rippleRow > 0 ? rippleRow : (int)(NPC.Center.Y / 16f) - 2;
                    bool headroom = !Collision.SolidTiles(exitTileX - 1, exitTileX + 1, surface - 5, surface - 1);
                    if (headroom) {
                        float vx = MathHelper.Clamp((target.Center.X - NPC.Center.X) / 22f, -8f, 8f);
                        NPC.velocity = new Vector2(vx, -9.5f);
                    }
                    else {
                        NPC.velocity = new Vector2(Math.Sign(target.Center.X - NPC.Center.X) * 8.5f, -2f);
                    }
                    NPC.netUpdate = true;
                }
                return;
            }

            if (t >= BurstTelegraph + 1 && BeatForward(1) && !Main.dedServ) {
                //水炸开：污水团+白沫贴面+满身水珠三层（发射四相之"发射炸点"）
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 1f, Pitch = -0.2f, MaxInstances = 3 }, NPC.Center);
                float surfY = RenderRippleRow > 0 ? RenderRippleRow * 16f : NPC.Top.Y;
                for (int i = 0; i < 7; i++) {
                    PRTLoader.NewParticle<PRT_SewageGlob>(NPC.Top, VaultUtils.RandVr(2f, 6f) - Vector2.UnitY * 3f,
                        SwampMul, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(28, 44));
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_TurnkeyFoam>(
                        new Vector2(NPC.Center.X + Main.rand.NextFloat(-26f, 26f), surfY + Main.rand.NextFloat(-4f, 4f)),
                        new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.5f, 1.5f)),
                        FoamPale * 0.85f, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(40, 70), surfY);
                }
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_TurnkeyDrip>(NPC.Center + VaultUtils.RandVr(4f, 14f),
                        VaultUtils.RandVr(1.5f, 4f) - Vector2.UnitY * 2f,
                        FoamPale * 0.8f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(22, 36));
                }
            }

            //落干地拍：铁罐坠地闷响+溅水（各端本地由物理态推导，严格前进沿防重播）
            if (!Main.dedServ && t > BurstTelegraph + 10 && !NPC.wet
                && NPC.velocity.Y == 0f && BeatForward(2)) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 2 }, NPC.Bottom);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_TurnkeyDrip>(
                        NPC.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), -4f),
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f)),
                        FoamPale * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 30));
                }
            }

            //空中段：重力自然抛物线；落定裁决（服务器）
            if (VaultUtils.isClient) {
                return;
            }
            //16f 承诺期：水线横扑全程贴水（wet 恒真），6f 就裁决会把横扑掐成抽搐
            if (t > BurstTelegraph + 16 && NPC.wet) {
                ChangeState(target != null && PlayerInBoundWater(target) ? StateSuppress : StateLurk);
                return;
            }
            if (t > BurstTelegraph + 10 && !NPC.wet && NPC.velocity.Y == 0f) {
                //落干地：回潜航壳（由它的干地分支走搁浅累计）
                ChangeState(StateLurk);
                return;
            }
            if (t > BurstAirCap) {
                ChangeState(StateLurk);
            }
        }

        //==================== 压制（同水体强化：冲程节奏划水）====================

        private void UpdateSuppress(Player target) {
            StateTimer++;
            //42f 冲程：12f 突进（1.5×速直扑预测位）+ 30f 滑行（衰减+微调向）。
            //捕食者划水而非匀速追踪；滑行段即玩家的反打空窗（公平阀）。
            //冲程相位由已同步的 StateTimer 推导，各端一致
            int phase = (int)StateTimer % StrokePeriod;
            if (target != null && !target.dead) {
                Vector2 lead = target.Center + target.velocity * 8f;
                Vector2 desired = lead - NPC.Center;
                if (phase < StrokeSurge) {
                    float cap = SuppressSpeed * 1.5f;
                    if (desired.Length() > cap) {
                        desired = desired.SafeNormalize(Vector2.Zero) * cap;
                    }
                    NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.20f);
                }
                else {
                    NPC.velocity *= 0.985f;
                    if (desired.Length() > SuppressSpeed) {
                        desired = desired.SafeNormalize(Vector2.Zero) * SuppressSpeed;
                    }
                    NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.02f);
                }
            }
            //冲程起点音画（本地表现，相位确定函数）
            if (!Main.dedServ && phase == 0 && NPC.wet) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.25f, Pitch = 0.35f, MaxInstances = 3 }, NPC.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 12f + VaultUtils.RandVr(0f, 6f),
                        -NPC.velocity * 0.15f, FoamPale * 0.4f, Main.rand.NextFloat(0.2f, 0.35f))
                        ?.Configure(Main.rand.Next(12, 20));
                }
                if (rippleRow > 0) {
                    PRTLoader.NewParticle<PRT_TurnkeyFoam>(
                        new Vector2(NPC.Center.X + Main.rand.NextFloat(-14f, 14f), rippleRow * 16f + 2f),
                        new Vector2(NPC.velocity.X * 0.2f, 0f),
                        FoamPale * 0.6f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(30, 50), rippleRow * 16f + 2f);
                }
            }
            ClampInsideBound();
            if (VaultUtils.isClient || StateTimer < 20f) {
                return;
            }
            if (target == null || target.dead || !PlayerInBoundWater(target)) {
                ChangeState(StateLurk);
            }
        }

        //==================== 搁浅 ====================

        private void UpdateBeached() {
            StateTimer++;
            SeekWaterGait(1.1f);

            //锈甲吱响拍：每 90f 一声（严格前进沿；beat 随 timer 单调）+ 甲缝渗珠一簇
            if (!Main.dedServ && BeatForward(1 + (int)(StateTimer / 90f))) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.28f, Pitch = -0.75f, MaxInstances = 2 }, NPC.Center);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_TurnkeyDrip>(
                        NPC.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-14f, 4f)),
                        new Vector2(0f, 0.4f), FoamPale * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(Main.rand.Next(20, 32));
                }
            }

            if (VaultUtils.isClient) {
                return;
            }
            if (NPC.wet) {
                //爬回水里：复位
                StackCount = 0f;
                ChangeState(StateLurk);
                return;
            }
            if (StateTimer >= BeachSelfDestruct) {
                //无水可回：散架（服务器裁决死亡，checkDead 内部走 HitEffect+同步）
                NPC.life = 0;
                NPC.checkDead();
            }
        }

        /// <summary>最笨寻水步态：朝绑定舱段中心横移+跳（不做 A*；客户端无表时维持现向）</summary>
        private void SeekWaterGait(float maxSpeed) {
            L4WaterWorks.Compartment c = Bound;
            if (c != null) {
                float centerX = c.Area.Center.X * 16f;
                if (Math.Abs(centerX - NPC.Center.X) > 24f) {
                    NPC.direction = Math.Sign(centerX - NPC.Center.X);
                }
            }
            else if (NPC.direction == 0) {
                NPC.direction = 1;
            }
            NPC.velocity.X += 0.08f * NPC.direction;
            NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -maxSpeed, maxSpeed);
            if (NPC.velocity.Y == 0f && NPC.collideX) {
                NPC.velocity.Y = -6.5f;
            }
        }

        //==================== 命中：拽向水体 + 浸寒 ====================

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            if ((int)State == StateSuppress) {
                //同水体强化：接触 44→52
                modifiers.SourceDamage *= 52f / 44f;
            }
            if ((int)State != StateBeached) {
                //击退方向强制拽向它（=拽向它所在/所来的水体；受害端本地解算，原版路径）
                int dir = Math.Sign(NPC.Center.X - target.Center.X);
                if (dir != 0) {
                    modifiers.HitDirectionOverride = dir;
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.Chilled, 180);
        }

        //==================== 表现：涟漪 / 气泡 / 渗水 ====================

        /// <summary>各端本地扫水面：从体位向上找第一行无水格，其下即水面行（联机客户端无舱段表的替代数据源）</summary>
        private void RefreshRippleRow() {
            if ((int)AmbientClock % 10 != 0 && rippleRow > 0) {
                return;
            }
            rippleRow = -1;
            int x = (int)(NPC.Center.X / 16f);
            int y = (int)(NPC.Center.Y / 16f);
            if (!WorldGen.InWorld(x, y, 10) || Main.tile[x, y].LiquidAmount == 0) {
                return;
            }
            for (int k = 1; k < 60; k++) {
                int yy = y - k;
                if (yy < 10) {
                    return;
                }
                if (Main.tile[x, yy].LiquidAmount == 0) {
                    rippleRow = yy + 1;
                    lastSurfaceRow = rippleRow;
                    return;
                }
            }
        }

        private void DoAmbientWaterFx() {
            //潜航稀疏气泡列 + 偶发到面即破的沫斑
            if ((int)State == StateLurk && NPC.wet && (int)AmbientClock % 9 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Top + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f),
                    new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.2f)),
                    FoamPale * 0.35f, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(Main.rand.Next(14, 24));
                if (rippleRow > 0 && (int)AmbientClock % 45 == 0) {
                    PRTLoader.NewParticle<PRT_TurnkeyFoam>(
                        new Vector2(NPC.Center.X + Main.rand.NextFloat(-10f, 10f), rippleRow * 16f + 2f),
                        Vector2.Zero, FoamPale * 0.4f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(26, 40), rippleRow * 16f + 2f);
                }
            }
            //搁浅甲缝渗水滴（受伤提示的一半；另一半是防御减半）；晾干段（渗水包络收干）自然停
            if ((int)State == StateBeached && seepEnv > 0.15f && (int)AmbientClock % 8 == 0) {
                PRTLoader.NewParticle<PRT_TurnkeyDrip>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-14f, 6f)),
                    new Vector2(0f, 0.5f), FoamPale * (0.5f * seepEnv + 0.2f), Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(20, 32));
            }
            //出水挂水淌滴（暴起空中段密集甩水）
            if (!NPC.wet && (int)State == StateBurst && (int)AmbientClock % 3 == 0) {
                PRTLoader.NewParticle<PRT_TurnkeyDrip>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-16f, 16f)),
                    NPC.velocity * 0.25f + new Vector2(0f, 1f), FoamPale * 0.75f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(22, 34));
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            int n = NPC.life <= 0 ? 14 : 3;
            for (int i = 0; i < n; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone, hit.HitDirection * 1.5f, -1f);
            }
            if (NPC.life <= 0) {
                //散架：污水泼洒 + 钥匙串崩飞（钥吏死了，钥匙还它水牢）
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, NPC.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_SewageGlob>(NPC.Center, VaultUtils.RandVr(1.5f, 5f),
                        SwampMul, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(26, 40));
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_TurnkeyKey>(
                        NPC.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-8f, 4f)),
                        new Vector2(Main.rand.NextFloat(-3.2f, 3.2f), -Main.rand.NextFloat(2.5f, 5f)),
                        Color.White, 1f)?.Configure(Main.rand.Next(200, 280));
                }
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_TurnkeyDrip>(NPC.Center + VaultUtils.RandVr(2f, 12f),
                        VaultUtils.RandVr(1f, 4f) - Vector2.UnitY * 1.5f,
                        FoamPale * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(20, 34));
                }
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(new CommonDrop(ItemID.GillsPotion, 3));
            npcLoot.Add(new CommonDrop(ItemID.WaterWalkingPotion, 100, 1, 1, 15));
            npcLoot.Add(new CommonDrop(ItemID.BreathingReed, 10));
        }

        //==================== 绘制 ====================
        //本体：PreDraw 切 Immediate 套 TurnkeyBody.fx（浸水材质三态），PostDraw 还原；
        //钥匙串：默认批借金钥匙物品图三摆悬挂；
        //暴起三环收束/跃出冲击环：ShockRingDraw（内部自管批次）；
        //水面尾流/沸腾层在 TurnkeyWaterlineRender（EndEntityDraw，画在水体之上）。

        public override Color? GetAlpha(Color drawColor) {
            Color mul = (int)State == StateBeached ? new Color(80, 112, 102) : SwampMul;
            return drawColor.MultiplyRGB(mul) * NPC.Opacity;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态泄漏自愈（netcode 7.2）
            BeginDefault(spriteBatch);

            Effect fx = EffectLoader.TurnkeyBody?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            float material = Math.Max(Math.Max(wetEnv, dripEnv), Math.Max(beachEnv, seepEnv));
            if (fx == null || noise == null || material <= 0.02f || NPC.IsABestiaryIconDummy) {
                return true;
            }

            Texture2D tex = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = tex.Bounds;
            }
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uTexelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            //帧界半像素内缩：所有采样钳回帧内，防精灵表渗色横线
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                (frame.X + 0.5f) / tex.Width, (frame.Y + 0.5f) / tex.Height,
                (frame.X + frame.Width - 0.5f) / tex.Width, (frame.Y + frame.Height - 0.5f) / tex.Height));
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uWet"]?.SetValue(wetEnv);
            fx.Parameters["uDrip"]?.SetValue(dripEnv * (1f - wetEnv));
            fx.Parameters["uBeach"]?.SetValue(beachEnv);
            fx.Parameters["uSeep"]?.SetValue(seepEnv);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique = fx.Techniques["TechBody"];
            fx.CurrentTechnique.Passes[0].Apply();
            bodyShaderActive = true;
            return true;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (bodyShaderActive) {
                bodyShaderActive = false;
                BeginDefault(spriteBatch);
            }
            if (NPC.IsABestiaryIconDummy) {
                return;
            }

            DrawKeychain(spriteBatch, screenPos);

            //暴起水面环（ShockRingDraw 内部切批并还原，调用点须处于默认实体批）；
            //跃出后本体已离水，环锚用记忆水面行画完
            int ringRow = RenderRippleRow;
            if ((int)State == StateBurst && ringRow > 0) {
                Vector2 anchor = new(StateParam * 16f + 8f, ringRow * 16f + 4f);
                int t = (int)StateTimer;
                if (t <= BurstTelegraph) {
                    //三环收束：radius 64→10 逐环错 5f（预告的可读时钟）
                    for (int i = 0; i < 3; i++) {
                        float p = MathHelper.Clamp((t - i * 5) / 22f, 0f, 1f);
                        if (p <= 0f) {
                            continue;
                        }
                        ShockRingDraw.Draw(spriteBatch, anchor, MathHelper.Lerp(64f, 10f, p), 4f,
                            FoamPale, RingMain, RingDeep, 0.18f + 0.34f * p,
                            tearPx: 6f, squish: 0.30f, innerGlow: 0f, timeSeed: Seed + i * 1.7f);
                    }
                }
                else if (t <= BurstTelegraph + 16) {
                    //跃出冲击环：出水点炸开一圈外扩浪环
                    float p = (t - BurstTelegraph) / 16f;
                    ShockRingDraw.Draw(spriteBatch, anchor, MathHelper.Lerp(12f, 82f, p), 6f,
                        FoamPale, RingMain, RingDeep, (1f - p) * 0.55f,
                        tearPx: 9f, squish: 0.30f, innerGlow: 0.25f, timeSeed: Seed);
                }
            }
        }

        /// <summary>
        /// 钥匙串：腰侧挂点垂三枚金钥匙（异长 11/15/19px 异相自然错拍），
        /// 链子 MagicPixel 细线，钥匙借金钥匙物品图；乘环境光与沼色，随本体 Opacity 隐显
        /// </summary>
        private void DrawKeychain(SpriteBatch sb, Vector2 screenPos) {
            Main.instance.LoadItem(ItemID.GoldenKey);
            Texture2D keyTex = TextureAssets.Item[ItemID.GoldenKey].Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 anchor = NPC.Center + new Vector2(-7f * NPC.spriteDirection, 4f + NPC.gfxOffY);
            Color lit = Lighting.GetColor((int)(anchor.X / 16f), (int)(anchor.Y / 16f));
            Color keyCol = lit.MultiplyRGB(new Color(210, 225, 215)) * NPC.Opacity;
            Color chainCol = lit.MultiplyRGB(new Color(105, 118, 112)) * (0.9f * NPC.Opacity);
            Vector2 keyOrigin = new(keyTex.Width * 0.28f, keyTex.Height * 0.2f);

            for (int i = 0; i < 3; i++) {
                Vector2 dir = new(MathF.Sin(keyAngle[i]), MathF.Cos(keyAngle[i]));
                Vector2 keyPos = anchor + dir * KeyLen[i];
                //链线：从挂点铺向钥匙（MagicPixel 竖向 1×len 缩放，旋到 dir 向）
                float rot = MathF.Atan2(dir.Y, dir.X) - MathHelper.PiOver2;
                sb.Draw(pixel, anchor - screenPos, new Rectangle(0, 0, 1, 1), chainCol, rot,
                    new Vector2(0.5f, 0f), new Vector2(1.4f, KeyLen[i]), SpriteEffects.None, 0f);
                //钥匙体：物品图斜置，摆角驱动旋转
                sb.Draw(keyTex, keyPos - screenPos, null, keyCol,
                    keyAngle[i] - MathHelper.PiOver4, keyOrigin, 0.8f, SpriteEffects.None, 0f);
                //湿钥匙一线水光（湿度包络驱动）
                float glint = Math.Max(wetEnv, dripEnv);
                if (glint > 0.05f) {
                    sb.Draw(keyTex, keyPos - screenPos, null,
                        new Color(150, 195, 188, 0) * (0.4f * glint * NPC.Opacity),
                        keyAngle[i] - MathHelper.PiOver4, keyOrigin, 0.8f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
