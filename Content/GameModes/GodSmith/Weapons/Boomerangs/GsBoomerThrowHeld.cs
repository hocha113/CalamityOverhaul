using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 回旋镖族 A 档蓄力掷手持基类。相位时间线 举臂蓄势-掷出-跟随收势；
    /// 蓄势期武器在手中可见、辉光渐亮，释放帧 owner 端真正掷出镖弹，
    /// 全身配套：体态后仰前倾（fullRotation 钉脚底、坐骑冲刺让位）+ 出手后坐踏步。<br/>
    /// 方案侧用法镜像 GsIronBroadsword：GsCanUseItem 里 HeldAlive 守门 + myPlayer 生成 + 全端返 false
    /// </summary>
    internal abstract class GsBoomerThrowHeldBase : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName
            => Language.GetText("ItemName." + ItemID.Search.GetName(SourceItemID));

        //==================== 子类必填 ====================

        /// <summary>目标物品 ID（校验与贴图来源）</summary>
        protected abstract int SourceItemID { get; }

        /// <summary>释放帧掷出的镖弹类型</summary>
        protected abstract int BoomerangType { get; }

        /// <summary>主题辉光色</summary>
        protected abstract Color GlowColor { get; }

        //==================== 参数面 ====================

        /// <summary>举臂蓄势帧数（未除攻速）</summary>
        protected virtual int RaiseDur => 9;
        /// <summary>掷出跟随帧数（未除攻速）</summary>
        protected virtual int ReleaseDur => 7;
        /// <summary>出手速度倍率（乘 Item.shootSpeed）</summary>
        protected virtual float ThrowSpeedMul => 1.15f;
        /// <summary>体态倾斜幅度</summary>
        protected virtual float LeanAmp => 0.07f;
        /// <summary>出手瞬间沿掷向的踏步力度</summary>
        protected virtual float ForwardStep => 1.6f;
        /// <summary>手到武器握点距离 px</summary>
        protected virtual float HoldDist => 24f;
        /// <summary>掷出音</summary>
        protected virtual SoundStyle ThrowSound => SoundID.Item1 with { Volume = 0.85f, Pitch = -0.1f };

        //==================== 状态 ====================

        private int timer;
        private int raiseD;
        private int releaseD;
        private bool thrown;
        private float bodyLean;
        private bool leanApplied;
        private float aimAngle;
        private int facingDir = 1;

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        protected float RaiseProgress => MathHelper.Clamp(timer / (float)Math.Max(1, raiseD), 0f, 1f);
        protected bool Thrown => thrown;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override void AI() {
            if (Item.type != SourceItemID || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            if (timer == 0) {
                //各相时长除以攻速，攻速词条真实生效
                float speed = Owner.GetWeaponAttackSpeed(Item);
                if (speed <= 0f) {
                    speed = 1f;
                }
                raiseD = Math.Max(2, (int)MathF.Round(RaiseDur / speed));
                releaseD = Math.Max(2, (int)MathF.Round(ReleaseDur / speed));
                aimAngle = Projectile.velocity.ToRotation();
                float cos = MathF.Cos(aimAngle);
                facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            }
            timer++;

            //owner 端蓄势期持续跟枪：释放前允许微调瞄准
            if (Projectile.IsOwnedByLocalPlayer() && !thrown) {
                Vector2 aim = Main.MouseWorld - Owner.Center;
                if (aim.LengthSquared() > 4f) {
                    float newAngle = aim.ToRotation();
                    if (MathF.Abs(MathHelper.WrapAngle(newAngle - aimAngle)) > 0.02f) {
                        aimAngle = newAngle;
                        float c = MathF.Cos(aimAngle);
                        facingDir = MathF.Abs(c) < 0.05f ? Owner.direction : Math.Sign(c);
                        Projectile.velocity = aimAngle.ToRotationVector2();
                        Projectile.netUpdate = true;
                    }
                }
            }

            UpdatePose();

            //释放帧：owner 掷出镖弹 + 全端演出
            if (!thrown && timer >= raiseD) {
                thrown = true;
                OnReleaseFX();
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Vector2 vel = aimAngle.ToRotationVector2() * (Item.shootSpeed * ThrowSpeedMul);
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), Owner.Center, vel,
                        BoomerangType, Projectile.damage, Projectile.knockBack, Owner.whoAmI);
                    if (!Owner.mount.Active) {
                        Owner.velocity.X += MathF.Sign(vel.X) * ForwardStep;
                    }
                }
            }

            if (timer >= raiseD + releaseD) {
                Projectile.Kill();
            }
        }

        /// <summary>释放瞬间演出（各端；粒子音效自守服务器）</summary>
        protected virtual void OnReleaseFX() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(ThrowSound, Owner.Center);
            PRTLoader.NewParticle<PRT_Light>(Hand + (aimAngle.ToRotationVector2() * HoldDist),
                aimAngle.ToRotationVector2() * 2f, GlowColor, 0.35f)?.Configure(8, 0.85f);
        }

        /// <summary>臂姿 + 体态时间线：举臂后仰，掷出前倾</summary>
        private void UpdatePose() {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Projectile.Center = Hand;

            float armWorld;   //以 facingDir=1 计算再镜像
            Player.CompositeArmStretchAmount stretch;
            if (!thrown) {
                //举臂蓄势：从出手向抬到过顶偏后
                float p = GsBoomerScheme.EaseOutQuad(RaiseProgress);
                armWorld = MathHelper.Lerp(-0.35f, -2.2f, p);
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
                bodyLean = MathHelper.Lerp(bodyLean, -facingDir * LeanAmp, 0.25f);
            }
            else {
                //掷出跟随：甩向出手向略下压
                float p = MathHelper.Clamp((timer - raiseD) / (float)Math.Max(1, releaseD), 0f, 1f);
                armWorld = MathHelper.Lerp(-2.2f, 0.35f, GsBoomerScheme.SmoothStep01(MathF.Min(1f, p * 1.6f)));
                stretch = Player.CompositeArmStretchAmount.Full;
                bodyLean = MathHelper.Lerp(bodyLean, facingDir * LeanAmp * 1.5f * (1f - p), 0.35f);
            }
            if (facingDir < 0) {
                armWorld = MathHelper.Pi - armWorld;
            }
            Owner.SetCompositeArmFront(true, stretch, armWorld - MathHelper.PiOver2);
            Owner.itemRotation = (armWorld.ToRotationVector2() * Owner.direction).ToRotation();
            ApplyBodyLean();
        }

        /// <summary>体态倾斜钉脚底，坐骑与冲刺旋转让位</summary>
        private void ApplyBodyLean() {
            CWRPlayer modPlayer = Owner.CWR();
            if (Owner.mount.Active || (modPlayer != null && modPlayer.IsRotatingDuringDash)) {
                leanApplied = false;
                return;
            }
            Owner.fullRotation = bodyLean * Owner.gravDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.gravDir >= 0f ? Owner.height : 0f);
            leanApplied = true;
        }

        public override void OnKill(int timeLeft) {
            if (leanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                leanApplied = false;
            }
        }

        //==================== 绘制：蓄势期手中武器 + 辉光 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (thrown) {
                return false;   //镖已离手，手部跟随空挥
            }
            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadItem(SourceItemID);
            Texture2D tex = TextureAssets.Item[SourceItemID].Value;
            Vector2 origin = tex.Size() / 2f;

            float armWorld = MathHelper.Lerp(-0.35f, -2.2f, GsBoomerScheme.EaseOutQuad(RaiseProgress));
            if (facingDir < 0) {
                armWorld = MathHelper.Pi - armWorld;
            }
            Vector2 pos = Hand + (armWorld.ToRotationVector2() * HoldDist) - Main.screenPosition;
            float rot = armWorld + (facingDir >= 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            SpriteEffects fx = facingDir >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //蓄势微震（whoAmI 种子，不掷 Main.rand）
            float charge = RaiseProgress;
            Vector2 shake = new(
                MathF.Sin((timer * 1.7f) + Projectile.whoAmI) * charge * 0.8f,
                MathF.Cos((timer * 1.3f) + Projectile.whoAmI) * charge * 0.8f);

            sb.Draw(tex, pos + shake, null, lightColor, rot, origin, 1f, fx, 0);
            Color glow = GlowColor * (0.15f + (0.5f * charge));
            glow.A = 0;
            sb.Draw(tex, pos + shake, null, glow, rot, origin, 1.05f, fx, 0);
            PostDrawHeld(sb, pos + shake, rot, charge);
            return false;
        }

        /// <summary>蓄势期追加绘制层（满蓄光辉等）</summary>
        protected virtual void PostDrawHeld(SpriteBatch sb, Vector2 drawPos, float rot, float charge) { }
    }
}
