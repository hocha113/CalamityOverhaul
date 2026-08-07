using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using OSR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSlashs.OniSlashRenderer;
using RiftDef = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSlashs.OniSlashRenderer.RiftDef;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSlashs
{
    /// <summary>
    /// 鬼门开缝,按住左键滚动五段连段控制器(绯红裂空斩的平行普攻,材质=撕开世界膜)<br/>
    /// 连段状态机/铭刻/役鬼/教程集成与绯红裂空斩行为一致,表现层为曝光表语法:<br/>
    /// S0 蓄势(应力线预告)→S1 一帧撕满(整形出现,无揭开wipe)→S2 冻结保持→S3 鬼门闭合<br/>
    /// 形状升级链:拍0/1 直线斩缝组X→拍2 首记月牙→拍3 大月牙带力点→拍4 巨弧鬼门大开<br/>
    /// 刀身/缝带/碰撞共用真投影几何源;命中停顿冻结整张画(Birth顺延)<br/>
    /// ai[0]=初始瞄准角(弧度) ai[2]=尺寸倍率
    /// </summary>
    internal class OniSlash : BaseHeldProj, IPrimitiveDrawable, ICrimsonFarDrawable, IOverlayDrawable
        , IOniComboController
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override bool CanFire => !firstBeatFired
            || scheduling && hasHandoff && timer < nextBeatTime;

        //==== 节拍常量 ====
        private const int BeatCount = 5;
        private const int BurstFadeFrames = 16;
        private const int AfterglowEnd = 46;   //命中余韵最晚结束帧(相对 lastImpactFrame)
        private const int BaseHitCooldown = 10;
        private const int BladeReleaseRecoveryFrames = 12;
        /// <summary>疾走接管时旧斩缝保留的极速褪去帧数</summary>
        private const int FlashStepInterruptFadeFrames = 6;
        /// <summary>首次纯起手帧数,反向蓄势后无条件出首拍(仅首拍延后)</summary>
        private const int FirstWindupFrames = 2;
        private const float BladePathStart = 0.08f;
        /// <summary>刀停驻处路径参数,收在缝腹侧防刀尖穿出窄带</summary>
        private const float BladePathEnd = 0.88f;
        private const float BladeDrawScale = 0.90f;
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

        /// <summary>已撕出的斩缝,出生帧与瞄准角开火瞬间冻结</summary>
        private sealed class ActiveRift
        {
            public RiftDef Def;
            public int Birth;        //绝对 timer 帧(整画冻结时顺延)
            public int Beat;         //0..4 拍位
            public float Aim;        //该拍开火瞄准角
            public int Facing;       //该拍冻结朝向
            public bool ImpactDone;  //本拍首次命中爆点已触发
            public bool ResourceGranted;  //本拍首次命中的资源已结算
            public bool RipPlayed;   //撕开脆响已播(对齐撕开帧)
            public bool PingPlayed;  //收势轻确认已播(整画冻结下 lt 停帧,须一次性)
            public Vector2? FrozenCenter;   //硬让位瞬间冻结世界锚点
            public float GatherFromRot;     //收势起点刀角(上一停驻位)
            public float GatherFromDepth;
            public bool HasGatherFrom;
            public OniMeiCombatProfile Profile;
            public uint ActionSerial;
            public int BaseWeaponDamage;
            public float ArmedConditionMul;
            public bool TideOnBeat;
            public bool ExecuteRefunded;
        }

        /// <summary>停手超过该帧后再按从第一拍重启,短停续接拍序</summary>
        private const int ComboResetFrames = 30;
        /// <summary>模组交接重启微前摇(帧),仅 <see cref="OniBladeHandoff"/> 新鲜时;普通点按当帧出刀</summary>
        private const int RestartWindupFrames = 3;
        /// <summary>轻点缓冲(帧),让位/签名拍保留中的点击不丢,窗口关后补发</summary>
        private const int PressBufferFrames = 24;

        private readonly List<ActiveRift> actives = new(8);
        private readonly OniSlashRibbon ribbon = new();
        private int timer;
        private int comboIndex;
        private int nextBeatTime;
        private int lastBeatFire;
        private bool scheduling;
        private bool firstBeatFired;
        /// <summary>疾走已接管本控制器;旧斩缝只退场、不再排拍或造成伤害</summary>
        private bool flashStepInterrupted;
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
        //====铭刻(控制器出生时冻结三槽；每拍仅分配新的动作序号与条件快照)====
        private OniMeiCombatProfile meiProfile = OniMeiCombatProfile.Identity;
        /// <summary>狮势链:连续未被打断的拍数,第五拍吼开;打断/让位归零</summary>
        private int meiLionChain;
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
        private float bladeDepth;          //-1=身后 .. +1=身前(归一化z)
        private float bladeStretch = 1f;   //轴向透视缩放(同源投影导出)
        private float bladeOpacity;
        private int bladeFacing = 1;
        private bool bladeEdgeFlip;        //翻刃态,反向拍刃口朝挥动前缘
        private bool bladePoseInitialized;
        private Player.CompositeArmStretchAmount bladeArmStretch = Player.CompositeArmStretchAmount.Full;
        private Vector2 bladeHandWorld;
        //==== 命中反馈(整画冻结+回坐+尺寸脉冲,不碰节拍与判定) ====
        /// <summary>整画冻结余量:斩缝时间轴/刀/体态/条带同帧冻住,Birth 顺延</summary>
        private int impactHoldFrames;
        private float impactRecoil;
        private float impactRecoilSign = 1f;
        private float bladeScalePulse;
        //==== 爆发态 ====
        private bool bladeInBurst;          //本帧处于撕开跨越段
        private float bladeSpeedFade;       //角速度包络,峰值期本体让位残迹
        //==== 全身体态(纯视觉,由刀时间轴确定性导出,不上网络) ====
        private float bodyLean;
        private bool bodyLeanApplied;

        /// <summary>刃身环境光锚点(非命中点),最新斩缝力点位;真实命中用 <see cref="lastImpactPos"/></summary>
        private Vector2 AmbientAnchor {
            get {
                if (actives.Count > 0) {
                    ActiveRift a = actives[^1];
                    return OSR.StaticPointAt(in a.Def, CenterOf(a), a.Def.GapePeakU);
                }
                return Projectile.Center + curAim.ToRotationVector2() * 180f * sizeMul;
            }
        }

        /// <summary>持有者客户端调用(<c>myPlayer</c>),tML 自动同步</summary>
        /// <param name="aim">无需归一化,此后每拍重捕鼠标</param>
        /// <param name="source">null 则 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 origin, Vector2 aim, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX).ToRotation();
            Projectile projectile = Projectile.NewProjectileDirect(source, origin, Vector2.Zero
                , ModContent.ProjectileType<OniSlash>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai2: scale);
            OniMeiActionContext.Capture(projectile, player, source, damage, OniMeiActionKind.Combo);
            return projectile;
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
            OniMeiActionContext context = OniMeiActionContext.Get(Projectile);
            meiProfile = context?.HasSnapshot == true
                ? context.Profile
                : OniMeiCombatProfile.Identity;
        }

        /// <summary>
        /// 五拍形状升级链:快斩是线重斩是月牙,全部跨度&lt;π,力点由张口不对称写出<br/>
        /// 拍0/1 镜像对角直缝组X(长度读作速度)→拍2 首记压扁月牙→拍3 舀击大月牙(入锋尖收锋肥撕)
        /// →拍4 巨弧终结鬼门大开+魂火<br/>
        /// Seed 掺入出生帧防循环同噪声;深度剖面笔画固定不随朝向镜像
        /// </summary>
        private RiftDef BuildBeatDef(int beat, float a, float f, float s) {
            RiftDef d = beat switch {
                //0 顺手斜切直缝,X第一笔;正向偏移把缝画在目标身上而非穿过自己
                0 => new RiftDef {
                    Mode = 1f, Rot = a + f * 0.44f, R = 195f * s, LineZSlope = 0.30f,
                    OffsetAlongAim = 108f * s,
                    Flip = f, GapeMax = 24f * s, GapePeakU = 0.60f, GapePowIn = 1.55f, GapePowOut = 0.85f,
                    GatherFrames = 2, HoldFrames = 2, Life = 17, DamageStart = 2, DamageEnd = 5,
                    TelegraphAmt = 0f, EmberAmt = 0f, FarDim = 0.72f, Opacity = 0.94f,
                },
                //1 反手镜像斜切,X第二笔,稍长
                1 => new RiftDef {
                    Mode = 1f, Rot = a - f * 0.44f, R = 215f * s, LineZSlope = -0.30f,
                    OffsetAlongAim = 118f * s,
                    Flip = -f, GapeMax = 26f * s, GapePeakU = 0.58f, GapePowIn = 1.55f, GapePowOut = 0.85f,
                    GatherFrames = 2, HoldFrames = 2, Life = 17, DamageStart = 2, DamageEnd = 5,
                    TelegraphAmt = 0f, EmberAmt = 0f, FarDim = 0.72f, Opacity = 0.96f,
                },
                //2 首记月牙,贯通面压扁0.60,腹部朝目标中心后拉
                2 => new RiftDef {
                    Mode = 0f, Rot = a, Span = 2.60f, R = 240f * s, Tilt = 0.93f, ZPhase = 0f,
                    Flip = f, OffsetAlongAim = -22f * s,
                    GapeMax = 34f * s, GapePeakU = 0.58f, GapePowIn = 1.40f, GapePowOut = 0.80f,
                    GatherFrames = 3, HoldFrames = 2, Life = 24, DamageStart = 3, DamageEnd = 6,
                    TelegraphAmt = 0.30f, EmberAmt = 0f, FarDim = 0.72f, Opacity = 1f,
                },
                //3 蓄势大月牙,舀击面中段朝观者扑近(整弧近侧,重拍不沉身后),力点后置肥撕
                3 => new RiftDef {
                    Mode = 0f, Rot = a - f * 0.30f, Span = 2.85f, R = 330f * s, Tilt = 0.98f, ZPhase = 1f,
                    Flip = f, OffsetAlongAim = -48f * s,
                    GapeMax = 46f * s, GapePeakU = 0.66f, GapePowIn = 1.75f, GapePowOut = 0.72f,
                    GatherFrames = 6, HoldFrames = 3, Life = 30, DamageStart = 6, DamageEnd = 10,
                    TelegraphAmt = 0.55f, EmberAmt = 0f, FarDim = 0.68f, Opacity = 0.98f,
                },
                //4 终结巨弧,鬼门大开,魂火涌出
                _ => new RiftDef {
                    Mode = 0f, Rot = a + f * 0.18f, Span = 3.02f, R = 430f * s, Tilt = 0.90f, ZPhase = 0f,
                    Flip = -f, OffsetAlongAim = -70f * s,
                    GapeMax = 62f * s, GapePeakU = 0.60f, GapePowIn = 1.50f, GapePowOut = 0.68f,
                    GatherFrames = 7, HoldFrames = 3, Life = 44, DamageStart = 7, DamageEnd = 11,
                    TelegraphAmt = 0.65f, EmberAmt = 1f, FarDim = 0.64f, Opacity = 1f,
                },
            };
            d.Seed = (beat * 0.191f + timer * 0.037f) % 1f;
            return d;
        }

        /// <summary>斩缝锚点,平时随玩家,硬让位后取冻结点</summary>
        private Vector2 CenterOf(ActiveRift a)
            => a.FrozenCenter ?? (Projectile.Center + a.Aim.ToRotationVector2() * a.Def.OffsetAlongAim);

        /// <summary>本帧是否持刀权(排拍/活斩缝/实体刀未收完);硬让位冻结的尸体斩缝不算</summary>
        public bool ClaimsBlade => scheduling || bladeOpacity > 0.03f || AnyLiveRift();

        /// <summary>是否存在未被硬让位冻结的活斩缝</summary>
        private bool AnyLiveRift() {
            for (int i = 0; i < actives.Count; i++) {
                if (actives[i].FrozenCenter == null) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>不动护窗口:后两重拍(蓄势/终结)的活斩缝尚在伤害窗附近</summary>
        public bool InCommittedBeats {
            get {
                for (int i = 0; i < actives.Count; i++) {
                    ActiveRift a = actives[i];
                    if (a.FrozenCenter != null || a.Beat < 3) {
                        continue;
                    }
                    if (timer - a.Birth <= a.Def.DamageEnd + 6) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>实体刀当前姿态(肢解居合继承用);不可见返回 false</summary>
        public bool TryGetBladePose(out float rotation, out int facing) {
            rotation = bladeRotation;
            facing = bladeFacing;
            return bladePoseInitialized && bladeOpacity > 0.05f;
        }

        /// <summary>
        /// 持续左键→疾走的刀权交接,返回当前刀角;
        /// 旧斩缝冻结退场并关闭伤害
        /// </summary>
        public bool BeginFlashStepInterrupt(Vector2 dashAim, out float startRotation) {
            startRotation = bladeRotation;
            bool leftHeld = DownLeft || Owner.controlUseItem
                || (Projectile.IsOwnedByLocalPlayer() && Main.mouseLeft);
            bool attackActive = scheduling || firstWindupTicks > 0
                || bladePoseInitialized || AnyLiveRift();
            if (flashStepInterrupted || !leftHeld || !attackActive) {
                return false;
            }

            if (!bladePoseInitialized || bladeOpacity <= 0.05f) {
                Vector2 aim = dashAim.SafeNormalize(Vector2.UnitX * Owner.direction);
                int facing = MathF.Abs(aim.X) < 0.05f ? Owner.direction : (aim.X > 0f ? 1 : -1);
                startRotation = aim.ToRotation() + facing * 0.55f;
            }

            flashStepInterrupted = true;
            scheduling = false;
            pressBuffer = 0;
            prevDownLeft = true;
            lastImpactFrame = -999;
            Projectile.friendly = false;
            ribbon.Clear();
            FreezeSlashVisuals(FlashStepInterruptFadeFrames);
            //铭刻:疾走取消=狮势打断(金粉散落);友切在原刀位留延迟斩影并积咎
            if (meiLionChain > 1) {
                OniMeiStrikes.SpawnLionScatter(Projectile.Center, sizeMul);
            }
            meiLionChain = 0;
            if (Projectile.IsOwnedByLocalPlayer()) {
                TrySpawnGuiltEcho();
            }
            Projectile.netUpdate = true;
            return true;
        }

        /// <summary>友切「咎影」:被疾走取消的那拍在原地留错位残像,滞拍后由断斩咬合(owner 端)</summary>
        private void TrySpawnGuiltEcho() {
            //锚在最近活斩缝;拍间空窗退回玩家中心+当前瞄准
            Vector2 center;
            float aim;
            OniMeiCombatProfile profile = meiProfile;
            int baseWeaponDamage = OniMeiActionContext.Get(Projectile)?.BaseWeaponDamage
                ?? Projectile.damage;
            if (actives.Count > 0) {
                ActiveRift a = actives[^1];
                center = CenterOf(a);
                aim = a.Aim;
                profile = a.Profile;
                baseWeaponDamage = a.BaseWeaponDamage;
            }
            else {
                center = Projectile.Center;
                aim = curAim;
            }
            if (!profile.GuiltEcho) {
                return;
            }
            OniMeiStrikes.FireGuiltEcho(Owner, center, aim, baseWeaponDamage, Projectile.knockBack,
                sizeMul, Projectile.GetSource_FromAI());
            Owner.GetModPlayer<OnikiriPlayer>().OnGuiltEchoSpawned();
        }

        /// <summary>残心接管本次左键,清掉普攻补发</summary>
        public void ConsumeZanshinInput() {
            pressBuffer = 0;
            scheduling = false;
            prevDownLeft = DownLeft;
            //残心属特殊技打断,狮势链归零
            meiLionChain = 0;
        }

        /// <summary>刀柄方向支点(解算朝向);绘制锚用 <see cref="bladeHandWorld"/>,拆开斩断手↔臂依赖环</summary>
        private Vector2 BladeHandPosition(int facing) => Owner.GetPlayerStabilityCenter()
            + new Vector2(facing * 3f, -5f * Owner.gravDir);

        /// <summary>缝中线点→实体刀朝向,采静态投影只取方向</summary>
        private float BladePathRotation(in RiftDef d, float aim, int facing, float u) {
            Vector2 center = Projectile.Center + aim.ToRotationVector2() * d.OffsetAlongAim;
            Vector2 edge = OSR.StaticPointAt(in d, center, MathHelper.Clamp(u, BladePathStart, BladePathEnd));
            Vector2 fromHand = edge - BladeHandPosition(facing);
            return fromHand.LengthSquared() > 1f ? fromHand.ToRotation() : aim;
        }

        /// <summary>路径 u 处归一化深度(实体刀深度通道)</summary>
        private float BladePathDepth(in RiftDef d, float aim, float u) {
            Vector2 center = Projectile.Center + aim.ToRotationVector2() * d.OffsetAlongAim;
            OSR.StaticPointAt(in d, center, MathHelper.Clamp(u, BladePathStart, BladePathEnd), out float z);
            float amp = OSR.DepthAmp(in d);
            return amp > 0.001f ? MathHelper.Clamp(z / amp, -1f, 1f) : 0f;
        }

        /// <summary>起笔端角速度符号,+1 顺时针 −1 逆时针</summary>
        private float PathSweepSign(in RiftDef d, float aim, int facing) {
            float rotA = BladePathRotation(in d, aim, facing, BladePathStart);
            float rotB = BladePathRotation(in d, aim, facing, BladePathStart + 0.12f);
            return MathHelper.WrapAngle(rotB - rotA) >= 0f ? 1f : -1f;
        }

        /// <summary>反向扫掠拍须翻刃,刃口镜像到挥动前缘;由真实路径角速度导出,直线拍同样成立</summary>
        private bool EdgeFlipOf(in RiftDef d, float aim, int facing)
            => PathSweepSign(in d, aim, facing) * facing < 0f;

        private static float LerpAngle(float from, float to, float amount)
            => from + MathHelper.WrapAngle(to - from) * MathHelper.Clamp(amount, 0f, 1f);

        /// <summary>深度→近景权重,±0.22 交叉淡化</summary>
        private static float NearWeight(float depth) => OSR.SmoothStep01((depth + 0.22f) / 0.44f);

        /// <summary>路径 u 处刀身视觉倍率(轴向透视缩短,同源投影);停驻态收紧下限防刀身发矮</summary>
        private float StretchOf(in RiftDef d, float u, bool burst) {
            float raw = OSR.BladeStretchAt(in d, MathHelper.Clamp(u, BladePathStart, BladePathEnd));
            (float floor, float ceil) = burst ? (0.45f, 1.30f) : (0.66f, 1.16f);
            return MathHelper.Clamp(raw, floor, ceil);
        }

        /// <summary>体态倾斜幅度(rad);仅后两重拍(蓄势/终结)前倾发力,前三拍直立</summary>
        private static float BeatLeanAmp(int beat) => beat switch {
            3 => 0.12f,
            4 => 0.17f,
            _ => 0f,
        };

        /// <summary>深度→亮度,身后压暗至~0.72</summary>
        private static float DepthDim(float depth) => MathHelper.Lerp(1f, 0.72f, MathHelper.Clamp(-depth, 0f, 1f));

        //==================== 连段推进 ====================

        public override void AI() {
            Projectile.Center = Owner.Center;
            timer++;

            //整画冻结:斩缝时间轴锚点顺延,刀/体态/条带同帧冻住,输入与判定照常
            bool frozen = impactHoldFrames > 0;
            if (frozen) {
                impactHoldFrames--;
                for (int i = 0; i < actives.Count; i++) {
                    actives[i].Birth++;
                }
                nextBeatTime++;
                lastBeatFire++;
            }

            UpdateYield();
            UpdateCombo();
            UpdateBeatEvents();
            if (!frozen) {
                UpdateBladePose();
                ribbon.Update();
            }

            UpdatePose();
            if (bladeOpacity <= 0.01f) {
                ReleaseBodyLean();
            }

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
        /// <summary>硬让位后斩缝最大余寿(帧)</summary>
        private const int YieldFadeFrames = 8;
        //上帧硬让位,检测让位起始沿
        private bool yielding;

        /// <summary>把在场斩缝冻结在世界坐标并压缩到指定余寿</summary>
        private void FreezeSlashVisuals(int maxFadeFrames) {
            foreach (ActiveRift a in actives) {
                a.FrozenCenter ??= Projectile.Center + a.Aim.ToRotationVector2() * a.Def.OffsetAlongAim;
                int remain = a.Def.Life - (timer - a.Birth);
                if (remain > maxFadeFrames) {
                    a.Birth = timer - (a.Def.Life - maxFadeFrames);
                }
            }
        }

        /// <summary>刀权仲裁,主人有硬占刀权技能时停排/冻结斩缝速褪/实体刀交权;狮势链随让位归零</summary>
        private void UpdateYield() {
            bool hard = OniBladeOccupancy.AnyHardOccupant(Owner);
            if (hard && !yielding) {
                scheduling = false;
                bladeOpacity = 0f;
                ribbon.Clear();
                FreezeSlashVisuals(YieldFadeFrames);
                if (meiLionChain > 1) {
                    OniMeiStrikes.SpawnLionScatter(Projectile.Center, sizeMul);
                }
                meiLionChain = 0;
            }
            yielding = hard;
        }

        /// <summary>连段排拍,按住推进松手停排,收势再按从第一拍重启;居合/签名拍软保留期间不夺刀</summary>
        private void UpdateCombo() {
            if (flashStepInterrupted) {
                prevDownLeft = DownLeft;
                return;
            }
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
                    OniMeiActionContext context = OniMeiActionContext.Get(Projectile);
                    meiProfile = context?.HasSnapshot == true
                        ? context.Profile
                        : OniMeiCombatProfile.Identity;
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
                //处决后续优先消费按下沿,清缓冲防同点击再兑现
                if (justPressed && Projectile.IsOwnedByLocalPlayer()
                    && Owner.GetModPlayer<OnikiriPlayer>().TryExecutionAnnihilate(Item, edgeVerified: true)) {
                    pressBuffer = 0;
                    return;
                }
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
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.GetModPlayer<OnikiriPlayer>().CancelExecutionIntent(settleFollowup: true);
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

        /// <summary>开火一拍，冻结方向、铭刻条件与动作序号；三槽和基础伤害沿用控制器出生快照。</summary>
        private void FireBeat() {
            hasHandoff = false;   //交接前摇已兑现
            pressBuffer = 0;      //缓冲点击已兑现
            float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : Projectile.ai[0];
            curAim = aim;
            float cos = MathF.Cos(aim);
            float flip = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1f : -1f);
            int beat = comboIndex;

            if (Projectile.IsOwnedByLocalPlayer()) {
                OniMeiActionContext.BeginSubAction(Projectile, Owner, OniMeiActionKind.Combo);
                OniMeiActionContext.ArmConditions(Projectile, Owner,
                    allowSilent: true, allowPlanted: beat == BeatCount - 1);
            }
            OniMeiActionContext context = OniMeiActionContext.Get(Projectile);
            meiProfile = context?.HasSnapshot == true
                ? context.Profile
                : OniMeiCombatProfile.Identity;
            float beatSpeedFactor = MathHelper.Clamp(1f / Owner.GetWeaponAttackSpeed(Item), 0.5f, 1.6f);

            //鵺切:离地够高的第五拍整拍换成扑击,本拍不再撕常规巨缝(狮势链同时断)
            if (beat == BeatCount - 1 && meiProfile.NueDive && Projectile.IsOwnedByLocalPlayer()
                && Owner.GetModPlayer<OnikiriPlayer>().TryNueDive(in meiProfile,
                    context?.BaseWeaponDamage ?? Projectile.damage, sizeMul, Projectile)) {
                meiLionChain = 0;
                comboIndex = 0;
                lastBeatFire = timer;
                nextBeatTime = timer + Math.Max(4,
                    (int)MathF.Round(BeatGap[beat] * beatSpeedFactor * meiProfile.ComboGapMul));
                return;
            }

            ActiveRift active = new() {
                Def = BuildBeatDef(beat, aim, flip, sizeMul),
                Birth = timer,
                Beat = beat,
                Aim = aim,
                Facing = (int)flip,
                GatherFromRot = bladeRotation,
                GatherFromDepth = bladeDepth,
                HasGatherFrom = bladePoseInitialized && bladeOpacity > 0.05f,
                Profile = meiProfile,
                ActionSerial = context?.ActionSerial ?? 0,
                BaseWeaponDamage = context?.BaseWeaponDamage ?? Projectile.damage,
                ArmedConditionMul = context?.ArmedConditionMul ?? 1f,
                TideOnBeat = context?.TideOnBeat == true,
            };
            actives.Add(active);
            if (Projectile.IsOwnedByLocalPlayer()) {
                WraithComboBeatEvent wraithBeat = new(active.Beat, active.Aim, active.Facing,
                    active.BaseWeaponDamage, Projectile.knockBack, sizeMul,
                    active.Def.DamageStart, active.ActionSerial);
                WraithAbilityService.PublishComboBeat(Owner, in wraithBeat);
            }
            PlayBeatFireSound(beat);

            Projectile.localNPCHitCooldown = Math.Max(5, (int)(BaseHitCooldown * beatSpeedFactor));
            comboIndex = (comboIndex + 1) % BeatCount;
            lastBeatFire = timer;
            nextBeatTime = timer + Math.Max(4
                , (int)MathF.Round(BeatGap[beat] * beatSpeedFactor * meiProfile.ComboGapMul));

            UpdateMeiOnBeatFired(active);
        }

        /// <summary>
        /// 铭刻逐拍推进:狮势链全客户端按拍序确定性蓄势(档随物品同步),
        /// 副斩仅 owner 生成;龙火窗口态在 owner 的 ModPlayer 上
        /// </summary>
        private void UpdateMeiOnBeatFired(ActiveRift action) {
            int beat = action.Beat;
            float aim = action.Aim;
            OniMeiCombatProfile profile = action.Profile;
            if (profile.LionRoar) {
                meiLionChain = beat == 0 ? 1 : beat == meiLionChain ? meiLionChain + 1 : 0;
                if (meiLionChain > 1) {
                    OniMeiStrikes.SpawnLionBuildup(Projectile.Center, aim, sizeMul, meiLionChain);
                }
                if (meiLionChain >= BeatCount) {
                    meiLionChain = 0;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        OniMeiStrikes.FireLionJaw(Owner, Projectile.Center, aim, action.BaseWeaponDamage
                            , Projectile.knockBack, sizeMul, Projectile.GetSource_FromAI());
                    }
                }
            }
            else {
                meiLionChain = 0;
            }

            if (profile.DragonfireLoop && Projectile.IsOwnedByLocalPlayer()) {
                OnikiriPlayer okp = Owner.GetModPlayer<OnikiriPlayer>();
                if (beat == BeatCount - 1 && okp.TryConsumeKurikara()) {
                    OniMeiStrikes.FireKurikaraLoop(Owner, Projectile.Center, aim, action.BaseWeaponDamage
                        , Projectile.knockBack, sizeMul, Projectile.GetSource_FromAI());
                }
                else if (okp.KurikaraWindow > 0) {
                    //窗口内前四拍:刀侧火鞘火星(owner 可见,窗口态不进网络)
                    OniMeiStrikes.SpawnDragonfireBeatFlame(Owner, aim, sizeMul);
                }
            }
        }

        /// <summary>
        /// 实体刀姿态时间轴(纯视觉),收势反拉→撕开2帧跨越(行程交条带)→停驻静止谷→松手收刀<br/>
        /// 角度/深度/轴向缩放全部从投影几何源导出
        /// </summary>
        private void UpdateBladePose() {
            bladeInBurst = false;
            bladeSpeedFade *= 0.55f;

            //硬让位期间藏刀
            if (yielding) {
                bladeOpacity = 0f;
                bladePoseInitialized = false;
                return;
            }

            //签名拍软保留藏刀(有活斩缝则不受影响);仅剩尸体斩缝时同样藏
            if (!AnyLiveRift() && OniBladeOccupancy.BladeReserved(Owner)) {
                bladeOpacity = 0f;
                bladePoseInitialized = false;
                return;
            }

            float targetRotation;
            float targetDepth;
            float targetStretch = 1f;
            float leanTarget = 0f;
            float leanRate = 0.25f;
            var stretchArm = Player.CompositeArmStretchAmount.Full;
            bladeOpacity = 1f;

            if (!firstBeatFired) {
                //A 首次起手,反拉蓄势沉入身后;有交接角则顺势拉入
                float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : curAim;
                float cos = MathF.Cos(aim);
                int facing = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1 : -1);
                bladeFacing = facing;
                RiftDef first = BuildBeatDef(0, aim, facing, sizeMul);
                bladeEdgeFlip = EdgeFlipOf(in first, aim, facing);
                float startRot = BladePathRotation(in first, aim, facing, BladePathStart);
                float sweepSign = PathSweepSign(in first, aim, facing);
                int windFrames = FirstWindupFrames;
                float windT = OSR.EaseOutCubic(MathHelper.Clamp(firstWindupTicks / (float)Math.Max(windFrames, 1), 0f, 1f));
                targetRotation = hasHandoff
                    ? OniBladePose.LerpAngle(handoffRot, startRot - sweepSign * 0.55f, windT)
                    : startRot - sweepSign * 0.55f * windT;
                targetDepth = MathHelper.Lerp(0.15f, -0.60f, windT);
                targetStretch = 0.86f;
                stretchArm = Player.CompositeArmStretchAmount.ThreeQuarters;
                //首拍起手不前倾,体态留给后两重拍
            }
            else if (actives.Count == 0) {
                if (scheduling && hasHandoff && timer < nextBeatTime) {
                    //B0 交接重启微前摇
                    float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : curAim;
                    float cos = MathF.Cos(aim);
                    int facing = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1 : -1);
                    bladeFacing = facing;
                    RiftDef next = BuildBeatDef(comboIndex, aim, facing, sizeMul);
                    bladeEdgeFlip = EdgeFlipOf(in next, aim, facing);
                    float startRot = BladePathRotation(in next, aim, facing, BladePathStart);
                    float windT = OSR.EaseOutCubic(
                        1f - (nextBeatTime - timer) / (float)RestartWindupFrames);
                    targetRotation = OniBladePose.LerpAngle(handoffRot, startRot, windT);
                    targetDepth = MathHelper.Lerp(-0.20f, -0.45f, windT);
                    targetStretch = 0.88f;
                    stretchArm = Player.CompositeArmStretchAmount.ThreeQuarters;
                    //仅即将进入后两重拍时微前倾蓄力
                    float nextLean = BeatLeanAmp(comboIndex);
                    if (nextLean > 0f) {
                        leanTarget = -facing * nextLean * 0.4f;
                        leanRate = 0.30f;
                    }
                }
                else {
                    //收势完藏刀等待重启
                    bladeOpacity = 0f;
                    bladePoseInitialized = false;
                    impactRecoil = 0f;
                    bladeScalePulse = 0f;
                    return;
                }
            }
            else {
                ActiveRift a = actives[^1];
                int lt = Math.Max(0, timer - a.Birth);
                int rip = a.Def.GatherFrames;
                bladeFacing = a.Facing;
                bladeEdgeFlip = EdgeFlipOf(in a.Def, a.Aim, a.Facing);
                float leanAmp = BeatLeanAmp(a.Beat);

                if (lt < rip) {
                    //B1 收势,自上一停驻位反拉上膛;缝侧只有应力线,刀全藏行程
                    float gT = OSR.EaseOutCubic((lt + 1) / (float)(rip + 1));
                    float startRot = BladePathRotation(in a.Def, a.Aim, a.Facing, BladePathStart);
                    float sweepSign = PathSweepSign(in a.Def, a.Aim, a.Facing);
                    float pull = a.Beat >= 3 ? 0.62f : 0.38f;
                    float chamberRot = a.HasGatherFrom ? a.GatherFromRot : startRot - sweepSign * pull * 0.5f;
                    targetRotation = LerpAngle(chamberRot, startRot - sweepSign * pull, gT);
                    targetDepth = MathHelper.Lerp(a.HasGatherFrom ? a.GatherFromDepth : 0f, -0.55f, gT);
                    targetStretch = MathHelper.Lerp(0.94f, 0.84f, gT);
                    stretchArm = Player.CompositeArmStretchAmount.ThreeQuarters;
                    leanTarget = -a.Facing * leanAmp * 0.9f;
                    leanRate = 0.35f;
                }
                else if (lt <= rip + 1) {
                    //B2 撕开跨越,2帧甩过整条缝,行程交给条带;深度走路径真实z
                    bladeInBurst = true;
                    float edgeU = lt == rip ? 0.60f : BladePathEnd;
                    targetRotation = BladePathRotation(in a.Def, a.Aim, a.Facing, edgeU);
                    targetDepth = BladePathDepth(in a.Def, a.Aim, edgeU);
                    targetStretch = StretchOf(in a.Def, edgeU, burst: true);
                    leanTarget = a.Facing * leanAmp * 1.35f;
                    leanRate = 0.80f;
                }
                else {
                    float endRot = BladePathRotation(in a.Def, a.Aim, a.Facing, BladePathEnd);
                    float sweepSign = PathSweepSign(in a.Def, a.Aim, a.Facing);

                    if (scheduling && timer < nextBeatTime) {
                        //C 停驻,2帧小回坐落定后真正静止(只留呼吸颤),换向交给下一拍收势
                        float settle = OSR.EaseOutCubic((lt - rip - 1) / 2f);
                        targetRotation = endRot + sweepSign * 0.09f * (1f - settle)
                            + MathF.Sin(timer * 0.9f) * 0.011f;
                        //停驻深度收进±0.60,持刀永不发矮
                        targetDepth = MathHelper.Clamp(BladePathDepth(in a.Def, a.Aim, BladePathEnd), -0.60f, 0.60f);
                        targetStretch = StretchOf(in a.Def, BladePathEnd, burst: false);
                        leanTarget = a.Facing * leanAmp * 0.30f;
                        leanRate = 0.22f;
                    }
                    else if (!scheduling) {
                        //D 松手收势,短过冲→收刀回背→淡出
                        float recoverT = MathHelper.Clamp((lt - rip - 1)
                            / (float)BladeReleaseRecoveryFrames, 0f, 1f);
                        float overshoot = MathF.Sin(MathHelper.Clamp(recoverT / 0.40f, 0f, 1f) * MathF.PI) * 0.16f;
                        float guardRotation = a.Aim - a.Facing * 1.05f;
                        targetRotation = LerpAngle(endRot + sweepSign * overshoot, guardRotation
                            , OSR.SmoothStep01((recoverT - 0.18f) / 0.82f));
                        targetDepth = MathHelper.Lerp(0.60f, -0.90f, OSR.SmoothStep01(recoverT));
                        targetStretch = MathHelper.Lerp(
                            StretchOf(in a.Def, BladePathEnd, burst: false), 0.86f, OSR.SmoothStep01(recoverT));
                        bladeOpacity = 1f - OSR.SmoothStep01((recoverT - 0.68f) / 0.32f);
                        stretchArm = Player.CompositeArmStretchAmount.ThreeQuarters;
                    }
                    else {
                        //兜底(理论不可达),停在收笔端
                        targetRotation = endRot;
                        targetDepth = 0.3f;
                        targetStretch = 1f;
                    }
                }
            }

            bodyLean = MathHelper.Lerp(bodyLean, leanTarget, leanRate);
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

            bladeArmStretch = stretchArm;

            if (!bladePoseInitialized) {
                bladeRotation = targetRotation + recoilOffset;
                bladePrevRotation = bladeRotation;
                bladeDepth = targetDepth;
                bladeStretch = targetStretch;
                bladePoseInitialized = true;
                return;
            }

            bladePrevRotation = bladeRotation;
            bladeRotation = targetRotation + recoilOffset;
            //角速度包络,峰值期刀体隐去让条带读速度
            float absDelta = MathF.Abs(MathHelper.WrapAngle(bladeRotation - bladePrevRotation));
            bladeSpeedFade = MathF.Max(bladeSpeedFade, MathHelper.Clamp((absDelta - 0.32f) / 0.55f, 0f, 1f));
            float prevDepth = bladeDepth;
            bladeDepth = MathHelper.Lerp(bladeDepth, targetDepth, bladeInBurst ? 0.85f : 0.5f);
            bladeStretch = MathHelper.Lerp(bladeStretch, targetStretch, bladeInBurst ? 0.85f : 0.45f);

            PushRibbonSamples(prevDepth);
        }

        /// <summary>高速帧细分采样入条带,撕开跨越的行程由此可见</summary>
        private void PushRibbonSamples(float prevDepth) {
            if (Main.dedServ || bladeOpacity <= 0.05f) {
                return;
            }
            float delta = MathHelper.WrapAngle(bladeRotation - bladePrevRotation);
            float absDelta = MathF.Abs(delta);
            if (absDelta < 0.07f) {
                return;
            }
            float bladeLen = BladeWorldLength();
            int steps = Math.Min(bladeInBurst ? 6 : 3, (int)(absDelta / 0.10f) + 1);
            float strength = MathHelper.Clamp((absDelta - 0.05f) / 0.40f, 0f, 1f) * bladeOpacity;
            Vector2 hand = BladeHandPosition(bladeFacing);
            for (int i = 0; i < steps; i++) {
                float t = (i + 1) / (float)(steps + 1);
                float depth = MathHelper.Lerp(prevDepth, bladeDepth, t);
                ribbon.Push(hand, bladePrevRotation + delta * t, bladeLen, depth, strength);
            }
        }

        /// <summary>当前刀身世界长度(手心→刀尖,含透视缩放)</summary>
        private float BladeWorldLength() {
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 textureSize = blade.Size();
            Vector2 origin = new(textureSize.X * BladeHiltUV.X, textureSize.Y * BladeHiltUV.Y);
            Vector2 tip = new(textureSize.X * BladeTipUV.X, textureSize.Y * BladeTipUV.Y);
            return (tip - origin).Length() * BladeDrawScale * sizeMul * bladeStretch;
        }

        /// <summary>起手音,轻拍只留低量气口(主响在撕开帧),重击低鸣预告</summary>
        private void PlayBeatFireSound(int beat) {
            (float pitch, float volume) = beat switch {
                0 => (0.45f, 0.20f),
                1 => (0.55f, 0.18f),
                2 => (0.60f, 0.20f),
                3 => (-0.45f, 0.42f),
                _ => (-0.60f, 0.50f),
            };
            SoundEngine.PlaySound(CWRSound.KatanaSwing with { Pitch = pitch, Volume = volume, MaxInstances = 3 }, Projectile.Center);
        }

        /// <summary>斩缝时间轴事件,撕开帧脆响+缝缘火花/快斩轻确认/过期剔除</summary>
        private void UpdateBeatEvents() {
            for (int i = actives.Count - 1; i >= 0; i--) {
                ActiveRift a = actives[i];
                int lt = timer - a.Birth;
                if (lt >= a.Def.Life) {
                    actives.RemoveAt(i);
                    continue;
                }

                //撕开帧主响,收势静默后一声劈开
                if (!a.RipPlayed && lt == OSR.RipFrame(in a.Def)) {
                    a.RipPlayed = true;
                    (float pitch, float volume) = a.Beat switch {
                        0 => (0.24f, 0.72f),
                        1 => (0.40f, 0.66f),
                        2 => (0.55f, 0.74f),
                        3 => (0.78f, 0.70f),
                        _ => (0.60f, 0.90f),
                    };
                    SoundEngine.PlaySound(CWRSound.KatanaSwing with {
                        Pitch = pitch,
                        Volume = volume,
                        MaxInstances = 3
                    }, Projectile.Center);

                    if (!Main.dedServ) {
                        SpawnRipBurst(a);
                    }

                    //息合:卡在撕开脆响同一帧甩出弧剑气(从本拍缝腹甩离)
                    if (a.Beat == BeatCount - 1 && a.Profile.BreathWave
                        && Projectile.IsOwnedByLocalPlayer()) {
                        Vector2 tip = OSR.PointAt(in a.Def, CenterOf(a), 0.62f, lt);
                        OniMeiStrikes.FireBreathWave(Owner, tip, a.Aim, a.BaseWeaponDamage
                            , Projectile.knockBack, sizeMul, a.Def.Flip, Projectile.GetSource_FromAI());
                    }
                }

                if (!a.PingPlayed && a.Beat < PingTable.Length && lt >= OSR.CloseStart(in a.Def)) {
                    a.PingPlayed = true;
                    PingBeat(a);
                }
            }
        }

        /// <summary>撕开帧缝缘火花,沿缝取点火花沿切向溅出;终结拍额外魂火自缝内漂出</summary>
        private void SpawnRipBurst(ActiveRift a) {
            Vector2 center = CenterOf(a);
            int lt = timer - a.Birth;
            int sparkCount = a.Beat >= 3 ? 5 : 3;
            for (int k = 0; k < sparkCount; k++) {
                float uc = Main.rand.NextFloat(0.18f, 0.92f);
                Vector2 pos = OSR.PointAt(in a.Def, center, uc, lt);
                Vector2 tangent = (OSR.PointAt(in a.Def, center, MathHelper.Clamp(uc + 0.04f, 0f, 1f), lt) - pos)
                    .SafeNormalize(a.Aim.ToRotationVector2());
                Vector2 vel = tangent * Main.rand.NextFloat(5f, 12f) + Main.rand.NextVector2Circular(1.4f, 1.4f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                    , Main.rand.NextFloat(0.3f, 0.55f) * sizeMul)
                    ?.Configure(Main.rand.Next(9, 16), affectedByGravity: false);
            }
            if (a.Def.EmberAmt > 0.1f) {
                //鬼门大开,魂火(冷青幽光)自缝内缓漂而出
                for (int k = 0; k < 5; k++) {
                    float uc = Main.rand.NextFloat(0.25f, 0.85f);
                    Vector2 pos = OSR.PointAt(in a.Def, center, uc, lt);
                    Vector2 drift = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.6f, 1.8f)
                        + new Vector2(0f, -0.5f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, drift, new Color(150, 226, 228)
                        , Main.rand.NextFloat(0.22f, 0.38f) * sizeMul)
                        ?.Configure(Main.rand.Next(20, 32), affectedByGravity: false);
                }
            }
        }

        /// <summary>保持段完成轻确认(白闪/火花/音效),挥空也有</summary>
        private void PingBeat(ActiveRift a) {
            (float flash, int sparks, float pitch, bool hitFlash) = PingTable[a.Beat];
            int lt = timer - a.Birth;
            Vector2 pos = OSR.PointAt(in a.Def, CenterOf(a), 0.94f, lt);

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = pitch, Volume = 0.38f }, pos);

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

            ApplyBodyLean();

            //连段刀角上黑板,技能起手可继承
            OniBladeHandoff.Publish(Owner, bladeRotation, bladeFacing);
        }

        /// <summary>体态倾斜上身,收势后仰爆发前甩;坐骑/冲刺旋转让位,origin 钉脚底</summary>
        private void ApplyBodyLean() {
            CWRPlayer mp = Owner.CWR();
            if (Owner.mount.Active || (mp != null && mp.IsRotatingDuringDash)) {
                bodyLeanApplied = false;
                return;
            }
            Owner.fullRotation = bodyLean * Owner.gravDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.gravDir >= 0f ? Owner.height : 0f);
            bodyLeanApplied = true;
        }

        /// <summary>藏刀期体态回正,归零后交还 fullRotation</summary>
        private void ReleaseBodyLean() {
            if (!bodyLeanApplied) {
                bodyLean = 0f;
                return;
            }
            bodyLean *= 0.6f;
            if (MathF.Abs(bodyLean) < 0.012f) {
                bodyLean = 0f;
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
                return;
            }
            Owner.fullRotation = bodyLean * Owner.gravDir;
        }

        /// <summary>存活契约,排拍/斩缝续命;收势且余韵完 Kill,再按由 UpdateCombo 重启</summary>
        private void UpdateLifetime() {
            bool visualsAlive = actives.Count > 0
                || (lastImpactFrame >= 0 && timer - lastImpactFrame < AfterglowEnd);
            //疾走控身期保留休眠控制器,阻止按住左键被 autoReuse 重新起刀
            if (scheduling || visualsAlive || (flashStepInterrupted && yielding)) {
                Projectile.timeLeft = 30;
                return;
            }
            Projectile.Kill();
        }

        /// <summary>控制器死亡兜底,交还 fullRotation 防斜身残留</summary>
        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        /// <summary>每拍首次命中爆点全层,power 0..1 按拍位;材质按金属/血肉分流;轻拍也给小幅相机响应</summary>
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

            CrimsonRendHitVFX.SpawnImpactBurst(pos, aim.ToRotationVector2(), power, sizeMul, steel);

            //方向性相机位移,轻拍小幅重拍大幅(纯本地视觉)
            if (!Main.dedServ) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(pos
                    , aim.ToRotationVector2(), 1.0f + 5.5f * power, 5f, 8, -1f, FullName));
            }
        }

        /// <summary>屏幕包络,Bloom + 命中脉冲;排拍恒亮,收势随末斩缝余寿衰减</summary>
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

        //==================== 判定 ====================

        /// <summary>当前伤害窗内斩缝(同帧多窗取最新一拍)</summary>
        private ActiveRift FindDamagingRift() {
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
        /// <summary>辐条判定厚度(px),缝内侧刀身带宽(贴脸不落空)</summary>
        private const float SpokeThickness = 36f;

        /// <summary>贪婪判定,缝折线 + 辐条(玩家→缝,补贴脸) + 箱外扩 <see cref="GrazePad"/>;窗内整形已在,全程满采样</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(GrazePad, GrazePad);

            for (int i = 0; i < actives.Count; i++) {
                ActiveRift a = actives[i];
                int lt = timer - a.Birth;
                if (lt < a.Def.DamageStart || lt > a.Def.DamageEnd) {
                    continue;
                }
                Vector2 center = CenterOf(a);

                const int samples = 15;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                float cp = 0f;
                for (int k = 0; k < samples; k++) {
                    float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                    OSR.RiftBandSample band = OSR.SampleBand(in a.Def, center, uc, lt);
                    float thickWorld = MathF.Max(32f, band.Width);
                    if (hasPrev && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                        , prev, band.Center, thickWorld, ref cp)) {
                        return true;
                    }
                    if (k % 3 == 0 && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                        , Projectile.Center, band.Center, SpokeThickness, ref cp)) {
                        return true;
                    }
                    prev = band.Center;
                    hasPrev = true;
                }
            }
            return false;
        }

        /// <summary>割草断藤,沿斩缝折线+辐条扫切,与判定同几何源</summary>
        public override void CutTiles() {
            if (actives.Count == 0) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            for (int i = 0; i < actives.Count; i++) {
                ActiveRift a = actives[i];
                int lt = timer - a.Birth;
                if (lt < OSR.RipFrame(in a.Def) || lt > a.Def.DamageEnd + 2) {
                    continue;
                }
                Vector2 center = CenterOf(a);

                const int samples = 9;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                for (int k = 0; k < samples; k++) {
                    float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                    OSR.RiftBandSample band = OSR.SampleBand(in a.Def, center, uc, lt);
                    float width = MathF.Max(30f, band.Width * 0.8f);
                    if (hasPrev) {
                        Utils.PlotTileLine(prev, band.Center, width, DelegateMethods.CutTiles);
                    }
                    if (k % 2 == 0) {
                        //辐条,缝内侧贴脸割草
                        Utils.PlotTileLine(Projectile.Center, band.Center, SpokeThickness, DelegateMethods.CutTiles);
                    }
                    prev = band.Center;
                    hasPrev = true;
                }
            }
        }

        /// <summary>重击拍伤害加成,快斩×1 重斩×1.3 终结×1.6</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            ActiveRift a = FindDamagingRift();
            if (a != null && a.Beat >= 3) {
                modifiers.SourceDamage *= a.Beat == BeatCount - 1 ? 1.6f : 1.3f;
            }
            float offsetX = Projectile.To(target.Center).X;
            modifiers.HitDirectionOverride = MathF.Abs(offsetX) > 0.01f
                ? Math.Sign(offsetX)
                : (MathF.Cos(a?.Aim ?? curAim) >= 0f ? 1 : -1);
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
            if (Projectile.IsOwnedByLocalPlayer() && a != null) {
                OnikiriPlayer okp = Owner.GetModPlayer<OnikiriPlayer>();
                float meiMul = okp.BuildMeiHitMultiplier(target, in a.Profile,
                    a.ActionSerial, allowPlanted: a.Beat == BeatCount - 1,
                    allowIron: !a.ResourceGranted, zanshin: false,
                    armedConditionMul: a.ArmedConditionMul,
                    tideOnBeatSnapshot: a.TideOnBeat, combo: true);
                if (OniMeiCombat.TryGetExecuteBonus(in a.Profile, target, out float executeMul)) {
                    meiMul *= executeMul;
                }
                modifiers.FinalDamage *= OniMeiCombat.ClampConditionalDamage(
                    meiMul, in a.Profile, target);
                if (a.TideOnBeat) {
                    bladeScalePulse = Math.Max(bladeScalePulse, 0.07f);
                }
            }
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.75f;
            }
            //对双子魔眼造成1.25倍伤害
            if (target.type == NPCID.Spazmatism || target.type == NPCID.Retinazer) {
                modifiers.FinalDamage *= 1.25f;
            }
            //对塔纳托斯头造成2.85倍伤害
            if (target.type == CWRID.NPC_ThanatosHead) {
                modifiers.FinalDamage *= 2.85f;
            }
            //对星流双子造成1.66倍伤害
            if (target.type == CWRID.NPC_Apollo || target.type == CWRID.NPC_Artemis) {
                modifiers.FinalDamage *= 1.66f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            ActiveRift a = FindDamagingRift();

            //伤害逐敌结算,资源按拍结算;同拍后续目标仍进入处决命中记忆
            if (Projectile.IsOwnedByLocalPlayer()) {
                bool grantResources = a != null && !a.ResourceGranted;
                if (grantResources) {
                    a.ResourceGranted = true;
                }
                OnikiriPlayer okp = Owner.GetModPlayer<OnikiriPlayer>();
                OniMeiCombatProfile profile = a?.Profile ?? OniMeiCombatProfile.Identity;
                okp.OnComboHit(target, grantResources, in profile, a?.TideOnBeat == true);
                if (a != null) {
                    OniMeiCombat.OnExecuteStrikeHit(Owner, target, a.Aim,
                        ref a.ExecuteRefunded, in profile, a.ActionSerial);
                }
                if (grantResources && a != null) {
                    Tutorial.OnikiriTutorialEvents.FireComboBeatHit(a.Beat, target);
                }
                //雷切:雷暴天连第五拍都引得下雷来;晴天只有大招落
                if (a != null && a.Beat == BeatCount - 1 && profile.ThunderCall
                    && Main.raining && MathF.Abs(Main.windSpeedCurrent) >= 0.4f) {
                    okp.TryCallThunder(target, in profile, a.BaseWeaponDamage,
                        Projectile.knockBack, Projectile);
                }
                if (!target.active || target.life <= 0) {
                    okp.TryPetalPruneOnKill(target,
                        a?.BaseWeaponDamage ?? Projectile.damage, Projectile.knockBack,
                        Projectile, in profile);
                    OniMeiDeedEvents.NotifyKill(Owner, target, OniMeiDeedKillSource.Combo);
                }
            }

            //每拍首次命中爆点,强度按拍位递增
            if (a != null && !a.ImpactDone) {
                a.ImpactDone = true;
                TriggerImpactBurst(target.Center + VaultUtils.RandVr(0, target.width / 3f), (a.Beat + 1) / (float)BeatCount, a.Aim, a.Def.Flip, steel);

                //命中冻结整张画(斩缝/刀/体态/条带同帧),按拍位分级;回坐+尺寸脉冲其后衰减
                if (actives.Count > 0 && a == actives[^1]) {
                    impactHoldFrames = a.Beat >= 4 ? 3 : a.Beat == 3 ? 2 : 1;
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
        //  身后层,远半侧斩缝+身后刀身
        //  图元层,刀路条带+近半侧斩缝+命中爆点
        //  遮挡层,近景刀身本体

        /// <summary>实体刀精灵,护手钉 <see cref="bladeHandWorld"/>;朝左时垂直翻转并镜像支点,edgeFlip 再镜像一次=转腕翻刃</summary>
        private void DrawBladeSprite(SpriteBatch sb, float rotation, int facing, float scale, Color color
            , Vector2 posOffset = default, bool edgeFlip = false) {
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 textureSize = blade.Size();
            Vector2 origin = new(textureSize.X * BladeHiltUV.X, textureSize.Y * BladeHiltUV.Y);
            Vector2 textureTip = new(textureSize.X * BladeTipUV.X, textureSize.Y * BladeTipUV.Y);
            SpriteEffects bladeEffect = SpriteEffects.None;
            if (facing < 0 != edgeFlip) {
                bladeEffect = SpriteEffects.FlipVertically;
                origin.Y = textureSize.Y - origin.Y;
                textureTip.Y = textureSize.Y - textureTip.Y;
            }
            float textureAxis = (textureTip - origin).ToRotation();
            sb.Draw(blade, bladeHandWorld + posOffset - Main.screenPosition, null, color
                , rotation - textureAxis, origin, scale, bladeEffect, 0f);
        }

        /// <summary>身后层,远半侧斩缝+身后刀身</summary>
        void ICrimsonFarDrawable.DrawFarSlashes() {
            if (Main.dedServ) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (actives.Count > 0 && OSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                for (int i = 0; i < actives.Count; i++) {
                    ActiveRift a = actives[i];
                    int lt = timer - a.Birth;
                    if (lt < 0 || lt >= a.Def.Life || a.Def.FarDim <= 0f) {
                        continue;
                    }
                    OSR.DrawRift(device, fx, in a.Def, CenterOf(a), lt, -1f);
                }
                OSR.EndDraw(device, pb, pr, pd);
            }

            float farW = 1f - NearWeight(bladeDepth);
            bool bladeFar = bladeOpacity > 0.01f && farW > 0.02f;
            if (!bladeFar) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Color lightColor = Lighting.GetColor((int)(Owner.Center.X / 16f), (int)(Owner.Center.Y / 16f));
            float scale = BladeDrawScale * sizeMul * bladeStretch * (1f + bladeScalePulse);
            Color body = Color.Lerp(lightColor, Color.White, 0.12f)
                * (bladeOpacity * farW * DepthDim(bladeDepth) * (1f - 0.62f * bladeSpeedFade));
            DrawBladeSprite(sb, bladeRotation, bladeFacing, scale, body, default, bladeEdgeFlip);
            DrawEngrave(sb, scale, bladeOpacity * farW * DepthDim(bladeDepth)
                * (1f - 0.62f * bladeSpeedFade));
            sb.End();
        }

        /// <summary>遮挡层,近景刀身(阴影+主体),盖在图元斩缝之上</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch sb) {
            if (Main.dedServ || bladeOpacity <= 0.01f) {
                return;
            }
            float nearW = NearWeight(bladeDepth);
            if (nearW <= 0.02f) {
                return;
            }

            Color lightColor = Lighting.GetColor((int)(Owner.Center.X / 16f), (int)(Owner.Center.Y / 16f));
            float scale = BladeDrawScale * sizeMul * bladeStretch * (1f + bladeScalePulse);
            //爆发峰值刀体隐去,速度读感交给条带(swoosh 替刀)
            float speedThin = 1f - 0.62f * bladeSpeedFade;

            Color shadow = new Color(15, 3, 8, 190) * (bladeOpacity * 0.62f * nearW * speedThin);
            DrawBladeSprite(sb, bladeRotation, bladeFacing, scale * 1.018f, shadow
                , new Vector2(bladeFacing, 1f), bladeEdgeFlip);

            Color body = Color.Lerp(lightColor, Color.White, 0.24f) * (bladeOpacity * nearW * speedThin);
            DrawBladeSprite(sb, bladeRotation, bladeFacing, scale, body, default, bladeEdgeFlip);
            DrawEngrave(sb, scale, bladeOpacity * nearW * speedThin);
        }

        /// <summary>刀身铭刻层：只叠在刀体本身上；铭档取本拍已同步的动作快照</summary>
        private void DrawEngrave(SpriteBatch sb, float scale, float alpha) {
            OniMeiEngraveState state = OniMeiBladeEngrave.Resolve(Projectile, Owner);
            if (!state.AnyEngraved) {
                return;
            }
            //狮势链归控制器所有，各端自行推进，故远端刀也看得见蓄势
            state.LionChain = meiLionChain / (float)BeatCount;
            OniBladeProfile.BladeXform xform = OniBladeProfile.BuildXform(bladeHandWorld,
                bladeRotation, bladeFacing, scale, bladeEdgeFlip, BladeHiltUV, BladeTipUV);
            OniMeiBladeEngrave.Draw(sb, in xform, in state, alpha);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || actives.Count == 0 && lastImpactFrame < 0 && !ribbon.AnyAlive()) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (OSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                //刀路条带先画,斩缝(伤口)盖在挥动痕之上
                ribbon.Draw(device, fx, Projectile.whoAmI * 0.137f);
                for (int i = 0; i < actives.Count; i++) {
                    ActiveRift a = actives[i];
                    int lt = timer - a.Birth;
                    if (lt < 0 || lt >= a.Def.Life) {
                        continue;
                    }
                    //FarDim>0 只画近半侧,远半侧已在身后层
                    OSR.DrawRift(device, fx, in a.Def, CenterOf(a), lt, a.Def.FarDim > 0f ? 1f : 0f);
                }
                OSR.EndDraw(device, pb, pr, pd);
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
                float oS = MathHelper.Lerp(0.9f, 0.18f, OSR.EaseOutCubic(t)) * sizeMul;
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
