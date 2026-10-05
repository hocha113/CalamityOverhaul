using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Shared swing and draw behavior for the two Sakura slash presets.</summary>
public abstract class SakuraSlashBaseProj : ModProjectile
{
    protected virtual bool UseSecondPreset => false;
    // 弹幕动作总帧数；决定挥刀姿态从起点过渡到终点的时间。
    private int AttackFrames => 20;
    // 同时控制武器关键帧、碰撞时间轴和刀光内部动画；1 为原始速度。
    private float AnimationSpeedMultiplier => 1.0f;
    // 弹幕需覆盖刀光完整生命周期；多留若干帧避免更新/绘制顺序截断末帧。
    private int ProjectileLifetimeFrames => Math.Max(25, (int)MathF.Ceiling(SakuraSlashStyle.Lifetime * 60f)) + 1;
    // ai[1] 是生成时锁定并同步的实际倍率；ai[2] 可额外指定自定义倍率（0 = 1）。
    public float SpeedMultiplier => SakuraPlayback.ClampSpeed(Projectile.ai[1]);
    private float previousAnimationFrame;
    // 伤害判定开始与结束的动作帧（含边界）。
    private int StrikeStart => 6;
    private int StrikeEnd => 20;
    // 刀光局部网格缩放；不影响弹幕碰撞范围。
    private float SlashScale => 1.0f;
    // 径向消融方向：指向保留时间更长的一侧；(0,-1)/(0,1)/(-1,0)/(1,0) 为上/下/左/右。
    private Vector2 DissolveDirection => new(-1, 0);
    // 覆盖完整溶解尾部，武器/刀光/伤害共用原速时间轴。
    private float SlashLifetime => SakuraSlashStyle.Lifetime;
    // Legacy pose fallback still follows the original weapon sweep.
    private float SweepStartOffset => -0.5f * facingDirection * gravityDirection;

    private SakuraSlashInstance slash;
    private BasicEffect weaponEffect;
    private readonly VertexPositionColorTexture[] weaponVertices = new VertexPositionColorTexture[4];
    private static readonly int[] WeaponIndices = [0, 1, 2, 0, 2, 3];
    private bool weaponAnimationWarningLogged;
    private int facingDirection = 1;
    private int gravityDirection = 1;
    private bool initialized;

    // The item texture is authored vertically, with the grip near its lower-left
    // corner.  Keeping the pivot in texture pixels makes the hand anchor
    // independent of the sprite's bounding box and of SpriteEffects mirroring.
    private Vector2 WeaponGrip => new Vector2(14f, 208f);
    private Vector2 WeaponBladeVector  => new(58f, -218f);

    private Player Owner => Main.player[Projectile.owner];

    public override string Texture => "CalamityOverhaul/Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/OnikiriItem";

