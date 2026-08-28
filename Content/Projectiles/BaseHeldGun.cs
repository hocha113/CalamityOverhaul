using InnoVault.GameContent.BaseEntity;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    /// <summary>
    /// Shoot 瞬间 <see cref="SpawnHeldProj{T}"/> 生成，松键且无 StayAlive 自毁；无固定开火管线，子类自管 AI
    /// </summary>
    public abstract class BaseHeldGun : BaseHeldProj
    {
        /// <summary>CanConsumeAmmo 返回此值；仅 <see cref="ConsumeAmmo"/> 放行消耗</summary>
        public static bool AmmoConsumeContext { get; private set; }

        /// <summary>Shoot 里调；用 T 的弹幕ID，勿用 Shoot 的 type（会被 useAmmo 换掉）</summary>
        /// <returns>恒 false，可作 Shoot 返回值</returns>
        public static bool SpawnHeldProj<T>(Player player, EntitySource_ItemUse_WithAmmo source) where T : BaseHeldGun {
            int heldType = ModContent.ProjectileType<T>();
            if (player.CountProjectilesOfID(heldType) <= 0) {
                Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, heldType, 0, 0, player.whoAmI);
            }
            return false;
        }

        #region 识别属性
        /// <summary>所属物品ID，不符则自杀</summary>
        public abstract int TargetID { get; }
        /// <summary><see cref="CWRLoad"/> 用，默认 false</summary>
        public virtual bool IsCrossbow => false;
        /// <summary>无视节约，默认 true；<see cref="CWRLoad"/> / <see cref="ConsumeAmmo"/></summary>
        public virtual bool MustConsumeAmmunition => true;
        /// <summary>右键开火，默认 false；<see cref="CWRLoad"/> / <see cref="WantsFireRight"/></summary>
        public virtual bool CanRightClick => false;
        /// <summary>手持展示，默认 true；ModGanged / 默认绘制</summary>
        public virtual bool OnHandheldDisplayBool => true;
        #endregion

        #region 持握与后坐力参数（在 SetGunProperty 中配置）
        /// <summary>闲置持枪点 X，默认15</summary>
        public float HandIdleDistanceX = 15;
        /// <summary>闲置持枪点 Y，默认0</summary>
        public float HandIdleDistanceY = 0;
        /// <summary>瞄准持枪点 X，默认20</summary>
        public float HandFireDistanceX = 20;
        /// <summary>瞄准持枪点 Y，默认-4</summary>
        public float HandFireDistanceY = -4;
        /// <summary>闲置仰角(周角)，默认12</summary>
        public float AngleFirearmRest = 12f;
        /// <summary>闲置右手角矫正(周角)</summary>
        public float ArmRotSengsFrontNoFireOffset;
        /// <summary>闲置左手角矫正(周角)</summary>
        public float ArmRotSengsBackNoFireOffset;
        public bool Onehanded;
        /// <summary>瞄准→闲置过渡速，默认0.2</summary>
        public float AimingAnimationSpeed = 0.2f;
        /// <summary>始终瞄准(旧 AlwaysSetInFireRoding)</summary>
        public bool AlwaysAimPose;
        /// <summary>枪压上抬，默认0</summary>
        public float GunPressure = 0;
        /// <summary>枪压恢复，默认0.01</summary>
        public float ControlForce = 0.01f;
        /// <summary>制退模长，0=无，默认0</summary>
        public float RecoilRetroForceMagnitude = 0;
        /// <summary>制退恢复，近1更慢，默认0.6</summary>
        public float RecoilOffsetRecoverValue = 0.6f;
        /// <summary>枪口沿枪身偏移(旧 ShootPosToMouLengValue)</summary>
        public float MuzzleForwardOffset = 0;
        /// <summary>枪口垂直偏移(旧 ShootPosNorlLengValue)</summary>
        public float MuzzleNormalOffset = 0;
        /// <summary>枪口火光，0关，默认1</summary>
        public float FireLight = 1;
        #endregion

        #region 运行时状态
        /// <summary>后坐俯仰(旧 OffsetRot)</summary>
        public float RecoilPitch;
        /// <summary>后坐制退(旧 OffsetPos)</summary>
        public Vector2 RecoilOffset;
        public float ArmRotSengsFront;
        public float ArmRotSengsBack;
        /// <summary>姿态进度 0闲置~1瞄准</summary>
        protected float aimProgress;
        public int fireIndex;
        /// <summary>弹药预览，不消耗</summary>
        public ShootState AmmoState;
        /// <summary>魔力恢复延迟基准</summary>
        private float manaRegenDelayValue;
        /// <summary>非开火时刷新，已同步</summary>
        private bool mouseUIFree = true;
        private bool oldMouseUIFree = true;
        #endregion

        #region 快捷访问
        /// <summary>ai[0] 通用计时</summary>
        public ref float Time => ref Projectile.ai[0];
        /// <summary>ai[1] 开火冷却，PreUpdate 递减</summary>
        public float FireCooldown {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }
        public bool HasAmmo => Item.useAmmo == AmmoID.None || AmmoState.HasAmmo;
        /// <summary>攻速系数，换算开火间隔</summary>
        public virtual float AttackSpeed => Owner.GetWeaponAttackSpeed(Item) + Item.GetPrefixState().shootSpeedMult - 1f;
        public int AmmoTypes => AmmoState.AmmoTypes;
        public int WeaponDamage => AmmoState.WeaponDamage;
        public float WeaponKnockback => AmmoState.WeaponKnockback;
        /// <summary>射击向量(鼠标向)</summary>
        public Vector2 ShootVelocity => UnitToMouseV * AmmoState.ShootSpeed;
        /// <summary>射击向量(枪旋)</summary>
        public Vector2 ShootVelocityInProjRot => Projectile.rotation.ToRotationVector2() * AmmoState.ShootSpeed;
        public virtual Vector2 ShootPos => GetMuzzlePos(MuzzleForwardOffset, MuzzleNormalOffset);
        /// <summary>CWRGunShoot 生成源</summary>
        public virtual EntitySource_ItemUse_WithAmmo Source => new(Owner, Item, AmmoState.UseAmmoItemType, "CWRGunShoot");
        /// <summary>鼠标未占UI，已同步</summary>
        public bool MouseUIFree => mouseUIFree;
        /// <summary>未悬停可交互物，仅主人端有意义</summary>
        public bool MouseIconFree => !Owner.cursorItemIconEnabled && Owner.cursorItemIconID == ItemID.None;
        /// <summary>可操作，默认排除亵渎水晶</summary>
        public virtual bool GunCanUse => !Owner.GetPlayerProfanedCrystalBuffs();
        public virtual bool WantsFireLeft => DownLeft && mouseUIFree && GunCanUse;
        public virtual bool WantsFireRight => CanRightClick && DownRight && !DownLeft && mouseUIFree && MouseIconFree && GunCanUse;
        /// <summary>开火尝试，兼驱动 BaseHeldProj 鼠标同步</summary>
        public override bool CanFire => (DownLeft || DownRight && CanRightClick && MouseIconFree) && mouseUIFree;
        /// <summary>瞄准时保持鼠标同步。帧模节流：空闲瞄准的姿态包从鼠标一动就发（~60Hz）
        /// 压到 ~15Hz，仅影响旁观端看到的持枪朝向平滑度；开火窗口由 <see cref="CanFire"/>
        /// 驱动全速同步，命中判定端的瞄向不受影响</summary>
        public override bool CanMouseNet => AlwaysAimPose
            && (Main.GameUpdateCount + (uint)Projectile.whoAmI) % 4 == 0;
        /// <summary>绘制位矫正，跟身体起伏</summary>
        public Vector2 SpecialDrawPositionOffset => CanFire ? Vector2.Zero : Owner.CWR().SpecialDrawPositionOffset;
        /// <summary>发光层，null 不绘</summary>
        public virtual Asset<Texture2D> GlowAsset => null;
        public float DrawGunBodyRotOffset;
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

        //不跟速度，防抽搐
        public override bool ShouldUpdatePosition() => false;

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

        /// <summary>SetDefaults 末配置持握/后坐力</summary>
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

        /// <summary>松键后仍存活，默认 false</summary>
        public virtual bool StayAlive() => false;

        /// <summary>保活/自毁/手持注册/弹药预览/占用锁/冷却；重写须调 base</summary>
        public override bool PreUpdate() {
            //松键且无 StayAlive 则自毁
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

        /// <summary>锁物品使用与切枪，PreUpdate 在开火/冷却时调</summary>
        public void KeepWeaponOccupied() {
            Owner.itemTime = 2;
            UIInputGuard.SuppressWeaponSwitch();
        }

        private void UpdateMouseUIFree() {
            //开火中不改，防UI打断抖动
            if (CanFire || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            mouseUIFree = !Owner.mouseInterface;
            if (oldMouseUIFree != mouseUIFree) {
                NetUpdate();
            }
            oldMouseUIFree = mouseUIFree;
        }

        /// <summary>flags[2]=mouseUIFree，子类从3起</summary>
        public override BitsByte SendBitsByte(BitsByte flags) {
            flags = base.SendBitsByte(flags);
            flags[2] = mouseUIFree;
            return flags;
        }

        /// <summary>flags[2]=mouseUIFree，子类从3起</summary>
        public override void ReceiveBitsByte(BitsByte flags) {
            base.ReceiveBitsByte(flags);
            mouseUIFree = flags[2];
        }
        #endregion

        #region 姿态工具
        /// <summary>闲置↔瞄准过渡+后坐+手臂；子类 AI 每帧调</summary>
        public void UpdateHeldPose(bool aiming) {
            //冲刺旋转不瞄准
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

        public virtual float GetAimGunRot() => ToMouseA - RecoilPitch * DirSign;

        public virtual Vector2 GetAimGunCenter() {
            Vector2 gunBodyRotOffset = Projectile.rotation.ToRotationVector2() * HandFireDistanceX;
            Vector2 gunHeldOffsetY = new Vector2(0, HandFireDistanceY * SafeGravDir);
            return Owner.GetPlayerStabilityCenter() + gunBodyRotOffset + gunHeldOffsetY + RecoilOffset;
        }

        public virtual float GetIdleGunRot() {
            float art = AngleFirearmRest;
            if (SafeGravDir < 0) {
                art = 360 - AngleFirearmRest;
            }
            float fullRotation = MathHelper.ToDegrees(Owner.fullRotation) * Owner.direction;
            float value = art + fullRotation;
            return Owner.direction > 0 ? MathHelper.ToRadians(value) : MathHelper.ToRadians(180 - value);
        }

        public virtual Vector2 GetIdleGunCenter() {
            Vector2 handOffset = new Vector2(Owner.direction * HandIdleDistanceX, HandIdleDistanceY * SafeGravDir);
            return Owner.GetPlayerStabilityCenter() + handOffset.RotatedBy(Owner.fullRotation);
        }

        /// <summary>瞬切瞄准姿态</summary>
        public void SnapToAimPose() {
            Owner.direction = ToMouse.X > 0 ? 1 : -1;
            Projectile.rotation = GetAimGunRot();
            Projectile.Center = GetAimGunCenter();
            ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
            aimProgress = 1f;
        }

        public virtual void SetCompositeArm() {
            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                if (!Onehanded) {
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
                }
            }
        }

        public void UpdateRecoil() {
            RecoilPitch = MathHelper.Clamp(RecoilPitch - ControlForce, 0, GunPressure * 2);
            if (RecoilOffset != Vector2.Zero) {
                RecoilOffset *= RecoilOffsetRecoverValue;
                if (RecoilOffset.LengthSquared() < 0.0001f) {
                    RecoilOffset = Vector2.Zero;
                }
            }
        }

        /// <summary>开火时上抬+制退</summary>
        public virtual void CreateRecoil() {
            RecoilPitch += GunPressure;
            if (RecoilRetroForceMagnitude > 0) {
                RecoilOffset -= ShootVelocity.UnitVector() * RecoilRetroForceMagnitude;
            }
        }
        #endregion

        #region 弹药与魔力工具
        public Vector2 GetMuzzlePos(float forward, float normal) {
            Vector2 norlVr = (Projectile.rotation + (DirSign > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2)).ToRotationVector2();
            return Projectile.Center + Projectile.rotation.ToRotationVector2() * forward + norlVr * normal;
        }

        /// <summary>全端消耗弹药；期间放行 AmmoConsumeContext</summary>
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

        /// <summary>按 useTime/攻速累加冷却</summary>
        public void SetFireCooldown(float timeMultiplier = 1f) {
            FireCooldown += MathF.Max(Item.useTime * timeMultiplier / AttackSpeed, 1f);
        }

        /// <summary>按住开火时压魔力恢复</summary>
        protected void HoldManaRegenDelay() {
            if (manaRegenDelayValue == 0) {
                manaRegenDelayValue = Owner.maxRegenDelay;
            }
            Owner.manaRegenDelay = manaRegenDelayValue;
        }

        /// <summary>支付魔力，成功与否</summary>
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

        /// <summary>物品使用已付魔力，首发跳过</summary>
        protected bool manaPaidByItemUse = true;

        /// <summary>每发魔力，首发代付其后 TryConsumeMana</summary>
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
        /// <summary>开火音；物品 UseSound 宜置 null 防重复</summary>
        public virtual SoundStyle? ShootSound => Item.UseSound;

        public virtual void PlayShootSound() {
            if (ShootSound.HasValue) {
                SoundEngine.PlaySound(ShootSound.Value, Projectile.Center);
            }
        }

        public void CreateFireLight() {
            if (FireLight > 0) {
                Lighting.AddLight(ShootPos, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(0.3f, 0.65f)
                    , Color.Red, Color.Gold).ToVector3() * FireLight);
            }
        }

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

        /// <summary>默认枪体+可选发光层</summary>
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
