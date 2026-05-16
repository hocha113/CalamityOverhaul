using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    //僚机：在玩家上下伴飞并向敌人发射小型星弹
    internal class StarshipWingman : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //正负号决定僚机在玩家上下哪一侧
        private ref float Side => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override bool? CanDamage() => false;
        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //保持当前武器是群星巨舰
            if (owner.HeldItem?.type != ModContent.ItemType<Starship>()) {
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 20);
            }

            Vector2 targetOffset = new Vector2(-owner.direction * 30f, Side * -40f);
            float hover = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + Projectile.whoAmI * 0.5f) * 4f;
            targetOffset.Y += hover;

            Vector2 targetPos = owner.Center + targetOffset;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.25f);

            Projectile.localAI[0]++;
            if (Projectile.localAI[0] % 22 == 0 && Projectile.IsOwnedByLocalPlayer()) {
                NPC target = FindNearestEnemy(900f);
                Vector2 shootDir = target != null
                    ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                    : (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX);

                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, shootDir * 16f
                    , ModContent.ProjectileType<StarshipMicroStar>()
                    , Projectile.damage, 0f, Projectile.owner);
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.5f, 0.8f);
        }

        private NPC FindNearestEnemy(float range) {
            NPC best = null;
            float minDist = range * range;
            foreach (NPC n in Main.npc) {
                if (!n.CanBeChasedBy() || Vector2.DistanceSquared(n.Center, Projectile.Center) > minDist) {
                    continue;
                }
                minDist = Vector2.DistanceSquared(n.Center, Projectile.Center);
                best = n;
            }
            return best;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float time = Main.GlobalTimeWrappedHourly;
            float fade = Projectile.timeLeft < 30 ? Projectile.timeLeft / 30f : 1f;

            //菱形结构
            sb.Draw(glow, drawPos, null, new Color(120, 170, 255, 0) * 0.75f * fade, 0f, glow.Size() * 0.5f
                , new Vector2(1.4f, 0.6f), SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, new Color(220, 235, 255, 0) * 0.9f * fade, 0f, glow.Size() * 0.5f, 0.55f, SpriteEffects.None, 0);

            //顶端星芒
            if (star != null) {
                sb.Draw(star, drawPos, null, new Color(180, 210, 255, 0) * 0.9f * fade
                    , time * 5f, star.Size() * 0.5f, 0.6f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
