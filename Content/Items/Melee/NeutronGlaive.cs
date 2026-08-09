using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class NeutronGlaive : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";

        /// <summary>连段计数，取模三拍</summary>
        private int comboCounter;
        /// <summary>连段重置倒计时，断手后回到第一拍</summary>
        private int comboResetTimer;

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 16));
        }

        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 855;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 13;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item60;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<NeutronGlaiveHeld>();
            Item.shootSpeed = 18f;
            //noMelee 会丢近战词缀，这里强行标回
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<NeutronStarIngot>(11)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override bool CanUseItem(Player player) {
            Item.UseSound = SoundID.Item60;
            if (player.altFunctionUse == 2) {
                Item.UseSound = SoundID.AbigailAttack;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeldAlt>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeld>()] == 0;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void HoldItem(Player player) {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<NeutronGlaiveHeldAlt>(), damage, knockback, player.whoAmI);
                comboCounter = 0;//右键打断连段
                return false;
            }

            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            comboResetTimer = 60;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }
    }

    /// <summary>黑域斩切单拍的形状与节奏，出手时冻结</summary>
    internal struct NeutronSwingBeat
    {
        public float Span;              //弧跨度（弧度）
        public float Squash;            //短轴/长轴，0.53~0.62 读作倾斜圆而非压扁贴纸
        public float OffsetAlongAim;    //弧心沿瞄准偏移，正=推离身体，负=拉回身后
        public float Radius;            //长半轴，刃尖轨迹
        public float Thick;             //厚度峰值，占投影半径比
        public float ForcePoint;        //厚度峰值位置 0~1，越小越偏入刀侧
        public float Depth;             //伪 z 权重，0=平面
        public float Pullback;          //蓄势回拉，占弧长比
        public int Gather;              //蓄势帧
        public int Hold;                //死寂滞帧
        public int Burst;               //爆发帧
        public int Recover;             //收势帧
        public float Lean;              //爆发峰值身体前倾（弧度）
        public float DamageScale;
        public float BladeScale;
        public int HitStop;             //命中停驻帧

        public readonly int Total => Gather + Hold + Burst + Recover;
        public readonly int BurstStart => Gather + Hold;
        public readonly int BurstStop => Gather + Hold + Burst;
    }

    /// <summary>
    /// 黑域斩切的挥砍投影：把刀光椭圆当成一个倾斜的圆来算。<br/>
    /// 刀身缩放、缎带半宽、碰撞线、引力透镜采样全部从这里出，
    /// 单一投影源，从根上避免"刀比刀光大"那类各算各的错位
    /// </summary>
    internal static class NeutronSwingArc
    {
        /// <summary>透视参考距离基准（px）</summary>
        private const float BaseViewZ = 900f;
        /// <summary>远半侧压暗上限</summary>
        private const float FarDimAmount = 0.55f;

        public static float Saturate(float x) => MathHelper.Clamp(x, 0f, 1f);

        public static float SmoothStep01(float x) {
            x = Saturate(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>缓入，起手几乎察觉不到、末段才把刀收紧</summary>
        public static float EaseInCubic(float x) {
            x = Saturate(x);
            return x * x * x;
        }

        /// <summary>大弧要抬高参考距离，否则透视除会炸开</summary>
        public static float ViewZFor(float radius) => MathF.Max(BaseViewZ, radius * 4.4f);

        /// <summary>透视缩放，z 朝观者(+)放大、沉入画面(-)缩小</summary>
        public static float PerspectiveK(float z, float viewZ)
            => viewZ / MathF.Max(viewZ - z, viewZ * 0.26f);

        /// <summary>伪 z 幅度，由长短轴差导出；正圆没有深度</summary>
        public static float DepthAmp(in NeutronSwingBeat beat, float radius) {
            float hy = radius * beat.Squash;
            return MathF.Sqrt(MathF.Abs(radius * radius - hy * hy));
        }

        /// <summary>
        /// 刃尖相对弧心的投影偏移。位置相位带 flip（挥动方向镜像），
        /// 深度相位不带——深度剖面沿笔画固定：起笔沉在身后、收笔迎向镜头。
        /// Squash&gt;1（横宽月牙，引力波剑气用）时压缩换到另一根轴，z 相位随之切换
        /// </summary>
        public static Vector2 TipOffset(in NeutronSwingBeat beat, float radius, float uc, float flip
            , Vector2 axisX, out float projRadius, out float depth) {
            Vector2 axisY = new(-axisX.Y, axisX.X);
            float hy = radius * beat.Squash;
            float phiPos = flip * (uc - 0.5f) * beat.Span;
            float phiZ = (uc - 0.5f) * beat.Span;
            float zPhase = radius >= hy ? MathF.Sin(phiZ) : MathF.Cos(phiZ);
            depth = zPhase * DepthAmp(in beat, radius) * 0.9f * beat.Depth;
            float k = PerspectiveK(depth, ViewZFor(MathF.Max(radius, hy)));
            Vector2 local = ((axisX * MathF.Cos(phiPos) * radius) + (axisY * MathF.Sin(phiPos) * hy)) * k;
            projRadius = local.Length();
            return local;
        }

        /// <summary>远半侧压暗系数，1=近侧全亮</summary>
        public static float DepthDim(in NeutronSwingBeat beat, float radius, float depth) {
            float amp = DepthAmp(in beat, radius);
            if (amp <= 1f) {
                return 1f;
            }
            return 1f - (FarDimAmount * Saturate(-depth / amp));
        }

        /// <summary>带受力点的厚度包络：薄锐入刀 → 峰值偏置 → 撕裂厚出</summary>
        public static float ThickEnvelope(in NeutronSwingBeat beat, float uc) {
            float fp = MathHelper.Clamp(beat.ForcePoint, 0.1f, 0.9f);
            float side = uc < fp ? uc / fp : (1f - uc) / (1f - fp);
            float sharp = uc < fp ? 1.7f : 0.72f;
            return beat.Thick * MathF.Pow(Saturate(side), sharp);
        }
    }

    /// <summary>
    /// 黑域斩切主挥砍。三拍形状递进：两记推离身体的定向切 → 一记拉回身后的巨型月牙。<br/>
    /// 时间线 蓄势(缓入回拉+呼吸震颤) → 死寂滞帧 → 爆发(近匀速跨弧+过冲+半径弹开) → 几何冻结，
    /// 力量来自蓄爆比与身体甩动，不靠缓动曲线。<br/>
    /// 刀身、缎带、碰撞、引力透镜共用 <see cref="NeutronSwingArc"/> 一个投影源；
    /// 刀光走 NeutronSlashTrail.fx，背景扭曲走 NeutronWarp 的 GravitationalLens
    /// </summary>
    internal class NeutronGlaiveHeld : BaseHeldProj, IWarpDrawable, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<NeutronGlaive>();

        private const int FrameCount = 16;
        /// <summary>爆发末端过冲量，回坐两帧后冻结</summary>
        private const float Overshoot = 1.06f;
        /// <summary>刀身贴图中心停在手→刃尖的几成处，刀柄因此落在手上略后（长柄该有的配重）</summary>
        private const float BladePark = 0.5f;
        /// <summary>
        /// 刀尖顶到手→刃尖的几成。略微过 1 是故意的：
        /// 刀尖要压在刀光带的外缘上，让刀成为画面主体而不是被色片盖住的配角
        /// </summary>
        private const float BladeTipFill = 1.06f;

        /// <summary>
        /// 三拍形状表。轻拍是推出去的定向切（两记互为镜像画 X），
        /// 终结是唯一一记拉回身后的巨型月牙——形状升级才让终结显得贵。
        /// <br/>帧预算按"输入到打击"优化：总帧维持 28/28/36（出手频率与 DPS 不动），
        /// 但蓄势砍到 6/6/8，省下的帧全丢进收势——静止谷留在斩完之后，
        /// 而不是压在玩家按下左键之后，否则读作慢速刀。
        /// <br/><see cref="BladeScale"/> 现在只是细调倍率，刀身实际大小由
        /// <see cref="GetBladeDrawScale"/> 从刀光半径反推。
        /// <br/><see cref="NeutronSwingBeat.Thick"/> 刻意压薄：厚带会让观众的主体
        /// 变成一张大色片而不是刀，光带该是刃口而不是床单
        /// </summary>
        private static readonly NeutronSwingBeat[] Beats = [
            new NeutronSwingBeat {
                Span = 2.5f, Squash = 0.58f, OffsetAlongAim = 38f, Radius = 168f,
                Thick = 0.115f, ForcePoint = 0.36f, Depth = 1f, Pullback = 0.13f,
                Gather = 6, Hold = 1, Burst = 3, Recover = 18,
                Lean = 0.07f, DamageScale = 1f, BladeScale = 1f, HitStop = 1,
            },
            new NeutronSwingBeat {
                Span = 2.5f, Squash = 0.56f, OffsetAlongAim = 44f, Radius = 174f,
                Thick = 0.12f, ForcePoint = 0.64f, Depth = 1f, Pullback = 0.13f,
                Gather = 6, Hold = 1, Burst = 3, Recover = 18,
                Lean = 0.07f, DamageScale = 1f, BladeScale = 1f, HitStop = 1,
            },
            new NeutronSwingBeat {
                Span = 3.5f, Squash = 0.55f, OffsetAlongAim = -30f, Radius = 208f,
                Thick = 0.155f, ForcePoint = 0.55f, Depth = 1f, Pullback = 0.15f,
                Gather = 8, Hold = 2, Burst = 4, Recover = 22,
                Lean = 0.3f, DamageScale = 1.35f, BladeScale = 1.14f, HitStop = 2,
            },
        ];

        /// <summary>连段拍号 0/1=定向切 2=巨型月牙</summary>
        private ref float ComboIndex => ref Projectile.ai[0];
        /// <summary>挥动方向 ±1</summary>
        private ref float SwingDirAi => ref Projectile.ai[1];

        private int BeatIndex => Math.Clamp((int)ComboIndex, 0, Beats.Length - 1);
        private ref readonly NeutronSwingBeat Beat => ref Beats[BeatIndex];
        private bool IsFinisher => BeatIndex >= 2;

        private static readonly Color NeutronViolet = new(138, 80, 255);
        private static readonly Color NeutronBlue = new(120, 180, 255);

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float aimAngle;
        private float currentRotation;
        private float lastRotation;
        private bool slashSoundPlayed;
        private bool waveFired;
        private float trailFade = 1f;

        //本帧由投影解算，绘制与碰撞共享
        private float sweepUc;
        private float radiusMul = 0.82f;
        private Vector2 arcCenter;
        private Vector2 tipWorld;
        private float bladeReachNow = 1f;
        private float bladeDepth;

        //命中反馈
        private int impactHoldFrames;
        /// <summary>已被停驻吃掉的帧，从收势尾巴里等量扣回，保证顿帧对 DPS 中性</summary>
        private int hitStopSpent;
        private readonly HashSet<int> hitNPCs = [];

        //伤害窗用闩锁而非区间判断，见 UpdateDamageWindow
        private bool damageArmed;
        private bool damageWindowClosed;

        //身体演出，写者仲裁靠比对上帧自己写进去的值
        private float leanAmount;
        private float appliedLean;
        private bool leanOwned;

        //刀光缎带：只在爆发段追加，尾端靠 trailTail 蒸发，全程 O(1)
        private const int TrailMax = 96;
        private readonly TrailNode[] trail = new TrailNode[TrailMax];
        private int trailCount;
        private int trailTail;
        private float lastPushedUc;

        /// <summary>缎带上一个采样点，位置与半宽都已经过投影</summary>
        private struct TrailNode
        {
            public float Uc;        //弧参数，决定厚度包络
            public Vector2 Tip;     //世界刃尖
            public Vector2 Radial;  //弧心指向刃尖的单位向量，缎带沿此展宽
            public float HalfWidth;
            public float Dim;       //远半侧压暗
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 66;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.scale = 1.45f;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => damageArmed;

        /// <summary>
        /// 伤害窗贴着爆发帧，前后各留一帧余量吃贴脸与擦边。
        /// 用闩锁而不是区间判断：高攻速下 speedMul 一帧能跨过整段窗口，
        /// 区间写法会让这一刀彻底空掉
        /// </summary>
        private void UpdateDamageWindow() {
            if (damageWindowClosed) {
                return;
            }
            if (elapsed >= Beat.BurstStart - 1f) {
                damageArmed = true;
            }
            if (damageArmed && elapsed > Beat.BurstStop + 2f) {
                damageArmed = false;
                damageWindowClosed = true;
            }
        }

        /// <summary>停驻吃掉的帧从收势尾巴扣回，但不许啃进回坐的两帧</summary>
        private float EffectiveTotal
            => Beat.Total - Math.Min(hitStopSpent, Math.Max(Beat.Recover - 4, 0));

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            //贴身段单独兜一次，避免近身空洞——画面重叠了却打不到最伤玩家信任
            if (targetHitbox.Distance(hand) <= 46f) {
                return true;
            }
            Vector2 tip = hand + (currentRotation.ToRotationVector2() * (bladeReachNow + 12f));
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 56f, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + (currentRotation.ToRotationVector2() * (bladeReachNow + 12f));
            Utils.PlotTileLine(hand, tip, 46f, DelegateMethods.CutTiles);
        }

        public override void Initialize() {
            swingSign = Math.Sign(SwingDirAi);
            if (swingSign == 0) {
                swingSign = 1;
            }

            lockedDirection = Math.Sign(Projectile.velocity.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            aimAngle = Projectile.velocity.ToRotation();
            Projectile.scale = Beat.BladeScale;
            if (Beat.DamageScale != 1f) {
                Projectile.damage = (int)(Projectile.damage * Beat.DamageScale);
            }

            UpdateSwingState();
            UpdateBladeTransform();
            lastRotation = currentRotation;
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<NeutronGlaive>() || Owner.dead || !Owner.active) {
                ReleaseBodyLean();
                Projectile.Kill();
                return;
            }
            if (elapsed >= EffectiveTotal) {
                ReleaseBodyLean();
                Projectile.Kill();
                return;
            }

            lastRotation = currentRotation;
            UpdateSwingState();
            UpdateBladeTransform();
            UpdateDamageWindow();
            HandlePhaseEvents();
            UpdatePlayerPose();

            VaultUtils.ClockFrame(ref Projectile.frame, 5, FrameCount - 1);
            Lighting.AddLight(Vector2.Lerp(Owner.GetPlayerStabilityCenter(), tipWorld, 0.7f)
                , NeutronViolet.ToVector3() * 0.7f);

            //命中停驻：只要不推进 elapsed，刀角、揭开进度、前倾就一起冻住。
            //冻掉的帧记账，由 EffectiveTotal 从收势尾巴扣回——顿帧不许偷走 DPS
            if (impactHoldFrames > 0) {
                impactHoldFrames--;
                hitStopSpent++;
            }
            else {
                elapsed += speedMul;
            }
        }

        /// <summary>由 elapsed 解算揭开进度、半径弹开与身体前倾</summary>
        private void UpdateSwingState() {
            ref readonly NeutronSwingBeat beat = ref Beat;
            float gatherEnd = MathF.Max(beat.Gather, 1f);
            float holdEnd = beat.BurstStart;
            float burstEnd = beat.BurstStop;
            float chamber = -beat.Pullback;
            float leanBack = -beat.Lean * 0.55f;

            if (elapsed < gatherEnd) {
                //蓄势：缓入回拉，前段几乎不动、末段才把刀收紧，叠一层呼吸震颤
                float eased = NeutronSwingArc.EaseInCubic(elapsed / gatherEnd);
                sweepUc = (chamber * eased) + (MathF.Sin(elapsed * 2.3f) * 0.015f * (1f - eased));
                radiusMul = MathHelper.Lerp(0.86f, 0.76f, eased);
                leanAmount = leanBack * eased;
                trailFade = 1f;
            }
            else if (elapsed < holdEnd) {
                //死寂滞帧：完全不动，这段静默买的是下一帧的爆炸
                sweepUc = chamber;
                radiusMul = 0.76f;
                leanAmount = leanBack;
                trailFade = 1f;
            }
            else if (elapsed < burstEnd) {
                //爆发：近匀速跨弧，力量来自蓄爆比与身体甩动而非缓动曲线
                float t = (elapsed - holdEnd) / beat.Burst;
                sweepUc = MathHelper.Lerp(chamber, Overshoot, t);
                //半径在头一帧内弹开，力从地起
                radiusMul = MathHelper.Lerp(0.76f, 1.07f, NeutronSwingArc.Saturate(t * 2.4f));
                leanAmount = MathHelper.Lerp(leanBack, beat.Lean, NeutronSwingArc.Saturate(t * 1.7f));
                trailFade = 1f;
            }
            else {
                //收势：两帧过冲回坐后几何冻结，之后只有材质继续消散
                float t = (elapsed - burstEnd) / beat.Recover;
                float settle = NeutronSwingArc.SmoothStep01((elapsed - burstEnd) / 2f);
                sweepUc = MathHelper.Lerp(Overshoot, 1f, settle);
                radiusMul = MathHelper.Lerp(1.07f, 1.0f, settle);
                leanAmount = beat.Lean * (1f - NeutronSwingArc.SmoothStep01(t));
                trailFade = 1f - NeutronSwingArc.SmoothStep01((t - 0.35f) / 0.65f);
            }
        }

        /// <summary>刃尖投影 → 刀角 / 手到刃尖距离 / 刀身轴向前缩短</summary>
        private void UpdateBladeTransform() {
            ref readonly NeutronSwingBeat beat = ref Beat;
            Vector2 axisX = aimAngle.ToRotationVector2();
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float radius = beat.Radius * radiusMul;
            arcCenter = hand + (axisX * beat.OffsetAlongAim * radiusMul);
            Vector2 tipLocal = NeutronSwingArc.TipOffset(in beat, radius, sweepUc, swingSign
                , axisX, out _, out bladeDepth);
            tipWorld = arcCenter + tipLocal;

            Vector2 fromHand = tipWorld - hand;
            bladeReachNow = MathF.Max(fromHand.Length(), 28f);
            currentRotation = fromHand.ToRotation();
        }

        /// <summary>
        /// 刀身画多大由刀光决定：让刀恒定横跨手→刃尖的 <see cref="BladePark"/>±，
        /// 刀柄落在手上、刀尖顶到 <see cref="BladeTipFill"/>。
        /// 贴图每帧只有 78x82（<c>Item.width=154</c> 只是物品显示尺寸），
        /// 用固定 scale 会让刀只剩刀光的四成，读作"小气"；
        /// 反推之后投影前缩短自动继承——刀光塌缩多少，刀就短多少
        /// </summary>
        private float GetBladeDrawScale(Rectangle rect) {
            //刀刃在贴图里沿帧对角走（绘制时补了 ±PiOver4），所以对角长就是刃轴长
            float spriteAxis = MathF.Max(new Vector2(rect.Width, rect.Height).Length(), 1f);
            return bladeReachNow * (BladeTipFill - BladePark) * 2f / spriteAxis * Beat.BladeScale;
        }

        /// <summary>分相事件：爆发帧起音、发剑气、采缎带；收势期只蒸发不追加</summary>
        private void HandlePhaseEvents() {
            ref readonly NeutronSwingBeat beat = ref Beat;
            bool bursting = elapsed >= beat.BurstStart && elapsed < beat.BurstStop + 2f;

            if (elapsed >= beat.BurstStart) {
                //挥砍声与剑气都坐在爆发帧上，压在起手会听成脱拍
                if (!slashSoundPlayed) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        SoundStyle style = IsFinisher
                            ? SoundID.Item71 with { Volume = 0.9f, Pitch = -0.35f }
                            : SoundID.Item71 with { Volume = 0.6f, Pitch = 0.15f };
                        SoundEngine.PlaySound(style, Owner.Center);
                    }
                }
                if (!waveFired) {
                    waveFired = true;
                    FireGravityWave();
                }
            }

            if (bursting) {
                PushTrailSamples();
                SpawnSweepMotes();
            }
            else if (elapsed >= beat.BurstStop + 2f && trailCount > 0) {
                //尾端蒸发，起笔端先散，避免刀光赖在场上叠成笼子
                float t = NeutronSwingArc.Saturate((elapsed - beat.BurstStop - 2f) / MathF.Max(beat.Recover - 2f, 1f));
                int eroded = (int)(trailCount * t * 0.85f);
                trailTail = Math.Clamp(Math.Max(trailTail, eroded), 0, Math.Max(trailCount - 2, 0));
            }
        }

        /// <summary>按弧长增量决定细分数，追加式写入，没有整表搬移</summary>
        private void PushTrailSamples() {
            if (VaultUtils.isServer) {
                return;
            }
            ref readonly NeutronSwingBeat beat = ref Beat;
            //连续性用未夹取的原始 uc，节点里存的是夹取值，不能拿来算增量
            float lastUc = trailCount > 0 ? lastPushedUc : sweepUc;
            float delta = sweepUc - lastUc;
            if (trailCount > 0 && MathF.Abs(delta) < 0.0005f) {
                return;
            }

            Vector2 axisX = aimAngle.ToRotationVector2();
            float radius = beat.Radius * radiusMul;
            //一帧能跨掉大半条弧，按弧长补够中间点，否则缎带是折线
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * beat.Span * radius / 14f), 1, 14);
            for (int i = 1; i <= steps; i++) {
                if (trailCount >= TrailMax) {
                    break;
                }
                float uc = MathHelper.Lerp(lastUc, sweepUc, i / (float)steps);
                float ucClamped = NeutronSwingArc.Saturate(uc);
                Vector2 local = NeutronSwingArc.TipOffset(in beat, radius, uc, swingSign
                    , axisX, out float projRadius, out float depth);
                Vector2 tip = arcCenter + local;
                trail[trailCount++] = new TrailNode {
                    Uc = ucClamped,
                    Tip = tip,
                    Radial = local.SafeNormalize(axisX),
                    //保底半宽，免得月牙两端收成一根头发、被刀身整个盖过
                    HalfWidth = MathF.Max(NeutronSwingArc.ThickEnvelope(in beat, ucClamped) * projRadius, 6f),
                    Dim = NeutronSwingArc.DepthDim(in beat, radius, depth),
                };
                lastPushedUc = uc;
            }
        }

        /// <summary>刃口星屑，沿切线甩出</summary>
        private void SpawnSweepMotes() {
            if (VaultUtils.isServer || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 along = Vector2.Lerp(hand, tipWorld, Main.rand.NextFloat(0.6f, 1.02f));
            Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
            PRTLoader.NewParticle<PRT_HeavenfallStar>(along, tangent * Main.rand.NextFloat(1.5f, 4f)
                , Color.Lerp(NeutronViolet, NeutronBlue, Main.rand.NextFloat())
                , Main.rand.NextFloat(0.25f, 0.4f)).Configure(false, 14);
        }

        /// <summary>
        /// 引力波剑气沿这一刀的实际刀锋甩出去。<br/>
        /// 用横宽月牙而非直线飞弹：直线弹要靠高攻速刷出数量才好看，
        /// 而这套挥砍是重型节奏，一刀就得给出一记看得见的波
        /// </summary>
        private void FireGravityWave() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 dir = currentRotation.ToRotationVector2();
            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + (dir * bladeReachNow * 0.72f);
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spawnPos
                , dir * (IsFinisher ? 17f : 15f)
                , ModContent.ProjectileType<NeutronGravityWave>(), Projectile.damage
                , Projectile.knockBack, Owner.whoAmI, swingSign, IsFinisher ? 1f : 0f);
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters
                , currentRotation - MathHelper.PiOver2 + (0.22f * lockedDirection));
            ApplyBodyLean();
            Projectile.Center = Owner.GetPlayerStabilityCenter() + (currentRotation.ToRotationVector2() * bladeReachNow * 0.5f);
            Projectile.timeLeft = 90;
        }

        /// <summary>
        /// 身体压枪，支点钉在脚下。别的系统（冲刺、坐骑）也写 fullRotation，
        /// 这里比对上帧自己写进去的值来仲裁，一旦被抢走就彻底让位
        /// </summary>
        private void ApplyBodyLean() {
            if (leanOwned && MathF.Abs(Owner.fullRotation - appliedLean) > 0.0001f) {
                //上帧的值被别人改了，说明刀权已让出，之后不再碰
                leanOwned = false;
                return;
            }
            if (!leanOwned && MathF.Abs(Owner.fullRotation) > 0.0001f) {
                return;
            }
            appliedLean = leanAmount * lockedDirection;
            Owner.fullRotation = appliedLean;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.height);
            leanOwned = true;
        }

        /// <summary>每一条退出路径都要复位，卡住的前倾比没有前倾更糟</summary>
        private void ReleaseBodyLean() {
            if (!leanOwned) {
                return;
            }
            if (MathF.Abs(Owner.fullRotation - appliedLean) <= 0.0001f) {
                Owner.fullRotation = 0f;
                Owner.fullRotationOrigin = Vector2.Zero;
            }
            leanOwned = false;
        }

        public override void OnKill(int timeLeft) => ReleaseBodyLean();

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = currentRotation.ToRotationVector2().X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.425f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次挥砍对同一目标只转发一次外部命中钩子
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            ApplyImpactFeedback(target.Center);

            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0, Owner.whoAmI);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            ApplyImpactFeedback(target.Center);
            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0, Owner.whoAmI);
            }
        }

        /// <summary>分级停驻 + 沿切线的镜头冲击，轻拍也要有回应</summary>
        private void ApplyImpactFeedback(Vector2 hitPos) {
            //停驻总预算有限：一刀扫过一群怪也不能把这一拍拖长
            if (hitStopSpent < Math.Max(Beat.Recover - 4, 0)) {
                impactHoldFrames = Math.Max(impactHoldFrames, Beat.HitStop);
            }
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
            float power = IsFinisher ? 5.5f : 2.6f;
            var modifier = new PunchCameraModifier(hitPos, tangent, power
                , IsFinisher ? 6f : 3.5f, IsFinisher ? 10 : 6, 900f, FullName);
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>刀身残影，画在 DrawCustom 的实体刀之下</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (trailCount - trailTail < 2) {
                return false;
            }
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 origin = rect.Size() / 2f;
            GetBladeDraw(out SpriteEffects effect, out float rotOffset);

            float scale = GetBladeDrawScale(rect);
            float angleDelta = MathF.Abs(MathHelper.WrapAngle(currentRotation - lastRotation));
            float strength = NeutronSwingArc.Saturate((angleDelta - 0.05f) / 0.8f);
            int smears = Math.Clamp((int)MathF.Ceiling(angleDelta / 0.2f), 1, 5);
            for (int i = 1; i <= smears && strength > 0f; i++) {
                float amount = i / (float)(smears + 1);
                float rot = MathHelper.Lerp(currentRotation, lastRotation, amount);
                Vector2 pos = Owner.GetPlayerStabilityCenter()
                    + (rot.ToRotationVector2() * bladeReachNow * BladePark) - Main.screenPosition;
                Color smearColor = NeutronViolet * (0.34f * strength * (1f - amount));
                smearColor.A = 0;
                Main.EntitySpriteDraw(tex, pos, rect, smearColor, rot + rotOffset, origin
                    , scale, effect, 0);
            }
            return false;
        }

        private void GetBladeDraw(out SpriteEffects effect, out float rotOffset) {
            bool flip = lockedDirection == -1;
            effect = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flip ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }

        bool IWarpDrawable.CanDrawCustom() => true;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        /// <summary>
        /// 引力透镜：这一刀不是发光，是把背后的空间掰弯。
        /// 只在爆发帧沿弧采样几处，强度跟着爆发进度走
        /// </summary>
        void IWarpDrawable.Warp() {
            ref readonly NeutronSwingBeat beat = ref Beat;
            if (elapsed < beat.BurstStart || elapsed > beat.BurstStop + 3f) {
                return;
            }
            float burstT = NeutronSwingArc.Saturate((elapsed - beat.BurstStart) / MathF.Max(beat.Burst, 1f));
            float decay = 1f - NeutronSwingArc.Saturate((elapsed - beat.BurstStop) / 3f);
            float power = NeutronSwingArc.SmoothStep01(burstT * 2.4f) * decay;
            if (power <= 0.02f) {
                return;
            }

            Vector2 axisX = aimAngle.ToRotationVector2();
            float radius = beat.Radius * radiusMul;
            int samples = IsFinisher ? 3 : 2;
            for (int i = 0; i < samples; i++) {
                float uc = MathHelper.Lerp(0.22f, NeutronSwingArc.Saturate(sweepUc), (i + 0.5f) / samples);
                Vector2 local = NeutronSwingArc.TipOffset(in beat, radius, uc, swingSign
                    , axisX, out float projRadius, out float depth);
                float dim = NeutronSwingArc.DepthDim(in beat, radius, depth);
                float span = projRadius * (0.85f + (beat.Thick * 3.2f));
                NeutronWarpHelper.DrawWarp(arcCenter + local
                    , screenWidth: span, screenHeight: span
                    , intensity: power * dim * (IsFinisher ? 0.62f : 0.4f)
                    , progress: burstT
                    , rotation: currentRotation
                    , technique: "GravitationalLens"
                    , radius: 0.42f);
            }
        }

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) {
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 origin = rect.Size() / 2f;
            GetBladeDraw(out SpriteEffects effect, out float rotOffset);

            //贴图中心停在半程，刀柄落手上、刀尖顶到弧带内侧
            Vector2 drawPos = Owner.GetPlayerStabilityCenter()
                + (currentRotation.ToRotationVector2() * bladeReachNow * BladePark) - Main.screenPosition;
            float scale = GetBladeDrawScale(rect);
            //沉入身后的半侧压暗，与刀光远近分层同一个 z
            float dim = NeutronSwingArc.DepthDim(in Beat, Beat.Radius * radiusMul, bladeDepth);
            spriteBatch.Draw(tex, drawPos, rect, Color.White * dim, currentRotation + rotOffset, origin
                , scale, effect, 0);

            if (IsFinisher) {
                Color glow = NeutronBlue * (0.4f * dim);
                glow.A = 0;
                spriteBatch.Draw(tex, drawPos, rect, glow, currentRotation + rotOffset, origin
                    , scale * 1.04f, effect, 0);
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            int live = trailCount - trailTail;
            if (live < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.NeutronSlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[live * 2];
            for (int i = 0; i < live; i++) {
                ref TrailNode node = ref trail[trailTail + i];
                //沿笔画 0=起笔（最老，先蒸发） 1=收笔（最新，最亮）
                float along = i / (float)(live - 1);
                Vector2 outward = node.Radial * node.HalfWidth;
                Color vertexColor = Color.White * node.Dim;
                bars[i * 2] = new VertexPositionColorTexture((node.Tip + outward).ToVector3()
                    , vertexColor, new Vector2(along, 0f));
                bars[(i * 2) + 1] = new VertexPositionColorTexture((node.Tip - outward).ToVector3()
                    , vertexColor, new Vector2(along, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(trailFade);
            effect.Parameters["uHeat"]?.SetValue(IsFinisher ? 1f : 0.4f);
            effect.Parameters["uForcePoint"]?.SetValue(Beat.ForcePoint);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>
    /// 引力波剑气：挥砍甩出去的一道弧形时空涟漪，物品说明里的"引力波剑气"本体。<br/>
    /// 复用 <see cref="NeutronSwingArc"/> 的倾斜圆投影，和母刀光同一套几何血统，
    /// 只是把长轴放在横向（Squash&gt;1）做成横宽月牙，波峰朝前。<br/>
    /// 飞行中弧面扩张、带宽变薄——引力波振幅随距离衰减，
    /// 沿途背景被 GravitationalLens 掰弯，死后留一层比弹幕活得更久的余痕
    /// </summary>
    internal class NeutronGravityWave : ModProjectile, IWarpDrawable, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>横宽比，月牙宽度约等于前凸的 2.6 倍</summary>
        private const float WaveSquash = 2.6f;
        /// <summary>月牙跨度，半圈：从一侧翼尖穿过波峰到另一侧翼尖</summary>
        private const float WaveSpan = MathHelper.Pi;
        private const int WaveLife = 48;
        /// <summary>寿命末的弧面扩张倍率</summary>
        private const float GrowthEnd = 1.45f;

        /// <summary>挥动方向 ±1，只影响深度相位，与母刀光同源</summary>
        private ref float Flip => ref Projectile.ai[0];
        /// <summary>0=轻拍 1=终结</summary>
        private ref float Tier => ref Projectile.ai[1];

        private bool IsHeavy => Tier >= 1f;
        private float LifeT => NeutronSwingArc.Saturate(1f - (Projectile.timeLeft / (float)WaveLife));

        private static readonly Color WaveViolet = new(138, 80, 255);
        private static readonly Color WaveBlue = new(120, 180, 255);

        private bool payloadSpawned;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            //穿透但每个目标只吃一次，扩张的波面不许变成多段刮伤
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = WaveLife;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        /// <summary>本帧月牙几何，碰撞/绘制/扭曲共用一份，避免三处各算各的</summary>
        private NeutronSwingBeat BuildWave(out float bulge, out Vector2 axisX, out float bandRef) {
            float grow = MathHelper.Lerp(1f, GrowthEnd, NeutronSwingArc.SmoothStep01(LifeT));
            bulge = (IsHeavy ? 56f : 40f) * grow;
            axisX = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            //振幅随扩张衰减：波面越大越薄，这是"波"而不是"贴纸"的关键
            bandRef = bulge * WaveSquash * (IsHeavy ? 0.24f : 0.21f) * (1f - (0.5f * LifeT));
            return new NeutronSwingBeat {
                Span = WaveSpan,
                Squash = WaveSquash,
                Depth = 1f,
                Thick = 1f,
                ForcePoint = 0.5f,
                Radius = bulge,
            };
        }

        /// <summary>波峰最厚、双翼收尖，包络与母刀光同一条曲线</summary>
        private static float BandHalfWidth(in NeutronSwingBeat wave, float u, float bandRef)
            => MathF.Max(NeutronSwingArc.ThickEnvelope(in wave, u) * bandRef, 4f);

        public override void AI() {
            NeutronSwingBeat wave = BuildWave(out float bulge, out Vector2 axisX, out _);
            //波在扩张中散失能量，速度持续衰减，不是匀速平移的贴图
            Projectile.velocity *= 0.985f;
            Projectile.rotation = axisX.ToRotation();

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center + (axisX * bulge * 0.6f)
                , WaveViolet.ToVector3() * (0.85f * (1f - (0.4f * LifeT))));

            //波前星屑，沿弧面法向渗出
            if (Main.rand.NextBool(2)) {
                float u = Main.rand.NextFloat();
                Vector2 local = NeutronSwingArc.TipOffset(in wave, bulge, u, Flip, axisX, out _, out _);
                Vector2 outward = local.SafeNormalize(axisX);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + local
                    , outward * Main.rand.NextFloat(0.6f, 2.2f) + (Projectile.velocity * 0.25f)
                    , Color.Lerp(WaveViolet, WaveBlue, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.24f, 0.42f)).Configure(false, 16);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NeutronSwingBeat wave = BuildWave(out float bulge, out Vector2 axisX, out float bandRef);
            for (int i = 0; i <= 12; i++) {
                float u = i / 12f;
                Vector2 point = Projectile.Center
                    + NeutronSwingArc.TipOffset(in wave, bulge, u, Flip, axisX, out _, out _);
                //擦边算中，波面看着盖住了就该打到
                if (targetHitbox.Distance(point) <= BandHalfWidth(in wave, u, bandRef) + 14f) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (payloadSpawned || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //沿用原剑气的载荷：命中后落一次中子星爆炸，对应物品说明
            payloadSpawned = true;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage / 2, 0, Projectile.owner);
        }

        /// <summary>余痕：波散之后仍在原地慢慢消的引力残留</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            NeutronSwingBeat wave = BuildWave(out float bulge, out Vector2 axisX, out _);
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f, Pitch = -0.6f }, Projectile.Center);
            for (int i = 0; i <= 14; i++) {
                float u = i / 14f;
                Vector2 local = NeutronSwingArc.TipOffset(in wave, bulge, u, Flip, axisX, out _, out _);
                Vector2 drift = local.SafeNormalize(axisX) * Main.rand.NextFloat(0.15f, 0.7f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + local, drift
                    , Color.Lerp(WaveViolet, WaveBlue, u), Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(false, 34);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        bool IWarpDrawable.CanDrawCustom() => false;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>波面沿途把背景掰弯，强度随振幅一起衰减</summary>
        void IWarpDrawable.Warp() {
            NeutronSwingBeat wave = BuildWave(out float bulge, out Vector2 axisX, out float bandRef);
            float amp = (1f - NeutronSwingArc.SmoothStep01(LifeT)) * (IsHeavy ? 0.7f : 0.46f);
            if (amp <= 0.02f) {
                return;
            }
            int samples = IsHeavy ? 3 : 2;
            for (int i = 0; i < samples; i++) {
                float u = (i + 0.5f) / samples;
                Vector2 local = NeutronSwingArc.TipOffset(in wave, bulge, u, Flip
                    , axisX, out float projRadius, out float depth);
                float span = MathF.Max(projRadius * 1.1f, bandRef * 5f);
                NeutronWarpHelper.DrawWarp(Projectile.Center + local
                    , screenWidth: span, screenHeight: span
                    , intensity: amp * NeutronSwingArc.DepthDim(in wave, bulge, depth)
                    , progress: LifeT
                    , rotation: Projectile.rotation
                    , technique: "GravitationalLens"
                    , radius: 0.4f);
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.NeutronSlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }
            NeutronSwingBeat wave = BuildWave(out float bulge, out Vector2 axisX, out float bandRef);

            const int Segments = 40;
            var bars = new VertexPositionColorTexture[(Segments + 1) * 2];
            for (int i = 0; i <= Segments; i++) {
                float u = i / (float)Segments;
                Vector2 local = NeutronSwingArc.TipOffset(in wave, bulge, u, Flip
                    , axisX, out _, out float depth);
                Vector2 center = Projectile.Center + local;
                Vector2 outward = local.SafeNormalize(axisX) * BandHalfWidth(in wave, u, bandRef);
                //uv.x 映成 sin(pi*u)：波峰给 1、双翼给 0，
                //于是着色器里那条按"新旧"淡出的曲线正好把两个翼尖一起收掉
                float rail = MathF.Sin(MathHelper.Pi * u);
                Color tint = Color.White * NeutronSwingArc.DepthDim(in wave, bulge, depth);
                bars[i * 2] = new VertexPositionColorTexture((center + outward).ToVector3()
                    , tint, new Vector2(rail, 0f));
                bars[(i * 2) + 1] = new VertexPositionColorTexture((center - outward).ToVector3()
                    , tint, new Vector2(rail, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(1f - NeutronSwingArc.SmoothStep01((LifeT - 0.55f) / 0.45f));
            effect.Parameters["uHeat"]?.SetValue(IsHeavy ? 1f : 0.45f);
            effect.Parameters["uForcePoint"]?.SetValue(0.5f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    internal class NeutronGlaiveHeldAlt : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        [VaultLoaden(CWRConstant.UI + "NeutronsBar")]
        private static Asset<Texture2D> bar1 = null;
        [VaultLoaden(CWRConstant.UI + "NeutronsBar2")]
        private static Asset<Texture2D> bar2 = null;
        [VaultLoaden(CWRConstant.UI + "NeutronsBarTop")]
        private static Asset<Texture2D> bar3 = null;
        [VaultLoaden(CWRConstant.UI + "NeutronsBarTop2")]
        private static Asset<Texture2D> bar4 = null;
        private bool canatcck;
        private bool canatcck2 = true;
        private bool canatcck3 = true;
        private int uiframe;
        private const int maxatcck = 80;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 112;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.hide = true;
        }

        public override void AI() {
            if (Owner.dead || !Owner.active || canatcck || !DownRight) {
                canatcck = true;
                if (Projectile.ai[0] >= maxatcck) {
                    Projectile.Kill();
                }
                else {
                    canatcck2 = false;
                    Projectile.scale = 1.25f;

                    if (++Projectile.ai[1] > 5) {
                        SoundEngine.PlaySound(SoundID.Item4, Projectile.Center);
                        Vector2 pos = Projectile.Center + Projectile.velocity.UnitVector() * Main.rand.Next(-52, 112);
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos
                        , Projectile.velocity.RotatedByRandom(0.2f), ModContent.ProjectileType<NeutronsOrb>(), Projectile.damage, 0);
                        Main.projectile[proj].SetAllProjectilesHome(true);
                        for (int i = 0; i < 4; i++) {
                            float rot1 = MathHelper.PiOver2 * i;
                            Vector2 vr = rot1.ToRotationVector2();
                            for (int j = 0; j < 13; j++) {
                                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, vr * (0.1f + j * 0.14f), Color.BlueViolet, Main.rand.NextFloat(0.5f, 0.7f)).Configure(false, 17);
                            }
                        }
                        Projectile.ai[1] = 0;
                    }

                    Projectile.ai[0]--;
                    if (Projectile.ai[0] <= 0) {
                        Projectile.Kill();
                    }
                }
            }
            if (canatcck2) {
                Projectile.velocity = ToMouse.UnitVector() * 18;
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 40 * Projectile.scale;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!canatcck && Projectile.ai[0] <= maxatcck) {
                Projectile.ai[0]++;
            }
            if (Projectile.ai[0] >= maxatcck) {
                if (canatcck3) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f }, Projectile.Center);
                    canatcck3 = false;
                }
                Projectile.scale = 1.5f;
            }
            SetHeld();
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 15);
            if (canatcck2) {
                VaultUtils.ClockFrame(ref uiframe, 5, 6);
            }
            float rot = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.direction = Math.Sign(Projectile.velocity.X);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && canatcck2) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity.UnitVector() * 255
                    , Vector2.Zero, ModContent.ProjectileType<EXNeutronExplode>(), Projectile.damage * 10, 0);
            }
        }

        public static void DrawBar(Player Owner, float sengs, int uiframe) {
            sengs = MathHelper.Clamp(sengs, 0, maxatcck);
            if (!(sengs <= 0f)) {
                Texture2D barBG = bar3.Value;
                Texture2D barFG = bar1.Value;
                if (sengs >= maxatcck) {
                    barBG = bar4.Value;
                    barFG = bar2.Value;
                }
                float barScale = 1.2f;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + new Vector2(0, 90) - Main.screenPosition;
                Rectangle frameCrop = new Rectangle(0, 0, (int)(sengs / maxatcck * barFG.Width), barFG.Height);
                Color color = Color.White;
                Main.spriteBatch.Draw(barBG, drawPos, barBG.GetRectangle(uiframe, 7), color, 0f, VaultUtils.GetOrig(barBG, 7), barScale, 0, 0f);
                Main.spriteBatch.Draw(barFG, drawPos + new Vector2(2, 4), frameCrop, color, 0f, VaultUtils.GetOrig(barFG, 1), barScale, 0, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawBar(Owner, Projectile.ai[0], uiframe);
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, value.GetRectangle(Projectile.frame, 16)
                , Color.White, Projectile.rotation + MathHelper.PiOver4 * Owner.direction, VaultUtils.GetOrig(value, 16) + new Vector2(0, 5 * Owner.direction)
                , Projectile.scale, Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
            return false;
        }
    }

    internal class NeutronsOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.timeLeft = 120;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, Projectile.velocity, Color.BlueViolet, Main.rand.NextFloat(0.2f, 0.3f)).Configure(false, 17);
        }
    }

    internal class NeutronExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + i * 0.24f), Color.BlueViolet, Main.rand.NextFloat(1.2f, 2.3f)).Configure(false, 7);
                    }
                }
                Projectile.ai[2]++;
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);

            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 400f * scale,
                screenHeight: 400f * scale,
                intensity: Projectile.ai[1] * 0.85f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalVortex"
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }

    internal class EXNeutronExplode : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2000;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = EndlessDamageClass.Instance;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.1f + j * 0.34f), Color.BlueViolet, Main.rand.NextFloat(2.2f, 2.3f)).Configure(false, 7);
                    }
                }
            }
            if (Projectile.ai[2] % 6 == 0) {
                float randvalue = Main.rand.NextFloat(MathHelper.TwoPi);
                float randvalue2 = Main.rand.NextFloat(0.3f, 1.6f);
                for (int z = 0; z < 4; z++) {
                    Vector2 rand = (MathHelper.PiOver2 * z + randvalue).ToRotationVector2() * 130 * randvalue2;
                    for (int i = 0; i < 4; i++) {
                        float rot1 = MathHelper.PiOver2 * i;
                        Vector2 vr = rot1.ToRotationVector2();
                        for (int j = 0; j < 33; j++) {
                            PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + rand, vr * 0.24f, Color.CadetBlue, Main.rand.NextFloat(0.9f, 1.3f)).Configure(false, 13);
                        }
                    }
                }
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.ai[2]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 1200f * scale,
                screenHeight: 1200f * scale,
                intensity: Projectile.ai[1] * 1.0f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "GravitationalVortex",
                radius: 0.48f
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
