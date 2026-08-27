using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 毁灭者之撕咬。玩家化身黑红毁灭者的头颅(带短链体)扑向猎物:
    /// 盘绕蓄势→高速突袭→合颚重咬→散解归位。撕咬期间玩家本体隐藏、
    /// 位置随头颅拖行(仅主人端做位移权威,远端凭弹幕同步观演)。
    /// 咬中造成巨额伤害并进入歼灭协议。
    /// ai[0]=目标NPC索引,-1为空放:朝出手方向(鼠标)定距突进
    /// </summary>
    internal class DestroyerBiteProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int CoilFrames = 10;
        private const int LungeMax = 16;
        private const int ClampFrames = 7;
        private const int ReleaseFrames = 16;
        /// <summary>合颚后的伤害窗(帧)</summary>
        private const int DamageWindow = 3;

        private const int SegCount = 5;
        private const float DrawScale = 0.85f;
        private const float SegSpacing = 64f * DrawScale;
        /// <summary>空放的突进距离(px)</summary>
        private const float EmptyDashRange = 540f;

        private const int PhaseCoil = 0;
        private const int PhaseLunge = 1;
        private const int PhaseClamp = 2;
        private const int PhaseRelease = 3;

        private int TargetIndex => (int)Projectile.ai[0];

        private int phase;
        private int phaseTimer;
        private bool bitConnected;
        private bool clampBurstDone;
        private Vector2 aimPoint;
        private float headRot;
        private int frameTick;
        private int frameIndex;

        //链体:头 + 体节 + 尾,逐节阻尼跟随(BTD 本体同款手感)
        private readonly Vector2[] spine = new Vector2[SegCount];
        private readonly float[] segRot = new float[SegCount];
        private bool spineInit;

        //突袭残影快照
        private readonly Vector2[][] ghostSnaps = new Vector2[2][];
        private int ghostTick;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = CoilFrames + LungeMax + ClampFrames + ReleaseFrames + 20;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => phase == PhaseClamp && phaseTimer <= DamageWindow;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            //以头颅为圆心的贪咬判定
            float radius = 92f;
            Vector2 head = spine[0];
            Vector2 closest = new(
                MathHelper.Clamp(head.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(head.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(head, closest) <= radius * radius;
        }

        public override void Initialize() {
            //出手记账:撕咬同样打断潜行计时
            DestroyerEXPlayer mp = Owner.GetModPlayer<DestroyerEXPlayer>();
            mp.NoteAttack();

            //有目标扑向目标,空放沿出手方向(鼠标)定距突进
            Vector2 toTarget = TargetValid() ? Main.npc[TargetIndex].Center - Owner.Center
                : Projectile.velocity.SafeNormalize(Vector2.UnitX) * EmptyDashRange;
            headRot = toTarget.SafeNormalize(Vector2.UnitX).ToRotation();
            aimPoint = Owner.Center + toTarget;

            //链体初始盘绕在玩家身后
            Vector2 back = -headRot.ToRotationVector2();
            for (int i = 0; i < SegCount; i++) {
                spine[i] = Owner.Center + back * (i * SegSpacing * 0.5f);
                segRot[i] = headRot;
            }
            spineInit = true;
            Projectile.Center = Owner.Center;

            if (!VaultUtils.isServer) {
                //机械苏醒:液压蓄能 + 从胸腔里挤出来的闷吼
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.8f, Pitch = -0.6f, MaxInstances = 2 }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.85f, MaxInstances = 2 }, Owner.Center);
            }
        }

        private bool TargetValid()
            => TargetIndex >= 0 && TargetIndex < Main.maxNPCs
            && Main.npc[TargetIndex] is { active: true } npc && npc.CanBeChasedBy();

        public override void AI() {
            if (!spineInit) {
                Initialize();
            }
            DestroyerEXPlayer mp = Owner.GetModPlayer<DestroyerEXPlayer>();
            //自愈式隐藏标记:弹幕意外消失时下一帧自动恢复绘制
            mp.BiteHideTick = phase < PhaseRelease ? Main.GameUpdateCount : mp.BiteHideTick;

            phaseTimer++;
            switch (phase) {
                case PhaseCoil: UpdateCoil(); break;
                case PhaseLunge: UpdateLunge(); break;
                case PhaseClamp: UpdateClamp(); break;
                case PhaseRelease: UpdateRelease(); break;
            }

            UpdateChain();
            UpdateFrames();
            HoldOwner(mp);
            Lighting.AddLight(spine[0], new Vector3(0.7f, 0.1f, 0.08f));
        }

        private void EnterPhase(int next) {
            phase = next;
            phaseTimer = 0;
        }

        /// <summary>盘绕蓄势:头颅在玩家位置成形,向后收拢压缩(扑击前的反向预备)</summary>
        private void UpdateCoil() {
            if (TargetValid()) {
                aimPoint = Main.npc[TargetIndex].Center;
                headRot = headRot.AngleLerp((aimPoint - spine[0]).ToRotation(), 0.3f);
            }
            float t = phaseTimer / (float)CoilFrames;
            //反向预备:头往目标反方向压一段,末两帧定住
            float pull = MathF.Sin(MathF.Min(t, 0.8f) / 0.8f * MathHelper.PiOver2) * 54f;
            Vector2 head = Owner.Center - headRot.ToRotationVector2() * pull;
            spine[0] = head;
            Projectile.Center = head;

            if (!VaultUtils.isServer) {
                //黑红雾息向头颅汇聚:化身成形
                if (phaseTimer % 2 == 0) {
                    Vector2 from = head + Main.rand.NextVector2Unit() * Main.rand.NextFloat(50f, 120f);
                    PRTLoader.NewParticle<PRT_Spark>(from, (head - from) * 0.16f,
                        Main.rand.NextBool(3) ? Color.White : new Color(255, 40, 30),
                        Main.rand.NextFloat(0.8f, 1.3f))?.Configure(false, 10);
                    PRTLoader.NewParticle<PRT_GhostRainMist>(from, (head - from) * 0.1f,
                        new Color(16, 4, 6) * 0.8f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(14);
                }
            }

            if (phaseTimer >= CoilFrames) {
                EnterPhase(PhaseLunge);
                if (!VaultUtils.isServer) {
                    //出击:破空 + 吼
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.75f, Pitch = -0.55f, MaxInstances = 2 }, spine[0]);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.9f, Pitch = -0.25f, MaxInstances = 2 }, spine[0]);
                    ShakeLocal(3f, spine[0]);
                }
            }
        }

        /// <summary>高速突袭:限转率追踪预测点,速度爬升,链体拉直成箭</summary>
        private void UpdateLunge() {
            //先吸收同步包对弹幕位置的纠偏,远端头颅不脱轨
            spine[0] = Projectile.Center;
            if (TargetValid()) {
                NPC npc = Main.npc[TargetIndex];
                aimPoint = npc.Center + npc.velocity * 4f;
            }
            float speed = MathHelper.Lerp(21f, 60f, MathF.Min(phaseTimer / 7f, 1f));
            float wantRot = (aimPoint - spine[0]).ToRotation();
            headRot = headRot.AngleTowards(wantRot, 0.22f);
            Vector2 step = headRot.ToRotationVector2() * speed;
            spine[0] += step;
            Projectile.Center = spine[0];

            //残影快照
            if (++ghostTick % 3 == 0) {
                ghostSnaps[1] = ghostSnaps[0];
                ghostSnaps[0] = (Vector2[])spine.Clone();
            }

            if (!VaultUtils.isServer) {
                //气流撕裂 + 沿途暗雾
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    spine[0] - step * 0.5f + Main.rand.NextVector2Circular(10f, 10f),
                    -step * 0.05f, new Color(14, 3, 5) * 0.75f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(16);
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Spark>(spine[0] + Main.rand.NextVector2Circular(16f, 16f),
                        -step * 0.08f, new Color(255, 40, 30), Main.rand.NextFloat(0.6f, 1f))?.Configure(false, 8);
                }
            }

            bool reached = Vector2.Distance(spine[0], aimPoint) <= speed * 1.1f;
            if (reached || phaseTimer >= LungeMax) {
                if (reached && TargetValid()) {
                    spine[0] = Main.npc[TargetIndex].Center;
                    Projectile.Center = spine[0];
                }
                EnterPhase(PhaseClamp);
            }
        }

        /// <summary>合颚重咬:颚爪猛合,伤害窗开启,震撼的撞击拍</summary>
        private void UpdateClamp() {
            //咬住活目标就钉在它身上
            if (bitConnected && TargetValid()) {
                spine[0] = Main.npc[TargetIndex].Center;
                Projectile.Center = spine[0];
            }

            if (!clampBurstDone) {
                clampBurstDone = true;
                DoClampBurst();
            }

            if (phaseTimer >= ClampFrames) {
                EnterPhase(PhaseRelease);
                //冷却在各端按同一拍落账(只有主人端的值真正参与门槛)
                Owner.GetModPlayer<DestroyerEXPlayer>().BiteCooldown = DestroyerEXPlayer.BiteCooldownTime;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 2 }, spine[0]);
                }
            }
        }

        /// <summary>撞击拍的全套演出:重低音层叠 + 冲击环 + 白红电弧 + 震屏</summary>
        private void DoClampBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            //震撼音效:巨吼压底、金属撕咬、重锤落地三层同拍
            SoundEngine.PlaySound(SoundID.Roar with { Volume = 1f, Pitch = -0.4f, MaxInstances = 2 }, spine[0]);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.95f, Pitch = -0.55f, MaxInstances = 3 }, spine[0]);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.85f, Pitch = -0.5f, MaxInstances = 2 }, spine[0]);

            ShakeLocal(11f, spine[0]);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(spine[0],
                    headRot.ToRotationVector2(), 7f, 6f, 11, 1000f, FullName));
            }

            //冲击环:红大环 + 白小环
            PRTLoader.NewParticle<PRT_StarPulseRing>(spine[0], Vector2.Zero,
                new Color(255, 50, 35), 0f)?.Configure(0.12f, 1.35f, 18);
            PRTLoader.NewParticle<PRT_StarPulseRing>(spine[0], Vector2.Zero,
                Color.White, 0f)?.Configure(0.06f, 0.7f, 12);

            //咬合点炸开白红电弧
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(0.6f);
                Vector2 dir = ang.ToRotationVector2();
                Vector2[] path = new Vector2[4];
                for (int k = 0; k < 4; k++) {
                    path[k] = spine[0] + dir * (k * Main.rand.NextFloat(26f, 40f));
                }
                PRTLoader.NewParticle<PRT_TeslaArc>(spine[0], Vector2.Zero,
                    new Color(255, 120, 100), 1f)?.Configure(path, Main.rand.Next(12, 18), 12f, (0f, 9f), 5f);
            }

            //金属碎火 + 暗渣
            for (int i = 0; i < 14; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(spine[0], Main.rand.NextVector2Circular(9f, 9f),
                    Main.rand.NextBool(3) ? Color.White : new Color(255, 50, 35),
                    Main.rand.NextFloat(1.2f, 2.2f))?.Configure(false, Main.rand.Next(12, 22));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(spine[0] + Main.rand.NextVector2Circular(20f, 20f),
                    Main.rand.NextVector2Circular(3f, 3f), new Color(16, 4, 6) * 0.85f,
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(26, 44));
            }
            Color warm = new(255, 70, 40);
            PRTLoader.NewParticle<PRT_MechExplosion>(spine[0], Main.rand.NextVector2Circular(1f, 1f),
                warm, 1f)?.Configure(Main.rand.Next(20, 30), warm);
        }

        /// <summary>散解归位:头颅松口后坐,链体逐节化作黑红雾散去,玩家现身</summary>
        private void UpdateRelease() {
            //松口后坐一小段
            if (phaseTimer <= 5) {
                spine[0] -= headRot.ToRotationVector2() * (5f - phaseTimer) * 1.6f;
                Projectile.Center = spine[0];
            }

            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                //逐节散解:尾先化
                int idx = Math.Clamp(SegCount - 1 - phaseTimer / 3, 0, SegCount - 1);
                Vector2 at = spine[idx] + Main.rand.NextVector2Circular(14f, 14f);
                PRTLoader.NewParticle<PRT_GhostRainMist>(at, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                    new Color(16, 4, 6) * 0.8f, Main.rand.NextFloat(0.6f, 0.9f))?.Configure(Main.rand.Next(24, 40));
                PRTLoader.NewParticle<PRT_Spark>(at, Main.rand.NextVector2Circular(2f, 2f),
                    new Color(255, 40, 30), Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, 10);
            }

            if (phaseTimer >= ReleaseFrames) {
                Projectile.Kill();
            }
        }

        /// <summary>链体逐节阻尼跟随(DestroyerBodyAI.Move 同款)</summary>
        private void UpdateChain() {
            segRot[0] = headRot + MathHelper.PiOver2;
            const float dampingInertia = 0.22f;
            for (int i = 1; i < SegCount; i++) {
                Vector2 segmentTarget = spine[i - 1] - spine[i];
                if (segRot[i - 1] != segRot[i]) {
                    segmentTarget = segmentTarget.RotatedBy(
                        MathHelper.WrapAngle(segRot[i - 1] - segRot[i]) * dampingInertia);
                }
                segRot[i] = segmentTarget.ToRotation() + MathHelper.PiOver2;
                spine[i] = spine[i - 1] - segmentTarget.SafeNormalize(Vector2.Zero) * SegSpacing;
            }
        }

        /// <summary>咬合动画:蓄势缓张、突袭大张、合颚急闭</summary>
        private void UpdateFrames() {
            int want = phase switch {
                PhaseCoil => 1 + phaseTimer / 4 % 2,
                PhaseLunge => 3,
                PhaseClamp => phaseTimer <= 2 ? 0 : 1,
                _ => 0,
            };
            if (frameIndex != want && ++frameTick >= 1) {
                frameTick = 0;
                frameIndex = want;
            }
        }

        /// <summary>玩家托管:隐身期无敌、跟随头颅位移(无敌与位移只在主人端做权威)</summary>
        private void HoldOwner(DestroyerEXPlayer mp) {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Owner.GivePlayerImmuneState(20);
            if (phase is PhaseLunge or PhaseClamp) {
                //贴着头颅拖行,墙体照常阻挡(头可以过,人不穿墙)
                Vector2 want = spine[0] - Owner.Center;
                Vector2 resolved = Collision.TileCollision(Owner.position, want, Owner.width, Owner.height,
                    fallThrough: true, fall2: true, gravDir: (int)Owner.gravDir);
                Owner.position += resolved;
                Owner.velocity = Vector2.Zero;
                Owner.fallStart = (int)(Owner.position.Y / 16f);
            }
            else if (phase == PhaseRelease && phaseTimer == 1) {
                //现身拍:落地缓冲
                Owner.velocity = -headRot.ToRotationVector2() * 2.5f;
                Owner.fallStart = (int)(Owner.position.Y / 16f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!bitConnected) {
                bitConnected = true;
                //咬中:主人端进歼灭协议,buff 经原版同步各端
                Owner.GetModPlayer<DestroyerEXPlayer>().GrantFrenzy();
                //咬中追加震撼:首个受害者结算一次,沿咬合方向重锤镜头
                if (!VaultUtils.isServer) {
                    ShakeLocal(6f, target.Center);
                    if (CWRClientConfig.Instance.ScreenVibration) {
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(target.Center,
                            headRot.ToRotationVector2(), 9f, 6f, 12, 1000f, FullName));
                    }
                }
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center, Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextBool() ? Color.White : new Color(255, 60, 40),
                        Main.rand.NextFloat(1.2f, 2f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //保险:哪怕中途被杀也把玩家还回来(隐藏标记自愈,这里只兜运动状态)
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.fallStart = (int)(Owner.position.Y / 16f);
            }
        }

        private static void ShakeLocal(float amount, Vector2 at) {
            if (Main.LocalPlayer != null && Vector2.Distance(Main.LocalPlayer.Center, at) < 1400f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(amount);
            }
        }

        //==================== 绘制 ====================

        private void GetSegDraw(int i, out Texture2D tex, out Texture2D glow, out Rectangle frame) {
            if (i == 0) {
                tex = DestroyerHeadAI.Head.Value;
                glow = DestroyerHeadAI.Head_Glow.Value;
                frame = tex.GetRectangle(frameIndex, 4);
                return;
            }
            if (i == SegCount - 1) {
                tex = DestroyerBodyAI.Tail.Value;
                glow = DestroyerBodyAI.Tail_Glow.Value;
                frame = tex.GetRectangle(i % 4, 4);
                return;
            }
            if (i % 2 == 0) {
                tex = DestroyerBodyAI.BodyAlt.Value;
                glow = DestroyerBodyAI.BodyAlt_Glow.Value;
                frame = tex.GetRectangle();
                return;
            }
            tex = DestroyerBodyAI.Body.Value;
            glow = DestroyerBodyAI.Body_Glow.Value;
            frame = tex.GetRectangle(i % 4, 4);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!spineInit) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //成形度:盘绕期淡入,散解期尾先蚀
            float formT = phase == PhaseCoil ? phaseTimer / (float)CoilFrames : 1f;

            //突袭残影:旧快照压成黑色剪影(越旧越黑越淡),再沿冲刺反向叠两层即时黑拖影
            if (phase is PhaseLunge or PhaseClamp) {
                for (int s = 1; s >= 0; s--) {
                    Vector2[] snap = ghostSnaps[s];
                    if (snap == null) {
                        continue;
                    }
                    //新快照留一点血红余温,旧快照沉入纯黑
                    Color ghostCol = s == 0 ? new Color(70, 12, 12) * 0.55f : new Color(10, 2, 4) * 0.45f;
                    for (int i = SegCount - 1; i >= 0; i--) {
                        GetSegDraw(i, out Texture2D gtex, out _, out Rectangle gframe);
                        float rot = i == 0 ? segRot[0] + MathHelper.Pi
                            : (snap[i - 1] - snap[i]).ToRotation() + MathHelper.PiOver2 + MathHelper.Pi;
                        sb.Draw(gtex, snap[i] - Main.screenPosition, gframe, ghostCol,
                            rot, gframe.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
                    }
                }
                //即时黑拖影:整条链体沿冲刺反方向错位重画,贴着本体的速度感
                Vector2 back = -headRot.ToRotationVector2();
                for (int k = 1; k <= 2; k++) {
                    Color smear = new Color(8, 2, 4) * (0.42f / k);
                    for (int i = SegCount - 1; i >= 0; i--) {
                        GetSegDraw(i, out Texture2D gtex, out _, out Rectangle gframe);
                        Vector2 pos = spine[i] + back * (k * 30f) - Main.screenPosition;
                        sb.Draw(gtex, pos, gframe, smear, segRot[i] + MathHelper.Pi,
                            gframe.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
                    }
                }
            }

            //本体:尾→头,头压顶
            for (int i = SegCount - 1; i >= 0; i--) {
                float segAlpha = formT;
                if (phase == PhaseRelease) {
                    //尾先化,头最后
                    float dissolveStart = (SegCount - 1 - i) * 2.4f;
                    segAlpha = 1f - MathHelper.Clamp((phaseTimer - dissolveStart) / 8f, 0f, 1f);
                }
                if (segAlpha <= 0.02f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Texture2D glow, out Rectangle frame);
                Vector2 pos = spine[i] - Main.screenPosition;
                float rot = segRot[i] + MathHelper.Pi;
                Color body = Color.Lerp(lightColor, Color.White, 0.35f) * segAlpha;
                sb.Draw(tex, pos, frame, body, rot, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
                //血灯辉光
                Color lamp = new Color(255, 60, 40) * (0.85f * segAlpha);
                lamp.A = 0;
                sb.Draw(glow, pos, frame, lamp, rot, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>撕咬期间隐藏玩家绘制:化身头颅的完全体,各端凭本地标记各自隐藏</summary>
    internal class DestroyerBiteHideOverride : PlayerOverride
    {
        public override bool PreDrawPlayers(ref Camera camera, ref IEnumerable<Player> players) {
            //由本机玩家的实例统一过滤一次,避免每个实例重复接管
            if (Player.whoAmI != Main.myPlayer) {
                return true;
            }
            players = players.Where(p => !IsHidden(p));
            return true;
        }

        private static bool IsHidden(Player player)
            => player.active && !player.dead
            && player.TryGetModPlayer(out DestroyerEXPlayer mp)
            && Main.GameUpdateCount <= mp.BiteHideTick + 1
            && mp.BiteHideTick > 0;
    }
}
