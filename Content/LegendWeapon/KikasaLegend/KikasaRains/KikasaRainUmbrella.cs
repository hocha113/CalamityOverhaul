using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 悬伞:普攻持有体。撑伞拍脱手上浮至头顶悬点绕柄自旋,
    /// 按住左键期间按节拍自伞缘甩出大墨滴(<see cref="KikasaInkDrop"/>),
    /// 每拍出手前有一记反向蓄势(与领域倒转同一套动作语法),松手收伞落回手中。
    /// 状态机由所有者的原版同步控制位驱动,各端自走,无自定义网络包;
    /// 蓄力倒撑形态(ai[0]=1)由重击模块接管
    /// </summary>
    internal class KikasaRainUmbrella : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 时序与几何 ====================

        /// <summary>撑伞上浮帧数</summary>
        public const int RiseFrames = 16;

        /// <summary>甩雨节拍周期</summary>
        public const int VolleyPeriod = 26;

        /// <summary>出手前反向蓄势帧数</summary>
        public const int WindupFrames = 4;

        /// <summary>每波墨滴数,错 2 帧甩出</summary>
        public const int DropsPerVolley = 3;

        /// <summary>收伞回手帧数</summary>
        public const int RecallFrames = 18;

        /// <summary>倒撑蓄满帧数,三档 30/60/90</summary>
        public const int ChargeFullFrames = 90;

        /// <summary>倾覆编舞:猛倾帧数/持续冲刷帧数/甩干回正帧数</summary>
        public const int PourTiltFrames = 5;
        public const int PourHoldFrames = 38;
        public const int PourShakeFrames = 12;

        /// <summary>悬点:玩家中心上方高度</summary>
        private const float HoverHeight = 92f;

        /// <summary>伞缘半径,墨滴的甩出点</summary>
        private const float RimRadius = 34f;

        /// <summary>光标搜敌半径与保底搜敌半径</summary>
        private const float CursorSeekRange = 520f;
        private const float FallbackSeekRange = 1100f;

        private enum UmbrellaState : byte { Rise, Hover, Recall, Flip, Pour }

        /// <summary>生成模式:0=墨雨,1=蓄力倒撑(重击模块接线)</summary>
        private ref float ModeAi => ref Projectile.ai[0];

        private UmbrellaState State {
            get => (UmbrellaState)Projectile.ai[1];
            set {
                if ((UmbrellaState)Projectile.ai[1] != value) {
                    Projectile.ai[1] = (float)value;
                    Projectile.ai[2] = 0f;
                    Projectile.netUpdate = true;
                }
            }
        }

        private ref float StateTimer => ref Projectile.ai[2];

        private Player Owner => Main.player[Projectile.owner];

        //表现状态:各端本地自走的连续量,不需同步
        private float spinPhase;
        private float spinSpeed;
        private float lean;
        private float bobPhase;
        private float visualScale;
        private Vector2 recallFrom;
        /// <summary>湿度:收伞时水膜退场</summary>
        private float wetness = 1f;
        /// <summary>节拍挤压:回拉蓄势时伞面绷紧</summary>
        private float beatSquash;
        /// <summary>甩雨后坐:出手拍向上一弹</summary>
        private float recoil;
        //鬼眼(锁定 telegraph 驱动)
        private float eyeOpen;
        private float eyeGlow;
        private Vector2 eyeLook = new(0f, 1f);

        /// <summary>伞面鬼眼锚点(帧内归一 uv)与半径,素材校准点</summary>
        private static readonly Vector2 EyeCenter = new(0.5f, 0.34f);
        private const float EyeRadius = 0.2f;

        /// <summary>当前视线目标,-1=无(眼睑垂着)</summary>
        private int gazeTarget = -1;
        private int blinkTimer = 150;

        //倒撑重击表现
        /// <summary>翻转进度 0=正撑 1=倒扣</summary>
        private float flipT;
        /// <summary>倾覆侧倾角(带符号)</summary>
        private float pourTilt;
        /// <summary>倾覆朝向:+1 右 -1 左</summary>
        private float pourDirSign = 1f;
        /// <summary>倾覆瞄准角(出手瞬间锁定,跟光标)</summary>
        private float pourAim = MathHelper.PiOver2;
        /// <summary>释放瞬间锁定的蓄力档</summary>
        private float pourFill;

        /// <summary>蓄墨水位(表现):Flip 期随蓄力涨,Pour 期排空</summary>
        private float ChargeFill => State switch {
            UmbrellaState.Flip => MathHelper.Clamp(StateTimer / (float)ChargeFullFrames, 0f, 1f),
            UmbrellaState.Pour => pourFill * (1f - MathHelper.Clamp(
                (StateTimer - PourTiltFrames) / (float)PourHoldFrames, 0f, 1f)),
            _ => 0f,
        };

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Player owner = Owner;
            if (owner?.active != true || owner.dead || owner.CCed
                || owner.HeldItem?.type != ModContent.ItemType<KikasaItem>()) {
                //持有条件破裂:人已不在持伞,直接谢幕不走收伞弧线
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;
            Projectile.velocity = Vector2.Zero;

            bobPhase += 0.07f;
            UpdateLean(owner);

            if (StateTimer == 0f && State == UmbrellaState.Rise) {
                //撑伞拍:伞骨闷扫+一层薄水
                KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.62f, -0.22f, 2);
                KikasaInk.Play(SoundID.SplashWeak, Projectile.Center, 0.4f, -0.15f, 2);
            }

            switch (State) {
                case UmbrellaState.Rise:
                    UpdateRise(owner);
                    break;
                case UmbrellaState.Hover:
                    UpdateHover(owner);
                    break;
                case UmbrellaState.Recall:
                    UpdateRecall(owner);
                    break;
                case UmbrellaState.Flip:
                    UpdateFlip(owner);
                    break;
                case UmbrellaState.Pour:
                    UpdatePour(owner);
                    break;
            }
            StateTimer++;
        }

        //==================== 状态推进 ====================

        private Vector2 HoverAnchor(Player owner)
            => owner.MountedCenter + new Vector2(owner.velocity.X * 2.2f,
                -HoverHeight + MathF.Sin(bobPhase) * 6f - recoil);

        private void UpdateRise(Player owner) {
            float t = MathHelper.Clamp(StateTimer / (float)RiseFrames, 0f, 1f);
            //EaseOutBack:上浮带过冲再回落定位
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float e = 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * (t - 1f) * (t - 1f);
            Projectile.Center = Vector2.Lerp(owner.MountedCenter, HoverAnchor(owner), e);
            visualScale = MathHelper.Lerp(0.55f, 1f, MathHelper.Clamp(t * 1.4f, 0f, 1f));

            //自旋从零加速起转
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.4f, 0.09f);
            spinPhase += spinSpeed;

            if (!Main.dedServ) {
                //撑满拍:一圈水膜甩出去
                if (StateTimer == 1f) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 dir = (MathHelper.TwoPi * i / 10f).ToRotationVector2();
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            Projectile.Center + dir * RimRadius * 0.5f,
                            dir * Main.rand.NextFloat(2.4f, 4f) - Vector2.UnitY * 1.2f,
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(16, 26));
                    }
                }
                //加速段甩出螺旋墨珠:旋转要被看见
                else if ((int)StateTimer % 2 == 0) {
                    float xOff = MathF.Cos(spinPhase) * RimRadius * visualScale;
                    Vector2 rim = Projectile.Center + new Vector2(xOff, 2f);
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(rim,
                        new Vector2(MathF.Sign(xOff) * Main.rand.NextFloat(1.5f, 3f), -Main.rand.NextFloat(0.5f, 1.5f)),
                        KikasaInk.InkBody, Main.rand.NextFloat(0.28f, 0.44f))?.Configure(Main.rand.Next(14, 22));
                }
            }

            if (!HoldingAttack(owner)) {
                BeginRecall();
                return;
            }
            if (StateTimer >= RiseFrames) {
                if (ModeAi > 0.5f) {
                    State = UmbrellaState.Flip;
                    //翻成倒扣:伞面一拧
                    KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.7f, -0.45f, 2);
                }
                else {
                    State = UmbrellaState.Hover;
                }
            }
        }

        private void UpdateHover(Player owner) {
            Projectile.Center = Vector2.Lerp(Projectile.Center, HoverAnchor(owner), 0.22f);
            visualScale = MathHelper.Lerp(visualScale, 1f, 0.2f);
            flipT = MathHelper.Lerp(flipT, 0f, 0.2f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.3f);
            UpdateEye(owner);

            if (!HoldingAttack(owner)) {
                BeginRecall();
                return;
            }

            //节拍:回拉蓄势 → 出手窗猛甩 → 回稳;每波滴数随域形态走
            int dropCount = VolleyDropCount(owner);
            int beat = (int)(StateTimer % VolleyPeriod);
            float targetSpin = beat < WindupFrames
                ? -0.22f
                : beat < WindupFrames + dropCount * 2 + 2 ? 0.74f : 0.4f;
            spinSpeed = MathHelper.Lerp(spinSpeed, targetSpin, 0.28f);
            spinPhase += spinSpeed;

            //蓄势绷紧、出手回弹
            beatSquash = MathHelper.Lerp(beatSquash, beat < WindupFrames ? 0.08f : 0f, 0.35f);
            recoil *= 0.8f;

            //伞缘闲滴:泡透的伞一直在滴
            if (!Main.dedServ && Main.rand.NextBool(24)) {
                float xOff = Main.rand.NextFloat(-1f, 1f) * RimRadius * visualScale;
                PRTLoader.NewParticle<PRT_KikasaInkDrip>(
                    Projectile.Center + new Vector2(xOff, 6f), Vector2.Zero,
                    KikasaInk.InkBody, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(24, 36));
            }

            if (beat == WindupFrames) {
                //出手拍:湿掌甩墨+向上后坐+眼睛燃一下+两粒随甩的碎珠
                recoil = 4.5f;
                eyeGlow = MathF.Max(eyeGlow, 0.5f);
                KikasaInk.Play(KikasaInk.InkFlick, Projectile.Center, 0.72f, 0.08f, 4);
                KikasaInk.Play(SoundID.SplashWeak, Projectile.Center, 0.42f, 0.12f, 4);
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        float xOff = MathF.Cos(spinPhase + i * 2.4f) * RimRadius * visualScale;
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            Projectile.Center + new Vector2(xOff, 3f),
                            new Vector2(MathF.Sign(xOff) * Main.rand.NextFloat(2f, 3.6f), -Main.rand.NextFloat(1f, 2.4f)),
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.3f, 0.46f))?.Configure(Main.rand.Next(14, 24));
                    }
                }
            }
            //出手窗:错 2 帧连甩
            if (beat >= WindupFrames && beat < WindupFrames + dropCount * 2
                && (beat - WindupFrames) % 2 == 0) {
                FireDrop((beat - WindupFrames) / 2);
            }
        }

        /// <summary>形态差异:血湖形态少而重、鬼雨形态密而细,域外基准三滴</summary>
        private int VolleyDropCount(Player owner) {
            KikasaDomainPlayer kdp = owner.GetModPlayer<KikasaDomainPlayer>();
            if (kdp.AnyActive) {
                return kdp.IsRainForm ? 5 : DropsPerVolley;
            }
            return DropsPerVolley;
        }

        private void UpdateRecall(Player owner) {
            float t = MathHelper.Clamp(StateTimer / (float)RecallFrames, 0f, 1f);
            //二次贝塞尔回手弧线,控制点在头顶侧上方
            Vector2 hand = owner.MountedCenter + new Vector2(owner.direction * 8f, -4f);
            Vector2 ctrl = (recallFrom + hand) * 0.5f + new Vector2(0f, -46f);
            float u = 1f - t;
            Projectile.Center = u * u * recallFrom + 2f * u * t * ctrl + t * t * hand;
            visualScale = MathHelper.Lerp(visualScale, 0.5f, 0.14f);
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.05f, 0.2f);
            spinPhase += spinSpeed;
            //水膜退场,眼睛合上,翻转回正
            wetness = MathHelper.Lerp(wetness, 0.15f, 0.12f);
            eyeOpen = MathHelper.Lerp(eyeOpen, 0f, 0.3f);
            flipT = MathHelper.Lerp(flipT, 0f, 0.22f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.3f);

            if (t >= 1f) {
                Projectile.Kill();
            }
        }

        //==================== 倒撑重击:蓄墨与倾覆 ====================

        /// <summary>
        /// 倒扣蓄墨:伞翻转成器皿,墨在伞里蓄积——
        /// "伞下无雨":你没淋过的雨都记在伞里,现在连本带利还给别人
        /// </summary>
        private void UpdateFlip(Player owner) {
            Vector2 anchor = HoverAnchor(owner) + new Vector2(0f, -12f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.2f);
            visualScale = MathHelper.Lerp(visualScale, 1f, 0.2f);

            //翻成倒扣,自旋慢下来——蓄力不是转出来的
            flipT = MathHelper.Lerp(flipT, 1f, 0.16f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.3f);
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.06f, 0.2f);
            spinPhase += spinSpeed;
            beatSquash = MathHelper.Lerp(beatSquash, 0f, 0.3f);
            recoil *= 0.8f;

            UpdateEye(owner);
            //蓄力越满眼睁越大,蓄势本身就是 telegraph
            float fill = ChargeFill;
            eyeOpen = MathF.Max(eyeOpen, fill * 0.85f);

            //三档换挡拍:一声比一声沉的水花,碗沿荡出一圈碎珠
            if (StateTimer == ChargeFullFrames / 3f || StateTimer == ChargeFullFrames * 2f / 3f
                || StateTimer == ChargeFullFrames) {
                float tier = StateTimer / (float)ChargeFullFrames;
                eyeGlow = MathF.Max(eyeGlow, 0.3f + 0.4f * tier);
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.48f + 0.28f * tier, -0.25f - 0.35f * tier, 3);
                KikasaInk.Play(SoundID.Item21, Projectile.Center, 0.32f + 0.22f * tier, -0.4f - 0.25f * tier, 3);
                if (!Main.dedServ) {
                    for (int i = 0; i < 6; i++) {
                        float xOff = Main.rand.NextFloat(-1f, 1f) * RimRadius * 0.8f * visualScale;
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            BowlMouthPos() + new Vector2(xOff, -2f),
                            new Vector2(xOff * 0.04f, -Main.rand.NextFloat(1.2f, 2.4f) * tier),
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(14, 22));
                    }
                }
            }

            //蓄近满时墨自碗沿外溢
            if (!Main.dedServ && fill > 0.88f && Main.rand.NextBool(7)) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_KikasaInkDrip>(
                    BowlMouthPos() + new Vector2(side * RimRadius * 0.86f * visualScale, 2f),
                    new Vector2(side * 0.3f, 0.2f), KikasaInk.InkBody,
                    Main.rand.NextFloat(0.55f, 0.85f))?.Configure(Main.rand.Next(26, 38));
            }

            if (!owner.controlUseTile) {
                //一档都不到就松手:忍住不泼,原路收伞
                if (ChargeFill < 0.2f) {
                    BeginRecall();
                    return;
                }
                BeginPour(owner);
            }
        }

        private void BeginPour(Player owner) {
            pourFill = ChargeFill;
            //倾覆朝向:所有者按光标,旁观端按朝向——纯表现
            pourDirSign = Main.myPlayer == Projectile.owner
                ? MathF.Sign(Main.MouseWorld.X - Projectile.Center.X + 0.01f)
                : owner.direction;
            State = UmbrellaState.Pour;
            eyeOpen = 1f;
            eyeGlow = 1f;
            KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.65f, -0.55f, 2);
            KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.9f, -0.45f, 2);
        }

        /// <summary>倾覆:猛倾→墨瀑冲刷→甩干回正;结束后按输入续蓄或收伞</summary>
        private void UpdatePour(Player owner) {
            Vector2 anchor = HoverAnchor(owner) + new Vector2(pourDirSign * 10f, -8f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.2f);

            float tiltPhase = MathHelper.Clamp(StateTimer / (float)PourTiltFrames, 0f, 1f);
            bool shaking = StateTimer > PourTiltFrames + PourHoldFrames;
            if (!shaking) {
                //猛倾跟瞄准走:出手前跟光标,出手后锁角;侧倾随偏角加大,几乎倒平
                float tiltWant = 0.62f;
                if (Main.myPlayer == Projectile.owner) {
                    if (StateTimer <= PourTiltFrames) {
                        pourAim = (Main.MouseWorld - Projectile.Center).ToRotation();
                    }
                    pourDirSign = MathF.Sign(MathF.Cos(pourAim) + 1e-4f);
                    float fromDown = MathHelper.WrapAngle(pourAim - MathHelper.PiOver2);
                    tiltWant = MathHelper.Clamp(0.32f + MathF.Abs(fromDown) * 0.55f, 0.32f, 1.12f);
                }
                pourTilt = MathHelper.Lerp(pourTilt, tiltWant * (1.1f - 0.1f * tiltPhase), 0.4f);
            }
            else {
                //甩干:回正路上抖两下
                float st = (StateTimer - PourTiltFrames - PourHoldFrames) / (float)PourShakeFrames;
                pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.35f)
                    + MathF.Sin(st * MathHelper.Pi * 3f) * 0.06f * (1f - st);
            }
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.03f, 0.25f);
            spinPhase += spinSpeed;
            eyeGlow *= 0.92f;

            //倾覆拍:墨瀑出手(所有者端),各端同帧甩出一蓬碎珠与墨雾
            if ((int)StateTimer == PourTiltFrames) {
                KikasaInk.Play(KikasaInk.InkSpray, Projectile.Center, 0.7f + 0.2f * pourFill, -0.55f, 2);
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.75f + 0.2f * pourFill, -0.3f, 2);
                if (Main.myPlayer == Projectile.owner) {
                    //跟光标走,不再卡在朝下 ±31°——倒撑是碗,但瞄准必须跟手
                    pourAim = (Main.MouseWorld - Projectile.Center).ToRotation();
                    int damage = (int)(Projectile.damage * (1.2f + 0.9f * pourFill));
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), BowlMouthPos(), Vector2.Zero,
                        ModContent.ProjectileType<KikasaInkPour>(), damage, Projectile.knockBack * 1.5f,
                        Projectile.owner, pourAim, pourFill);
                }
                if (!Main.dedServ) {
                    //旁观这帧可能还没收到墨瀑包,用倾覆朝向保底
                    Vector2 pourDir = Main.myPlayer == Projectile.owner
                        ? pourAim.ToRotationVector2()
                        : new Vector2(pourDirSign, 1f).SafeNormalize(Vector2.UnitY);
                    for (int i = 0; i < 9; i++) {
                        Vector2 vel = pourDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5.5f);
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(BowlMouthPos() + Main.rand.NextVector2Circular(10f, 5f),
                            vel, Main.rand.NextBool(3) ? KikasaInk.InkDeep : KikasaInk.InkBody,
                            Main.rand.NextFloat(0.4f, 0.7f) * (0.8f + 0.4f * pourFill))?.Configure(Main.rand.Next(18, 30));
                    }
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_KikasaInkMist>(BowlMouthPos(),
                            pourDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.6f, 1.4f),
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.9f, 1.3f))?.Configure(Main.rand.Next(30, 44));
                    }
                }
            }

            if (StateTimer >= PourTiltFrames + PourHoldFrames + PourShakeFrames) {
                if (owner.controlUseTile) {
                    //按着不放:从空碗重新蓄
                    State = UmbrellaState.Flip;
                    KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.5f, -0.35f, 2);
                }
                else {
                    BeginRecall();
                }
            }
        }

        /// <summary>倒扣时的碗口位置(蓄墨液面与墨瀑源头)</summary>
        private Vector2 BowlMouthPos()
            => Projectile.Center + new Vector2(0f, -4f * visualScale);

        /// <summary>攻击是否仍被按住:墨雨=左键 channel;倒撑模式由重击模块接管</summary>
        private bool HoldingAttack(Player owner)
            => ModeAi < 0.5f ? owner.channel : owner.controlUseTile;

        private void BeginRecall() {
            if (State == UmbrellaState.Recall) {
                return;
            }
            recallFrom = Projectile.Center;
            State = UmbrellaState.Recall;
            KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.4f, -0.5f, 2);
            //收拢拍抖落最后一圈墨珠
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dir = (MathHelper.TwoPi * i / 8f + 0.3f).ToRotationVector2();
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(
                        Projectile.Center + dir * RimRadius * 0.4f,
                        dir * Main.rand.NextFloat(1.6f, 3f),
                        KikasaInk.InkBody, Main.rand.NextFloat(0.28f, 0.46f))?.Configure(Main.rand.Next(14, 24));
                }
            }
        }

        private void UpdateLean(Player owner) {
            //移动倾斜各端一致;光标倾斜只有所有者本机看得到,纯表现无碍
            float target = MathHelper.Clamp(owner.velocity.X * 0.028f, -0.16f, 0.16f);
            if (Main.myPlayer == Projectile.owner) {
                target += MathHelper.Clamp((Main.MouseWorld.X - owner.Center.X) * 0.0004f, -0.1f, 0.1f);
            }
            lean = MathHelper.Lerp(lean, target, 0.1f);
        }

        //==================== 鬼眼:雨随目光 ====================

        /// <summary>
        /// 锁定 telegraph:获取目标的瞬间猛地睁眼(伴一记低哑水响),
        /// 出手窗全睁、持锁半睁、无人时眼睑几乎垂死;偶发眨眼保活性。
        /// 视线纯表现——所有者按光标就近,旁观端按离所有者最近,端间近似无碍
        /// </summary>
        private void UpdateEye(Player owner) {
            int newTarget = FindGazeTarget(owner);
            if (newTarget != gazeTarget) {
                if (newTarget >= 0) {
                    //睁眼拍:锁定瞬间
                    eyeOpen = MathF.Max(eyeOpen, 0.95f);
                    eyeGlow = MathF.Max(eyeGlow, 0.45f);
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = -0.85f, MaxInstances = 2 }, Projectile.Center);
                }
                gazeTarget = newTarget;
            }

            float openTarget;
            if (gazeTarget >= 0) {
                int beat = (int)(StateTimer % VolleyPeriod);
                openTarget = beat >= WindupFrames && beat < WindupFrames + DropsPerVolley * 2
                    ? 0.9f : 0.6f;
            }
            else {
                openTarget = 0.12f;
            }
            if (--blinkTimer <= 0) {
                blinkTimer = Main.rand.Next(130, 260);
            }
            if (blinkTimer < 5) {
                openTarget = 0f;
            }
            eyeOpen = MathHelper.Lerp(eyeOpen, openTarget, 0.22f);
            eyeGlow *= 0.86f;

            Vector2 lookTarget = gazeTarget >= 0
                ? (Main.npc[gazeTarget].Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                : new Vector2(lean * 2.5f, 1f).SafeNormalize(Vector2.UnitY);
            eyeLook = Vector2.Lerp(eyeLook, lookTarget, 0.15f).SafeNormalize(Vector2.UnitY);
        }

        private int FindGazeTarget(Player owner) {
            Vector2 anchor = Main.myPlayer == Projectile.owner ? Main.MouseWorld : owner.Center;
            float bestDist = Main.myPlayer == Projectile.owner ? CursorSeekRange : FallbackSeekRange;
            int best = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, anchor);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        //==================== 甩雨 ====================

        /// <summary>甩出一滴:弹幕只在所有者端生成,生成包带走目标与坠落列</summary>
        private void FireDrop(int slot) {
            if (Main.myPlayer != Projectile.owner) {
                return;
            }
            int target = PickTarget(slot, out float fallbackX);

            //伞缘切向甩出:出点随自旋相位在伞沿摆动,初速偏外偏上
            float yaw = MathF.Cos(spinPhase);
            float xOff = yaw * RimRadius * visualScale;
            Vector2 rimPos = Projectile.Center + new Vector2(xOff, 2f);
            float side = xOff >= 0f ? 1f : -1f;
            Vector2 flickVel = new(side * Main.rand.NextFloat(3.2f, 5.8f), -Main.rand.NextFloat(2.4f, 4.6f));

            //形态差异:血湖滴少而重,鬼雨滴密而细
            float scale = 1f;
            float dmgMul = 1f;
            KikasaDomainPlayer kdp = Owner.GetModPlayer<KikasaDomainPlayer>();
            if (kdp.AnyActive) {
                if (kdp.IsRainForm) {
                    scale = 0.85f;
                    dmgMul = 0.72f;
                }
                else {
                    scale = 1.12f;
                    dmgMul = 1.15f;
                }
            }

            int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(), rimPos, flickVel,
                ModContent.ProjectileType<KikasaInkDrop>(), (int)(Projectile.damage * dmgMul),
                Projectile.knockBack, Projectile.owner, target, fallbackX, 0f);
            if (p >= 0 && p < Main.maxProjectiles && scale != 1f) {
                Main.projectile[p].scale = scale;
                Main.projectile[p].netUpdate = true;
            }
        }

        /// <summary>光标附近的敌人按距离轮转分配,无人则退回玩家身边最近的,再无则落向光标列</summary>
        private int PickTarget(int slot, out float fallbackX) {
            fallbackX = Main.MouseWorld.X + Main.rand.NextFloat(-34f, 34f);

            List<(float dist, int who)> nearCursor = [];
            int nearestWho = -1;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float cursorDist = Vector2.Distance(npc.Center, Main.MouseWorld);
                if (cursorDist < CursorSeekRange) {
                    nearCursor.Add((cursorDist, i));
                }
                float ownerDist = Vector2.Distance(npc.Center, Owner.Center);
                if (ownerDist < FallbackSeekRange && ownerDist < nearestDist) {
                    nearestDist = ownerDist;
                    nearestWho = i;
                }
            }
            if (nearCursor.Count > 0) {
                nearCursor.Sort((a, b) => a.dist.CompareTo(b.dist));
                return nearCursor[slot % nearCursor.Count].who;
            }
            return nearestWho;
        }

        //==================== 绘制(由 KikasaRainRender 集中调用) ====================

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>伪偏航自旋的公共布局:cos 过零翻面+横向压缩,不对称剪影的翻面读作绕柄旋转</summary>
        private bool SolveDrawLayout(out Texture2D tex, out Rectangle frame, out Vector2 pos,
            out float rotation, out Vector2 scale, out SpriteEffects flip, out Color light) {
            int itemType = ModContent.ItemType<KikasaItem>();
            Main.instance.LoadItem(itemType);
            tex = TextureAssets.Item[itemType]?.Value;
            frame = default;
            pos = default;
            rotation = 0f;
            scale = default;
            flip = SpriteEffects.None;
            light = Color.White;
            if (tex == null) {
                return false;
            }
            frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            pos = Projectile.Center - Main.screenPosition;

            float yaw = MathF.Cos(spinPhase);
            flip = yaw < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //节拍挤压:蓄势时纵向绷紧、横向微鼓
            scale = new Vector2((0.7f + 0.3f * MathF.Abs(yaw)) * (1f + beatSquash * 0.6f),
                1f - beatSquash) * visualScale;
            //倒撑翻转+倾覆侧倾都走旋转,翻到一半的过程本身就是演出
            rotation = lean + MathF.Sin(bobPhase) * 0.035f
                + flipT * MathHelper.Pi + pourTilt * pourDirSign;
            light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            return true;
        }

        /// <summary>着色器路径:湿光扫掠/伞骨水膜/轮廓湿线/鬼眼全在 TechCanopy 里</summary>
        internal void DrawUmbrellaShader(SpriteBatch sb, Effect fx) {
            if (!SolveDrawLayout(out Texture2D tex, out Rectangle frame, out Vector2 pos,
                out float rotation, out Vector2 scale, out SpriteEffects flip, out Color light)) {
                return;
            }

            //蓄墨液面(TechFill):倒扣稳定后才显,画在伞体之下让伞缘盖住碗沿
            float fill = ChargeFill;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (fill > 0.02f && flipT > 0.8f && canvas != null) {
                fx.Parameters["uFill"]?.SetValue(fill);
                fx.Parameters["uSlosh"]?.SetValue(State == UmbrellaState.Pour ? 1f : 0.25f + 0.75f * fill);
                fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 4f);
                fx.CurrentTechnique = fx.Techniques["TechFill"];
                fx.CurrentTechnique.Passes[0].Apply();
                Vector2 mouth = BowlMouthPos() - Main.screenPosition;
                float w = 64f * visualScale;
                float h = 36f * visualScale;
                sb.Draw(canvas, mouth, null, Color.White, 0f, canvas.Size() * 0.5f,
                    new Vector2(w / canvas.Width, h / canvas.Height), SpriteEffects.None, 0f);
            }

            //翻面帧的采样 x 镜像,瞳向补一次镜像才保持世界朝向
            Vector2 look = eyeLook;
            if (flip == SpriteEffects.FlipHorizontally) {
                look.X = -look.X;
            }
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
            fx.Parameters["uSpinPhase"]?.SetValue(spinPhase);
            fx.Parameters["uSpinSpeed"]?.SetValue(MathHelper.Clamp(MathF.Abs(spinSpeed) / 0.74f, 0f, 1f));
            fx.Parameters["uWet"]?.SetValue(wetness);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 4f);
            fx.Parameters["uEye"]?.SetValue(eyeOpen);
            fx.Parameters["uEyeLook"]?.SetValue(look);
            fx.Parameters["uEyeGlow"]?.SetValue(eyeGlow);
            fx.Parameters["uEyeCenter"]?.SetValue(EyeCenter);
            fx.Parameters["uEyeR"]?.SetValue(EyeRadius);
            fx.CurrentTechnique = fx.Techniques["TechCanopy"];
            fx.CurrentTechnique.Passes[0].Apply();

            DrawLayered(sb, tex, frame, pos, rotation, scale, flip, light);
        }

        /// <summary>精灵回退:同一布局的裸贴图</summary>
        internal void DrawUmbrella(SpriteBatch sb) {
            if (!SolveDrawLayout(out Texture2D tex, out Rectangle frame, out Vector2 pos,
                out float rotation, out Vector2 scale, out SpriteEffects flip, out Color light)) {
                return;
            }
            DrawLayered(sb, tex, frame, pos, rotation, scale, flip, light);
        }

        /// <summary>自旋残影+本体:高转速时两侧各一道淡影,旋转要被看见</summary>
        private void DrawLayered(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 pos,
            float rotation, Vector2 scale, SpriteEffects flip, Color light) {
            Vector2 origin = frame.Size() * 0.5f;
            float smear = MathHelper.Clamp(MathF.Abs(spinSpeed) * 1.2f, 0f, 0.55f);
            if (smear > 0.08f) {
                sb.Draw(tex, pos, frame, light * (smear * 0.3f), rotation - 0.1f * MathF.Sign(spinSpeed),
                    origin, scale * 0.98f, flip, 0f);
                sb.Draw(tex, pos, frame, light * (smear * 0.18f), rotation - 0.2f * MathF.Sign(spinSpeed),
                    origin, scale * 0.95f, flip, 0f);
            }
            sb.Draw(tex, pos, frame, light, rotation, origin, scale, flip, 0f);
        }
    }
}
