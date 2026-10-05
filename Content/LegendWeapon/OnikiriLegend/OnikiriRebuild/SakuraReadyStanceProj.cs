using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed class SakuraReadyStanceProj : ModProjectile
{
    public override string Texture => SakuraFinaleAssets.TexturePath + "OnikiriItem";
    private int time;
    private int sourceItemType;
    private Player Owner => Main.player[Projectile.owner];

    internal Vector2 WeaponDrawOffset => SakuraReadyStanceWeaponVisual.WeaponOffset
        + SakuraReadyStanceWeaponVisual.GetLungeOffset(time);
    internal float GlowStrength => SakuraReadyStanceWeaponVisual.GetGlowStrength(time);
    internal Texture2D GetWeaponGlowMask(Texture2D texture) =>
        ModContent.GetInstance<SakuraFinaleAssets>().GlowMask();

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.tileCollide = false;
        Projectile.timeLeft = 60;
        Projectile.hide = true;
    }

    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers || !Owner.active || Owner.dead || Owner.CCed
            || (time > 0 && Owner.HeldItem.type != sourceItemType))
        {
            Projectile.Kill();
            return;
        }
        if (time == 0) sourceItemType = Owner.HeldItem.type;
        Projectile.Center = Owner.MountedCenter;
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemAnimation = Owner.itemTime = 5;
        if (++time != SakuraReadyStanceWeaponVisual.BurstFrame) return;
        if (Owner.whoAmI == Main.myPlayer)
            SakuraRangedSlashProj.Spawn(Projectile.GetSource_FromThis(), Main.MouseWorld, Vector2.Zero,
                0, 0f, Owner.whoAmI);
        Vector2 pivot = Owner.position + Owner.fullRotationOrigin;
        Vector2 starCenter = pivot + Vector2.Transform(Projectile.Center - pivot, Matrix.CreateRotationZ(Owner.fullRotation));
        SakuraFinaleFeedback.ReadyBurst(Projectile.Center, starCenter, Owner);
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => overPlayers.Add(index);

    public override bool PreDraw(ref Color lightColor)
    {
        if (Main.dedServ || time <= SakuraReadyStanceWeaponVisual.BurstFrame) return false;
        Player player = Owner;
        Vector2 pivot = player.position + player.fullRotationOrigin;
        Vector2 starCenter = pivot + Vector2.Transform(Projectile.Center - pivot, Matrix.CreateRotationZ(player.fullRotation));
        int age = time - SakuraReadyStanceWeaponVisual.BurstFrame;
        if (age <= 20)
            ModContent.GetInstance<SakuraFinaleAssets>().DrawReadyStar(starCenter, Projectile.rotation,
                age, player.direction < 0 ? -1 : 1, player.gravDir < 0 ? -1 : 1, player.fullRotation);
        return false;
    }

    public override void OnKill(int timeLeft)
    {
        if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers
            && Owner.heldProj == Projectile.whoAmI) Owner.heldProj = -1;
    }
}
