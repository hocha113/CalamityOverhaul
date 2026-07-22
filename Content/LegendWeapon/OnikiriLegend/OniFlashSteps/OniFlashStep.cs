using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSakuraFlights;
using InnoVault.GameContent.BaseEntity;
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
    /// <summary>神威疾走主控. ai[0]=瞄准角(弧度) ai[1]=冲刺距离(px) ai[2]=尺寸倍率</summary>
    internal class OniFlashStep : BaseHeldProj, IPrimitiveDrawable, IAdditiveDrawable, IOverlayDrawable, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>单段冲刺速度(px/帧)，距离在出手时由光标位置一次性确定</summary>
        private const float DashSpeed = 170f;

        private const int JudgmentDelay = 8;    //刹停到纳刀结算

        private const int RetractDelay = 10;    //刹停到流带开始蒸发

        private const int RetractFrames = 22;   //蒸发时长

        private const int MaxMarks = 24;        //单次冲刺标记上限

        private const int NotoFlickFrames = 6;  //纳刀一挑时长(起于纳刀结算帧,与"锵"同步)

        private const int TailFadeFrames = 8;   //纳刀后持刀淡出

        private const float CollisionSubStep = 14f; //直线斩停子步长(小于玩家宽度,防隧穿)

        private const float SweepLead = 44f;        //扫掠前导:冲刺终点脸前的目标不漏标

        private const float SweepBackPad = 24f;     //扫掠后补:起手贴脸的目标不漏标

        private const float MarkSweepWidth = 140f;  //扫掠走廊宽(对齐墨绸视觉宽度,玩家"明明穿过了"的判断依据是那条彩带)

        /// <summary>A/B、冲刺期隐藏本地玩家（"人化作一道神威"的完全体），默认关</summary>
        public static bool HidePlayerDuringDash => true;
        /// <summary>本地玩家当前处于冲刺隐藏帧</summary>
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

        /// <summary>流带头端超前身体的距离（px），停止时按身前自由空间 clamp、墨最多亲到墙面，永不入墙</summary>
        private float headOffset = 100f;
        /// <summary>已衔接樱流(表世界跑满仍按住右键)，跳过残心纳刀并当帧移交操控</summary>
        private bool chained;

        /// <summary>衔接视觉分支的判据、owner 端看 chained</summary>
        private bool ChainedToSakura => chained || OniSakuraFlight.ControlsOwner(Projectile.owner);

        private bool Dashing => stopFrame < 0;
        /// <summary>纳刀结算的绝对帧，按出手时确定的距离稳定排拍</summary>
        private int JudgmentFrame => plannedDashFrames + JudgmentDelay;

        //收尾残心/纳刀的实体刀(纯视觉,非阻塞)

        private readonly OniBladePose bladePose = new();

        /// <summary>位移段硬占刀权:人已化入神威,连段就地冻结让位</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => stopFrame < 0;

        /// <summary>挥空后的保留余量(帧):没东西可演就不收税,只留极短落地拍</summary>
        private const int WhiffReserveFrames = 2;

        /// <summary>刹停后的签名拍软保留</summary>
        bool IOniBladeOccupant.ReservesBlade => stopFrame >= 0
            && (Owner.GetModPlayer<OnikiriPlayer>().ZanshinAutoHandoffActive
                || timer <= (marked.Count == 0 ? stopFrame + WhiffReserveFrames : JudgmentFrame + NotoFlickFrames));

        private float DashAngle => Projectile.ai[0];
        private float Distance => MathF.Max(Projectile.ai[1], 1f);

        internal static int CalculateTravelFrames(float distance)
            => Math.Max((int)MathF.Ceiling(MathF.Max(distance, 1f) / DashSpeed), 2);

        /// <summary>触发接口、在持有者客户端调用</summary>
        /// <param name="player">冲刺者</param>
        /// <param name="aim">冲刺方向（无需归一化）</param>
        /// <param name="damage">墨痕引爆伤害（每个被穿过的敌人全额一次）</param>
        /// <param name="knockback">击退</param>
        /// <param name="distance">冲刺距离(px)，触碰实体物块立即终止</param>
        /// <param name="scale">尺寸倍率（流带幅宽/粒子随之缩放）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 aim, int damage, float knockback,
            float distance, float scale = 1f, IEntitySource source = null) {
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

        public override void Initialize() {
            initialized = true;
            dashDir = DashAngle.ToRotationVector2();
            sizeMul = Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;
            seed = Projectile.identity * 0.6180339887f % 1f;
            plannedDashFrames = CalculateTravelFrames(Distance);
            Projectile.timeLeft = JudgmentFrame + RetractDelay + RetractFrames + 30;

            path.Add(GetCenter());
            if (Owner.whoAmI == Main.myPlayer) {
                Owner.RemoveAllGrapplingHooks();
            }

            //出发即巅峰、布帛撕裂 + 风切 + 低太鼓，没有任何充能音

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
                if (!Projectile.active) {
                    return;
                }
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

        /// <summary>冲刺帧，沿出手时锁定的方向和距离推进</summary>
        private void DashFrame() {
            Vector2 prevHead = GetCenter();
            Vector2 fromBody = Owner.Center;

            float moved = 0f;
            bool blocked = false;
            float stepLen = MathF.Min(DashSpeed, MathF.Max(Distance - traveled, 0f));

            //高速位移必须保留子步检测防止穿墙，但不再尝试抬阶或修正轨迹
            while (moved < stepLen - 0.01f) {
                float sub = MathF.Min(CollisionSubStep, stepLen - moved);
                Vector2 next = Owner.position + dashDir * sub;
                if (Collision.SolidCollision(next, Owner.width, Owner.height)) {
                    blocked = true;
                    break;
                }
                Owner.position = next;
                moved += sub;
            }

            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            traveled += moved;
            Owner.GivePlayerImmuneState(10);
            HoldPose();

            //扫掠锚定身体并带前导/后补、起手贴脸与终点脸前的目标都不漏
            Vector2 sweepEnd = blocked ? Owner.Center : Owner.Center + dashDir * SweepLead;
            MarkSweep(fromBody - dashDir * SweepBackPad, sweepEnd);

            if (blocked) {
                Projectile.Kill();
                return;
            }

            if (traveled >= Distance - 1f) {
                headOffset = MathF.Min(headOffset, MathF.Max(FreeAheadBudget() - 6f, 8f));
                if (!TryChainIntoSakura()) {
                    FinishDash();
                }
            }

            if (Vector2.DistanceSquared(path[^1], GetCenter()) > 64f) {
                path.Add(GetCenter());
            }

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

        /// <summary>表世界的樱流衔接、跑满计划距离时右键仍按住</summary>
        private bool TryChainIntoSakura() {
            if (chained || !Projectile.IsOwnedByLocalPlayer() || !Main.mouseRight) {
                return false;
            }
            if (!Owner.GetModPlayer<OnikiriPlayer>().TryChainSakuraFlight(dashDir, Projectile.GetSource_FromAI())) {
                return false;
            }
            chained = true;
            stopFrame = timer;
            headExt = MathF.Min(22f * sizeMul, MathF.Max(FreeAheadBudget() - headOffset - 4f, 0f));
            return true;
        }

        /// <summary>自然抵达目标距离，原地结束位移并进入残心排拍</summary>
        private void FinishDash() {
            stopFrame = timer;
            headOffset = MathF.Min(headOffset, MathF.Max(FreeAheadBudget() - 6f, 8f));
            headExt = MathF.Min(22f * sizeMul, MathF.Max(FreeAheadBudget() - headOffset - 4f, 0f));
            Owner.CWR().GetScreenShake(2.2f);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<OnikiriPlayer>().OpenZanshinWindow(
                    JudgmentFrame - timer, marked.Count, dashDir);
            }

            if (!Main.dedServ) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(GetCenter() + Main.rand.NextVector2Circular(18f, 24f)
                        , dashDir * Main.rand.NextFloat(0.6f, 1.8f) + Main.rand.NextVector2Circular(0.5f, 0.5f)
                        , Color.White, Main.rand.NextFloat(0.05f, 0.09f) * sizeMul)
                        ?.Configure(Main.rand.Next(16, 26), new Color(120, 26, 34), new Color(30, 14, 22));
                }
            }
        }

        /// <summary>冲刺期持械姿态，角色读作低姿态直线突进</summary>
        private void HoldPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            if (MathF.Abs(dashDir.X) >= 0.05f) {
                Owner.ChangeDir(dashDir.X > 0f ? 1 : -1);
            }
            Owner.itemRotation = (dashDir * Owner.direction).ToRotation();
        }

        /// <summary>本帧扫掠段上的敌人缠上墨痕（无伤害）、微时停 + 穿身墨屑，结算全部押后到纳刀帧</summary>
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
                npc.CWR().TimeFrozenTick = 3;   //穿身微滞、世界不停，只有被穿者顿一下

                if (Projectile.IsOwnedByLocalPlayer()) {
                    //墨痕走向与本次直线居合方向一致

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

        /// <summary>纳刀帧、"锵"一声，墨痕们（各自对齐本帧）同时裂开；主控只负责声与光的确认</summary>
        private void Judge() {
            judged = true;
            if (marked.Count == 0) {
                return;   //挥空不响鞘、死寂本身就是收势

            }
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.10f, Volume = 0.55f }, GetCenter());
            SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.35f, Volume = 0.22f }, GetCenter());
            if (marked.Count >= 3) {
                //群裂低频垫底、单声限流，不随痕数叠加防爆音

                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.62f, Volume = 0.55f, MaxInstances = 1 }, GetCenter());
            }
            CrimsonImpactFX.PushImpact(GetCenter(), MathF.Min(0.02f + marked.Count * 0.008f, 0.07f));
            Owner.CWR().GetScreenShake(MathF.Min(3f + marked.Count * 0.8f, 8f));
        }

        /// <summary>刹停后的残心→纳刀(纯视觉,非阻塞)</summary>
        private void UpdateTailPose() {
            bladePose.Update();
            if (stopFrame < 0 || !Owner.active || Owner.dead) {
                return;
            }
            //已化樱远去:残心纳刀没有身体可摆,结算照常押后

            if (ChainedToSakura) {
                bladePose.Opacity = 0f;
                return;
            }
            if (timer - JudgmentFrame > NotoFlickFrames + TailFadeFrames
                || OniBladeOccupancy.ComboClaims(Owner)
                || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile)) {
                bladePose.Opacity = 0f;
                return;
            }

            //残心/纳刀沿本次居合方向

            float dirA = dashDir.ToRotation();
            int facing = MathF.Abs(dashDir.X) < 0.05f ? Owner.direction : (dashDir.X > 0f ? 1 : -1);
            int sinceJudge = timer - JudgmentFrame;
            OnikiriPlayer onikiri = Owner.GetModPlayer<OnikiriPlayer>();
            if (onikiri.ZanshinAutoHandoffActive) {
                float t = onikiri.ZanshinAutoHandoffProgress;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(dirA, dirA - facing * 1.20f, ease);
                bladePose.Opacity = 1f;
                if (t > 0f && t < 1f) {
                    bladePose.PushSmear(0.65f);
                }
            }
            else if (sinceJudge <= 0) {
                //残心:刀沿冲刺向平指,极轻的呼吸下沉

                bladePose.Rotation = dirA + facing * 0.05f * MathF.Sin((timer - stopFrame) * 0.35f);
                bladePose.Opacity = 1f;
            }
            else if (sinceJudge <= NotoFlickFrames) {
                //纳刀一挑:EaseOut 干脆收刀回背(与连段收势的持刀位同一套语言)

                float t = sinceJudge / (float)NotoFlickFrames;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(dirA, dirA - facing * 1.20f, ease);
                bladePose.Opacity = 1f;
                if (sinceJudge <= 3) {
                    bladePose.PushSmear(1f - t * 0.4f);
                }
            }
            else {
                bladePose.Opacity = 1f - (sinceJudge - NotoFlickFrames) / (float)TailFadeFrames;
            }
            bladePose.ApplyPose(Owner, Projectile, fixedFacing: facing);
        }

        /// <summary>遮挡层、收尾残心/纳刀的实体刀,稳定盖在流带辉光之上</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        private bool hideHeld;

        /// <summary>可选隐藏，冲刺期本地玩家不绘制，交还操控立刻恢复</summary>
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

        /// <summary>出发点墨爆、黑红墨浪 + 碎晶 + 一帧白闪、"人从墨里挣脱出去"</summary>
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

        /// <summary>冲刺途中、新鲜路径段上剥落的墨屑（速度语言的粒子层，量随位移走）</summary>
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

        /// <summary>蒸发期、烟屑沿蒸发前沿剥落，墨绸"化墨散掉"而不是原地淡出</summary>
        private void SpawnRetractWisps() {
            float t = RetractT;
            if (Main.dedServ || t <= 0f || t >= 1f || path.Count < 2) {
                return;
            }
            //对齐 shader 蒸发阈值
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

        /// <summary>屏幕级包络、冲刺恒亮 Bloom，蒸发期回落（复用绯红裂空 Bloom 管线）</summary>
        private void PushScreenState() {
            float envelope = stopFrame < 0 ? 1f : 1f - RetractT;
            if (envelope <= 0.02f || path.Count == 0) {
                return;
            }
            CrimsonImpactFX.PushAmbience(path[^1], 0.30f * envelope);
        }

        //流带 → EndEntityDraw

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>四股子带、白热主脊（图一那道横光）+ 主墨绸 + 两条细丝（层间视差）</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || path.Count < 2) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OKF.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }

            //头端 follow-through、绘制用路径副本，末点沿冲刺方向外推残余量

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

            //超短径（向脚下急停等）幅宽随长度收窄

            float totalLen = 0f;
            for (int i = 1; i < pts.Count; i++) {
                totalLen += Vector2.Distance(pts[i - 1], pts[i]);
            }
            float s = sizeMul * MathHelper.Clamp(totalLen / 320f, 0.4f, 1f);
            Span<OKF.RibbonDef> defs = [
                //白热主脊、窄、快、几乎不撕裂，头段发光的骨架

                new() { HalfWidth = 15f * s, PerpOffset = 0f, Seed = seed + 0.71f,
                    FlowMul = 1.60f, TearAmp = 0.22f, HeadBoost = 1.55f, OpacityMul = 0.72f },
                //主墨绸、全宽、撕裂大舌，黑红的身体

                new() { HalfWidth = 58f * s, PerpOffset = 0f, Seed = seed,
                    FlowMul = 1.00f, TearAmp = 0.95f, HeadBoost = 0.55f, OpacityMul = 0.95f },
                //上侧细丝、快流、碎

                new() { HalfWidth = 24f * s, PerpOffset = 34f * s, Seed = seed + 0.37f,
                    FlowMul = 1.45f, TearAmp = 1.25f, HeadBoost = 0.25f, OpacityMul = 0.80f },
                //下侧细丝、慢流、最碎（层间视差的第三速度）

                new() { HalfWidth = 19f * s, PerpOffset = -40f * s, Seed = seed + 0.53f,
                    FlowMul = 0.70f, TearAmp = 1.35f, HeadBoost = 0.20f, OpacityMul = 0.75f },
            ];
            for (int i = 0; i < defs.Length; i++) {
                OKF.DrawRibbon(device, fx, pts, in defs[i], retract, flash, opacity);
            }

            OKF.EndDraw(device, pb, pr, pd);
        }

        /// <summary>加色层、冲刺期头端流光锋头包裹 + 出发点撕裂形/白闪（前 10 帧）</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Main.dedServ || path.Count == 0) {
                return;
            }

            //---- 刹停爆点

            if (stopFrame >= 0 && timer - stopFrame < 5 && !ChainedToSakura
                && CWRAsset.StarFlare02?.Value is Texture2D popFlare) {
                float popT = (timer - stopFrame) / 5f;
                float popA = MathF.Pow(1f - popT, 1.6f);
                Vector2 popPos = path[^1] + dashDir * headExt - Main.screenPosition;
                spriteBatch.Draw(popFlare, popPos, null, new Color(255, 244, 232) * (popA * 0.9f)
                    , seed * 3f, popFlare.Size() * 0.5f, (1.3f + popT * 0.6f) * sizeMul, SpriteEffects.None, 0);
            }

            //---- 出发点告别语、撕裂形沿冲刺方向绽开，短命 ----

            if (timer < 10 && CWRAsset.TearSpread01?.Value is Texture2D tear) {
                Vector2 origin = path[0] - Main.screenPosition;
                float t = timer / 10f;
                float tA = MathF.Pow(1f - t, 1.7f) * 0.9f;
                float tS = (1.15f + CrimsonSlashRenderer.EaseOutCubic(t) * 0.55f) * sizeMul;
                spriteBatch.Draw(tear, origin, null, new Color(255, 140, 105) * tA, DashAngle
                    , tear.Size() * 0.5f, tS, SpriteEffects.None, 0);
                spriteBatch.Draw(tear, origin, null, new Color(200, 52, 40) * (tA * 0.8f), DashAngle + 0.4f
                    , tear.Size() * 0.5f, tS * 0.72f, SpriteEffects.FlipVertically, 0);
            }
            if (timer < 4 && CWRAsset.StarFlare02?.Value is Texture2D flare) {
                Vector2 origin = path[0] - Main.screenPosition;
                float fA = 1f - timer / 4f;
                spriteBatch.Draw(flare, origin, null, new Color(255, 240, 228) * (fA * 0.85f)
                    , seed * 6f, flare.Size() * 0.5f, (0.7f + fA * 0.3f) * sizeMul, SpriteEffects.None, 0);
            }
        }
    }
}
