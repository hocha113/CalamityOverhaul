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
    /// 【乱舞组共享手持基类】原版 Terragrim/Arkhalis（aiStyle75）的重铸骨架：
    /// 按住持续乱舞，驻场跟随玩家、朝向光标，判定=玩家前方 68×64 大盒（原版 5 帧复击、12 帧一记挥砍音）。<br/>
    /// 乱舞姿势：每 FlashInterval 帧换一道刃影角（identity+姿势序号播种的确定性随机角，各端同一场乱舞），
    /// 绘制只画当前姿势的武器贴图本体一笔。<br/>
    /// 联机：松手信号走 InnoVault DownLeft（方案 CanUseItem 全程压原版，player.channel 永不置位，
    /// 已对 TML 源核实）；瞄准向存 velocity，owner 侧 4 帧节流 netUpdate；音效守 !isServer；
    /// 绘制禁 Main.rand，姿势角一律 identity 播种
    /// </summary>
    internal abstract class GsOdditiesFlurryHeldBase : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName
            => Language.GetText("ItemName." + ItemID.Search.GetName(SwordItemID));

        //==================== 子类契约 ====================

        /// <summary>目标物品 ID：物品切换自杀检查 + 刃影贴图来源</summary>
        protected abstract int SwordItemID { get; }

        /// <summary>几帧换一次刃影姿势</summary>
        protected virtual int FlashInterval => 5;

        /// <summary>刃影相对瞄准向的最大偏角（弧度）</summary>
        protected virtual float SpreadArc => 0.7f;

        /// <summary>挥砍音基准音高</summary>
        protected virtual float SwingPitch => 0f;

        /// <summary>手→刃影贴图中心的距离（px）</summary>
        protected virtual float BladeReach => 52f;

        /// <summary>刃影贴图缩放</summary>
        protected virtual float BladeScale => 1.15f;

        /// <summary>true 时暂停乱舞（不生成新姿势、不响挥砍音、大盒判定关闭）；旧姿势照常渐隐</summary>
        protected virtual bool FlurrySuspended => false;

        /// <summary>0~1：刃影姿势向瞄准线收拢的程度（处决突刺的收束演出用）</summary>
        protected virtual float PoseConvergence => 0f;

        /// <summary>乱舞命中追加（识破/生长等记账；OnHitNPC 只在攻击方端跑）</summary>
        protected virtual void OnFlurryHit(NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>松手/缴械/被控收刀前的结算；各端都会被调，弹幕生成自守 owner</summary>
        protected virtual void OnRelease() { }

        /// <summary>子类每帧扩展（生长曲线/突刺状态机）</summary>
        protected virtual void FlurryAI() { }

        //==================== 常量与状态 ====================

        /// <summary>判定盒中心 = 玩家中心 + 瞄准向 × 52（原版 aiStyle75 几何）</summary>
        private const float BoxForward = 52f;
        /// <summary>开局宽限：远端首包 DownLeft 未到前不做松手判定</summary>
        private const int SpawnGrace = 4;

        /// <summary>当前刃影姿势角；poseValid 为 false 时退回瞄准角</summary>
        private float poseAngle;
        private bool poseValid;
        private int poseCountdown;
        private int poseIndex;
        private Vector2 lastSyncedAim;
        private float bodyLean;
        private bool bodyLeanApplied;
        /// <summary>本场乱舞已转发过外部命中钩子的目标（每目标只喂一次饰品链）</summary>
        private readonly HashSet<int> hitNPCs = [];

        /// <summary>存活帧计数，子类只读驱动生长/节奏</summary>
        protected int timer;
        /// <summary>出手朝向 ±1，按瞄准向每帧刷新</summary>
        protected int facingDir = 1;

        protected Vector2 Hand => Owner.GetPlayerStabilityCenter();
        /// <summary>瞄准单位向量：owner 每帧写自鼠标，远端读 velocity 同步值</summary>
        protected Vector2 AimUnit => Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
        protected float AimAngle => AimUnit.ToRotation();

        /// <summary>压掉基类逐帧鼠标移动发包，瞄准同步改走 velocity 的 4 帧节流</summary>
        public override bool CanFire => false;

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 64;   //原版 595/735 判定箱
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5; //原版 5 帧复击节奏
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        //==================== 主循环 ====================

        public override void AI() {
            //物品切换/死亡自杀
            if (Item.type != SwordItemID || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            //模式中途关闭：静默收场，不触发松手结算
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }

            timer++;
            UpdateAim();

            //松手/缴械/被控 → 结算后收刀（开局留宽限等远端首包 DownLeft）
            if (timer > SpawnGrace && (!DownLeft || Owner.noItems || Owner.CCed)) {
                OnRelease();
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2; //自续：AI 停跑即自然消亡
            Projectile.Center = Owner.Center + (AimUnit * BoxForward);
            Projectile.rotation = AimAngle;

            UpdateHeldPose();
            if (!FlurrySuspended) {
                UpdateFlurryPose();
                HandleSound();
            }
            FlurryAI();
        }

        /// <summary>owner 每帧读鼠标写 velocity；同步 4 帧节流（DownLeft 变化由基类即时发包）</summary>
        private void UpdateAim() {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            Vector2 aim = ToMouse.SafeNormalize(Vector2.UnitX * Owner.direction);
            Projectile.velocity = aim;
            if (timer % 4 == 0 && (aim - lastSyncedAim).LengthSquared() > 0.0001f) {
                lastSyncedAim = aim;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>持械姿态：itemTime 钉住、前臂指向瞄准向、朝向跟手</summary>
        private void UpdateHeldPose() {
            float aimAngle = AimAngle;
            float cos = MathF.Cos(aimAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (AimUnit * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, aimAngle - MathHelper.PiOver2);

            //乱舞压身前倾，收束时略加深
            float target = facingDir * (0.05f + (0.04f * PoseConvergence));
            bodyLean = MathHelper.Lerp(bodyLean, target, 0.2f);
            ApplyBodyLean();
        }

        /// <summary>体态倾斜上身，坐骑/冲刺旋转让位，origin 钉脚底</summary>
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

        /// <summary>确定性乱舞角：左右交替 × identity+姿势序号播种的随机幅度，各端同一场乱舞</summary>
        private void UpdateFlurryPose() {
            if (--poseCountdown > 0) {
                return;
            }
            poseCountdown = Math.Max(2, FlashInterval);
            poseIndex++;
            float side = poseIndex % 2 == 0 ? 1f : -1f;
            float mag = 0.25f + (0.75f * SeedRand01(poseIndex));
            poseAngle = AimAngle + (side * mag * SpreadArc);
            poseValid = true;
        }

        /// <summary>原版 12 帧一记挥砍音（AI 内 Main.rand 允许）</summary>
        private void HandleSound() {
            if (VaultUtils.isServer) {
                return;
            }
            if (timer % 12 == 1) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.62f,
                    Pitch = SwingPitch + Main.rand.NextFloat(-0.08f, 0.08f),
                }, Owner.Center);
            }
        }

        //==================== 判定与命中 ====================

        public override bool? CanDamage() => FlurrySuspended ? false : null;

        public override void CutTiles() {
            if (FlurrySuspended) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Owner.Center, Owner.Center + (AimUnit * 90f), 60f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = facingDir;//击退跟出手朝向

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本场乱舞对同一目标只转发一次外部命中钩子（模拟物品直击链，喂饰品与神赋）
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            OnFlurryHit(target, hit, damageDone);
        }

        //==================== 绘制（禁 Main.rand，identity 播种） ====================

        /// <summary>姿势角专用确定性伪随机（identity+salt 播种）</summary>
        private float SeedRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>只画当前刃影姿势的武器贴图本体一笔；收束时姿势角向瞄准线归拢（取最短角差路径）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            float aimAngle = AimAngle;
            float angle = poseValid
                ? poseAngle + (MathHelper.WrapAngle(aimAngle - poseAngle) * PoseConvergence)
                : aimAngle;
            GetBladeDrawOrientation(out SpriteEffects fx, out float rotOff);
            Vector2 at = Hand + (angle.ToRotationVector2() * BladeReach) - Main.screenPosition;
            Main.spriteBatch.Draw(tex, at, null, lightColor, angle + rotOff, tex.Size() / 2f, BladeScale, fx, 0f);
            return false;
        }

        /// <summary>反向朝向翻刃：刃口镜像，双向朝向都读得对（同范例映射）</summary>
        private void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset) {
            bool flip = facingDir < 0;
            effect = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flip ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }
    }
}
