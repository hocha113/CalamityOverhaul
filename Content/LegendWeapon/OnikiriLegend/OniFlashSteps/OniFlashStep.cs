using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OKF = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps.OniKamuiFlowRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps
{
    /// <summary>
    /// 神威疾走主控：零前摇零后摇的按住可控冲刺 + 延迟居合结算。<br/>
    /// 手感契约：按键帧=位移第一帧，操作锁定只有冲刺本身（900px 全程 ~10 帧），
    /// 之后立刻交还操控——一切华丽的东西都是非阻塞的事后余像，自己播完自己。<br/>
    /// 长度控制走缓起加速曲线（<see cref="DashSpeedRamp"/>）：起步低位移帧就是
    /// "点按还是长按"的输入辨义窗——点按只花掉便宜的起步帧（最小跳距 ~160-260px），
    /// 按住则加速走满全程；松开右键即刹停；撞墙直线斩停（子步扫描，轨迹恒直，墨溅上墙）。<br/>
    /// 时间轴（60fps，距离 900 全程约 0.85s 演出 / 0.17s 锁定）：<br/>
    /// 0 爆发起步、出发点墨爆、布帛撕裂+风切+低太鼓 →
    /// 巡航推进（撞墙/松手提前止步）、身后神威流带逐帧延伸、穿过的敌人缠上墨痕
    /// （无伤害）+ 微时停 → 硬刹带过冲回弹 → 交还操控 →
    /// 纳刀帧（按计划距离恒定）"锵"，全部墨痕同帧裂开结算 →
    /// 流带从尾端化墨蒸发（~22 帧），烟屑沿蒸发前沿剥落。<br/>
    /// 视觉语言：绯红偏黑红的流动墨绸（详见 <see cref="OniKamuiFlowRenderer"/>），
    /// 与雷电/科技无关——鬼切的神威是墨与绸，不是电。<br/>
    /// ai[0]=瞄准角(弧度) ai[1]=冲刺距离(px) ai[2]=尺寸倍率；伤害经 damage 传入墨痕全额结算
    /// </summary>
    internal class OniFlashStep : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable, IOverlayDrawable, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.Placeholder;

        //==== 时间轴常量 ====
        //缓起加速曲线：把"点按还是长按"的输入辨义窗叠进低位移的起步帧——
        //起步走得便宜（点按只花掉这几帧，最小跳距压到 ~160-260px），
        //输入表明意图后再进高速段，长按 900px 全程约 10 帧（~170ms），总时长不增反缩。
        //旧的爆发前置（头 2 帧 260px）把距离花在了松开信号还没到达的盲区里
        private static readonly float[] DashSpeedRamp = [35f, 50f, 70f, 100f, 125f];  //px/帧，末值为巡航速度
        private const int BrakeFrames = 2;      //硬刹：+过冲 → −回拉
        private const int JudgmentDelay = 8;    //刹停到纳刀结算
        private const int RetractDelay = 10;    //刹停到流带开始蒸发
        private const int RetractFrames = 22;   //蒸发时长
        private const int MaxMarks = 24;        //单次冲刺标记上限
        private const int NotoFlickFrames = 6;  //纳刀一挑时长(起于纳刀结算帧,与"锵"同步)
        private const int TailFadeFrames = 8;   //纳刀后持刀淡出
        //==== 位移与判定常量 ====
        private const float CollisionSubStep = 14f; //直线斩停子步长(小于玩家宽度,防隧穿)
        /// <summary>巡航段方向键转向速率(弧度/帧)：全程合计约 ±28° 的小幅弯曲——
        /// 响应感来自"手上的键确实在掰弯这道墨"，幅度收着防拐成蛇形</summary>
        private const float SteerRate = 0.155f;
        private const float SweepLead = 44f;        //扫掠前导:冲刺终点脸前的目标不漏标
        private const float SweepBackPad = 24f;     //扫掠后补:起手贴脸的目标不漏标
        private const float MarkSweepWidth = 140f;  //扫掠走廊宽(对齐墨绸视觉宽度,玩家"明明穿过了"的判断依据是那条彩带)

        /// <summary>A/B：冲刺期隐藏本地玩家（"人化作一道神威"的完全体），默认关</summary>
        public static bool HidePlayerDuringDash => true;
        /// <summary>本地玩家当前处于冲刺隐藏帧（由 <see cref="OniFlashStepHideOverride"/> 消费）</summary>
        internal static bool LocalPlayerHidden;

        private readonly List<Vector2> path = new(16);
        private readonly HashSet<int> marked = new(16);
        private bool initialized;
        private int timer;
        private Vector2 dashDir;
        private float traveled;
        private float seed;
        private float sizeMul = 1f;
        private int plannedDashFrames;
        private int stopFrame = -1;      //刹停帧（操控交还帧）
        private bool judged;
        private float headExt;           //刹停后流带头端 follow-through 残余外推
        /// <summary>被墙面斩停：刹车改回弹、头端预算清零、墨溅上墙</summary>
        private bool wallStopped;
        /// <summary>流带头端超前身体的距离（px），停止时按身前自由空间 clamp——墨最多亲到墙面，永不入墙</summary>
        private float headOffset = 100f;

        private bool Dashing => stopFrame < 0 && timer <= plannedDashFrames;
        private bool Braking => stopFrame < 0 && timer > plannedDashFrames;
        /// <summary>纳刀结算的绝对帧：按计划距离恒定，撞墙早停只是"锵"前多一拍死寂，节奏不散</summary>
        private int JudgmentFrame => plannedDashFrames + BrakeFrames + JudgmentDelay;

        //收尾残心/纳刀的实体刀(纯视觉,非阻塞)
        private readonly OniBladePose bladePose = new();

        /// <summary>位移+刹车段硬占刀权:人已化入神威,连段就地冻结让位</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => stopFrame < 0;

        /// <summary>挥空后的保留余量(帧):没东西可演就不收税,只留极短落地拍</summary>
        private const int WhiffReserveFrames = 2;

        /// <summary>
        /// 刹停后的签名拍软保留：残心 → 纳刀一挑期间连段不得重启夺刀（输入按住即缓冲，
        /// 窗口一关自动续接）——这声"锵"与墨痕齐裂是疾走的 payoff，值得眼睛读完；
        /// 挥空（无墨痕）提前释放；位移/技能/肢解点选不受保留影响
        /// </summary>
        bool IOniBladeOccupant.ReservesBlade => stopFrame >= 0
            && timer <= (marked.Count == 0 ? stopFrame + WhiffReserveFrames : JudgmentFrame + NotoFlickFrames);

        private Player Owner => Main.player[Projectile.owner];
        private float DashAngle => Projectile.ai[0];
        private float Distance => Projectile.ai[1] > 60f ? Projectile.ai[1] : 900f;

        /// <summary>
        /// 触发接口：在持有者客户端调用（<c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步；按下即出发，无任何前摇
        /// </summary>
        /// <param name="player">冲刺者</param>
        /// <param name="aim">冲刺方向（无需归一化）</param>
        /// <param name="damage">墨痕引爆伤害（每个被穿过的敌人全额一次）</param>
        /// <param name="knockback">击退</param>
        /// <param name="distance">冲刺距离(px)，撞墙提前止步</param>
        /// <param name="scale">尺寸倍率（流带幅宽/粒子随之缩放）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 aim, int damage, float knockback,
            float distance = 900f, float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFlashStep");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX * player.direction).ToRotation();
            return Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<OniFlashStep>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai1: distance, ai2: scale);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;   //主控无判定，伤害全在墨痕
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;     //Initialize 按计划帧重设
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private Vector2 GetCenter() => Owner.Center + dashDir * headOffset;

        private void Initialize() {
            initialized = true;
            dashDir = DashAngle.ToRotationVector2();
            sizeMul = Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;
            seed = Projectile.identity * 0.6180339887f % 1f;
            //沿加速曲线累计到计划距离
            plannedDashFrames = 0;
            for (float acc = 0f; acc < Distance - 0.5f; plannedDashFrames++) {
                acc += DashSpeedRamp[Math.Min(plannedDashFrames, DashSpeedRamp.Length - 1)];
            }
            plannedDashFrames = Math.Max(plannedDashFrames, 2);
            Projectile.timeLeft = JudgmentFrame + RetractDelay + RetractFrames + 30;

            path.Add(GetCenter());
            if (Owner.whoAmI == Main.myPlayer) {
                Owner.RemoveAllGrapplingHooks();
            }

            //出发即巅峰：布帛撕裂 + 风切 + 低太鼓，没有任何充能音
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.78f, Volume = 0.90f }, GetCenter());
            SoundEngine.PlaySound(CWRSound.SwiftSlice with { Pitch = -0.05f, Volume = 0.80f }, GetCenter());
            SoundEngine.PlaySound(CWRSound.KatanaSprint with { Pitch = -0.72f, Volume = 0.62f }, GetCenter());
            Owner.CWR().GetScreenShake(4f);

            SpawnOriginInkBurst();

            //只设置冲刺玩家的镜头，不要把别的玩家的镜头也设置了
            if (Projectile.IsOwnedByLocalPlayer() && CWRServerConfig.Instance.LensEasing) {
                Main.SetCameraLerp(0.12f, 20);
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
            }
            timer++;

            if (!Owner.active || Owner.dead) {
                ReleaseHide();
                Projectile.Kill();
                return;
            }

            if (Dashing) {
                DashFrame();
            }
            else if (Braking) {
                BrakeFrame();
            }
            else {
                headExt *= 0.80f;   //follow-through 残余回缩
            }

            if (!judged && timer >= JudgmentFrame) {
                Judge();
            }

            UpdateTailPose();
            UpdateHideState();
            SpawnRetractWisps();
            PushScreenState();

            //路径头/中段常驻微光
            if (path.Count >= 2) {
                Lighting.AddLight(path[^1], new Vector3(0.95f, 0.22f, 0.16f));
                Lighting.AddLight(OKF.PointAlong(path, 0.5f), new Vector3(0.5f, 0.10f, 0.09f));
            }
        }

        //==================== 位移 ====================

        /// <summary>
        /// 冲刺帧：~150px 定距推进，位移、标记、姿态、无敌帧。<br/>
        /// 碰撞走"直线斩停"：沿冲刺向子步扫描（一格台阶容差），位移严格共线——
        /// 轨迹几何上不可能弯（TileCollision 的分轴滑动是弯轨/贴地滑行的根源，弃用）；
        /// 撞墙即停止事件（回弹刹车 + 墨溅上墙），owner 松开右键也提前收势（轻点短刺，按住全程）
        /// </summary>
        private void DashFrame() {
            Vector2 prevHead = GetCenter();
            Vector2 fromBody = Owner.Center;

            //方向键微转向：按住的方向把墨绸小幅掰弯（首帧保持出手直线，转向随控制位同步各端）
            if (timer > 1) {
                int h = (Owner.controlRight ? 1 : 0) - (Owner.controlLeft ? 1 : 0);
                int v = (Owner.controlDown ? 1 : 0) - (Owner.controlUp ? 1 : 0);
                if (h != 0 || v != 0) {
                    float delta = MathHelper.WrapAngle(new Vector2(h, v).ToRotation() - dashDir.ToRotation());
                    dashDir = (dashDir.ToRotation() + MathHelper.Clamp(delta, -SteerRate, SteerRate))
                        .ToRotationVector2();
                }
            }

            float speed = DashSpeedRamp[Math.Min(timer - 1, DashSpeedRamp.Length - 1)];
            float stepLen = MathF.Min(speed, Distance - traveled);

            //直线斩停子步推进
            float moved = 0f;
            bool blocked = false;
            while (moved < stepLen - 0.01f) {
                float sub = MathF.Min(CollisionSubStep, stepLen - moved);
                Vector2 next = Owner.position + dashDir * sub;
                if (!Collision.SolidCollision(next, Owner.width, Owner.height)) {
                    Owner.position = next;
                    moved += sub;
                    continue;
                }
                //一格台阶容差：抬升一格可过则继续——地面小台阶不打断冲刺；
                //16px 竖向微错位在 path 点距(≥64px)下彩带读不出折角
                Vector2 lifted = next - Vector2.UnitY * (16f * Owner.gravDir);
                if (!Collision.SolidCollision(lifted, Owner.width, Owner.height)) {
                    Owner.position = lifted;
                    moved += sub;
                    continue;
                }
                blocked = true;
                break;
            }

            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            traveled += moved;
            Owner.GivePlayerImmuneState(10);
            HoldPose();

            //松手提前收势，无下限——松手即停，最短一帧微刺也认（owner 端意图，
            //缩短后的距离回写 ai[1] 同步远端按距离条件自然停下）
            bool released = Projectile.IsOwnedByLocalPlayer() && !Main.mouseRight;
            bool finished = blocked || released || traveled >= Distance - 1f;
            if (finished) {
                if (blocked && !wallStopped) {
                    wallStopped = true;
                    WallSplat();
                }
                if (released && Projectile.owner == Main.myPlayer && traveled < Distance - 1f) {
                    Projectile.ai[1] = MathF.Max(traveled, 61f);
                    Projectile.netUpdate = true;
                }
                //任何停止都按身前自由空间收拢头端：墨最多亲到墙面，永不入墙
                headOffset = MathF.Min(headOffset, MathF.Max(FreeAheadBudget() - 6f, 8f));
                timer = Math.Max(timer, plannedDashFrames);
            }

            //撞墙帧不塞重合点，避免流带出现退化段
            if (Vector2.DistanceSquared(path[^1], GetCenter()) > 64f) {
                path.Add(GetCenter());
            }

            //扫掠锚定身体并带前导/后补：起手贴脸与终点脸前的目标都不漏
            MarkSweep(fromBody - dashDir * SweepBackPad, Owner.Center + dashDir * SweepLead);

            if (!Main.dedServ && moved > 1f) {
                SpawnDashWisps(prevHead, GetCenter());
            }
        }

        /// <summary>身前沿冲刺向的自由距离（px，扫描上限盖住 头端+外推 预算）</summary>
        private float FreeAheadBudget() {
            const float MaxScan = 132f;
            float d = 8f;
            while (d < MaxScan) {
                Vector2 probe = Owner.Center + dashDir * d;
                if (Collision.SolidCollision(probe - new Vector2(2f, 2f), 4, 4)) {
                    return d;
                }
                d += 8f;
            }
            return MaxScan;
        }

        /// <summary>撞墙的落点反馈：墨溅上墙（贴墙横向铺开）+ 闷响 + 震屏——150px/帧的身体撞在墙上应该有一声"咚"</summary>
        private void WallSplat() {
            Vector2 contact = Owner.Center + dashDir * MathF.Max(FreeAheadBudget() - 4f, 8f);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.75f, Pitch = -0.55f, MaxInstances = 2 }, contact);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 2 }, contact);
            Owner.CWR().GetScreenShake(3.5f);

            if (Main.dedServ) {
                return;
            }
            //墨沿墙面（垂直冲刺向）溅开：动能没有消失，只是换了方向
            Vector2 perp = new(-dashDir.Y, dashDir.X);
            for (int i = 0; i < 10; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 vel = perp * side * Main.rand.NextFloat(2f, 6.5f) - dashDir * Main.rand.NextFloat(0.4f, 1.6f);
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(contact + perp * side * Main.rand.NextFloat(0f, 18f)
                    , vel, Color.White, Main.rand.NextFloat(0.07f, 0.13f) * sizeMul)
                    ?.Configure(Main.rand.Next(18, 30), new Color(115, 24, 32), new Color(28, 13, 21));
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = (-dashDir).RotatedByRandom(0.85) * Main.rand.NextFloat(2.5f, 7f);
                PRTLoader.NewParticle<PRT_OniShard>(contact, vel, new Color(255, 116, 66)
                    , Main.rand.NextFloat(0.3f, 0.55f) * sizeMul)
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.2f, 0.2f)
                        , Main.rand.NextFloat(1.2f, 2.2f), affectedByGravity: true);
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(contact, Vector2.Zero
                , new Color(255, 190, 170), 0.7f * sizeMul);
        }

        /// <summary>硬刹两帧：+过冲 → −回拉，随后交还操控；流带头端获得 follow-through 外推。<br/>
        /// 被墙面斩停时改为反震回弹（−回弹 → +落定），墙前没有前过冲的物理空间</summary>
        private void BrakeFrame() {
            int bt = timer - plannedDashFrames;   //1..BrakeFrames
            Vector2 fromBody = Owner.Center;
            float move = wallStopped
                ? (bt == 1 ? -14f : 5f)
                : (bt == 1 ? 26f : -12f);
            //小步位移同样走共线检测,保持轨迹笔直
            Vector2 next = Owner.position + dashDir * move;
            if (!Collision.SolidCollision(next, Owner.width, Owner.height)) {
                Owner.position = next;
            }
            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);

            //过冲帧记录头端；回拉帧不回撤墨迹——身体从墨里向后挣出，墨保持前伸
            if (bt == 1 && !wallStopped && Vector2.DistanceSquared(path[^1], GetCenter()) > 16f) {
                path.Add(GetCenter());
            }
            Owner.GivePlayerImmuneState(8);
            HoldPose();

            //过冲尖端补扫：刹车段掠过的目标同样入痕
            MarkSweep(fromBody - dashDir * SweepBackPad, Owner.Center + dashDir * SweepLead);

            if (bt >= BrakeFrames) {
                stopFrame = timer;
                //follow-through 外推吃身前预算：墙前清零,墨不入墙
                headExt = MathF.Min(22f * sizeMul, MathF.Max(FreeAheadBudget() - headOffset - 4f, 0f));
                Owner.CWR().GetScreenShake(2.2f);

                if (!Main.dedServ) {
                    //刹停点几缕墨屑落定
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_CrimsonSmoke>(GetCenter() + Main.rand.NextVector2Circular(18f, 24f)
                            , dashDir * Main.rand.NextFloat(0.6f, 1.8f) + Main.rand.NextVector2Circular(0.5f, 0.5f)
                            , Color.White, Main.rand.NextFloat(0.05f, 0.09f) * sizeMul)
                            ?.Configure(Main.rand.Next(16, 26), new Color(120, 26, 34), new Color(30, 14, 22));
                    }
                }
            }
        }

        /// <summary>冲刺/刹车期持械姿态：角色读作"低姿态突进"，不占用物品使用（朝向跟随实时转向）</summary>
        private void HoldPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            if (MathF.Abs(dashDir.X) >= 0.05f) {
                Owner.ChangeDir(dashDir.X > 0f ? 1 : -1);
            }
            Owner.itemRotation = (dashDir * Owner.direction).ToRotation();
        }

        //==================== 标记 ====================

        /// <summary>本帧扫掠段上的敌人缠上墨痕（无伤害）：微时停 + 穿身墨屑，结算全部押后到纳刀帧</summary>
        private void MarkSweep(Vector2 from, Vector2 to) {
            if (marked.Count >= MaxMarks) {
                return;
            }
            float sweepWidth = MarkSweepWidth * sizeMul;
            int judgeDelay = JudgmentFrame - timer;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (marked.Contains(npc.whoAmI) || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float cp = 0f;
                if (!Collision.CheckAABBvLineCollision(npc.Hitbox.TopLeft(), npc.Hitbox.Size()
                    , from, to, sweepWidth, ref cp)) {
                    continue;
                }

                marked.Add(npc.whoAmI);
                npc.CWR().TimeFrozenTick = 3;   //穿身微滞：世界不停，只有被穿者顿一下

                if (Projectile.IsOwnedByLocalPlayer()) {
                    //墨痕走向对齐穿过瞬间的实时方向（转向后仍与轨迹一致）
                    OniFlashMark.Fire(Owner, npc, judgeDelay, Projectile.damage
                        , Projectile.knockBack, dashDir.ToRotation(), Projectile.GetSource_FromAI());
                    //穿身即格挡:居合掠过之敌为主人蓄势(封顶/蠕虫去重在资源层)
                    Owner.GetModPlayer<OnikiriPlayer>().OnDashParry(npc);
                }

                SoundEngine.PlaySound(SoundID.Item71 with {
                    Pitch = 0.55f + marked.Count * 0.04f,
                    Volume = 0.30f,
                }, npc.Center);

                if (!Main.dedServ) {
                    for (int i = 0; i < 5; i++) {
                        Vector2 vel = dashDir.RotatedByRandom(0.5) * Main.rand.NextFloat(3f, 8f);
                        PRTLoader.NewParticle<PRT_CrimsonSpark>(npc.Center, vel, new Color(255, 110, 70)
                            , Main.rand.NextFloat(0.3f, 0.5f) * sizeMul)
                            ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
                    }
                }

                if (marked.Count >= MaxMarks) {
                    return;
                }
            }
        }

        /// <summary>纳刀帧："锵"一声，墨痕们（各自对齐本帧）同时裂开；主控只负责声与光的确认。<br/>
        /// 齐裂的重量随痕数升档（震屏/白闪/群裂闷爆）——死寂越久、穿得越多，那一声就该越响</summary>
        private void Judge() {
            judged = true;
            if (marked.Count == 0) {
                return;   //挥空不响鞘：死寂本身就是收势
            }
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.10f, Volume = 0.55f }, GetCenter());
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.35f, Volume = 0.22f }, GetCenter());
            if (marked.Count >= 3) {
                //群裂低频垫底：单声限流，不随痕数叠加防爆音
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.62f, Volume = 0.55f, MaxInstances = 1 }, GetCenter());
            }
            CrimsonImpactFX.PushImpact(GetCenter(), MathF.Min(0.02f + marked.Count * 0.008f, 0.07f));
            Owner.CWR().GetScreenShake(MathF.Min(3f + marked.Count * 0.8f, 8f));
        }

        /// <summary>
        /// 刹停后的残心→纳刀(纯视觉,非阻塞)：重现身即前指残心,死寂里持刀不动,
        /// 纳刀结算帧一挑收刀与"锵"同帧——居合的身体动画补在眼睛来得及读的地方。<br/>
        /// 连段夺权(按住左键续连段)或新技能硬占时立刻放手,玩家输入永远优先
        /// </summary>
        private void UpdateTailPose() {
            bladePose.Update();
            if (stopFrame < 0 || !Owner.active || Owner.dead) {
                return;
            }
            if (timer - JudgmentFrame > NotoFlickFrames + TailFadeFrames
                || OniBladeOccupancy.ComboClaims(Owner)
                || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile)) {
                bladePose.Opacity = 0f;
                return;
            }

            //残心/纳刀沿刹停时的实时方向（转向后的最终朝向），不回读出手角
            float dirA = dashDir.ToRotation();
            int facing = dashDir.X >= 0f ? 1 : -1;
            int sinceJudge = timer - JudgmentFrame;
            if (sinceJudge <= 0) {
                //残心:刀沿冲刺向平指,极轻的呼吸下沉
                bladePose.Rotation = dirA + facing * 0.05f * MathF.Sin((timer - stopFrame) * 0.35f);
                bladePose.Opacity = 1f;
            }
            else if (sinceJudge <= NotoFlickFrames) {
                //纳刀一挑:EaseOut 干脆收刀回背(与连段收势的持刀位同一套语言)
                float t = sinceJudge / (float)NotoFlickFrames;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(dirA, dirA - facing * 1.05f, ease);
                bladePose.Opacity = 1f;
                if (sinceJudge <= 3) {
                    bladePose.PushSmear(1f - t * 0.4f);
                }
            }
            else {
                bladePose.Opacity = 1f - (sinceJudge - NotoFlickFrames) / (float)TailFadeFrames;
            }
            bladePose.ApplyPose(Owner, Projectile);
        }

        /// <summary>遮挡层：收尾残心/纳刀的实体刀,稳定盖在流带辉光之上</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        //==================== 演出状态 ====================

        private bool hideHeld;

        /// <summary>可选隐藏：冲刺+刹车期本地玩家不绘制，交还操控立刻恢复</summary>
        private void UpdateHideState() {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            if (HidePlayerDuringDash && stopFrame < 0) {
                LocalPlayerHidden = hideHeld = true;
            }
            else if (hideHeld) {
                hideHeld = false;
                LocalPlayerHidden = false;
            }
        }

        private void ReleaseHide() {
            if (hideHeld && Owner.whoAmI == Main.myPlayer) {
                hideHeld = false;
                LocalPlayerHidden = false;
            }
        }

        public override void OnKill(int timeLeft) => ReleaseHide();

        /// <summary>蒸发进度 0..1（刹停前恒 0）</summary>
        private float RetractT => stopFrame < 0 ? 0f
            : MathHelper.Clamp((timer - stopFrame - RetractDelay) / (float)RetractFrames, 0f, 1f);

        /// <summary>出发点墨爆：黑红墨浪 + 碎晶 + 一帧白闪——"人从墨里挣脱出去"</summary>
        private void SpawnOriginInkBurst() {
            CrimsonImpactFX.PushImpact(GetCenter(), 0.02f);
            if (Main.dedServ) {
                return;
            }
            Vector2 origin = GetCenter();

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.2f, 3.4f)
                    - dashDir * Main.rand.NextFloat(0.5f, 1.6f);
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(origin + Main.rand.NextVector2Circular(14f, 14f)
                    , vel, Color.White, Main.rand.NextFloat(0.09f, 0.16f) * sizeMul)
                    ?.Configure(Main.rand.Next(24, 40), new Color(110, 22, 32), new Color(26, 12, 20)
                        , Main.rand.NextFloat(0.012f, 0.028f));
            }
            for (int i = 0; i < 7; i++) {
                Vector2 vel = (-dashDir).RotatedByRandom(0.9) * Main.rand.NextFloat(3f, 9f);
                PRTLoader.NewParticle<PRT_OniShard>(origin, vel, new Color(255, 120, 70)
                    , Main.rand.NextFloat(0.35f, 0.6f) * sizeMul)
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.2f, 0.2f)
                        , Main.rand.NextFloat(1.4f, 2.4f), affectedByGravity: false);
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(origin, Vector2.Zero
                , new Color(255, 200, 185), 0.9f * sizeMul);
        }

        /// <summary>冲刺途中：新鲜路径段上剥落的墨屑（速度语言的粒子层，量随位移走）</summary>
        private void SpawnDashWisps(Vector2 from, Vector2 to) {
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Vector2.Lerp(from, to, Main.rand.NextFloat())
                    + Main.rand.NextVector2Circular(20f, 34f) * sizeMul;
                Vector2 vel = -dashDir * Main.rand.NextFloat(0.8f, 2.6f) + Main.rand.NextVector2Circular(0.6f, 0.6f);
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, Color.White
                    , Main.rand.NextFloat(0.05f, 0.10f) * sizeMul)
                    ?.Configure(Main.rand.Next(14, 24), new Color(125, 26, 34), new Color(30, 14, 22));
            }
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Vector2.Lerp(from, to, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, -dashDir * Main.rand.NextFloat(2f, 5f)
                    , new Color(255, 96, 58), Main.rand.NextFloat(0.25f, 0.45f) * sizeMul)
                    ?.Configure(Main.rand.Next(8, 14), affectedByGravity: false);
            }
        }

        /// <summary>蒸发期：烟屑沿蒸发前沿剥落，墨绸"化墨散掉"而不是原地淡出</summary>
        private void SpawnRetractWisps() {
            float t = RetractT;
            if (Main.dedServ || t <= 0f || t >= 1f || path.Count < 2) {
                return;
            }
            //对齐 shader 蒸发阈值（eTh = retract*2.3 - u*1.15）中 flow≈0.5 的等值线
            float frontU = MathHelper.Clamp((t * 2.3f - 0.5f) / 1.15f, 0f, 1f);
            Vector2 front = OKF.PointAlong(path, frontU);
            for (int i = 0; i < 2; i++) {
                Vector2 pos = front + Main.rand.NextVector2Circular(26f, 40f) * sizeMul;
                Vector2 vel = Main.rand.NextVector2Circular(0.7f, 0.7f) - Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f);
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, Color.White
                    , Main.rand.NextFloat(0.05f, 0.09f) * sizeMul)
                    ?.Configure(Main.rand.Next(16, 26), new Color(115, 24, 32), new Color(28, 13, 21));
            }
        }

        /// <summary>屏幕级包络：冲刺恒亮 Bloom，蒸发期回落（复用绯红裂空 Bloom 管线）</summary>
        private void PushScreenState() {
            float envelope = stopFrame < 0 ? 1f : 1f - RetractT;
            if (envelope <= 0.02f || path.Count == 0) {
                return;
            }
            CrimsonImpactFX.PushAmbience(path[^1], 0.30f * envelope);
        }

        //==================== 绘制 ====================
        //流带 → EndEntityDraw 弹幕扩展图元层；头端辉光/出发点撕裂形 → 加色层

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>四股子带：白热主脊（图一那道横光）+ 主墨绸 + 两条细丝（层间视差）</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || path.Count < 2) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OKF.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }

            //头端 follow-through：绘制用路径副本，末点沿冲刺方向外推残余量
            IReadOnlyList<Vector2> pts = path;
            if (headExt > 0.5f) {
                List<Vector2> extended = new(path) { path[^1] + dashDir * headExt };
                pts = extended;
            }

            float retract = RetractT;
            //出发过曝一拍速落
            float flash = timer <= 1 ? 0.9f : MathF.Pow(0.55f, timer - 1) * 0.9f;
            //兜底淡出（蒸发进度之外的最后保险）
            float opacity = 1f - MathHelper.Clamp((timer - (JudgmentFrame + RetractDelay + RetractFrames)) / 10f, 0f, 1f);

            //超短径（向脚下急停等）幅宽随长度收窄：58px 半幅配 150px 短径会糊成团块
            float totalLen = 0f;
            for (int i = 1; i < pts.Count; i++) {
                totalLen += Vector2.Distance(pts[i - 1], pts[i]);
            }
            float s = sizeMul * MathHelper.Clamp(totalLen / 320f, 0.4f, 1f);
            Span<OKF.RibbonDef> defs = [
                //白热主脊：窄、快、几乎不撕裂，头段发光的骨架
                new() { HalfWidth = 15f * s, PerpOffset = 0f, Seed = seed + 0.71f,
                    FlowMul = 1.60f, TearAmp = 0.22f, HeadBoost = 1.55f, OpacityMul = 0.72f },
                //主墨绸：全宽、撕裂大舌，黑红的身体
                new() { HalfWidth = 58f * s, PerpOffset = 0f, Seed = seed,
                    FlowMul = 1.00f, TearAmp = 0.95f, HeadBoost = 0.55f, OpacityMul = 0.95f },
                //上侧细丝：快流、碎
                new() { HalfWidth = 24f * s, PerpOffset = 34f * s, Seed = seed + 0.37f,
                    FlowMul = 1.45f, TearAmp = 1.25f, HeadBoost = 0.25f, OpacityMul = 0.80f },
                //下侧细丝：慢流、最碎（层间视差的第三速度）
                new() { HalfWidth = 19f * s, PerpOffset = -40f * s, Seed = seed + 0.53f,
                    FlowMul = 0.70f, TearAmp = 1.35f, HeadBoost = 0.20f, OpacityMul = 0.75f },
            ];
            for (int i = 0; i < defs.Length; i++) {
                OKF.DrawRibbon(device, fx, pts, in defs[i], retract, flash, opacity);
            }

            OKF.EndDraw(device, pb, pr, pd);
        }

        /// <summary>加色层：冲刺期头端流光锋头包裹 + 出发点撕裂形/白闪（前 10 帧）</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Main.dedServ || path.Count == 0) {
                return;
            }

            //---- 刹停爆点：星芒过曝一拍，把"收束成一点"钉进眼里 ----
            if (stopFrame >= 0 && timer - stopFrame < 5 && OnikiriAssets.StarFlare02?.Value is Texture2D popFlare) {
                float popT = (timer - stopFrame) / 5f;
                float popA = MathF.Pow(1f - popT, 1.6f);
                Vector2 popPos = path[^1] + dashDir * headExt - Main.screenPosition;
                spriteBatch.Draw(popFlare, popPos, null, new Color(255, 244, 232) * (popA * 0.9f)
                    , seed * 3f, popFlare.Size() * 0.5f, (1.3f + popT * 0.6f) * sizeMul, SpriteEffects.None, 0);
            }

            //---- 出发点告别语：撕裂形沿冲刺方向绽开，短命 ----
            if (timer < 10 && OnikiriAssets.TearSpread01?.Value is Texture2D tear) {
                Vector2 origin = path[0] - Main.screenPosition;
                float t = timer / 10f;
                float tA = MathF.Pow(1f - t, 1.7f) * 0.9f;
                float tS = (1.15f + CrimsonSlashRenderer.EaseOutCubic(t) * 0.55f) * sizeMul;
                spriteBatch.Draw(tear, origin, null, new Color(255, 140, 105) * tA, DashAngle
                    , tear.Size() * 0.5f, tS, SpriteEffects.None, 0);
                spriteBatch.Draw(tear, origin, null, new Color(200, 52, 40) * (tA * 0.8f), DashAngle + 0.4f
                    , tear.Size() * 0.5f, tS * 0.72f, SpriteEffects.FlipVertically, 0);
            }
            if (timer < 4 && OnikiriAssets.StarFlare02?.Value is Texture2D flare) {
                Vector2 origin = path[0] - Main.screenPosition;
                float fA = 1f - timer / 4f;
                spriteBatch.Draw(flare, origin, null, new Color(255, 240, 228) * (fA * 0.85f)
                    , seed * 6f, flare.Size() * 0.5f, (0.7f + fA * 0.3f) * sizeMul, SpriteEffects.None, 0);
            }
        }
    }
}
