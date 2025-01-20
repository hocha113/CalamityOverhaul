using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    /// <summary>
    /// 基础魔法书类
    /// </summary>
    internal abstract class BaseMagicBook<TItem> : BaseMagicGun where TItem : ModItem {
        public override string Texture => CWRConstant.Cay_Wap_Magic + typeof(TItem).Name;
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<TItem>();
        public override int targetCayItem => ModContent.ItemType<TItem>();
        public override int targetCWRItem => CWRServerConfig.Instance.WeaponOverhaul
            ? ItemID.None : CWRMod.Instance.Find<ModItem>(typeof(TItem).Name + "EcType").Type;
        private int useAnimation;
        public sealed override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandFireDistanceX = 20;
            HandFireDistanceY = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            Onehanded = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            SetBookProperty();
        }

        public override void Initialize() {
            useAnimation = Item.useAnimation;
        }

        public override void HanderPlaySound() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            useAnimation -= Item.useTime;
            if (useAnimation <= 0) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                useAnimation = Item.useAnimation;
            }
        }

        public virtual void SetBookProperty() {

        }

        public override void FiringShoot() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            OrigItemShoot();
        }

        public override void FiringShootR() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            Owner.altFunctionUse = 2;
            OrigItemShoot();
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            HandFireDistanceX = TextureValue.Width / 2;
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Vector2 orig = DirSign > 0 ? new Vector2(0, TextureValue.Height) : new Vector2(0, 0);
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }

    /// <summary>
    /// 基础法杖类
    /// </summary>
    internal abstract class BaseMagicStaff<TItem> : BaseMagicGun where TItem : ModItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + typeof(TItem).Name;
        public override int targetCayItem => ModContent.ItemType<TItem>();
        public override int targetCWRItem => CWRServerConfig.Instance.WeaponOverhaul
            ? ItemID.None : CWRMod.Instance.Find<ModItem>(typeof(TItem).Name + "EcType").Type;
        public sealed override void SetMagicProperty() {
            ShootPosToMouLengValue = -30;
            ShootPosNorlLengValue = 0;
            HandFireDistanceX = 0;
            HandFireDistanceY = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            Onehanded = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            SetStaffProperty();
        }

        public virtual void SetStaffProperty() {

        }

        public override void FiringShoot() {
            OrigItemShoot();
        }

        public override void FiringShootR() {
            OrigItemShoot();
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float rot = DirSign > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Vector2 orig = DirSign > 0 ? new Vector2(0, TextureValue.Height) : new Vector2(0, 0);
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + offsetRot + rot, orig, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
