using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 提灯翁（P4 §2.1）：雾夜提灯引路的事件敌，全场唯一，导演调度。
    /// 规矩：远远跟着有赏（跟随计量 ≥ 阈值，到点挂悬灯清雾 90 秒）；
    /// 凑近或动手遭殃（转身 20t 公平前摇后冷握，扣一成二血 + 黑暗，人化雾）。
    /// 状态机 ai[0]：0 灯前静立 → 1 沿地表行至目的地 → 2 到点结算 → 4 化雾；
    /// 任意时刻惊扰（贴身/受击）→ 3 转身 → 冷握 → 4。
    /// 联机合同：ai[0]=状态 ai[1]=计时 ai[2]=跟随计量（任一玩家在带内即 +）
    /// ai[3]=目的地索引；转移全服务器，各端由 ai 重放；冷握走服务器 player.Hurt
    /// 只结算 ≤GripRange 内最近者。目的地坐标只在服务器消费（KiyumeStructures
    /// 列表客户端恒空），客户端行走靠同步 velocity 外推 + 本地贴地探针。
    /// 绘制全接管：TheGroom 帧过 KiyumeKaidan.fx TechPaperGhost 纸衣化，
    /// 灯为 ChainLantern 物品贴图摆锤（镜像 LanternWarden 画法），着色器缺编回退近白剪影
    /// </summary>
    internal class LanternGuideYokai : KiyumeYokaiNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.TheGroom;

        private const int StateIdle = 0;
        private const int StateWalk = 1;
        private const int StateArrive = 2;
        private const int StateTurn = 3;
        private const int StateDissolve = 4;

        //──── 纯演出常量（调音数值在 KiyumeYokaiMetrics.Lantern*，此处不放机制量）────

        /// <summary>到点举灯拍（拍上生成悬灯 prop）/ 挂灯变体总时长 / 没人跟的收场时长</summary>
        private const int ArriveHangBeatTick = 30;
        private const int ArriveHangTotal = 46;
        private const int ArriveSilentTotal = 12;
        /// <summary>化雾时长（uDissolve 走满同帧退场，残点不上屏）</summary>
        private const int DissolveTicks = 46;
        /// <summary>统一现形语法的本怪极性：远处=雾里剪影，入此带强制现形——
        /// 他要能被跟随与对视，不能像犬影那样近身消隐</summary>
        private const float RevealEngageNearPx = 220f;
        private const float RevealEngageFarPx = 760f;
        /// <summary>眼锚：TheGroom 帧内原生 uv（面向左，34×54 帧实测）</summary>
        private static readonly Vector2 EyeAnchor = new(0.44f, 0.38f);
        /// <summary>纸衣主调（近白暖灰丧服，不抢血暮色）</summary>
        private static readonly Vector3 PaperTint = new(0.88f, 0.84f, 0.78f);
        /// <summary>轮廓缘光（血暮系，与 HoundShade 同族）</summary>
        private static readonly Vector3 EdgeTint = new Color(112, 26, 26).ToVector3();

        //──── 服务器侧字段（不入同步；目的地索引进 ai[3] 供排查）────

        private bool destInit;
        private float destX;
        private float stuckRefX;
        private int stuckTicks;

        //──── 各端本地表现 ────

        private float presentAlpha;
        private int facing = -1;
        /// <summary>灯已挂出（本地沿拍记录：挂灯后被惊扰的窗口里不许把灯画回手上）</summary>
        private bool lampHanded;

        /// <summary>冷握死亡播报（{0}=玩家名；ToNetworkText 让各端按自己语言解）</summary>
        private static LocalizedText gripDeathReason;

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.TheGroom];
            gripDeathReason = this.GetLocalization("GripDeathReason", () => "{0}跟错了灯");
        }

        protected override void SetYokaiDefaults() {
            NPC.width = 22;
            NPC.height = 44;
            NPC.damage = 0;          //无接触伤：冷握是转身后点名结算
            NPC.defense = 0;
            NPC.lifeMax = KiyumeYokaiMetrics.LanternLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;   //贴地靠探针，坡地宁飘不卡
            NPC.HitSound = SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.4f };
            NPC.DeathSound = null;
            AnimationType = NPCID.TheGroom;   //fighter 步行帧，velocity 驱动
        }

        //==================== AI ====================

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateCue();
            }
            ServerSyncPacer();
            NPC.velocity.Y = 0f;

            if (!VaultUtils.isClient && !destInit) {
                InitDestination();
            }

            switch ((int)State) {
                case StateIdle:
                    UpdateIdle();
                    break;
                case StateWalk:
                    UpdateWalk();
                    break;
                case StateArrive:
                    UpdateArrive();
                    break;
                case StateTurn:
                    UpdateTurn();
                    break;
                default:
                    UpdateDissolve();
                    break;
            }

            if (!VaultUtils.isClient && (int)State <= StateArrive) {
                JudgeScare();
                JudgeFollowMeter();
            }

            NPC.direction = facing;
            NPC.spriteDirection = facing;
            UpdatePresentation();
        }

        private void UpdateIdle() {
            NPC.velocity.X = 0f;
            StateTimer++;
            if (!VaultUtils.isClient && StateTimer >= KiyumeYokaiMetrics.LanternIdleTicks) {
                AdvanceState(StateWalk);
            }
        }

        private void UpdateWalk() {
            StateTimer++;
            if (!VaultUtils.isClient) {
                int dir = destX > NPC.Center.X ? 1 : -1;
                facing = dir;
                NPC.velocity.X = dir * KiyumeYokaiMetrics.LanternWalkSpeed;
                if (Math.Abs(NPC.Center.X - destX) <= 24f || StuckTooLong()) {
                    NPC.velocity.X = 0f;
                    AdvanceState(StateArrive);
                }
            }
            else if (Math.Abs(NPC.velocity.X) > 0.01f) {
                //客户端从同步速度读朝向（速度由服务器裁决，同步包外推）
                facing = NPC.velocity.X > 0f ? 1 : -1;
            }
            GroundGlide();
        }

        private void UpdateArrive() {
            NPC.velocity.X = 0f;
            StateTimer++;
            bool followed = StateParam >= KiyumeYokaiMetrics.LanternFollowGoal;
            if (followed) {
                //举灯拍：服务器挂出悬灯 prop，各端同拍放点火声
                if ((int)StateTimer >= ArriveHangBeatTick && BeatForward(1)) {
                    lampHanded = true;
                    SoundEngine.PlaySound(SoundID.Item20 with {
                        Volume = 0.5f,
                        Pitch = 0.25f,
                        MaxInstances = 2
                    }, LampWorldPos());
                    if (!VaultUtils.isClient) {
                        //NewNPC 以底中定位：挂点上方一点，灯浮在他头前的空里
                        Vector2 hangAt = NPC.Top + new Vector2(facing * 8f, -18f);
                        KiyumeHauntDirector.SpawnYokai(
                            ModContent.NPCType<LanternGuideHungLamp>(), hangAt);
                    }
                }
                if (!VaultUtils.isClient && StateTimer >= ArriveHangTotal) {
                    ChangeState(StateDissolve);
                }
            }
            else if (!VaultUtils.isClient && StateTimer >= ArriveSilentTotal) {
                //没人跟的谜没有答案：站定一拍就散
                ChangeState(StateDissolve);
            }
            GroundGlide();
        }

        private void UpdateTurn() {
            NPC.velocity.X = 0f;
            StateTimer++;
            //公平前摇的可读性：他停下、回头、看着你，帽下余烬这 20t 里点亮
            Player nearest = NearestLivePlayer(out _);
            if (nearest != null) {
                facing = nearest.Center.X > NPC.Center.X ? 1 : -1;
            }
            if (!VaultUtils.isClient && StateTimer >= KiyumeYokaiMetrics.LanternTurnTicks) {
                SettleGrip();
                ChangeState(StateDissolve, 1f);   //param=1：冷握变体化雾（音画重放读它）
            }
            GroundGlide();
        }

        private void UpdateDissolve() {
            NPC.velocity.X = 0f;
            StateTimer++;
            if (StateTimer >= DissolveTicks) {
                //各端同式确定退场；服务器 SyncNPC 兜底迟到端
                NPC.active = false;
            }
        }

        //==================== 服务器裁决 ====================

        /// <summary>惊扰判定：贴身或掉血即回头（受击不看来源，血线就是证词）</summary>
        private void JudgeScare() {
            bool hurt = NPC.life < NPC.lifeMax;
            Player nearest = NearestLivePlayer(out float dist);
            bool close = nearest != null && dist <= KiyumeYokaiMetrics.LanternScareRadius;
            if (hurt || close) {
                NPC.velocity.X = 0f;
                ChangeState(StateTurn);
            }
        }

        /// <summary>跟随计量：MP 语义「任一玩家在带内即 +」，带外衰减，钳上限</summary>
        private void JudgeFollowMeter() {
            if ((int)State > StateWalk
                || (int)AmbientClock % KiyumeYokaiMetrics.LanternFollowJudgeTicks != 0) {
                return;
            }
            bool inBand = false;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float d = player.Distance(NPC.Center);
                if (d >= KiyumeYokaiMetrics.LanternFollowBandNear
                    && d <= KiyumeYokaiMetrics.LanternFollowBandFar) {
                    inBand = true;
                    break;
                }
            }
            StateParam = MathHelper.Clamp(
                StateParam + (inBand ? KiyumeYokaiMetrics.LanternFollowGain
                                     : -KiyumeYokaiMetrics.LanternFollowLoss),
                0f, KiyumeYokaiMetrics.LanternFollowCap);
            //计量不逐拍置 netUpdate：低频 SyncPacer 过线，Arrive 转移包里带到精确值
        }

        /// <summary>冷握：只结算 GripRange 内最近者（远程惊扰者不奖不罚，灯照灭）。<br/>
        /// 服务器 Hurt 只写本端镜像（对源：Player.Hurt 发包分支仅 netMode==1 且 myPlayer，
        /// 服务器直调零包，受害端下一次上行同步还会把镜像改回去）——须显式广播 HurtInfo；
        /// 服务器 AddBuff 同理不自发包（Player.cs L5698 仅 netMode==1），显式补 55 号</summary>
        private void SettleGrip() {
            Player victim = NearestLivePlayer(out float dist);
            if (victim == null || dist > KiyumeYokaiMetrics.LanternGripRange) {
                return;
            }
            int damage = Math.Max(1, (int)(victim.statLifeMax2 * KiyumeYokaiMetrics.LanternGripFrac));
            int dir = victim.Center.X < NPC.Center.X ? -1 : 1;
            double dealt = victim.Hurt(PlayerDeathReason.ByCustomReason(
                gripDeathReason.ToNetworkText(victim.name)), damage, dir, out Player.HurtInfo info);
            if (dealt > 0.0 && VaultUtils.isServer) {
                NetMessage.SendPlayerHurt(victim.whoAmI, info);
            }
            victim.AddBuff(BuffID.Darkness, KiyumeYokaiMetrics.LanternGripDarkTicks);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.AddPlayerBuff, -1, -1, null,
                    victim.whoAmI, BuffID.Darkness, KiyumeYokaiMetrics.LanternGripDarkTicks);
            }
        }

        /// <summary>目的地：{最近井口, 主坟, 滩涂水线} 距生成位 ≥DestMinDist 的最近者；
        /// 锚点全空回退「向枯林方向 1200px」。列表只在服务器有值（生成端写入）</summary>
        private void InitDestination() {
            destInit = true;
            stuckRefX = NPC.Center.X;
            Vector2 origin = NPC.Center;
            float bestDist = float.MaxValue;
            int bestIdx = 3;
            float groveCenterX = (KiyumeMetrics.GroveLeft + KiyumeMetrics.GroveCols * 0.5f) * 16f;
            float fallDir = groveCenterX >= origin.X ? 1f : -1f;
            float bestX = origin.X + fallDir * KiyumeYokaiMetrics.LanternFallbackDestPx;

            foreach (Point well in KiyumeStructures.WellMouths) {
                Consider(new Vector2(well.X * 16f + 8f, well.Y * 16f), 0,
                    origin, ref bestDist, ref bestIdx, ref bestX);
            }
            if (KiyumeStructures.GraveMain is Point grave) {
                Consider(new Vector2(grave.X * 16f + 8f, grave.Y * 16f), 1,
                    origin, ref bestDist, ref bestIdx, ref bestX);
            }
            float waterX = KiyumeMetrics.WaterRightPx - KiyumeYokaiMetrics.LanternWaterEdgeBackPx;
            Consider(new Vector2(waterX, KiyumePlans.FloorTopAt((int)(waterX / 16f)) * 16f), 2,
                origin, ref bestDist, ref bestIdx, ref bestX);

            destX = bestX;
            StackCount = bestIdx;
            NPC.netUpdate = true;
        }

        private static void Consider(Vector2 candidate, int idx, Vector2 origin,
            ref float bestDist, ref int bestIdx, ref float bestX) {
            float d = Vector2.Distance(candidate, origin);
            if (d < KiyumeYokaiMetrics.LanternDestMinDist || d >= bestDist) {
                return;
            }
            bestDist = d;
            bestIdx = idx;
            bestX = candidate.X;
        }

        /// <summary>卡步计：20t 一测，累计 LanternStuckArriveTicks 走不动就地当到点</summary>
        private bool StuckTooLong() {
            if ((int)AmbientClock % 20 == 0) {
                if (Math.Abs(NPC.Center.X - stuckRefX) < 4f) {
                    stuckTicks += 20;
                }
                else {
                    stuckTicks = 0;
                    stuckRefX = NPC.Center.X;
                }
            }
            return stuckTicks >= KiyumeYokaiMetrics.LanternStuckArriveTicks;
        }

        /// <summary>不可击杀获利：血线归零只是惊扰的另一个入口，无掉落无播报</summary>
        public override bool CheckDead() {
            NPC.life = 1;
            if (!VaultUtils.isClient && (int)State < StateTurn) {
                ChangeState(StateTurn);
            }
            return false;
        }

        //==================== 贴地与工具 ====================

        /// <summary>贴地滑行：探针失败保持高度（HoundShade 同款，宁飘不瞬移）；
        /// 两端各自探（tile 已同步，结果确定一致）</summary>
        private void GroundGlide() {
            if (TryFindGround(NPC.Center.X, NPC.Bottom.Y - 48f, out float ground)) {
                float targetY = ground - NPC.height;
                NPC.position.Y = MathHelper.Lerp(NPC.position.Y, targetY, 0.25f);
            }
        }

        //从起始高度向下探地表（镜像 KiyumeHoundShade）
        private static bool TryFindGround(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 60; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        /// <summary>转移保计量：ChangeState 会清 ai[2]，跟随计量要跨态存活</summary>
        private void AdvanceState(int state) => ChangeState(state, StateParam);

        private Player NearestLivePlayer(out float dist) {
            Player best = null;
            dist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float d = player.Distance(NPC.Center);
                if (d < dist) {
                    dist = d;
                    best = player;
                }
            }
            return best;
        }

        //==================== 表现（各端本地，由 ai 重放） ====================

        /// <summary>状态变迁沿音画（各端本地重放；迟入端首帧也吃到一次沿）</summary>
        private void PlayStateCue() {
            switch ((int)State) {
                case StateTurn:
                    //停步回头：帽檐下一声很低的呻吟
                    SoundEngine.PlaySound(SoundID.ZombieMoan with {
                        Volume = 0.45f,
                        Pitch = -0.72f,
                        MaxInstances = 2
                    }, NPC.Center);
                    break;
                case StateDissolve:
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                        Volume = 0.4f,
                        Pitch = -0.35f,
                        MaxInstances = 2
                    }, NPC.Center);
                    if ((int)StateParam == 1) {
                        //冷握拍：很冷的手，摸过就收
                        SoundEngine.PlaySound(SoundID.NPCHit54 with {
                            Volume = 0.55f,
                            Pitch = -0.5f,
                            MaxInstances = 2
                        }, NPC.Center);
                    }
                    EmitDissolveMist((int)StateParam == 1 ? 12 : 8);
                    break;
            }
        }

        private void EmitDissolveMist(int count) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + Main.rand.NextVector2Circular(14f, 20f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.55f, -0.1f)),
                    new Color(196, 188, 178), Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(50, 80));
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            //纸身受击：几缕灰屑，无血（他不是肉）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Smoke, hit.HitDirection * 1.2f, -0.6f, 120, new Color(210, 202, 190), 0.9f);
                dust.noGravity = true;
            }
        }

        private void UpdatePresentation() {
            //现形语法：远处走浓度项（雾里剪影），入遭遇带强制现形；转身起不许再藏
            float fog = FogRevealTerm(NPC.Center);
            NearestLivePlayer(out float dist);
            float engage = 1f - DistanceRevealTerm(dist, RevealEngageNearPx, RevealEngageFarPx);
            float target = MathHelper.Lerp(fog, 1f, engage);
            if ((int)State >= StateTurn) {
                target = Math.Max(target, 0.92f);
            }
            presentAlpha = MathHelper.Lerp(presentAlpha, MathHelper.Clamp(target, 0f, 1f), 0.08f);

            //提灯暖光：转身渐灭，挂灯后由悬灯 prop 接光
            float flame = FlameLevel();
            if (!Main.dedServ && flame > 0.01f) {
                float mul = KiyumeYokaiMetrics.LanternLightMul * 0.72f * flame;
                Lighting.AddLight(LampWorldPos(),
                    KiyumeYokaiMetrics.LanternLightR * mul,
                    KiyumeYokaiMetrics.LanternLightG * mul,
                    KiyumeYokaiMetrics.LanternLightB * mul);
            }
        }

        /// <summary>灯焰强度：状态×计时的确定函数（各端一致）；挂出后手上无火</summary>
        private float FlameLevel() {
            if (lampHanded) {
                return 0f;
            }
            int t = (int)StateTimer;
            switch ((int)State) {
                case StateTurn:
                    //回头即灭灯：公平前摇的另一半可读性
                    return MathHelper.Lerp(0.58f, 0f,
                        Math.Min(1f, t / (float)KiyumeYokaiMetrics.LanternTurnTicks));
                case StateDissolve:
                    return 0f;
                case StateArrive:
                    if (StateParam >= KiyumeYokaiMetrics.LanternFollowGoal) {
                        //举灯渐亮到交接拍，交接后灯在悬灯手里
                        return t >= ArriveHangBeatTick
                            ? 0f : 0.58f + 0.5f * (t / (float)ArriveHangBeatTick);
                    }
                    return 0.58f;
                default:
                    return 0.55f + 0.06f * MathF.Sin(AmbientClock * 0.06f + Seed);
            }
        }

        /// <summary>灯是否还提在手里：挂出后与化雾中都不画（灯灭了，哪都没有灯了）</summary>
        private bool LampCarried() {
            if (lampHanded || (int)State == StateDissolve) {
                return false;
            }
            return !((int)State == StateArrive
                && StateParam >= KiyumeYokaiMetrics.LanternFollowGoal
                && (int)StateTimer >= ArriveHangBeatTick);
        }

        private Vector2 LampWorldPos() {
            //灯提在行进方向前手；挂灯变体里随拍举高
            float lift = 0f;
            if ((int)State == StateArrive && StateParam >= KiyumeYokaiMetrics.LanternFollowGoal) {
                lift = MathHelper.Clamp((int)StateTimer / (float)ArriveHangBeatTick, 0f, 1f) * 26f;
            }
            return NPC.Center + new Vector2(facing * 11f, 4f - lift);
        }

        private float LampRotation() {
            float swing = 0.15f + 0.09f * Math.Min(1f,
                Math.Abs(NPC.velocity.X) / KiyumeYokaiMetrics.LanternWalkSpeed);
            return MathF.Sin(AmbientClock * 0.085f + Seed) * swing;
        }

        /// <summary>帽下余烬：转身 20t 内 0→峰值；冷握化雾里短暂余留后熄</summary>
        private float EyeGlow01() {
            int t = (int)StateTimer;
            if ((int)State == StateTurn) {
                return KiyumeYokaiMetrics.LanternEyeGlowMax
                    * Math.Min(1f, t / (float)KiyumeYokaiMetrics.LanternTurnTicks);
            }
            if ((int)State == StateDissolve && (int)StateParam == 1) {
                return KiyumeYokaiMetrics.LanternEyeGlowMax * Math.Max(0f, 1f - t / 20f);
            }
            return 0f;
        }

        /// <summary>纸衣蚀散：常态只碎下摆，化雾态走满</summary>
        private float Dissolve01() {
            if ((int)State == StateDissolve) {
                return MathHelper.Lerp(KiyumeYokaiMetrics.LanternPaperDissolve, 1f,
                    MathHelper.Clamp((int)StateTimer / (float)DissolveTicks, 0f, 1f));
            }
            return KiyumeYokaiMetrics.LanternPaperDissolve;
        }

        //==================== 绘制（全接管） ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态自愈：先归位默认批（netcode 7.2 教训）
            BeginDefault(spriteBatch);
            DrawPaperBody(spriteBatch, screenPos);
            if (LampCarried()) {
                DrawCarriedLamp(spriteBatch, screenPos, drawColor);
            }
