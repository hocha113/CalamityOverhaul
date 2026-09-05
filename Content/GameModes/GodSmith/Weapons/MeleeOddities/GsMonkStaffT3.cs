using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 【天龙之怒】材质：衔着雷龙珠的天界法杖。签名：左键快速旋抡（30 帧 1.5 圈加速度曲线，
    /// 保真原版半程换向/松键提前收）。右键是原版能力（Player.cs 对 3858 硬编码 altFunctionUse），
    /// 方案不接管，GsCanUseItem 对右键返 null 交还原版掷雷龙球
    /// </summary>
    internal class GsMonkStaffT3 : GodSmithScheme
    {
        public override int TargetItemID => ItemID.MonkStaffT3;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: the twirl builds Dragon Breath on hit; at 5 stacks the next thrown orb ascends as a sky dragon, half again as large, calling down up to three lightning bolts";
        public override bool? GsCanUseItem(Item item, Player player) {
            //右键放回原版：原版自己掷雷龙球
            if (player.altFunctionUse == 2) {
                return null;
            }
            //手持弹幕在场即攻击冷却（真实冷却 = max(useTime, 相位总帧)，两者都吃攻速）
            if (HeldAlive<GsMonkStaffT3Held>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsMonkStaffT3Held>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            //全端返回 false 压掉原版行为；远端靠弹幕同步看到动作
            return false;
        }

        //底伤不加成（×1.0）：综合 DPS 落在原版 100%~118%
    }

    /// <summary>
    /// 天龙之怒手持旋抡：镜像 T1 自管 spin 骨架，30 帧转 1.5 圈（÷攻速），加速度曲线同款，
    /// 杆线 中心±60px 复击 6 帧，无砸地，保真半程换向与松键提前收（reuseDelay=2）
    /// </summary>
    internal class GsMonkStaffT3Held : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MonkStaffT3");

        /// <summary>旋抡基准总帧（除以攻速）</summary>
        private const int SpinBaseDur = 30;
        /// <summary>旋抡总转角：1.5 圈</summary>
        private const float SpinTotalAngle = MathHelper.TwoPi * 1.5f;
        /// <summary>杆线半长（px），原版 T3 切割半径 60</summary>
        private const float PoleHalf = 60f;

        private int spinDur = SpinBaseDur;
        private int halfFrame;

        private int timer;
        private int dir = 1;
        private float rot;
        private float prevRot;
        private float speedFrac = 0.5f;
        private float bodyLean;
        private bool bodyLeanApplied;
        private readonly HashSet<int> hitNPCs = [];

        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 RotVec => rot.ToRotationVector2();

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6; //旋抡复击节奏（原版 T3 更快）
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 120;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private static float MirrorAngle(float angle, int direction)
            => direction == 1 ? angle : MathHelper.Pi - angle;

        private void InitSpin() {
            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            dir = MathF.Abs(Projectile.velocity.X) < 0.001f
                ? Owner.direction : Math.Sign(Projectile.velocity.X);
            //方向寄存在 velocity（±1,0），换向 netUpdate 过线，远端在 AI 里侦测翻杆（同 T1 骨架）
            Projectile.velocity = new Vector2(dir, 0f);
            spinDur = Math.Max(14, (int)MathF.Round(SpinBaseDur / speed));
            halfFrame = spinDur / 2;
            //起角取上后方，1.5 圈后杆头收在身前下方
            rot = MirrorAngle(-2.2f, dir);
            prevRot = rot;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_SkyDragonsFurySwing with { Volume = 0.9f }, Owner.Center);
            }
        }

        public override void AI() {
            if (Item.type != ItemID.MonkStaffT3 || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            if (timer == 0 && Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                InitSpin();
            }
            SpinAI();
        }

        //==================== 旋抡相 ====================

        private void SpinAI() {
            //远端换向侦测（owner 半程翻向后 velocity 随 netUpdate 过线）
            int dirNow = Projectile.velocity.X >= 0f ? 1 : -1;
            if (dirNow != dir && timer > 0) {
                dir = dirNow;
                rot -= MathHelper.Pi;
            }

            timer++;
            prevRot = rot;
            //加速度曲线同 T1：线性权重 0.7→1.4 中点采样，总转角严格 1.5 圈
            float pMid = MathHelper.Clamp((timer - 0.5f) / spinDur, 0f, 1f);
            float w = MathHelper.Lerp(0.7f, 1.4f, pMid);
            speedFrac = w / 1.4f;
            rot += SpinTotalAngle / spinDur * (w / 1.05f) * dir;

            //半程帧：松键提前收（原版 T3 reuseDelay=2）；仍按住可随鼠标换向
            if (timer == halfFrame) {
                if (!Owner.controlUseItem) {
                    EndSpin();
                    return;
                }
                if (Projectile.owner == Main.myPlayer) {
                    int side = Main.MouseWorld.X > Owner.Center.X ? 1 : -1;
                    if (side != dir) {
                        dir = side;
                        Owner.ChangeDir(side);
                        Projectile.velocity = new Vector2(side, 0f);
                        rot -= MathHelper.Pi;
                        Projectile.netUpdate = true;
                    }
                }
            }
            if (timer >= spinDur) {
                EndSpin();
                return;
            }

            UpdateSpinPose();
        }

        /// <summary>自然收尾与提前收共用：owner 补原版 reuseDelay=2</summary>
        private void EndSpin() {
            if (Owner.whoAmI == Main.myPlayer) {
                Owner.reuseDelay = 2;
            }
            Projectile.Kill();
        }

        private void UpdateSpinPose() {
            Owner.ChangeDir(dir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (RotVec * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot - MathHelper.PiOver2);
            float p = timer / (float)spinDur;
            Projectile.Center = Hand + (RotVec * (p * 8f));
            Projectile.rotation = rot;

            float target = timer >= spinDur - 2 ? 0f : dir * 0.06f * speedFrac;
            bodyLean = MathHelper.Lerp(bodyLean, target, 0.32f);
            ApplyBodyLean();
        }

        /// <summary>体态倾斜钉脚底，坐骑/冲刺旋转让位</summary>
        private void ApplyBodyLean() {
            CWRPlayer modPlayer = Owner.CWR();
            if (Owner.mount.Active || (modPlayer != null && modPlayer.IsRotatingDuringDash)) {
                bodyLeanApplied = false;
                return;
            }
            Owner.fullRotation = bodyLean * Owner.gravDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.gravDir >= 0f ? Owner.height : 0f);
            bodyLeanApplied = true;
        }

        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        /// <summary>贪婪判定：本帧扫过角度区间逐段采样杆线（中心±60px）；翻杆瞬间只判当前姿态</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(6, 6);
            Vector2 center = Projectile.Center;
            float delta = MathHelper.WrapAngle(rot - prevRot);
            int steps = MathF.Abs(delta) > 1.5f ? 0 : Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * PoleHalf / 16f), 1, 8);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float ang = steps == 0 ? rot : MathHelper.Lerp(prevRot, rot, i / (float)steps);
                Vector2 half = ang.ToRotationVector2() * PoleHalf;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(),
                    center - half, center + half, 36f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 half = RotVec * PoleHalf;
            Utils.PlotTileLine(Projectile.Center - half, Projectile.Center + half, 40f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = dir;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次旋抡对同一目标只转发一次外部命中钩子（喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }
        }

        /// <summary>杆体本体一笔：原版物品贴图 origin 按握把端</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (timer <= 0) {
                return false;
            }
            Main.instance.LoadItem(ItemID.MonkStaffT3);
            Texture2D tex = TextureAssets.Item[ItemID.MonkStaffT3].Value;
            Vector2 origin = new(8f, tex.Height - 8f);
            float diag = new Vector2(tex.Width, tex.Height).Length();
            float scale = ((PoleHalf * 2f) + 16f) / MathF.Max(diag - 16f, 1f);
            Vector2 gripPos = Projectile.Center - (RotVec * PoleHalf) - Main.screenPosition;
            Main.spriteBatch.Draw(tex, gripPos, null, lightColor, rot + MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
