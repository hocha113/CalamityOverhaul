using CalamityMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon
{
    internal class GhostFires : ModProjectile
    {
        public bool ableToHit = true;

        public NPC target;

        public new string LocalizationCategory => "Projectiles.Summon";

        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionShot[Projectile.type] = true;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 200;
            Projectile.extraUpdates = 3;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 50;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override bool? CanDamage() {
            if (!ableToHit) {
                return false;
            }

            return null;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Projectile.localAI[0] += 1f / (Projectile.extraUpdates + 1);
            if (Projectile.penetrate < 200) {
                if (Projectile.timeLeft > 60) {
                    Projectile.timeLeft = 60;
                }

                Projectile.velocity *= 0.88f;
            }
            else if (Projectile.localAI[0] < 60f) {
                Projectile.velocity *= 0.93f;
            }
            else {
                FindTarget(player);
            }

            if (Projectile.timeLeft <= 20) {
                ableToHit = false;
            }
        }

        public void FindTarget(Player player) {
            float num = 3000f;
            bool flag = false;
            if (player.HasMinionAttackTargetNPC) {
                NPC nPC = Main.npc[player.MinionAttackTargetNPC];
                if (nPC.CanBeChasedBy(Projectile)) {
                    float num2 = Vector2.Distance(nPC.Center, Projectile.Center);
                    if (num2 < num) {
                        num = num2;
                        flag = true;
                        target = nPC;
                    }
                }
            }

            if (!flag) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nPC2 = Main.npc[i];
                    if (nPC2.CanBeChasedBy(Projectile)) {
                        float num3 = Vector2.Distance(nPC2.Center, Projectile.Center);
                        if (num3 < num) {
                            num = num3;
                            flag = true;
                            target = nPC2;
                        }
                    }
                }
            }

            if (!flag) {
                Projectile.velocity *= 0.98f;
            }
            else {
                KillTheThing(target);
            }
        }

        public void KillTheThing(NPC npc) {
            Projectile.velocity = Projectile.SuperhomeTowardsTarget(npc, 50f / (Projectile.extraUpdates + 1), 60f / (Projectile.extraUpdates + 1), 1f / (Projectile.extraUpdates + 1));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/SmallGreyscaleCircle").Value;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float amount = MathF.Cos(Projectile.timeLeft / 32f + Main.GlobalTimeWrappedHourly / 20f + i / (float)Projectile.oldPos.Length * MathF.PI) * 0.5f + 0.5f;
                Color color = Color.Lerp(Color.Cyan, Color.LightBlue, amount) * 0.4f;
                color.A = 0;
                Vector2 position = Projectile.oldPos[i] + value.Size() * 0.5f - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY) + new Vector2(-28f, -28f);
                Color color2 = color;
                Color color3 = color * 0.5f;
                float num = 0.9f + 0.15f * MathF.Cos(Main.GlobalTimeWrappedHourly % 60f * MathHelper.TwoPi);
                num *= MathHelper.Lerp(0.15f, 1f, 1f - i / (float)Projectile.oldPos.Length);
                if (Projectile.timeLeft <= 60) {
                    num *= Projectile.timeLeft / 60f;
                }

                Vector2 vector = new Vector2(1f) * num;
                Vector2 vector2 = new Vector2(1f) * num * 0.7f;
                color2 *= num;
                color3 *= num;
                Main.EntitySpriteDraw(value, position, null, color2, 0f, value.Size() * 0.5f, vector * 0.6f, SpriteEffects.None);
                Main.EntitySpriteDraw(value, position, null, color3, 0f, value.Size() * 0.5f, vector2 * 0.6f, SpriteEffects.None);
            }

            return false;
        }
    }
}
