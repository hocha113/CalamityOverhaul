
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraReadyStanceWeaponLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.ArmOverItem);

    public override Transformation Transform => PlayerDrawLayers.TorsoGroup;

    internal static SakuraReadyStanceProj GetHeldStance(Player player)
    {
        int index = player.heldProj;
        if (!player.active || player.dead || index < 0 || index >= Main.maxProjectiles)
            return null;

        Projectile projectile = Main.projectile[index];
        return projectile.active && projectile.owner == player.whoAmI
            ? projectile.ModProjectile as SakuraReadyStanceProj : null;
    }

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => !drawInfo.headOnlyRender
        && drawInfo.shadow == 0f && GetHeldStance(drawInfo.drawPlayer) != null;

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        Player player = drawInfo.drawPlayer;
        SakuraReadyStanceProj stance = GetHeldStance(player);
        if (stance == null) return;

        Texture2D texture = ModContent.GetInstance<SakuraFinaleAssets>().Weapon;
        int facing = player.direction < 0 ? -1 : 1;
        int gravity = player.gravDir < 0f ? -1 : 1;
        SpriteEffects effects = (facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
            | (gravity < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None);
        Vector2 localOffset = stance.WeaponDrawOffset;
        Vector2 position = new Vector2(MathF.Floor(player.MountedCenter.X), MathF.Floor(player.MountedCenter.Y))
            + new Vector2(0f, MathF.Floor(player.gfxOffY))
            + new Vector2(localOffset.X * facing, localOffset.Y * gravity)
            + (drawInfo.Position - player.position) - Main.screenPosition;
        float rotation = SakuraReadyStanceWeaponVisual.WeaponRotation * facing * gravity;
        float glow = stance.GlowStrength;

        if (glow > 0f)
        {
            Texture2D mask = stance.GetWeaponGlowMask(texture);
            drawInfo.DrawDataCache.Add(new DrawData(mask, position, null,
                SakuraReadyStanceWeaponVisual.GlowColor(glow), rotation,
                new Vector2(mask.Width * .5f, mask.Height * .5f), 1f, effects, 0));
        }

        drawInfo.DrawDataCache.Add(new DrawData(texture, position, null, Color.White,
            rotation, new Vector2(texture.Width * .5f, texture.Height * .5f), 1f, effects, 0));
    }
}
