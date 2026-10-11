using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>各端独立处理固定路径位移和玩家动画；只有运镜属于本地玩家。</summary>
public sealed class SakuraDashPlayer : ModPlayer
{
    private int dashIndex = -1;
    private int dashIdentity = -1;
    private Vector2 dashDirection;
    private readonly List<DrawData> appearance = new();
    private int cameraFramesRemaining;
    private Vector2 cameraStart;
    private ulong lastCameraTick = ulong.MaxValue;

    private SakuraDashProj Dash
    {
        get
        {
            if (dashIndex < 0 || dashIndex >= Main.maxProjectiles || Player.dead || !Player.active) return null;
            Projectile p = Main.projectile[dashIndex];
            return p.active && p.owner == Player.whoAmI && p.identity == dashIdentity
                ? p.ModProjectile as SakuraDashProj : null;
        }
    }
    public bool IsDashing => Dash != null;
    internal SakuraDashProj ActiveDash => Dash;
    private bool Traveling => Dash is { } dash && dash.Projectile.ai[0] < SakuraDashMotion.TravelFrames;

    public bool Begin(SakuraDashProj dash)
    {
        if (Dash is { } current && current != dash) return false;
        if (dashIndex == dash.Projectile.whoAmI && dashIdentity == dash.Projectile.identity) return true;
        dashIndex = dash.Projectile.whoAmI;
        dashIdentity = dash.Projectile.identity;
        dashDirection = dash.Projectile.rotation.ToRotationVector2();
        Player.RemoveAllGrapplingHooks();
        if (Player.mount.Active) Player.mount.Dismount(Player);
        Player.velocity = Vector2.Zero;
        if (Player.whoAmI == Main.myPlayer)
        {
            cameraStart = Main.screenPosition;
            cameraFramesRemaining = SakuraDashMotion.CameraMoveFrames;
            lastCameraTick = Main.GameUpdateCount;
        }
        return true;
    }

    public Vector2 ResolveBlinkDestination(Vector2 start, Vector2 target) =>
        SakuraDashMotion.ResolveLanding(start, target, IsPathPointClear);

