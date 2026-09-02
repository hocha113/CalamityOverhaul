using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
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
using CSW = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSweepRenderer;
using SweepDef = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSweepRenderer.SweepDef;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>
    /// 绯红裂空斩·扫掠版,按住左键滚动五段连段控制器(与 <see cref="CrimsonRendSlash"/> 同一套连段/铭刻/让位骨架,
    /// 表现层换成刀身扫过的体积)<br/>
    /// 每拍:拉背蓄势(刀沉身后,后仰,前手收)→重拍死寂→爆发 2~4 帧刀铺满弧、落位帧闪+踏步前甩
    /// →体 6~10 帧向刀收缩→只剩 2px 刃痕冷却成墨线蚀退<br/>
    /// 刀尖/刀光/碰撞同一投影源;命中冻结整张画(Birth 顺延)<br/>
    /// ai[0]=初始瞄准角(弧度) ai[2]=尺寸倍率
    /// </summary>
    internal class CrimsonSweepSlash : BaseHeldProj, IPrimitiveDrawable, ICrimsonFarDrawable, IOverlayDrawable
        , IOniComboController
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override bool CanFire => !firstBeatFired
            || scheduling && hasHandoff && timer < nextBeatTime;

        //==== 节拍常量 ====
        private const int BeatCount = 5;
        private const int BurstFadeFrames = 16;
        private const int AfterglowEnd = 46;
        /// <summary>引擎级本地免疫只兜同帧重入;一拍一目标一次伤害由 <see cref="ActiveSlash.HitTargets"/> 裁定</summary>
        private const int EngineHitCooldown = 2;
        private const int BladeReleaseRecoveryFrames = 12;
        private const int FlashStepInterruptFadeFrames = 6;
        private const int FirstWindupFrames = 2;
        /// <summary>停手超过该帧后再按从第一拍重启,短停续接拍序</summary>
        private const int ComboResetFrames = 30;
        private const int RestartWindupFrames = 3;
        private const int PressBufferFrames = 24;
        private const int YieldFadeFrames = 8;
        /// <summary>刀精灵护手/刀尖 UV,护手作手心支点</summary>
        private static Vector2 BladeHiltUV => new(0.1f, 1f);
        private static Vector2 BladeTipUV => new(0.73f, 0.01f);
        /// <summary>各拍到下一拍基准间隔(攻速 1)</summary>
        private static readonly int[] BeatGap = [10, 10, 13, 15, 24];
        /// <summary>命中整画冻结帧数(按拍)</summary>
        private static readonly int[] HitStopFrames = [2, 2, 3, 4, 5];
        /// <summary>落位帧相机沿切向位移(px,按拍)</summary>
        private static readonly float[] LandPunch = [1.5f, 1.5f, 2f, 4f, 6f];
        /// <summary>落位帧刀尖甩出的墨滴数(余韵留墨)</summary>
        private static readonly int[] LandInkDrops = [1, 1, 2, 3, 4];
        /// <summary>落位轻确认音高(前四拍)</summary>
        private static readonly float[] PingPitch = [0.50f, 0.65f, 0.75f, 0.85f];

        /// <summary>已挥出的一拍,出生帧与瞄准角开火瞬间冻结</summary>
        private sealed class ActiveSlash
        {
            public SweepDef Def;
            public int Birth;        //绝对 timer 帧(整画冻结时顺延)
            public int Beat;
            public float Aim;
            public int Facing;
            public bool ImpactDone;
            public bool ResourceGranted;
            public bool SnapPlayed;  //爆发主响已播
            public bool LandPlayed;  //落位帧演出已播
            public bool StepDone;    //踏步已给
            public bool HopDone;     //蓄势末小跳已给
            public Vector2? FrozenCenter;
            public float GatherFromRot;
            public float GatherFromDepth;
            public bool HasGatherFrom;
            public OniMeiCombatProfile Profile;
            public uint ActionSerial;
            public int BaseWeaponDamage;
            public float ArmedConditionMul;
            public bool TideOnBeat;
            public bool ExecuteRefunded;
            public readonly List<int> HitTargets = [];
        }

        private readonly List<ActiveSlash> actives = new(8);
        private int timer;
        private int comboIndex;
        private int nextBeatTime;
        private int lastBeatFire;
        private bool scheduling;
        private bool firstBeatFired;
        private bool flashStepInterrupted;
        private int firstWindupTicks;
        private bool prevDownLeft;
        private int pressBuffer;
        private float handoffRot;
        private bool hasHandoff;
        private float sizeMul = 1f;
        private float curAim;
        private OniMeiCombatProfile meiProfile = OniMeiCombatProfile.Identity;
        private int meiLionChain;
        private int lastImpactFrame = -999;
        private Vector2 lastImpactPos;
        private float lastImpactAim;
        private float lastImpactFlip = 1f;
        private bool lastImpactSteel;
        private Rectangle[] speedLineRects;
        private float[] speedLineOffsets;
        private bool yielding;
        //==== 实体刀姿态(纯视觉,确定性,不上网络) ====
        private float bladeRotation;
        private float bladePrevRotation;
        private float bladeDepth;          //-1 身后 .. +1 身前
        private float bladeLength;         //px 手到刀尖(投影后,含前缩)
        private float bladeOpacity;
        private int bladeFacing = 1;
        private bool bladeEdgeFlip;
        private bool bladePoseInitialized;
        private bool bladeInBurst;
        private float bladeBurstAlpha = 1f;
        private Player.CompositeArmStretchAmount bladeArmStretch = Player.CompositeArmStretchAmount.Full;
        private Vector2 bladeHandWorld;
        //==== 爆发残影(最多两张:上一帧位与中间位) ====
        private float ghostRot;
        private float ghostMidRot;
        private float ghostLength;
        private float ghostStrength;
        private int ghostLife;
        private int ghostFacing = 1;
        private bool ghostEdgeFlip;
        private const int GhostLifeFrames = 4;
        //==== 命中反馈 ====
        private int impactHoldFrames;
        private float impactRecoil;
        private float impactRecoilSign = 1f;
        private float bladeScalePulse;
        //==== 体态 ====
        private float bodyLean;
        private bool bodyLeanApplied;

        /// <summary>贴图上护手→刀尖的像素长度,刀精灵缩放基准</summary>
        private static float TexBladeLength {
            get {
                Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
                Vector2 size = blade.Size();
                return (new Vector2(size.X * BladeTipUV.X, size.Y * BladeTipUV.Y)
                    - new Vector2(size.X * BladeHiltUV.X, size.Y * BladeHiltUV.Y)).Length();
            }
        }

        /// <summary>刃身环境光锚点,最新刀光刃头;真实命中用 <see cref="lastImpactPos"/></summary>
        private Vector2 AmbientAnchor {
            get {
                if (actives.Count > 0) {
                    ActiveSlash a = actives[^1];
                    float p = CSW.Anim(in a.Def, Math.Max(0, timer - a.Birth)).HeadP;
                    return CSW.OuterAt(in a.Def, CenterOf(a), MathF.Max(p, 0.3f), out _);
                }
                return Projectile.Center + curAim.ToRotationVector2() * 160f * sizeMul;
            }
        }

        /// <summary>持有者客户端调用(<c>myPlayer</c>),tML 自动同步</summary>
        public static Projectile Fire(Player player, Vector2 origin, Vector2 aim, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_CrimsonRendSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX).ToRotation();
            Projectile projectile = Projectile.NewProjectileDirect(source, origin, Vector2.Zero
                , ModContent.ProjectileType<CrimsonSweepSlash>(), damage, knockback, player.whoAmI
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
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = EngineHitCooldown;
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

        private SweepDef BuildBeatDef(int beat, float aim, int facing)
            => CSW.BuildBeat(beat, aim, facing, sizeMul, (beat * 0.191f + timer * 0.037f) % 1f);

        /// <summary>刀光轴心,平时随玩家,硬让位后取冻结点</summary>
        private Vector2 CenterOf(ActiveSlash a) => a.FrozenCenter ?? Projectile.Center;

        public bool ClaimsBlade => scheduling || bladeOpacity > 0.03f || AnyLiveSlash();

        private bool AnyLiveSlash() {
            for (int i = 0; i < actives.Count; i++) {
                if (actives[i].FrozenCenter == null) {
                    return true;
                }
            }
            return false;
        }

        public bool InCommittedBeats {
            get {
                for (int i = 0; i < actives.Count; i++) {
                    ActiveSlash a = actives[i];
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

        public bool TryGetBladePose(out float rotation, out int facing) {
            rotation = bladeRotation;
            facing = bladeFacing;
            return bladePoseInitialized && bladeOpacity > 0.05f;
        }

        /// <summary>持续左键→疾走的刀权交接,返回当前刀角;旧刀光冻结退场并关闭伤害</summary>
        public bool BeginFlashStepInterrupt(Vector2 dashAim, out float startRotation) {
            startRotation = bladeRotation;
            bool attackActive = scheduling || firstWindupTicks > 0
                || bladePoseInitialized || AnyLiveSlash();
            if (flashStepInterrupted || !attackActive) {
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
            FreezeSlashVisuals(FlashStepInterruptFadeFrames);
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
            Vector2 center;
            float aim;
            OniMeiCombatProfile profile = meiProfile;
            int baseWeaponDamage = OniMeiActionContext.Get(Projectile)?.BaseWeaponDamage
                ?? Projectile.damage;
            if (actives.Count > 0) {
                ActiveSlash a = actives[^1];
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

        public void ConsumeZanshinInput() {
            pressBuffer = 0;
            scheduling = false;
            prevDownLeft = DownLeft;
            meiLionChain = 0;
        }

        /// <summary>刀柄方向支点(解算朝向);绘制锚用 <see cref="bladeHandWorld"/></summary>
        private Vector2 BladeHandPosition(int facing) => Owner.GetPlayerStabilityCenter()
            + new Vector2(facing * 3f, -5f * Owner.gravDir);

        /// <summary>行程 p、刀半径 radius 处实体刀朝向/归一深度/投影刀长(手→刀尖),刀尖钉在投影上</summary>
        private float BladeRotationAt(in SweepDef d, float p, float radius, out float depth, out float length) {
            Vector2 tip = CSW.BladeTipAt(in d, Projectile.Center, p, radius, out float z);
            Vector2 fromHand = tip - BladeHandPosition(d.Facing);
            depth = CSW.DepthNorm(in d, radius, z);
            length = MathF.Max(fromHand.Length(), 24f);
            return fromHand.LengthSquared() > 1f ? fromHand.ToRotation() : d.Aim;
        }

        /// <summary>屏面角速度符号,+1 顺时针</summary>
        private static float SweepSign(in SweepDef d) => MathF.Sign(d.Span * d.Facing) >= 0f ? 1f : -1f;

        private static float LerpAngle(float from, float to, float amount)
            => from + MathHelper.WrapAngle(to - from) * MathHelper.Clamp(amount, 0f, 1f);

        /// <summary>深度→近景权重,±0.22 交叉淡化</summary>
        private static float NearWeight(float depth) => CSW.SmoothStep01((depth + 0.22f) / 0.44f);

        /// <summary>深度→亮度,身后压暗至~0.72</summary>
        private static float DepthDim(float depth) => MathHelper.Lerp(1f, 0.72f, MathHelper.Clamp(-depth, 0f, 1f));

        //==================== 连段推进 ====================

        public override void AI() {
            Projectile.Center = Owner.Center;
            timer++;

            //命中整画冻结:刀光时间轴锚点顺延,刀/体态同帧冻住,输入与判定照常
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
                if (!Main.dedServ) {
                    SpawnSweepFX();
                }
            }

            UpdatePose();
            if (bladeOpacity <= 0.01f) {
                ReleaseBodyLean();
            }

            Lighting.AddLight(AmbientAnchor, new Vector3(1.0f, 0.25f, 0.18f));
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.12f, 0.10f));
            if (bladeOpacity > 0.01f) {
                Vector2 bladeLight = BladeHandPosition(bladeFacing)
                    + bladeRotation.ToRotationVector2() * bladeLength * 0.75f;
                Lighting.AddLight(bladeLight, new Vector3(0.72f, 0.10f, 0.06f) * bladeOpacity);
            }

            PushScreenState();
            UpdateLifetime();
        }

        /// <summary>把在场刀光冻结在世界坐标并压缩到指定余寿</summary>
        private void FreezeSlashVisuals(int maxFadeFrames) {
            foreach (ActiveSlash a in actives) {
                a.FrozenCenter ??= Projectile.Center;
                int remain = a.Def.Life - (timer - a.Birth);
                if (remain > maxFadeFrames) {
                    a.Birth = timer - (a.Def.Life - maxFadeFrames);
                }
            }
        }

        /// <summary>刀权仲裁,主人有硬占刀权技能时停排/冻结刀光速褪/实体刀交权;狮势链随让位归零</summary>
        private void UpdateYield() {
            bool hard = OniBladeOccupancy.AnyHardOccupant(Owner);
            if (hard && !yielding) {
                scheduling = false;
                bladeOpacity = 0f;
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
            //化樱接管期间连段全冻,轻点缓冲一并吞掉,樱流结束仍按着则正常重启排拍
            if (OniSakuraFlights.OniSakuraFlight.ControlsOwner(Owner.whoAmI)) {
                prevDownLeft = DownLeft;
                scheduling = false;
                pressBuffer = 0;
                return;
            }
            bool canContinue = !Owner.noItems && !Owner.CCed
                && Item.type == ModContent.ItemType<OnikiriItem>()
                && Owner.ownedProjectileCounts[ModContent.ProjectileType<OniDismembers.OniSeverStrike>()] == 0;
            bool justPressed = DownLeft && !prevDownLeft;
            prevDownLeft = DownLeft;
            if (pressBuffer > 0) {
                pressBuffer--;
            }
            if (justPressed) {
                pressBuffer = Math.Max(pressBuffer, PressBufferFrames);
            }
            bool holding = (DownLeft || pressBuffer > 0) && canContinue && !yielding;

            if (!firstBeatFired) {
                if (yielding || OniBladeOccupancy.BladeReserved(Owner)) {
                    return;
                }
                if (++firstWindupTicks == 1) {
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
                if (justPressed && Projectile.IsOwnedByLocalPlayer()
                    && Owner.GetModPlayer<OnikiriPlayer>().TryExecutionAnnihilate(Item, edgeVerified: true)) {
                    pressBuffer = 0;
                    return;
                }
                if (justPressed && Projectile.IsOwnedByLocalPlayer()
                    && Owner.GetModPlayer<OnikiriPlayer>().TryClickDismember(Item)) {
                    return;
                }
                if (justPressed && Projectile.IsOwnedByLocalPlayer()
                    && Owner.GetModPlayer<OnikiriPlayer>().TryZanshinStrike(Item, edgeVerified: true)) {
                    pressBuffer = 0;
                    return;
                }
                if (OniBladeOccupancy.BladeReserved(Owner)) {
                    return;
                }
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.GetModPlayer<OnikiriPlayer>().CancelExecutionIntent(settleFollowup: true);
                }
                scheduling = true;
                bool comboExpired = timer - lastBeatFire > ComboResetFrames;
                if (comboExpired) {
                    comboIndex = 0;
                }
                hasHandoff = OniBladeHandoff.TryPeek(Owner, out handoffRot, out _);
                int earliest = timer + (hasHandoff ? RestartWindupFrames : 0);
                nextBeatTime = comboExpired ? earliest : Math.Max(nextBeatTime, earliest);
                pressBuffer = Math.Max(pressBuffer, nextBeatTime - timer + 1);
            }
            if (timer >= nextBeatTime) {
                FireBeat();
            }
        }

        /// <summary>开火一拍,冻结方向、铭刻条件与动作序号;三槽和基础伤害沿用控制器出生快照</summary>
        private void FireBeat() {
            hasHandoff = false;
            pressBuffer = 0;
            float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : Projectile.ai[0];
            curAim = aim;
            float cos = MathF.Cos(aim);
            int facing = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1 : -1);
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

            //鵺切:离地够高的第五拍整拍换成扑击
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

            ActiveSlash active = new() {
                Def = BuildBeatDef(beat, aim, facing),
                Birth = timer,
                Beat = beat,
                Aim = aim,
                Facing = facing,
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

            comboIndex = (comboIndex + 1) % BeatCount;
            lastBeatFire = timer;
            nextBeatTime = timer + Math.Max(4
                , (int)MathF.Round(BeatGap[beat] * beatSpeedFactor * meiProfile.ComboGapMul));

            UpdateMeiOnBeatFired(active);
        }

        /// <summary>铭刻逐拍推进:狮势链全客户端按拍序确定性蓄势,副斩仅 owner 生成;龙火窗口态在 owner 的 ModPlayer 上</summary>
        private void UpdateMeiOnBeatFired(ActiveSlash action) {
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
                    OniMeiStrikes.SpawnDragonfireBeatFlame(Owner, aim, sizeMul);
                }
            }
        }

        /// <summary>
        /// 实体刀姿态时间轴:拉背蓄势(刀沉身后)→死寂→爆发沿投影跟刀尖→过冲回坐→停驻→松手收刀<br/>
        /// 体态全拍生效:蓄势后仰+前手收,爆发一帧甩到前倾+前手伸满+踏步
        /// </summary>
        private void UpdateBladePose() {
            if (ghostLife > 0) {
                ghostLife--;
            }
            bladeInBurst = false;
            bladeBurstAlpha = 1f;

            if (yielding) {
                bladeOpacity = 0f;
                bladePoseInitialized = false;
                return;
            }
            if (!AnyLiveSlash() && OniBladeOccupancy.BladeReserved(Owner)) {
                bladeOpacity = 0f;
                bladePoseInitialized = false;
                return;
            }

            float targetRotation;
            float targetDepth;
            float targetLength;
            float leanTarget = 0f;
            float leanRate = 0.25f;
            var stretch = Player.CompositeArmStretchAmount.Full;
            bladeOpacity = 1f;

            if (!firstBeatFired) {
                //A 首次起手:反拉蓄势沉入身后;有交接角则顺势拉入
                float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : curAim;
                float cos = MathF.Cos(aim);
                int facing = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1 : -1);
                bladeFacing = facing;
                SweepDef first = BuildBeatDef(0, aim, facing);
                bladeEdgeFlip = first.EdgeFlip;
                float windT = CSW.EaseOutCubic(firstWindupTicks / (float)Math.Max(FirstWindupFrames, 1));
                float restR = first.RestLen * 0.94f;
                float windRot = BladeRotationAt(in first, first.WindupP, restR, out float windDepth, out float windLen);
                float startRot = BladeRotationAt(in first, 0f, restR, out _, out _);
                targetRotation = LerpAngle(hasHandoff ? handoffRot : startRot, windRot, windT);
                targetDepth = MathHelper.Lerp(0.15f, windDepth, windT);
                targetLength = windLen;
                stretch = Player.CompositeArmStretchAmount.Quarter;
                leanTarget = -facing * first.LeanBack * 0.6f;
                leanRate = 0.30f;
            }
            else if (actives.Count == 0) {
                if (scheduling && hasHandoff && timer < nextBeatTime) {
                    //B0 交接重启微前摇,先拉进半程拉背位
                    float aim = ToMouse.LengthSquared() > 1f ? ToMouseA : curAim;
                    float cos = MathF.Cos(aim);
                    int facing = MathF.Abs(cos) < 0.05f ? Owner.direction : (cos > 0f ? 1 : -1);
                    bladeFacing = facing;
                    SweepDef next = BuildBeatDef(comboIndex, aim, facing);
                    bladeEdgeFlip = next.EdgeFlip;
                    float windT = CSW.EaseOutCubic(1f - (nextBeatTime - timer) / (float)RestartWindupFrames);
                    float halfRot = BladeRotationAt(in next, next.WindupP * 0.5f, next.RestLen * 0.94f
                        , out float halfDepth, out float halfLen);
                    targetRotation = LerpAngle(handoffRot, halfRot, windT);
                    targetDepth = MathHelper.Lerp(-0.20f, halfDepth, windT);
                    targetLength = halfLen;
                    stretch = Player.CompositeArmStretchAmount.Quarter;
                    leanTarget = -facing * next.LeanBack * 0.4f;
                    leanRate = 0.30f;
                }
                else {
                    bladeOpacity = 0f;
                    bladePoseInitialized = false;
                    impactRecoil = 0f;
                    bladeScalePulse = 0f;
                    return;
                }
            }
            else {
                ActiveSlash a = actives[^1];
                int lt = Math.Max(0, timer - a.Birth);
                ref SweepDef d = ref a.Def;
                bladeFacing = a.Facing;
                bladeEdgeFlip = d.EdgeFlip;
                float sweepSign = SweepSign(in d);

                float radius = CSW.BladeRadius(in d, lt);
                if (lt < d.GatherFrames) {
                    //B1 蓄势:自上一停驻位反拉进拉背位,刀沉身后,前手收,后仰
                    float gT = CSW.EaseOutCubic((lt + 1) / (float)(d.GatherFrames + 1));
                    float windRot = BladeRotationAt(in d, d.WindupP, radius, out float windDepth, out float windLen);
                    float fromRot = a.HasGatherFrom
                        ? a.GatherFromRot
                        : BladeRotationAt(in d, d.WindupP * 0.5f, radius, out _, out _);
                    float fromDepth = a.HasGatherFrom ? a.GatherFromDepth : 0f;
                    targetRotation = LerpAngle(fromRot, windRot, gT);
                    targetDepth = MathHelper.Lerp(fromDepth, windDepth, gT);
                    targetLength = windLen;
                    stretch = Player.CompositeArmStretchAmount.Quarter;
                    leanTarget = -a.Facing * d.LeanBack;
                    leanRate = 0.35f;
                    //终结拍蓄势末小跳,人先起再劈(owner 权威)
                    if (d.HopVy != 0f && lt == d.GatherFrames - 1 && !a.HopDone) {
                        a.HopDone = true;
                        if (Projectile.IsOwnedByLocalPlayer() && Owner.velocity.Y == 0f && !Owner.mount.Active) {
                            Owner.velocity.Y = d.HopVy * Owner.gravDir;
                        }
                    }
                }
                else if (lt < d.SweepStart) {
                    //B1' 死寂谷:蓄满的静止里只有指尖发颤,静默买爆发
                    float windRot = BladeRotationAt(in d, d.WindupP, radius, out float windDepth, out float windLen);
                    targetRotation = windRot + sweepSign * 0.016f * MathF.Sin(timer * 1.9f);
                    targetDepth = windDepth;
                    targetLength = windLen;
                    stretch = Player.CompositeArmStretchAmount.Quarter;
                    leanTarget = -a.Facing * d.LeanBack;
                    leanRate = 0.5f;
                }
                else if (lt < d.CollapseStart) {
                    //B2 爆发:刀拉长到剃刀线、刀尖钉在投影上跟行程曲线;一帧甩到前倾、前手伸满、踏步
                    bladeInBurst = true;
                    float p = CSW.BladeProgress(in d, lt);
                    targetRotation = BladeRotationAt(in d, p, radius, out targetDepth, out targetLength);
                    stretch = Player.CompositeArmStretchAmount.Full;
                    leanTarget = a.Facing * d.LeanFwd;
                    leanRate = 0.9f;
                    //刀就是刀光的前缘,爆发帧要看得见;落位帧全实
                    bladeBurstAlpha = lt == d.LandFrame ? 1f : 0.7f;
                    if (lt == d.SweepStart && !a.StepDone) {
                        a.StepDone = true;
                        if (Projectile.IsOwnedByLocalPlayer() && Owner.velocity.Y == 0f && !Owner.mount.Active
                            && MathF.Abs(Owner.velocity.X) < 4f) {
                            Owner.velocity.X += a.Facing * d.StepPx;
                        }
                    }
                }
                else {
                    float p = CSW.BladeProgress(in d, lt);
                    float endRot = BladeRotationAt(in d, p, radius, out float endDepth, out float endLen);

                    if (scheduling && timer < nextBeatTime) {
                        //C 停驻:过冲回坐后真正静止(只留呼吸颤),换向交给下一拍蓄势
                        targetRotation = endRot + MathF.Sin(timer * 0.9f) * 0.011f;
                        targetDepth = endDepth;
                        targetLength = endLen;
                        stretch = Player.CompositeArmStretchAmount.Full;
                        leanTarget = a.Facing * d.LeanFwd * 0.35f;
                        leanRate = 0.22f;
                    }
                    else if (!scheduling) {
                        //D 松手收势:短过冲→收刀回背→淡出
                        float recoverT = MathHelper.Clamp((lt - d.CollapseStart)
                            / (float)BladeReleaseRecoveryFrames, 0f, 1f);
                        float overshoot = MathF.Sin(MathHelper.Clamp(recoverT / 0.40f, 0f, 1f) * MathF.PI) * 0.16f;
                        float guardRotation = a.Aim - a.Facing * 1.05f;
                        targetRotation = LerpAngle(endRot + sweepSign * overshoot, guardRotation
                            , CSW.SmoothStep01((recoverT - 0.18f) / 0.82f));
                        targetDepth = MathHelper.Lerp(endDepth, -0.90f, CSW.SmoothStep01(recoverT));
                        targetLength = MathHelper.Lerp(endLen, d.RestLen * 0.9f, CSW.SmoothStep01(recoverT));
                        bladeOpacity = 1f - CSW.SmoothStep01((recoverT - 0.68f) / 0.32f);
                        stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
                    }
                    else {
                        targetRotation = endRot;
                        targetDepth = endDepth;
                        targetLength = endLen;
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
            bladeArmStretch = stretch;

            if (!bladePoseInitialized) {
                bladeRotation = targetRotation + recoilOffset;
                bladePrevRotation = bladeRotation;
                bladeDepth = targetDepth;
                bladeLength = targetLength;
                bladePoseInitialized = true;
                return;
            }

            bladePrevRotation = bladeRotation;
            bladeRotation = targetRotation + recoilOffset;
            bladeDepth = MathHelper.Lerp(bladeDepth, targetDepth, bladeInBurst ? 0.85f : 0.5f);
            bladeLength = MathHelper.Lerp(bladeLength, targetLength, bladeInBurst ? 0.85f : 0.45f);

            //爆发帧把上一帧姿态与中点压成两张残影,行程由刀光扛,残影只补刀体的跳帧
            float delta = MathHelper.WrapAngle(bladeRotation - bladePrevRotation);
            if (bladeInBurst && MathF.Abs(delta) > 0.06f) {
                ghostRot = bladePrevRotation;
                ghostMidRot = bladePrevRotation + delta * 0.5f;
                ghostLength = bladeLength;
                ghostStrength = MathHelper.Clamp(MathF.Abs(delta) / 0.5f, 0.35f, 1f);
                ghostLife = GhostLifeFrames;
                ghostFacing = bladeFacing;
                ghostEdgeFlip = bladeEdgeFlip;
            }
        }

        /// <summary>起手音,轻拍只留低量气口(主响在爆发帧),重击低鸣预告</summary>
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

        /// <summary>刀光时间轴事件:爆发主响/落位演出/过期剔除</summary>
        private void UpdateBeatEvents() {
            for (int i = actives.Count - 1; i >= 0; i--) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt >= a.Def.Life) {
                    actives.RemoveAt(i);
                    continue;
                }

                if (!a.SnapPlayed && lt >= a.Def.SweepStart) {
                    a.SnapPlayed = true;
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

                    //息合:卡在爆发同一帧甩出弧剑气(从本拍刃弧中段甩离)
                    if (a.Beat == BeatCount - 1 && a.Profile.BreathWave
                        && Projectile.IsOwnedByLocalPlayer()) {
                        Vector2 tip = CSW.OuterAt(in a.Def, CenterOf(a), 0.62f, out _);
                        OniMeiStrikes.FireBreathWave(Owner, tip, a.Aim, a.BaseWeaponDamage
                            , Projectile.knockBack, sizeMul, SweepSign(in a.Def), Projectile.GetSource_FromAI());
                    }
                }

                if (!a.LandPlayed && lt >= a.Def.LandFrame) {
                    a.LandPlayed = true;
                    OnLanding(a);
                }
            }
        }

        /// <summary>
        /// 落位帧(money frame):轻确认音、沿弧火花、刀尖甩墨(余韵留墨)、重拍刃头白闪、相机沿切向轻推;挥空也有
        /// </summary>
        private void OnLanding(ActiveSlash a) {
            Vector2 pivot = CenterOf(a);
            ref SweepDef d = ref a.Def;
            if (a.Beat < PingPitch.Length) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = PingPitch[a.Beat], Volume = 0.34f }
                    , CSW.OuterAt(in d, pivot, 0.9f, out _));
            }
            if (Main.dedServ) {
                return;
            }

            Vector2 headTangent = CSW.TangentAt(in d, pivot, 0.98f);
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(pivot
                , headTangent, LandPunch[a.Beat], 5f, 6, -1f, FullName));

            int sparks = 3 + a.Beat;
            for (int k = 0; k < sparks; k++) {
                float p = Main.rand.NextFloat(0.25f, 0.98f);
                Vector2 pos = CSW.OuterAt(in d, pivot, p, out _);
                Vector2 tangent = CSW.TangentAt(in d, pivot, p);
                Vector2 vel = tangent * Main.rand.NextFloat(5f, 12f) + Main.rand.NextVector2Circular(1.4f, 1.4f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                    , Main.rand.NextFloat(0.30f, 0.55f) * sizeMul)
                    ?.Configure(Main.rand.Next(9, 16), affectedByGravity: false);
            }

            //余韵留墨:刀尖沿切向甩出几滴墨,坠落
            Vector2 tip = CSW.OuterAt(in d, pivot, 1f, out _);
            for (int k = 0; k < LandInkDrops[a.Beat]; k++) {
                Vector2 vel = headTangent * Main.rand.NextFloat(5f, 9f) * sizeMul
                    + new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 2.5f));
                PRTLoader.NewParticle<PRT_OniInkDrop>(tip + Main.rand.NextVector2Circular(6f, 6f), vel
                    , new Color(96, 24, 28), Main.rand.NextFloat(0.18f, 0.30f) * sizeMul)
                    ?.Configure(Main.rand.Next(16, 24));
            }

            if (a.Beat >= 3) {
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(CSW.OuterAt(in d, pivot, 0.92f, out _), Vector2.Zero
                    , new Color(255, 200, 180), (0.7f + 0.15f * (a.Beat - 3)) * sizeMul);
            }
        }

        /// <summary>爆发帧刃头介质:沿切向的火花与速度线,只从刃头发;整画冻结帧不发</summary>
        private void SpawnSweepFX() {
            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                ref SweepDef d = ref a.Def;
                if (lt < d.SweepStart || lt > d.LandFrame || a.FrozenCenter != null) {
                    continue;
                }
                Vector2 pivot = CenterOf(a);
                float headP = CSW.Anim(in d, lt).HeadP;
                if (headP <= 0.02f) {
                    continue;
                }
                Vector2 head = CSW.OuterAt(in d, pivot, headP, out _);
                Vector2 tangent = CSW.TangentAt(in d, pivot, headP);
                Vector2 normal = tangent.RotatedBy(MathHelper.PiOver2);

                for (int k = 0; k < 2; k++) {
                    Vector2 vel = tangent * Main.rand.NextFloat(9f, 15f) + Main.rand.NextVector2Circular(1.2f, 1.2f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(head + normal * Main.rand.NextFloat(-6f, 6f), vel
                        , new Color(255, 190, 150), Main.rand.NextFloat(0.32f, 0.55f) * sizeMul)
                        ?.Configure(Main.rand.Next(8, 12), affectedByGravity: false);
                }

                int lines = a.Beat >= 2 ? 2 : 1;
                for (int k = 0; k < lines; k++) {
                    Vector2 pos = head - tangent * Main.rand.NextFloat(10f, 40f) * sizeMul
                        + normal * Main.rand.NextFloat(-16f, 16f) * sizeMul;
                    Vector2 vel = tangent * Main.rand.NextFloat(4f, 7f);
                    PRTLoader.NewParticle<PRT_CrimsonSpeedLine>(pos, vel, new Color(255, 140, 100) * 0.8f, sizeMul)
                        ?.Configure(Main.rand.Next(4, 7), (60f + 12f * a.Beat) * sizeMul, 0.06f);
                }
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
            OniBladeHandoff.Publish(Owner, bladeRotation, bladeFacing);
        }

        /// <summary>体态倾斜上身,蓄势后仰爆发前甩;坐骑/冲刺旋转让位,origin 钉脚底</summary>
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

        /// <summary>存活契约,排拍/刀光续命;收势且余韵完 Kill,再按由 UpdateCombo 重启</summary>
        private void UpdateLifetime() {
            bool visualsAlive = actives.Count > 0
                || (lastImpactFrame >= 0 && timer - lastImpactFrame < AfterglowEnd);
            if (scheduling || visualsAlive || (flashStepInterrupted && yielding)) {
                Projectile.timeLeft = 30;
                return;
            }
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
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

            CrimsonRendHitVFX.SpawnImpactBurst(pos, aim.ToRotationVector2(), power, sizeMul, steel);

            if (!Main.dedServ) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(pos
                    , aim.ToRotationVector2(), 1.5f + 6.5f * power, 5f, 8, -1f, FullName));
            }
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

        //==================== 判定 ====================

        private ActiveSlash FindDamagingSlash(NPC target) {
            for (int i = actives.Count - 1; i >= 0; i--) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt < a.Def.DamageStart || lt > a.Def.DamageEnd) {
                    continue;
                }
                if (a.HitTargets.Contains(target.whoAmI)) {
                    continue;
                }
                return a;
            }
            return null;
        }

        /// <summary>一拍对同一目标只出一次伤害,由每拍自己的命中登记裁定</summary>
        public override bool? CanHitNPC(NPC target) => FindDamagingSlash(target) == null ? false : null;

        private const int GrazePad = 12;
        /// <summary>辐条判定厚度(px),刀光内侧刀身带宽(贴脸不落空)</summary>
        private const float SpokeThickness = 36f;

        /// <summary>贪婪判定:刀光带中线折线(厚度=带宽)+辐条(轴心→带)+箱外扩;只判 [tail, head] 活着的段</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(GrazePad, GrazePad);

            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt < a.Def.DamageStart || lt > a.Def.DamageEnd) {
                    continue;
                }
                CSW.SweepAnim anim = CSW.Anim(in a.Def, lt);
                float head = anim.HeadP;
                float tail = MathHelper.Clamp(anim.TailP, 0f, 1f);
                if (head <= 0.03f || tail >= head) {
                    continue;
                }
                Vector2 pivot = CenterOf(a);

                const int samples = 15;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                float cp = 0f;
                for (int k = 0; k < samples; k++) {
                    float p = MathHelper.Lerp(tail, head, k / (float)(samples - 1));
                    Vector2 outer = CSW.OuterAt(in a.Def, pivot, p, out _);
                    Vector2 inner = CSW.InnerAt(in a.Def, pivot, p);
                    Vector2 mid = (outer + inner) * 0.5f;
                    float thick = MathF.Max(32f, (outer - inner).Length());
                    if (hasPrev && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                        , prev, mid, thick, ref cp)) {
                        return true;
                    }
                    if (k % 3 == 0 && Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                        , pivot, mid, SpokeThickness, ref cp)) {
                        return true;
                    }
                    prev = mid;
                    hasPrev = true;
                }
            }
            return false;
        }

        /// <summary>割草断藤,沿刀光带+辐条扫切,与判定同几何源</summary>
        public override void CutTiles() {
            if (actives.Count == 0) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            for (int i = 0; i < actives.Count; i++) {
                ActiveSlash a = actives[i];
                int lt = timer - a.Birth;
                if (lt < a.Def.SweepStart || lt > a.Def.DamageEnd + 2) {
                    continue;
                }
                CSW.SweepAnim anim = CSW.Anim(in a.Def, lt);
                float head = anim.HeadP;
                float tail = MathHelper.Clamp(anim.TailP, 0f, 1f);
                if (head <= 0.03f || tail >= head) {
                    continue;
                }
                Vector2 pivot = CenterOf(a);

                const int samples = 9;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                for (int k = 0; k < samples; k++) {
                    float p = MathHelper.Lerp(tail, head, k / (float)(samples - 1));
                    Vector2 outer = CSW.OuterAt(in a.Def, pivot, p, out _);
                    Vector2 inner = CSW.InnerAt(in a.Def, pivot, p);
                    Vector2 mid = (outer + inner) * 0.5f;
                    float width = MathF.Max(30f, (outer - inner).Length() * 0.8f);
                    if (hasPrev) {
                        Utils.PlotTileLine(prev, mid, width, DelegateMethods.CutTiles);
                    }
                    if (k % 2 == 0) {
                        Utils.PlotTileLine(pivot, mid, SpokeThickness, DelegateMethods.CutTiles);
                    }
                    prev = mid;
                    hasPrev = true;
                }
            }
        }

        /// <summary>重击拍伤害加成,快斩×1 重斩×1.3 终结×1.6</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            ActiveSlash a = FindDamagingSlash(target);
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
            if (target.type == NPCID.Spazmatism || target.type == NPCID.Retinazer) {
                modifiers.FinalDamage *= 1.25f;
            }
            if (target.type == CWRID.NPC_ThanatosHead) {
                modifiers.FinalDamage *= 2.85f;
            }
            if (target.type == CWRID.NPC_Apollo || target.type == CWRID.NPC_Artemis) {
                modifiers.FinalDamage *= 1.66f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            ActiveSlash a = FindDamagingSlash(target);
            a?.HitTargets.Add(target.whoAmI);

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

            //每拍首次命中爆点,强度按拍位递增;命中冻结整张画(刀光/刀/体态同帧)
            if (a != null && !a.ImpactDone) {
                a.ImpactDone = true;
                float sweepSign = SweepSign(in a.Def);
                TriggerImpactBurst(target.Center + VaultUtils.RandVr(0, target.width / 3f)
                    , (a.Beat + 1) / (float)BeatCount, a.Aim, sweepSign, steel);
                if (actives.Count > 0 && a == actives[^1]) {
                    impactHoldFrames = HitStopFrames[a.Beat];
                    impactRecoil = 1f;
                    impactRecoilSign = sweepSign;
                    bladeScalePulse = 0.04f;
                }
            }

            Vector2 aimDir = (a?.Aim ?? curAim).ToRotationVector2();
            CrimsonRendHitVFX.SpawnHitTick(target.Center, aimDir, sizeMul, steel);
        }

        //==================== 绘制 ====================
        //  身后层:远半侧刀光+身后残影+身后刀身
        //  图元层:近半侧刀光+命中爆点
        //  遮挡层:近侧残影+近景刀身本体

        /// <summary>实体刀精灵,护手钉 <see cref="bladeHandWorld"/>,刀尖钉在投影上(scale=投影刀长/贴图刀长);朝左垂直翻转并镜像支点,edgeFlip 再镜像一次=翻刃</summary>
        private void DrawBladeSprite(SpriteBatch sb, float rotation, int facing, float lengthPx, Color color
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
            float scale = lengthPx / MathF.Max((textureTip - origin).Length(), 1f);
            sb.Draw(blade, bladeHandWorld + posOffset - Main.screenPosition, null, color
                , rotation - textureAxis, origin, scale, bladeEffect, 0f);
        }

        /// <summary>爆发残影(上一帧位+中点位),按年龄与深度侧渐隐</summary>
        private void DrawGhosts(SpriteBatch sb, bool nearSide) {
            if (ghostLife <= 0) {
                return;
            }
            float ageT = 1f - ghostLife / (float)GhostLifeFrames;
            float sideW = nearSide ? NearWeight(bladeDepth) : 1f - NearWeight(bladeDepth);
            float alpha = ghostStrength * (1f - ageT) * sideW;
            if (alpha <= 0.02f) {
                return;
            }
            Color c = new Color(210, 42, 38, 130) * (alpha * 0.30f);
            Color cMid = new Color(226, 70, 52, 130) * (alpha * 0.42f);
            if (!nearSide) {
                c *= DepthDim(bladeDepth);
                cMid *= DepthDim(bladeDepth);
            }
            DrawBladeSprite(sb, ghostRot, ghostFacing, ghostLength, c, default, ghostEdgeFlip);
            DrawBladeSprite(sb, ghostMidRot, ghostFacing, ghostLength, cMid, default, ghostEdgeFlip);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>身后层,远半侧刀光+身后残影+身后刀身</summary>
        void ICrimsonFarDrawable.DrawFarSlashes() {
            if (Main.dedServ) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (actives.Count > 0 && CSW.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                for (int i = 0; i < actives.Count; i++) {
                    ActiveSlash a = actives[i];
                    int lt = timer - a.Birth;
                    if (lt < 0 || lt >= a.Def.Life || a.Def.FarDim <= 0f) {
                        continue;
                    }
                    CSW.DrawSweep(device, fx, in a.Def, CenterOf(a), lt, -1f);
                }
                CSW.EndDraw(device, pb, pr, pd);
            }

            float farW = 1f - NearWeight(bladeDepth);
            bool bladeFar = bladeOpacity > 0.01f && farW > 0.02f;
            if (!bladeFar && ghostLife <= 0) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawGhosts(sb, nearSide: false);
            if (bladeFar) {
                Color lightColor = Lighting.GetColor((int)(Owner.Center.X / 16f), (int)(Owner.Center.Y / 16f));
                float len = bladeLength * (1f + bladeScalePulse);
                float a = bladeOpacity * farW * DepthDim(bladeDepth) * bladeBurstAlpha;
                Color body = Color.Lerp(lightColor, Color.White, 0.12f) * a;
                DrawBladeSprite(sb, bladeRotation, bladeFacing, len, body, default, bladeEdgeFlip);
                DrawEngrave(sb, len, a);
            }
            sb.End();
        }

        /// <summary>遮挡层,近侧残影+近景刀身(阴影+主体),盖在图元刀光之上</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch sb) {
            if (Main.dedServ) {
                return;
            }
            DrawGhosts(sb, nearSide: true);
            if (bladeOpacity <= 0.01f) {
                return;
            }
            float nearW = NearWeight(bladeDepth);
            if (nearW <= 0.02f) {
                return;
            }

            Color lightColor = Lighting.GetColor((int)(Owner.Center.X / 16f), (int)(Owner.Center.Y / 16f));
            float len = bladeLength * (1f + bladeScalePulse);
            float a = bladeOpacity * nearW * bladeBurstAlpha;

            Color shadow = new Color(15, 3, 8, 190) * (a * 0.62f);
            DrawBladeSprite(sb, bladeRotation, bladeFacing, len * 1.018f, shadow
                , new Vector2(bladeFacing, 1f), bladeEdgeFlip);

            Color body = Color.Lerp(lightColor, Color.White, 0.24f) * a;
            DrawBladeSprite(sb, bladeRotation, bladeFacing, len, body, default, bladeEdgeFlip);
            DrawEngrave(sb, len, a);
        }

        /// <summary>刀身铭刻层:只叠在刀体本身上,残影不带铭</summary>
        private void DrawEngrave(SpriteBatch sb, float lengthPx, float alpha) {
            OniMeiEngraveState state = OniMeiBladeEngrave.Resolve(Projectile, Owner);
            if (!state.AnyEngraved) {
                return;
            }
            state.LionChain = meiLionChain / (float)BeatCount;
            float scale = lengthPx / MathF.Max(TexBladeLength, 1f);
            OniBladeProfile.BladeXform xform = OniBladeProfile.BuildXform(bladeHandWorld,
                bladeRotation, bladeFacing, scale, bladeEdgeFlip, BladeHiltUV, BladeTipUV);
            OniMeiBladeEngrave.Draw(sb, in xform, in state, alpha);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || actives.Count == 0 && lastImpactFrame < 0) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (actives.Count > 0 && CSW.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                for (int i = 0; i < actives.Count; i++) {
                    ActiveSlash a = actives[i];
                    int lt = timer - a.Birth;
                    if (lt < 0 || lt >= a.Def.Life) {
                        continue;
                    }
                    CSW.DrawSweep(device, fx, in a.Def, CenterOf(a), lt, a.Def.FarDim > 0f ? 1f : 0f);
                }
                CSW.EndDraw(device, pb, pr, pd);
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

            if (afterglowActive && CWRAsset.StarFlare01?.Value is Texture2D orb) {
                float t = (timer - lastImpactFrame - 26) / 20f;
                float oA = MathF.Sin(t * MathF.PI) * 0.42f;
                float oS = MathHelper.Lerp(0.9f, 0.18f, CSW.EaseOutCubic(t)) * sizeMul;
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
            if (CWRAsset.StarFlare02?.Value is Texture2D flare) {
                float coreA = MathF.Pow(inv, 1.8f) * 0.48f;
                float coreS = (0.7f + easeOut * 0.55f) * sizeMul;
                sb.Draw(flare, impact, null, CrimsonRendHitVFX.WoundHot * coreA, seedRot
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
                sb.Draw(flare, impact, null, CrimsonRendHitVFX.BloodDeep * (coreA * 0.7f), -seedRot * 0.5f
                    , flare.Size() * 0.5f, coreS * 1.25f, SpriteEffects.None, 0);
            }

            if (bt < 10f && CWRAsset.TearSpread01?.Value is Texture2D tear) {
                float tA = MathF.Pow(1f - bt / 10f, 1.6f) * 0.9f;
                sb.Draw(tear, impact, null, CrimsonRendHitVFX.Arterial * tA, lastImpactAim
                    , tear.Size() * 0.5f, (1.35f + easeOut * 0.5f) * sizeMul, SpriteEffects.None, 0);
                sb.Draw(tear, impact, null, CrimsonRendHitVFX.BloodDeep * (tA * 0.8f)
                    , lastImpactAim + 0.4f * lastImpactFlip
                    , tear.Size() * 0.5f, (0.95f + easeOut * 0.35f) * sizeMul
                    , SpriteEffects.FlipVertically, 0);
            }

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

        /// <summary>负片收缩,爆闪第 2~8 帧暗核压加色星爆(Fog 真 alpha 才能画暗)</summary>
        private void DrawCollapseCore() {
            float bt = timer - lastImpactFrame;
            if (lastImpactFrame < 0 || bt < 2f || bt > 8f) {
                return;
            }
            Texture2D cloud = CWRAsset.Fog?.Value;
            if (cloud == null) {
                return;
            }

            float t = (bt - 2f) / 6f;
            float coreS = MathHelper.Lerp(0.216f, 0.072f, t * t) * sizeMul;
            float coreA = MathF.Sin(t * MathF.PI) * 0.78f;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(cloud, lastImpactPos - Main.screenPosition, null
                , new Color(16, 4, 9) * coreA, Projectile.whoAmI * 1.37f
                , cloud.Size() * 0.5f, coreS, SpriteEffects.None, 0);
            sb.End();
        }
    }
}
