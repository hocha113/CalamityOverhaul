using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 黑闪黑洞弹体，四拍：撕空飞行（自掌中接棒，复合加速直奔锚点，不追踪，身后留撕开的空间缝）
    /// → 钉锚坍缩（奇点定桩，预告环立起=爆点判定半径，引力拉拽，末段寂静收干）
    /// → 黑闪爆点（冲击帧 + 黑雷放射 + 判定环=可见环）
    /// → 虚空创口（撕开的空间伤口留场数秒，触之受伤，缓慢愈合）。
    /// 相位与相位计时经 ExtraAI 同步（timeLeft 不在同步包内，只作兜底寿命）；
    /// 飞行用同一套确定性运动学各端预测，到锚各端自判，服务端在到锚/爆点两处决策点补发校正。
    /// 暗核真 alpha 遮挡 + 红黑电弧加色缘；引力透镜走 Warp 层
    /// </summary>
    internal class MLordBlackHoleProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>相位（ExtraAI 同步，只准前进）</summary>
        private enum Phase : byte
        {
            Flight = 0,
            Collapse = 1,
            Flash = 2,
            Scar = 3,
        }

        //―――― 相位时长（帧）――――
        /// <summary>飞行超时兜底（正常 20~45 帧到锚即止）</summary>
        internal const int FlightCap = 90;
        /// <summary>钉锚坍缩拍：预告环立起 + 引力拉拽</summary>
        internal const int CollapseLife = 56;
        /// <summary>坍缩末段寂静拍：拉拽/粒子/声全部收干，爆发前的黑（公平阀：最后一段无拉力的干净冲刺窗）</summary>
        internal const int SilenceFrames = 10;
        /// <summary>黑闪爆点窗</summary>
        internal const int FlashLife = 20;
        /// <summary>虚空创口留场</summary>
        internal const int ScarLife = 168;
        internal const int DesperateScarLife = 216;

        //―――― 公平阀（发射/拉拽/爆点逻辑真正读取的命名常量）――――
        /// <summary>出手初速（撕空：出手即快，读向时间由揉搓末段预读线 + 寂静拍承诺线给足）</summary>
        internal const float LaunchSpeed = 22f;
        /// <summary>残血变体出手初速</summary>
        internal const float DesperateLaunchSpeed = 27f;
        /// <summary>复合加速倍率/帧</summary>
        private const float AccelRate = 1.045f;
        private const float MaxSpeed = 46f;
        private const float DesperateMaxSpeed = 54f;
        /// <summary>坍缩期引力井作用半径 px（大于爆点半径：环外一圈也被往里拽）</summary>
        private const float PullRadius = 820f;
        /// <summary>强拉半径（此内拉力最大）</summary>
        private const float HardPullRadius = 240f;
        /// <summary>被拉向洞的分速度封顶：低于它才施力，正常位移速度即可挣脱（逃逸阀）</summary>
        private const float EscapeTowardSpeedCap = 8f;
        /// <summary>出手宽限帧：球还在掌间成形，贴脸不做接触判定</summary>
        private const int GraceFrames = 8;
        /// <summary>飞行暗核判定半径（藏在可见暗核内）</summary>
        private const float CoreRadius = 52f;
        /// <summary>黑闪爆点半径：伤害窗逐帧取当前可见环半径，绝不超出（预告环同值）</summary>
        internal const float DetonationRadius = 520f;
        internal const float DesperateDetonationRadius = 620f;
        /// <summary>飞行体绘制半径（暗核+吸积盘+电弧结构约 2.5 倍此值）</summary>
        private const float FlightBodyRadius = 96f;
        /// <summary>自掌中接棒的初始体半径（与状态 Throw 拍手中球半径一致），飞行首段膨胀到全量</summary>
        private const float HandoffRadius = 56f;
        private const float DesperateHandoffRadius = 160f;
        /// <summary>体量膨胀完成帧</summary>
        private const int SwellFrames = 12;
        /// <summary>创口半长 px</summary>
        private const float ScarHalfLength = 210f;
        /// <summary>创口宽长比（沿掷向拉开的一道缝）</summary>
        private const float ScarAspect = 0.42f;
        /// <summary>判定藏在可见暗核内的比例：shader 暗核约 0.6×体半径，判定取 0.52×</summary>
        private const float HitInsideBody = 0.52f;

        private Phase phase;
        /// <summary>相位内帧计数（各端确定性推进，服务端决策点校正）</summary>
        private int phaseTimer;
        /// <summary>本端已放过入相提示的最高相位（回退/重发不重播）</summary>
        private Phase highestCued;

        /// <summary>飞行帧计数（本地推进，膨胀与宽限判断用）</summary>
        private ref float FlightTimer => ref Projectile.localAI[0];

        private Vector2 Anchor => new(Projectile.ai[0], Projectile.ai[1]);
        /// <summary>残血变体标记（ai[2]，随生成包同步）：巨体量+更快+更大爆点+大幅震屏与扭曲</summary>
        private bool Desperate => Projectile.ai[2] == 1f;
        /// <summary>残血变体体量倍率：暗核视觉与接触判定同倍放大（视觉=判定的承诺不破）</summary>
        private float BodyScale => Desperate ? 2f : 1f;
        private float RingRadius => Desperate ? DesperateDetonationRadius : DetonationRadius;
        private int ScarLifeNow => Desperate ? DesperateScarLife : ScarLife;

        //―――― 黑雷（纯客户端表现，随机形态各端不一致无妨：不参与任何判定）――――
        private sealed class BlackBolt
        {
            public ThunderTrail Trail;
            public int Age;
            public int Life;
            public float Width;
            public float Alpha => (1f - Age / (float)Life) * (Age % 2 == 0 ? 1f : 0.62f);
        }

        private readonly List<BlackBolt> bolts = [];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
            //同材质拖尾缓存（契约5：飞行弹必须有可读尾迹）；撕空速度下 16 位足够连成一道缝
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = (int)(CoreRadius * 2f);
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //兜底寿命：相位链最长总和再留余量，正常由创口闭合自杀
            Projectile.timeLeft = FlightCap + CollapseLife + FlashLife + DesperateScarLife + 30;
        }

        //―――― 相位进度（phase/phaseTimer 各端一致）――――
        private bool InFlight => phase == Phase.Flight;
        private bool InCollapse => phase == Phase.Collapse;
        private bool InFlash => phase == Phase.Flash;
        private bool InScar => phase == Phase.Scar;
        /// <summary>坍缩进度 0~1</summary>
        private float CollapseT => InCollapse ? MathHelper.Clamp(phaseTimer / (float)CollapseLife, 0f, 1f) : (InFlight ? 0f : 1f);
        /// <summary>坍缩末段寂静拍</summary>
        private bool InSilence => InCollapse && phaseTimer >= CollapseLife - SilenceFrames;
        /// <summary>爆闪进度 0~1</summary>
        private float FlashT => InFlash ? MathHelper.Clamp(phaseTimer / (float)FlashLife, 0f, 1f) : (InScar ? 1f : 0f);
        /// <summary>创口进度 0~1</summary>
        private float ScarT => InScar ? MathHelper.Clamp(phaseTimer / (float)ScarLifeNow, 0f, 1f) : 0f;
        /// <summary>当前可见冲击环半径（伤害窗逐帧对齐它；指数缓出=一记猛张）</summary>
        private float FlashRingRadius => RingRadius * VaultUtils.EaseOutExpo(FlashT);
        /// <summary>接棒膨胀 0~1：掌中球半径涨到飞行全量</summary>
        private float Swell => VaultUtils.EaseOutCubic(MathHelper.Clamp(FlightTimer / SwellFrames, 0f, 1f));
        /// <summary>飞行体半径（含膨胀与变体倍率）</summary>
        private float FlightBodyRadiusNow => MathHelper.Lerp(
            Desperate ? DesperateHandoffRadius : HandoffRadius, FlightBodyRadius * BodyScale, Swell);

        /// <summary>坍缩体半径：先缓收再在寂静拍急缩成一点</summary>
        private float CollapseBodyRadius {
            get {
                float mainSpan = 1f - SilenceFrames / (float)CollapseLife;
                float t = MathHelper.Clamp(CollapseT / mainSpan, 0f, 1f);
                float r = MathHelper.Lerp(FlightBodyRadius, 34f, VaultUtils.EaseInQuad(t));
                if (InSilence) {
                    float s = (phaseTimer - (CollapseLife - SilenceFrames)) / (float)SilenceFrames;
                    r = MathHelper.Lerp(34f, 14f, MathHelper.Clamp(s, 0f, 1f));
                }
                return r * BodyScale;
            }
        }

        /// <summary>创口当前半长：猛地撑开（回弹缓出）→ 持住 → 后 40% 缓慢愈合</summary>
        private float ScarHalfLengthNow {
            get {
                if (!InScar) {
                    return 0f;
                }
                float open = VaultUtils.EaseOutBack(MathHelper.Clamp(phaseTimer / 14f, 0f, 1f));
                float close = ScarT > 0.6f ? 1f - VaultUtils.EaseInCubic((ScarT - 0.6f) / 0.4f) : 1f;
                return ScarHalfLength * BodyScale * open * close;
            }
        }

        public override void AI() {
            switch (phase) {
                case Phase.Flight:
                    UpdateFlight();
                    break;
                case Phase.Collapse:
                    UpdateCollapse();
                    break;
                case Phase.Flash:
                    UpdateFlash();
                    break;
                default:
                    UpdateScar();
                    break;
            }
            if (!Projectile.active) {
                return;
            }
            UpdateBolts();
            float light = InScar ? 0.35f * (1f - ScarT) : 0.5f * (1f - FlashT * 0.4f);
            Lighting.AddLight(Projectile.Center, MLordDirector.BlackFlashRed.ToVector3() * light);
        }

        #region 相位推进与同步

        /// <summary>
        /// 入相：计时归零、坍缩起停摆、服务端在到锚/爆点两个决策点补发校正，
        /// 本端只对严格前进的相位放一次入相提示（重发/回退不重播）
        /// </summary>
        private void EnterPhase(Phase next, int timer, bool snapToAnchor) {
            phase = next;
            phaseTimer = timer;
            if (next >= Phase.Collapse) {
                Projectile.velocity = Vector2.Zero;
                if (snapToAnchor) {
                    Projectile.Center = Anchor;
                }
            }
            if (!VaultUtils.isClient && next is Phase.Collapse or Phase.Flash) {
                Projectile.netUpdate = true;
            }
            if (VaultUtils.isServer || next <= highestCued) {
                return;
            }
            highestCued = next;
            switch (next) {
                case Phase.Collapse:
                    CollapseCue();
                    break;
                case Phase.Flash:
                    FlashCue();
                    break;
                case Phase.Scar:
                    ScarCue();
                    break;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)phase);
            writer.Write((short)phaseTimer);
            writer.Write(Projectile.rotation);
        }

        /// <summary>只接受相位/计时的严格前进：位置速度已由原版包写好，不再吸锚</summary>
        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase remotePhase = (Phase)reader.ReadByte();
            int remoteTimer = reader.ReadInt16();
            float remoteRotation = reader.ReadSingle();
            if (remotePhase > phase || (remotePhase == phase && remoteTimer > phaseTimer)) {
                if (remotePhase >= Phase.Collapse) {
                    Projectile.rotation = remoteRotation;
                }
                EnterPhase(remotePhase, remoteTimer, snapToAnchor: false);
            }
        }

        #endregion

        #region 相位更新

        /// <summary>飞行：复合加速直奔锚点，各端同式预测；到锚各端自判入坍缩，超时原地坍缩</summary>
        private void UpdateFlight() {
            FlightTimer++;
            //复合加速：出手即快、越飞越快（撕开空间的一道黑），残血变体全程更快
            float speed = Projectile.velocity.Length();
            if (speed < (Desperate ? DesperateMaxSpeed : MaxSpeed)) {
                Projectile.velocity *= AccelRate;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //到锚判据（各端同判：位置速度锚点都是同步量）：越过锚点或下一帧就会越过→钉在锚点坍缩
            Vector2 toAnchor = Anchor - Projectile.Center;
            bool arrived = Vector2.Dot(Projectile.velocity, toAnchor) <= 0f
                || toAnchor.Length() <= Projectile.velocity.Length();
            if (arrived) {
                EnterPhase(Phase.Collapse, 0, snapToAnchor: true);
                return;
            }
            //超时兜底：原地坍缩，锚点改写成当前位（ai 槽随校正包同步，预告环跟着搬）
            if (FlightTimer >= FlightCap) {
                Projectile.ai[0] = Projectile.Center.X;
                Projectile.ai[1] = Projectile.Center.Y;
                EnterPhase(Phase.Collapse, 0, snapToAnchor: true);
                return;
            }

            //―――― 客户端飞行表现 ――――
            if (VaultUtils.isServer) {
                return;
            }
            //撕空甩尾：红黑星屑从缝口向后甩出（甩出量随速），读作洞体撕开空间的碎屑
            int shed = 1 + (int)(speed / 20f);
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = back.RotatedBy(MathHelper.PiOver2);
            float bodyR = FlightBodyRadiusNow;
            for (int i = 0; i < shed; i++) {
                Vector2 pos = Projectile.Center + back * Main.rand.NextFloat(0f, speed * 1.2f)
                    + perp * Main.rand.NextFloat(-0.7f, 0.7f) * bodyR;
                Vector2 vel = back * Main.rand.NextFloat(1.5f, 4f) + perp * Main.rand.NextFloat(-2.5f, 2.5f);
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.VoidBlack, Main.rand.NextFloat(0.6f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, vel, c,
                    Main.rand.NextFloat(0.3f, 0.65f))?.Configure(false, Main.rand.Next(10, 18));
            }
            //缘弧迸溅
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Unit() * bodyR * 0.7f,
                    back * Main.rand.NextFloat(3f, 7f) + Main.rand.NextVector2Circular(2f, 2f),
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
            //锚点预告环在飞行期接住寂静拍的环，一路亮到弹体钉锚
            //（绘制放 PreDraw；此处只保证引力昏暗不断档）
            MLordScreenEffects.PushGravityDim(Anchor, 0.45f);
        }

        /// <summary>
        /// 钉锚坍缩：奇点定桩，预告环立起（半径=爆点判定半径），引力向内拉（逃逸阀留挣脱手段），
        /// 三记升调倒计时，末段寂静拍一切收干（无拉力的干净冲刺窗，爆发前的黑）
        /// </summary>
        private void UpdateCollapse() {
            if (phaseTimer >= CollapseLife) {
                EnterPhase(Phase.Flash, 0, snapToAnchor: false);
                UpdateFlash();
                return;
            }
            Projectile.velocity = Vector2.Zero;
            float t = CollapseT;
            bool silence = InSilence;

            //引力井：只拉本地玩家（动作权威在玩家本地），寂静拍不拉
            Player local = Main.LocalPlayer;
            if (!VaultUtils.isServer && !silence && local.active && !local.dead) {
                Vector2 toHole = Projectile.Center - local.Center;
                float dist = toHole.Length();
                if (dist < PullRadius && dist > 30f) {
                    float radial = MathHelper.Clamp(1f - (dist - HardPullRadius) / (PullRadius - HardPullRadius), 0f, 1f);
                    float strength = MathHelper.Lerp(0.10f, 0.42f, t) * MathHelper.Lerp(0.35f, 1f, radial);
                    Vector2 pullDir = toHole / dist;
                    //逃逸阀：朝洞分速度低于封顶才施力，位移技/正常横移足以挣脱
                    if (Vector2.Dot(local.velocity, pullDir) < EscapeTowardSpeedCap) {
                        local.velocity += pullDir * strength;
                    }
                }
            }

            phaseTimer++;
            if (VaultUtils.isServer) {
                return;
            }

            //―――― 客户端坍缩表现 ――――
            if (silence) {
                //寂静：不推昏暗（画面亮度回抬一拍再暗），不出粒子，不震
                if (phaseTimer - 1 == CollapseLife - SilenceFrames) {
                    //吸气：所有声音的截断由这一声短促的收干标出
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.65f, Pitch = -0.8f }, Projectile.Center);
                }
                return;
            }
            MLordScreenEffects.PushGravityDim(Projectile.Center, 0.55f + 0.45f * t);
            if (Desperate) {
                Main.LocalPlayer.CWR()?.GetScreenShake(2f + 3f * t);
            }
            //三记升调倒计时（玩家可内化的节拍）
            int beatIndex = (phaseTimer - 1) switch { 10 => 0, 24 => 1, 38 => 2, _ => -1 };
            if (beatIndex >= 0) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.9f, Pitch = -0.15f + beatIndex * 0.35f }, Projectile.Center);
                MLordScreenFX.Punch(Projectile.Center, 2.5f + beatIndex * 1.3f, 8);
            }
            //万物被吸进奇点：越到后段吸得越快越密
            int converge = 2 + (int)(t * 3f);
            float reach = MathHelper.Lerp(260f, 640f, t) * MathF.Sqrt(BodyScale);
            for (int i = 0; i < converge; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(reach * 0.35f, reach);
                Vector2 pull = (Projectile.Center - pos) * MathHelper.Lerp(0.07f, 0.13f, t);
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.VoidBlack, Main.rand.NextFloat(0.55f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, pull.RotatedBy(0.35f), c,
                    Main.rand.NextFloat(0.3f, 0.7f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        /// <summary>黑闪爆点：判定环逐帧对齐可见环（入相提示已放），爆完开创口</summary>
        private void UpdateFlash() {
            if (phaseTimer >= FlashLife) {
                EnterPhase(Phase.Scar, 0, snapToAnchor: false);
                UpdateScar();
                return;
            }
            Projectile.velocity = Vector2.Zero;
            phaseTimer++;
        }

        /// <summary>虚空创口：留场的空间伤口，触之受伤；沿缝偶发黑雷放电，愈合到头自灭</summary>
        private void UpdateScar() {
            if (phaseTimer >= ScarLifeNow) {
                if (!VaultUtils.isServer) {
                    //愈合到头：缝合的一声脆响 + 一小撮碎星（最后一笔不能凭空消失）
                    SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                    MLordScreenFX.StarBurst(Projectile.Center, 0.8f, 10);
                }
                Projectile.Kill();
                return;
            }
            Projectile.velocity = Vector2.Zero;
            phaseTimer++;
            if (VaultUtils.isServer) {
                return;
            }

            float halfLen = ScarHalfLengthNow;
            Vector2 axis = Projectile.rotation.ToRotationVector2();
            Vector2 normal = axis.RotatedBy(MathHelper.PiOver2);
            //残光被伤口继续吞噬：星屑顺缝滑入
            if (Main.rand.NextBool(2) && halfLen > 20f) {
                Vector2 pos = Projectile.Center + axis * Main.rand.NextFloat(-1f, 1f) * halfLen * 1.3f
                    + normal * Main.rand.NextFloat(-1f, 1f) * halfLen * 0.9f;
                Vector2 pull = (Projectile.Center - pos) * 0.06f;
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.VoidBlack, Main.rand.NextFloat(0.6f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, pull, c,
                    Main.rand.NextFloat(0.25f, 0.5f))?.Configure(false, Main.rand.Next(14, 22));
            }
            //缝口两端迸红火花
            if (Main.rand.NextBool(6) && halfLen > 20f) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + axis * side * halfLen * 0.6f,
                    axis * side * Main.rand.NextFloat(2f, 5f) + normal * Main.rand.NextFloat(-2f, 2f),
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
            //沿缝偶发放电（短黑雷）
            if (phaseTimer % 26 == 0 && ScarT < 0.78f && halfLen > 40f) {
                Vector2 from = Projectile.Center + axis * Main.rand.NextFloat(-0.8f, 0.8f) * halfLen * 0.6f;
                Vector2 to = from + Main.rand.NextVector2Unit() * Main.rand.NextFloat(90f, 170f) * BodyScale;
                SpawnBolt(from, to, 9f * MathF.Sqrt(BodyScale), 8);
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = -0.5f }, from);
            }
            //透镜与昏暗随伤口缓慢回稳
            MLordScreenEffects.PushGravityDim(Projectile.Center, 0.3f * (1f - ScarT));
        }

        #endregion

        #region 入相提示（仅绘制端，每相位一次）

        /// <summary>钉锚：洞把自己钉进空间的一声闷响 + 收缩的吸气</summary>
        private void CollapseCue() {
            SoundEngine.PlaySound(CWRSound.BlackHole with { Volume = 0.9f, Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -1f }, Projectile.Center);
            MLordScreenFX.Punch(Projectile.Center, Desperate ? 8f : 6f, 12);
            MLordScreenFX.StarBurst(Projectile.Center, 1.1f, 12);
        }

        /// <summary>创口撑开：低沉的撕裂声</summary>
        private void ScarCue() {
            SoundEngine.PlaySound(SoundID.Zombie96 with { Volume = 0.7f, Pitch = -0.7f }, Projectile.Center);
        }

        /// <summary>黑闪爆点：冲击帧 + 全屏红黑冲击波 + 低频轰体/雷鸣 + 黑雷放射 + 碎星裂纹（伤害窗同帧开启）。
        /// 残血变体全面放大：涟漪列全屏波纹（护盾爆碎级）+ 更多更长的黑雷 + 更重的震</summary>
        private void FlashCue() {
            Vector2 c = Projectile.Center;
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f, Pitch = -0.45f }, c);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = 0.1f }, c);
            //低频轰体 + 雷鸣：黑闪的"闪"要有雷跟着
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.7f }, c);
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.85f, Pitch = -0.3f }, c);
            MLordScreenFX.Punch(c, Desperate ? 22f : 16f, Desperate ? 28 : 20);
            Main.LocalPlayer.CWR()?.GetScreenShake(Desperate ? 14f : 6f);
            //超 1 强度显形尾随涟漪列，长扩散窗把波扫出全屏
            MLordBlackFlashFX.PushFlash(c, Desperate ? 2.2f : 1.35f, Desperate ? 58 : 34);
            MLordScreenEffects.PushStarRing(c, 1.2f, RingRadius * 1.7f, 32);
            //红黑碎星 + 空间裂纹（残血变体：更多、更快、更大）
            int starCount = Desperate ? 54 : 34;
            float velScale = Desperate ? 1.6f : 1.15f;
            float sizeScale = Desperate ? 1.35f : 1.05f;
            for (int i = 0; i < starCount; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 15f) * velScale;
                Color color = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.MoonWhite, Main.rand.NextFloat(0.35f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(c, vel, color,
                    Main.rand.NextFloat(0.6f, 1.2f) * sizeScale)?.Configure(true, Main.rand.Next(22, 40));
            }
            int fractureCount = Desperate ? 14 : 8;
            for (int i = 0; i < fractureCount; i++) {
                PRTLoader.NewParticle<PRT_SpaceFracture>(c,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f) * velScale,
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(1f, 1.6f) * sizeScale)
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.05f, 0.05f));
            }
            //黑雷放射：自爆心劈到环外一截（黑体真 alpha 咬底 + 红芯加色），这就是"黑闪"的闪
            int boltCount = Desperate ? 12 : 8;
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < boltCount; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / boltCount + Main.rand.NextFloat(-0.18f, 0.18f);
                float length = RingRadius * Main.rand.NextFloat(1.02f, 1.34f);
                SpawnBolt(c, c + angle.ToRotationVector2() * length, (Desperate ? 26f : 20f) * Main.rand.NextFloat(0.8f, 1.15f), 22);
            }
        }

        #endregion

        #region 黑雷

        /// <summary>一道黑雷：ThunderTrail 一体两层，黑体 NonPremultiplied 真 alpha 咬掉背景，红芯 Additive 走 FlowColor</summary>
        private void SpawnBolt(Vector2 from, Vector2 to, float width, int life) {
            if (CWRAsset.ThunderTrail == null) {
                return;
            }
            const int PointCount = 12;
            Vector2 dir = (to - from).SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float bow = Main.rand.NextFloat(-0.16f, 0.16f) * Vector2.Distance(from, to);
            Vector2[] points = new Vector2[PointCount];
            for (int i = 0; i < PointCount; i++) {
                float f = i / (PointCount - 1f);
                //整体带一点弓形，避免每道都是笔直的辐条
                points[i] = Vector2.Lerp(from, to, f) + perp * (bow * MathF.Sin(f * MathHelper.Pi));
            }
            BlackBolt bolt = new() { Life = life, Width = width };
            bolt.Trail = new ThunderTrail(CWRAsset.ThunderTrail,
                f => bolt.Width * (1f - f * 0.55f) * (1f - bolt.Age / (float)bolt.Life * 0.5f),
                _ => new Color(14, 6, 22),
                _ => bolt.Alpha) {
                CanDraw = true,
                UseNonOrAdd = true,
                PartitionPointCount = 2,
                FlowColor = MLordDirector.BlackFlashRed,
            };
            bolt.Trail.SetRange((0f, MathF.Max(10f, width * 1.5f)));
            bolt.Trail.SetExpandWidth(width * 0.45f);
            bolt.Trail.BasePositions = points;
            bolt.Trail.RandomThunder();
            bolts.Add(bolt);
        }

        private void UpdateBolts() {
            if (VaultUtils.isServer || bolts.Count == 0) {
                return;
            }
            for (int i = bolts.Count - 1; i >= 0; i--) {
                BlackBolt bolt = bolts[i];
                bolt.Age++;
                if (bolt.Age >= bolt.Life) {
                    bolts.RemoveAt(i);
                    continue;
                }
                if (bolt.Age % 2 == 0) {
                    bolt.Trail.RandomThunder();
                }
            }
        }

        private void DrawBolts() {
            if (bolts.Count == 0) {
                return;
            }
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            foreach (BlackBolt bolt in bolts) {
                bolt.Trail.DrawThunder(gd);
            }
        }

        #endregion

        /// <summary>判定：飞行/坍缩=暗核圆（随可见体缩放）；爆闪=逐帧对齐可见冲击环；创口=暗核椭圆。
        /// 出手宽限帧内不做接触判定：球还在掌间成形（契约3）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 center = Projectile.Center;
            switch (phase) {
                case Phase.Flight:
                    if (FlightTimer <= GraceFrames) {
                        return false;
                    }
                    return CircleHits(center, FlightBodyRadiusNow * HitInsideBody, targetHitbox);
                case Phase.Collapse:
                    return CircleHits(center, CollapseBodyRadius * HitInsideBody, targetHitbox);
                case Phase.Flash:
                    return CircleHits(center, FlashRingRadius, targetHitbox);
                default:
                    return ScarHits(center, targetHitbox);
            }
        }

        private static bool CircleHits(Vector2 center, float radius, Rectangle target) {
            if (radius <= 1f) {
                return false;
            }
            Vector2 closest = new(
                MathHelper.Clamp(center.X, target.Left, target.Right),
                MathHelper.Clamp(center.Y, target.Top, target.Bottom));
            return Vector2.DistanceSquared(closest, center) <= radius * radius;
        }

        /// <summary>创口椭圆判定：沿缝半长与半宽各取可见暗核内比例，测目标矩形中心与四角</summary>
        private bool ScarHits(Vector2 center, Rectangle target) {
            float a = ScarHalfLengthNow * HitInsideBody;
            float b = a * ScarAspect;
            if (a <= 2f) {
                return false;
            }
            Vector2 axis = Projectile.rotation.ToRotationVector2();
            Vector2 normal = axis.RotatedBy(MathHelper.PiOver2);
            Span<Vector2> probes = [
                target.Center.ToVector2(),
                new Vector2(target.Left, target.Top), new Vector2(target.Right, target.Top),
                new Vector2(target.Left, target.Bottom), new Vector2(target.Right, target.Bottom),
            ];
            foreach (Vector2 p in probes) {
                Vector2 d = p - center;
                float u = Vector2.Dot(d, axis) / a;
                float v = Vector2.Dot(d, normal) / b;
                if (u * u + v * v <= 1f) {
                    return true;
                }
            }
            return false;
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            //爆闪窗吃满额外爆点伤害（长预告演出级的一锤）；创口触伤降档（留场惩罚，不是二次大招）
            if (InFlash) {
                modifiers.SourceDamage *= MLordDirector.BlackFlashBurstDamage / (float)MLordDirector.BlackHoleContactDamage;
            }
            else if (InScar) {
                modifiers.SourceDamage *= MLordDirector.BlackFlashScarDamage / (float)MLordDirector.BlackHoleContactDamage;
            }
        }

        #region 扭曲与绘制

        public bool DontUseBlueshiftEffect() => true;
        public bool CanDrawCustom() => false;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>引力透镜：飞行常驻，坍缩收紧，爆闪一记扩张脉冲，创口期随伤口愈合回稳。
        /// 残血变体：透镜场随体量放大，爆点扩成大面积光线扭曲并在创口初段继续外扩</summary>
        public void Warp() {
            float env;
            float size;
            if (InFlight) {
                env = MathHelper.Clamp(FlightTimer / 12f, 0f, 1f);
                size = 800f * BodyScale;
            }
            else if (InCollapse) {
                env = 1f;
                size = MathHelper.Lerp(800f, 380f, CollapseT) * BodyScale;
            }
            else if (InFlash) {
                env = 1f - FlashT * 0.5f;
                size = MathHelper.Lerp(380f * BodyScale, Desperate ? 4600f : 1600f, VaultUtils.EaseOutCubic(FlashT));
            }
            else {
                //创口：爆点余波在前 30 帧继续外扩扫过战场，之后收成伤口自身的稳定透镜
                float wave = MathHelper.Clamp(phaseTimer / 30f, 0f, 1f);
                float waveSize = MathHelper.Lerp(Desperate ? 4600f : 1600f, Desperate ? 7200f : 2600f, wave);
                float waveEnv = 0.5f * (1f - wave);
                float scarEnv = 0.3f * (1f - ScarT);
                float scarSize = 620f * BodyScale * (ScarHalfLengthNow / (ScarHalfLength * BodyScale) + 0.2f);
                if (waveEnv > scarEnv) {
                    env = waveEnv;
                    size = waveSize;
                }
                else {
                    env = scarEnv;
                    size = scarSize;
                }
            }
            if (env <= 0.04f) {
                return;
            }
            //残血变体飞行/坍缩 0.55，爆闪与余波顶到 0.7，增强聚焦在爆点
            float strength = Desperate ? (InFlight || InCollapse ? 0.55f : 0.7f) : 0.38f;
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size,
                strength * env, 1f, 0f, "GravitationalLens", 0.42f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //黑雷是原始图元即时出，先画，落在同批精灵之下
            DrawBolts();
            switch (phase) {
                case Phase.Flight:
                    DrawFlightPhase(pos);
                    break;
                case Phase.Collapse:
                    DrawCollapsePhase(pos);
                    break;
                case Phase.Flash:
                    DrawFlashPhase(pos);
                    break;
                default:
                    DrawScarPhase(pos);
                    break;
            }
            return false;
        }

        /// <summary>飞行：锚点预告环（接住寂静拍的环）+ 撕空尾缝 + 随速拉长的黑体</summary>
        private void DrawFlightPhase(Vector2 pos) {
            DrawTelegraphRing(Anchor, RingRadius, 0.42f, locked: false);
            float bodyR = FlightBodyRadiusNow;
            float speed = Projectile.velocity.Length();
            float along = 1f + speed / 34f;
            Vector2 stretch = new(along, 1f / MathF.Sqrt(along));
            DrawTrail(bodyR, stretch.Y, 1f);
            DrawHoleBody(pos, bodyR, 1f, Projectile.rotation, stretch, 0.9f, 0.85f);
        }

        /// <summary>坍缩：预告环立起并锁定，尾缝被吸回奇点，黑体缓收再急缩成一点</summary>
        private void DrawCollapsePhase(Vector2 pos) {
            float t = CollapseT;
            bool silence = InSilence;
            float ringR = RingRadius * VaultUtils.EaseOutCubic(MathHelper.Clamp(t / 0.45f, 0f, 1f));
            DrawTelegraphRing(Projectile.Center, ringR, silence ? 1f : MathHelper.Lerp(0.55f, 0.9f, t), locked: silence);
            if (phaseTimer < 16) {
                DrawTrail(FlightBodyRadius * BodyScale, 0.8f, 1f - phaseTimer / 16f);
            }
            float arc = 0.9f + (silence ? 0.6f : 0.3f * t);
            DrawHoleBody(pos, CollapseBodyRadius, 1f, Projectile.rotation, Vector2.One, 0.9f + 0.1f * t, arc);
        }

        /// <summary>爆闪：奇点被吞尽，冲击环猛张（环缘=判定边界）</summary>
        private void DrawFlashPhase(Vector2 pos) {
            float ft = FlashT;
            float consumed = MathHelper.Clamp(ft * 3f, 0f, 1f);
            float bodyR = MathHelper.Lerp(14f, 0f, consumed) * BodyScale;
            if (bodyR > 1f) {
                DrawHoleBody(pos, bodyR, 1f - consumed, Projectile.rotation, Vector2.One, 1f, 1.5f);
            }
            DrawFlashRing(pos, 1f);
        }

        /// <summary>创口：爆闪余晖在前 12 帧散尽，沿掷向撑开的一道空间缝，愈合时电弧越躁</summary>
        private void DrawScarPhase(Vector2 pos) {
            if (phaseTimer < 12) {
                DrawFlashRing(pos, 1f - phaseTimer / 12f);
            }
            float halfLen = ScarHalfLengthNow;
            if (halfLen <= 2f) {
                return;
            }
            float closing = ScarT > 0.6f ? (ScarT - 0.6f) / 0.4f : 0f;
            DrawHoleBody(pos, halfLen, 1f, Projectile.rotation, new Vector2(1f, ScarAspect), 1f, 1.1f + 0.6f * closing);
        }

        /// <summary>
        /// 撕空尾缝（契约5，同材质）：黑洞自己的暗核+红缘按 oldPos 重绘，每段沿运动方向拉长到盖过帧间距，
        /// 连成一道被撕开的空间缝而非一串圆点；横向厚度取体横向的 0.6~1.0 倍。
        /// across=本体横向压缩比，fade=整体透明（钉锚后尾缝被吸回奇点时渐隐）
        /// </summary>
        private void DrawTrail(float bodyR, float across, float fade) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null || fade <= 0.02f) {
                return;
            }
            Vector2 half = Projectile.Size * 0.5f;
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 oldPos = Projectile.oldPos[i];
                if (oldPos == Vector2.Zero) {
                    continue;
                }
                Vector2 prev = Projectile.oldPos[i - 1] == Vector2.Zero ? Projectile.position : Projectile.oldPos[i - 1];
                Vector2 step = prev - oldPos;
                float gap = step.Length();
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = oldPos + half - Main.screenPosition;
                float r = bodyR * across * (0.6f + 0.4f * k);
                float texScale = r * 2.6f / glow.Width;
                //沿运动方向拉长：至少盖过与前一位的间距，静止后退化为圆点（尾缝被吸拢）
                float alongScale = gap > 1f ? MathF.Max(1f, gap * 1.6f / (r * 2f)) : 1f;
                float rot = gap > 1f ? step.ToRotation() : Projectile.rotation;
                Vector2 rimScale = new(texScale * 1.25f * alongScale, texScale * 1.25f);
                Vector2 coreScale = new(texScale * alongScale, texScale);
                //红缘（加色）压底，暗核（真 alpha）叠上：与本体同层序同材质
                Main.EntitySpriteDraw(glow, pos, null,
                    MLordDirector.BlackFlashRed with { A = 0 } * (0.36f * k * fade),
                    rot, glow.Size() / 2f, rimScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.62f * k * fade),
                    rot, glow.Size() / 2f, coreScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 洞体：shader 量体（暗核+吸积盘+电弧），画布按 stretch 非等比缩放并沿 rotation 旋转，
        /// 飞行期拉成随速的黑色长条，创口期压成沿掷向的一道缝；缺 shader 走 CPU 双层
        /// </summary>
        private void DrawHoleBody(Vector2 pos, float radius, float vis, float rotation, Vector2 stretch, float collapse, float arc) {
            if (vis <= 0.02f || radius <= 1f) {
                return;
            }
            Effect shader = EffectLoader.MLordBlackFlash?.Value;
            if (shader != null) {
                Texture2D canvas = CWRUtils.GetT2DAsset(CWRConstant.VaultPlaceholder2).Value;
                float scale = radius * 5f / canvas.Width;
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uCollapse"]?.SetValue(MathHelper.Clamp(collapse, 0f, 1f));
                shader.Parameters["uArc"]?.SetValue(arc);
                shader.Parameters["uAlpha"]?.SetValue(vis);
                shader.Parameters["uSeed"]?.SetValue(Projectile.identity % 89 * 0.211f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                shader.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Draw(canvas, pos, null, Color.White, rotation,
                    canvas.Size() * 0.5f, stretch * scale, SpriteEffects.None, 0f);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                return;
            }

            //CPU 回退：暗核真 alpha + 红缘 + 斜吸积盘（同样吃 stretch/rotation）
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) {
                return;
            }
            float texScale = radius * 2.6f / glow.Width;
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.5f * vis),
                rotation, glow.Size() / 2f, stretch * (texScale * 1.25f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.96f * vis),
                rotation, glow.Size() / 2f, stretch * texScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.55f * vis),
                rotation + Main.GlobalTimeWrappedHourly * 2.4f, glow.Size() / 2f,
                new Vector2(texScale * 1.7f, texScale * 0.4f), SpriteEffects.None, 0);
        }

        /// <summary>爆闪冲击环：可见环半径即伤害半径（视觉=判定，一像素不差的承诺），环缘用白热点阵钉死边界</summary>
        private void DrawFlashRing(Vector2 pos, float fade) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D dot = CWRAsset.SoftGlow?.Value;
            if (glow == null || star == null || dot == null) {
                return;
            }
            float ft = FlashT;
            float ringR = FlashRingRadius;
            float ringScale = ringR * 2f / glow.Width;
            //白芯闪帧（只在爆闪窗内的短脉冲）；残血变体白芯放大，读作爆心体量而非判定边界
            if (InFlash) {
                float coreScale = (0.9f + ft * 0.6f) * (Desperate ? 2f : 1.3f);
                Main.EntitySpriteDraw(star, pos, null, MLordDirector.MoonWhite with { A = 0 } * (0.9f * (1f - ft)),
                    Main.GlobalTimeWrappedHourly * 3f, star.Size() / 2f, coreScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.7f * (1f - ft)),
                    -Main.GlobalTimeWrappedHourly * 2f, star.Size() / 2f, coreScale * 1.5f, SpriteEffects.None, 0);
            }
            //红黑环体：外红缘 + 内暗吞（暗层真 alpha，把爆心咬出一圈黑）
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.85f * fade),
                0f, glow.Size() / 2f, ringScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.7f * fade * (1f - ft)),
                0f, glow.Size() / 2f, ringScale * 0.62f, SpriteEffects.None, 0);
            //环缘点阵：环走到哪判定就到哪，白热点压在正好的半径上
            if (ringR > 40f) {
                const int Dots = 96;
                float dotScale = 0.36f * (0.7f + 0.3f * fade);
                Color rim = MLordDirector.MoonWhite with { A = 0 } * (0.9f * fade);
                Color rimRed = MLordDirector.BlackFlashRed with { A = 0 } * (0.7f * fade);
                for (int i = 0; i < Dots; i++) {
                    Vector2 p = pos + (MathHelper.TwoPi * i / Dots).ToRotationVector2() * ringR;
                    Main.EntitySpriteDraw(dot, p, null, rimRed, 0f, dot.Size() / 2f, dotScale * 2.2f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(dot, p, null, rim, 0f, dot.Size() / 2f, dotScale, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 爆点预告环（幻影臂寂静拍、弹体飞行/坍缩期共用同一只）：
        /// 淡红薄盘铺出整个判定范围 + 环缘光点慢转；locked=寂静拍锁定态，点阵加密转白、反向快转
        /// </summary>
        internal static void DrawTelegraphRing(Vector2 worldCenter, float radius, float strength, bool locked) {
            if (strength <= 0.01f || radius < 8f) {
                return;
            }
            Texture2D disc = CWRAsset.DiffusionCircle?.Value;
            Texture2D dot = CWRAsset.SoftGlow?.Value;
            if (disc == null || dot == null) {
                return;
            }
            Vector2 pos = worldCenter - Main.screenPosition;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * (locked ? 30f : 12f));
            Main.EntitySpriteDraw(disc, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.11f * strength * pulse),
                0f, disc.Size() / 2f, radius * 2f / disc.Width, SpriteEffects.None, 0);
            int dots = locked ? 96 : 72;
            float spin = Main.GlobalTimeWrappedHourly * (locked ? -0.9f : 0.45f);
            float dotScale = (locked ? 0.34f : 0.26f) * (0.85f + 0.15f * pulse);
            Color dotColor = (locked ? MLordDirector.MoonWhite : MLordDirector.BlackFlashRed) with { A = 0 } * (0.85f * strength);
            Color haloColor = MLordDirector.BlackFlashRed with { A = 0 } * (0.55f * strength);
            for (int i = 0; i < dots; i++) {
                Vector2 p = pos + (MathHelper.TwoPi * i / dots + spin).ToRotationVector2() * radius;
                if (locked) {
                    Main.EntitySpriteDraw(dot, p, null, haloColor, 0f, dot.Size() / 2f, dotScale * 1.8f, SpriteEffects.None, 0);
                }
                Main.EntitySpriteDraw(dot, p, null, dotColor, 0f, dot.Size() / 2f, dotScale, SpriteEffects.None, 0);
            }
        }

        #endregion
    }
}
