using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 黑闪黑洞弹体，两拍两种性格：<br/>
    /// 开幕拍：慢起步复合加速直线掷向锚点（预告即承诺，不追踪），飞行期引力井拉拽玩家（公平阀：牵引朝向分速度封顶），
    /// 到锚/寿终→坍缩预兆→黑闪爆点（伤害窗与可见冲击环严格同半径）→余辉。<br/>
    /// 残血底牌拍（ai[2]=1）：极速起手指数减速到锚点附近，之后不停下，转为缓慢追踪玩家；出手后体量慢慢长到 3.5 倍，
    /// 引力从弱到强，事件视界随之从零长到满，界内没有逃逸阀；透镜扭曲随体量与引力一路加深，后段叠出自旋涡流；
    /// 爆点秒杀圈特别大，圈外一段距离的玩家被冲击波抛飞半屏；全程持有死寂（<see cref="MLordSilence"/>），爆点一声放回。<br/>
    /// 暗核真 alpha 遮挡 + 红黑电弧加色缘；引力透镜走 Warp 层。timeLeft 经 ExtraAI 随包同步，各端阶段一致
    /// </summary>
    internal class MLordBlackHoleProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //―――― 时间轴（timeLeft 递减制）――――
        internal const int FlightLife = 180;
        /// <summary>残血底牌拍飞行帧：一秒极速减速到锚点附近，其后三秒半缓慢追踪，体量、引力、扭曲一路涨到顶</summary>
        internal const int DesperateFlightLife = 270;
        internal const int CollapseLife = 16;
        internal const int FlashLife = 14;
        internal const int LingerLife = 34;
        private const int TailLife = CollapseLife + FlashLife + LingerLife;
        internal const int TotalLife = FlightLife + TailLife;
        internal const int DesperateTotalLife = DesperateFlightLife + TailLife;

        //―――― 开幕拍公平阀（发射/拉拽/爆点逻辑真正读取的命名常量）――――
        /// <summary>出手初速（慢起步：给玩家读向时间）</summary>
        internal const float LaunchSpeed = 4.6f;
        /// <summary>复合加速倍率/帧</summary>
        private const float AccelRate = 1.0175f;
        /// <summary>速度上限</summary>
        private const float MaxSpeed = 14.5f;
        /// <summary>引力井作用半径 px</summary>
        private const float PullRadius = 780f;
        /// <summary>强拉半径（此内拉力最大）</summary>
        private const float HardPullRadius = 260f;
        /// <summary>被拉向洞的分速度封顶：低于它才施力，正常位移速度即可挣脱（逃逸阀）</summary>
        private const float EscapeTowardSpeedCap = 8f;
        /// <summary>出手后引力宽限帧：贴脸掷出不做无预警吸附（接触伤同吃此宽限）</summary>
        private const int GraceFrames = 20;
        /// <summary>黑洞本体接触判定半径（与可见暗核 φ 对齐：判定=视觉）</summary>
        private const float CoreRadius = 48f;
        /// <summary>开幕拍黑闪爆点最大半径：伤害窗逐帧取当前可见环半径，绝不超出</summary>
        internal const float DetonationRadius = 320f;
        /// <summary>飞行体绘制半径（终局大招的体量：暗核+吸积盘+电弧结构约 2.5 倍此值）</summary>
        private const float FlightBodyRadius = 76f;

        //―――― 残血底牌拍 ――――
        //惩罚态：爆点秒杀无上限伤害，豁免伤害侧公平契约。公平阀全部前置：出手宽限帧、逃逸阀随引力渐收、
        //事件视界四秒半里从零长到满、揉搓期打断窗、秒杀圈外只抛飞不伤
        /// <summary>出手速度保留率/帧：极速起手→指数减速→交给缓慢追踪</summary>
        private const float DesperateDecay = 0.935f;
        /// <summary>出手初速下限/上限 px/f（按锚距反推，减速到追踪速度时恰在锚点附近）</summary>
        private const float DesperateLaunchMin = 26f;
        private const float DesperateLaunchMax = 64f;
        /// <summary>减速后的缓慢追踪速度 起→终 px/f：黑洞不会停下，它慢慢朝你漂过来（远低于玩家常态移速）</summary>
        private const float DesperateTrackSpeedStart = 1.8f;
        private const float DesperateTrackSpeedEnd = 2.8f;
        /// <summary>追踪转向的平滑系数/帧：越小越像有质量的东西在改向</summary>
        private const float DesperateTrackSteer = 0.045f;
        /// <summary>高速段每帧最大转向弧度（一点追踪，约 0.6°/帧）</summary>
        private const float DesperateHomingTurn = 0.011f;
        /// <summary>体量增长帧数：出手后慢慢长到 <see cref="DesperateMaxScale"/> 倍</summary>
        private const int DesperateGrowFrames = 200;
        internal const float DesperateMaxScale = 3.5f;
        /// <summary>引力井半径 起→终 px</summary>
        private const float DesperatePullRadiusStart = 620f;
        private const float DesperatePullRadiusEnd = 1500f;
        /// <summary>事件视界半径 起→终 px：界内无逃逸阀，拉力全开并逐帧抹掉外逸分速度</summary>
        private const float DesperateHorizonStart = 40f;
        private const float DesperateHorizonEnd = 400f;
        /// <summary>界外拉力 起→终 px/f²</summary>
        private const float DesperatePullStart = 0.05f;
        private const float DesperatePullEnd = 0.62f;
        /// <summary>界内拉力倍率</summary>
        private const float DesperateHorizonPullMul = 2.2f;
        /// <summary>界内外逸分速度每帧抹除比例</summary>
        private const float DesperateHorizonDrag = 0.08f;
        /// <summary>逃逸阀（朝洞分速度封顶）起→终：越到后段越难挣脱</summary>
        private const float DesperateEscapeCapStart = 8f;
        private const float DesperateEscapeCapEnd = 2.5f;
        /// <summary>残血爆点秒杀半径 px（可见冲击环半径即秒杀半径）</summary>
        internal const float DesperateDetonationRadius = 760f;
        /// <summary>冲击波抛飞外沿 px：秒杀圈外到此距离的玩家被炸飞</summary>
        internal const float DesperateKnockRadius = 1800f;
        /// <summary>冲击波前沿从爆心扫到抛飞外沿的总帧数（与 PushStarRing 的可见环同曲线）</summary>
        private const int DesperateShockFrames = FlashLife + 22;
        /// <summary>抛飞初速 内沿→外沿 px/f（空中自然减速下约飞半屏）</summary>
        private const float DesperateKnockInner = 26f;
        private const float DesperateKnockOuter = 14f;
        /// <summary>秒杀伤害：穿透一切防御与免伤</summary>
        private const int DesperateLethalDamage = 9999;
        /// <summary>爆点巨响的全音量范围 px：范围内不做距离衰减</summary>
        private const float DesperateBoomFullRange = 4000f;

        /// <summary>飞行帧计数（本地推进，仅表现与宽限判断用）</summary>
        private ref float FlightTimer => ref Projectile.localAI[0];
        /// <summary>已越过锚点后的累计位移 px（开幕拍服务端提前引爆判据）</summary>
        private ref float PassedDist => ref Projectile.localAI[1];
        /// <summary>残血爆点：本地玩家已被秒杀/已被抛飞（各只结算一次）</summary>
        private bool blastLethalDone;
        private bool blastKnockDone;

        private Vector2 Anchor => new(Projectile.ai[0], Projectile.ai[1]);
        /// <summary>残血底牌拍标记（ai[2]，随生成包同步）</summary>
        private bool Desperate => Projectile.ai[2] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
            //同材质拖尾缓存（契约5：飞行弹必须有可读尾迹）
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = (int)(CoreRadius * 2f);
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>残血拍飞得更久：生成端改写寿命，随包经 ExtraAI 下发</summary>
        public override void OnSpawn(IEntitySource source) {
            if (Desperate) {
                Projectile.timeLeft = DesperateTotalLife;
            }
        }

        //timeLeft 不在原版同步包里：两拍寿命不同、开幕拍服务端过锚改写寿命，都靠这里各端对齐
        public override void SendExtraAI(BinaryWriter writer) => writer.Write(Projectile.timeLeft);
        public override void ReceiveExtraAI(BinaryReader reader) => Projectile.timeLeft = reader.ReadInt32();

        #region 阶段判定（timeLeft 同步，各端一致）
        private bool InFlight => Projectile.timeLeft > TailLife;
        private bool InCollapse => !InFlight && Projectile.timeLeft > FlashLife + LingerLife;
        private bool InFlash => !InFlight && !InCollapse && Projectile.timeLeft > LingerLife;
        private bool InLinger => Projectile.timeLeft <= LingerLife;
        /// <summary>飞行进度 0~1</summary>
        private float FlightT => InFlight
            ? MathHelper.Clamp(1f - (Projectile.timeLeft - TailLife) / (float)(Desperate ? DesperateFlightLife : FlightLife), 0f, 1f)
            : 1f;
        /// <summary>坍缩进度 0~1</summary>
        private float CollapseT => InCollapse
            ? 1f - (Projectile.timeLeft - FlashLife - LingerLife) / (float)CollapseLife : (InFlight ? 0f : 1f);
        /// <summary>爆闪进度 0~1</summary>
        private float FlashT => InFlash ? 1f - (Projectile.timeLeft - LingerLife) / (float)FlashLife : (InLinger ? 1f : 0f);
        /// <summary>自爆闪首帧起的帧数（余辉期继续计）</summary>
        private int FlashElapsed => FlashLife + LingerLife - Projectile.timeLeft;
        /// <summary>本拍爆点最大半径</summary>
        private float DetonationRadiusNow => Desperate ? DesperateDetonationRadius : DetonationRadius;
        /// <summary>当前可见冲击环半径（伤害窗逐帧对齐它）</summary>
        private float FlashRingRadius => DetonationRadiusNow * VaultUtils.EaseOutCubic(FlashT);
        /// <summary>残血体量：出手后按飞行进度慢慢长到 <see cref="DesperateMaxScale"/> 倍，坍缩起保持满值（视觉、判定、引力场同一倍率）</summary>
        private float BodyScale {
            get {
                if (!Desperate) {
                    return 1f;
                }
                float t = MathHelper.Clamp(FlightT * DesperateFlightLife / DesperateGrowFrames, 0f, 1f);
                float eased = 0.5f - 0.5f * MathF.Cos(t * MathHelper.Pi);
                return MathHelper.Lerp(1f, DesperateMaxScale, eased);
            }
        }
        /// <summary>残血引力/扭曲爬升 0~1：前段弱、后段陡（飞行按进度幂次爬，坍缩起满值）</summary>
        private float GravityRamp => InFlight ? MathF.Pow(FlightT, 1.7f) : 1f;
        /// <summary>残血冲击波前沿半径：与 <see cref="MLordScreenEffects.PushStarRing"/> 的可见环同一条曲线（可见前沿=抛飞前沿）</summary>
        private float ShockFrontRadius => DesperateKnockRadius
            * VaultUtils.EaseOutCubic(MathHelper.Clamp(FlashElapsed / (float)DesperateShockFrames, 0f, 1f));
        #endregion

        /// <summary>残血拍出手初速：按锚距反推，指数减速后总程 ≈ 锚距（略过一点再被追踪拉回）</summary>
        internal static float DesperateLaunchSpeedFor(float distToAnchor) {
            return MathHelper.Clamp(distToAnchor * (1f - DesperateDecay) * 1.05f, DesperateLaunchMin, DesperateLaunchMax);
        }

        /// <summary>残血拍出手后 frames 帧的累计位移（掌中球交棒外推同用此曲线）</summary>
        internal static float DesperateTravel(float launchSpeed, int frames) {
            return launchSpeed * (1f - MathF.Pow(DesperateDecay, frames)) / (1f - DesperateDecay);
        }

        public override void AI() {
            FlightTimer++;

            if (Desperate) {
                DesperateAI();
            }
            else {
                OpenerAI();
            }

            Lighting.AddLight(Projectile.Center, MLordDirector.BlackFlashRed.ToVector3() * 0.5f * BodyScale * (1f - FlashT * 0.5f));
        }

        #region 开幕拍

        private void OpenerAI() {
            if (InFlight) {
                UpdateFlight();
            }
            else if (InCollapse) {
                //坍缩预兆：停摆 + 收缩（变小再变响）
                Projectile.velocity *= 0.82f;
                if (Projectile.timeLeft == FlashLife + LingerLife + CollapseLife && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -1f }, Projectile.Center);
                }
            }
            else if (InFlash) {
                Projectile.velocity = Vector2.Zero;
                if (Projectile.timeLeft == FlashLife + LingerLife && !VaultUtils.isServer) {
                    FireFlashPresentation();
                }
            }
            else {
                //余辉：无判定的消散
                Projectile.velocity = Vector2.Zero;
            }
        }

        /// <summary>飞行：复合加速 + 引力拉拽 + 过锚提前引爆（服务端判据）</summary>
        private void UpdateFlight() {
            //复合加速：慢起步越飞越快（重量感=起步迟，威胁感=后段快）
            float speed = Projectile.velocity.Length();
            if (speed < MaxSpeed) {
                Projectile.velocity *= AccelRate;
            }

            //引力井：只拉本地玩家（动作权威在玩家本地），宽限期不吸
            Player local = Main.LocalPlayer;
            if (!VaultUtils.isServer && FlightTimer > GraceFrames && local.active && !local.dead) {
                Vector2 toHole = Projectile.Center - local.Center;
                float dist = toHole.Length();
                if (dist < PullRadius && dist > 30f) {
                    float strength = MathHelper.Lerp(0.07f, 0.36f,
                        MathHelper.Clamp(1f - (dist - HardPullRadius) / (PullRadius - HardPullRadius), 0f, 1f));
                    Vector2 pullDir = toHole.SafeNormalize(Vector2.Zero);
                    //逃逸阀：朝洞分速度低于封顶才施力，位移技/正常横移足以挣脱
                    if (Vector2.Dot(local.velocity, pullDir) < EscapeTowardSpeedCap) {
                        local.velocity += pullDir * strength;
                    }
                }
            }

            //过锚判据（服务端权威）：越过锚点后再飞 140px 即入坍缩，timeLeft 改写随包同步
            if (!VaultUtils.isClient) {
                if (Vector2.Dot(Projectile.velocity, Anchor - Projectile.Center) < 0f) {
                    PassedDist += Projectile.velocity.Length();
                    if (PassedDist > 140f) {
                        Projectile.timeLeft = TailLife;
                        Projectile.netUpdate = true;
                    }
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            EmitFlightParticles(1f);
        }

        /// <summary>吸积星尘 + 缘弧迸溅：红黑材质，取位随体量同倍外扩，密度随 density 上量</summary>
        private void EmitFlightParticles(float density) {
            //吸积：周边星尘被拉进洞
            int accrete = (int)density + (Main.rand.NextFloat() < density - (int)density ? 1 : 0);
            for (int i = 0; i < accrete; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit()
                    * Main.rand.NextFloat(90f, 240f) * BodyScale;
                Vector2 pull = (Projectile.Center - pos) * 0.1f + Projectile.velocity * 0.4f;
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.VoidBlack, Main.rand.NextFloat(0.6f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, pull.RotatedBy(0.4f), c,
                    Main.rand.NextFloat(0.3f, 0.6f) * MathF.Sqrt(BodyScale))?.Configure(false, Main.rand.Next(10, 16));
            }
            //缘弧迸溅
            if (Main.rand.NextBool(7)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Unit() * CoreRadius * BodyScale * 1.3f,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        /// <summary>开幕拍黑闪爆点表现：冲击帧 + 屏效 + 红黑碎星（伤害窗同帧开启）</summary>
        private void FireFlashPresentation() {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f, Pitch = -0.45f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = 0.1f }, Projectile.Center);
            MLordScreenFX.Punch(Projectile.Center, 13f, 18);
            MLordBlackFlashFX.PushFlash(Projectile.Center);
            SpawnBurstDebris(26, 6, 1f, 1f);
        }

        #endregion

        #region 残血底牌拍

        private void DesperateAI() {
            if (InFlight) {
                UpdateDesperateFlight();
                if (VaultUtils.isServer) {
                    return;
                }
                float g = GravityRamp;
                ApplyDesperateGravity(g);
                //死寂由弹体接手持有：嗡鸣张力从掷出值接着爬到 1
                MLordSilence.Hold(MathHelper.Lerp(MLordBlackFlashState.SilenceTensionAtThrow, 1f, FlightT));
                //光被吸走：洞越大越黑
                MLordScreenEffects.PushGravityDim(Projectile.Center, 0.35f + 0.6f * g);
                EmitFlightParticles(1f + g * 2.5f);
                return;
            }
            if (InCollapse) {
                //坍缩预兆：停死 + 收缩，引力不松手（坍缩拍里不该有人逃出去）
                Projectile.velocity *= 0.82f;
                if (VaultUtils.isServer) {
                    return;
                }
                ApplyDesperateGravity(1f);
                MLordSilence.Hold(1f);
                MLordScreenEffects.PushGravityDim(Projectile.Center, 1f);
                return;
            }
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            if (InFlash) {
                if (Projectile.timeLeft == FlashLife + LingerLife) {
                    FireDesperateFlashPresentation();
                }
                ApplyDesperateBlast();
                return;
            }
            //余辉：抛飞前沿继续外扫；迟到的雷鸣压住尾巴
            ApplyDesperateBlast();
            if (Projectile.timeLeft == LingerLife - 14) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.9f, Pitch = -0.95f, MaxInstances = 0 }, BoomPosition());
            }
        }

        /// <summary>极速起手→指数减速→缓慢追踪，高速段也带一点转向；服务端定期校准（追踪读的是各端略有出入的玩家位）</summary>
        private void UpdateDesperateFlight() {
            float speed = Projectile.velocity.Length();
            Player target = NearestPlayer(Projectile.Center);
            float trackSpeed = MathHelper.Lerp(DesperateTrackSpeedStart, DesperateTrackSpeedEnd, FlightT);
            if (speed > trackSpeed + 0.6f) {
                Projectile.velocity *= DesperateDecay;
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    if (want != Vector2.Zero) {
                        float turned = Projectile.velocity.ToRotation().AngleTowards(want.ToRotation(), DesperateHomingTurn);
                        Projectile.velocity = turned.ToRotationVector2() * Projectile.velocity.Length();
                    }
                }
            }
            else if (target != null) {
                //缓慢追踪：速度收敛到追踪速度，方向平滑转向玩家（永不停下，也追不上正常移动的人）
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * trackSpeed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, DesperateTrackSteer);
            }
            if (!VaultUtils.isClient && (int)FlightTimer % 12 == 0) {
                Projectile.netUpdate = true;
            }
        }

        private static Player NearestPlayer(Vector2 from) {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float dist = Vector2.DistanceSquared(player.Center, from);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = player;
                }
            }
            return best;
        }

        /// <summary>
        /// 残血引力：只拉本地玩家。井半径、拉力、事件视界都随 g 爬升；界外保留逃逸阀（封顶随 g 收窄），
        /// 界内没有阀：拉力全开并逐帧抹掉外逸分速度，后段几乎无法逃离。出手宽限帧内不吸
        /// </summary>
        private void ApplyDesperateGravity(float g) {
            Player local = Main.LocalPlayer;
            if (FlightTimer <= GraceFrames || !local.active || local.dead) {
                return;
            }
            Vector2 toHole = Projectile.Center - local.Center;
            float dist = toHole.Length();
            if (dist < 30f) {
                return;
            }
            float pullRadius = MathHelper.Lerp(DesperatePullRadiusStart, DesperatePullRadiusEnd, g);
            if (dist >= pullRadius) {
                return;
            }
            float horizon = MathHelper.Lerp(DesperateHorizonStart, DesperateHorizonEnd, g);
            float basePull = MathHelper.Lerp(DesperatePullStart, DesperatePullEnd, g);
            Vector2 pullDir = toHole / dist;

            if (dist < horizon) {
                local.velocity += pullDir * basePull * DesperateHorizonPullMul;
                float outward = Vector2.Dot(local.velocity, -pullDir);
                if (outward > 0f) {
                    local.velocity += pullDir * outward * DesperateHorizonDrag;
                }
                return;
            }
            float falloff = MathF.Pow(MathHelper.Clamp(1f - (dist - horizon) / (pullRadius - horizon), 0f, 1f), 1.4f);
            float escapeCap = MathHelper.Lerp(DesperateEscapeCapStart, DesperateEscapeCapEnd, g);
            if (Vector2.Dot(local.velocity, pullDir) < escapeCap) {
                local.velocity += pullDir * basePull * falloff;
            }
        }

        /// <summary>巨响的声源：全音量范围内不定位（不吃距离衰减），更远才按位置衰减</summary>
        private Vector2? BoomPosition() {
            return Vector2.Distance(Projectile.Center, Main.LocalPlayer.Center) < DesperateBoomFullRange
                ? null : Projectile.Center;
        }

        /// <summary>
        /// 残血爆点：死寂在这一帧放行，五层低频巨响叠成一记；冲击帧强度 3.4 拉长扩散窗（涟漪列+内陷透镜扫全屏），
        /// 满幅震屏，抛飞前沿的可见环推到抛飞外沿，碎星与空间裂纹成倍
        /// </summary>
        private void FireDesperateFlashPresentation() {
            MLordSilence.Release();
            Vector2? at = BoomPosition();
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.85f, MaxInstances = 0 }, at);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.6f, MaxInstances = 0 }, at);
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1f, Pitch = -0.75f, MaxInstances = 0 }, at);
            SoundEngine.PlaySound(CWRSound.BlackHole with { Volume = 1f, Pitch = -0.55f, MaxInstances = 0 }, at);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.15f, MaxInstances = 0 }, at);

            MLordScreenFX.Punch(Projectile.Center, 34f, 44);
            Main.LocalPlayer.CWR()?.GetScreenShake(20f);
            MLordBlackFlashFX.PushFlash(Projectile.Center, 3.4f, 92);
            MLordScreenEffects.PushStarRing(Projectile.Center, 1.2f, DesperateKnockRadius, DesperateShockFrames);
            SpawnBurstDebris(90, 22, 2.1f, 1.6f);
        }

        /// <summary>
        /// 残血爆点结算（本地玩家，各一次）：可见冲击环扫到即秒杀（绕开通用无敌帧，防御与免伤都挡不住）；
        /// 秒杀圈外、抛飞前沿扫到的玩家被抛向远离爆心的方向（带抬升，约半屏距离），钩爪一并扯断
        /// </summary>
        private void ApplyDesperateBlast() {
            Player local = Main.LocalPlayer;
            if (!local.active || local.dead) {
                return;
            }
            float dist = DistanceToHitbox(local.Hitbox);

            if (InFlash && !blastLethalDone && dist <= FlashRingRadius) {
                blastLethalDone = true;
                blastKnockDone = true;
                local.hurtCooldowns[ImmunityCooldownID.Bosses] = 0;
                local.immune = false;
                local.Hurt(PlayerDeathReason.ByProjectile(-1, Projectile.whoAmI), DesperateLethalDamage,
                    Math.Sign(local.Center.X - Projectile.Center.X), cooldownCounter: ImmunityCooldownID.Bosses, knockback: 0f);
                return;
            }

            if (blastKnockDone || dist <= DesperateDetonationRadius || dist > ShockFrontRadius) {
                return;
            }
            blastKnockDone = true;
            float k = 1f - MathHelper.Clamp((dist - DesperateDetonationRadius)
                / (DesperateKnockRadius - DesperateDetonationRadius), 0f, 1f);
            float speed = MathHelper.Lerp(DesperateKnockOuter, DesperateKnockInner, k);
            Vector2 away = (local.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY);
            Vector2 fling = away * speed;
            if (fling.Y > -6f) {
                fling.Y = -6f;
            }
            local.RemoveAllGrapplingHooks();
            local.velocity = fling;
            MLordScreenFX.Punch(local.Center, 14f, 18, away);
        }

        /// <summary>爆心到矩形最近点的距离</summary>
        private float DistanceToHitbox(Rectangle hitbox) {
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, hitbox.Left, hitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, hitbox.Top, hitbox.Bottom));
            return Vector2.Distance(closest, Projectile.Center);
        }

        #endregion

        /// <summary>红黑碎星 + 空间裂纹</summary>
        private void SpawnBurstDebris(int starCount, int fractureCount, float velScale, float sizeScale) {
            for (int i = 0; i < starCount; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 13f) * velScale;
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.MoonWhite, Main.rand.NextFloat(0.35f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vel, c,
                    Main.rand.NextFloat(0.6f, 1.2f) * sizeScale)?.Configure(true, Main.rand.Next(20, 36));
            }
            for (int i = 0; i < fractureCount; i++) {
                PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f) * velScale,
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.9f, 1.4f) * sizeScale)
                    ?.Configure(Main.rand.Next(18, 28), Main.rand.NextFloat(-0.05f, 0.05f));
            }
        }

        /// <summary>判定：飞行/坍缩=本体暗核圆（随体量放大）；开幕爆闪=逐帧对齐可见冲击环；余辉无判定。
        /// 出手宽限帧内接触伤随引力一起豁免：贴脸掷出不做无预警判定（契约3）。
        /// 残血爆闪不走弹幕命中链：由 <see cref="ApplyDesperateBlast"/> 直判秒杀</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (InLinger) {
                return false;
            }
            if (InFlight && FlightTimer <= GraceFrames) {
                return false;
            }
            if (InFlash && Desperate) {
                return false;
            }
            float radius = InFlash ? FlashRingRadius : CoreRadius * BodyScale;
            return DistanceToHitbox(targetHitbox) <= radius;
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            //开幕爆闪窗吃满额外爆点伤害（长预告演出级的一锤）
            if (InFlash) {
                modifiers.SourceDamage *= MLordDirector.BlackFlashBurstDamage / (float)MLordDirector.BlackHoleContactDamage;
            }
        }

        #region 扭曲与绘制

        public bool DontUseBlueshiftEffect() => true;
        public bool CanDrawCustom() => false;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        public void Warp() {
            if (Desperate) {
                DesperateWarp();
                return;
            }
            //开幕拍引力透镜：飞行常驻，坍缩收紧，爆闪一记扩张脉冲
            float env;
            float size;
            if (InFlight) {
                env = MathHelper.Clamp(FlightTimer / 20f, 0f, 1f);
                size = 800f;
            }
            else if (InCollapse) {
                env = 1f;
                size = MathHelper.Lerp(800f, 420f, CollapseT);
            }
            else {
                float fade = InFlash ? 1f : 1f - Projectile.timeLeft / (float)LingerLife;
                env = 1f - fade * 0.85f;
                size = MathHelper.Lerp(420f, 1400f, VaultUtils.EaseOutCubic(FlashT));
            }
            if (env <= 0.04f) {
                return;
            }
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size, 0.38f * env, 1f, 0f, "GravitationalLens", 0.42f);
        }

        /// <summary>
        /// 残血扭曲：透镜场随体量与引力一路加深加宽（像黑洞一样越来越扭），后段叠出自旋涡流；
        /// 爆点透镜炸成大面积光线扭曲并在余辉期继续外扩，再叠一圈与抛飞前沿同步外扫的冲击波折射环
        /// </summary>
        private void DesperateWarp() {
            if (InFlight || InCollapse) {
                float g = GravityRamp;
                float env = MathHelper.Clamp(FlightTimer / 20f, 0f, 1f);
                float scaleK = BodyScale / DesperateMaxScale;
                float lensSize = MathHelper.Lerp(700f, 2600f, g) * (0.55f + 0.45f * scaleK);
                float lensStrength = MathHelper.Lerp(0.3f, 1f, g);
                if (InCollapse) {
                    lensSize = MathHelper.Lerp(lensSize, lensSize * 0.6f, CollapseT);
                    lensStrength = 1f;
                }
                NeutronWarpHelper.DrawWarp(Projectile.Center, lensSize, lensSize,
                    lensStrength * env, 1f, 0f, "GravitationalLens", 0.42f);
                float vortex = g * g * 0.85f;
                if (vortex > 0.03f) {
                    float vortexSize = lensSize * 0.75f;
                    NeutronWarpHelper.DrawWarp(Projectile.Center, vortexSize, vortexSize,
                        vortex * env, 1f, Main.GlobalTimeWrappedHourly * 0.6f, "GravitationalVortex", 0.42f);
                }
                return;
            }

            float fade = InFlash ? 1f : 1f - Projectile.timeLeft / (float)LingerLife;
            float lensEnv = 1f - fade * 0.85f;
            float size = MathHelper.Lerp(1600f, 7600f, VaultUtils.EaseOutCubic(FlashT));
            if (InLinger) {
                size += (1f - Projectile.timeLeft / (float)LingerLife) * 3400f;
            }
            if (lensEnv > 0.04f) {
                NeutronWarpHelper.DrawWarp(Projectile.Center, size, size, lensEnv, 1f, 0f, "GravitationalLens", 0.42f);
            }
            //冲击波折射环：ShockwaveRing 的波前在 progress=1 时落到 1.2×uRadius×边长=外沿，
            //progress 喂前沿同一条 EaseOutCubic，可见折射环=抛飞前沿
            float shockT = MathHelper.Clamp(FlashElapsed / (float)DesperateShockFrames, 0f, 1f);
            if (shockT < 1f) {
                float quad = DesperateKnockRadius / (1.2f * 0.42f);
                NeutronWarpHelper.DrawWarp(Projectile.Center, quad, quad,
                    1f - shockT * 0.5f, VaultUtils.EaseOutCubic(shockT), 0f, "ShockwaveRing", 0.42f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //残血拍体量随飞行长大（坍缩/爆闪的收缩终值同倍，节奏曲线不变）
            float scale = BodyScale;
            float bodyR = (InCollapse
                ? MathHelper.Lerp(FlightBodyRadius, 40f, CollapseT) : FlightBodyRadius) * scale;
            float bodyVis = InLinger ? Projectile.timeLeft / (float)LingerLife : 1f;
            if (InFlash) {
                bodyR = MathHelper.Lerp(40f, 18f, FlashT) * scale;
            }

            if (InFlight) {
                DrawTrail(bodyR);
            }
            DrawHoleBody(pos, bodyR, bodyVis * (1f - FlashT));
            if (FlashT > 0f) {
                DrawFlashRing(pos);
            }
            return false;
        }

        /// <summary>
        /// 同材质拖尾（契约5）：黑洞自己的暗核+红缘按 oldPos 重绘（0.55×、衰减 alpha），
        /// 读作洞体撕开空间留下的尾迹而非装饰光带
        /// </summary>
        private void DrawTrail(float bodyR) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) {
                return;
            }
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 oldPos = Projectile.oldPos[i];
                if (oldPos == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = oldPos + Projectile.Size * 0.5f - Main.screenPosition;
                float r = bodyR * 0.55f * (0.45f + 0.55f * k);
                float texScale = r * 2.6f / glow.Width;
                //红缘（加色）压底，暗核（真 alpha）叠上：与本体同层序同材质
                Main.EntitySpriteDraw(glow, pos, null,
                    MLordDirector.BlackFlashRed with { A = 0 } * (0.34f * k),
                    Main.GlobalTimeWrappedHourly * 1.5f + i * 0.3f, glow.Size() / 2f,
                    texScale * 1.25f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.55f * k),
                    -Main.GlobalTimeWrappedHourly + i * 0.3f, glow.Size() / 2f,
                    texScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>洞体：shader 量体（暗核+吸积盘+电弧），缺 shader 走 CPU 双层</summary>
        private void DrawHoleBody(Vector2 pos, float radius, float vis) {
            if (vis <= 0.02f) {
                return;
            }
            Effect shader = EffectLoader.MLordBlackFlash?.Value;
            if (shader != null) {
                Texture2D canvas = CWRUtils.GetT2DAsset(CWRConstant.VaultPlaceholder2).Value;
                float scale = radius * 5f / canvas.Width;
                //残血拍电弧随引力爬升越来越躁
                float arc = Desperate ? 0.7f + 0.3f * GravityRamp : 0.85f + CollapseT * 0.15f;
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uCollapse"]?.SetValue(0.9f + CollapseT * 0.1f);
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
                Main.spriteBatch.Draw(canvas, pos, null, Color.White, 0f,
                    canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                return;
            }

            //CPU 回退：暗核真 alpha + 红缘 + 斜吸积盘
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) {
                return;
            }
            float texScale = radius * 2.6f / glow.Width;
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.5f * vis),
                Main.GlobalTimeWrappedHourly * 1.5f, glow.Size() / 2f, texScale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.96f * vis),
                -Main.GlobalTimeWrappedHourly, glow.Size() / 2f, texScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.55f * vis),
                Main.GlobalTimeWrappedHourly * 2.4f, glow.Size() / 2f,
                new Vector2(texScale * 1.7f, texScale * 0.4f), SpriteEffects.None, 0);
        }

        /// <summary>爆闪冲击环：可见环半径即伤害半径（视觉=判定，一像素不差的承诺）</summary>
        private void DrawFlashRing(Vector2 pos) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return;
            }
            float ringR = FlashRingRadius;
            float fade = InLinger ? Projectile.timeLeft / (float)LingerLife : 1f;
            float ringScale = ringR * 2f / glow.Width;
            //白芯闪帧（只在爆闪窗内的短脉冲）；残血拍白芯放大三倍——爆心体量而非判定边界，不误读
            if (InFlash) {
                float coreScale = (0.6f + FlashT * 0.5f) * (Desperate ? 3f : 1f);
                Main.EntitySpriteDraw(star, pos, null, MLordDirector.MoonWhite with { A = 0 } * (0.9f * (1f - FlashT)),
                    Main.GlobalTimeWrappedHourly * 3f, star.Size() / 2f, coreScale, SpriteEffects.None, 0);
            }
            //红黑环体：外红缘 + 内暗吞（暗层真 alpha，把爆心咬出一圈黑）
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.85f * fade),
                0f, glow.Size() / 2f, ringScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.7f * fade * (1f - FlashT)),
                0f, glow.Size() / 2f, ringScale * 0.62f, SpriteEffects.None, 0);
        }

        #endregion
    }
}
