using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>为 SakuraKiriPlayerProj 接管玩家输入、移动、受伤和绘制残影。</summary>
public sealed class SakuraKiriPlayer : ModPlayer
{
    private int actionIndex = -1;
    private int actionIdentity = -1;
    private readonly List<DrawData> appearance = new();

    private SakuraKiriPlayerProj Action
    {
        get
        {
            if (actionIndex < 0 || actionIndex >= Main.maxProjectiles || !Player.active || Player.dead)
                return null;
            Projectile projectile = Main.projectile[actionIndex];
            return projectile.active && projectile.owner == Player.whoAmI
                && projectile.identity == actionIdentity
                ? projectile.ModProjectile as SakuraKiriPlayerProj : null;
        }
    }

    public bool IsActing => Action != null;
    internal SakuraKiriPlayerProj ActiveAction => Action;

    public void Begin(SakuraKiriPlayerProj action)
    {
        actionIndex = action.Projectile.whoAmI;
        actionIdentity = action.Projectile.identity;
        Player.RemoveAllGrapplingHooks();
        if (Player.mount.Active) Player.mount.Dismount(Player);
        Player.velocity = Vector2.Zero;
    }

    public void End(SakuraKiriPlayerProj action)
    {
        if (actionIndex != action.Projectile.whoAmI || actionIdentity != action.Projectile.identity) return;
        actionIndex = actionIdentity = -1;
        appearance.Clear();
    }

    public void ApplyAnimationPose(SakuraKiriPlayerProj action)
    {
        if (Action != action || !Player.active || Player.dead) return;
        Player.ChangeDir(action.Facing);
        Player.itemTime = Player.itemAnimation = 5;
        Player.velocity = Vector2.Zero;
        Player.gravity = 0f;
        Player.fallStart = Player.fallStart2 = (int)(Player.position.Y / 16f);

        if (action.DrawState is SakuraKiriDrawState.Charge or SakuraKiriDrawState.Disappear
            or SakuraKiriDrawState.Reappear or SakuraKiriDrawState.Sheathe)
        {
            Player.heldProj = action.Projectile.whoAmI;
            if (action.IsCenterSheathing)
            {
                int gravity = Player.gravDir < 0f ? -1 : 1;
                float rotation = SakuraKiriSheatheAnimation.ArmRotation(action.CenterSheatheFrame, action.Facing, gravity);
                // 测试动画只驱动前臂和 itemRotation，不继承第一段的双臂后伸。
                Player.SetCompositeArmBack(false, Player.CompositeArmStretchAmount.Full, 0f);
                Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
                Player.itemRotation = rotation * action.Facing;
            }
            else if (action.IsFinishingPose)
            {
                float rotation = action.FinisherArmRotation * (Player.gravDir < 0f ? -1 : 1);
                Player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rotation);
                Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
            }
            else
            {
                Player.SetCompositeArmFront(false, Player.CompositeArmStretchAmount.Full, 0f);
            }
        }
        else if (Player.heldProj == action.Projectile.whoAmI)
        {
            Player.heldProj = -1;
        }
    }

    public override void SetControls()
    {
        if (!IsActing) return;
        Player.controlUseItem = Player.controlUseTile = false;
        Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = false;
        Player.controlJump = Player.controlHook = Player.controlMount = false;
    }

    public override void PreUpdateMovement()
    {
        if (!IsActing) return;
        Player.velocity = Vector2.Zero;
        Player.gravity = 0f;
    }

    public override void PostUpdate()
    {
        if (!IsActing) return;
        Player.velocity = Vector2.Zero;
        Player.gravity = 0f;
        Player.fallStart = Player.fallStart2 = (int)(Player.position.Y / 16f);
        if (actionIndex >= 0 && Action == null)
        {
            actionIndex = actionIdentity = -1;
            appearance.Clear();
        }
    }

    public override bool FreeDodge(Player.HurtInfo info) => IsActing || base.FreeDodge(info);

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (Main.gameMenu || drawInfo.headOnlyRender || Action is not { IsFinishingPose: true } action) return;
        // 原版统一旋转身体、手臂和刀，残影随后只平移，避免刀柄与手掌分离。
        // 不写 Player.fullRotation，结束/中断时无须恢复持久化旋转状态。
        drawInfo.rotation = action.FinisherBodyRotation;
        drawInfo.rotationOrigin = Player.Size * .5f;
    }

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        if (IsActing) PlayerDrawLayers.HeldItem.Hide();
    }

    public override void TransformDrawData(ref PlayerDrawSet drawInfo)
    {
        SakuraKiriPlayerProj action = Action;
        if (Main.gameMenu || drawInfo.headOnlyRender || action == null) return;

        drawInfo.projectileDrawPosition = -1;
        SakuraKiriDrawState state = action.DrawState;
        if (state is SakuraKiriDrawState.Hidden or SakuraKiriDrawState.Complete)
        {
            drawInfo.DrawDataCache.Clear();
            return;
        }

        if (state == SakuraKiriDrawState.Charge) return;

        appearance.Clear();
        appearance.AddRange(drawInfo.DrawDataCache);
        drawInfo.DrawDataCache.Clear();

        if (state == SakuraKiriDrawState.Sheathe)
        {
            // The center reveal uses two side echoes that converge while the real player fades in.
            float revealOpacity = action.BodyOpacity;
            float echoOpacity = action.CenterEchoOpacity;
            float echoOffset = action.CenterEchoOffset;
            foreach (DrawData original in appearance)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (echoOpacity <= .001f) break;
                    DrawData echo = original;
                    Vector2 shift = new(side * echoOffset, 0f);
                    echo.position += shift;
                    if (echo.useDestinationRectangle)
                    {
                        echo.destinationRectangle.X += (int)shift.X;
                        echo.destinationRectangle.Y += (int)shift.Y;
                    }
                    echo.color = Color.Lerp(original.color, new Color(255, 70, 100, original.color.A), .42f)
                        * echoOpacity;
                    drawInfo.DrawDataCache.Add(echo);
                }

                DrawData player = original;
                player.color *= revealOpacity;
                drawInfo.DrawDataCache.Add(player);
            }
            return;
        }

        Vector2 offset = action.DrawOffset;
        Vector2 direction = action.EchoDirection;
        float opacity = action.BodyOpacity;
        float echoStrength = state == SakuraKiriDrawState.Disappear ? 1f : action.FinisherEchoStrength;

        for (int echo = 3; echo >= 0; echo--)
        {
            float echoOpacity = echo == 0 ? opacity : opacity * echoStrength * (.24f - (echo - 1) * .045f);
            if (echoOpacity <= .001f) continue;
            Vector2 echoOffset = offset + direction * (echo * SakuraKiriFinisher.EchoSpacing * echoStrength);
            foreach (DrawData original in appearance)
            {
                DrawData copy = original;
                copy.position += echoOffset;
                if (copy.useDestinationRectangle)
                {
                    copy.destinationRectangle.X += (int)echoOffset.X;
                    copy.destinationRectangle.Y += (int)echoOffset.Y;
                }
                copy.color = Color.Lerp(copy.color, new Color(255, 70, 100, copy.color.A), .42f)
                    * echoOpacity;
                drawInfo.DrawDataCache.Add(copy);
            }
        }
    }

    public override void UpdateDead() => Clear();
    public override void PlayerDisconnect() => Clear();
    public override void OnEnterWorld() => Clear();

    private void Clear()
    {
        actionIndex = actionIdentity = -1;
        appearance.Clear();
    }
}
