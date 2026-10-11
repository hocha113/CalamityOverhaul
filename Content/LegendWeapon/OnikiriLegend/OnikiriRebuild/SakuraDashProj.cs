using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>各端首帧单点扫路、遇障停止并移动玩家，随后播放现身和瞬斩。</summary>
public sealed class SakuraDashProj : ModProjectile
{
    public override string Texture => SakuraDashSystem.BasePath + "SakuraDashProj";

    private bool routeReady;
    private int finisherFacing = 1;
    private int sourceItemType;
    public bool ShowFinisherWeapon => routeReady && Projectile.ai[0] >= SakuraDashMotion.TravelFrames;
    public float PlayerDrawRotation => SakuraDashWeaponPose.PlayerRotation(Projectile.rotation, finisherFacing);
    public float TravelDistance { get; private set; } = SakuraDashTiming.DashDistance;
    public Vector2 DashDirection => Projectile.ai[1].ToRotationVector2();

    public static int Spawn(IEntitySource source, Vector2 position, Vector2 dashVelocity, int owner)
    {
        if (owner < 0 || owner >= Main.maxPlayers) return Main.maxProjectiles;
        if (!float.IsFinite(dashVelocity.X) || !float.IsFinite(dashVelocity.Y))
            dashVelocity = Vector2.Zero;
        Vector2 direction = dashVelocity.LengthSquared() > .01f
            ? Vector2.Normalize(dashVelocity)
            : new Vector2(Main.player[owner].direction, 0f);
        float speed = dashVelocity.LengthSquared() > .01f ? dashVelocity.Length() : 18f;
        return Projectile.NewProjectile(source, position, Vector2.Zero, ModContent.ProjectileType<SakuraDashProj>(),
            0, 0f, owner, 0f, direction.ToRotation(), MathHelper.Clamp(speed, 8f, 30f));
    }

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hide = true;
        Projectile.timeLeft = SakuraDashTiming.ProjectileLifetime;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
        {
            Projectile.Kill();
            return;
        }
        Player owner = Main.player[Projectile.owner];
        owner.fallStart = (int)(owner.position.Y / 16f);
        if (!owner.active || owner.dead || owner.CCed || (routeReady && sourceItemType != owner.HeldItem.type))
        {
            Projectile.Kill();
            return;
        }
        SakuraDashPlayer dashPlayer = owner.GetModPlayer<SakuraDashPlayer>();
        bool blinkNow = false;
        if (!routeReady)
        {
            // 各端独立初始化固定路径，不等待拥有者发送额外状态。
            if (dashPlayer.IsDashing)
            {
                Projectile.Kill();
                return;
            }
            Projectile.ai[1] = Projectile.velocity.LengthSquared() > .001f
                ? Projectile.velocity.ToRotation() : Projectile.ai[1];
            Projectile.rotation = Projectile.ai[1];
            finisherFacing = System.MathF.Abs(DashDirection.X) > .01f ? (DashDirection.X > 0f ? 1 : -1) : owner.direction;
            sourceItemType = owner.HeldItem.type;
            // 单点检测始终基于弹幕生成点，与玩家当前中心、宽高无关。
            if (owner.mount.Active) owner.mount.Dismount(owner);
            Vector2 start = Projectile.Center;
            Vector2 end = dashPlayer.ResolveBlinkDestination(start, start + DashDirection * SakuraDashTiming.DashDistance);
            TravelDistance = Vector2.Distance(start, end);
            if (TravelDistance < 4f)
            {
                Projectile.Kill();
                return;
            }
            routeReady = true;
            blinkNow = true;
        }
        Projectile.rotation = Projectile.ai[1];
        if (!dashPlayer.Begin(this))
        {
            Projectile.Kill();
            return;
        }
        // 镜头已在 Begin 捕获闪现前位置，立即移动角色但继续隐藏至冲击抵达。
        if (blinkNow) dashPlayer.BlinkTo(Projectile.Center + DashDirection * TravelDistance);
        Projectile.ai[0]++;
        ApplyFinisherPose(owner);
    }

    public void ApplyFinisherPose(Player player)
    {
        if (!routeReady || !Projectile.active || !player.active || player.dead || player.HeldItem.type != sourceItemType) return;
        player.itemTime = player.itemAnimation = 2;
        if (!ShowFinisherWeapon) return;
        player.ChangeDir(finisherFacing);
        player.heldProj = Projectile.whoAmI;
        int gravity = player.gravDir < 0f ? -1 : 1;
        player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            SakuraDashWeaponPose.ArmRotation(finisherFacing, gravity));
        // Projectile.Center 必须保持闪现起点，不能像普通手持弹幕那样移到手上。
    }

    public void DrawHeldWeapon(ref PlayerDrawSet drawInfo)
    {
        Player player = drawInfo.drawPlayer;
        if (!ShowFinisherWeapon || !player.active || player.dead || player.HeldItem.type != sourceItemType) return;
        // 与其他 Sakura 手持 slash 一致，使用本地测试刀及其真实握点，不复用刀光贴图。
        Texture2D texture = ModContent.GetInstance<SakuraSlashSystem>().WeaponTexture;
        if (texture == null) return;
        int gravity = player.gravDir < 0f ? -1 : 1;
        SakuraDashWeaponPose.GetSpritePose(new Vector2(texture.Width, texture.Height), finisherFacing, gravity,
            out Vector2 origin, out float rotation, out SpriteEffects effects);
        // SetCompositeArmFront 内部会对反重力角度取反；GetFrontHandPosition 本身不处理重力。
        // 先取正常重力的同一姿态手位，再只镜像局部 Y，避免反重力下握点横向也翻转。
        Vector2 center = player.MountedCenter;
        Vector2 handOffset = player.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
            SakuraDashWeaponPose.UprightArmRotation(finisherFacing)) - center;
        // 沿持刀方向把剑向外推出，单位：像素。
        handOffset += SakuraDashWeaponPose.BodyDirection(finisherFacing) * 22f;
        
        handOffset.Y *= gravity;
        Vector2 hand = center + handOffset + new Vector2(0f, player.gfxOffY)
            + (drawInfo.Position - player.position);
        Color color = Lighting.GetColor((int)(hand.X / 16f), (int)(hand.Y / 16f));
        drawInfo.DrawDataCache.Add(new DrawData(texture, hand - Main.screenPosition, null, color,
            rotation, origin, SakuraDashWeaponPose.WeaponScale, effects, 0));
        // 玩家 DrawData 统一处理 fullRotation、重力视图、现身淡入及残影，不能重复乘透明度。
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => overPlayers.Add(index);

    public override bool PreDraw(ref Color lightColor)
    {
        if (Main.dedServ || !routeReady) return false;
        Main.spriteBatch.End();
        try
        {
            Matrix worldToScreen = Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0f)
                * Main.GameViewMatrix.TransformationMatrix;
            ModContent.GetInstance<SakuraDashSystem>().Draw(Projectile.Center, Projectile.rotation,
                Projectile.ai[0], TravelDistance, worldToScreen, Main.player[Projectile.owner].gravDir < 0f);
        }
        finally
        {
            // Restore the stock sprite shaders before player layers draw. A deferred Begin
            // alone does not apply them after the cut-line's custom effect.
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }
        return false;
    }

    public override void OnKill(int timeLeft)
    {
        if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
        {
            Player player = Main.player[Projectile.owner];
            if (player.heldProj == Projectile.whoAmI) player.heldProj = -1;
            Main.player[Projectile.owner].GetModPlayer<SakuraDashPlayer>().End(this);
        }
    }
}
