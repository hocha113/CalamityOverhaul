using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using OAR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates.OniAnnihilateRenderer;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates
{
    /// <summary>
    /// 鬼哭·灭世一闪：按下即斩的瞬间水墨巨弧。<br/>
    /// 没有蓄力、没有时停、没有闪屏 —— 出生帧一声爆响，以玩家为曲率中心的
    /// 环绕巨月牙（弓背朝瞄准方向、跨度 ~206° 绕身扫开）两帧内揭开，身后
    /// 错帧跟两层淡墨残像（复笔读法）；刀身水墨化：墨分五色的密度台阶、
    /// 干笔飞白、暗侧洇边、起笔端散锋分叉，白热剃刀线仍贴锋利侧。<br/>
    /// 施展帧玩家身周同时炸开一圈泼墨罡气（黑红墨舌 + 冲击环 + 墨浪烟，
    /// 画在身后层，人从墨浪里劈出来），伤害沿可见弧带单次巨额结算。<br/>
    /// 判定为贴刀光的弧形折线带（蠕虫/阿瑞斯节段减伤惯例）。<br/>
    /// ai[0]=刀线角(弧度) ai[1]=尺寸倍率
    /// </summary>
    internal class OniAnnihilate : ModProjectile, IPrimitiveDrawable, ICrimsonFarDrawable, IOverlayDrawable, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>伤害窗末帧</summary>
        private const int DamageEnd = 8;
        /// <summary>演出总时长</summary>
        private const int Lifetime = 46;
        /// <summary>施展摆臂帧数（只摆姿态，不锁位移不锁操控）</summary>
        private const int PoseFrames = 6;
        /// <summary>大挥后的残心余韵帧数(持刀停在收势位,末段淡出)</summary>
        private const int ZanshinFrames = 12;
        /// <summary>主弧 quad 半长轴(px)</summary>
        private const float ArcHalfX = 760f;
        /// <summary>主弧 quad 半短轴(px)（略压扁的滚转透视）</summary>
        private const float ArcHalfY = 690f;
        /// <summary>罡气舌数量</summary>
        private const int TongueCount = 10;

        private OFR.BladeDef arcDef;      //主弧：血墨挥毫的本体

        private bool initialized;
        private int timer;

        //罡气舌静态定义（出生帧生成，泼在触发瞬间的位置不追人）
        private readonly float[] tongueAngle = new float[TongueCount];
        private readonly float[] tongueLen = new float[TongueCount];
        private readonly float[] tongueHalfWidth = new float[TongueCount];
        private readonly float[] tongueSeed = new float[TongueCount];

        private float CutAngle => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;
        private Player Owner => Main.player[Projectile.owner];

        //大挥的实体刀:巨弧是这一挥的结果,不是凭空出现的天象
        private readonly OniBladePose bladePose = new();

        /// <summary>大挥+残心头段硬占刀权:连段就地冻结让位,10 帧后即恢复,"零后摇"身份不破</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => timer <= PoseFrames + 4;

        /// <summary>
        /// 触发接口：在持有者客户端调用（<c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步；按下即斩，调用方无需后续干预。
        /// 同一玩家已有巨斩进行中时忽略并返回 null
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="focus">刀线中心（世界坐标，一般传玩家中心）</param>
        /// <param name="aim">瞄准方向（无需归一化，决定巨斩的刀线角度）</param>
        /// <param name="damage">伤害（单次巨额结算，倍率由调用方控制）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率（巨弧/罡气随之缩放）</param>
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
                , ai0: MathHelper.WrapAngle(aimAngle), ai1: scale);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;   //伤害窗单次结算
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            float seed = Projectile.identity * 0.6180339887f % 1f;

            float cos = MathF.Cos(CutAngle);
            int facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : MathF.Sign(cos);
            Owner.ChangeDir(facingDir);
            float flip = facingDir;

            //主弧：quad 中心 = 玩家（曲率中心在人身上，读作"从人挥出去"），
            //Rot = 瞄准角 → 弓背朝瞄准方向鼓出；两帧揭开，真·一闪
            arcDef = new OFR.BladeDef {
                SweepFrames = 2, Life = Lifetime,
                ErodeStart = 10, ErodeFrames = 30,
                ColorShiftDelay = 12, ColorShiftFrames = 26,
                Mode = 0f, Rot = CutAngle, Span = 3.60f,
                Thick = 0.40f,
                HalfX = ArcHalfX * s, HalfY = ArcHalfY * s, Flip = flip,
                Opacity = 1f, FrontGlow = 2.2f, Seed = seed + 0.37f,
                TailErode = 0.35f, FlashPower = 1f,
                RazorTailWiden = 0.85f,
                Palette = OFR.BladePalette.Escalate(0.55f),
            };
            
            //罡气舌：黄金角均布 + 随机抖动，长短宽窄各异 —— 泼出去的墨不整齐
            const float GoldenAngle = 2.39996323f;
            for (int i = 0; i < TongueCount; i++) {
                tongueAngle[i] = MathHelper.WrapAngle(seed * MathHelper.TwoPi + i * GoldenAngle
                    + Main.rand.NextFloat(-0.15f, 0.15f));
                tongueLen[i] = Main.rand.NextFloat(190f, 280f) * s;
                tongueHalfWidth[i] = Main.rand.NextFloat(27f, 45f) * s;
                tongueSeed[i] = seed + i * 0.173f;
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                DetonateFx();
            }
            timer++;

            bladePose.Update();
            if (timer <= PoseFrames + ZanshinFrames && Owner.active && !Owner.dead) {
                ApplyCastPose();
            }

            float seam = MathF.Exp(-timer * 0.10f);
            Lighting.AddLight(Projectile.Center, new Vector3(1.35f, 0.55f, 0.32f) * seam * 1.5f);
        }

        /// <summary>
        /// 施展大挥：实体刀 6 帧内自肩后甩到收势位(挥动帧甩出角度残影),
        /// 随后残心停刀、末段淡出——巨弧读作这一挥的延伸。
        /// 只摆姿态不锁位移;itemTime 锁仅覆盖原摆臂窗,残心期操控完全自由
        /// </summary>
        private void ApplyCastPose() {
            int dir = MathF.Cos(CutAngle) >= 0f ? 1 : -1;
            float sw = OFR.EaseOutCubic(MathHelper.Clamp(timer / (float)PoseFrames, 0f, 1f));
            bladePose.Rotation = CutAngle + dir * MathHelper.Lerp(-2.0f, 0.55f, sw);

            if (timer <= PoseFrames) {
                Owner.itemTime = Owner.itemAnimation = 2;
                Owner.itemRotation = MathHelper.WrapAngle(CutAngle
                    + Owner.direction * MathHelper.Lerp(-0.9f, 0.45f, sw));
                bladePose.Opacity = 1f;
                if (timer >= 2) {
                    bladePose.PushSmear(1f);
                }
            }
            else {
                //残心:停在收势位,后段淡出;玩家续连段或另起技能时立刻放手
                if (OniBladeOccupancy.ComboClaims(Owner) || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile)) {
                    bladePose.Opacity = 0f;
                    return;
                }
                bladePose.Opacity = 1f - MathHelper.Clamp((timer - PoseFrames - 4f) / (ZanshinFrames - 4f), 0f, 1f);
            }
            bladePose.ApplyPose(Owner, Projectile);
        }

        /// <summary>遮挡层：大挥实体刀与其残影,稳定盖在巨弧与罡气之上</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        /// <summary>出生帧：全部声画一次砸下</summary>
        private void DetonateFx() {
            //爆响复合：低爆垫底、布帛撕裂、高频刀鸣、太鼓落点
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.30f, Volume = 1f }, Projectile.Center);
            //SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.50f, Volume = 0.85f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.50f, Volume = 0.90f }, Projectile.Center);
            //SoundEngine.PlaySound(CWRSound.KatanaA, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.KatanaSwing, Projectile.Center);

            if (Main.dedServ) {
                return;
            }

            //Bloom 冲击降档保留（不是闪屏）：轻微拉丝 + 环境辉光
            CrimsonImpactFX.PushImpact(Projectile.Center, 0.20f);
            CrimsonImpactFX.PushAmbience(Projectile.Center, 0.35f);

            Vector2 perp = (CutAngle + MathHelper.PiOver2).ToRotationVector2();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                , perp, 15f, 9f, 24, -1f, FullName));

            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(Projectile.Center, Vector2.Zero
                , new Color(255, 236, 216), 1.7f * SizeMul);

            SpawnBurstParticles();
        }

        /// <summary>泼墨罡气的粒子敷层：墨浪烟横推 + 上涌、墨滴飞溅、绯红火花点缀</summary>
        private void SpawnBurstParticles() {
            float s = SizeMul;
            Vector2 feet = Owner.active ? Owner.Bottom : Projectile.Center;

            //墨浪烟：从脚下向外横推的尘浪
            for (int i = 0; i < 16; i++) {
                float dir = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = feet + new Vector2(dir * Main.rand.NextFloat(6f, 30f) * s, -Main.rand.NextFloat(0f, 14f));
                Vector2 vel = new(dir * Main.rand.NextFloat(4f, 9f), -Main.rand.NextFloat(0.2f, 1.1f));
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, Color.White
                    , Main.rand.NextFloat(0.10f, 0.17f) * s)
                    ?.Configure(Main.rand.Next(26, 40), new Color(70, 18, 26), new Color(18, 8, 14));
            }
            //少量竖直上涌：罡气立起来的那几缕
            for (int i = 0; i < 6; i++) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-26f, 26f) * s, Main.rand.NextFloat(-10f, 16f));
                Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.4f, 2.6f));
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, Color.White
                    , Main.rand.NextFloat(0.09f, 0.14f) * s)
                    ?.Configure(Main.rand.Next(30, 44), new Color(60, 16, 24), new Color(16, 8, 14));
            }
            //墨滴飞溅：AlphaBlend 暗墨圆滴，抛物甩出（加色画不了黑，专用墨滴粒子）
            for (int i = 0; i < 14; i++) {
                Vector2 vel = (Main.rand.NextFloat(MathHelper.TwoPi)).ToRotationVector2()
                    * Main.rand.NextFloat(5f, 13f);
                vel.Y -= Main.rand.NextFloat(0f, 2.5f);
                PRTLoader.NewParticle<PRT_OniInkDrop>(Projectile.Center, vel, new Color(60, 14, 20)
                    , Main.rand.NextFloat(0.30f, 0.55f) * s)
                    ?.Configure(Main.rand.Next(22, 36));
            }
            //绯红火花点缀：纯黑会闷，留一点能量感
            for (int i = 0; i < 10; i++) {
                Vector2 vel = (Main.rand.NextFloat(MathHelper.TwoPi)).ToRotationVector2()
                    * Main.rand.NextFloat(4f, 11f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center, vel, new Color(255, 120, 70)
                    , Main.rand.NextFloat(0.4f, 0.7f) * s)
                    ?.Configure(Main.rand.Next(18, 30), affectedByGravity: true);
            }
        }

        //==================== 判定 ====================

        public override bool? CanHitNPC(NPC target) {
            if (timer > DamageEnd) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        /// <summary>巨物减伤（参照村正处刑斩）：蠕虫节体 0.2，阿瑞斯节段 0.4</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.2f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.4f;
            }
        }

        /// <summary>贴刀光的弧形判定：沿主弧带中线取 24 段折线逐段检测，刀光画到哪打到哪</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized) {
                return false;
            }
            const int Segments = 24;
            OFR.BladeState state = OFR.ComputeState(in arcDef, Math.Max(timer, 1));
            float cp = 0f;
            Vector2 prev = OFR.PointAt(in arcDef, in state, Projectile.Center, 0f);
            for (int i = 1; i <= Segments; i++) {
                Vector2 next = OFR.PointAt(in arcDef, in state, Projectile.Center, i / (float)Segments);
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , prev, next, 220f * SizeMul, ref cp)) {
                    return true;
                }
                prev = next;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(CWRSound.KatanaHit with { Pitch = 0.15f, Volume = 1.2f }, target.Center);

            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(target.Center, Vector2.Zero
                , new Color(255, 222, 198), 1.2f);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = CutAngle.ToRotationVector2().RotatedByRandom(0.55) * Main.rand.NextFloat(5f, 13f);
                PRTLoader.NewParticle<PRT_OniShard>(target.Center, vel, new Color(255, 132, 76)
                    , Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.25f, 0.25f)
                        , Main.rand.NextFloat(1.5f, 2.6f), affectedByGravity: true);
            }
        }

        //==================== 水墨旋钮时间轴 ====================

        /// <summary>主弧水墨旋钮：墨阶恒强，飞白/洇边随侵蚀期加深，散锋常备</summary>
        private OAR.InkParams ComposeInk() {
            float erodeT = MathHelper.Clamp((timer - 8) / 26f, 0f, 1f);
            return new OAR.InkParams {
                InkStep = 0.85f,
                FeiBai = 0.30f + 0.55f * erodeT,
                Bleed = MathHelper.Clamp((timer - 8) / 22f, 0f, 1f),
                SplitTail = 0.90f,
            };
        }

        //==================== 绘制 ====================

        /// <summary>主弧 + 残像：实体扩展图元层（EndEntityDraw，盖在实体之上）</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OAR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }

            OFR.BladeState arcState = OFR.ComputeState(in arcDef, timer);
            if (arcState.Opacity > 0.012f) {
                OAR.InkParams ink = ComposeInk();
                OAR.DrawBladeLayers(device, fx, in arcDef, in arcState, Projectile.Center, in ink);
            }
            OAR.EndDraw(device, pb, pr, pd);
        }

        /// <summary>泼墨罡气：玩家身后层（<see cref="CrimsonFarLayerRender"/> 收集），
        /// 冲击环垫底、墨舌盖上，人从墨浪里劈出来</summary>
        void ICrimsonFarDrawable.DrawFarSlashes() {
            if (Main.dedServ || !initialized || timer > 20) {
                return;
            }

            DrawBurstRings();

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OAR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            //舌根锚在触发点（泼出去的墨不追人）
            float extend = OFR.EaseOutCubic(MathHelper.Clamp(timer / 7f, 0f, 1f));
            float dissolve = MathHelper.Clamp((timer - 6) / 10f, 0f, 1f);
            float intensity = 1f - 0.30f * dissolve;
            for (int i = 0; i < TongueCount; i++) {
                OAR.DrawTongue(device, fx, Projectile.Center, tongueAngle[i]
                    , tongueLen[i] * extend, tongueHalfWidth[i]
                    , tongueSeed[i], dissolve, intensity, 1f);
            }
            OAR.EndDraw(device, pb, pr, pd);
        }

        /// <summary>冲击环双层：暗墨环（AlphaBlend）+ 绯红缘环（加色、略超前）</summary>
        private void DrawBurstRings() {
            if (OnikiriAssets.Ring01?.Value is not Texture2D ring) {
                return;
            }
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            SpriteBatch sb = Main.spriteBatch;

            //暗墨环：泼出去的那圈墨
            float darkT = MathHelper.Clamp(timer / 12f, 0f, 1f);
            if (darkT < 1f) {
                float ease = 1f - MathF.Pow(1f - darkT, 3f);
                float scale = MathHelper.Lerp(0.4f, 2.6f, ease) * SizeMul;
                float alpha = 0.5f * MathF.Pow(1f - darkT, 1.2f);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(ring, screenPos, null, new Color(28, 12, 18) * alpha, 0f
                    , ring.Size() * 0.5f, scale, SpriteEffects.None, 0);
                sb.End();
            }

            //绯红缘环：墨圈外沿的一线燃边，略超前
            float rimT = MathHelper.Clamp(timer / 10f, 0f, 1f);
            if (rimT < 1f) {
                float ease = 1f - MathF.Pow(1f - rimT, 3f);
                float scale = MathHelper.Lerp(0.55f, 2.9f, ease) * SizeMul;
                float alpha = 0.45f * MathF.Pow(1f - rimT, 2f);
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(ring, screenPos, null, new Color(255, 90, 50) * alpha, 0f
                    , ring.Size() * 0.5f, scale, SpriteEffects.None, 0);
                sb.End();
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
