using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OAF = CalamityOverhaul.Content.LegendWeapon.Onikiris.OniAnnihilates.OniAnnihilateFieldRenderer;
using OFR = CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniAnnihilates
{
    /// <summary>
    /// 鬼哭·灭世一闪主控：整场演出的时间轴导演，自身无判定。<br/>
    /// 与「终之太刀」的乱舞美学相对，这一招是"过程压抑、一次倾泻"：<br/>
    /// 分镜（60fps，约 2.5 秒）：<br/>
    /// 展开(0~14) 时停落下、脚下血渊弹开、暗场浸入 → 蓄力(8~78) 极点核心随三段
    /// 脉冲(24/46/64)阶梯增长、血气碎晶从领域缘吸入核心、领域上方墨流升腾、
    /// 压暗加深、末段径向模糊悄悄爬入 → 死寂收束(78~85) 声音全断、墨流减速、
    /// 核心反向缩成刺目极小白点、领域向中心抽干、85 帧负片闪 →
    /// 爆发(86) 巨浪月牙刃面压过刀线铺满画面、整屏暖白一瞬、径向模糊连推十余帧、
    /// 时停解除、伤害单次巨额结算、操控交还 → 收势(86~150) 画面回落、领域抽干殆尽。<br/>
    /// 时停走 <see cref="CWRWorld.TimeFrozenTick"/>（终之太刀同款）：逐帧刷新、
    /// 自带衰减兜底，主控意外死亡下一帧世界自动解冻。<br/>
    /// 蓄力期玩家站桩锁定（时停中无风险），领域世界锚定在触发瞬间的脚下。<br/>
    /// ai[0]=瞄准角(弧度，决定巨斩刀线) ai[1]=尺寸倍率；伤害经 damage 全额传入巨斩
    /// </summary>
    internal class OniAnnihilate : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable, ICrimsonFarDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //==== 时间轴常量 ====
        /// <summary>领域展开帧数</summary>
        public const int OpenFrames = 14;
        /// <summary>死寂收束起点</summary>
        public const int SilenceStart = 78;
        /// <summary>负片闪帧</summary>
        public const int NegativeFrame = 85;
        /// <summary>爆发帧 = 时停停刷帧 = 操控交还帧</summary>
        public const int BurstFrame = 86;
        /// <summary>演出总时长</summary>
        public const int TotalDuration = 150;

        /// <summary>蓄力脉冲排拍：每拍一波血气吸入 + 核心弹升 + 低太鼓</summary>
        private static readonly int[] PulseBeats = [24, 46, 64];

        /// <summary>领域半长轴(px)，压扁率见 <see cref="OAF.Squash"/></summary>
        private const float FieldHalfX = 230f;
        /// <summary>径向模糊爬入起点</summary>
        private const int BlurCreepStart = 56;
        /// <summary>核心峰值半径(px)：像素小人 ~48px 高，峰值几乎盖住角色</summary>
        private const float CorePeakRadius = 60f;

        private bool initialized;
        private int timer;
        private Vector2 fieldCenter;    //脚下椭圆中心（触发瞬间世界锚定）
        private Vector2 lockPos;        //蓄力站桩锁定位置
        private float seed;
        private float flowTime;         //外部积分的流动时间（死寂段减速）
        private float pulseFlash;       //脉冲闪包络（推高速落）
        private float corePulseKick;    //核心脉冲弹升增量（缓落）
        private int pulseIndex;

        private float Aim => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;
        private Player Owner => Main.player[Projectile.owner];
        /// <summary>极点核心（玩家中心，站桩锁定后与领域中心同轴）</summary>
        private Vector2 CoreCenter => fieldCenter - new Vector2(0f, Owner.height * 0.5f);
        /// <summary>蓄力总进度 0..1</summary>
        private float ChargeT => MathHelper.Clamp((timer - 8) / (float)(SilenceStart - 8), 0f, 1f);
        private float HalfX => FieldHalfX * SizeMul;

        /// <summary>
        /// 触发接口（调试入口）：在持有者客户端调用（<c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步；整场演出由主控自驱，调用方无需后续干预。
        /// 同一玩家已有演出进行中时忽略并返回 null
        /// </summary>
        /// <param name="player">攻击发起者（领域锚定其触发瞬间的脚下）</param>
        /// <param name="focus">演出焦点（世界坐标，主控弹幕生成位置）</param>
        /// <param name="aim">瞄准方向（无需归一化，决定爆发巨斩的刀线角度）</param>
        /// <param name="damage">伤害（爆发帧单次巨额结算，倍率由调用方控制）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率（领域/核心/巨斩随之缩放）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 focus, Vector2 aim, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<OniAnnihilate>()] > 0) {
                return null;
            }
            source ??= player.GetSource_Misc("CWR_OniAnnihilate");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX * player.direction).ToRotation();
            return Projectile.NewProjectileDirect(source, focus, Vector2.Zero
                , ModContent.ProjectileType<OniAnnihilate>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai1: scale);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;   //主控无判定，伤害全在爆发巨斩
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            fieldCenter = Owner.Bottom;
            lockPos = Owner.position;
            seed = Projectile.identity * 0.6180339887f % 1f;
            flowTime = seed * 13f;   //随机初相，重复触发花纹不同

            //开幕：低鸣沉底 + 墨水漫开，没有任何锐音——压抑从第一帧开始
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.85f, Volume = 0.65f }, fieldCenter);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.90f, Volume = 0.35f }, fieldCenter);
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.80f, Volume = 0.55f }, fieldCenter);
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
            }
            timer++;

            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //时停：爆发帧前每帧刷新，之后停止——tick 自然衰减，世界恰在巨斩落下时苏醒
            if (timer <= BurstFrame) {
                CWRWorld.TimeFrozenTick = 2;
            }

            //流动时间积分：死寂段整体减速（墨流近停，世界屏息）
            float flowRate = timer < SilenceStart ? 1f
                : MathHelper.Lerp(1f, 0.12f, MathHelper.Clamp(
                    (timer - SilenceStart) / (float)(NegativeFrame - SilenceStart), 0f, 1f));
            flowTime += flowRate / 60f;

            pulseFlash *= 0.86f;
            corePulseKick *= 0.90f;

            //站桩锁定：蓄力全程持械低姿态，爆发帧交还操控
            if (timer <= BurstFrame) {
                HoldStance();
            }

            PushScreenState();
            RunTimeline();

            if (!Main.dedServ) {
                SpawnChargeParticles();
            }

            //领域与核心的常驻微光
            Lighting.AddLight(fieldCenter, new Vector3(0.65f, 0.13f, 0.11f));
            Lighting.AddLight(CoreCenter, new Vector3(0.95f, 0.24f, 0.17f) * (0.4f + ChargeT));
        }

        /// <summary>站桩姿态：位置钉死、低姿态持械，读作"正在把血气压进刀里"</summary>
        private void HoldStance() {
            Owner.position = lockPos;
            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            Owner.GivePlayerImmuneState(10);

            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            float cos = MathF.Cos(Aim);
            if (MathF.Abs(cos) >= 0.05f) {
                Owner.ChangeDir(cos > 0f ? 1 : -1);
            }
            //刀尖压向斜下的固定蓄势角：低姿态纳气，与瞄准无关
            Owner.itemRotation = 0.55f * Owner.direction;
        }

        //==================== 时间轴 ====================

        /// <summary>帧事件：脉冲排拍、死寂、负片闪、爆发</summary>
        private void RunTimeline() {
            if (pulseIndex < PulseBeats.Length && timer == PulseBeats[pulseIndex]) {
                TriggerPulse();
            }

            //死寂中一丝极轻的高频吸气，暗示屏息
            if (timer == BurstFrame - 2) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.95f, Volume = 0.22f }, CoreCenter);
            }

            if (timer == NegativeFrame) {
                OniFinaleFX.PushNegative(CoreCenter, 0.85f);
            }

            if (timer == BurstFrame) {
                Burst();
            }
        }

        /// <summary>蓄力脉冲：一波血气从领域缘吸入核心，太鼓逐拍上调</summary>
        private void TriggerPulse() {
            pulseIndex++;
            pulseFlash = 1f;
            corePulseKick += 0.22f;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.65f + pulseIndex * 0.14f,
                Volume = 0.60f,
            }, fieldCenter);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.25f + pulseIndex * 0.12f, Volume = 0.30f }, CoreCenter);
            Owner.CWR().GetScreenShake(1.6f + pulseIndex * 0.5f);

            if (Main.dedServ) {
                return;
            }
            //吸入波：领域缘一圈碎晶加速涌向极点
            for (int i = 0; i < 13; i++) {
                SpawnSuctionMote(0.42f, 0.62f);
            }
            for (int i = 0; i < 4; i++) {
                float theta = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = fieldCenter + new Vector2(MathF.Cos(theta) * HalfX, MathF.Sin(theta) * HalfX * OAF.Squash);
                Vector2 vel = (CoreCenter - pos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(6f, 10f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 110, 70)
                    , Main.rand.NextFloat(0.30f, 0.48f) * SizeMul)
                    ?.Configure(Main.rand.Next(12, 18), affectedByGravity: false);
            }
        }

        /// <summary>爆发：巨斩落下，攒了一整场的东西一次全倾泻</summary>
        private void Burst() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                OniAnnihilateCleave.Fire(Owner, CoreCenter, Aim
                    , Projectile.damage, Projectile.knockBack, SizeMul, Projectile.GetSource_FromAI());
            }

            if (Main.dedServ) {
                return;
            }
            //核心终末白闪：极点在这一帧被斩开
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(CoreCenter, Vector2.Zero
                , new Color(255, 240, 226), 1.3f * SizeMul);
        }

        //==================== 屏幕状态 ====================

        /// <summary>暗场包络 + Bloom 微光 + 径向模糊爬入（尖峰由巨斩自推）</summary>
        private void PushScreenState() {
            //压暗：浸入 → 蓄力缓深 → 死寂最深 → 爆发后停推自然回落
            float dim;
            if (timer < OpenFrames) {
                dim = 0.55f * timer / OpenFrames;
            }
            else if (timer < SilenceStart) {
                dim = MathHelper.Lerp(0.55f, 0.66f, (timer - OpenFrames) / (float)(SilenceStart - OpenFrames));
            }
            else if (timer <= BurstFrame + 6) {
                dim = MathHelper.Lerp(0.66f, 0.74f, MathHelper.Clamp(
                    (timer - SilenceStart) / (float)(BurstFrame - SilenceStart), 0f, 1f));
            }
            else {
                dim = -1f;
            }
            if (dim > 0f) {
                OniFinaleFX.PushDim(CoreCenter, dim);
            }

            if (timer >= 8 && timer <= BurstFrame + 10) {
                CrimsonImpactFX.PushAmbience(CoreCenter, 0.18f + 0.12f * ChargeT);
            }

            //模糊爬入：空间向极点塌陷的暗示，量级刻意压得很低
            if (timer >= BlurCreepStart && timer <= BurstFrame) {
                float creep = MathHelper.Lerp(0f, 0.025f
                    , (timer - BlurCreepStart) / (float)(BurstFrame - BlurCreepStart));
                OniAnnihilateFX.PushBlur(CoreCenter, creep);
            }
        }

        //==================== 环境粒子 ====================

        /// <summary>蓄力常驻粒子：细流吸入 + 领域后半升烟 + 时停悬浮碎屑；死寂段全部静止</summary>
        private void SpawnChargeParticles() {
            if (timer < 6 || timer >= SilenceStart) {
                return;
            }

            //细流吸入：持续一两缕，脉冲时的大波在 TriggerPulse
            if (timer % 4 == 0) {
                SpawnSuctionMote(0.26f, 0.40f);
            }

            //领域后半升起的墨缕（与 shader 墨流同源的粒子层）
            if (timer % 5 == 0) {
                Vector2 pos = fieldCenter + new Vector2(Main.rand.NextFloat(-0.72f, 0.72f) * HalfX
                    , -Main.rand.NextFloat(0.10f, 0.55f) * HalfX * OAF.Squash);
                Vector2 vel = new(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.4f, 1.0f));
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, Color.White
                    , Main.rand.NextFloat(0.06f, 0.11f) * SizeMul)
                    ?.Configure(Main.rand.Next(26, 40), new Color(120, 26, 34), new Color(30, 14, 22));
            }

            //时停悬浮碎屑：缓慢上浮的小晶片，帧住的世界里唯一还在动的尘埃
            if (timer % 9 == 0) {
                Vector2 pos = fieldCenter + new Vector2(Main.rand.NextFloat(-1.1f, 1.1f) * HalfX
                    , -Main.rand.NextFloat(0f, 90f) * SizeMul);
                PRTLoader.NewParticle<PRT_OniShard>(pos, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.5f))
                    , new Color(200, 82, 62), Main.rand.NextFloat(0.18f, 0.30f) * SizeMul)
                    ?.Configure(Main.rand.Next(40, 60), Main.rand.NextFloat(-0.06f, 0.06f)
                        , Main.rand.NextFloat(1.1f, 1.6f), affectedByGravity: false);
            }
        }

        /// <summary>一缕吸入碎晶：从领域缘出发、带微弱切向的向心加速，抵达即熄</summary>
        private void SpawnSuctionMote(float scaleMin, float scaleMax) {
            float theta = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = fieldCenter + new Vector2(MathF.Cos(theta) * HalfX, MathF.Sin(theta) * HalfX * OAF.Squash)
                * Main.rand.NextFloat(0.95f, 1.30f);
            Vector2 toCore = CoreCenter - pos;
            float dist = toCore.Length();
            Vector2 dir = (toCore / MathF.Max(dist, 1f)).RotatedBy(Main.rand.NextFloat(-0.28f, 0.28f));
            //PRT_OniShard 速度每帧 ×0.955：v0=dist/10 时 ~16 帧后累计位移≈dist，恰好收进核心
            PRTLoader.NewParticle<PRT_OniShard>(pos, dir * (dist / Main.rand.NextFloat(9f, 12f))
                , new Color(255, 120, 70), Main.rand.NextFloat(scaleMin, scaleMax) * SizeMul)
                ?.Configure(Main.rand.Next(14, 20), Main.rand.NextFloat(-0.15f, 0.15f)
                    , Main.rand.NextFloat(1.6f, 2.4f), affectedByGravity: false);
        }

        //==================== 领域状态合成 ====================

        /// <summary>领域单帧状态：展开 → 蓄力 → 死寂部分抽干 → 爆发抽干殆尽</summary>
        private OAF.FieldState ComposeFieldState() {
            float drain;
            if (timer < SilenceStart) {
                drain = 0f;
            }
            else if (timer <= BurstFrame) {
                //死寂：领域开始被向中心吸走——能量正在离开地面进入极点
                drain = 0.42f * OFR.SmoothStep01((timer - SilenceStart) / (float)(BurstFrame - SilenceStart));
            }
            else {
                drain = MathHelper.Lerp(0.42f, 1f, MathHelper.Clamp((timer - BurstFrame) / 9f, 0f, 1f));
            }

            return new OAF.FieldState {
                Expand = OFR.EaseOutBack(MathHelper.Clamp(timer / (float)OpenFrames, 0f, 1f)),
                Drain = drain,
                Pulse = pulseFlash,
                Charge = ChargeT,
                FlowTime = flowTime,
                Opacity = 1f,
                Seed = seed,
            };
        }

        //==================== 绘制 ====================
        //前半椭圆 → EndEntityDraw 弹幕扩展图元层（盖住脚面）；
        //后半椭圆 + 升腾墨流 → 玩家绘制前回调（画在身后）；核心极点 → 加色层

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>近端前半：实体层，玩家真的"站在领域里"</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized || timer > BurstFrame + 10) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OAF.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            OAF.FieldState state = ComposeFieldState();
            OAF.DrawField(device, fx, fieldCenter, HalfX, in state, +1f);
            OAF.EndDraw(device, pb, pr, pd);
        }

        /// <summary>远端后半 + 升腾墨流：玩家绘制前回调（<see cref="CrimsonFarLayerRender"/> 收集）</summary>
        void ICrimsonFarDrawable.DrawFarSlashes() {
            if (Main.dedServ || !initialized || timer > BurstFrame + 10) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OAF.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            OAF.FieldState state = ComposeFieldState();
            OAF.DrawField(device, fx, fieldCenter, HalfX, in state, -1f);

            //墨流强度：随蓄力升起，收束抽干时一同散尽
            float intensity = MathHelper.Clamp(0.25f + ChargeT * 0.75f, 0f, 1f) * (1f - state.Drain);
            if (intensity > 0.02f) {
                float s = SizeMul;
                Vector2 root = fieldCenter + new Vector2(0f, -HalfX * OAF.Squash * 0.30f);
                Span<OAF.StreamDef> streams = [
                    //主流：宽幅慢涌，领域呼吸的主体
                    new() { OffsetX = 0f, Width = 300f * s, Height = 185f * s, SeedOffset = 0.17f, IntensityMul = 1f },
                    //左右两股窄流：更快更碎，层间视差
                    new() { OffsetX = -96f * s, Width = 130f * s, Height = 150f * s, SeedOffset = 0.53f, IntensityMul = 0.75f },
                    new() { OffsetX = 88f * s, Width = 118f * s, Height = 165f * s, SeedOffset = 0.87f, IntensityMul = 0.70f },
                ];
                for (int i = 0; i < streams.Length; i++) {
                    OAF.DrawStream(device, fx, root, in streams[i], in state, intensity);
                }
            }
            OAF.EndDraw(device, pb, pr, pd);
        }

        /// <summary>加色层：蓄力极点核心——积蓄、脉冲弹升、死寂反向收缩成刺目白点</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Main.dedServ || !initialized || timer < 5 || timer > BurstFrame + 2) {
                return;
            }

            //---- 尺寸曲线 ----
            float baseR = 0.30f + 0.70f * OFR.EaseOutCubic(ChargeT);
            float breath = 1f + 0.05f * MathF.Sin(timer * 0.33f + seed * 9f);
            float collapse = 1f;
            float blinding = 0f;   //收束增亮：越缩越烫
            if (timer >= SilenceStart && timer <= BurstFrame) {
                float ct = (timer - SilenceStart) / (float)(BurstFrame - SilenceStart);
                collapse = MathHelper.Lerp(1f, 0.22f, ct * ct * ct);   //缓起陡收
                blinding = ct;
            }
            else if (timer > BurstFrame) {
                //爆发后两帧残光速灭（星爆敷层由巨斩接管）
                collapse = 0.22f * (1f - (timer - BurstFrame) / 3f);
            }
            float radius = CorePeakRadius * SizeMul * (baseR + corePulseKick) * breath * collapse;
            if (radius < 2f) {
                return;
            }
            float glowA = MathHelper.Clamp(0.55f + ChargeT * 0.35f + pulseFlash * 0.25f + blinding * 0.4f, 0f, 1.1f);
            Vector2 pos = CoreCenter - Main.screenPosition;

            //---- 外层绯红光斑（慢旋） ----
            if (OnikiriAssets.StarFlare01?.Value is Texture2D outer) {
                float s = radius * 2f / outer.Width * 1.9f;
                spriteBatch.Draw(outer, pos, null, new Color(255, 92, 56) * (glowA * 0.50f)
                    , timer * 0.017f + seed * 5f, outer.Size() * 0.5f, s, SpriteEffects.None, 0);
            }
            //---- 白热核（反向慢旋） ----
            if (OnikiriAssets.StarFlare02?.Value is Texture2D core) {
                float s = radius * 2f / core.Width;
                spriteBatch.Draw(core, pos, null, new Color(255, 214, 188) * (glowA * 0.85f)
                    , -timer * 0.023f + seed * 3f, core.Size() * 0.5f, s * 1.15f, SpriteEffects.None, 0);
                spriteBatch.Draw(core, pos, null, new Color(255, 246, 236) * glowA
                    , timer * 0.011f, core.Size() * 0.5f, s * 0.62f, SpriteEffects.None, 0);
                //死寂收束的刺目极点：反向增亮的小白心
                if (blinding > 0f) {
                    spriteBatch.Draw(core, pos, null, new Color(255, 252, 246) * MathHelper.Clamp(blinding * 1.2f, 0f, 1f)
                        , -timer * 0.05f, core.Size() * 0.5f, s * (0.30f + 0.25f * blinding), SpriteEffects.None, 0);
                }
            }
            //---- 十字闪芒：双臂反向缓旋，蓄力点的"镜头光"读法 ----
            if (OnikiriAssets.StarGlow01?.Value is Texture2D cross) {
                float armLen = radius * 2f / cross.Height * 2.6f * (1f + pulseFlash * 0.35f);
                float armA = glowA * 0.55f;
                spriteBatch.Draw(cross, pos, null, new Color(255, 170, 140) * armA, timer * 0.008f + seed
                    , cross.Size() * 0.5f, new Vector2(0.36f, armLen), SpriteEffects.None, 0);
                spriteBatch.Draw(cross, pos, null, new Color(255, 130, 92) * (armA * 0.8f)
                    , -timer * 0.006f + seed + MathHelper.PiOver2 * 0.92f
                    , cross.Size() * 0.5f, new Vector2(0.30f, armLen * 0.78f), SpriteEffects.None, 0);
            }
        }
    }
}
