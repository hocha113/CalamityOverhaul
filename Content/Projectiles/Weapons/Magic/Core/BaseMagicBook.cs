using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core
{
    internal abstract class BaseMagicBook<TItem> : BaseMagicActionBook where TItem : ModItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + typeof(TItem).Name;
        public override int TargetID => ModContent.ItemType<TItem>();
    }

    internal abstract class BaseMagicBook : BaseMagicActionBook
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TextureAssets.Item[TargetID].Value;
        public override int TargetID => ItemID.None;
    }

    internal abstract class BaseMagicActionBook : BaseMagicAction
    {
        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Vector2 orig = DirSign > 0 ? new Vector2(0, TextureValue.Height / 2) : new Vector2(0, 0);
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + offsetRot, new Vector2(0, TextureValue.Height / 2), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