#if DEBUG
            Utils.DrawBorderString(spriteBatch,
                $"计量 {(int)StateParam}  状态 {(int)State}",
                NPC.Top - screenPos + new Vector2(-30f, -36f),
                Color.LightGoldenrodYellow, 0.7f);
#endif
            return false;
        }

        private void DrawPaperBody(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (presentAlpha < 0.02f) {
                return;
            }
            Main.instance.LoadNPC(NPCID.TheGroom);
            Texture2D tex = TextureAssets.Npc[NPCID.TheGroom].Value;
            if (tex == null) {
                return;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.TheGroom];
            int frameTop = Math.Clamp(NPC.frame.Y, 0, tex.Height - frameH);
            //源矩形上下各内缩 1px + shader 帧界钳制，双通道防帧表渗色
            var source = new Rectangle(0, frameTop + 1, tex.Width, frameH - 2);
            var topLeft = new Vector2(NPC.Center.X - tex.Width * 0.5f,
                NPC.Bottom.Y + 2f - source.Height);

            Effect fx = EffectLoader.KiyumeKaidan?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                //着色器缺编：近白剪影平涂回退（HoundShade 同款语义）
                SpriteEffects flip = facing > 0
                    ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                spriteBatch.Draw(tex, topLeft - screenPos, source,
                    new Color(214, 206, 196) * (presentAlpha * 0.85f),
                    0f, Vector2.Zero, 1f, flip, 0f);
                return;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            fx.Parameters["uFlipH"]?.SetValue(facing > 0 ? 1f : 0f);
            fx.Parameters["uFlipV"]?.SetValue(0f);
            fx.Parameters["uEyeGlow"]?.SetValue(EyeGlow01());
            fx.Parameters["uEyeAnchor"]?.SetValue(EyeAnchor);
            fx.Parameters["uDissolve"]?.SetValue(Dissolve01());
            fx.Parameters["uEdgeTint"]?.SetValue(EdgeTint);
            fx.Parameters["uPaperTint"]?.SetValue(PaperTint);
            fx.Parameters["uFaceRect"]?.SetValue(Vector4.Zero);   //纸衣 pass 不吃面区，显式清零
            fx.CurrentTechnique = fx.Techniques["TechPaperGhost"];
            fx.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(tex, topLeft - screenPos, source,
                Color.White * MathHelper.Clamp(presentAlpha * 1.25f, 0f, 1f),
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            BeginDefault(spriteBatch);
            gd.Textures[1] = null;
        }

        /// <summary>提灯：ChainLantern 物品贴图绕提手摆锤（镜像 LanternWarden 画法）+ 双层暖辉</summary>
        private void DrawCarriedLamp(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadItem(ItemID.ChainLantern);
            Texture2D lantern = TextureAssets.Item[ItemID.ChainLantern]?.Value;
            if (lantern == null) {
                return;
            }
            float rot = LampRotation();
            float flame = FlameLevel();
            //灯先于人影可见：灯体透明度带抬升
            float lampAlpha = MathHelper.Clamp(presentAlpha * 1.6f, 0f, 1f);
            Vector2 anchor = LampWorldPos();
            Vector2 origin = new(lantern.Width * 0.5f, 2f);
            Color bodyCol = drawColor.MultiplyRGB(new Color(232, 205, 165)) * lampAlpha;
            spriteBatch.Draw(lantern, anchor - screenPos, null, bodyCol,
                rot, origin, 0.8f, SpriteEffects.None, 0f);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || flame <= 0.01f) {
                return;
            }
            //加色批：强度写进色值整体（A 随乘法收缩），禁 A=0
            BeginAdditive(spriteBatch);
            Vector2 flamePos = anchor + new Vector2(0f, 12f).RotatedBy(rot);
            Vector2 gOrigin = glow.Size() * 0.5f;
            spriteBatch.Draw(glow, flamePos - screenPos, null,
                new Color(255, 178, 92) * (0.5f * flame * lampAlpha), 0f, gOrigin,
                new Vector2(24f * 2f / glow.Width), SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, flamePos - screenPos, null,
                new Color(255, 228, 168) * (0.4f * flame * lampAlpha), 0f, gOrigin,
                new Vector2(10f * 2f / glow.Width), SpriteEffects.None, 0f);
            BeginDefault(spriteBatch);
        }
    }

    /// <summary>
    /// 悬灯（提灯翁的报偿 prop）：不可击打零伤害，存活 LanternRewardLife；
    /// 每帧本地续订清雾圈（Suppression 本就是本地表现，简报 §3）+ 暖光，
    /// 尾段渐熄时清雾强度同步收口（雾合拢回来）。寿命各端同式推演，服务器同步兜底
    /// </summary>
    internal class LanternGuideHungLamp : KiyumeYokaiNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //渐显 / 渐熄（纯演出）
        private const int FadeInTicks = 20;
        private const int FadeOutTicks = 60;

        protected override void SetYokaiDefaults() {
            NPC.width = 18;
            NPC.height = 26;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 5;
            NPC.dontTakeDamage = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
        }

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;
            ServerSyncPacer(60);   //静物低频锚
            NPC.velocity = Vector2.Zero;
            StateTimer++;

            float fade = LifeFade01();
            if (!Main.dedServ) {
                KiyumeFogSuppression.RequestCircle(NPC.Center,
                    KiyumeYokaiMetrics.LanternRewardRadius,
                    KiyumeYokaiMetrics.LanternRewardTtl,
                    KiyumeYokaiMetrics.LanternRewardFeatherPx,
                    KiyumeYokaiMetrics.LanternRewardStrength * fade);
                float flick = 0.92f + 0.08f * MathF.Sin(AmbientClock * 0.11f + Seed);
                float mul = KiyumeYokaiMetrics.LanternLightMul * flick * fade;
                Lighting.AddLight(NPC.Center,
                    KiyumeYokaiMetrics.LanternLightR * mul,
                    KiyumeYokaiMetrics.LanternLightG * mul,
                    KiyumeYokaiMetrics.LanternLightB * mul);
                //偶发一粒灯口余烬（火要有物证）
                if (Main.rand.NextBool(40)) {
                    Dust dust = Dust.NewDustPerfect(NPC.Top + new Vector2(0f, 12f),
                        DustID.Torch, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.35f),
                        150, default, Main.rand.NextFloat(0.5f, 0.8f));
                    dust.noGravity = true;
                }
            }

            if (StateTimer >= KiyumeYokaiMetrics.LanternRewardLife) {
                NPC.active = false;
            }
        }

        private float LifeFade01() {
            float t = StateTimer;
            float fadeIn = MathHelper.Clamp(t / FadeInTicks, 0f, 1f);
            float fadeOut = MathHelper.Clamp(
                (KiyumeYokaiMetrics.LanternRewardLife - t) / FadeOutTicks, 0f, 1f);
            return Math.Min(fadeIn, fadeOut);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            BeginDefault(spriteBatch);
            Main.instance.LoadItem(ItemID.ChainLantern);
            Texture2D lantern = TextureAssets.Item[ItemID.ChainLantern]?.Value;
            if (lantern == null) {
                return false;
            }
            float fade = LifeFade01();
            float rot = MathF.Sin(AmbientClock * 0.045f + Seed) * 0.06f;
            Vector2 origin = new(lantern.Width * 0.5f, 2f);
            Color bodyCol = drawColor.MultiplyRGB(new Color(235, 210, 170)) * fade;
            spriteBatch.Draw(lantern, NPC.Top - screenPos, null, bodyCol,
                rot, origin, 0.9f, SpriteEffects.None, 0f);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && fade > 0.01f) {
                BeginAdditive(spriteBatch);
                Vector2 flamePos = NPC.Top + new Vector2(0f, 14f).RotatedBy(rot) - screenPos;
                Vector2 gOrigin = glow.Size() * 0.5f;
                float flick = 0.9f + 0.1f * MathF.Sin(AmbientClock * 0.13f + Seed * 2f);
                spriteBatch.Draw(glow, flamePos, null,
                    new Color(255, 178, 92) * (0.55f * fade * flick), 0f, gOrigin,
                    new Vector2(30f * 2f / glow.Width), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, flamePos, null,
                    new Color(255, 228, 168) * (0.42f * fade * flick), 0f, gOrigin,
                    new Vector2(12f * 2f / glow.Width), SpriteEffects.None, 0f);
                BeginDefault(spriteBatch);
            }
            return false;
        }
    }
}
