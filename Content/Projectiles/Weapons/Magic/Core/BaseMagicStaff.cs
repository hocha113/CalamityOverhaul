using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core
{
    internal abstract class BaseMagicStaff<TItem> : BaseMagicActionStaff where TItem : ModItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + typeof(TItem).Name;
        public override int targetCayItem => ModContent.ItemType<TItem>();
        public override int targetCWRItem => CWRServerConfig.Instance.WeaponOverhaul
            ? ItemID.None : CWRMod.Instance.Find<ModItem>(typeof(TItem).Name + "EcType").Type;
    }

    internal abstract class BaseMagicStaff : BaseMagicActionStaff
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TextureAssets.Item[targetCayItem].Value;
        public override LocalizedText DisplayName => Language.GetOrRegister("Mods.CalamityOverhaul.Projectiles.MagicStaff_" + targetCayItem);
        public override int targetCayItem => ItemID.None;
        public override int targetCWRItem => targetCayItem;
    }

    internal abstract class BaseMagicActionStaff : BaseMagicAction
    {
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