    private static bool IsPathPointClear(Vector2 point)
    {
        int x = (int)MathF.Floor(point.X / 16f);
        int y = (int)MathF.Floor(point.Y / 16f);
        if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return false;
        Tile tile = Main.tile[x, y];
        return !(tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]);
    }

    public void BlinkTo(Vector2 center)
    {
        // 所有端首帧自行移动一次，不发送位置包，也不持续覆盖原版同步的位置。
        Player.Center = center;
        Player.velocity = Vector2.Zero;
        Player.fallStart = Player.fallStart2 = (int)(Player.position.Y / 16f);
        if (MathF.Abs(dashDirection.X) > .01f) Player.ChangeDir(dashDirection.X > 0f ? 1 : -1);
    }

    public void End(SakuraDashProj dash)
    {
        if (dashIndex != dash.Projectile.whoAmI || dashIdentity != dash.Projectile.identity) return;
        dashIndex = dashIdentity = -1;
        appearance.Clear();
        // 弹幕结束后镜头仍完成自己的 40 帧计时，不提前截断或重启。
    }

    public override void SetControls()
    {
        if (!IsDashing) return;
        Player.controlUseItem = Player.controlUseTile = false;
        if (!Traveling) return;
        Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = false;
        Player.controlJump = Player.controlHook = Player.controlMount = false;
    }

    public override void PreUpdateMovement()
    {
        if (!Traveling) return;
        Player.velocity = Vector2.Zero;
        Player.gravity = 0f;
    }

    public override void PostUpdate()
    {
        Dash?.ApplyFinisherPose(Player);
        if (Traveling)
        {
            // 只压制动画期间的速度/下落，不把收到的原版位置修正拉回旧落点。
            Player.velocity = Vector2.Zero;
            Player.fallStart = Player.fallStart2 = (int)(Player.position.Y / 16f);
        }
        if (dashIndex >= 0 && Dash == null)
        {
            dashIndex = dashIdentity = -1;
            appearance.Clear();
        }
    }

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (Main.gameMenu || drawInfo.headOnlyRender || Dash is not { } dash) return;
        // 在原版 TransformDrawData 之前提供整套角色的旋转；前臂和武器沿用身体局部姿态。
        // 只修改本次绘制，不写 Player.fullRotation，动画结束自动恢复正常朝向。
        drawInfo.rotation = dash.PlayerDrawRotation;
        drawInfo.rotationOrigin = Player.Size * .5f;
    }

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        if (Dash != null) PlayerDrawLayers.HeldItem.Hide();
    }

    public override void TransformDrawData(ref PlayerDrawSet drawInfo)
    {
        if (Main.gameMenu || Dash is not { } dash) return;
        // 手持语义由 heldProj 保留，但武器已在专属玩家层中绘制。
        // 禁止原版在手中再次调用该弹幕 PreDraw，否则会把整段冲击/刀光重复画一遍。
        drawInfo.projectileDrawPosition = -1;
        float age = dash.Projectile.ai[0];
        if (age < SakuraDashMotion.TravelFrames)
        {
            // Hide the complete equipment/vanity draw, not just the skin; don't persist Player.invis.
            drawInfo.DrawDataCache.Clear();
            drawInfo.projectileDrawPosition = -1;
            return;
        }
        if (age >= SakuraDashMotion.TravelFrames + SakuraDashMotion.AfterimageFrames || drawInfo.shadow != 0f) return;
        appearance.Clear();
        appearance.AddRange(drawInfo.DrawDataCache);
        drawInfo.DrawDataCache.Clear();
        for (int echo = 2; echo >= 0; echo--)
        {
            float opacity = SakuraDashMotion.EchoOpacity(age, echo);
            float trail = SakuraDashMotion.EchoDistance(age, echo);
            foreach (DrawData original in appearance)
            {
                DrawData copy = original;
                copy.position -= dashDirection * trail;
                if (copy.useDestinationRectangle)
                {
                    copy.destinationRectangle.X -= (int)(dashDirection.X * trail);
                    copy.destinationRectangle.Y -= (int)(dashDirection.Y * trail);
                }
                copy.color = Color.Lerp(copy.color, new Color(255, 100, 145, copy.color.A), .45f) * opacity;
                drawInfo.DrawDataCache.Add(copy);
            }
        }
        int originalsStart = drawInfo.DrawDataCache.Count;
        foreach (DrawData original in appearance)
        {
            DrawData copy = original;
            copy.color *= SakuraDashMotion.Appearance(age);
            drawInfo.DrawDataCache.Add(copy);
        }
        if (drawInfo.projectileDrawPosition >= 0) drawInfo.projectileDrawPosition += originalsStart;
    }

    public override void ModifyScreenPosition()
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || cameraFramesRemaining <= 0 || Main.gameMenu) return;
        // 使用这一帧的正常镜头位置，而不是固定终点，玩家现身后移动也能自然跟随。
        Vector2 targetScreen = Main.screenPosition;
        if (lastCameraTick != Main.GameUpdateCount && !Main.gamePaused)
        {
            lastCameraTick = Main.GameUpdateCount;
            cameraFramesRemaining--;
        }
        Vector2 cameraPosition = SakuraDashMotion.Camera(cameraStart, targetScreen, cameraFramesRemaining);
        Main.screenPosition = new Vector2(
            MathHelper.Clamp(cameraPosition.X, 0f, Math.Max(0f, Main.maxTilesX * 16f - Main.screenWidth)),
            MathHelper.Clamp(cameraPosition.Y, 0f, Math.Max(0f, Main.maxTilesY * 16f - Main.screenHeight)));
    }

    public override void UpdateDead() => Clear();
    public override void PlayerDisconnect() => Clear();
    public override void OnEnterWorld() => Clear();
    private void Clear()
    {
        dashIndex = dashIdentity = -1;
        cameraFramesRemaining = 0;
        appearance.Clear();
    }
}
