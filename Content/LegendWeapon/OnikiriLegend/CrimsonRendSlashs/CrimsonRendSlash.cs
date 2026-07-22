using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using CSR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer;
using SlashDef = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer.SlashDef;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>
    /// 绯红裂空斩,按住左键滚动五段连段控制器<br/>
    /// 按住循环出刀,松手停排,收势再按从第一拍重启,实体刀独立姿态时间轴(纯视觉)<br/>
    /// 拍间隔/命中冷却随近战攻速缩放,伤害类无速真近战<br/>
    /// ai[0]=初始瞄准角(弧度) ai[2]=尺寸倍率
    /// </summary>
    internal class CrimsonRendSlash : BaseHeldProj, IPrimitiveDrawable, ICrimsonFarDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==== 节拍常量 ====
        private const int BeatCount = 5;
        private const int BurstFadeFrames = 16;
        private const int AfterglowEnd = 46;   //命中余韵最晚结束帧(相对 lastImpactFrame)
        private const int BaseHitCooldown = 10;
        private const int BladeReleaseRecoveryFrames = 12;
        /// <summary>首次纯起手帧数,反向蓄势后无条件出首拍(仅首拍延后)</summary>
        private const int FirstWindupFrames = 2;
        private const float BladePathStart = 0.06f;
        private const float BladePathEnd = 0.94f;
        private const float BladeDrawScale = 0.90f;
        //==== 姿态残影常量 ====
        private const int SmearCapacity = 20;
        private const int SmearLifeFrames = 6;
        private const int SmearMaxDrawn = 8;
        /// <summary>贴图护手/刀尖 UV,护手作手心支点</summary>
        private static Vector2 BladeHiltUV => new(0.1f, 1f);
        private static Vector2 BladeTipUV => new(0.73f, 0.01f);
        /// <summary>各拍到下一拍基准间隔(攻速 1),末位终结后循环呼吸</summary>
        private static readonly int[] BeatGap = [10, 10, 13, 15, 24];
        /// <summary>快斩收势轻确认(白闪/火花/音高/是否命中型白闪)</summary>
        private static readonly (float Flash, int Sparks, float Pitch, bool HitFlash)[] PingTable = [
            (0.02f, 6, 0.50f, false),
            (0.01f, 8, 0.65f, false),
            (0.05f, 10, 0.75f, true),
            (0.06f, 12, 0.85f, true),
        ];

        /// <summary>已挥出子刀光,出生帧与瞄准角开火瞬间冻结</summary>
        private sealed class ActiveSlash
        {
            public SlashDef Def;
            public int Birth;        //绝对 timer 帧
            public int Beat;         //0..4 拍位
            public float Aim;        //该拍开火瞄准角
            public int Facing;       //该拍冻结朝向
            public bool ImpactDone;  //本拍首次命中爆点已触发
            public bool SnapPlayed;  //重击爆发脆响已播(快斩恒 true)
            public Vector2? FrozenCenter;   //硬让位瞬间冻结世界锚点
        }

        /// <summary>停手超过该帧后再按从第一拍重启,短停续接拍序</summary>
        private const int ComboResetFrames = 30;
        /// <summary>模组交接重启微前摇(帧),仅 <see cref="OniBladeHandoff"/> 新鲜时;普通点按当帧出刀</summary>
        private const int RestartWindupFrames = 3;
        /// <summary>轻点缓冲(帧),让位/签名拍保留中的点击不丢,窗口关后补发</summary>
        private const int PressBufferFrames = 24;

        /// <summary>实体刀姿态残影(挂当前手心,只留角度/深度)</summary>
        private struct BladeSmear
        {
            public float Rotation;
            public float Depth;
            public int Facing;
            public float Scale;
            public int Life;        //剩余帧
            public float Strength;  //出生角速度权重
        }

        private readonly List<ActiveSlash> actives = new(8);
        private int timer;
        private int comboIndex;
        private int nextBeatTime;
        private int lastBeatFire;
        private bool scheduling;
        private bool firstBeatFired;
        /// <summary>首拍前摇已走帧数(软保留/让位不计)</summary>
        private int firstWindupTicks;
        /// <summary>上帧 DownLeft,检测真按下沿</summary>
        private bool prevDownLeft;
        /// <summary>轻点缓冲余量,按下填满,开火即清</summary>
        private int pressBuffer;
        /// <summary>本次继承的交接刀角(<see cref="OniBladeHandoff"/>),开火即清</summary>
        private float handoffRot;
        private bool hasHandoff;
        private float sizeMul = 1f;
        private float curAim;
        private int lastImpactFrame = -999;
        private Vector2 lastImpactPos;
        private float lastImpactAim;
        private float lastImpactFlip = 1f;
        /// <summary>最近爆点是否金属(驱动粒子/爆点材质)</summary>
        private bool lastImpactSteel;
        private Rectangle[] speedLineRects;
        private float[] speedLineOffsets;
        //==== 实体刀姿态时间轴(纯视觉,确定性,不上网络) ====
        private float bladeRotation;
        private float bladePrevRotation;
        private float bladeDepth;          //-1=身后 .. +1=身前
        private float bladeOpacity;
        private int bladeFacing = 1;
        private bool bladePoseInitialized;
        private Player.CompositeArmStretchAmount bladeArmStretch = Player.CompositeArmStretchAmount.Full;
        private Vector2 bladeHandWorld;
        //==== 命中反馈(视觉停驻/回坐/尺寸脉冲,不碰节拍与判定) ====
        private int impactHoldFrames;
        private float impactRecoil;
        private float impactRecoilSign = 1f;
        private float bladeScalePulse;
        //==== 姿态残影环形缓冲 ====
        private readonly BladeSmear[] smears = new BladeSmear[SmearCapacity];
        private int smearHead;

        /// <summary>刃身环境光锚点(非命中点),最新子刀光鼓腹位;真实命中用 <see cref="lastImpactPos"/></summary>
        private Vector2 AmbientAnchor {
            get {
                if (actives.Count > 0) {
                    ActiveSlash a = actives[^1];
                    return Projectile.Center + a.Aim.ToRotationVector2() * (a.Def.OffsetAlongAim + a.Def.HalfX * 0.55f);
                }
                return Projectile.Center + curAim.ToRotationVector2() * 180f * sizeMul;
            }
        }

        /// <summary>持有者客户端调用(<c>myPlayer</c>),tML 自动同步</summary>
        /// <param name="aim">无需归一化,此后每拍重捕鼠标</param>
        /// <param name="source">null 则 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 origin, Vector2 aim, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_CrimsonRendSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX).ToRotation();
            return Projectile.NewProjectileDirect(source, origin, Vector2.Zero
                , ModContent.ProjectileType<CrimsonRendSlash>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai2: scale);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;   //常态 UpdateLifetime 刷新,收势完主动 Kill
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = BaseHitCooldown;   //随攻速在 FireBeat 重设
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            float itemScale = Item.type == ModContent.ItemType<OnikiriItem>()
                ? MathHelper.Clamp(Owner.GetAdjustedItemScale(Item), 0.5f, 1.5f)
                : 1f;
            sizeMul = (Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f) * itemScale;
            sizeMul = Math.Min(sizeMul, OnikiriOverride.MaxCompositeBladeScale);
            curAim = Projectile.ai[0];
            scheduling = true;
            nextBeatTime = FirstWindupFrames + 1;
        }

        /// <summary>五段弧形变奏美术参数,Seed 掺入出生帧防循环同噪声</summary>
        private SlashDef BuildBeatDef(int beat, float a, float f, float s) {
            SlashDef d = beat switch {
                //0 纵斩下劈,干笔飞白重、墨轻、几乎不洇
                0 => new SlashDef {
                    SweepFrames = 4, Life = 26, ErodeStart = 8, ErodeFrames = 14,
                    ColorShiftDelay = 7, ColorShiftFrames = 12, DamageStart = 1, DamageEnd = 9,
                    Mode = 0f, Rot = a + f * 0.15f, Span = 3.60f, Thick = 0.30f,
                    HalfX = 150f * s, HalfY = 208f * s, Flip = f,
                    Opacity = 0.92f, FrontGlow = 2.2f, OffsetAlongAim = 30f * s,
                    TailErode = 0.50f, FlashPower = 0.62f, RazorTailWiden = 0.40f, FarDim = 0.78f,
                    Ink = 0.42f, FeiBai = 0.58f, Bleed = 0.06f, SplitTail = 0.50f,
                },
                //1 反手上撩,同平面反向更大更立
                1 => new SlashDef {
                    SweepFrames = 3, Life = 26, ErodeStart = 8, ErodeFrames = 14,
                    ColorShiftDelay = 7, ColorShiftFrames = 12, DamageStart = 1, DamageEnd = 8,
                    Mode = 0f, Rot = a - f * 0.10f, Span = 3.55f, Thick = 0.33f,
                    HalfX = 172f * s, HalfY = 238f * s, Flip = -f,
                    Opacity = 0.96f, FrontGlow = 2.4f, OffsetAlongAim = 44f * s,
                    TailErode = 0.45f, FlashPower = 0.68f, RazorTailWiden = 0.40f, FarDim = 0.78f,
                    Ink = 0.42f, FeiBai = 0.62f, Bleed = 0.06f, SplitTail = 0.50f,
                },
                //2 月牙重斩,满弧中墨过渡
                2 => new SlashDef {
                    SweepFrames = 3, Life = 34, ErodeStart = 8, ErodeFrames = 18,
                    ColorShiftDelay = 6, ColorShiftFrames = 14, DamageStart = 1, DamageEnd = 10,
                    Mode = 0f, Rot = a, Span = 3.55f, Thick = 0.36f,
                    HalfX = 245f * s, HalfY = 245f * s, Flip = f,
                    Opacity = 1f, FrontGlow = 2.6f, OffsetAlongAim = 0f,
                    TailErode = 0.42f, FlashPower = 0.60f, RazorTailWiden = 0.55f, FarDim = 0.74f,
                    Ink = 0.52f, FeiBai = 0.42f, Bleed = 0.15f, SplitTail = 0.58f,
                },
                //3 蓄势重斩,缓推滞帧后爆发,伤害窗对齐爆发,湿笔洇边
                3 => new SlashDef {
                    SweepFrames = 8, Life = 30, ErodeStart = 9, ErodeFrames = 16,
                    ColorShiftDelay = 7, ColorShiftFrames = 12, DamageStart = 7, DamageEnd = 12,
                    Mode = 0f, Rot = a - f * 0.35f, Span = 3.45f, Thick = 0.42f,
                    HalfX = 330f * s, HalfY = 195f * s, Flip = f,
                    Opacity = 0.97f, FrontGlow = 2.6f, OffsetAlongAim = -35f * s,
                    TailErode = 0.32f, FlashPower = 0.75f, SweepSnap = 1f, RazorTailWiden = 0.75f,
                    FarDim = 0.70f,
                    Ink = 0.62f, FeiBai = 0.24f, Bleed = 0.30f, SplitTail = 0.75f,
                },
                //4 蓄势终结,巨弧罩身,压在歼灭斩之下
                _ => new SlashDef {
                    SweepFrames = 9, Life = 56, ErodeStart = 12, ErodeFrames = 30,
                    ColorShiftDelay = 7, ColorShiftFrames = 18, DamageStart = 8, DamageEnd = 14,
                    Mode = 0f, Rot = a + f * 0.20f, Span = 3.35f, Thick = 0.44f,
                    HalfX = 400f * s, HalfY = 230f * s, Flip = -f,
                    Opacity = 1f, FrontGlow = 2.9f, OffsetAlongAim = -60f * s,
                    TailErode = 0.30f, FlashPower = 0.95f, SweepSnap = 1f, RazorTailWiden = 0.85f,
                    FarDim = 0.66f,
                    Ink = 0.68f, FeiBai = 0.26f, Bleed = 0.45f, SplitTail = 0.85f,
                },
            };
            d.Seed = (beat * 0.191f + timer * 0.037f) % 1f;
            return d;
        }

        /// <summary>子刀光锚点,平时随玩家,硬让位后取冻结点</summary>
        private Vector2 CenterOf(ActiveSlash a)
            => a.FrozenCenter ?? (Projectile.Center + a.Aim.ToRotationVector2() * a.Def.OffsetAlongAim);

        /// <summary>本帧是否持刀权(排拍/活刀光/实体刀未收完);硬让位冻结的尸体刀光不算</summary>
        internal bool ClaimsBlade => scheduling || bladeOpacity > 0.03f || AnyLiveSlash();

        /// <summary>是否存在未被硬让位冻结的活刀光</summary>
        private bool AnyLiveSlash() {
            for (int i = 0; i < actives.Count; i++) {
                if (actives[i].FrozenCenter == null) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>实体刀当前姿态(肢解居合继承用);不可见返回 false</summary>
        internal bool TryGetBladePose(out float rotation, out int facing) {
            rotation = bladeRotation;
            facing = bladeFacing;
            return bladePoseInitialized && bladeOpacity > 0.05f;
        }

        /// <summary>查找该玩家连段控制器,无则 null</summary>
        internal static CrimsonRendSlash FindController(Player player) {
            int type = ModContent.ProjectileType<CrimsonRendSlash>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type
                    && proj.ModProjectile is CrimsonRendSlash controller) {
                    return controller;
                }
            }
            return null;
        }

        /// <summary>残心接管本次左键,清掉普攻补发</summary>
        internal void ConsumeZanshinInput() {
            pressBuffer = 0;
            scheduling = false;
            prevDownLeft = DownLeft;
        }

        /// <summary>刀柄方向支点(解算朝向);绘制锚用 <see cref="bladeHandWorld"/>,拆开斩断手↔臂依赖环</summary>
        private Vector2 BladeHandPosition(int facing) => Owner.GetPlayerStabilityCenter()
            + new Vector2(facing * 3f, -5f * Owner.gravDir);

        /// <summary>刀光中线点→实体刀朝向,采静态椭圆只取方向</summary>
        private float BladePathRotation(in SlashDef d, float aim, int facing, float u) {
            Vector2 center = Projectile.Center + aim.ToRotationVector2() * d.OffsetAlongAim;
            Vector2 edge = CSR.StaticPointAt(in d, center, MathHelper.Clamp(u, BladePathStart, BladePathEnd));
            Vector2 fromHand = edge - BladeHandPosition(facing);
            return fromHand.LengthSquared() > 1f ? fromHand.ToRotation() : aim;
        }

        /// <summary>起笔端角速度符号,+1 顺时针 −1 逆时针</summary>
        private float PathSweepSign(in SlashDef d, float aim, int facing) {
            float rotA = BladePathRotation(in d, aim, facing, BladePathStart);
            float rotB = BladePathRotation(in d, aim, facing, BladePathStart + 0.12f);
            return MathHelper.WrapAngle(rotB - rotA) >= 0f ? 1f : -1f;
        }

        private static float LerpAngle(float from, float to, float amount)
            => from + MathHelper.WrapAngle(to - from) * MathHelper.Clamp(amount, 0f, 1f);

        /// <summary>深度→近景权重,±0.22 交叉淡化</summary>
        private static float NearWeight(float depth) => CSR.SmoothStep01((depth + 0.22f) / 0.44f);

        /// <summary>深度→透视缩放,身后~0.90 身前~1.045</summary>
        private static float DepthScale(float depth) => depth < 0f
            ? MathHelper.Lerp(1f, 0.90f, MathHelper.Clamp(-depth, 0f, 1f))
            : MathHelper.Lerp(1f, 1.045f, MathHelper.Clamp(depth, 0f, 1f));

        /// <summary>深度→亮度,身后压暗至~0.72</summary>
        private static float DepthDim(float depth) => MathHelper.Lerp(1f, 0.72f, MathHelper.Clamp(-depth, 0f, 1f));

        //==================== 连段推进 ====================

        public override void AI() {
            Projectile.Center = Owner.Center;
            timer++;

            UpdateYield();
            UpdateCombo();
            UpdateBeatEvents();
            UpdateBladePose();

            if (!Main.dedServ) {
                SpawnSweepSparks();
                SpawnEdgeSmoke();
            }

            UpdatePose();

            Lighting.AddLight(AmbientAnchor, new Vector3(1.0f, 0.25f, 0.18f));
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.12f, 0.10f));
            if (bladeOpacity > 0.01f) {
                Vector2 bladeLight = BladeHandPosition(bladeFacing)
                    + bladeRotation.ToRotationVector2() * 92f * sizeMul;
                Lighting.AddLight(bladeLight, new Vector3(0.72f, 0.10f, 0.06f) * bladeOpacity);
            }

            PushScreenState();
            UpdateLifetime();
        }

        //====刀权让位====
        /// <summary>硬让位后子刀光最大余寿(帧)</summary>
        private const int YieldFadeFrames = 8;
        //上帧硬让位,检测让位起始沿
        private bool yielding;

        /// <summary>刀权仲裁,主人有硬占刀权技能时停排/冻结刀光速褪/实体刀交权</summary>
        private void UpdateYield() {
            bool hard = OniBladeOccupancy.AnyHardOccupant(Owner);
            if (hard && !yielding) {
                scheduling = false;
                bladeOpacity = 0f;
                foreach (ActiveSlash a in actives) {
                    a.FrozenCenter ??= Projectile.Center + a.Aim.ToRotationVector2() * a.Def.OffsetAlongAim;
                    int remain = a.Def.Life - (timer - a.Birth);
                    if (remain > YieldFadeFrames) {
                        a.Birth = timer - (a.Def.Life - YieldFadeFrames);
                    }
                }
            }
            yielding = hard;
        }

        /// <summary>连段排拍,按住推进松手停排,收势再按从第一拍重启;居合/签名拍软保留期间不夺刀</summary>
        private void UpdateCombo() {
            bool canContinue = !Owner.noItems && !Owner.CCed
                && Item.type == ModContent.ItemType<OnikiriItem>()
                && Owner.ownedProjectileCounts[ModContent.ProjectileType<OniDismembers.OniSeverStrike>()] == 0;
            //真按下沿(区别于让位/保留后的按住续接)
            bool justPressed = DownLeft && !prevDownLeft;
            prevDownLeft = DownLeft;
            //轻点缓冲
            if (pressBuffer > 0) {
                pressBuffer--;
            }
            if (justPressed) {
                pressBuffer = PressBufferFrames;
            }
            bool holding = (DownLeft || pressBuffer > 0) && canContinue && !yielding;

            //首拍纯起手窗,走完无条件出刀;仅首次有此前摇
            if (!firstBeatFired) {
                if (yielding || OniBladeOccupancy.BladeReserved(Owner)) {
                    //技能/签名拍保留优先,前摇计数不走
                    return;
                }
                if (++firstWindupTicks == 1) {
                    //起手第一帧继承交接刀角
                    hasHandoff = OniBladeHandoff.TryPeek(Owner, out handoffRot, out _);
                }
                if (firstWindupTicks > FirstWindupFrames) {
                    FireBeat();
                    firstBeatFired = true;
                    scheduling = holding;
                }
                return;
            }

            if (!holding) {
                scheduling = false;
                return;
            }
            if (!scheduling) {
                //按下沿,里世界点中媒介/真身→肢解居合(不重启排拍)
                if (justPressed && Projectile.IsOwnedByLocalPlayer()
                    && Owner.GetModPlayer<OnikiriPlayer>().TryClickDismember(Item)) {
                    return;
                }
                //追斩窗按下沿→残心斩,清缓冲防同点击再兑现
                if (justPressed && Projectile.IsOwnedByLocalPlayer()
                    && Owner.GetModPlayer<OnikiriPlayer>().TryZanshinStrike(Item, edgeVerified: true)) {
                    pressBuffer = 0;
                    return;
                }
                //签名拍软保留,不重启夺刀
                if (OniBladeOccupancy.BladeReserved(Owner)) {
                    return;
                }
                scheduling = true;
                if (timer - lastBeatFire > ComboResetFrames) {
                    comboIndex = 0;
                }
                //模组交接吃 RestartWindupFrames 微前摇;普通点按当帧出刀
                hasHandoff = OniBladeHandoff.TryPeek(Owner, out handoffRot, out _);
                nextBeatTime = timer + (hasHandoff ? RestartWindupFrames : 0);
            }
            if (timer >= nextBeatTime) {
                FireBeat();
            }
        }

        /// <summary>开火一拍,冻结鼠标方向,按攻速排下一拍并缩放命中冷却</summary>
        private void FireBeat() {
            hasHandoff = false;   //交接前摇已兑现
            pressBuffer = 0;      //缓冲点击已兑现
            float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : Projectile.ai[0];
            curAim = aim;
            float cos = MathF.Cos(aim);
            float flip = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1f : -1f);
            int beat = comboIndex;

            actives.Add(new ActiveSlash {
                Def = BuildBeatDef(beat, aim, flip, sizeMul),
                Birth = timer,
                Beat = beat,
                Aim = aim,
                Facing = (int)flip,
                SnapPlayed = beat < 3,
            });
            PlayBeatFireSound(beat);

            float speedFactor = MathHelper.Clamp(1f / Owner.GetWeaponAttackSpeed(Item), 0.5f, 1.6f);
            Projectile.localNPCHitCooldown = Math.Max(5, (int)(BaseHitCooldown * speedFactor));
            comboIndex = (comboIndex + 1) % BeatCount;
            lastBeatFire = timer;
            nextBeatTime = timer + Math.Max(4, (int)MathF.Round(BeatGap[beat] * speedFactor));
        }

        /// <summary>实体刀姿态时间轴(纯视觉),起手→扫掠→拍间换向→松手收刀;深度驱动远近景</summary>
        private void UpdateBladePose() {
            DecaySmears();

            //硬让位期间藏刀
            if (yielding) {
                bladeOpacity = 0f;
                bladePoseInitialized = false;
                return;
            }

            //签名拍软保留藏刀(有活刀光则不受影响);仅剩尸体刀光时同样藏
            if (!AnyLiveSlash() && OniBladeOccupancy.BladeReserved(Owner)) {
                bladeOpacity = 0f;
                bladePoseInitialized = false;
                return;
            }

            float targetRotation;
            float targetDepth;
            var stretch = Player.CompositeArmStretchAmount.Full;
            bladeOpacity = 1f;

            if (!firstBeatFired) {
                //A 首次起手,反拉蓄势沉入身后;有交接角则顺势拉入
                float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : curAim;
                float cos = MathF.Cos(aim);
                int facing = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1 : -1);
                bladeFacing = facing;
                SlashDef first = BuildBeatDef(0, aim, facing, sizeMul);
                float startRot = BladePathRotation(in first, aim, facing, BladePathStart);
                float sweepSign = PathSweepSign(in first, aim, facing);
                float windT = CSR.EaseOutCubic(MathHelper.Clamp(firstWindupTicks / (float)FirstWindupFrames, 0f, 1f));
                targetRotation = hasHandoff
                    ? OniBladePose.LerpAngle(handoffRot, startRot - sweepSign * 0.55f, windT)
                    : startRot - sweepSign * 0.55f * windT;
                targetDepth = MathHelper.Lerp(0.15f, -0.85f, windT);
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }
            else if (actives.Count == 0) {
                if (scheduling && hasHandoff && timer < nextBeatTime) {
                    //B0 交接重启微前摇
                    float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : curAim;
                    float cos = MathF.Cos(aim);
                    int facing = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1 : -1);
                    bladeFacing = facing;
                    SlashDef next = BuildBeatDef(comboIndex, aim, facing, sizeMul);
                    float startRot = BladePathRotation(in next, aim, facing, BladePathStart);
                    float windT = CSR.EaseOutCubic(
                        1f - (nextBeatTime - timer) / (float)RestartWindupFrames);
                    targetRotation = OniBladePose.LerpAngle(handoffRot, startRot, windT);
                    targetDepth = MathHelper.Lerp(-0.20f, -0.45f, windT);
                    stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
                }
                else {
                    //收势完藏刀等待重启
                    bladeOpacity = 0f;
                    bladePoseInitialized = false;
                    impactHoldFrames = 0;
                    impactRecoil = 0f;
                    bladeScalePulse = 0f;
                    return;
                }
            }
            else {
                ActiveSlash a = actives[^1];
                int lt = Math.Max(0, timer - a.Birth);
                bladeFacing = a.Facing;

                if (lt <= a.Def.SweepFrames) {
                    //B 扫掠,深度随扫掠进度自身后甩到身前
                    float sweepProgress = MathHelper.Clamp(CSR.Sweep(in a.Def, lt), 0f, 1f);
                    float edgeU = MathHelper.Clamp(sweepProgress * 1.05f, BladePathStart, BladePathEnd);
                    targetRotation = BladePathRotation(in a.Def, a.Aim, a.Facing, edgeU);
                    targetDepth = MathHelper.Lerp(-0.45f, 0.95f, sweepProgress);
                }
                else {
                    float endRot = BladePathRotation(in a.Def, a.Aim, a.Facing, BladePathEnd);
                    float sweepSign = PathSweepSign(in a.Def, a.Aim, a.Facing);

                    if (scheduling && timer < nextBeatTime) {
                        //C 拍间衔接,过冲→沉入身后谷底换向→下一拍起点
                        int prepStart = a.Birth + a.Def.SweepFrames;
                        float prepT = MathHelper.Clamp((timer - prepStart)
                            / (float)Math.Max(1, nextBeatTime - prepStart), 0f, 1f);

                        float overshoot = MathF.Sin(MathHelper.Clamp(prepT / 0.55f, 0f, 1f) * MathF.PI) * 0.22f;
                        float fromRot = endRot + sweepSign * overshoot;

                        float previewAim = ToMouse.LengthSquared() > 1f ? ToMouseA : curAim;
                        float previewCos = MathF.Cos(previewAim);
                        int previewFacing = MathF.Abs(previewCos) < 0.05f
                            ? a.Facing
                            : (previewCos > 0f ? 1 : -1);
                        SlashDef nextDef = BuildBeatDef(comboIndex, previewAim, previewFacing, sizeMul);
                        float nextStart = BladePathRotation(in nextDef, previewAim, previewFacing
                            , BladePathStart);

                        float settle = CSR.SmoothStep01((prepT - 0.30f) / 0.70f);
                        targetRotation = LerpAngle(fromRot, nextStart, settle);
                        targetDepth = PrepDepth(prepT);
                        stretch = prepT < 0.35f
                            ? Player.CompositeArmStretchAmount.Full
                            : Player.CompositeArmStretchAmount.ThreeQuarters;
                        if (prepT > 0.62f) {
                            bladeFacing = previewFacing;
                        }
                    }
                    else if (!scheduling) {
                        //D 松手收势,短过冲→收刀回背→淡出
                        float recoverT = MathHelper.Clamp((lt - a.Def.SweepFrames)
                            / (float)BladeReleaseRecoveryFrames, 0f, 1f);
                        float overshoot = MathF.Sin(MathHelper.Clamp(recoverT / 0.40f, 0f, 1f) * MathF.PI) * 0.16f;
                        float guardRotation = a.Aim - a.Facing * 1.05f;
                        targetRotation = LerpAngle(endRot + sweepSign * overshoot, guardRotation
                            , CSR.SmoothStep01((recoverT - 0.18f) / 0.82f));
                        targetDepth = MathHelper.Lerp(0.85f, -0.90f, CSR.SmoothStep01(recoverT));
                        bladeOpacity = 1f - CSR.SmoothStep01((recoverT - 0.68f) / 0.32f);
                        stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
                    }
                    else {
                        //兜底(理论不可达),停在收笔端
                        targetRotation = endRot;
                        targetDepth = 0.3f;
                    }
                }
            }

            //命中视觉停驻一帧;回坐包络其后衰减
            if (impactHoldFrames > 0 && bladePoseInitialized) {
                impactHoldFrames--;
                targetRotation = bladeRotation;
            }
            float recoilOffset = 0f;
            if (impactRecoil > 0.01f) {
                recoilOffset = -impactRecoilSign * 0.07f * impactRecoil;
                impactRecoil *= 0.62f;
            }
            else {
                impactRecoil = 0f;
            }
            bladeScalePulse *= 0.72f;
            if (bladeScalePulse < 0.002f) {
                bladeScalePulse = 0f;
            }

            bladeArmStretch = stretch;

            if (!bladePoseInitialized) {
                bladeRotation = targetRotation + recoilOffset;
                bladePrevRotation = bladeRotation;
                bladeDepth = targetDepth;
                bladePoseInitialized = true;
                return;
            }

            bladePrevRotation = bladeRotation;
            bladeRotation = targetRotation + recoilOffset;
            float prevDepth = bladeDepth;
            bladeDepth = MathHelper.Lerp(bladeDepth, targetDepth, 0.5f);

            PushSmearSamples(prevDepth);
        }

        /// <summary>拍间深度曲线,过冲身前→谷底≈0.62 换向→下一拍起手偏后</summary>
        private static float PrepDepth(float prepT) {
            if (prepT < 0.25f) {
                return MathHelper.Lerp(0.85f, 0.15f, CSR.SmoothStep01(prepT / 0.25f));
            }
            if (prepT < 0.62f) {
                return MathHelper.Lerp(0.15f, -0.95f, CSR.SmoothStep01((prepT - 0.25f) / 0.37f));
            }
            return MathHelper.Lerp(-0.95f, -0.45f, CSR.SmoothStep01((prepT - 0.62f) / 0.38f));
        }

        /// <summary>残影寿命衰减(环形缓冲原地更新)</summary>
        private void DecaySmears() {
            for (int i = 0; i < smears.Length; i++) {
                if (smears[i].Life > 0) {
                    smears[i].Life--;
                }
            }
        }

        /// <summary>高速帧细分采样入环,代码式 smear</summary>
        private void PushSmearSamples(float prevDepth) {
            if (Main.dedServ || bladeOpacity <= 0.05f) {
                return;
            }
            float delta = MathHelper.WrapAngle(bladeRotation - bladePrevRotation);
            float absDelta = MathF.Abs(delta);
            if (absDelta < 0.09f) {
                return;
            }
            int steps = Math.Min(3, (int)(absDelta / 0.14f) + 1);
            float strength = MathHelper.Clamp((absDelta - 0.06f) / 0.50f, 0f, 1f) * bladeOpacity;
            for (int i = 0; i < steps; i++) {
                float t = (i + 1) / (float)(steps + 1);   //不含当前帧本体
                float depth = MathHelper.Lerp(prevDepth, bladeDepth, t);
                smears[smearHead] = new BladeSmear {
                    Rotation = bladePrevRotation + delta * t,
                    Depth = depth,
                    Facing = bladeFacing,
                    Scale = DepthScale(depth),
                    Life = SmearLifeFrames,
                    Strength = strength,
                };
                smearHead = (smearHead + 1) % SmearCapacity;
            }
        }

        /// <summary>起挥音效,快斩升调,重击低音</summary>
        private void PlayBeatFireSound(int beat) {
            (float pitch, float volume) = beat switch {
                0 => (0.20f, 0.60f),
                1 => (0.38f, 0.50f),
                2 => (0.55f, 0.60f),
                3 => (-0.45f, 0.42f),
                _ => (-0.60f, 0.50f),
            };
            SoundEngine.PlaySound(CWRSound.KatanaSwing with { Pitch = pitch, Volume = volume }, Projectile.Center);
        }

        /// <summary>子刀光时间轴事件,重击爆发脆响/快斩轻确认/过期剔除</summary>
        private void UpdateBeatEvents() {
            for (int i = actives.Count - 1; i >= 0; i--) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt >= a.Def.Life) {
                    actives.RemoveAt(i);
                    continue;
                }

                if (!a.SnapPlayed && lt == (int)(a.Def.SweepFrames * 0.75f)) {
                    a.SnapPlayed = true;
                    SoundEngine.PlaySound(CWRSound.KatanaSwing with {
                        Pitch = a.Beat == 3 ? 0.78f : 0.60f,
                        Volume = a.Beat == 3 ? 0.70f : 0.90f,
                        MaxInstances = 2
                    }, Projectile.Center);
                }

                if (a.Beat < PingTable.Length && lt == a.Def.SweepFrames) {
                    PingBeat(a);
                }
            }
        }

        /// <summary>扫掠完成轻确认(白闪/火花/音效),挥空也有</summary>
        private void PingBeat(ActiveSlash a) {
            (float flash, int sparks, float pitch, bool hitFlash) = PingTable[a.Beat];
            int lt = timer - a.Birth;
            Vector2 pos = CSR.PointAt(in a.Def, CenterOf(a), 0.94f, lt);

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = pitch, Volume = 0.38f }, pos);
            CrimsonImpactFX.PushImpact(pos, flash);

            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 130, 90)
                    , Main.rand.NextFloat(0.35f, 0.65f) * sizeMul)
                    ?.Configure(Main.rand.Next(14, 22), affectedByGravity: false);
            }
            if (hitFlash) {
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero
                    , new Color(255, 200, 180), 1.0f * sizeMul);
            }
        }

        /// <summary>持械姿态,身体双臂服从实体刀;取真实前手手心作绘制锚</summary>
        private void UpdatePose() {
            if (bladeOpacity <= 0.01f) {
                return;
            }

            SetHeld();
            Owner.ChangeDir(bladeFacing);
            Owner.itemRotation = (bladeRotation.ToRotationVector2() * Owner.direction).ToRotation();
            Owner.itemTime = Owner.itemAnimation = 2;

            float armRotation = bladeRotation - MathHelper.PiOver2;
            var backStretch = bladeArmStretch == Player.CompositeArmStretchAmount.Full
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Quarter;
            Owner.SetCompositeArmFront(true, bladeArmStretch, armRotation);
            Owner.SetCompositeArmBack(true, backStretch, armRotation + 0.16f * bladeFacing);
            bladeHandWorld = Owner.GetFrontHandPosition(bladeArmStretch, armRotation);

            //连段刀角上黑板,技能起手可继承
            OniBladeHandoff.Publish(Owner, bladeRotation, bladeFacing);
        }

        /// <summary>存活契约,排拍/子刀光续命;收势且余韵完 Kill,再按由 UpdateCombo 重启</summary>
        private void UpdateLifetime() {
            bool visualsAlive = actives.Count > 0
                || (lastImpactFrame >= 0 && timer - lastImpactFrame < AfterglowEnd);
            if (scheduling || visualsAlive) {
                Projectile.timeLeft = 30;
                return;
            }
            Projectile.Kill();
        }

        /// <summary>每拍首次命中爆点全层,power 0..1 按拍位;材质按金属/血肉分流</summary>
        private void TriggerImpactBurst(Vector2 pos, float power, float aim, float flip, bool steel) {
            lastImpactFrame = timer;
            lastImpactPos = pos;
            lastImpactAim = aim;
            lastImpactFlip = flip;
            lastImpactSteel = steel;

            if (!steel) {
                SoundEngine.PlaySound(CWRSound.KatanaHitB, pos);
            }
            else {
                SoundEngine.PlaySound(CWRSound.KatanaHit with { Pitch = 0.5f - power * 0.2f, Volume = 0.5f + power * 0.4f }, pos);
            }

            //血肉命中压低屏幕白闪
            float flash = steel ? 0.02f + power * 0.01f : 0.008f + power * 0.004f;
            CrimsonImpactFX.PushImpact(pos, flash);

            CrimsonRendHitVFX.SpawnImpactBurst(pos, aim.ToRotationVector2(), power, sizeMul, steel);
        }

        /// <summary>屏幕包络,Bloom + 命中脉冲;排拍恒亮,收势随末刀光余寿衰减</summary>
        private void PushScreenState() {
            float envelope = scheduling ? 1f : 0f;
            if (!scheduling) {
                for (int i = 0; i < actives.Count; i++) {
                    float remain = (actives[i].Def.Life - (timer - actives[i].Birth)) / 14f;
                    envelope = MathF.Max(envelope, MathHelper.Clamp(remain, 0f, 1f));
                }
            }
            float bloom = 0.28f * envelope;
            if (lastImpactFrame >= 0) {
                float bp = MathHelper.Clamp((timer - lastImpactFrame) / (float)BurstFadeFrames, 0f, 1f);
                bloom += 0.38f * (1f - bp) * (1f - bp);
            }
            CrimsonImpactFX.PushAmbience(AmbientAnchor, MathF.Max(bloom, 0f));
        }

        /// <summary>扫开中刀光前缘火花,喷量随扫掠增量;湿笔拍位部分换成暗墨滴</summary>
        private void SpawnSweepSparks() {
            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt < 0 || lt > a.Def.SweepFrames + 1) {
                    continue;
                }
                float delta = CSR.Sweep(in a.Def, lt) - (lt > 0 ? CSR.Sweep(in a.Def, lt - 1) : 0f);
                int count = delta > 0.20f ? 5 : delta > 0.015f ? 2 : lt % 2 == 0 ? 1 : 0;
                if (count == 0) {
                    continue;
                }
                float speedMul = delta > 0.20f ? 1.5f : 1f;

                Vector2 center = CenterOf(a);
                float edgeU = MathHelper.Clamp(CSR.Sweep(in a.Def, lt) * 1.05f, 0.06f, 0.94f);
                Vector2 pos = CSR.PointAt(in a.Def, center, edgeU, lt);
                Vector2 tangent = (CSR.PointAt(in a.Def, center, MathHelper.Clamp(edgeU + 0.03f, 0f, 1f), lt) - pos)
                    .SafeNormalize(a.Aim.ToRotationVector2());

                for (int k = 0; k < count; k++) {
                    Vector2 vel = tangent * Main.rand.NextFloat(4f, 11f) * speedMul + Main.rand.NextVector2Circular(1.2f, 1.2f);
                    //墨滴 AlphaBlend 染暗(加色画不了黑),色值抬到深酒红
                    if (Main.rand.NextFloat() < a.Def.Bleed + 0.15f) {
                        PRTLoader.NewParticle<PRT_OniInkDrop>(pos, vel * 0.55f, new Color(96, 24, 28)
                            , Main.rand.NextFloat(0.18f, 0.34f) * sizeMul)
                            ?.Configure(Main.rand.Next(16, 26));
                        continue;
                    }
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                        , Main.rand.NextFloat(0.3f, 0.6f) * sizeMul)
                        ?.Configure(Main.rand.Next(10, 18), affectedByGravity: false);
                }
            }
        }

        /// <summary>重击两拍侵蚀期外缘烟屑,终结拍喷量更足</summary>
        private void SpawnEdgeSmoke() {
            if (timer % 2 != 0) {
                return;
            }
            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                if (a.Beat < 3) {
                    continue;
                }
                int lt = timer - a.Birth;
                if (lt <= a.Def.ErodeStart) {
                    continue;
                }
                float erode = CSR.Erode(in a.Def, lt);
                if (erode > 0.78f) {
                    continue;
                }
                int wisps = a.Beat == BeatCount - 1 ? 2 : 1;
                Vector2 finCenter = CenterOf(a);
                for (int k = 0; k < wisps; k++) {
                    float uc = Main.rand.NextFloat(0.12f, 0.96f);
                    Vector2 mid = CSR.PointAt(in a.Def, finCenter, uc, lt);
                    Vector2 dir = (mid - Projectile.Center).SafeNormalize(a.Aim.ToRotationVector2());
                    Vector2 pos = mid + dir * a.Def.HalfX * 0.06f;
                    Vector2 vel = dir * Main.rand.NextFloat(0.3f, 1.1f) + Main.rand.NextVector2Circular(0.35f, 0.35f);

                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel
                        , Color.White, Main.rand.NextFloat(0.055f, 0.105f) * sizeMul)
                        ?.Configure(Main.rand.Next(16, 26)
                            , new Color(150, 26, 34), new Color(46, 16, 24)
                            , Main.rand.NextFloat(0.01f, 0.024f));
                }
            }
        }

        //==================== 判定 ====================

        /// <summary>当前伤害窗内子刀光(同帧多窗取最新一拍)</summary>
        private ActiveSlash FindDamagingSlash() {
            for (int i = actives.Count - 1; i >= 0; i--) {
                int lt = timer - actives[i].Birth;
                if (lt >= actives[i].Def.DamageStart && lt <= actives[i].Def.DamageEnd) {
                    return actives[i];
                }
            }
            return null;
        }

        /// <summary>擦边宽恕(px),目标箱外扩</summary>
        private const int GrazePad = 12;
        /// <summary>辐条判定厚度(px),月牙内侧刀身带宽</summary>
        private const float SpokeThickness = 36f;

        /// <summary>贪婪判定,弧线折线 + 辐条(月牙内侧) + 箱外扩 <see cref="GrazePad"/></summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(GrazePad, GrazePad);

            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt < a.Def.DamageStart || lt > a.Def.DamageEnd) {
                    continue;
                }
                float sweepU = MathHelper.Clamp(CSR.Sweep(in a.Def, lt) * 1.05f, 0f, 1f);
                Vector2 center = CenterOf(a);

                //弧/椭圆,折线采样 + 内侧辐条
                const int samples = 15;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                float thickWorld = MathF.Max(32f, a.Def.Thick * a.Def.HalfX);
                float cp = 0f;
                for (int k = 0; k < samples; k++) {
                    float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                    if (uc > sweepU) {
                        break;
                    }
                    Vector2 mid = CSR.PointAt(in a.Def, center, uc, lt);
                    if (hasPrev && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                        , prev, mid, thickWorld, ref cp)) {
                        return true;
                    }
                    if (k % 3 == 0 && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                        , center, mid, SpokeThickness, ref cp)) {
                        return true;
                    }
                    prev = mid;
                    hasPrev = true;
                }
            }
            return false;
        }

        /// <summary>割草断藤,沿活跃刀光弧线+辐条扫切</summary>
        public override void CutTiles() {
            if (actives.Count == 0) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt < 0 || lt > Math.Max(a.Def.DamageEnd, a.Def.SweepFrames)) {
                    continue;
                }
                float sweepU = MathHelper.Clamp(CSR.Sweep(in a.Def, lt) * 1.05f, 0f, 1f);
                Vector2 center = CenterOf(a);

                const int samples = 9;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                float width = MathF.Max(30f, a.Def.Thick * a.Def.HalfX * 0.8f);
                for (int k = 0; k < samples; k++) {
                    float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                    if (uc > sweepU) {
                        break;
                    }
                    Vector2 mid = CSR.PointAt(in a.Def, center, uc, lt);
                    if (hasPrev) {
                        Utils.PlotTileLine(prev, mid, width, DelegateMethods.CutTiles);
                    }
                    if (k % 2 == 0) {
                        //辐条,月牙内侧
                        Utils.PlotTileLine(center, mid, SpokeThickness, DelegateMethods.CutTiles);
                    }
                    prev = mid;
                    hasPrev = true;
                }
            }
        }

        /// <summary>重击拍伤害加成,快斩×1 重斩×1.3 终结×1.6</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            ActiveSlash a = FindDamagingSlash();
            if (a != null && a.Beat >= 3) {
                modifiers.SourceDamage *= a.Beat == BeatCount - 1 ? 1.6f : 1.3f;
            }
            float offsetX = Projectile.To(target.Center).X;
            modifiers.HitDirectionOverride = MathF.Abs(offsetX) > 0.01f
                ? Math.Sign(offsetX)
                : (MathF.Cos(a?.Aim ?? curAim) >= 0f ? 1 : -1);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);

            //连段命中回气+处决记忆(owner 端)
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<OnikiriPlayer>().OnComboHit(target);
            }

            //每拍首次命中爆点,强度按拍位递增
            ActiveSlash a = FindDamagingSlash();
            if (a != null && !a.ImpactDone) {
                a.ImpactDone = true;
                TriggerImpactBurst(target.Center + VaultUtils.RandVr(0, target.width / 3f), (a.Beat + 1) / (float)BeatCount, a.Aim, a.Def.Flip, steel);

                //命中只捏实体刀姿态(停驻+回坐+尺寸脉冲),不碰节拍/判定
                if (actives.Count > 0 && a == actives[^1]) {
                    impactHoldFrames = 1;
                    impactRecoil = 1f;
                    impactRecoilSign = PathSweepSign(in a.Def, a.Aim, a.Facing);
                    bladeScalePulse = 0.04f;
                }
            }

            Vector2 aimDir = (a?.Aim ?? curAim).ToRotationVector2();
            CrimsonRendHitVFX.SpawnHitTick(target.Center, aimDir, sizeMul, steel);
        }

        //==================== 绘制 ====================
        //深度分层(bladeDepth / FarDim,NearWeight 交叉淡化)
        //  身后层,远半侧刀光+身后残影+身后刀身
        //  PreDraw,近侧残影
        //  图元层,近半侧刀光+命中爆点
        //  遮挡层,近景刀身本体

        /// <summary>实体刀精灵,护手钉 <see cref="bladeHandWorld"/>;朝左时垂直翻转并镜像支点</summary>
        private void DrawBladeSprite(SpriteBatch sb, float rotation, int facing, float scale, Color color, Vector2 posOffset = default) {
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 textureSize = blade.Size();
            Vector2 origin = new(textureSize.X * BladeHiltUV.X, textureSize.Y * BladeHiltUV.Y);
            Vector2 textureTip = new(textureSize.X * BladeTipUV.X, textureSize.Y * BladeTipUV.Y);
            SpriteEffects bladeEffect = SpriteEffects.None;
            if (facing < 0) {
                bladeEffect = SpriteEffects.FlipVertically;
                origin.Y = textureSize.Y - origin.Y;
                textureTip.Y = textureSize.Y - textureTip.Y;
            }
            float textureAxis = (textureTip - origin).ToRotation();
            sb.Draw(blade, bladeHandWorld + posOffset - Main.screenPosition, null, color
                , rotation - textureAxis, origin, scale, bladeEffect, 0f);
        }

        private bool AnySmearAlive() {
            for (int i = 0; i < smears.Length; i++) {
                if (smears[i].Life > 0) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>姿态残影,环内自旧到新,按年龄/角速度/深度侧渐隐</summary>
        private void DrawSmears(SpriteBatch sb, bool nearSide) {
            int alive = 0;
            for (int i = 0; i < smears.Length; i++) {
                if (smears[i].Life > 0) {
                    alive++;
                }
            }
            if (alive == 0) {
                return;
            }
            int skip = Math.Max(0, alive - SmearMaxDrawn);

            for (int i = 0; i < SmearCapacity; i++) {
                BladeSmear s = smears[(smearHead + i) % SmearCapacity];
                if (s.Life <= 0) {
                    continue;
                }
                if (skip > 0) {
                    skip--;
                    continue;
                }
                float ageT = 1f - s.Life / (float)SmearLifeFrames;
                float sideW = nearSide ? NearWeight(s.Depth) : 1f - NearWeight(s.Depth);
                float alpha = s.Strength * (1f - ageT) * sideW;
                if (alpha <= 0.02f) {
                    continue;
                }
                Color c = Color.Lerp(new Color(210, 42, 38, 130), new Color(126, 20, 30, 105), ageT)
                    * (alpha * 0.55f);
                if (!nearSide) {
                    c *= DepthDim(s.Depth);
                }
                DrawBladeSprite(sb, s.Rotation, s.Facing, BladeDrawScale * sizeMul * s.Scale, c);
            }
        }

        /// <summary>实体层只画近侧残影;刀身交遮挡层,身后交远景层</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (!Main.dedServ) {
                DrawSmears(Main.spriteBatch, nearSide: true);
            }
            return false;
        }

        /// <summary>身后层,远半侧刀光+身后残影+身后刀身</summary>
        void ICrimsonFarDrawable.DrawFarSlashes() {
            if (Main.dedServ) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (actives.Count > 0 && CSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                for (int i = 0; i < actives.Count; i++) {
                    ActiveSlash a = actives[i];
                    int lt = timer - a.Birth;
                    if (lt < 0 || lt >= a.Def.Life || a.Def.FarDim <= 0f) {
                        continue;
                    }
                    CSR.DrawThreeLayers(device, fx, in a.Def, CenterOf(a), lt, -1f);
                }
                CSR.EndDraw(device, pb, pr, pd);
            }

            float farW = 1f - NearWeight(bladeDepth);
            bool bladeFar = bladeOpacity > 0.01f && farW > 0.02f;
            if (!bladeFar && !AnySmearAlive()) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawSmears(sb, nearSide: false);
            if (bladeFar) {
                Color lightColor = Lighting.GetColor((int)(Owner.Center.X / 16f), (int)(Owner.Center.Y / 16f));
                float scale = BladeDrawScale * sizeMul * DepthScale(bladeDepth) * (1f + bladeScalePulse);
                Color body = Color.Lerp(lightColor, Color.White, 0.12f)
                    * (bladeOpacity * farW * DepthDim(bladeDepth));
                DrawBladeSprite(sb, bladeRotation, bladeFacing, scale, body);
            }
            sb.End();
        }

        /// <summary>遮挡层,近景刀身(阴影+主体),盖在图元刀光之上</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch sb) {
            if (Main.dedServ || bladeOpacity <= 0.01f) {
                return;
            }
            float nearW = NearWeight(bladeDepth);
            if (nearW <= 0.02f) {
                return;
            }

            Color lightColor = Lighting.GetColor((int)(Owner.Center.X / 16f), (int)(Owner.Center.Y / 16f));
            float scale = BladeDrawScale * sizeMul * DepthScale(bladeDepth) * (1f + bladeScalePulse);

            Color shadow = new Color(15, 3, 8, 190) * (bladeOpacity * 0.62f * nearW);
            DrawBladeSprite(sb, bladeRotation, bladeFacing, scale * 1.018f, shadow, new Vector2(bladeFacing, 1f));

            Color body = Color.Lerp(lightColor, Color.White, 0.24f) * (bladeOpacity * nearW);
            DrawBladeSprite(sb, bladeRotation, bladeFacing, scale, body);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || actives.Count == 0 && lastImpactFrame < 0) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (actives.Count > 0 && CSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                for (int i = 0; i < actives.Count; i++) {
                    ActiveSlash a = actives[i];
                    int lt = timer - a.Birth;
                    if (lt < 0 || lt >= a.Def.Life) {
                        continue;
                    }
                    //FarDim>0 只画近半侧,远半侧已在身后层
                    CSR.DrawThreeLayers(device, fx, in a.Def, CenterOf(a), lt, a.Def.FarDim > 0f ? 1f : 0f);
                }
                CSR.EndDraw(device, pb, pr, pd);
            }

            DrawAdditiveLayers();
            DrawCollapseCore();
        }

        /// <summary>命中爆点 + 余韵光球</summary>
        private void DrawAdditiveLayers() {
            bool burstActive = lastImpactFrame >= 0 && timer - lastImpactFrame < BurstFadeFrames;
            bool afterglowActive = lastImpactFrame >= 0 && timer - lastImpactFrame is >= 26 and < AfterglowEnd;
            if (!burstActive && !afterglowActive) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (burstActive) {
                DrawImpactBurst(sb);
            }

            //余韵暗紫红光球内爆
            if (afterglowActive && CWRAsset.StarFlare01?.Value is Texture2D orb) {
                float t = (timer - lastImpactFrame - 26) / 20f;
                float oA = MathF.Sin(t * MathF.PI) * 0.42f;
                float oS = MathHelper.Lerp(0.9f, 0.18f, CSR.EaseOutCubic(t)) * sizeMul;
                Color oc = Color.Lerp(new Color(210, 70, 130), new Color(70, 24, 66), t);
                sb.Draw(orb, lastImpactPos - Main.screenPosition, null, oc * oA
                    , t * 2.4f, orb.Size() * 0.5f, oS, SpriteEffects.None, 0);
            }

            sb.End();
        }

        /// <summary>命中爆点,金属白热星爆/放射/十字;血肉暗红撕裂/血环</summary>
        private void DrawImpactBurst(SpriteBatch sb) {
            float bt = MathHelper.Clamp(timer - lastImpactFrame, 0f, BurstFadeFrames);
            float bp = bt / BurstFadeFrames;
            if (bp >= 1f) {
                return;
            }

            Vector2 impact = lastImpactPos - Main.screenPosition;
            Vector2 aimDir = lastImpactAim.ToRotationVector2();
            float inv = 1f - bp;
            float easeOut = 1f - MathF.Pow(inv, 3f);
            float seedRot = Projectile.whoAmI * 1.37f;

            if (lastImpactSteel) {
                DrawSteelImpactBurst(sb, impact, aimDir, inv, easeOut, seedRot, bt);
            }
            else {
                DrawFleshImpactBurst(sb, impact, aimDir, inv, easeOut, seedRot, bt);
            }
        }

        private void DrawSteelImpactBurst(SpriteBatch sb, Vector2 impact, Vector2 aimDir
            , float inv, float easeOut, float seedRot, float bt) {
            //白热核心,峰值收紧到 0.7 防糊笔触
            if (CWRAsset.StarFlare02?.Value is Texture2D flare) {
                float coreA = MathF.Pow(inv, 2.0f) * 0.70f;
                float coreS = (0.85f + easeOut * 0.65f) * sizeMul;
                sb.Draw(flare, impact, null, new Color(255, 244, 232) * coreA, seedRot
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
                sb.Draw(flare, impact, null, new Color(255, 120, 80) * (coreA * 0.5f), -seedRot * 0.6f
                    , flare.Size() * 0.5f, coreS * 1.3f, SpriteEffects.None, 0);
            }

            if (CWRAsset.RayBurst01?.Value is Texture2D rays) {
                float rayA = MathF.Pow(inv, 1.8f) * 0.78f;
                float rayS = (1.1f + easeOut * 1.0f) * sizeMul;
                sb.Draw(rays, impact, null, new Color(255, 190, 160) * rayA, seedRot * 0.4f
                    , rays.Size() * 0.5f, rayS, SpriteEffects.None, 0);
            }

            if (CWRAsset.RayCross01?.Value is Texture2D cross) {
                float cA = MathF.Pow(inv, 2.4f) * 0.82f;
                sb.Draw(cross, impact, null, new Color(255, 230, 215) * cA, lastImpactAim
                    , cross.Size() * 0.5f, new Vector2(2.2f, 1.0f) * easeOut * sizeMul, SpriteEffects.None, 0);
            }

            if (CWRAsset.Ring01?.Value is Texture2D ring) {
                float ringS = (0.4f + easeOut * 2.2f) * sizeMul;
                float ringA = MathF.Pow(inv, 2.5f) * 0.6f;
                sb.Draw(ring, impact, null, new Color(255, 90, 60) * ringA, 0f
                    , ring.Size() * 0.5f, ringS, SpriteEffects.None, 0);
            }

            if (bt < 9f && CWRAsset.TearSpread01?.Value is Texture2D tear) {
                float tA = MathF.Pow(1f - bt / 9f, 1.8f) * 0.85f;
                sb.Draw(tear, impact, null, new Color(255, 150, 120) * tA, lastImpactAim
                    , tear.Size() * 0.5f, (1.5f + easeOut * 0.55f) * sizeMul, SpriteEffects.None, 0);
                sb.Draw(tear, impact, null, new Color(255, 60, 40) * (tA * 0.75f), lastImpactAim + 0.35f * lastImpactFlip
                    , tear.Size() * 0.5f, (1.0f + easeOut * 0.4f) * sizeMul
                    , SpriteEffects.FlipVertically, 0);
            }

            if (CWRAsset.SpeedLines01?.Value is Texture2D lines) {
                EnsureSpeedLineRects();
                float lA = MathF.Pow(inv, 1.6f) * 0.5f;
                for (int i = 0; i < speedLineRects.Length; i++) {
                    Rectangle src = speedLineRects[i];
                    float off = speedLineOffsets[i];
                    Vector2 pos = impact - aimDir * (40f + off * 70f + easeOut * 40f) * sizeMul
                        + aimDir.RotatedBy(MathHelper.PiOver2) * (off - 0.5f) * 110f * sizeMul;
                    sb.Draw(lines, pos, src, new Color(255, 170, 140) * lA, lastImpactAim
                        , src.Size() * 0.5f, new Vector2(0.40f + easeOut * 0.30f, 0.42f) * sizeMul
                        , SpriteEffects.None, 0);
                }
            }
        }

        private void DrawFleshImpactBurst(SpriteBatch sb, Vector2 impact, Vector2 aimDir
            , float inv, float easeOut, float seedRot, float bt) {
            //暗红软核,禁纯白
            if (CWRAsset.StarFlare02?.Value is Texture2D flare) {
                float coreA = MathF.Pow(inv, 1.8f) * 0.48f;
                float coreS = (0.7f + easeOut * 0.55f) * sizeMul;
                sb.Draw(flare, impact, null, CrimsonRendHitVFX.WoundHot * coreA, seedRot
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
                sb.Draw(flare, impact, null, CrimsonRendHitVFX.BloodDeep * (coreA * 0.7f), -seedRot * 0.5f
                    , flare.Size() * 0.5f, coreS * 1.25f, SpriteEffects.None, 0);
            }

            //血环外扩
            if (CWRAsset.Ring01?.Value is Texture2D ring) {
                float ringS = (0.35f + easeOut * 1.8f) * sizeMul;
                float ringA = MathF.Pow(inv, 2.2f) * 0.55f;
                sb.Draw(ring, impact, null, CrimsonRendHitVFX.Blood * ringA, 0f
                    , ring.Size() * 0.5f, ringS, SpriteEffects.None, 0);
            }

            //刃向撕裂口
            if (bt < 10f && CWRAsset.TearSpread01?.Value is Texture2D tear) {
                float tA = MathF.Pow(1f - bt / 10f, 1.6f) * 0.9f;
                sb.Draw(tear, impact, null, CrimsonRendHitVFX.Arterial * tA, lastImpactAim
                    , tear.Size() * 0.5f, (1.35f + easeOut * 0.5f) * sizeMul, SpriteEffects.None, 0);
                sb.Draw(tear, impact, null, CrimsonRendHitVFX.BloodDeep * (tA * 0.8f)
                    , lastImpactAim + 0.4f * lastImpactFlip
                    , tear.Size() * 0.5f, (0.95f + easeOut * 0.35f) * sizeMul
                    , SpriteEffects.FlipVertically, 0);
            }

            //暗红速度线(无白热放射/十字)
            if (CWRAsset.SpeedLines01?.Value is Texture2D lines) {
                EnsureSpeedLineRects();
                float lA = MathF.Pow(inv, 1.5f) * 0.42f;
                for (int i = 0; i < speedLineRects.Length; i++) {
                    Rectangle src = speedLineRects[i];
                    float off = speedLineOffsets[i];
                    Vector2 pos = impact - aimDir * (36f + off * 60f + easeOut * 36f) * sizeMul
                        + aimDir.RotatedBy(MathHelper.PiOver2) * (off - 0.5f) * 95f * sizeMul;
                    sb.Draw(lines, pos, src, CrimsonRendHitVFX.WoundHot * lA, lastImpactAim
                        , src.Size() * 0.5f, new Vector2(0.36f + easeOut * 0.28f, 0.4f) * sizeMul
                        , SpriteEffects.None, 0);
                }
            }
        }

        private void EnsureSpeedLineRects() {
            if (speedLineRects != null) {
                return;
            }
            speedLineRects = new Rectangle[3];
            speedLineOffsets = new float[3];
            for (int i = 0; i < 3; i++) {
                speedLineRects[i] = new Rectangle(0, Main.rand.Next(0, 1024 - 96), 1024, 96);
                speedLineOffsets[i] = Main.rand.NextFloat();
            }
        }

        /// <summary>负片收缩,爆闪第2~8帧暗核压加色星爆<br/>
        /// AlphaBlend 压暗须用带 alpha 形状贴图(SmokeSheet01),黑底不透明亮度贴会糊成暗方框</summary>
        private void DrawCollapseCore() {
            float bt = timer - lastImpactFrame;
            if (lastImpactFrame < 0 || bt < 2f || bt > 8f) {
                return;
            }
            Texture2D cloud = CWRAsset.SmokeSheet01?.Value;
            if (cloud == null) {
                return;
            }

            float t = (bt - 2f) / 6f;   //0..1
            //峰值~0.36,收缩至约 1/3
            float coreS = MathHelper.Lerp(0.36f, 0.12f, t * t) * sizeMul;
            float coreA = MathF.Sin(t * MathF.PI) * 0.78f;
            int frameSize = cloud.Width / 2;
            Rectangle frame = new(Projectile.whoAmI % 2 * frameSize, Projectile.whoAmI / 2 % 2 * frameSize, frameSize, frameSize);

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(cloud, lastImpactPos - Main.screenPosition, frame
                , new Color(16, 4, 9) * coreA, Projectile.whoAmI * 1.37f
                , frame.Size() * 0.5f, coreS, SpriteEffects.None, 0);
            sb.End();
        }
    }
}
