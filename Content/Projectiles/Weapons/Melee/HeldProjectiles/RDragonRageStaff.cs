using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.CalPlayer;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RDragonRageStaff : ModProjectile
    {
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<DragonRageEcType>();

        public override string Texture => CWRConstant.Cay_Proj_Melee + "DragonRageStaff";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 408;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90000;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.hide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
        }

        public override void AI() {
            Projectile.ai[2]++;
            if (Projectile.ai[1] == 0) {
                SamsAI();
            }
            if (Projectile.ai[1] == 1) {
                SamsAI();
                if (Projectile.ai[2] % 10 == 0) {
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath, Projectile.Center);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * 25,
                        ModContent.ProjectileType<DragonRageFireball>(),
                        Projectile.damage,
                        Projectile.knockBack / 2f,
                        Projectile.owner
                        );
                }
            }
        }

        public void SamsAI() {
            Player player = Main.player[Projectile.owner];
            float num = 50f;
            if (!player.Alives()) {
                Projectile.Kill();
                player.reuseDelay = 2;
                return;
            }

            if (player.PressKey(false) || player.PressKey()) {
                Projectile.timeLeft = 2;
            }
            else {
                Projectile.Kill();
            }

            int num2 = Math.Sign(Projectile.velocity.X);
            Projectile.velocity = new Vector2(num2, 0f);
            if (Projectile.ai[0] == 0f) {
                Projectile.rotation = new Vector2(num2, 0f - player.gravDir).ToRotation() + MathHelper.ToRadians(135f);
                if (Projectile.velocity.X < 0f) {
                    Projectile.rotation -= MathF.PI / 2f;
                }
            }

            Projectile.ai[0] += 1f;
            Projectile.rotation += MathF.PI * 4f / num * num2;
            int toMous = (player.SafeDirectionTo(Main.MouseWorld).X > 0f).ToDirectionInt();
            if (Projectile.ai[0] % num > num * 0.5f && toMous != Projectile.velocity.X) {
                player.ChangeDir(toMous);
                Projectile.velocity = Vector2.UnitX * toMous;
                Projectile.rotation -= MathF.PI;
                Projectile.netUpdate = true;
            }
            SpawnDust(player, num2);
            PositionAndRotation(player);
            VisibilityAndLight();
        }

        private void SpawnDust(Player player, int direction) {
            float rot = Projectile.rotation - MathF.PI / 4f * direction;
            Vector2 vector = Projectile.Center + (rot + (direction == -1 ? MathF.PI : 0f)).ToRotationVector2() * 200 * Projectile.scale;
            Vector2 vector2 = rot.ToRotationVector2();
            Vector2 vector3 = vector2.RotatedBy(MathF.PI / 2f * Projectile.spriteDirection);
            if (Main.rand.NextBool()) {
                Dust dust = Dust.NewDustDirect(vector - new Vector2(5f), 10, 10, DustID.CopperCoin, player.velocity.X, player.velocity.Y, 150);
                dust.velocity = Projectile.SafeDirectionTo(dust.position) * 0.1f + dust.velocity * 0.1f;
            }

            for (int i = 0; i < 4; i++) {
                float speedRands = 1f;
                float modeRands = 1f;
                switch (i) {
                    case 1:
                        modeRands = -1f;
                        break;
                    case 2:
                        modeRands = 1.25f;
                        speedRands = 0.5f;
                        break;
                    case 3:
                        modeRands = -1.25f;
                        speedRands = 0.5f;
                        break;
                }

                if (!Main.rand.NextBool(6)) {
                    Dust dust2 = Dust.NewDustDirect(Projectile.position, 0, 0, DustID.CopperCoin, 0f, 0f, 100);
                    dust2.position = Projectile.Center + vector2 * (180 * Projectile.scale + Main.rand.NextFloat() * 20f) * modeRands;
                    dust2.velocity = vector3 * (4f + 4f * Main.rand.NextFloat()) * modeRands * speedRands;
                    dust2.noGravity = true;
                    dust2.noLight = true;
                    dust2.scale = 0.5f;
                    if (Main.rand.NextBool(4)) {
                        dust2.noGravity = false;
                    }
                }
            }
        }

        private void PositionAndRotation(Player player) {
            Vector2 vector = player.RotatedRelativePoint(player.MountedCenter, reverseRotation: true);
            Vector2 zero = Vector2.Zero;
            Projectile.Center = vector + zero;
            Projectile.spriteDirection = Projectile.direction;
            Projectile.timeLeft = 2;
            player.ChangeDir(Projectile.direction);
            player.heldProj = Projectile.whoAmI;
            player.itemTime = player.itemAnimation = 2;
            player.itemRotation = MathHelper.WrapAngle(Projectile.rotation);
        }

        private void VisibilityAndLight() {
            Lighting.AddLight(Projectile.Center, 1.45f, 1.22f, 0.58f);
            Projectile.alpha -= 128;
            if (Projectile.alpha < 0) {
                Projectile.alpha = 0;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Dragonfire>(), 180);
            OnHitEffects(target.Center);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            OnHitEffects(target.Center);
        }

        private void OnHitEffects(Vector2 position) {
            if (Projectile.owner == Main.myPlayer && Projectile.ai[1] == 0) {
                CalamityPlayer calamityPlayer = Main.player[Projectile.owner].Calamity();
                calamityPlayer.dragonRageHits++;
                if (calamityPlayer.dragonRageHits > 10 && calamityPlayer.dragonRageCooldown <= 0) {
                    SpawnFireballs();
                    calamityPlayer.dragonRageHits = 0;
                }

                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, Vector2.Zero, ModContent.ProjectileType<FuckYou>(), Projectile.damage / 4, Projectile.knockBack, Projectile.owner, 0f, 0.85f + Main.rand.NextFloat() * 1.15f);
                Main.projectile[proj].DamageType = DamageClass.Melee;
            }
        }

        private void SpawnFireballs() {
            int maxShootNum = Main.rand.Next(10, 16);
            for (int i = 0; i < maxShootNum; i++) {
                float rots = MathF.PI * 2f / maxShootNum;
                float y = 20f;
                Vector2 spinningpoint = new Vector2(0f, y);
                spinningpoint = spinningpoint.RotatedBy(rots * i * Main.rand.NextFloat(0.9f, 1.1f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, spinningpoint, ModContent.ProjectileType<DragonRageFireball>(), Projectile.damage / 8, Projectile.knockBack / 3f, Projectile.owner);
            }

            Main.player[Projectile.owner].Calamity().dragonRageCooldown = 60;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            Player player = Main.player[Projectile.owner];
            Rectangle hitbox = Projectile.Hitbox;
            if (Projectile.owner != Main.myPlayer) {
                return;
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC nPC = Main.npc[i];
                bool flag = Projectile.owner < 255 && (nPC.type == NPCID.Guide && player.killGuide || nPC.type == NPCID.Clothier && player.killClothier);
                bool flag2 = Projectile.friendly && (!nPC.friendly || flag);
                bool flag3 = Projectile.hostile && nPC.friendly && !nPC.dontTakeDamageFromHostiles;
                if (nPC.active && !nPC.dontTakeDamage && (flag2 || flag3) && (Projectile.owner < 0 || nPC.immune[Projectile.owner] == 0 || Projectile.maxPenetrate == 1) && (nPC.noTileCollide || !Projectile.ownerHitCheck || ProjectileLoader.CanHitNPC(Projectile, nPC).GetValueOrDefault())) {
                    Rectangle hitbox2 = nPC.Hitbox;
                    bool flag4;
                    if (nPC.type == NPCID.SolarCrawltipedeTail) {
                        int num = 8;
                        hitbox2.X -= num;
                        hitbox2.Y -= num;
                        hitbox2.Width += num * 2;
                        hitbox2.Height += num * 2;
                        flag4 = Projectile.Colliding(hitbox, hitbox2);
                    }
                    else {
                        flag4 = Projectile.Colliding(hitbox, hitbox2);
                    }

                    if (flag4) {
                        modifiers.HitDirectionOverride = player.Center.X < nPC.Center.X ? 1 : -1;
                    }
                }
            }
        }

        public override void CutTiles() {
            float num = 60f;
            float f = Projectile.rotation - MathF.PI / 4f * Math.Sign(Projectile.velocity.X);
            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Projectile.Center + f.ToRotationVector2() * (0f - num), Projectile.Center + f.ToRotationVector2() * num, Projectile.width * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (projHitbox.Intersects(targetHitbox)) {
                return true;
            }

            float f = Projectile.rotation - MathF.PI / 4f * Math.Sign(Projectile.velocity.X);
            float collisionPoint = 0f;
            float num = 110f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center + f.ToRotationVector2() * (0f - num), Projectile.Center + f.ToRotationVector2() * num, 23f * Projectile.scale, ref collisionPoint)
                ? true
                : false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 position = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
            Rectangle value2 = new Rectangle(0, 0, value.Width, value.Height);
            Vector2 origin = value.Size() / 2f;
            SpriteEffects effects = SpriteEffects.None;
            if (Projectile.spriteDirection == -1)
                effects = SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(value, position, value2, lightColor, Projectile.rotation, origin, Projectile.scale, effects);
            return false;
        }
    }
}
