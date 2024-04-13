using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    /// <summary>
    /// 一个改进版的枪基类，这个基类的基础实现会更加快捷和易于模板化
    /// </summary>
    internal abstract class BaseGun : BaseHeldRanged
    {
        #region Date
        private bool old_downLeftValue;
        private bool downLeftValue;
        protected bool DownLeft {
            get {
                downLeftValue = Owner.PressKey();
                if (old_downLeftValue != downLeftValue) {
                    Projectile.netUpdate = true;
                }
                old_downLeftValue = downLeftValue;
                return downLeftValue;
            }
        }
        /// <summary>
        /// 每次发射事件是否运行全局物品行为，默认为<see cref="true"/>
        /// </summary>
        public bool GlobalItemBehavior = true;
        /// <summary>
        /// 枪械旋转角矫正
        /// </summary>
        public float OffsetRot;
        /// <summary>
        /// 枪械位置矫正
        /// </summary>
        public Vector2 OffsetPos;
        /// <summary>
        /// 右手角度值，这个值被自动设置，不要手动给它赋值
        /// </summary>
        public float ArmRotSengsFront;
        /// <summary>
        /// 右手角度值，这个值被自动设置，不要手动给它赋值
        /// </summary>
        public float ArmRotSengsBack;
        /// <summary>
        /// 右手角度值矫正
        /// </summary>
        public float ArmRotSengsFrontNoFireOffset;
        /// <summary>
        /// 左手角度值矫正
        /// </summary>
        public float ArmRotSengsBackNoFireOffset;
        /// <summary>
        /// 是否可以右键，默认为<see langword="false"/>
        /// </summary>
        public bool CanRightClick;
        /// <summary>
        /// 是否正在右键开火
        /// </summary>
        protected bool onFireR;
        /// <summary>
        /// 是否在<see cref="InOwner"/>执行后自动更新手臂参数，默认为<see langword="true"/>
        /// </summary>
        public bool SetArmRotBool = true;
        /// <summary>
        /// 枪械是否受到应力缩放，默认为<see langword="true"/>
        /// </summary>
        public bool PressureWhetherIncrease = true;
        /// <summary>
        /// 开火时是否默认播放手持物品的使用音效<see cref="Item.UseSound"/>，但如果准备重写<see cref="SpanProj"/>，这个属性将失去作用，默认为<see langword="true"/>
        /// </summary>
        public bool FiringDefaultSound = true;
        /// <summary>
        /// 枪械开火冷切计时器
        /// </summary>
        public float GunShootCoolingValue {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }
        /// <summary>
        /// 这个角度用于设置枪体在玩家非开火阶段的仰角，这个角度是周角而非弧度角，默认为20f
        /// </summary>
        public float AngleFirearmRest = 20f;
        /// <summary>
        /// 枪压，决定开火时的上抬力度，默认为0
        /// </summary>
        public float GunPressure = 0;
        /// <summary>
        /// 控制力度，决定压枪的力度，默认为0.01f
        /// </summary>
        public float ControlForce = 0.01f;
        /// <summary>
        /// 手持距离，生效于非开火状态下，默认为15
        /// </summary>
        public float HandDistance = 15;
        /// <summary>
        /// 手持距离，生效于非开火状态下，默认为0
        /// </summary>
        public float HandDistanceY = 0;
        /// <summary>
        /// 手持距离，生效于开火状态下，默认为20
        /// </summary>
        public float HandFireDistance = 20;
        /// <summary>
        /// 手持距离，生效于开火状态下，默认为-3
        /// </summary>
        public float HandFireDistanceY = -3;
        /// <summary>
        /// 应力范围，默认为10
        /// </summary>
        public float RangeOfStress = 10;
        /// <summary>
        /// 开火时会制造的后坐力模长，默认为5
        /// </summary>
        public float Recoil = 5;
        /// <summary>
        /// 止推模长恢复系数，值越接近1恢复的越加缓慢，默认为0.5f
        /// </summary>
        protected float RecoilOffsetRecoverValue = 0.5f;
        /// <summary>
        /// 该枪械在开火时的一个转动角，用于快捷获取
        /// </summary>
        public virtual float GunOnFireRot => ToMouseA - OffsetRot * DirSign;
        /// <summary>
        /// 发射口的长度矫正值，默认为0
        /// </summary>
        public float ShootPosToMouLengValue = 0;
        /// <summary>
        /// 发射口的竖直方向长度矫正值，默认为0
        /// </summary>
        public float ShootPosNorlLengValue = 0;
        /// <summary>
        /// 光照强度，默认为1，用于控制在开火时制造光火效果的强度，为0时表示关闭
        /// </summary>
        public float fireLight = 1;
        /// <summary>
        /// 快捷获取该枪械的发射口位置
        /// </summary>
        public Vector2 GunShootPos => GetShootPos(ShootPosToMouLengValue, ShootPosNorlLengValue);
        /// <summary>
        /// 玩家是否正在行走
        /// </summary>
        public virtual bool WalkDetection => Owner.velocity.Y == 0 && Math.Abs(Owner.velocity.X) > 0;
        /// <summary>
        /// 应力缩放系数
        /// </summary>
        public float OwnerPressureIncrease => PressureWhetherIncrease ? ModOwner.PressureIncrease : 1;
        /// <summary>
        /// 快速的获取该枪械是否正在进行开火尝试，包括左键或者右键的情况
        /// </summary>
        public bool CanFire => DownLeft && !(Owner.Calamity().mouseRight && !onFire && CanRightClick);
        /// <summary>
        /// 是否允许手持状态，如果玩家关闭了手持动画设置，这个值将在非开火状态时返回<see langword="false"/>
        /// </summary>
        public virtual bool OnHandheldDisplayBool {
            get {
                if (WeaponHandheldDisplay) {
                    return true;
                }
                return CanFire;
            }
        }
        /// <summary>
        /// 获取来自物品的生成源，该生成源实例会附加CWRGun标签，用于特殊识别
        /// </summary>
        internal EntitySource_ItemUse_WithAmmo Source => new EntitySource_ItemUse_WithAmmo(Owner, Item, Owner.GetShootState().UseAmmoItemType, "CWRGunShoot");
        /// <summary>
        /// 获取来自物品的生成源，该生成源仅仅用于派生于物品关系，如果不想发射的弹幕被识别为枪械类射弹并受到特殊加成，使用这个
        /// </summary>
        internal EntitySource_ItemUse_WithAmmo Source2 => new EntitySource_ItemUse_WithAmmo(Owner, Item, Owner.GetShootState().UseAmmoItemType);
        /// <summary>
        /// 该枪体使用的实际纹理
        /// </summary>
        public virtual Texture2D TextureValue => CWRUtils.GetT2DValue(Texture);
        #endregion

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(downLeftValue);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            downLeftValue = reader.ReadBoolean();
        }

        /// <summary>
        /// 更新枪压的作用状态
        /// </summary>
        public void UpdateRecoil() {
            OffsetRot -= ControlForce;
            if (OffsetRot <= 0) {
                OffsetRot = 0;
            }
            if (OffsetPos != Vector2.Zero) {
                OffsetPos *= RecoilOffsetRecoverValue;
            }
        }
        /// <summary>
        /// 制造后坐力，这个函数只应该由弹幕主人调用，它不会自动调用，需要重写时在合适的代码片段中调用这个函数
        /// ，以确保制造后坐力的时机正确，一般在<see cref="BaseHeldRanged.SpanProj"/>中调用
        /// </summary>
        /// <returns>返回制造出的后坐力向量</returns>
        public virtual Vector2 CreateRecoil() {
            OffsetRot += GunPressure * OwnerPressureIncrease;
            Vector2 recoilVr = ShootVelocity.UnitVector() * (Recoil * -OwnerPressureIncrease);
            if (Math.Abs(Owner.velocity.X) < RangeOfStress && Math.Abs(Owner.velocity.Y) < RangeOfStress) {
                Owner.velocity += recoilVr;
                if (!CWRUtils.isSinglePlayer) {//&& netUpdateCooldingTime <= 0
                    //netUpdateCooldingTime += netUpdateCoold;
                    var msg = Mod.GetPacket();
                    msg.Write((byte)CWRMessageType.RecoilAcceleration);
                    msg.Write(Owner.whoAmI);
                    msg.Write(true);
                    msg.Write(recoilVr.X);
                    msg.Write(recoilVr.Y);
                    msg.Send();
                }
            }
            return recoilVr;
        }
        /// <summary>
        /// 在枪械的更新周期中的最后被调用，用于复原一些数据
        /// </summary>
        public virtual void Recover() {
        }
        /// <summary>
        /// 一个快捷创建手持事件的方法，在<see cref="InOwner"/>中被调用，值得注意的是，如果需要更强的自定义效果，一般是需要直接重写<see cref="InOwner"/>的
        /// </summary>
        public virtual void FiringIncident() {
            if (DownLeft) {
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = GunOnFireRot;
                Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY) + OffsetPos;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
                if (HaveAmmo && Projectile.IsOwnedByLocalPlayer()) {
                    onFire = true;
                    //Projectile.ai[1]++;
                }
            }
            else {
                onFire = false;
            }

            if (Owner.Calamity().mouseRight && !onFire && CanRightClick) {//Owner.PressKey()
                Owner.direction = ToMouse.X > 0 ? 1 : -1;
                Projectile.rotation = GunOnFireRot;
                Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.rotation.ToRotationVector2() * HandFireDistance + new Vector2(0, HandFireDistanceY) + OffsetPos;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
                if (HaveAmmo && Projectile.IsOwnedByLocalPlayer()) {
                    onFireR = true;
                    //Projectile.ai[1]++;
                }
            }
            else {
                onFireR = false;
            }
        }

        public override void InOwner() {
            ArmRotSengsFront = (60 + ArmRotSengsFrontNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            ArmRotSengsBack = (110 + ArmRotSengsBackNoFireOffset) * CWRUtils.atoR * SafeGravDir;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + new Vector2(DirSign * HandDistance, HandDistanceY * SafeGravDir) * SafeGravDir;
            Projectile.rotation = Owner.direction > 0 ? MathHelper.ToRadians(AngleFirearmRest) : MathHelper.ToRadians(180 - AngleFirearmRest);
            Projectile.timeLeft = 2;
            if (GunShootCoolingValue > 0) {
                GunShootCoolingValue--;
            }
            SetHeld();
            if (!Owner.mouseInterface) {
                FiringIncident();
            }
        }
        /// <summary>
        /// 一个快捷创建发射事件的方法，在<see cref="SpanProj"/>中被调用，<see cref="BaseHeldRanged.onFire"/>为<see cref="true"/>才可能调用。
        /// 值得注意的是，如果需要更强的自定义效果，一般是需要直接重写<see cref="SpanProj"/>的
        /// </summary>
        public virtual void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
        /// <summary>
        /// 一个快捷创建发射事件的方法，在<see cref="SpanProj"/>中被调用，<see cref="onFireR"/>为<see cref="true"/>才可能调用。
        /// 值得注意的是，如果需要更强的自定义效果，一般是需要直接重写<see cref="SpanProj"/>的
        /// </summary>
        public virtual void FiringShootR() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
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
        /// <summary>
        /// 快速创建一个卢克索发射事件的方法，默认在<see cref="LuxirEvent"/>中调用
        /// </summary>
        /// <param name="luxirDamage"></param>
        /// <returns></returns>
        public virtual int SpanLuxirProj(int luxirDamage) {
            return 0;
        }
        /// <summary>
        /// 获取枪口位置，一般用于发射口的矫正
        /// </summary>
        /// <param name="toMouLeng"></param>
        /// <param name="norlLeng"></param>
        /// <returns></returns>
        public virtual Vector2 GetShootPos(float toMouLeng, float norlLeng) {
            Vector2 norlVr = (Projectile.rotation + (DirSign > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2)).ToRotationVector2();
            return Projectile.Center + Projectile.rotation.ToRotationVector2() * toMouLeng + norlVr * norlLeng;
        }
        /// <summary>
        /// 一个快捷的抛壳方法，需要自行调用
        /// </summary>
        /// <param name="slp"></param>
        public virtual void CaseEjection(float slp = 1) {
            if (CWRMod.Instance.terrariaOverhaul != null && slp == 1) {
                return;
            }
            Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign).ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
            int proj = Projectile.NewProjectile(Source2, Projectile.Center, vr, ModContent.ProjectileType<GunCasing>(), 10, Projectile.knockBack, Owner.whoAmI);
            Main.projectile[proj].scale = slp;
        }
        /// <summary>
        /// 一个快捷的创造开火烟尘粒子效果的方法
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="velocity"></param>
        /// <param name="splNum"></param>
        /// <param name="dustID1"></param>
        /// <param name="dustID2"></param>
        /// <param name="dustID3"></param>
        public virtual void SpawnGunFireDust(Vector2 pos = default, Vector2 velocity = default, int splNum = 1, int dustID1 = 262, int dustID2 = 54, int dustID3 = 53) {
            if (Main.myPlayer != Projectile.owner) return;
            if (pos == default) {
                pos = GunShootPos;
            }
            if (velocity == default) {
                velocity = ShootVelocity;
            }
            pos += velocity.SafeNormalize(Vector2.Zero) * Projectile.width * Projectile.scale * 0.71f;
            for (int i = 0; i < 30 * splNum; i++) {
                int dustID;
                switch (Main.rand.Next(6)) {
                    case 0:
                        dustID = dustID1;
                        break;
                    case 1:
                    case 2:
                        dustID = dustID2;
                        break;
                    default:
                        dustID = dustID3;
                        break;
                }
                float num = Main.rand.NextFloat(3f, 13f) * splNum;
                float angleRandom = 0.06f;
                Vector2 dustVel = new Vector2(num, 0f).RotatedBy((double)velocity.ToRotation(), default);
                dustVel = dustVel.RotatedBy(0f - angleRandom);
                dustVel = dustVel.RotatedByRandom(2f * angleRandom);
                float scale = Main.rand.NextFloat(0.5f, 1.5f);
                int idx = Dust.NewDust(pos, 1, 1, dustID, dustVel.X, dustVel.Y, 0, default, scale);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].position = pos;
            }
        }

        public override void SpanProj() {
            if (GunShootCoolingValue <= 0 && (onFire || onFireR)) {
                if (FiringDefaultSound) {
                    SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                }
                if (onFire) {
                    FiringShoot();
                }
                if (onFireR) {
                    FiringShootR();
                }
                if (Owner.Calamity().luxorsGift || ModOwner.TheRelicLuxor > 0) {
                    LuxirEvent();
                }
                if (GlobalItemBehavior) {
                    ItemLoaderInFireSetBaver();
                }
                if (fireLight > 0) {
                    Lighting.AddLight(GunShootPos, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(0.3f, 0.65f), Color.Red, Color.Gold).ToVector3() * fireLight);
                }

                GunShootCoolingValue += Item.useTime;
                onFire = false;
            }
        }

        internal void ItemLoaderInFireSetBaver() {
            foreach (var g in CWRMod.CWR_InItemLoader_Set_CanUse_Hook.Enumerate(Item)) {
                g.CanUseItem(Item, Owner);
            }
            foreach (var g in CWRMod.CWR_InItemLoader_Set_UseItem_Hook.Enumerate(Item)) {
                g.UseItem(Item, Owner);
            }
            foreach (var g in CWRMod.CWR_InItemLoader_Set_Shoot_Hook.Enumerate(Item)) {
                g.Shoot(Item, Owner, Source2, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback);
            }
        }

        public override bool PreAI() {
            bool reset = base.PreAI();
            if (ModOwner == null) {
                ModOwner = Owner.CWR();
            }
            ModOwner.HeldGunBool = true;
            return reset;
        }

        public override void AI() {
            InOwner();
            if (SetArmRotBool) {
                SetCompositeArm();
            }
            UpdateRecoil();
            if (Projectile.IsOwnedByLocalPlayer()) {
                SpanProj();
            }
            Time++;
            Recover();
        }

        public void SetCompositeArm() {
            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (OnHandheldDisplayBool) {
                GunDraw(ref lightColor);
            }
            return false;
        }

        public virtual void GunDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, onFire ? Color.White : lightColor
                , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }

        public string GetLckRecoilKey() {
            float recoilValue = Math.Abs(Recoil);
            string recoilKey;
            if (recoilValue == 0) {
                return "CWRGun_Recoil_Level_0";
            }
            if (recoilValue < 0.2f) {
                recoilKey = "CWRGun_Recoil_Level_1";
            } else if (recoilValue < 0.5f) {
                recoilKey = "CWRGun_Recoil_Level_2";
            } else if (recoilValue < 1f) {
                recoilKey = "CWRGun_Recoil_Level_3";
            } else if (recoilValue < 1.5f) {
                recoilKey = "CWRGun_Recoil_Level_4";
            } else if (recoilValue < 2.2f) {
                recoilKey = "CWRGun_Recoil_Level_5";
            } else {
                recoilKey = "CWRGun_Recoil_Level_6";
            }
            return recoilKey;
        }
    }
}
