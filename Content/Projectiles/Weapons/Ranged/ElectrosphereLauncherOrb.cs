using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Terraria;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class ElectrosphereLauncherOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "LightningOrb";
        public List<Projectile> Orbs = new List<Projectile>();
        public Projectile[] orbList = new Projectile[] { };
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.timeLeft = 320;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
        }

        public override bool PreAI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            return true;
        }

        public override void AI() {
            Projectile.scale = 0.6f + Math.Abs(MathF.Sin(Projectile.ai[0] * 0.1f) * 0.2f);
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            Projectile.ai[0]++;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            foreach (Projectile p in orbList) {
                if (p == Projectile || !p.Alives() || p.type != Projectile.type) {
                    continue;
                }
                Vector2 pos = p.Center;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, pos, 3, ref point)) {
                    return true;
                }
            }
            return null;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Texture2D value2 = CWRUtils.GetT2DValue(CWRConstant.Projectile_Ranged + "ElectricBolt");

            foreach (Projectile p in orbList) {
                if (p == Projectile || !p.Alives() || p.type != Projectile.type) {
                    continue;
                }
                Vector2 pos = p.Center;
                Vector2 toPos = Projectile.Center.To(pos);
                Vector2 toPosNor = toPos.UnitVector();
                float rot = toPosNor.ToRotation();
                float leng = toPos.Length();
                int wid = value2.Width / 2;
                int num = (int)(leng / wid) + 1;
                for (int i = 0; i < num; i++) {
                    Vector2 drawPos = toPosNor * (i * wid) + Projectile.Center - Main.screenPosition;
                    Main.EntitySpriteDraw(value2, drawPos, CWRUtils.GetRec(value2, Projectile.frame, 4)
                    , Color.White, rot, CWRUtils.GetOrig(value2, 4), Projectile.scale, SpriteEffects.None, 0);
                }
            }
            

            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 4)
                , Color.White, Projectile.rotation, CWRUtils.GetOrig(value, 4), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
