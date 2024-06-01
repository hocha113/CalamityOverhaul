using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseBow : BaseHeldRanged
    {
        #region Date
        public virtual Texture2D TextureValue => CWRUtils.GetT2DValue(Texture);
        /// <summary>
        /// 右手角度值
        /// </summary>
        public float ArmRotSengsFront;
        /// <summary>
        /// 左手角度值
        /// </summary>
        public float ArmRotSengsBack;
        /// <summary>
        /// 右手基本角度值
        /// </summary>
        public int ArmRotSengsFrontBaseValue = 60;
        /// <summary>
        /// 左手基本角度值
        /// </summary>
        public int ArmRotSengsBackBaseValue = 70;
        /// <summary>
        /// 是否在<see cref="InOwner"/>执行后自动更新手臂参数，默认为<see langword="true"/>
        /// </summary>
        public bool SetArmRotBool = true;
        /// <summary>
        /// 手持距离，生效于非开火状态下，默认为15
        /// </summary>
        public float HandDistance = 15;
        /// <summary>
        /// 手持距离，生效于非开火状态下，默认为0
        /// </summary>
        public float HandDistanceY = 0;
        /// <summary>
        /// 手持距离，生效于开火状态下，默认为12
        /// </summary>
        public float HandFireDistance = 12;
        /// <summary>
        /// 手持距离，生效于开火状态下，默认为0
        /// </summary>
        public float HandFireDistanceY = 0;
        /// <summary>
        /// 一个开火周期中手臂动画开始的时间，默认为0
        /// </summary>
        public float HandRotStartTime = 0;
        /// <summary>
        /// 一个开火周期中手臂动画的播放速度，默认为0.4f
        /// </summary>
        public float HandRotSpeedSengs = 0.4f;
        /// <summary>
        /// 一个开火周期中手臂动画的播放幅度，默认为0.7f
        /// </summary>
        public float HandRotRange = 0.7f;
        /// <summary>
        /// 是否启用开火动画，默认为<see langword="true"/>
        /// </summary>
        public bool CanFireMotion = true;
        /// <summary>
        /// 开火后是否自动执行弹药消耗逻辑，默认为<see langword="true"/>
        /// </summary>
        public bool CanUpdateConsumeAmmoInShootBool = true;
        /// <summary>
        /// 开火时是否默认播放手持物品的使用音效<see cref="Item.UseSound"/>，但如果准备重写<see cref="SpanProj"/>，这个属性将失去作用，默认为<see langword="true"/>
        /// </summary>
        public bool FiringDefaultSound = true;
        /// <summary>
        /// 是否绘制箭矢动画，默认为<see langword="true"/>
        /// </summary>
        public bool BowArrowDrawBool = true;
        /// <summary>
        /// 绘制箭矢的数量，默认值为1
        /// </summary>
        public int BowArrowDrawNum = 1;
        /// <summary>
        /// 箭矢绘制模长偏移值，默认值为-16
        /// </summary>
        protected int DrawArrowMode = -16;
        /// <summary>
        /// 箭矢绘制角度矫正值，默认为0
        /// </summary>
        protected float DrawArrowOffsetRot = 0;
        /// <summary>
        /// 自定义绘制中心点，默认为<see cref="Vector2.Zero"/>，即不启用
        /// </summary>
        protected Vector2 CustomDrawOrig = Vector2.Zero;
        /// <summary>
        /// 开火额外矫正位置，这个值在开火后自动回归默认值<see cref="Vector2.Zero"/>
        /// </summary>
        public Vector2 FireOffsetPos = Vector2.Zero;
        /// <summary>
        /// 开火速度额外矫正，这个值在开火后自动回归默认值<see cref="Vector2.Zero"/>
        /// </summary>
        public Vector2 FireOffsetVector = Vector2.Zero;
        /// <summary>
        /// 是一把弓
        /// </summary>
        public bool IsBow = true;
        /// <summary>
        /// 射弹特殊生成属性，用于决定射弹的特殊行为，默认值为<see cref="SpanTypesEnum.None"/>
        /// </summary>
        public SpanTypesEnum ShootSpanTypeValue = SpanTypesEnum.None;
        /// <summary>
        /// 是否处于开火时间
        /// </summary>
        public override bool CanFire => DownLeft || (DownRight && CanRightClick && !onFire && SafeMousetStart);
        /// <summary>
        /// 是否允许手持状态，如果玩家关闭了手持动画设置，这个值将在非开火状态时返回<see langword="false"/>
        /// </summary>
        public override bool OnHandheldDisplayBool {
            get {
                if (WeaponHandheldDisplay) {
                    return true;
                }
                return CanFire;
            }
        }
        /// <summary>
        /// 获取来自物品的生成源
        /// </summary>
        protected EntitySource_ItemUse_WithAmmo Source => new EntitySource_ItemUse_WithAmmo(Owner, Item, UseAmmoItemType, "CWRBow");
        protected EntitySource_ItemUse_WithAmmo Source2 => new EntitySource_ItemUse_WithAmmo(Owner, Item, UseAmmoItemType);
        #endregion

        public void SetArmInFire() {
            ShootCoolingValue++;
            if (ShootCoolingValue > HandRotStartTime && CanFireMotion) {
                float backArmRotation = (Projectile.rotation * SafeGravDir + MathHelper.PiOver2) + MathHelper.Pi * DirSign;
                float amountValue = 1 - Projectile.ai[1] / (Item.useTime - HandRotStartTime);
                Player.CompositeArmStretchAmount stretch = amountValue.ToStretchAmount();
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
                Owner.SetCompositeArmFront(true, stretch, backArmRotation);
            }
        }

        public override void FiringIncident() {
            void setBaseFromeAI() {
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = ToMouseA;
                Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() 
                    * HandFireDistance + new Vector2(0, HandFireDistanceY * SafeGravDir);
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (ToMouseA + 0.5f * DirSign)) * DirSign;
                SetCompositeArm();
            }

            if (DownLeft) {
                setBaseFromeAI();
                if (HaveAmmo) {
                    onFire = true;
                    SetArmInFire();
                }
            }
            else {
                onFire = false;
            }

            if (DownRight && CanRightClick && !onFire && SafeMousetStart) {
                setBaseFromeAI();
                if (HaveAmmo) {
                    SafeMousetStart2 = true;
                    onFireR = true;
                    SetArmInFire();
                }
            }
            else {
                onFireR = false;
                SafeMousetStart2 = false;
            }
        }

        public virtual void PreInOwner() { }

        public override void InOwner() {
            PreInOwner();
            SetHeld();
            ArmRotSengsFront = ArmRotSengsFrontBaseValue * CWRUtils.atoR;
            ArmRotSengsBack = ArmRotSengsBackBaseValue * CWRUtils.atoR;

            Projectile.Center = Owner.GetPlayerStabilityCenter() + new Vector2(Owner.direction * HandDistance, HandDistanceY);
            int art = 20;
            if (SafeGravDir < 0) {
                art = 340;
            }
            Projectile.rotation = Owner.direction > 0 ? MathHelper.ToRadians(art) : MathHelper.ToRadians(180 - art);
            Projectile.timeLeft = 2;
            ModItem.IsBow = IsBow;
            SetCompositeArm();

            if (SafeMouseInterfaceValue) {
                FiringIncident();
            }

            PostInOwner();
        }

        public void SetCompositeArm() {
            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -Owner.direction);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -Owner.direction);
            }
        }

        public virtual void PostInOwner() { }

        public virtual void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, ShootVelocity + FireOffsetVector
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }

        public virtual void BowShootR() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center + FireOffsetPos, ShootVelocity + FireOffsetVector
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }

        /// <summary>
        /// 一个快捷创建属于卢克索饰品的发射事件，如果luxorsGift为<see langword="true"/>,
        /// 或者<see cref="CWRPlayer.TheRelicLuxor"/>大于0，便会调用该方法，在Firing方法之后调用
        /// </summary>
        public virtual void LuxirEvent() {
            float damageMult = 1f;
            if (Item.useTime < 10) {
                damageMult -= (10 - Item.useTime) / 10f;
            }
            int luxirDamage = Owner.ApplyArmorAccDamageBonusesTo(WeaponDamage * damageMult * 0.15f);
            if (luxirDamage > 1) {
                SpanLuxirProj(luxirDamage);
            }
        }

        public virtual int SpanLuxirProj(int luxirDamage) {
            return 0;
        }

        public virtual void SetShootAttribute() {

        }

        public virtual void PostBowShoot() {

        }

        public override void AI() {
            InOwner();
            if (overNoFireCeahks()) {
                SpanProj();
            }
            Time++;
        }

        public override void SpanProj() {
            if (Projectile.ai[1] > Item.useTime && (onFire || onFireR)) {
                if (ForcedConversionTargetAmmoFunc.Invoke()) {
                    AmmoTypes = ToTargetAmmo;
                }
                SetShootAttribute();
                if (Projectile.IsOwnedByLocalPlayer()) {
                    if (onFire) {
                        BowShoot();
                    }
                    if (onFireR) {
                        BowShootR();
                    }
                    if (CalOwner.luxorsGift || ModOwner.TheRelicLuxor > 0) {
                        LuxirEvent();
                    }
                    if (GlobalItemBehavior) {
                        ItemLoaderInFireSetBaver();
                    }
                    UpdateConsumeAmmo();
                }
                PostBowShoot();

                if (FiringDefaultSound) {
                    HanderPlaySound();
                }

                FireOffsetVector = FireOffsetPos = Vector2.Zero;
                ShootCoolingValue = 0;
                onFireR = onFire = false;
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (OnHandheldDisplayBool) {
                BowDraw(ref lightColor);
            }
            
            if (CWRServerConfig.Instance.BowArrowDraw && BowArrowDrawBool) {
                ArrowDraw();
            }
            return false;
        }

        public virtual void BowDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition
                , null, CanFire ? Color.White : lightColor
                , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }

        private void ArrowResourceProcessing(ref Texture2D value, Item arrow) {
            if (!arrow.consumable) {
                int newtype = ItemID.WoodenArrow;
                if (CWRIDs.ProjectileToSafeAmmoMap.TryGetValue(arrow.shoot, out int value2)) {
                    newtype = value2;
                }
                Main.instance.LoadItem(newtype);
                value = TextureAssets.Item[newtype].Value;
            }
        }

        public virtual void CustomArrowRP(ref Texture2D value, Item arrow) { }

        public void ArrowDraw() {
            int cooltime = 3;
            if (cooltime > Item.useTime / 3) {
                cooltime = Item.useTime / 3;
            }

            if (CanFire && Projectile.ai[1] > cooltime) {
                int useAmmoItemType = UseAmmoItemType;
                if (useAmmoItemType == ItemID.None) {
                    return;
                }
                if (useAmmoItemType > 0 && useAmmoItemType < TextureAssets.Item.Length) {
                    Main.instance.LoadItem(useAmmoItemType);
                }

                Texture2D arrowValue = TextureAssets.Item[useAmmoItemType].Value;
                Item arrowItemInds = new Item(useAmmoItemType);
                ArrowResourceProcessing(ref arrowValue, arrowItemInds);
                CustomArrowRP(ref arrowValue, arrowItemInds);

                if (ForcedConversionTargetAmmoFunc.Invoke() && ToTargetAmmoInDraw != -1) {
                    arrowValue = TextureAssets.Projectile[ToTargetAmmo].Value;
                    if (ToTargetAmmoInDraw > 0) {
                        arrowValue = TextureAssets.Projectile[ToTargetAmmoInDraw].Value;
                    }
                    if (ISForcedConversionDrawAmmoInversion) {
                        CustomDrawOrig = new Vector2(arrowValue.Width / 2, 0);
                        DrawArrowOffsetRot = MathHelper.Pi;
                    }
                }
                else {
                    CustomDrawOrig = Vector2.Zero;
                    DrawArrowOffsetRot = 0;
                }

                float drawRot = Projectile.rotation + MathHelper.PiOver2;
                float chordCoefficient = 1 - Projectile.ai[1] / Item.useTime;

                float lengsOFstValue = chordCoefficient * 16 + DrawArrowMode;
                Vector2 inprojRot = Projectile.rotation.ToRotationVector2();
                Vector2 offsetDrawPos = inprojRot * lengsOFstValue;
                Vector2 norlInRotUnit = inprojRot.GetNormalVector();
                Vector2 drawOrig = CustomDrawOrig == Vector2.Zero ? new(arrowValue.Width / 2, arrowValue.Height) : CustomDrawOrig;
                Vector2 drawPos = Projectile.Center - Main.screenPosition + offsetDrawPos;

                void drawArrow(float overOffsetRot = 0, Vector2 overOffsetPos = default) => Main.EntitySpriteDraw(arrowValue
                    , drawPos + (overOffsetPos == default ? Vector2.Zero : overOffsetPos)
                    , null, Color.White, drawRot + DrawArrowOffsetRot + overOffsetRot, drawOrig, Projectile.scale, SpriteEffects.FlipVertically);

                switch (BowArrowDrawNum) {
                    case 2:
                        drawArrow(0.3f * chordCoefficient);
                        drawArrow(-0.3f * chordCoefficient);
                        break;
                    case 3:
                        chordCoefficient += 0.5f;
                        if (chordCoefficient > 1) {
                            chordCoefficient = 1;
                        }
                        drawArrow(0.45f * chordCoefficient, norlInRotUnit * -1f);
                        drawArrow();
                        drawArrow(-0.45f * chordCoefficient, norlInRotUnit * 1f);
                        break;
                    case 4:
                        chordCoefficient += 0.3f;
                        if (chordCoefficient > 1) {
                            chordCoefficient = 1;
                        }
                        drawArrow(0.6f * chordCoefficient);
                        drawArrow(-0.6f * chordCoefficient);
                        drawArrow(0.2f * chordCoefficient);
                        drawArrow(-0.2f * chordCoefficient);
                        break;
                    case 5:
                        chordCoefficient += 0.3f;
                        if (chordCoefficient > 1) {
                            chordCoefficient = 1;
                        }
                        drawArrow(0.7f * chordCoefficient, norlInRotUnit * 0.3f);
                        drawArrow(-0.7f * chordCoefficient, norlInRotUnit * -0.3f);
                        drawArrow();
                        drawArrow(0.35f * chordCoefficient, norlInRotUnit * 0.2f);
                        drawArrow(-0.35f * chordCoefficient, norlInRotUnit * -0.2f);
                        break;
                    case 1:
                    default:
                        drawArrow();
                        break;
                }
            }
        }

        public void LimitingAngle(int minrot = 50, int maxrot = 130) {
            float minRot = MathHelper.ToRadians(minrot);
            float maxRot = MathHelper.ToRadians(maxrot);
            Projectile.rotation = MathHelper.Clamp(ToMouseA + MathHelper.Pi, minRot, maxRot) - MathHelper.Pi;
            if (ToMouseA + MathHelper.Pi > MathHelper.ToRadians(270)) {
                Projectile.rotation = minRot - MathHelper.Pi;
            }
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HandFireDistance;
            ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation + 0.5f * DirSign)) * DirSign;
            SetCompositeArm();
        }
    }
}
