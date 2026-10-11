using System;
using System.Collections.Generic;


using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>独立手工编排剑气乱斩（见同名 Pattern 文件）。ai[0] 原速帧龄，ai[1] 播放倍率（0 默认 2），ai[2] 初始角度（弧度）。</summary>
public sealed partial class SakuraRangedSlashProj : ModProjectile
{
    private SakuraRangedSlashSequence sequence;
    public float SpeedMultiplier => SakuraPlayback.ClampSpeed(Projectile.ai[1], SakuraRangedSlashSequence.DefaultSpeed);
    public override string Texture => SakuraFinaleAssets.TexturePath + "OnikiriItem";

    /// <summary>零速度：在 position 原地乱斩；非零速度：整组剑气随 velocity 飞行。</summary>
    public static int Spawn(IEntitySource source, Vector2 position, Vector2 velocity, int damage,
        float knockback, int owner, float speedMultiplier = 2f, float rotation = 0f) =>
        Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<SakuraRangedSlashProj>(),
            damage, knockback, owner, 0f, SakuraPlayback.ClampSpeed(speedMultiplier, 2f),
            float.IsFinite(rotation) ? rotation : 0f);

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 32;
        Projectile.aiStyle = -1;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hide = true;
        Projectile.timeLeft = 1800;
        Projectile.usesLocalNPCImmunity = true;
        // 每刀的命中记录由 sequence 独立维护，允许任意时序重叠；同 tick 最多命中一次。
        Projectile.localNPCHitCooldown = 1;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        float speed = SpeedMultiplier;
        Projectile.ai[1] = speed;
        if (!float.IsFinite(Projectile.ai[0]) || Projectile.ai[0] < 0) Projectile.ai[0] = 0;
        if (!float.IsFinite(Projectile.ai[2])) Projectile.ai[2] = 0;
        if (sequence == null)
        {
            var assets = ModContent.GetInstance<SakuraFinaleAssets>();
            sequence = new SakuraRangedSlashSequence(assets.RangedMesh, Cuts, assets.RangedPetals);
            // Network catch-up must not replay the petals of old strikes.
            if (Projectile.ai[0] > 0)
            {
                sequence.AdvanceTo(Projectile.ai[0] / 60f, Projectile.Center, Projectile.ai[2],
                    SakuraRangedSlashSequence.SlashScale, speed);
                foreach (var slash in sequence.Slashes) slash?.TakePetalEmission(out _, out _);
            }
        }
        if (Projectile.ai[0] / 60f >= sequence.Duration)
        {
            Projectile.Kill();
            return;
        }
        Projectile.timeLeft = 2;
        Projectile.Center += Projectile.velocity;
        Projectile.ai[0] += speed;
        sequence.AdvanceTo(Projectile.ai[0] / 60f, Projectile.Center, Projectile.ai[2],
            SakuraRangedSlashSequence.SlashScale, speed);
        if (!Main.dedServ)
        {
            foreach (var slash in sequence.Slashes)
                if (slash != null && slash.TakePetalEmission(out int first, out int count))
                {
                    slash.PetalBurst?.Emit(slash, first, count, EmitPetals);
                }
        }
    }

    private static void EmitPetals(Vector2 position, Vector2 direction, SakuraEmissionSettings settings) =>
        SakuraParticleSystem.EmitBurst(position, direction, settings);

    public override bool? CanDamage() => sequence != null && sequence.CanDamage;
    public override bool? CanHitNPC(NPC target) =>
        sequence != null && sequence.CanHitNpc(target.whoAmI, target.Hitbox) ? null : false;

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
        sequence?.RegisterNpcHit(target.whoAmI, target.Hitbox);

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
        sequence != null && sequence.Collides(targetHitbox);

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => overPlayers.Add(index);

    public override bool PreDraw(ref Color lightColor)
    {
        if (sequence == null || Main.dedServ) return false;
        SakuraSlashRenderer renderer = ModContent.GetInstance<SakuraFinaleAssets>().RangedRenderer();
        Main.spriteBatch.End();
        try
        {
            // Oldest first: the newest cut is always in front of its dissolving predecessors.
            foreach (SakuraSlashInstance slash in sequence.Slashes)
                if (slash != null && slash.IsActive && slash.DrawOpacity > 0)
                    renderer.Draw(slash, Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
        }
        finally
        {
            SakuraFinaleAssets.RestoreWorldBatch();
        }
        return false;
    }

    public override void OnKill(int timeLeft) => sequence?.Kill();
}