    public override void SetDefaults()
    {
        Projectile.width = 160;
        Projectile.height = 160;
        Projectile.aiStyle = -1;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.hide = true;
        Projectile.timeLeft = ProjectileLifetimeFrames;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => overPlayers.Add(index);

    public override bool? CanDamage() =>
        SakuraPlayback.CrossesWindow(previousAnimationFrame, Projectile.ai[0], StrikeStart, StrikeEnd);

    public override void ModifyDamageHitbox(ref Rectangle hitbox)
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) return;
        hitbox = ModContent.GetInstance<SakuraSlashSystem>().GetHitArc(UseSecondPreset)
            .Bounds(CreateHitPose(Owner), SlashScale);
    }

    public override void AI()
    {
        Player player = Owner;
        if (!player.active || player.dead || !IsSakuraWeapon(player.HeldItem))
        {
            slash?.Kill();
            Projectile.Kill();
            return;
        }

        if (!initialized)
        {
            initialized = true;
            if (!float.IsFinite(Projectile.ai[1]) || Projectile.ai[1] <= 0)
            {
                float requestedSpeed = float.IsFinite(Projectile.ai[2]) && Projectile.ai[2] > 0 ? Projectile.ai[2] : 1f;
                Projectile.ai[1] = SakuraPlayback.ClampSpeed(AnimationSpeedMultiplier * requestedSpeed
                    * player.GetWeaponAttackSpeed(player.HeldItem));
                if (Projectile.owner == Main.myPlayer) Projectile.netUpdate = true;
            }
            facingDirection = player.direction == 0 ? 1 : player.direction;
            gravityDirection = player.gravDir < 0 ? -1 : 1;
            SpawnSlash(player);
        }

        if (Projectile.ai[0] >= ProjectileLifetimeFrames)
        {
            Projectile.Kill();
            return;
        }
        previousAnimationFrame = Projectile.ai[0];
        float animationFrame = previousAnimationFrame + SpeedMultiplier;
        float progress = MathHelper.Clamp(animationFrame / (AttackFrames - 1), 0, 1);
        float easedProgress = 1 - MathF.Pow(1 - progress, 4);

        Projectile.timeLeft = 2;
        Projectile.velocity = Vector2.Zero;
        Projectile.direction = facingDirection;
        Projectile.spriteDirection = facingDirection;
        player.ChangeDir(facingDirection);
        // DrawBehind places this projectile over players; heldProj would draw it inside PlayerDrawLayers.
        player.heldProj = Projectile.whoAmI;
        player.itemTime = player.itemAnimation = 2;
        float armRotation = GetAnimatedArmRotation(animationFrame, easedProgress)*player.gravDir;
        player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
        Projectile.Center = GetHandPosition(player, armRotation);

        if (slash != null && slash.IsActive)
        {
            ConfigureSlash();
            slash.Position = CreatePose(player, visual: true).TransformLocalPoint(Vector2.Zero);
            slash.LocalTransform = CreatePose(player, visual: true).CreateSlashTransform(SlashScale, .1f);
        }

        Projectile.ai[0] = animationFrame;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!CanDamage().GetValueOrDefault()) return false;
        return ModContent.GetInstance<SakuraSlashSystem>().GetHitArc(UseSecondPreset)
            .Intersects(targetHitbox, CreateHitPose(Owner), SlashScale);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Player player = Owner;
        if (!player.active || player.dead) return false;

        Main.spriteBatch.End();
        try
        {
            Texture2D texture = ModContent.GetInstance<SakuraSlashSystem>().WeaponTexture;
            DrawAnimatedWeapon(texture, player, Projectile.ai[0], lightColor);
            if (slash != null && slash.IsActive && slash.DrawOpacity > 0) DrawSlash();
        }
        finally
        {
            // Deferred Begin does not bind its effect until it flushes. PlayerDraw's
            // vertex buffer can render first, so reset the slash vertex shader now.
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }
        return false;
    }

    private void DrawSlash()
    {
        
        ModContent.GetInstance<SakuraSlashSystem>().Draw(slash, UseSecondPreset);
    }

    private float GetArmRotation(float progress)
    {
        // GetFrontHandPosition uses the same arm convention as SetCompositeArmFront:
        // the hand direction is the blade direction minus 90 degrees.  Applying
        // gravDir here keeps inverted gravity from reversing the swing.
        return GetSweepAngle(progress) - MathHelper.PiOver2 * gravityDirection;
    }

    private float GetAnimatedArmRotation(float frame, float progress)
    {
        if (Main.dedServ) return GetArmRotation(progress);
        try
        {
            SakuraWeaponAnimation animation = ModContent.GetInstance<SakuraSlashSystem>().GetAnimation(UseSecondPreset);
            if (animation != null && animation.TryGetArmRotation(frame * animation.Fps / 60f, facingDirection, gravityDirection, out float rotation))
                return rotation;
        }
        catch (Exception)
        {
            // DrawAnimatedWeapon reports an unavailable animation once on the client.
        }
        return GetArmRotation(progress);
    }

    private Vector2 GetHandPosition(Player player, float armRotation)
    {
        Vector2 hand = player.GetFrontHandPosition(
            Player.CompositeArmStretchAmount.Full,
            armRotation);

        float bladeAngle = armRotation + MathHelper.PiOver2 * gravityDirection;
        hand += new Vector2(MathF.Cos(bladeAngle), MathF.Sin(bladeAngle))
                * 17;

        return CreatePose(player, visual: false).RotateWorldPoint(hand + new Vector2(0f, player.gfxOffY));
    }

    private SakuraWeaponPose CreatePose(Player player, bool visual) => new(
        player.Center + (visual ? new Vector2(0f, player.gfxOffY) : Vector2.Zero),
        player.position + player.fullRotationOrigin,
        player.fullRotation, facingDirection, gravityDirection);

    private SakuraWeaponPose CreateHitPose(Player player) => new(
        player.MountedCenter, player.position + player.fullRotationOrigin,
        player.fullRotation, facingDirection, gravityDirection);

    private Vector2 GetWeaponOrigin(Texture2D texture, SpriteEffects effects)
    {
        // SpriteEffects flips the texture coordinates, but SpriteBatch keeps the
        // supplied origin in source-texture space. Mirror the origin explicitly
        // so the same grip pixel remains on the player's hand on every side.
        Vector2 origin = WeaponGrip;
        if ((effects & SpriteEffects.FlipHorizontally) != 0) origin.X = texture.Width - origin.X;
        if ((effects & SpriteEffects.FlipVertically) != 0) origin.Y = texture.Height - origin.Y;
        return origin;
    }

    private float GetWeaponRotation(float progress, SpriteEffects effects)
    {
        // SpriteBatch applies mirroring before rotation. Transform the local
        // grip-to-tip vector first so the blade still points along the sweep on
        // both sides of the player and under inverted gravity.
        Vector2 bladeVector = WeaponBladeVector;
        if ((effects & SpriteEffects.FlipHorizontally) != 0) bladeVector.X = -bladeVector.X;
        if ((effects & SpriteEffects.FlipVertically) != 0) bladeVector.Y = -bladeVector.Y;
        return GetSweepAngle(progress) - bladeVector.ToRotation();
    }

    private bool DrawAnimatedWeapon(Texture2D texture, Player player, float frame, Color lightColor)
    {
        SakuraWeaponAnimation animation;
        try
        {
            animation = ModContent.GetInstance<SakuraSlashSystem>().GetAnimation(UseSecondPreset);
        }
        catch (Exception ex)
        {
            if (Main.netMode != NetmodeID.Server && !weaponAnimationWarningLogged)
            {
                weaponAnimationWarningLogged = true;
                Mod.Logger.Warn($"Weapon animation unavailable; using legacy pose. {ex.Message}");
            }
            return false;
        }
        if (animation == null) return false;

        Vector2[] corners = animation.Evaluate(frame * animation.Fps / 60f);
        float localScale = SlashScale;
        SakuraWeaponPose pose = CreatePose(player, visual: true);
        Matrix worldToScreen = Matrix.CreateTranslation(new Vector3(-Main.screenPosition, 0))
            * Main.GameViewMatrix.TransformationMatrix;
        pose.FillWeaponVertices(corners, localScale, worldToScreen, lightColor, weaponVertices);

        GraphicsDevice device = Main.instance.GraphicsDevice;
        weaponEffect ??= new BasicEffect(device)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            LightingEnabled = false,
            World = Matrix.Identity,
            View = Matrix.Identity
        };
        weaponEffect.Texture = texture;
        weaponEffect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width,
            device.Viewport.Height, 0, 0, 1);

        BlendState oldBlend = device.BlendState;
        DepthStencilState oldDepth = device.DepthStencilState;
        RasterizerState oldRasterizer = device.RasterizerState;
        SamplerState oldSampler = device.SamplerStates[0];
        Texture oldTexture = device.Textures[0];
        try
        {
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;
            foreach (EffectPass pass in weaponEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.Textures[0] = texture;
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, weaponVertices, 0, 4,
                    WeaponIndices, 0, 2);
            }
        }
        finally
        {
            device.BlendState = oldBlend;
            device.DepthStencilState = oldDepth;
            device.RasterizerState = oldRasterizer;
            device.SamplerStates[0] = oldSampler;
            device.Textures[0] = oldTexture;
        }
        return true;
    }

    public override void OnKill(int timeLeft)
    {
        slash?.Kill();
        weaponEffect?.Dispose();
        weaponEffect = null;
    }

    private void SpawnSlash(Player player)
    {
        if (Main.dedServ) return;

        SakuraWeaponPose pose = CreatePose(player, visual: true);
        slash = SakuraSlashSystem.Spawn(pose.TransformLocalPoint(Vector2.Zero),
            pose.CreateSlashTransform(SlashScale, .1f), SlashLifetime, UseSecondPreset);
        if (slash == null) return;
        ConfigureSlash();
        // Reconstruct the matching animation phase on a late network spawn.
        if (Projectile.ai[0] > 0)
        {
            slash.Advance(Projectile.ai[0] / (60f * SpeedMultiplier));
            slash.TakePetalEmission(out _, out _);
        }
    }

    private void ConfigureSlash()
    {
        SakuraSlashStyle.Apply(slash, SpeedMultiplier);
        slash.DissolveDirection = DissolveDirection;
    }

    private float GetSweepAngle(float progress)
    {
        float start = -MathHelper.PiOver2 * gravityDirection + SweepStartOffset;
        float end = -MathHelper.PiOver2 * gravityDirection + facingDirection * gravityDirection * MathHelper.Pi;
        return MathHelper.Lerp(start, end, progress);
    }

    private static bool IsSakuraWeapon(Item item) =>
        item.type == ModContent.ItemType<SakuraSlashTest>();
}

public sealed class SakuraSlashProj : SakuraSlashBaseProj { }
