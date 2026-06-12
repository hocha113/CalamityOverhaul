using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RangedModify.Core
{
    /// <summary>
    /// 新一代手持武器基类，直接建立在 InnoVault <see cref="BaseHeldProj"/> 之上
    /// <para>手持弹幕由物品的 <see cref="ModItem.Shoot"/> 在使用瞬间生成（物品侧调用 <see cref="SpawnHeldProj{T}"/>），
    /// 松开按键且没有未结算状态时自动销毁，不再使用选中即生成的 SetHeldProj 模式</para>
    /// <para>它不包含任何固定的开火管线，子类完全拥有自己的 <see cref="ModProjectile.AI"/> 循环</para>
    /// <para>基类只负责三件事：
    /// 一，生存性维护（物品校验、玩家状态校验、闲置自毁、武器占用锁）；
    /// 二，提供可选调用的工具（持枪姿态、后坐力、弹药、魔力、枪口、绘制）；
    /// 三，向 CWRLoad/ModGanged 等基础设施暴露识别属性</para>
    /// </summary>
    public abstract class BaseHeldGun : BaseHeldProj
    {
        /// <summary>
        /// 弹药消耗上下文开关：物品使用本身不应消耗弹药（物品侧的 <see cref="ModItem.CanConsumeAmmo"/> 返回该值），
        /// 只有手持弹幕经 <see cref="ConsumeAmmo"/> 主动拾取时才放行消耗
        /// </summary>
        public static bool AmmoConsumeContext { get; private set; }

        /// <summary>
        /// 在物品的 <see cref="ModItem.Shoot"/> 中调用：若场上没有对应手持弹幕则生成一个
        /// <para>注意必须显式使用 T 的弹幕ID而非 Shoot 传入的 type，后者会被 useAmmo 转化为弹药射弹</para>
        /// </summary>
        /// <returns>恒为<see langword="false"/>，可直接作为 Shoot 的返回值，阻止默认射击</returns>
        public static bool SpawnHeldProj<T>(Player player, EntitySource_ItemUse_WithAmmo source) where T : BaseHeldGun {
            int heldType = ModContent.ProjectileType<T>();
            if (player.CountProjectilesOfID(heldType) <= 0) {
                Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, heldType, 0, 0, player.whoAmI);
            }
            return false;
        }

        #region 识别属性
        /// <summary>
        /// 所属物品的ID，玩家手持物品与之不符时弹幕会自杀
        /// </summary>
        public abstract int TargetID { get; }
        /// <summary>
        /// 是否是一把弩，供 <see cref="CWRLoad"/> 数据表使用，默认为<see langword="false"/>
        /// </summary>
        public virtual bool IsCrossbow => false;
        /// <summary>
        /// 是否必定消耗弹药（无视弹药节约效果），供 <see cref="CWRLoad"/> 数据表与 <see cref="ConsumeAmmo"/> 使用，默认为<see langword="true"/>
        /// </summary>
        public virtual bool MustConsumeAmmunition => true;
        /// <summary>
        /// 是否响应右键开火，供 <see cref="CWRLoad"/> 数据表与 <see cref="WantsFireRight"/> 使用，默认为<see langword="false"/>
        /// </summary>
        public virtual bool CanRightClick => false;
        /// <summary>
        /// 当前是否处于手持展示状态，供 ModGanged 兼容层与默认绘制使用，默认为<see langword="true"/>
        /// </summary>
        public virtual bool OnHandheldDisplayBool => true;
        #endregion

        #region 持握与后坐力参数（在 SetGunProperty 中配置）
        /// <summary>闲置时持枪点相对玩家中心的水平距离，默认为15</summary>
        public float HandIdleDistanceX = 15;
        /// <summary>闲置时持枪点相对玩家中心的竖直距离，默认为0</summary>
        public float HandIdleDistanceY = 0;
        /// <summary>瞄准时持枪点相对玩家中心的距离，默认为20</summary>
        public float HandFireDistanceX = 20;
        /// <summary>瞄准时持枪点的竖直偏移，默认为-4</summary>
        public float HandFireDistanceY = -4;
        /// <summary>闲置时枪体的仰角（周角而非弧度角），默认为12</summary>
        public float AngleFirearmRest = 12f;
        /// <summary>闲置时右手角度矫正值（周角）</summary>
        public float ArmRotSengsFrontNoFireOffset;
        /// <summary>闲置时左手角度矫正值（周角）</summary>
        public float ArmRotSengsBackNoFireOffset;
        /// <summary>是否单手持握，默认为<see langword="false"/></summary>
        public bool Onehanded;
        /// <summary>从瞄准回到闲置姿势的过渡速度，默认为0.2f</summary>
        public float AimingAnimationSpeed = 0.2f;
        /// <summary>是否始终保持瞄准姿势（替代旧框架的 InOwner_HandState_AlwaysSetInFireRoding）</summary>
        public bool AlwaysAimPose;
        /// <summary>枪压，决定每次 <see cref="CreateRecoil"/> 的上抬力度，默认为0</summary>
        public float GunPressure = 0;
        /// <summary>压枪力度，决定枪压恢复速度，默认为0.01f</summary>
        public float ControlForce = 0.01f;
        /// <summary>后坐力制退模长，为0时不产生制退位移，默认为0</summary>
        public float RecoilRetroForceMagnitude = 0;
        /// <summary>制退位移恢复系数，越接近1恢复越慢，默认为0.6f</summary>
        public float RecoilOffsetRecoverValue = 0.6f;
        /// <summary>枪口沿枪身方向的偏移（原 ShootPosToMouLengValue）</summary>
        public float MuzzleForwardOffset = 0;
        /// <summary>枪口垂直枪身方向的偏移（原 ShootPosNorlLengValue）</summary>
        public float MuzzleNormalOffset = 0;
        /// <summary>开火时枪口火光强度，为0时关闭，默认为1</summary>
        public float FireLight = 1;
        #endregion

        #region 运行时状态
        /// <summary>当前的后坐力俯仰角累积（原 OffsetRot）</summary>
        public float RecoilPitch;
        /// <summary>当前的后坐力制退位移累积（原 OffsetPos）</summary>
        public Vector2 RecoilOffset;
        /// <summary>右手最终角度值，由 <see cref="UpdateHeldPose"/> 写入</summary>
        public float ArmRotSengsFront;
        /// <summary>左手最终角度值，由 <see cref="UpdateHeldPose"/> 写入</summary>
        public float ArmRotSengsBack;
        /// <summary>姿态过渡进度，0为完全闲置，1为完全瞄准</summary>
        protected float aimProgress;
        /// <summary>一个通用的开火计数器</summary>
        public int fireIndex;
        /// <summary>每帧刷新的弹药预览（不消耗），由 <see cref="PreUpdate"/> 维护</summary>
        public ShootState AmmoState;
        /// <summary>魔力恢复延迟基准值，首次使用魔力工具时取 <see cref="Player.maxRegenDelay"/></summary>
        private float manaRegenDelayValue;
        /// <summary>鼠标是否未悬停在UI上（仅在非开火期间更新，自动同步）</summary>
        private bool mouseUIFree = true;
        private bool oldMouseUIFree = true;
        #endregion

        #region 快捷访问
        /// <summary>一个通用计时器，存储在 ai[0] 中以获得自动同步</summary>
        public ref float Time => ref Projectile.ai[0];
        /// <summary>开火冷却计时器，存储在 ai[1] 中以获得自动同步，由 <see cref="PreUpdate"/> 自动递减</summary>
        public float FireCooldown {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }
        /// <summary>是否拥有可用弹药；不需要弹药的武器恒为<see langword="true"/></summary>
        public bool HasAmmo => Item.useAmmo == AmmoID.None || AmmoState.HasAmmo;
        /// <summary>攻速加成系数（含玩家加成与CWR前缀），用于换算开火间隔</summary>
        public virtual float AttackSpeed => Owner.GetWeaponAttackSpeed(Item) + Item.GetPrefixState().shootSpeedMult - 1f;
        /// <summary>当前弹药转化出的弹幕类型</summary>
        public int AmmoTypes => AmmoState.AmmoTypes;
        /// <summary>实时武器伤害</summary>
        public int WeaponDamage => AmmoState.WeaponDamage;
        /// <summary>实时武器击退</summary>
        public float WeaponKnockback => AmmoState.WeaponKnockback;
        /// <summary>射击向量，以鼠标方向为基准</summary>
        public Vector2 ShootVelocity => UnitToMouseV * AmmoState.ShootSpeed;
        /// <summary>射击向量，以枪体当前旋转角为基准</summary>
        public Vector2 ShootVelocityInProjRot => Projectile.rotation.ToRotationVector2() * AmmoState.ShootSpeed;
        /// <summary>枪口位置</summary>
        public virtual Vector2 ShootPos => GetMuzzlePos(MuzzleForwardOffset, MuzzleNormalOffset);
        /// <summary>携带CWRGunShoot标签的物品生成源</summary>
        public virtual EntitySource_ItemUse_WithAmmo Source => new(Owner, Item, AmmoState.UseAmmoItemType, "CWRGunShoot");
        /// <summary>鼠标是否未被UI占用（已同步，可在所有端安全使用）</summary>
        public bool MouseUIFree => mouseUIFree;
        /// <summary>鼠标指针是否未悬停在可交互物上（仅主人端有意义，远程端恒为真）</summary>
        public bool MouseIconFree => !Owner.cursorItemIconEnabled && Owner.cursorItemIconID == ItemID.None;
        /// <summary>武器是否可被操作，默认排除亵渎水晶变身状态</summary>
        public virtual bool GunCanUse => !Owner.GetPlayerProfanedCrystalBuffs();
        /// <summary>玩家是否正在尝试左键开火（已含UI与可用性判定）</summary>
        public virtual bool WantsFireLeft => DownLeft && mouseUIFree && GunCanUse;
        /// <summary>玩家是否正在尝试右键开火（已含UI与可用性判定）</summary>
        public virtual bool WantsFireRight => CanRightClick && DownRight && !DownLeft && mouseUIFree && MouseIconFree && GunCanUse;
        /// <summary>
        /// 是否处于开火尝试状态，该属性同时驱动 <see cref="BaseHeldProj"/> 的鼠标同步
        /// </summary>
        public override bool CanFire => (DownLeft || (DownRight && CanRightClick && MouseIconFree)) && mouseUIFree;
        /// <summary>瞄准期间保持鼠标同步，让动作在队友眼中保持平滑</summary>
        public override bool CanMouseNet => AlwaysAimPose;
        /// <summary>手持的绘制矫正值，跟随玩家身体动画的起伏</summary>
        public Vector2 SpecialDrawPositionOffset => CanFire ? Vector2.Zero : Owner.CWR().SpecialDrawPositionOffset;
        /// <summary>发光层资产，子类用 [VaultLoaden] 加载后返回，为 null 时不绘制</summary>
        public virtual Asset<Texture2D> GlowAsset => null;
        /// <summary>绘制时附加的旋转矫正值</summary>
        public float DrawGunBodyRotOffset;
        /// <summary>
        /// 自定义本地化键，沿用所属物品的名字
        /// </summary>
        public override LocalizedText DisplayName {
            get {
                if (TargetID <= ItemID.None) {
                    return base.DisplayName;
                }
                return TargetID < ItemID.Count ?
                    Language.GetText("ItemName." + ItemID.Search.GetName(TargetID))
                    : ItemLoader.GetItem(TargetID).GetLocalization("DisplayName");
            }
        }
        #endregion

        #region 生命周期
        public override bool IsLoadingEnabled(Mod mod) => TargetID > ItemID.None;

        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);

        //手持弹幕不应受速度更新，否则会发生轻微的抽搐
        public override bool ShouldUpdatePosition() => false;

        //手持枪体本身默认没有碰撞伤害
        public override bool? CanDamage() => false;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.hide = true;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            VaultUtils.SafeLoadItem(TargetID);
            SetGunProperty();
        }

        /// <summary>
        /// 在 <see cref="SetDefaults"/> 末尾被调用，用于配置持握与后坐力等参数
        /// </summary>
        public virtual void SetGunProperty() {

        }

        public override bool ExtraPreSet() {
            if (Item.type != TargetID || !Owner.active || Owner.dead || Owner.CCed) {
                Projectile.Kill();
                NetUpdate();
                return false;
            }
            return true;
        }

        /// <summary>
        /// 松开按键后是否仍需存活，用于结算未完成的状态（持续射线、终幕、已装填的特殊弹等），默认为<see langword="false"/>
        /// </summary>
        public virtual bool StayAlive() => false;

        /// <summary>
        /// 基础的生存性维护：保活、闲置自毁、手持注册、UI安全判定、弹药预览刷新、武器占用锁与冷却递减
        /// <para>子类重写时应当调用 <see langword="base"/>.<see cref="PreUpdate"/></para>
        /// </summary>
        public override bool PreUpdate() {
            //该弹幕由物品使用生成，松开所有开火键且没有待结算状态时自行销毁
            if (!DownLeft && !DownRight && !StayAlive()) {
                Projectile.Kill();
                return false;
            }

            Projectile.timeLeft = 2;
            SetHeld();
            UpdateMouseUIFree();
            AmmoState = Owner.GetShootState("CWRGunShoot");

            if (CanFire) {
                KeepWeaponOccupied();
                CWRRef.UpdateRogueStealth(Owner);
            }
            if (FireCooldown > 0) {
                KeepWeaponOccupied();
                FireCooldown--;
            }
            return true;
        }

        /// <summary>
        /// 锁定物品使用与武器切换，开火与冷却期间由 <see cref="PreUpdate"/> 自动调用
        /// </summary>
        public void KeepWeaponOccupied() {
            Owner.itemTime = 2;
            Owner.CWR().DontSwitchWeaponTime = 2;
        }

        private void UpdateMouseUIFree() {
            //只有在玩家不进行开火尝试时才能更改空闲状态，防止开火中途被UI打断造成抖动
            if (CanFire || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            mouseUIFree = !Owner.CWR().UIMouseInterface;
            if (oldMouseUIFree != mouseUIFree) {
                NetUpdate();
            }
            oldMouseUIFree = mouseUIFree;
        }

        /// <summary>
        /// 发送一个比特体，基类已占用至2号位，子类重写时应从3号位开始使用
        /// </summary>
        public override BitsByte SendBitsByte(BitsByte flags) {
            flags = base.SendBitsByte(flags);
            flags[2] = mouseUIFree;
            return flags;
        }

        /// <summary>
        /// 接收一个比特体，基类已占用至2号位，子类重写时应从3号位开始使用
        /// </summary>
        public override void ReceiveBitsByte(BitsByte flags) {
            base.ReceiveBitsByte(flags);
            mouseUIFree = flags[2];
        }
        #endregion

        #region 姿态工具
        /// <summary>
        /// 统一更新持枪姿态：在闲置姿势与瞄准姿势之间平滑过渡，处理后坐力恢复并设置手臂
        /// <para>在子类 AI 中每帧调用一次即可获得完整的持枪表现</para>
        /// </summary>
        /// <param name="aiming">当前是否处于瞄准（开火尝试）状态</param>
        public void UpdateHeldPose(bool aiming) {
            //冲刺旋转期间不应强行瞄准
            bool effectiveAim = (aiming || AlwaysAimPose) && !Owner.CWR().IsRotatingDuringDash;
            aimProgress = MathHelper.Clamp(aimProgress + (effectiveAim ? 1f : -AimingAnimationSpeed), 0f, 1f);

            float idleArmFront = (60 + ArmRotSengsFrontNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            float idleArmBack = (110 + ArmRotSengsBackNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            float idleRot = GetIdleGunRot();
            Vector2 idlePos = GetIdleGunCenter();

            int origDirection = Owner.direction;
            int targetDirection = ToMouse.X > 0 ? 1 : -1;

            Owner.direction = targetDirection;
            float aimRot = GetAimGunRot();

            float origProjRot = Projectile.rotation;
            Projectile.rotation = aimRot;
            Vector2 aimPos = GetAimGunCenter();
            Projectile.rotation = origProjRot;

            float aimArmRot = (MathHelper.PiOver2 * SafeGravDir - aimRot) * DirSign * SafeGravDir;
            Owner.direction = origDirection;

            Projectile.rotation = idleRot.AngleLerp(aimRot, aimProgress);
            Projectile.Center = Vector2.Lerp(idlePos, aimPos, aimProgress);
            ArmRotSengsFront = idleArmFront.AngleLerp(aimArmRot, aimProgress);
            ArmRotSengsBack = idleArmBack.AngleLerp(aimArmRot, aimProgress);

            if (aimProgress >= 0.9f) {
                Owner.direction = targetDirection;
            }

            UpdateRecoil();
            SetCompositeArm();
        }

        /// <summary>
        /// 获取瞄准状态下枪体的旋转角
        /// </summary>
        public virtual float GetAimGunRot() => ToMouseA - RecoilPitch * DirSign;

        /// <summary>
        /// 获取瞄准状态下枪体的中心位置
        /// </summary>
        public virtual Vector2 GetAimGunCenter() {
            Vector2 gunBodyRotOffset = Projectile.rotation.ToRotationVector2() * HandFireDistanceX;
            Vector2 gunHeldOffsetY = new Vector2(0, HandFireDistanceY * SafeGravDir);
            return Owner.GetPlayerStabilityCenter() + gunBodyRotOffset + gunHeldOffsetY + RecoilOffset;
        }

        /// <summary>
        /// 获取闲置状态下枪体的旋转角
        /// </summary>
        public virtual float GetIdleGunRot() {
            float art = AngleFirearmRest;
            if (SafeGravDir < 0) {
                art = 360 - AngleFirearmRest;
            }
            float fullRotation = MathHelper.ToDegrees(Owner.fullRotation) * Owner.direction;
            float value = art + fullRotation;
            return Owner.direction > 0 ? MathHelper.ToRadians(value) : MathHelper.ToRadians(180 - value);
        }

        /// <summary>
        /// 获取闲置状态下枪体的中心位置
        /// </summary>
        public virtual Vector2 GetIdleGunCenter() {
            Vector2 handOffset = new Vector2(Owner.direction * HandIdleDistanceX, HandIdleDistanceY * SafeGravDir);
            return Owner.GetPlayerStabilityCenter() + handOffset.RotatedBy(Owner.fullRotation);
        }

        /// <summary>
        /// 将枪体立即设置为瞄准姿态（不经过过渡），用于开火瞬间矫正延迟帧
        /// </summary>
        public void SnapToAimPose() {
            Owner.direction = ToMouse.X > 0 ? 1 : -1;
            Projectile.rotation = GetAimGunRot();
            Projectile.Center = GetAimGunCenter();
            ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
            aimProgress = 1f;
        }

        /// <summary>
        /// 按当前手臂角度设置玩家复合手臂
        /// </summary>
        public virtual void SetCompositeArm() {
            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                if (!Onehanded) {
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
                }
            }
        }

        /// <summary>
        /// 更新后坐力恢复，已由 <see cref="UpdateHeldPose"/> 自动调用
        /// </summary>
        public void UpdateRecoil() {
            RecoilPitch = MathHelper.Clamp(RecoilPitch - ControlForce, 0, GunPressure * 2);
            if (RecoilOffset != Vector2.Zero) {
                RecoilOffset *= RecoilOffsetRecoverValue;
                if (RecoilOffset.LengthSquared() < 0.0001f) {
                    RecoilOffset = Vector2.Zero;
                }
            }
        }

        /// <summary>
        /// 制造一次后坐力（枪压上抬与制退位移），应在开火时调用
        /// </summary>
        public virtual void CreateRecoil() {
            RecoilPitch += GunPressure;
            if (RecoilRetroForceMagnitude > 0) {
                RecoilOffset -= ShootVelocity.UnitVector() * RecoilRetroForceMagnitude;
            }
        }
        #endregion

        #region 弹药与魔力工具
        /// <summary>
        /// 获取枪口位置
        /// </summary>
        /// <param name="forward">沿枪身方向的偏移</param>
        /// <param name="normal">垂直枪身方向的偏移</param>
        public Vector2 GetMuzzlePos(float forward, float normal) {
            Vector2 norlVr = (Projectile.rotation + (DirSign > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2)).ToRotationVector2();
            return Projectile.Center + Projectile.rotation.ToRotationVector2() * forward + norlVr * normal;
        }

        /// <summary>
        /// 实际拾取并消耗一发弹药，所有端均可调用以保证背包消耗一致
        /// <para>消耗期间会放行 <see cref="AmmoConsumeContext"/>，物品侧的 CanConsumeAmmo 应返回该值</para>
        /// </summary>
        /// <param name="allowFreeChance">是否允许弹药节约效果生效（受 <see cref="MustConsumeAmmunition"/> 覆盖）</param>
        protected void ConsumeAmmo(bool allowFreeChance = true) {
            if (Item.useAmmo == AmmoID.None) {
                return;
            }
            bool dontConsume = allowFreeChance && !MustConsumeAmmunition
                && Owner.IsRangedAmmoFreeThisShot(new Item(AmmoState.UseAmmoItemType));
            AmmoConsumeContext = true;
            Owner.PickAmmo(Item, out _, out _, out _, out _, out _, dontConsume);
            AmmoConsumeContext = false;
        }

        /// <summary>
        /// 按物品使用时间与攻速加成累加开火冷却
        /// </summary>
        /// <param name="timeMultiplier">使用时间倍率</param>
        public void SetFireCooldown(float timeMultiplier = 1f) {
            FireCooldown += MathF.Max(Item.useTime * timeMultiplier / AttackSpeed, 1f);
        }

        /// <summary>
        /// 按住开火期间调用，抑制魔力自然恢复
        /// </summary>
        protected void HoldManaRegenDelay() {
            if (manaRegenDelayValue == 0) {
                manaRegenDelayValue = Owner.maxRegenDelay;
            }
            Owner.manaRegenDelay = manaRegenDelayValue;
        }

        /// <summary>
        /// 检查并支付一次魔力消耗，返回是否支付成功
        /// </summary>
        /// <param name="overrideMana">覆盖本次消耗的基础魔力值，不填则使用 <see cref="Item.mana"/></param>
        protected bool TryConsumeMana(int? overrideMana = null) {
            int baseMana = overrideMana ?? Item.mana;
            if (!Owner.CheckMana(Item, baseMana)) {
                return false;
            }
            int mana = (int)(baseMana * Owner.manaCost);
            Owner.statMana = Math.Max(Owner.statMana - mana, 0);
            HoldManaRegenDelay();
            return true;
        }

        /// <summary>
        /// 生成本弹幕的那次物品使用已经支付过一次魔力，首发射击应跳过支付
        /// </summary>
        protected bool manaPaidByItemUse = true;

        /// <summary>
        /// 魔法武器的每发魔力支付：首发由物品使用代付，其后每发经 <see cref="TryConsumeMana"/> 支付
        /// </summary>
        /// <param name="overrideMana">覆盖本次消耗的基础魔力值，不填则使用 <see cref="Item.mana"/></param>
        protected bool PayMana(int? overrideMana = null) {
            if (manaPaidByItemUse) {
                manaPaidByItemUse = false;
                HoldManaRegenDelay();
                return true;
            }
            return TryConsumeMana(overrideMana);
        }
        #endregion

        #region 表现工具
        /// <summary>
        /// 开火音效，物品侧的 UseSound 应设为 null 以避免使用动画重复出声，音效改由这里提供
        /// </summary>
        public virtual SoundStyle? ShootSound => Item.UseSound;

        /// <summary>
        /// 播放开火音效
        /// </summary>
        public virtual void PlayShootSound() {
            if (ShootSound.HasValue) {
                SoundEngine.PlaySound(ShootSound.Value, Projectile.Center);
            }
        }

        /// <summary>
        /// 在枪口制造火光照明，应在开火时调用
        /// </summary>
        public void CreateFireLight() {
            if (FireLight > 0) {
                Lighting.AddLight(ShootPos, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(0.3f, 0.65f)
                    , Color.Red, Color.Gold).ToVector3() * FireLight);
            }
        }

        /// <summary>
        /// 一个快捷的开火烟尘粒子效果
        /// </summary>
        public void SpawnGunFireDust(Vector2 pos = default, Vector2 velocity = default
            , float splNum = 1f, int dustID1 = 262, int dustID2 = 54, int dustID3 = 53) {
            if (pos == default) {
                pos = ShootPos;
            }
            if (velocity == default) {
                velocity = ShootVelocity;
            }
            pos += velocity.SafeNormalize(Vector2.Zero) * Projectile.width * Projectile.scale * 0.71f;
            for (int i = 0; i < 30 * splNum; i++) {
                int dustID = Main.rand.Next(6) switch {
                    0 => dustID1,
                    1 or 2 => dustID2,
                    _ => dustID3,
                };
                float num = Main.rand.NextFloat(3f, 13f) * splNum;
                Vector2 dustVel = new Vector2(num, 0f).RotatedBy(velocity.ToRotation());
                dustVel = dustVel.RotatedBy(-0.06f).RotatedByRandom(0.12f);
                int idx = Dust.NewDust(pos, 1, 1, dustID, dustVel.X, dustVel.Y, 0, default, Main.rand.NextFloat(0.5f, 1.5f));
                Main.dust[idx].noGravity = true;
                Main.dust[idx].position = pos;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (OnHandheldDisplayBool) {
                GunDraw(Projectile.Center - Main.screenPosition + SpecialDrawPositionOffset, ref lightColor);
            }
            return false;
        }

        /// <summary>
        /// 默认的枪体绘制：主体加可选的发光层，重写它以实现自定义绘制
        /// </summary>
        public virtual void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

            if (GlowAsset != null) {
                Main.EntitySpriteDraw(GlowAsset.Value, drawPos, null, Color.White
                    , Projectile.rotation + offsetRot, GlowAsset.Value.Size() / 2, Projectile.scale
                    , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
        }
        #endregion
    }
}
