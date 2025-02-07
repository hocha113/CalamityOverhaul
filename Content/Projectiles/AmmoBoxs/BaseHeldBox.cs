using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal abstract class BaseHeldBox : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item + "Placeable/NapalmBombBox";
        public CWRItems ModItem;
        public Vector2 DrawBoxOffsetPos;
        public int TargetItemID;
        /// <summary>
        /// 右手角度值，这个值被自动设置，不要手动给它赋值
        /// </summary>
        public float ArmRotSengsFront;
        /// <summary>
        /// 右手角度值，这个值被自动设置，不要手动给它赋值
        /// </summary>
        public float ArmRotSengsBack;
        /// <summary>
        /// 手持距离，生效于非使用状态下，默认为15
        /// </summary>
        public float HandDistance = 15;
        /// <summary>
        /// 手持距离，生效于非使用状态下，默认为0
        /// </summary>
        public float HandDistanceY = 0;
        /// <summary>
        /// 这个角度用于设置箱体在玩家非使用阶段的仰角，这个角度是周角而非弧度角，默认为20f
        /// </summary>
        public float AngleFirearmRest = 20f;
        public SoundStyle DeploymentSound = CWRSound.DeploymentSound;
        /// <summary>
        /// 是否启用手持开关
        /// </summary>
        public bool OnHandheldDisplayBool => CWRServerConfig.Instance.WeaponHandheldDisplay;
        private bool uiMouseInterface;
        public int Charge;
        public int MaxCharge = 600;
        public int AmmoBoxID;
        private int textlevelsengs;
        private int noCanUseTime;
        protected bool canUse_SetAmmoBox;
        protected Vector2 setAmmoBoxPos;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hide = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            SetBox();
        }

        public virtual void SetBox() {

        }

        public override bool PreUpdate() {
            if (Item.type > ItemID.None) {
                ModItem = Item.CWR();
            }
            else {
                Projectile.Kill();
                return false;
            }
            if (TargetItemID > 0) {
                if (TargetItemID != Item.type) {
                    Projectile.Kill();
                    return false;
                }
            }

            canUse_SetAmmoBox = CanUse();
            setAmmoBoxPos = new Vector2((int)(Main.MouseWorld.X / 16), (int)(Main.MouseWorld.Y / 16)) * 16 + new Vector2(0, 4);

            return true;
        }

        public override void AI() {
            InOwner();
            if (noCanUseTime > 0) {
                noCanUseTime--;
            }
        }

        private void SetHandBox() {
            ArmRotSengsFront = 60 * CWRUtils.atoR * SafeGravDir;
            ArmRotSengsBack = 110 * CWRUtils.atoR * SafeGravDir;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + new Vector2(Owner.direction * HandDistance, HandDistanceY).RotatedBy(Owner.fullRotation);
            float art = AngleFirearmRest;
            if (SafeGravDir < 0) {
                art = 360 - AngleFirearmRest;
            }
            float fullRotation = MathHelper.ToDegrees(Owner.fullRotation) * Owner.direction;
            float value = art + fullRotation;
            Projectile.rotation = Owner.direction > 0 ? MathHelper.ToRadians(value) : MathHelper.ToRadians(180 - value);
        }

        public virtual void InOwner() {
            uiMouseInterface = Owner.CWR().uiMouseInterface;
            Projectile.timeLeft = 2;
            SetHandBox();
            SetHeld();

            if (canUse_SetAmmoBox && DownLeft) {
                OnUse();
            }
            else {
                Charge = 0;
            }

            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
            }
        }

        public bool CanUse() {
            int x = (int)(setAmmoBoxPos.X / 16);
            int y = (int)(setAmmoBoxPos.Y / 16);

            if (CWRUtils.GetTile(x, y).HasSolidTile()) {
                return false;
            }
            if (CWRUtils.GetTile(x + 1, y).HasSolidTile()) {
                return false;
            }
            if (CWRUtils.GetTile(x - 1, y).HasSolidTile()) {
                return false;
            }

            if (ToMouse.Length() > 200) {
                return false;
            }

            return !Owner.CWR().uiMouseInterface && noCanUseTime <= 0;
        }

        public virtual void OnUse() {
            if (Owner.ownedProjectileCounts[Item.shoot] >= 25) {
                CombatText.NewText(Owner.Hitbox, Color.Gold, CWRLocText.GetTextValue("AmmoBox_Text"));
                noCanUseTime += 60;
                return;
            }

            if (Charge < MaxCharge) {
                ArmRotSengsBack += MathF.Sin(Main.GameUpdateCount * 0.3f) * 0.6f;
                Charge++;
            }
            else {
                if (AmmoBoxID > 0 && Projectile.IsOwnedByLocalPlayer()) {
                    ExtraGeneration();
                    Vector2 pos = new Vector2((int)(Main.MouseWorld.X / 16), (int)(Main.MouseWorld.Y / 16)) * 16;
                    int proj = Projectile.NewProjectile(Item.GetSource_FromThis(), pos, Vector2.Zero, AmmoBoxID, 0, 0, Owner.whoAmI);
                    BaseAmmoBox baseAmmoBox = Main.projectile[proj].ModProjectile as BaseAmmoBox;
                    if (baseAmmoBox != null) {
                        baseAmmoBox.FromeThisTImeID = TargetItemID;
                    }
                    Projectile proj2 = Projectile.NewProjectileDirect(Item.GetSource_FromThis(), Owner.Center, Vector2.Zero, ModContent.ProjectileType<SuccessfullyDeployedEffct>(), 0, 0, Owner.whoAmI);
                    SuccessfullyDeployedEffct successfullyDeployedEffct = proj2.ModProjectile as SuccessfullyDeployedEffct;
                    if (successfullyDeployedEffct != null) {
                        successfullyDeployedEffct.text = CWRUtils.SafeGetItemName(TargetItemID) + CWRLocText.GetTextValue("AmmoBox_Text3");
                        successfullyDeployedEffct.textColor = Color.OrangeRed;
                    }
                }
                if (--Item.stack <= 0) {
                    Item.TurnToAir();
                }
                Projectile.Kill();
            }
        }

        public virtual void ExtraGeneration() {

        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (OnHandheldDisplayBool) {
                BoxDraw(ref lightColor);
            }
            return false;
        }

        public virtual void BoxDraw(ref Color lightColor) {
            Vector2 drawBoxPos = Projectile.Center - Main.screenPosition;
            float rotation = Projectile.rotation;
            Color drawColor = lightColor;
            SpriteEffects spriteEffects = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            if (Charge <= 0) {
                Main.EntitySpriteDraw(TextureValue, drawBoxPos + DrawBoxOffsetPos + Owner.CWR().SpecialDrawPositionOffset
                    , null, drawColor, rotation, TextureValue.Size() / 2, Projectile.scale, spriteEffects);
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                if (!uiMouseInterface) {
                    rotation = 0;
                    drawColor = canUse_SetAmmoBox ? lightColor : Color.Red;
                    spriteEffects = SpriteEffects.None;
                    drawBoxPos = setAmmoBoxPos - Main.screenPosition;
                    Main.EntitySpriteDraw(TextureValue, drawBoxPos + DrawBoxOffsetPos, null, drawColor * 0.6f
                            , rotation, TextureValue.Size() / 2, Projectile.scale, spriteEffects);
                }

                if (!(Charge <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                    Texture2D barBG = CWRAsset.GenericBarBack.Value;
                    Texture2D barFG = CWRAsset.GenericBarFront.Value;
                    float barScale = 2f;
                    Vector2 barOrigin = barBG.Size() * 0.5f;
                    Vector2 drawPos = Owner.GetPlayerStabilityCenter() + Vector2.UnitY * 250 - Main.screenPosition;
                    Rectangle frameCrop = new Rectangle(0, 0, (int)(Charge / (float)MaxCharge * barFG.Width), barFG.Height);
                    Color color = Color.White;
                    Main.spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, barScale, 0, 0f);
                    Main.spriteBatch.Draw(barFG, drawPos, frameCrop, Color.Green * 0.8f, 0f, barOrigin, barScale, 0, 0f);
                    Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.ItemStack.Value, CWRLocText.GetTextValue("AmmoBox_Text2")
                        , drawPos.X - 32, drawPos.Y - 30, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
                    if (Charge % 10 == 0) {
                        textlevelsengs++;
                        if (textlevelsengs > 10) {
                            textlevelsengs = 0;
                        }
                    }
                    string text = "";
                    for (int i = 0; i < textlevelsengs; i++) {
                        text += ".";
                    }
                    Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.ItemStack.Value, text
                        , drawPos.X - 20, drawPos.Y, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
                }
            }
        }
    }
}
