using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 连枷族公共层·方案侧。主题锚「链体物理」：甩链加速度曲线不匀速、收链回坠、链节逐段绘制。<br/>
    /// 接管方式统一为 GsShoot 压掉原版连枷弹幕、生成族自定义链锤头（<see cref="GsFlailHeadProj"/> 子类）；
    /// 原版物品的使用流（channel/挥臂）保留，姿态由锤头弹幕全程接管。<br/>
    /// 联机纪律：GsShoot 只在 owner 端执行，锤头经生成包广播；方案单例瞬时字段只许在 myPlayer 守门路径消费
    /// </summary>
    internal abstract class GsFlailScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Flails";

        /// <summary>族自定义锤头弹幕类型</summary>
        protected abstract int FlailProjType { get; }

        /// <summary>一次出手生成几颗锤头（链式断头台=2）</summary>
        protected virtual int HeadCount => 1;

        /// <summary>锤头 ai[2] 载荷（热量/阴阳相/双铡序号等，随生成包过线）</summary>
        protected virtual float LaunchAi2(Player player, int index) => 0f;

        /// <summary>锤头在场即禁再次使用（真实冷却 = 一次完整甩收循环）</summary>
        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[FlailProjType] > 0) {
                return false;
            }
            return null;
        }

        public sealed override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            for (int i = 0; i < HeadCount; i++) {
                Projectile.NewProjectile(source, player.MountedCenter,
                    velocity.SafeNormalize(Vector2.UnitX * player.direction),
                    FlailProjType, damage, knockback, player.whoAmI, 0f, 0f, LaunchAi2(player, i));
            }
            OnHeadsSpawned(item, player);
            //压掉原版连枷弹幕；远端靠锤头生成包看到动作
            return false;
        }

        /// <summary>锤头生成后的方案侧回调（owner 端；连击层数消费等）</summary>
        protected virtual void OnHeadsSpawned(Item item, Player player) { }
    }

    /// <summary>甩转姿态：Orbit=绕身轮甩（球链），Brace=收臂蓄压（拳类）</summary>
    internal enum GsFlailSpinMode
    {
        Orbit,
        Brace,
    }

    /// <summary>
    /// 连枷族公共层·锤头基类。三态链体物理：<br/>
    /// 甩转（ai[0]=0）——角速度沿充能曲线爬升不匀速，链条低速下垂高速拉直；<br/>
    /// 掷出（ai[0]=1）——初速按转速结算，飞行带阻尼减速与微重力，绝不匀速直飞；<br/>
    /// 收链（ai[0]=2）——先回坠（重力主导、链条塌垂）再加速度回卷。<br/>
    /// 链节沿贝塞尔垂链逐段绘制，禁整条贴图平移。ai[1]=出手转速，ai[2]=武器自定义载荷
    /// </summary>
    internal abstract class GsFlailHeadProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName =>
            Language.GetText("ItemName." + ItemID.Search.GetName(SourceItemID));

        //==================== 状态常量 ====================
        protected const int StateSpin = 0;
        protected const int StateLaunch = 1;
        protected const int StateRetract = 2;

        //==================== 子类必填 ====================

        /// <summary>所属原版物品 ID（换手守门与攻速读取）</summary>
        public abstract int SourceItemID { get; }
        /// <summary>原版锤头弹幕 ID（锤头贴图与帧数来源）</summary>
        public abstract int VanillaProjID { get; }
        /// <summary>链节贴图（原版 TextureAssets.ChainXX，逐节绘制）</summary>
        public abstract Asset<Texture2D> ChainTexture { get; }
        /// <summary>族调色板重音色（辉光/满转提示/命中光）</summary>
        public abstract Color GlowColor { get; }

        //==================== 手感参数（子类按武器覆写） ====================

        /// <summary>锤头判定边长</summary>
        public virtual int HeadSize => 30;
        /// <summary>链长上限（超过即张力拽回）</summary>
        public virtual float MaxChainLength => 340f;
        /// <summary>基准出手速度（满转再乘 0.85~1.30）</summary>
        public virtual float LaunchSpeed => 16.5f;
        /// <summary>掷出巡航帧数（到点转收链）</summary>
        public virtual int LaunchFrames => 18;
        /// <summary>飞行阻尼（&lt;1，制造减速曲线）</summary>
        public virtual float LaunchDrag => 0.977f;
        /// <summary>收链末段最大回卷速度</summary>
        public virtual float RetractPullMax => 17f;
        /// <summary>回坠帧数（重力主导段）</summary>
        public virtual int RetractSagFrames => 9;
        /// <summary>甩转充满所需帧数</summary>
        public virtual int ChargeFrames => 46;
        /// <summary>甩转角速度下限/上限（弧度每帧）</summary>
        public virtual float SpinOmegaMin => 0.085f;
        public virtual float SpinOmegaMax => 0.335f;
        /// <summary>甩转半径下限/上限（离心感：转速越高甩越开）</summary>
        public virtual float SpinRadiusMin => 40f;
        public virtual float SpinRadiusMax => 96f;
        /// <summary>甩转伤害倍率</summary>
        public virtual float SpinDamageMul => 0.55f;
        /// <summary>收链伤害倍率</summary>
        public virtual float RetractDamageMul => 0.8f;
        /// <summary>链身擦伤倍率（贪婪判定：链条也能刮到人）</summary>
        public virtual float ChainGrazeMul => 0.45f;
        /// <summary>满转出手的伤害加成上限</summary>
        public virtual float ChargeDamageBonus => 0.30f;
        /// <summary>甩转姿态</summary>
        public virtual GsFlailSpinMode SpinMode => GsFlailSpinMode.Orbit;
        /// <summary>锤头朝向：true=按转速自旋（球锤），false=咬住速度方向（刀/拳/铡）</summary>
        public virtual bool SelfSpinHead => true;
        /// <summary>命中反冲进收链（肉感；穿透型武器可关）</summary>
        public virtual bool RecoilOnHit => true;
        /// <summary>是否由本锤头驱动玩家姿态（双锤武器只让 0 号驱动，其余只挂链不抢臂）</summary>
        protected virtual bool ControlsPose => true;

        //==================== 运行时字段 ====================

        /// <summary>状态：0 甩转 / 1 掷出 / 2 收链</summary>
        protected int State {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        /// <summary>出手瞬间锁定的转速充能 0~1（随生成后的 netUpdate 过线）</summary>
        protected float LaunchCharge => Projectile.ai[1];
        /// <summary>武器自定义载荷（生成时写入）</summary>
        protected float WeaponAi2 => Projectile.ai[2];

        protected int spinTimer;
        protected int flightTimer;
        protected int retractTimer;
        protected float spinAngle;
        protected float spinCharge;
        protected int swingSign = 1;
        protected bool fullChargeAnnounced;
        protected int chargeFlashTimer;
        private float lastRevAngle;
        private bool chainGrazeHit;
        private int catchGraceTimer;
        /// <summary>本帧链条采样点（手→头），AI 期构建，判定与绘制同源</summary>
        protected readonly List<Vector2> chainPoints = new(28);
        /// <summary>本帧手部锚点（链条起点）</summary>
        protected Vector2 handAnchor;
        private float armRotation;
        private Player.CompositeArmStretchAmount armStretch = Player.CompositeArmStretchAmount.Full;

        /// <summary>攻速倍率（词条真实生效：角速度/出手速/回卷速全吃）</summary>
        protected float AtkSpeed {
            get {
                Item held = Owner.HeldItem;
                float speed = held != null && held.type == SourceItemID
                    ? Owner.GetWeaponAttackSpeed(held) : 1f;
                return MathHelper.Clamp(speed, 0.5f, 3f);
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;
        }

        public sealed override void SetDefaults() {
            Projectile.width = Projectile.height = HeadSize;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = 3600;
            SetFlailDefaults();
        }

        /// <summary>子类补充弹幕字段（别动宽高与伤害语义）</summary>
        public virtual void SetFlailDefaults() { }

        public override void Initialize() {
            swingSign = Owner.direction >= 0 ? 1 : -1;
            spinAngle = (-Vector2.UnitY * swingSign).ToRotation();
            Projectile.Center = Owner.MountedCenter;
        }

        //==================== 主循环 ====================

        public override void AI() {
            //换手/异常守门：直接快收，不留孤链
            Item held = Owner.HeldItem;
            bool holderValid = held != null && held.type == SourceItemID
                && !Owner.dead && !Owner.CCed && !Owner.noItems;
            if (!holderValid && State != StateRetract) {
                EnterRetract(skipSag: true);
            }
            if (Owner.Center.Distance(Projectile.Center) > MaxChainLength + 620f) {
                Projectile.Kill();
                return;
            }

            switch (State) {
                case StateSpin:
                    SpinBehavior();
                    break;
                case StateLaunch:
                    LaunchBehavior();
                    break;
                default:
                    RetractBehavior();
                    break;
            }

            if (chargeFlashTimer > 0) {
                chargeFlashTimer--;
            }
            UpdatePose();
            BuildChainPoints();
            Lighting.AddLight(Projectile.Center, GlowColor.ToVector3() * (0.22f + spinCharge * 0.3f));
            PostStateAI();
        }

        /// <summary>状态机之后的每帧钩子（武器专属演出/子弹幕）</summary>
        protected virtual void PostStateAI() { }

        /// <summary>甩转：角速度沿充能曲线爬升，链条从下垂到绷直——加速度可见</summary>
        private void SpinBehavior() {
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.localNPCHitCooldown = 14;
            spinTimer++;

            float speedMul = AtkSpeed;
            spinCharge = MathHelper.Clamp(spinTimer * speedMul / ChargeFrames, 0f, 1f);
            //加速度曲线：前段慢起后段猛拉，绝不匀速
            float accel = spinCharge * spinCharge * (3f - 2f * spinCharge);
            float omega = MathHelper.Lerp(SpinOmegaMin, SpinOmegaMax, accel * accel) * speedMul;

            if (SpinMode == GsFlailSpinMode.Orbit) {
                spinAngle += omega * swingSign;
                float radius = MathHelper.Lerp(SpinRadiusMin, SpinRadiusMax, accel);
                Vector2 orbit = spinAngle.ToRotationVector2();
                orbit.Y *= 0.92f;
                Projectile.Center = Owner.MountedCenter + orbit * radius;
                //每过一整圈甩一记风声，音高随转速爬
                if (!VaultUtils.isServer && Math.Abs(spinAngle - lastRevAngle) >= MathHelper.TwoPi) {
                    lastRevAngle = spinAngle;
                    SoundEngine.PlaySound(SoundID.Item1 with {
                        Volume = 0.3f + spinCharge * 0.3f,
                        Pitch = -0.45f + spinCharge * 0.7f,
                        MaxInstances = 3
                    }, Projectile.Center);
                }
            }
            else {
                //蓄压：锤头收在身侧震颤，链条盘绕；抖动用确定性正弦，不掷 Main.rand
                spinAngle += omega * swingSign * 0.3f;
                float shake = spinCharge * 2.6f;
                Vector2 brace = Owner.MountedCenter
                    + new Vector2(-Owner.direction * 16f, 6f)
                    + new Vector2(MathF.Sin(spinTimer * 1.63f), MathF.Cos(spinTimer * 2.11f)) * shake;
                Projectile.Center = Vector2.Lerp(Projectile.Center, brace, 0.45f);
            }
            Projectile.velocity = Vector2.Zero;

            if (spinCharge >= 1f && !fullChargeAnnounced) {
                fullChargeAnnounced = true;
                chargeFlashTimer = 9;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = 0.25f }, Owner.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, GlowColor, 0.4f);
                }
                OnFullCharge();
            }
            OnSpinTick(spinCharge);

            //松手即掷（owner 权威，转速写进 ai[1] 随包过线）
            if (Projectile.IsOwnedByLocalPlayer() && !DownLeft) {
                Projectile.ai[1] = spinCharge;
                State = StateLaunch;
                flightTimer = 0;
                float power = MathHelper.Lerp(0.85f, 1.30f, EaseOutQuad(spinCharge));
                Projectile.velocity = UnitToMouseV * LaunchSpeed * power * AtkSpeed
                    + Owner.velocity * 0.35f;
                Projectile.netUpdate = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with {
                        Volume = 0.9f,
                        Pitch = -0.3f + spinCharge * 0.3f
                    }, Owner.Center);
                }
                OnLaunch(spinCharge);
            }
        }

        /// <summary>掷出：阻尼减速+微重力压弧线；张力绷满/超时/命中即收链</summary>
        private void LaunchBehavior() {
            Projectile.tileCollide = true;
            Projectile.ownerHitCheck = false;
            Projectile.localNPCHitCooldown = 8;
            flightTimer++;
            spinCharge = LaunchCharge;

            Projectile.velocity *= LaunchDrag;
            Projectile.velocity.Y += 0.09f;

            float dist = Projectile.Center.Distance(Owner.MountedCenter);
            bool tension = dist >= MaxChainLength;
            if (tension && !VaultUtils.isServer) {
                //链条绷到头：铁链锉响一声
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            }
            if (Projectile.IsOwnedByLocalPlayer()
                && (tension || flightTimer >= LaunchFrames || Projectile.velocity.Length() < LaunchSpeed * 0.30f)) {
                EnterRetract(skipSag: false);
            }
            OnLaunchTick(flightTimer);
        }

        /// <summary>收链：先回坠（重力赢过拉力、链条塌垂）再加速度回卷——两段式不匀速</summary>
        private void RetractBehavior() {
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = false;
            Projectile.localNPCHitCooldown = 12;
            retractTimer++;
            spinCharge = LaunchCharge;

            Vector2 toOwner = Projectile.Center.To(Owner.MountedCenter);
            float dist = toOwner.Length();
            Vector2 pullDir = toOwner.SafeNormalize(Vector2.Zero);

            if (retractTimer <= RetractSagFrames) {
                //回坠段：头往下坠，拉力只轻轻兜着
                Projectile.velocity.Y += 0.46f;
                Projectile.velocity.X *= 0.975f;
                Projectile.velocity = Projectile.velocity.MoveTowards(pullDir * 5f, 0.55f);
            }
            else {
                //回卷段：拉力按加速度曲线拉满
                float ramp = MathHelper.Clamp((retractTimer - RetractSagFrames) / 16f, 0f, 1f);
                float accel = MathHelper.Lerp(0.9f, 3.4f, ramp * ramp);
                Projectile.velocity *= 0.965f;
                Projectile.velocity = Projectile.velocity.MoveTowards(
                    pullDir * RetractPullMax * AtkSpeed, accel);
            }

            catchGraceTimer++;
            if (dist <= 46f && catchGraceTimer > 6) {
                //收回手中：轻拍一声，头上一点余光
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = -0.1f }, Owner.Center);
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GlowColor, 0.12f)
                        ?.Configure(8, 0.7f);
                }
                OnCaught();
                Projectile.Kill();
                return;
            }
            if (retractTimer > 110) {
                Projectile.Kill();
            }
        }

        /// <summary>进入收链（skipSag=true 跳过回坠直接猛拉，用于换手守门）</summary>
        protected void EnterRetract(bool skipSag) {
            if (State == StateRetract) {
                return;
            }
            State = StateRetract;
            retractTimer = skipSag ? RetractSagFrames + 1 : 0;
            catchGraceTimer = 0;
            Projectile.netUpdate = true;
            OnRetractStart();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //撞砖：弹一下再收链，铛
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                        -oldVelocity.RotatedByRandom(0.6) * Main.rand.NextFloat(0.05f, 0.16f), 120);
                    d.noGravity = true;
                }
            }
            Vector2 bounce = Projectile.velocity;
            if (Projectile.velocity.X != oldVelocity.X) {
                bounce.X = -oldVelocity.X * 0.35f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                bounce.Y = -oldVelocity.Y * 0.35f;
            }
            Projectile.velocity = bounce;
            OnTileImpact(oldVelocity);
            if (Projectile.IsOwnedByLocalPlayer() && State == StateLaunch) {
                EnterRetract(skipSag: false);
            }
            return false;
        }

        //==================== 姿态 ====================

        /// <summary>接管持械姿态：手臂追锤、身随链走</summary>
        private void UpdatePose() {
            if (!ControlsPose) {
                //从锤：不抢姿态，链条锚到躯干近肩处
                handAnchor = Owner.MountedCenter + new Vector2(-Owner.direction * 4f, -2f);
                return;
            }
            SetHeld();
            Owner.SetDummyItemTime(2);

            if (State == StateSpin) {
                //甩转期看鼠标（owner 权威，ToMouse 已同步）；正上方零值保持原朝向
                int mouseDir = Math.Sign(ToMouse.X);
                if (mouseDir != 0) {
                    Owner.ChangeDir(mouseDir);
                }
            }
            else {
                Owner.ChangeDir(Projectile.Center.X >= Owner.MountedCenter.X ? 1 : -1);
            }

            float toHeadRot = Owner.MountedCenter.To(Projectile.Center).ToRotation();
            Owner.itemRotation = MathHelper.WrapAngle(
                Projectile.Center.X < Owner.MountedCenter.X ? toHeadRot + MathHelper.Pi : toHeadRot);

            if (State == StateSpin && SpinMode == GsFlailSpinMode.Brace) {
                //蓄压臂姿：拳收肩后，充能越满收得越紧
                armStretch = Player.CompositeArmStretchAmount.ThreeQuarters;
                armRotation = -MathHelper.PiOver2 - Owner.direction * (0.9f + spinCharge * 0.5f);
            }
            else {
                armStretch = State == StateSpin
                    ? Player.CompositeArmStretchAmount.ThreeQuarters
                    : Player.CompositeArmStretchAmount.Full;
                armRotation = toHeadRot - MathHelper.PiOver2;
            }
            Owner.SetCompositeArmFront(true, armStretch, armRotation);
            handAnchor = Owner.GetFrontHandPosition(armStretch, armRotation);
        }

        //==================== 链体（判定与绘制同源） ====================

        /// <summary>
        /// 构建链条采样点：手→头的二次贝塞尔。垂度=松弛量×状态系数：
        /// 甩转低速下垂高速绷直、回坠段大幅塌垂、回卷段收紧——链条本身就是加速度的可视化
        /// </summary>
        private void BuildChainPoints() {
            chainPoints.Clear();
            Vector2 head = Projectile.Center;
            Vector2 hand = handAnchor == Vector2.Zero ? Owner.MountedCenter : handAnchor;
            float dist = hand.Distance(head);
            float slack = MathHelper.Clamp(1f - dist / MaxChainLength, 0f, 1f);

            Vector2 sag;
            switch (State) {
                case StateSpin: {
                    //甩转：垂度随充能收紧，外加一截逆切线滞后（链条追不上头）
                    float droop = MathHelper.Lerp(26f, 3f, spinCharge) * (0.4f + slack * 0.6f);
                    Vector2 lag = SpinMode == GsFlailSpinMode.Orbit
                        ? (spinAngle + MathHelper.PiOver2 * swingSign).ToRotationVector2() * -MathHelper.Lerp(6f, 16f, spinCharge)
                        : Vector2.Zero;
                    sag = Vector2.UnitY * droop + lag;
                    break;
                }
                case StateLaunch: {
                    //掷出：链被拉直，只留一点点反速度方向的鞭尾
                    sag = Vector2.UnitY * (4f + slack * 6f)
                        - Projectile.velocity.SafeNormalize(Vector2.Zero) * 5f;
                    break;
                }
                default: {
                    //回坠段塌垂最大，回卷段线性收紧
                    float sagAmp = retractTimer <= RetractSagFrames
                        ? MathHelper.Lerp(14f, 44f, retractTimer / (float)RetractSagFrames)
                        : MathHelper.Lerp(44f, 5f,
                            MathHelper.Clamp((retractTimer - RetractSagFrames) / 18f, 0f, 1f));
                    sag = Vector2.UnitY * sagAmp * (0.35f + slack * 0.65f);
                    break;
                }
            }

            Vector2 control = (hand + head) * 0.5f + sag;
            const int samples = 22;
            for (int i = 0; i <= samples; i++) {
                float t = i / (float)samples;
                Vector2 a = Vector2.Lerp(hand, control, t);
                Vector2 b = Vector2.Lerp(control, head, t);
                chainPoints.Add(Vector2.Lerp(a, b, t));
            }
        }

        /// <summary>贪婪判定：锤头矩形 + 掷出/收链期链身逐段线判定（擦到链条按倍率结伤）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            chainGrazeHit = false;
            Rectangle headBox = projHitbox;
            headBox.Inflate(8, 8);
            if (headBox.Intersects(targetHitbox)) {
                return true;
            }
            if (State == StateSpin || chainPoints.Count < 2) {
                return false;
            }
            //链身只在放出去够长时participate，贴身不啃自己
            if (Projectile.Center.Distance(Owner.MountedCenter) < 100f) {
                return false;
            }
            float _ = 0f;
            for (int i = 0; i < chainPoints.Count - 1; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    chainPoints[i], chainPoints[i + 1], 8f, ref _)) {
                    chainGrazeHit = true;
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            if (State == StateSpin) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Projectile.Center, Projectile.Center + Projectile.velocity,
                Projectile.width * 0.9f, DelegateMethods.CutTiles);
        }

        //==================== 伤害结算 ====================

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            float mul = State switch {
                StateSpin => SpinDamageMul,
                StateRetract => RetractDamageMul,
                _ => 1f + ChargeDamageBonus * EaseOutQuad(LaunchCharge),
            };
            if (chainGrazeHit) {
                mul *= ChainGrazeMul;
            }
            modifiers.SourceDamage *= mul;
            modifiers.HitDirectionOverride = Projectile.Center.X >= Owner.MountedCenter.X ? 1 : -1;
            ModifyFlailHit(target, ref modifiers);
        }

        /// <summary>族倍率结算后的武器伤害钩子</summary>
        protected virtual void ModifyFlailHit(NPC target, ref NPC.HitModifiers modifiers) { }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool headHit = !chainGrazeHit;
            //时序契约：命中钩先于状态转换——钩子读到的 State/velocity 是命中时刻的值，
            //子类查 State == StateLaunch 的满转 payoff 才可达；反冲收链在钩子之后执行，
            //掷出实打首中即转收链，保证满转 payoff 一掷至多结算一次
            if (!VaultUtils.isServer && headHit) {
                SpawnHitBurst(target, hit, LaunchCharge);
            }
            OnHeadHit(target, hit, damageDone, headHit);
            //锤头实打命中的物理反冲：弹开并转入收链，链感落地
            if (headHit && State == StateLaunch && RecoilOnHit && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.velocity = -Projectile.velocity.SafeNormalize(Vector2.Zero)
                    * MathF.Max(6f, Projectile.velocity.Length() * 0.35f);
                EnterRetract(skipSag: false);
                Projectile.netUpdate = true;
            }
        }

        /// <summary>族默认命中反馈：重音色火花+闷响，满转命中升级一圈脉冲</summary>
        protected virtual void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.4f }, target.Center);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            int sparks = 3 + (int)(charge * 4f);
            for (int i = 0; i < sparks; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    -dir.RotatedByRandom(0.85) * Main.rand.NextFloat(3f, 6.5f),
                    GlowColor, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(10, 18));
            }
            if (charge >= 0.99f && State == StateLaunch) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, GlowColor, 0.5f);
            }
        }

        //==================== 武器钩子面 ====================

        /// <summary>甩转每帧（charge 0~1）</summary>
        protected virtual void OnSpinTick(float charge) { }
        /// <summary>转速拉满的一瞬（一次性）</summary>
        protected virtual void OnFullCharge() { }
        /// <summary>出手瞬间（owner 端）</summary>
        protected virtual void OnLaunch(float charge) { }
        /// <summary>掷出飞行每帧</summary>
        protected virtual void OnLaunchTick(int flightTime) { }
        /// <summary>转入收链瞬间</summary>
        protected virtual void OnRetractStart() { }
        /// <summary>锤头命中（owner 端；headHit=false 表示链身擦伤）</summary>
        protected virtual void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) { }
        /// <summary>撞砖瞬间</summary>
        protected virtual void OnTileImpact(Vector2 oldVelocity) { }
        /// <summary>回到手中（Kill 前）</summary>
        protected virtual void OnCaught() { }

        //==================== 绘制（链节逐段 + 锤头三层） ====================

        /// <summary>链节贴图帧（滴血链等多帧链覆写；linkIndex 从手侧数起）</summary>
        public virtual Rectangle? ChainFrame(int linkIndex) => null;

        /// <summary>链节染色（t: 0=手 1=头；烈焰链近头炽亮之类在此做）</summary>
        public virtual Color ChainLinkColor(int linkIndex, float t, Color light) => light;

        /// <summary>锤头自旋角速度（SelfSpinHead=true 时用）</summary>
        protected virtual float HeadSpinRate =>
            State == StateSpin
                ? MathHelper.Lerp(0.05f, 0.3f, spinCharge) * swingSign
                : MathHelper.Clamp(Projectile.velocity.X * 0.035f, -0.4f, 0.4f);

        public override bool PreDraw(ref Color lightColor) {
            DrawChain();
            DrawHead(lightColor);
            return false;
        }

        /// <summary>沿贝塞尔垂链逐节铺链——每节独立取光、独立朝向，链条是曲线不是直线</summary>
        protected void DrawChain() {
            Texture2D chain = ChainTexture?.Value;
            if (chain == null || chainPoints.Count < 2) {
                return;
            }
            float linkLen = MathF.Max(6f, ChainFrame(0)?.Height ?? chain.Height);
            float carried = 0f;
            int linkIndex = 0;
            float total = 0f;
            for (int i = 0; i < chainPoints.Count - 1; i++) {
                total += chainPoints[i].Distance(chainPoints[i + 1]);
            }
            float walked = 0f;
            for (int i = 0; i < chainPoints.Count - 1; i++) {
                Vector2 a = chainPoints[i];
                Vector2 b = chainPoints[i + 1];
                Vector2 seg = b - a;
                float segLen = seg.Length();
                if (segLen <= 0.001f) {
                    continue;
                }
                Vector2 dir = seg / segLen;
                float rot = dir.ToRotation() + MathHelper.PiOver2;
                float pos = carried;
                while (pos < segLen) {
                    Vector2 at = a + dir * pos;
                    float t = total > 0f ? (walked + pos) / total : 0f;
                    Rectangle? frame = ChainFrame(linkIndex);
                    Vector2 origin = frame.HasValue
                        ? frame.Value.Size() / 2f : chain.Size() / 2f;
                    Color light = Lighting.GetColor((int)(at.X / 16f), (int)(at.Y / 16f));
                    Main.EntitySpriteDraw(chain, at - Main.screenPosition, frame,
                        ChainLinkColor(linkIndex, t, light), rot, origin, 1f, SpriteEffects.None, 0);
                    pos += linkLen;
                    linkIndex++;
                }
                carried = pos - segLen;
                walked += segLen;
            }
        }

        /// <summary>锤头三层：速度残影垫底 → 本体 → 充能辉光；PostDrawHead 加武器专属层</summary>
        protected void DrawHead(Color lightColor) {
            Main.instance.LoadProjectile(VanillaProjID);
            Texture2D tex = TextureAssets.Projectile[VanillaProjID].Value;
            int frameCount = Math.Max(1, Main.projFrames[VanillaProjID]);
            Rectangle frame = tex.Frame(1, frameCount, 0, (int)(Main.GameUpdateCount / 5) % frameCount);
            Vector2 origin = frame.Size() / 2f;
            float rot = SelfSpinHead
                ? Projectile.rotation
                : Projectile.velocity.Length() > 1f
                    ? Projectile.velocity.ToRotation()
                    : Projectile.rotation;

            //高速期残影：旋转涂抹/直线拖影都吃 oldPos 缓存
            float speedNow = State == StateSpin
                ? spinCharge
                : MathHelper.Clamp(Projectile.velocity.Length() / (LaunchSpeed * 1.2f), 0f, 1f);
            if (speedNow > 0.35f) {
                for (int g = 1; g < Projectile.oldPos.Length; g++) {
                    Vector2 gp = Projectile.oldPos[g];
                    if (gp == Vector2.Zero) {
                        continue;
                    }
                    float fade = (1f - g / (float)Projectile.oldPos.Length) * 0.28f * speedNow;
                    Color ghost = GlowColor * fade;
                    ghost.A = 0;
                    Main.EntitySpriteDraw(tex, gp + Projectile.Size / 2f - Main.screenPosition,
                        frame, ghost, Projectile.oldRot[g], origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition,
                frame, lightColor, rot, origin, Projectile.scale, SpriteEffects.None, 0);

            //充能/满转辉光罩层
            float glowAmp = spinCharge * 0.3f + chargeFlashTimer / 9f * 0.4f;
            if (glowAmp > 0.02f) {
                Color glow = GlowColor * glowAmp;
                glow.A = 0;
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition,
                    frame, glow, rot, origin, Projectile.scale * 1.06f, SpriteEffects.None, 0);
            }
            PostDrawHead(lightColor, rot, frame, origin);
        }

        /// <summary>锤头之上再叠武器专属层（炽熔皮肤/月晕之类）；绘制禁 Main.rand，抖动用 identity 种子</summary>
        protected virtual void PostDrawHead(Color lightColor, float headRotation, Rectangle frame, Vector2 origin) { }

        /// <summary>锤头自旋在 AI 前推进（TrailingMode=2 需要 oldRot 逐帧记录）</summary>
        public override bool PreUpdate() {
            if (SelfSpinHead) {
                Projectile.rotation += HeadSpinRate;
            }
            return true;
        }

        protected static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
