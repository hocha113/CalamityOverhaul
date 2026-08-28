using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 钴蓝薙刀重铸：薙斩变式。<br/>
    /// 材质：淬火钴钢刀刃。签名行为：①两拍交替——奇拍直线突刺，偶拍旁路突刺相位，
    /// 改为一记横扫弧斩（角度扫掠 + 弧线采样判定，几何与突刺完全不同）
    /// ②扫拍拖出钴蓝弧光涂抹与刀身残影 ③扫斩命中钴钢脆响、火花沿切线甩出
    /// </summary>
    internal class GsCobaltNaginata : GsSpearScheme
    {
        public override int TargetItemID => ItemID.CobaltNaginata;

        protected override string GsDescFallback =>
            "Reforged: alternating polework, a straight thrust then a wide cobalt sweep;" +
            "\nthe sweep carves an arc where the thrust line cannot reach";

        protected override int HeldProjType => ModContent.ProjectileType<GsCobaltNaginataHeld>();

        protected override int ComboBeats => 2;

        /// <summary>扫拍的扫向交替符号：第 1、3、5…次横扫上下轮换</summary>
        protected override float SpawnAi1(Item item, Player player)
            => (comboCounter - 1) / 2 % 2 == 0 ? 1f : -1f;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//横扫覆盖面即机制收益，底伤小补，综合 DPS 落在原版 105%~115%
    }

    /// <summary>
    /// 钴蓝薙刀手持。ai[0]=拍号 0 直刺 / 1 横扫，ai[1]=扫向符号。<br/>
    /// 直刺拍走基类持距相位；横扫拍整体旁路基类 AI，
    /// 自持角度扫掠时间线（举-扫-收）与弧线采样判定，镜像 GsIronBroadswordHeld 的弧线骨架
    /// </summary>
    internal class GsCobaltNaginataHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.CobaltNaginata;

        //淬火钴钢色板
        internal static readonly Color CobaltEdge = new(168, 214, 255);   //刃缘亮蓝
        internal static readonly Color CobaltMain = new(74, 128, 236);    //钴身
        internal static readonly Color CobaltDeep = new(30, 52, 120);     //深钴影
        internal static readonly Color CobaltFlash = new(220, 240, 255);  //淬火白闪

        //直刺拍手感：硬模最轻灵的一把
        protected override float WindupFrames => 4f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 3f;
        protected override float RecoverFrames => 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 58f;
        protected override float BladeLength => 88f;
        protected override float CollisionWidth => 28f;
        protected override float TipGreedRadius => 26f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.035f;
        protected override int HitboxSize => 48;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.12f;

        protected override Color EdgeColor => CobaltEdge;
        protected override Color CoreColor => CobaltMain;

        private bool IsSweep => ComboStage == 1;

        //==================== 横扫拍自持状态（旁路基类相位机） ====================

        private const int SweepRaise = 5;
        private const int SweepSlash = 5;
        private const int SweepRecover = 9;
        private const float RaiseBack = 1.30f;
        private const float Follow = 1.15f;

        private float sweepTimer;
        private float baseAngle;
        private float swingDir = 1f;
        private float mainAngle;
        private float lastAngle;
        private float mainReach;
        private float slashProgress;
        private float sweepFade = 1f;
        private bool sweepDamageActive;
        private bool sweepSoundPlayed;
        private int sweepHitstop;
        private float sweepHitstopSpent;
        private bool sweepHitstopApplied;
        private float sweepLean;
        private bool sweepLeanApplied;

        /// <summary>手→扫斩刃尖距离，与直刺持距量级一致，两种几何轮廓等长</summary>
        private float SweepReach => BladeLength + 16f;
        private float ArcStart => baseAngle - swingDir * RaiseBack;
        private float ArcEnd => baseAngle + swingDir * Follow;

        protected override void OnInit() {
            if (!IsSweep) {
                return;
            }
            //横扫是重几何拍：伤害小抬，扫向按 ai1 上下轮换
            baseAngle = stabUnit.ToRotation();
            swingDir = (WeaponParam >= 0f ? 1f : -1f) * facingDir;
            Projectile.damage = (int)(Projectile.damage * 1.12f);
        }

        //==================== 横扫拍 AI（直刺拍走基类） ====================

        public override void AI() {
            if (!IsSweep) {
                base.AI();
                return;
            }

            if (Item.type != TargetItemType || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 90;

            //命中顿帧：扫掠时间线冻结
            if (sweepHitstop > 0) {
                sweepHitstop--;
            }
            else {
                sweepTimer += speedMul;
            }

            lastAngle = mainAngle;
            int phase = SweepPhase;
            UpdateSweepTransform(phase);
            sweepDamageActive = phase == 1 && slashProgress <= 0.92f
                && MathF.Abs(mainAngle - lastAngle) > 0.004f;
            UpdateSweepPose(phase);
            HandleSweepEvents(phase);

            Lighting.AddLight(Hand + mainAngle.ToRotationVector2() * (SweepReach * 0.7f),
                CobaltMain.ToVector3() * (0.32f * sweepFade));

            //顿帧从收势尾巴等量扣回
            float total = SweepRaise + SweepSlash + SweepRecover;
            float effectiveTotal = MathF.Max(SweepRaise + SweepSlash + 2f, total - sweepHitstopSpent * speedMul);
            if (sweepTimer >= effectiveTotal && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.Kill();
            }
        }

        private int SweepPhase {
            get {
                if (sweepTimer < SweepRaise) {
                    return 0;
                }
                return sweepTimer < SweepRaise + SweepSlash ? 1 : 2;
            }
        }

        /// <summary>扫掠行程曲线：爆发过冲 4.5% 再回坐（收-爆-停）</summary>
        private static float SweepCurve(float p) {
            const float burstEnd = 0.52f;
            const float overshoot = 1.045f;
            static float Smooth(float x) {
                x = MathHelper.Clamp(x, 0f, 1f);
                return x * x * (3f - 2f * x);
            }
            if (p < burstEnd) {
                return overshoot * Smooth(p / burstEnd);
            }
            return MathHelper.Lerp(overshoot, 1f, Smooth((p - burstEnd) / (1f - burstEnd)));
        }

        private void UpdateSweepTransform(int phase) {
            float arcStart = ArcStart;
            float heldAngle = arcStart - swingDir * 0.06f;
            switch (phase) {
                case 0: {
                    float p = MathHelper.Clamp(sweepTimer / SweepRaise, 0f, 1f);
                    float eased = 1f - MathF.Pow(1f - p, 3f);
                    float liftFrom = arcStart + swingDir * RaiseBack * 0.62f;
                    mainAngle = MathHelper.Lerp(liftFrom, heldAngle, eased);
                    mainReach = SweepReach * MathHelper.Lerp(0.6f, 0.94f, eased);
                    slashProgress = 0f;
                    break;
                }
                case 1: {
                    float p = MathHelper.Clamp((sweepTimer - SweepRaise) / SweepSlash, 0f, 1f);
                    slashProgress = p;
                    mainAngle = MathHelper.Lerp(heldAngle, ArcEnd, SweepCurve(p));
                    mainReach = SweepReach * (0.96f + 0.04f * MathF.Sin(MathHelper.Clamp(p * 1.8f, 0f, 1f) * MathHelper.Pi));
                    break;
                }
                default: {
                    float q = MathHelper.Clamp((sweepTimer - SweepRaise - SweepSlash) / SweepRecover, 0f, 1f);
                    float settle = 1f - (1f - Math.Min(1f, q * 2.2f)) * (1f - Math.Min(1f, q * 2.2f));
                    mainAngle = ArcEnd + swingDir * 0.08f * (1f - settle);
                    mainReach = SweepReach * MathHelper.Lerp(0.96f, 0.8f, q * q);
                    slashProgress = 1f;
                    sweepFade = MathHelper.Clamp(1f - q * 1.3f, 0f, 1f);
                    break;
                }
            }
        }

        /// <summary>横扫姿态：双手持杆随扫角走，体态举势后仰扫出前甩</summary>
        private void UpdateSweepPose(int phase) {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (mainAngle.ToRotationVector2() * Owner.direction).ToRotation();

            float armRot = mainAngle - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRot - facingDir * 0.35f);

            Projectile.Center = Hand + mainAngle.ToRotationVector2() * (mainReach * 0.55f);
            Projectile.rotation = mainAngle;

            if (sweepHitstop > 0) {
                return;
            }
            (float target, float rate) = phase switch {
                0 => (-facingDir * 0.05f, 0.25f),
                1 => (facingDir * 0.07f, 0.65f),
                _ => (0f, 0.16f),
            };
            sweepLean = MathHelper.Lerp(sweepLean, target, rate);
            ApplySweepLean();
        }

        /// <summary>体态倾斜钉脚底，坐骑/冲刺旋转让位（镜像基类规矩）</summary>
        private void ApplySweepLean() {
            CWRPlayer modPlayer = Owner.CWR();
            if (Owner.mount.Active || (modPlayer != null && modPlayer.IsRotatingDuringDash)) {
                sweepLeanApplied = false;
                return;
            }
            Owner.fullRotation = sweepLean * Owner.gravDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.gravDir >= 0f ? Owner.height : 0f);
            sweepLeanApplied = true;
        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
            if (sweepLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                sweepLeanApplied = false;
            }
        }

        private void HandleSweepEvents(int phase) {
            if (phase != 1) {
                return;
            }
            //扫出首帧一记沉哨音
            if (!sweepSoundPlayed) {
                sweepSoundPlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = -0.22f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.3f, Pitch = 0.1f }, Owner.Center);
                }
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //扫掠期沿切线甩钴蓝火花
            Vector2 sweepVel = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2();
            Vector2 at = Hand + mainAngle.ToRotationVector2() * Main.rand.NextFloat(0.6f, 1f) * mainReach;
            Color c = Main.rand.NextBool(3) ? CobaltFlash : CobaltEdge;
            PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(3.5f, 7f), c,
                Main.rand.NextFloat(0.32f, 0.55f))?.Configure(true, Main.rand.Next(11, 18));
        }

        //==================== 判定分流：扫拍走弧线采样 ====================

        public override bool? CanDamage() {
            if (!IsSweep) {
                return base.CanDamage();
            }
            return sweepDamageActive ? null : false;
        }

        /// <summary>扫拍贪婪判定：本帧扫过的角度区间逐段采样，贴身段单独兜一次</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!IsSweep) {
                return base.Colliding(projHitbox, targetHitbox);
            }
            if (!sweepDamageActive) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(8, 8);
            Vector2 hand = Hand;
            if (greedyBox.Distance(hand) <= 40f) {
                return true;
            }
            float delta = mainAngle - lastAngle;
            float reach = mainReach * 1.04f + 8f;
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * reach / 30f), 1, 16);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)steps);
                Vector2 tip = hand + ang.ToRotationVector2() * reach;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(),
                    hand, tip, 38f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            if (!IsSweep) {
                base.CutTiles();
                return;
            }
            if (!sweepDamageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 hand = Hand;
            const int samples = 2;
            for (int i = 0; i <= samples; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)samples);
                Vector2 tip = hand + ang.ToRotationVector2() * (mainReach * 1.02f);
                Utils.PlotTileLine(hand, tip, 32f, DelegateMethods.CutTiles);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //扫拍顿帧自持（基类顿帧只冻结直刺时间线）
            if (IsSweep && !sweepHitstopApplied) {
                sweepHitstopApplied = true;
                sweepHitstop = 2;
                sweepHitstopSpent = 2;
            }
        }

        /// <summary>命中反馈：钴钢脆响 + 火花沿刃向甩出，扫拍音更沉、火花更宽</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 dir = IsSweep
                ? (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2()
                : stabUnit;
            Vector2 pos = IsSweep
                ? Vector2.Lerp(Hand + mainAngle.ToRotationVector2() * mainReach, target.Center, 0.5f)
                : Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = IsSweep ? 0.15f : 0.45f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, CobaltFlash, IsSweep ? 0.22f : 0.16f)?.Configure(9, 0.75f);
            int sparks = IsSweep ? 8 : 5;
            float spread = IsSweep ? 0.85f : 0.5f;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = dir.RotatedByRandom(spread) * Main.rand.NextFloat(3.5f, 8f);
                Color c = Main.rand.NextBool(3) ? CobaltFlash : CobaltEdge;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制分流：扫拍自绘弧光 + 残影 + 刀身 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!IsSweep) {
                return base.PreDraw(ref lightColor);
            }
            SpriteBatch sb = Main.spriteBatch;
            DrawSweepSmear(sb);
            DrawSweepBlade(sb, lightColor);
            return false;
        }

        /// <summary>钴蓝弧光：双层弧形涂抹沿扫角走（加色 A=0），扫掠亮收势蚀散</summary>
        private void DrawSweepSmear(SpriteBatch sb) {
            if (slashProgress <= 0.02f || sweepFade <= 0.02f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = sweepFade * (0.28f + slashProgress * 0.38f);
            Vector2 arcCenter = Hand + mainAngle.ToRotationVector2() * (mainReach * 0.55f) - Main.screenPosition;
            float rot = mainAngle + swingDir * 0.35f;
            Color c1 = CobaltEdge with { A = 0 } * alpha;
            sb.Draw(wave, arcCenter, null, c1, rot, wave.Size() / 2f,
                new Vector2(0.46f, 0.22f) * (mainReach / 118f), SpriteEffects.None, 0f);
            Color c2 = CobaltMain with { A = 0 } * (alpha * 0.7f);
            sb.Draw(wave, arcCenter, null, c2, rot, wave.Size() / 2f,
                new Vector2(0.42f, 0.10f) * (mainReach / 118f), SpriteEffects.None, 0f);
        }

        /// <summary>扫拍刀身：姿态残影 + 暗影垫底 + 本体 + 扫掠期淬火辉光</summary>
        private void DrawSweepBlade(SpriteBatch sb, Color lightColor) {
            Main.instance.LoadItem(TargetItemType);
            Texture2D tex = TextureAssets.Item[TargetItemType].Value;
            Vector2 origin = tex.Size() / 2f;
            float scale = BladeLength / MathF.Max(tex.Size().Length() * BladeTexFill, 1f);
            float rotOffset = MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (facingDir < 0) {
                rotOffset += MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }
            Vector2 hand = Hand;

            //扫掠期姿态残影，最近的最亮
            if (SweepPhase == 1 && slashProgress > 0.10f) {
                for (int g = 3; g >= 1; g--) {
                    float ghostAngle = mainAngle - swingDir * 0.20f * g;
                    float ghostAlpha = g switch { 1 => 0.32f, 2 => 0.17f, _ => 0.08f };
                    Color ghost = CobaltEdge with { A = 0 } * ghostAlpha;
                    Vector2 gPos = hand + ghostAngle.ToRotationVector2() * (mainReach * 0.52f) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, ghost, ghostAngle + rotOffset, origin, scale, effect, 0f);
                }
            }

            Vector2 drawPos = hand + mainAngle.ToRotationVector2() * (mainReach * 0.52f) - Main.screenPosition;

            //深钴暗影垫底
            Color shadow = new Color(14, 14, 20, 190) * 0.45f;
            sb.Draw(tex, drawPos + new Vector2(facingDir, 2f), null, shadow, mainAngle + rotOffset, origin, scale * 1.02f, effect, 0f);
            sb.Draw(tex, drawPos, null, lightColor, mainAngle + rotOffset, origin, scale, effect, 0f);

            //扫掠期淬火白辉光
            float glow = SweepPhase == 1 ? 0.35f * sweepFade : 0.12f * sweepFade;
            if (glow > 0.02f) {
                Color gc = CobaltFlash with { A = 0 } * glow;
                sb.Draw(tex, drawPos, null, gc, mainAngle + rotOffset, origin, scale * 1.045f, effect, 0f);
            }
        }
    }
}
