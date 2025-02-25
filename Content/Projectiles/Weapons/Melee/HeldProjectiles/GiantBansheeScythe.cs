using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

public class GiantBansheeScythe : ModProjectile
{
    public override string Texture => CWRConstant.Cay_Proj_Melee + "BansheeHookScythe";
    private HashSet<NPC> onHitNPCs = [];
    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
    }

    public override void SetDefaults() {
        Projectile.width = 38;
        Projectile.height = 38;
        Projectile.scale = 1.5f;
        Projectile.alpha = 100;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = 11;
        Projectile.timeLeft = 90;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        Lighting.AddLight(Projectile.Center, (255 - Projectile.alpha) * 0.6f / 255f, 0f, 0f);
        Projectile.ai[0] += MathHelper.ToRadians(35);
        NPC target = Projectile.Center.FindClosestNPC(600, onHitNPCs: onHitNPCs);
        if (Projectile.timeLeft < 65 && target != null) {
            Projectile.SmoothHomingBehavior(target.Center, 1, 0.2f);
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        Projectile.velocity *= 0.95f;
        if (Projectile.timeLeft <= 65) {
            onHitNPCs.Add(target);
        }
    }

    public override Color? GetAlpha(Color lightColor) {
        if (Projectile.timeLeft < 85) {
            byte b = (byte)(Projectile.timeLeft * 3);
            byte alpha = (byte)(100f * (b / 255f));
            return new Color(b, b, b, alpha);
        }

        return new Color(255, 255, 255, 100);
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D mainValue = TextureAssets.Projectile[Type].Value;
        float alp = Projectile.timeLeft / 30f;
        if (alp > 1) alp = 1;
        Color color = CWRUtils.RecombinationColor((Color.Red, 0.3f), (Projectile.GetAlpha(Color.Gold), 0.7f));
        Main.EntitySpriteDraw(
            mainValue,
            Projectile.Center - Main.screenPosition,
            null,
            color * alp,
            Projectile.ai[0],
            CWRUtils.GetOrig(mainValue),
            Projectile.scale,
            SpriteEffects.None,
            0
            );

        for (int i = 0; i < Projectile.oldPos.Length; i++) {
            float alp2 = 1 - i / (float)Projectile.oldPos.Length;
            float slp = 1 - i / (float)Projectile.oldPos.Length * 0.5f;
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.oldPos[i] + Projectile.Center - Projectile.position - Main.screenPosition,
                null,
                color * alp * alp2 * 0.5f,
                Projectile.ai[0],
                CWRUtils.GetOrig(mainValue),
                Projectile.scale * slp,
                SpriteEffects.None,
                0
            );
        }

        return false;
    }
}