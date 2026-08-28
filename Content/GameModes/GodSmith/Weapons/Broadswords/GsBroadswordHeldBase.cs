using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 单拍参数：未缩放帧数与几何，<see cref="GsBroadswordHeldBase.GetBeat"/> 按拍号返回。
    /// 帧数会在 InitStage 里统一除以攻速，攻速词条真实生效
    /// </summary>
    internal struct GsBroadBeat
    {
        /// <summary>举刀/滞帧/斩切/收势 各相帧数</summary>
        public int Raise, Hold, Slash, Recover;
        /// <summary>后摆弧度（举刀拉开的量）</summary>
        public float RaiseBack;
        /// <summary>跟进弧度（越过瞄准线的量）</summary>
        public float Follow;
        /// <summary>本拍触及距离倍率</summary>
        public float ReachScale;
        /// <summary>体态倾斜幅度</summary>
        public float LeanAmp;
        /// <summary>本拍伤害倍率（进 DPS 包络预算）</summary>
        public float DamageMult;
        /// <summary>命中顿帧帧数（从收势尾巴等量扣回）</summary>
        public int Hitstop;
        /// <summary>爆发首帧体术前压速度（0=无）</summary>
        public float LungeSpeed;
        /// <summary>挥砍音高</summary>
        public float SwingPitch;

        /// <summary>范例交替重劈的基准参数</summary>
        public static GsBroadBeat Standard => new() {
            Raise = 6, Hold = 2, Slash = 4, Recover = 9,
            RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
            DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.08f,
        };

        /// <summary>范例前压终结斩的基准参数</summary>
        public static GsBroadBeat Finisher => new() {
            Raise = 8, Hold = 3, Slash = 5, Recover = 12,
            RaiseBack = 2.25f, Follow = 1.25f, ReachScale = 1.18f, LeanAmp = 0.09f,
            DamageMult = 1.35f, Hitstop = 2, LungeSpeed = 3.2f, SwingPitch = -0.28f,
        };
    }

    /// <summary>
    /// 阔剑族手持基类：把 GsIronBroadswordHeld 的骨架抽象为可参数化的相位时间线。<br/>
    /// 固定资产：举-滞-斩-收四相、SwingCurve 过冲回坐、命中顿帧记账扣回、
    /// 贪婪逐段采样判定、体态倾斜（钉脚底、坐骑冲刺让位）、双层涂抹刀光+姿态残影+辉光绘制。<br/>
    /// 子类填拍表（<see cref="GetBeat"/>）与色板，签名行为走虚钩子；
    /// 需要整替几何的异形（太刀闪现/双弧钳咬）重写 <see cref="UpdateBladeTransform"/>。<br/>
    /// 联机纪律：ai[0]=拍号 ai[1]=交替符号 随生成包过线；签名弹幕用
    /// <see cref="SpawnOwnedProj"/>（守 owner）；粒子守 !isServer（基类已在 AI 里守）；
    /// 绘制路径禁 Main.rand，抖动用 <see cref="DrawRand01"/>
    /// </summary>
    internal abstract class GsBroadswordHeldBase : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName =>
            Language.GetText("ItemName." + ItemID.Search.GetName(SwordItemID));

        protected const int PhaseRaise = 0;
        protected const int PhaseHold = 1;
        protected const int PhaseSlash = 2;
        protected const int PhaseRecover = 3;

        //==================== 子类必填 ====================

        /// <summary>目标物品 ID（手上物品切换即自杀；也是刀身贴图来源）</summary>
        protected abstract int SwordItemID { get; }

        /// <summary>刃缘亮色（残影/涂抹外层）</summary>
        protected abstract Color EdgeBright { get; }
        /// <summary>体色（涂抹内层/光照）</summary>
        protected abstract Color BodyMain { get; }
        /// <summary>重击强调色（终结辉光/蓄力闪）</summary>
        protected abstract Color HotAccent { get; }

        /// <summary>拍表：按拍号返回本拍参数</summary>
        protected abstract GsBroadBeat GetBeat(int stage);

        //==================== 可调几何与节奏 ====================

        /// <summary>连段拍数（与方案侧 ComboBeats 一致）</summary>
        protected virtual int BeatCount => 3;
        /// <summary>手→刃尖基准距离（px）</summary>
        protected virtual float BaseReach => 118f;
        /// <summary>刀身贴图中心停在手→刃尖的几成处</summary>
        protected virtual float BladePark => 0.46f;
        /// <summary>刀尖顶到手→刃尖的几成</summary>
        protected virtual float BladeTipFill => 1.02f;
        /// <summary>斩切伤害窗（进度超过即停判，余下是纯演出）</summary>
        protected virtual float DamageWindowEnd => 0.9f;
        /// <summary>贪婪判定的线宽</summary>
        protected virtual float CollisionWidth => 40f;
        /// <summary>贴身兜底判定半径</summary>
        protected virtual float PointBlankRadius => 42f;
        /// <summary>垫影色</summary>
        protected virtual Color DeepShadow => new(14, 14, 20);

        //==================== 运行时状态（子类只读消费） ====================

        protected int raiseDur = 6;
        protected int holdDur = 2;
        protected int slashDur = 4;
        protected int recoverDur = 9;
        protected int totalDur;
        protected float raiseBack = 1.85f;
        protected float follow = 1.0f;
        protected float reachScale = 1f;
        protected float leanAmp = 0.045f;

        protected float baseAngle;
        protected float swingDir = 1f;
        protected int facingDir = 1;
        protected float mainAngle;
        protected float lastAngle;
        protected float mainReach;
        protected Vector2 mainTip;
        protected float slashProgress;
        protected float fanFade = 1f;
        protected int flashTimer;
        protected int flashDur = 7;
        protected int hitstopTimer;
        protected int hitstopSpent;
        protected bool hitstopApplied;
        protected bool slashStarted;
        protected bool lungeApplied;
        protected bool sweepDamageActive;
        protected float bodyLean;
        private bool bodyLeanApplied;
        protected readonly HashSet<int> hitNPCs = [];

        protected int timer;
        private GsBroadBeat beat;

        /// <summary>连段拍号（ai[0]，生成端写入随包过线）</summary>
        protected int ComboStage => Math.Clamp((int)Projectile.ai[0], 0, BeatCount - 1);
        /// <summary>是否终结拍（默认最后一拍）</summary>
        protected virtual bool IsFinisher => ComboStage == BeatCount - 1;
        /// <summary>本拍参数（InitStage 后有效）</summary>
        protected GsBroadBeat Beat => beat;

        protected int CurrentPhase {
            get {
                if (timer <= raiseDur) {
                    return PhaseRaise;
                }
                if (timer <= raiseDur + holdDur) {
                    return PhaseHold;
                }
                if (timer <= raiseDur + holdDur + slashDur) {
                    return PhaseSlash;
                }
                return PhaseRecover;
            }
        }

        protected float FullReach => BaseReach * reachScale;
        protected float ArcStart => baseAngle - (swingDir * raiseBack);
        protected float ArcEnd => baseAngle + (swingDir * follow);
        protected Vector2 Hand => Owner.GetPlayerStabilityCenter();

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 120;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
            SetSwordDefaults();
        }

        /// <summary>SetDefaults 追加项（大剑加宽判定等）</summary>
        protected virtual void SetSwordDefaults() { }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>按拍号写入时长与几何；各相时长除以攻速，攻速词条真实生效</summary>
        protected void InitStage() {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            swingDir = (Projectile.ai[1] >= 0f ? 1f : -1f) * facingDir;

            beat = GetBeat(ComboStage);
            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));

            raiseDur = D(beat.Raise);
            holdDur = D(beat.Hold);
            slashDur = D(beat.Slash);
            recoverDur = D(beat.Recover);
            raiseBack = beat.RaiseBack;
            follow = beat.Follow;
            reachScale = beat.ReachScale;
            leanAmp = beat.LeanAmp;
            if (beat.DamageMult != 1f) {
                Projectile.damage = (int)(Projectile.damage * beat.DamageMult);
            }
            totalDur = raiseDur + holdDur + slashDur + recoverDur;
            OnStageInit();
        }

        /// <summary>InitStage 尾部：子类改写几何/缓存拍相关状态</summary>
        protected virtual void OnStageInit() { }

        public override void AI() {
            if (Item.type != SwordItemID || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            if (timer == 0) {
                InitStage();
            }

            //命中顿帧：timer 冻结，刀角体态一起停
            if (hitstopTimer > 0) {
                hitstopTimer--;
            }
            else {
                timer++;
            }
            if (flashTimer > 0) {
                flashTimer--;
            }

            int phase = CurrentPhase;
            lastAngle = mainAngle;
            UpdateBladeTransform(phase);
            sweepDamageActive = phase == PhaseSlash
                && slashProgress <= DamageWindowEnd
                && MathF.Abs(mainAngle - lastAngle) > 0.004f;
            UpdatePose(phase);
            HandlePhaseEvents(phase);
            if (!VaultUtils.isServer) {
                HandleParticles(phase);
            }

            Lighting.AddLight(Vector2.Lerp(Hand, mainTip, 0.7f), BodyMain.ToVector3() * (0.5f * fanFade));

            //顿帧从收势尾巴等量扣回，命中不延长真实冷却
            int effectiveTotal = Math.Max(raiseDur + holdDur + slashDur + 4, totalDur - hitstopSpent);
            if (timer >= effectiveTotal) {
                Projectile.Kill();
            }
        }

        /// <summary>斩切行程曲线：爆发过冲再回坐（收-爆-停）</summary>
        protected virtual float SwingCurve(float p) {
            const float burstEnd = 0.56f;
            const float overshoot = 1.045f;
            if (p < burstEnd) {
                return overshoot * SmoothStep01(p / burstEnd);
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((p - burstEnd) / (1f - burstEnd)));
        }

        /// <summary>
        /// 相位几何：写 mainAngle/mainReach/slashProgress/fanFade，尾部必须更新 mainTip。
        /// 异形（闪现太刀/双弧）整体重写本方法即可换掉运动语言，判定与体态照常工作
        /// </summary>
        protected virtual void UpdateBladeTransform(int phase) {
            float arcStart = ArcStart;
            float heldAngle = arcStart - (swingDir * 0.08f);

            switch (phase) {
                case PhaseRaise: {
                    float p = timer / (float)raiseDur;
                    float eased = 1f - MathF.Pow(1f - p, 3f);
                    float liftFrom = arcStart + (swingDir * raiseBack * 0.68f);
                    mainAngle = MathHelper.Lerp(liftFrom, arcStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.58f, 0.92f, eased);
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    float p = (timer - raiseDur) / (float)holdDur;
                    mainAngle = MathHelper.Lerp(arcStart, heldAngle, EaseOutQuad(p));
                    mainReach = FullReach * MathHelper.Lerp(0.92f, 0.96f, EaseOutQuad(p));
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    mainAngle = MathHelper.Lerp(heldAngle, ArcEnd, SwingCurve(p));
                    mainReach = FullReach * (0.96f + 0.04f * MathF.Sin(MathHelper.Clamp(p * 1.8f, 0f, 1f) * MathHelper.Pi));
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 2.2f));
                    mainAngle = ArcEnd + (swingDir * 0.09f * (1f - settle));
                    mainReach = FullReach * MathHelper.Lerp(0.96f, 0.82f, q * q);
                    slashProgress = 1f;
                    float fadeDur = MathF.Max(4f, recoverDur * 0.7f);
                    fanFade = MathHelper.Clamp(1f - ((timer - raiseDur - holdDur - slashDur) / fadeDur), 0f, 1f);
                    break;
                }
            }

            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        public override bool? CanDamage() => sweepDamageActive ? null : false;

        /// <summary>持械姿态，体态收势后仰爆发前甩</summary>
        protected virtual void UpdatePose(int phase) {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (mainAngle.ToRotationVector2() * Owner.direction).ToRotation();

            Player.CompositeArmStretchAmount stretch = phase is PhaseRaise or PhaseRecover
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, mainAngle - MathHelper.PiOver2);

            Projectile.Center = Vector2.Lerp(Hand, mainTip, 0.6f);
            Projectile.rotation = mainAngle;

            if (hitstopTimer > 0) {
                return;
            }
            (float target, float rate) = phase switch {
                PhaseRaise => (-facingDir * leanAmp * 0.8f, 0.22f),
                PhaseHold => (-facingDir * leanAmp, 0.30f),
                PhaseSlash => (facingDir * leanAmp * 1.5f, 0.70f),
                _ => (0f, 0.16f),
            };
            bodyLean = MathHelper.Lerp(bodyLean, target, rate);
            ApplyBodyLean();
        }

        /// <summary>体态倾斜上身，坐骑/冲刺旋转让位，origin 钉脚底</summary>
        private void ApplyBodyLean() {
            CWRPlayer modPlayer = Owner.CWR();
            if (Owner.mount.Active || (modPlayer != null && modPlayer.IsRotatingDuringDash)) {
                bodyLeanApplied = false;
                return;
            }
            Owner.fullRotation = bodyLean * Owner.gravDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.gravDir >= 0f ? Owner.height : 0f);
            bodyLeanApplied = true;
        }

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
            OnKillEffects();
        }

        /// <summary>消亡追加（余痕/收刀演出）</summary>
        protected virtual void OnKillEffects() { }

        /// <summary>相位事件：终结蓄力闪、斩切起手音+前压。重写可换事件编排（记得调 base 或自管 slashStarted）</summary>
        protected virtual void HandlePhaseEvents(int phase) {
            //终结拍蓄力完成的瞬间刃身闪一记
            if (IsFinisher && timer == raiseDur + 1) {
                SetFlash(7);
            }

            if (phase == PhaseSlash && !slashStarted) {
                slashStarted = true;
                flashTimer = Math.Max(flashTimer, 5);
                if (!VaultUtils.isServer) {
                    PlaySwingSound();
                }
                OnSlashBegin();
            }

            //体术前压：爆发首帧沿出手向踏步（owner 端权威，位置随原版同步）
            if (beat.LungeSpeed > 0f && !lungeApplied && phase == PhaseSlash) {
                lungeApplied = true;
                if (Owner.whoAmI == Main.myPlayer && !Owner.mount.Active) {
                    Owner.velocity.X += facingDir * beat.LungeSpeed;
                }
            }
        }

        /// <summary>斩切起手音；默认 Item1 按拍调音高，终结拍补一记厚响</summary>
        protected virtual void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.45f }, Owner.Center);
            }
        }

        /// <summary>斩切爆发首帧（签名弹幕/驻场生成放这，内部用 SpawnOwnedProj 守 owner）</summary>
        protected virtual void OnSlashBegin() { }

        /// <summary>点亮刃身闪光</summary>
        protected void SetFlash(int frames) {
            flashDur = Math.Max(1, frames);
            flashTimer = Math.Max(flashTimer, frames);
        }

        /// <summary>当前闪光强度 0~1</summary>
        protected float FlashStrength => flashDur > 0 ? flashTimer / (float)flashDur : 0f;

        /// <summary>粒子演出（已在非服务器端调用）：默认斩切期沿切线甩族色火星</summary>
        protected virtual void HandleParticles(int phase) {
            if (phase != PhaseSlash) {
                return;
            }
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            int count = IsFinisher ? 2 : 1;
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.6f, 1.0f));
                Color c = Main.rand.NextBool(3) ? HotAccent : EdgeBright;
                PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(3.5f, 7f), c
                    , Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 判定 ====================

        /// <summary>贪婪判定：本帧扫过的角度区间逐段采样，贴身段单独兜一次</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!sweepDamageActive) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(8, 8);
            Vector2 hand = Hand;
            if (greedyBox.Distance(hand) <= PointBlankRadius) {
                return true;
            }

            float delta = mainAngle - lastAngle;
            float reach = mainReach * 1.04f + 10f;
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * reach / 30f), 1, 18);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)steps);
                Vector2 tip = hand + ang.ToRotationVector2() * reach;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , hand, tip, CollisionWidth, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            if (!sweepDamageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 hand = Hand;
            const int samples = 2;
            for (int i = 0; i <= samples; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)samples);
                Vector2 tip = hand + ang.ToRotationVector2() * (mainReach * 1.02f);
                Utils.PlotTileLine(hand, tip, CollisionWidth * 0.85f, DelegateMethods.CutTiles);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = facingDir;//击退跟出手朝向
            ModifyHitExtra(target, ref modifiers);
        }

        /// <summary>命中伤害修饰追加（破甲/条件增伤）</summary>
        protected virtual void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) { }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次挥砍对同一目标只转发一次外部命中钩子（模拟物品直击链，喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            //命中顿帧一拍只吃一次，扣回额度记账
            if (!hitstopApplied && CurrentPhase == PhaseSlash && beat.Hitstop > 0) {
                hitstopApplied = true;
                hitstopTimer = beat.Hitstop;
                hitstopSpent = beat.Hitstop;
            }

            OnHitTarget(target, hit, damageDone);
            if (!VaultUtils.isServer) {
                OnHitFX(target, hit, damageDone);
            }
        }

        /// <summary>命中逻辑追加（挂 buff/标记，各端一致量才写；owner 独占量守 myPlayer）</summary>
        protected virtual void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>命中反馈（已守非服务器端）：默认材质分流，钢质弹跳钢屑、血肉补原版血尘</summary>
        protected virtual void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            Vector2 aimDir = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            float power = IsFinisher ? 1f : 0.55f;

            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero
                , steel ? HotAccent : EdgeBright, 0.16f + power * 0.10f)?.Configure(10, 0.8f);
            int sparks = 4 + (int)(power * 4f);
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.7) * Main.rand.NextFloat(3.5f, 8f + power * 4f);
                Color c = Main.rand.NextBool(steel ? 2 : 3) ? HotAccent : EdgeBright;
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(true, Main.rand.Next(14, 24));
            }
            if (!steel && BleedOnFlesh) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood
                        , aimDir.RotatedByRandom(0.9) * Main.rand.NextFloat(1.5f, 3.5f), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                    d.noGravity = Main.rand.NextBool();
                }
            }
        }

        /// <summary>血肉目标是否补原版血尘（魔法质感的剑可关）</summary>
        protected virtual bool BleedOnFlesh => true;

        //==================== 工具 ====================

        /// <summary>owner 端生成签名弹幕（远端不执行，靠生成包同步）；返回 -1 表示非 owner</summary>
        protected int SpawnOwnedProj(int type, Vector2 pos, Vector2 vel, int damage, float kb,
            float ai0 = 0f, float ai1 = 0f, float ai2 = 0f) {
            if (Projectile.owner != Main.myPlayer) {
                return -1;
            }
            return Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), pos, vel, type, damage, kb
                , Owner.whoAmI, ai0, ai1, ai2);
        }

        /// <summary>绘制路径专用确定性伪随机 0~1（identity+timer+salt 播种，各端一致且逐帧稳定）</summary>
        protected float DrawRand01(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        protected static float EaseOutQuad(float t) => 1f - ((1f - t) * (1f - t));
        protected static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        //==================== 绘制（原版物品贴图垫底 + 自绘层） ====================

        public override bool PreDraw(ref Color lightColor) {
            DrawSmearArc(Main.spriteBatch);
            DrawBladeSet(Main.spriteBatch, lightColor);
            DrawExtra(Main.spriteBatch, lightColor);
            return false;
        }

        /// <summary>追加自绘层（驻场符光/延伸虚影），在刀身之上</summary>
        protected virtual void DrawExtra(SpriteBatch sb, Color lightColor) { }

        /// <summary>涂抹带外层色</summary>
        protected virtual Color SmearOuterColor => EdgeBright;
        /// <summary>涂抹带内层色</summary>
        protected virtual Color SmearInnerColor => IsFinisher ? HotAccent : BodyMain;

        /// <summary>紧凑刀光：双层弧形涂抹贴图沿刀角走（加色 A=0），斩切亮收势蚀散</summary>
        protected virtual void DrawSmearArc(SpriteBatch sb) {
            if (slashProgress <= 0.02f || fanFade <= 0.02f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.30f + slashProgress * 0.35f);
            Vector2 arcCenter = Hand + (mainAngle.ToRotationVector2() * mainReach * 0.55f) - Main.screenPosition;
            float rot = mainAngle + (swingDir * 0.35f);
            Color c = SmearOuterColor * alpha;
            c.A = 0;
            sb.Draw(wave, arcCenter, null, c, rot, wave.Size() / 2f
                , new Vector2(0.46f, 0.22f) * (mainReach / 118f), SpriteEffects.None, 0f);
            Color c2 = SmearInnerColor * (alpha * 0.7f);
            c2.A = 0;
            sb.Draw(wave, arcCenter, null, c2, rot, wave.Size() / 2f
                , new Vector2(0.42f, 0.10f) * (mainReach / 118f), SpriteEffects.None, 0f);
        }

        /// <summary>残影拍数（斩切期姿态残影层数）</summary>
        protected virtual int GhostCount => IsFinisher ? 3 : 2;
        /// <summary>残影角间距</summary>
        protected virtual float GhostSpacing => IsFinisher ? 0.24f : 0.18f;
        /// <summary>刀身整体透明度（藏刀入影类演出用）</summary>
        protected virtual float BladeAlpha => 1f;
        /// <summary>刀身光照染色（暗质材质可压暗）</summary>
        protected virtual Color BodyTint(Color lightColor) => lightColor;
        /// <summary>辉光层是否常亮（否则只在闪光时出现）</summary>
        protected virtual bool GlowAlways => IsFinisher;
        /// <summary>辉光色</summary>
        protected virtual Color GlowColor => HotAccent;

        /// <summary>残影+暗影垫底+本体+辉光</summary>
        protected virtual void DrawBladeSet(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            Vector2 origin = tex.Size() / 2f;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            Vector2 hand = Hand;
            float bladeAlpha = MathHelper.Clamp(BladeAlpha, 0f, 1f);

            //斩切期姿态残影，最近的最亮
            if (CurrentPhase == PhaseSlash && slashProgress > 0.10f) {
                for (int g = GhostCount; g >= 1; g--) {
                    float ghostAngle = mainAngle - (swingDir * GhostSpacing * g);
                    float ghostAlpha = g switch { 1 => 0.34f, 2 => 0.18f, _ => 0.08f } * bladeAlpha;
                    Color ghost = EdgeBright * ghostAlpha;
                    ghost.A = 0;
                    Vector2 gPos = hand + (ghostAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, ghost, ghostAngle + rotOffset, origin, scale, effect, 0);
                }
            }

            Vector2 drawPos = hand + (mainAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;

            //垫影
            Color shadow = new Color(DeepShadow.R, DeepShadow.G, DeepShadow.B, (byte)190) * (0.5f * bladeAlpha);
            sb.Draw(tex, drawPos + new Vector2(facingDir, 2f), null, shadow, mainAngle + rotOffset, origin, scale * 1.02f, effect, 0);

            sb.Draw(tex, drawPos, null, BodyTint(lightColor) * bladeAlpha, mainAngle + rotOffset, origin, scale, effect, 0);

            //重击辉光与蓄力闪
            float flash = FlashStrength;
            if (GlowAlways || flash > 0.01f) {
                Color glow = GlowColor * ((0.22f + flash * 0.45f) * MathF.Max(bladeAlpha, 0.35f));
                glow.A = 0;
                sb.Draw(tex, drawPos, null, glow, mainAngle + rotOffset, origin, scale * 1.04f, effect, 0);
            }
        }

        /// <summary>反向拍翻刃：刃口镜像到挥动前缘，双向朝向都要读得对</summary>
        protected void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset) {
            bool edgeFlip = (Projectile.ai[1] >= 0f ? 1 : -1) * facingDir < 0;
            bool flipVertically = (facingDir < 0) != edgeFlip;
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }
    }
}
