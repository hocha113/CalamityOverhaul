using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class Mechanicalworm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 10000;
        public const int DontAttackTime = 90;
        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.scale = 2f;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 380;
            Projectile.alpha = 0;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Math.Abs(Projectile.velocity.X) > 13 || Math.Abs(Projectile.velocity.Y) > 13) {
                Projectile.velocity *= 0.99f;
            }
            Projectile.ai[0]++;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.ai[0] < DontAttackTime) {
                float point = 0f;
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , Projectile.Center, Projectile.rotation.ToRotationVector2() * -3000 + Projectile.Center, 64, ref point);
            }
            return base.Colliding(projHitbox, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D[] worm = [
                DestroyerHeadAI.Head.Value
                , DestroyerBodyAI.Body.Value
                , DestroyerBodyAI.Tail.Value];
            if (HeadPrimeAI.DontReform()) {
                Main.instance.LoadNPC(NPCID.TheDestroyer);
                Main.instance.LoadNPC(NPCID.TheDestroyerBody);
                Main.instance.LoadNPC(NPCID.TheDestroyerTail);
                worm = [
                TextureAssets.Npc[NPCID.TheDestroyer].Value
                , TextureAssets.Npc[NPCID.TheDestroyerBody].Value
                , TextureAssets.Npc[NPCID.TheDestroyerTail].Value];
            }
            Vector2 toD = Projectile.rotation.ToRotationVector2();
            Color drawColor = Color.White;
            if (Projectile.ai[0] >= DontAttackTime) {
                drawColor = Color.White * 0.3f;
            }
            for (int i = 0; i < 122; i++) {
                Texture2D body = worm[1];
                if (!HeadPrimeAI.DontReform() && Projectile.ai[0] >= DontAttackTime) {
                    body = DestroyerBodyAI.Body_Stingless.Value;
                }
                if (i == 121) {
                    body = worm[2];
                }
                Vector2 drawPos = Projectile.Center - Main.screenPosition + toD * -(30 + i * 60) * Projectile.scale;
                Main.EntitySpriteDraw(body, drawPos, null, drawColor
                , Projectile.rotation + MathHelper.PiOver2 + MathHelper.Pi, body.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            }
            if (HeadPrimeAI.DontReform()) {
                Main.EntitySpriteDraw(worm[0], Projectile.Center - Main.screenPosition, null, drawColor
                , Projectile.rotation + MathHelper.PiOver2, worm[0].Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            }
            else {
                Main.EntitySpriteDraw(worm[0], Projectile.Center - Main.screenPosition, null, drawColor
                , Projectile.rotation + MathHelper.PiOver2 + MathHelper.Pi, worm[0].Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
