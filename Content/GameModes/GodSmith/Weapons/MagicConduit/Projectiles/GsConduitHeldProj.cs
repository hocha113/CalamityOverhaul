using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using InnoVault.GameContent.BaseEntity;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 持续束通道手持弹幕基类（美杜莎凝视 / 生命吸取 / 热射线的全接管载体）。<br/>
    /// 端别契约：<br/>
    /// · owner 端权威：输入存活判定、蓝耗、积热、瞄准方向（写 velocity，变化超阈值才 netUpdate）、
    ///   热段里程碑（ai[1]，跨段才 netUpdate）、收尾令（ai[2]=1 一次性 netUpdate）、伤害刷新与 Kill；<br/>
    /// · 远端只消费同步量：方向 = velocity、热段 = ai[1]、收尾 = ai[2]，
    ///   塌缩动画各端从收尾令起本地确定性推进（localAI[0]），owner 的 Kill 是最终兜底；<br/>
    /// · 命中裁决全在 owner 端（usesLocalNPCImmunity），判定几何与绘制几何同源
    /// </summary>
    internal abstract class GsConduitHeldProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 子类合同 ====================

        /// <summary>绑定物品（失手即收束）</summary>
        protected abstract int BoundItemID { get; }

        /// <summary>持续蓝耗（点/秒，白热 ×0.7、过热锁 ×1.5、再乘玩家魔耗乘区）</summary>
        protected abstract float ManaPerSecond { get; }

        /// <summary>引导积热（点/tick）</summary>
        protected abstract float HeatPerTick { get; }

        /// <summary>命中间隔（usesLocalNPCImmunity 的 tick 周期）</summary>
        protected abstract int HitCooldown { get; }

        /// <summary>tick 伤害系数（基伤 × 系数烘焙进每 tick）</summary>
        protected abstract float TickDamageCoef { get; }

        /// <summary>收尾塌缩帧数</summary>
        protected virtual int CollapseTicks => 8;

        /// <summary>true 读 Owner.channel（原生 channel 武器），false 读 controlUseItem（热射线）</summary>
        protected virtual bool UseChannelFlag => true;

        /// <summary>枪口前伸</summary>
        protected virtual float MuzzleOffset => 20f;

        /// <summary>活跃/塌缩每帧（各端）：几何演化与演出。collapse01 = 塌缩进度 0~1</summary>
        protected abstract void ChannelAI(float collapse01);

        /// <summary>活跃期伤害门（各端一致的相位闩锁；默认放行）</summary>
        protected virtual bool? DamageGate() => null;

        //==================== 同步槽语义 ====================

        /// <summary>热段里程碑（0 常态 / 1 白热），owner 写、跨段 netUpdate</summary>
        protected int HeatStageSync => (int)Projectile.ai[1];

        /// <summary>收尾令（0 活跃 / 1 收尾），owner 写一次性 netUpdate</summary>
        protected bool Collapsing => Projectile.ai[2] >= 1f;

        /// <summary>瞄准单位向量（velocity 承载，随原生弹幕同步走）</summary>
        protected Vector2 AimUnit => Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);

        private float manaAccumulator;
        private uint lastAimSyncTick;

        //==================== 生命周期 ====================

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = HitCooldown;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!Collapsing) {
                Projectile.timeLeft = 2;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    OwnerTick();
                }
            }

            UpdatePose();

            float collapse01 = 0f;
            if (Collapsing) {
                //收尾动画各端本地推进；远端 timeLeft 收口自灭兜底丢包
                if (Projectile.localAI[0] == 0f && Projectile.timeLeft > 40) {
                    Projectile.timeLeft = 40;
                }
                Projectile.localAI[0]++;
                collapse01 = MathHelper.Clamp(Projectile.localAI[0] / CollapseTicks, 0f, 1f);
                if (Projectile.IsOwnedByLocalPlayer() && Projectile.localAI[0] >= CollapseTicks) {
                    Projectile.Kill();
                    return;
                }
            }

            Projectile.localAI[1]++;
            ChannelAI(collapse01);
        }

        /// <summary>owner 端权威 tick：存活判定 → 蓝耗 → 积热 → 瞄准 → 里程碑 → 伤害刷新</summary>
        private void OwnerTick() {
            GsHeatPlayer hp = Owner.GetModPlayer<GsHeatPlayer>();

            bool wantsChannel = UseChannelFlag ? Owner.channel : Owner.controlUseItem;
            if (!wantsChannel || Owner.CCed || Owner.noItems || Owner.HeldItem.type != BoundItemID
                || hp.HardLocked || hp.Locked || !GameModeSystem.GodSmithActive) {
                BeginCollapse();
                return;
            }

            //持续蓝耗：累加器取整扣除，断蓝即收束
            float costMult = (hp.InWhiteHot ? 0.7f : 1f) * Owner.manaCost;
            manaAccumulator += ManaPerSecond / 60f * costMult;
            if (manaAccumulator >= 1f) {
                int cost = (int)manaAccumulator;
                manaAccumulator -= cost;
                if (!Owner.CheckMana(cost, true)) {
                    BeginCollapse();
                    return;
                }
            }

            //积热：Lock 政策触顶会当帧进锁，下帧上方检查收束
            if (HeatPerTick > 0f) {
                hp.AddHeat(BoundScheme, HeatPerTick);
            }

            //瞄准：方向变化超阈值或心跳周期才发包（连续小抖动不占带宽）
            Vector2 aim = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX * Owner.direction);
            if ((aim - Projectile.velocity).LengthSquared() > 0.0006f
                || Main.GameUpdateCount - lastAimSyncTick >= 12) {
                Projectile.velocity = aim;
                Projectile.netUpdate = true;
                lastAimSyncTick = Main.GameUpdateCount;
            }

            //热段里程碑：跨段才过线，远端按段渲染
            int stage = hp.HeatStage;
            if ((int)Projectile.ai[1] != stage) {
                Projectile.ai[1] = stage;
                Projectile.netUpdate = true;
            }

            //伤害真值只在 owner 端有意义（命中在 owner 端裁决），每帧动态跟白热乘区
            Projectile.damage = Math.Max(1, (int)(Owner.GetWeaponDamage(Owner.HeldItem) * TickDamageCoef));

            OwnerExtraTick(hp);
        }

        /// <summary>owner 端附加逻辑（目标锁定/回复等）</summary>
        protected virtual void OwnerExtraTick(GsHeatPlayer hp) { }

        /// <summary>下达收尾令（owner 端；一次性过线）</summary>
        protected void BeginCollapse() {
            if (!Collapsing) {
                Projectile.ai[2] = 1f;
                Projectile.netUpdate = true;
            }
        }

        private GsHeatScheme BoundScheme
            => GodSmithScheme.TryGetScheme(BoundItemID, out GodSmithScheme s) ? s as GsHeatScheme : null;

        /// <summary>持械姿态（各端，用同步方向）</summary>
        private void UpdatePose() {
            Vector2 dir = AimUnit;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.ChangeDir(dir.X >= 0f ? 1 : -1);
            Owner.itemRotation = (dir * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, dir.ToRotation() - MathHelper.PiOver2);
            Projectile.Center = Owner.MountedCenter + dir * MuzzleOffset;
        }

        public sealed override bool? CanDamage() => Collapsing ? false : DamageGate();
    }
}
