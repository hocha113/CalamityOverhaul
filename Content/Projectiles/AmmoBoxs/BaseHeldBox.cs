using CalamityMod;
using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal abstract class BaseHeldBox : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item + "Placeable/NapalmBomHeld";
        /// <summary>
        /// 该枪体使用的实际纹理
        /// </summary>
        public virtual Texture2D TextureValue => CWRUtils.GetT2DValue(Texture);
        public Item Item;
        public CWRItems ModItem;
        public Player Player;
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
        /// 右手角度值矫正
        /// </summary>
        public float ArmRotSengsFrontNoFireOffset;
        /// <summary>
        /// 左手角度值矫正
        /// </summary>
        public float ArmRotSengsBackNoFireOffset;
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
        /// 这个角度用于设置枪体在玩家非开火阶段的仰角，这个角度是周角而非弧度角，默认为20f
        /// </summary>
        public float AngleFirearmRest = 20f;
        public SoundStyle DeploymentSound = CWRSound.DeploymentSound;
        /// <summary>
        /// 是否启用手持开关
        /// </summary>
        public bool OnHandheldDisplayBool => CWRServerConfig.Instance.WeaponHandheldDisplay;

        public int Charge;
        public int MaxCharge = 600;
        public int AmmoBoxID;
        int textlevelsengs;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hide = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            SetBox();
        }

        public virtual void SetBox() {

        }

        public override bool PreAI() {
            Player = Main.player[Projectile.owner];
            Item = Player.ActiveItem();
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
            return true;
        }

        public override void AI() {
            InOwner();
        }

        public virtual void InOwner() {
            ArmRotSengsFront = (60 + ArmRotSengsFrontNoFireOffset) * CWRUtils.atoR;
            ArmRotSengsBack = (110 + ArmRotSengsBackNoFireOffset) * CWRUtils.atoR;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + new Vector2(DirSign * HandDistance, HandDistanceY);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(AngleFirearmRest) : MathHelper.ToRadians(180 - AngleFirearmRest);
            Projectile.timeLeft = 2;
            SetHeld();
            if (!Owner.mouseInterface && Owner.PressKey()) {
                OnUse();
            }
            if (!Owner.PressKey() || Owner.velocity.Length() > 0) {
                Charge = 0;
            }
            if (OnHandheldDisplayBool) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsFront * -DirSign);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, ArmRotSengsBack * -DirSign);
            }
        }

        public virtual void OnUse() {
            if (Charge < MaxCharge) {
                Charge++;
            } else {
                if (Owner.ownedProjectileCounts[Item.shoot] >= 5) {
                    CombatText.NewText(Owner.Hitbox, Color.Gold, CWRLocText.GetTextValue("AmmoBox_Text"));
                    return;
                }
                Vector2 pos = new Vector2((int)(Main.MouseWorld.X / 16), (int)(Main.MouseWorld.Y / 16)) * 16;
                if (AmmoBoxID > 0) {
                    SoundEngine.PlaySound(DeploymentSound, Projectile.Center);
                    Projectile.NewProjectile(Item.GetSource_FromThis(), pos, Vector2.Zero, AmmoBoxID, 0, 0, Owner.whoAmI);
                }
                Item.TurnToAir();
                Projectile.Kill();
            }
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (OnHandheldDisplayBool) {
                BoxDraw(ref lightColor);
            }
            return false;
        }

        public virtual void BoxDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation, TextureValue.Size() / 2, Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            if (!(Charge <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                Texture2D barBG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarBack", (AssetRequestMode)2).Value;
                Texture2D barFG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarFront", (AssetRequestMode)2).Value;
                float barScale = 2f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + Vector2.UnitY * 250 - Main.screenPosition;
                Rectangle frameCrop = new Rectangle(0, 0, (int)(Charge / (float)MaxCharge * barFG.Width), barFG.Height);
                Color color = Color.White;
                Main.spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, barScale, 0, 0f);
                Main.spriteBatch.Draw(barFG, drawPos, frameCrop, Color.Green * 0.8f, 0f, barOrigin, barScale, 0, 0f);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.ItemStack.Value, CWRLocText.GetTextValue("AmmoBox_Text2")
                    , drawPos.X - 32, drawPos.Y - 30, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
                if (Main.GameUpdateCount % 20 == 0) {
                    textlevelsengs++;
                    if (textlevelsengs > 4) {
                        textlevelsengs = 0;
                    }
                }
                string text = "";
                for(int i = 0; i <  textlevelsengs; i++) {
                    text += ".";
                }
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.ItemStack.Value, text
                    , drawPos.X + 32, drawPos.Y - 30, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
            }
        }
    }
}
