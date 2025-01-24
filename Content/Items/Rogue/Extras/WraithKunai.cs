using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ID.ContentSamples.CreativeHelper;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class WraithKunai : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "WraithKunai";
        public override void SetDefaults() {
            Item.CloneDefaults(ModContent.ItemType<LunarKunai>());
            Item.damage = 160;
            Item.UseSound = null;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.shoot = ModContent.ProjectileType<WraithKunaiThrowable>();
            Item.CWR().GetMeleePrefix = Item.CWR().GetRangedPrefix = true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CalamityResearchSorting.RogueWeapon;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class WraithKunaiProj : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "WraithKunai";
        private const int Inder1 = 45;
        private const int Inder2 = 80;
        private Vector2 origPos;
        private Vector2 origVer;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.timeLeft = 300;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.extraUpdates = 4;
        }

        public override void AI() {
            if (Projectile.ai[2] > 0) {
                Projectile.extraUpdates = 0;
                if (Projectile.ai[0] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                    origPos = Projectile.Center + CWRUtils.randVr(132, 660);
                    Projectile.netUpdate = true;
                }

                Projectile.ai[0]++;

                if (Projectile.ai[0] == Inder2) {
                    Projectile.extraUpdates = 2;
                }

                if (Projectile.ai[0] >= Inder2) {
                    if (Projectile.Center.FindClosestNPC(1300f) != null) {
                        CalamityUtils.HomeInOnNPC(Projectile, !Projectile.tileCollide, 1300f, 12f, 20f);
                        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                    }
                }
                else if (Projectile.ai[0] > Inder1) {
                    origVer = Projectile.Center.DirectionTo(Main.player[Projectile.owner].Center) * 45;
                    NPC target = Projectile.Center.FindClosestNPC(1600);
                    if (target != null) {
                        origVer = Projectile.Center.DirectionTo(target.Center) * 45;
                    }
                    Projectile.rotation = origVer.ToRotation() + MathHelper.PiOver2;
                }
                else if (Projectile.ai[0] == Inder1) {
                    Projectile.velocity = Vector2.Zero;
                    if (Main.myPlayer == Projectile.owner) {
                        origVer = Projectile.Center.DirectionTo(Main.player[Projectile.owner].Center) * 45;
                        Projectile.netUpdate = true;
                    }
                }
                else if (Projectile.ai[0] < Inder1) {
                    AdjustPosition(origPos, 15, 1);
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                }
            }
            else {
                CalamityUtils.HomeInOnNPC(Projectile, !Projectile.tileCollide, 300f, 12f, 20f);
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());
        }

        private void AdjustPosition(Vector2 destination, float maxSpeed, float increment = 1f) {
            float deltaX = destination.X - Projectile.Center.X;
            float deltaY = destination.Y - Projectile.Center.Y;

            // 调整水平速度
            if (Projectile.Center.X < destination.X && Projectile.velocity.X < maxSpeed) {
                Projectile.velocity.X = Math.Min(Projectile.velocity.X + increment, deltaX);
            }
            else if (Projectile.Center.X > destination.X && Projectile.velocity.X > -maxSpeed) {
                Projectile.velocity.X = Math.Max(Projectile.velocity.X - increment, deltaX);
            }

            // 调整垂直速度
            if (Projectile.Center.Y < destination.Y && Projectile.velocity.Y < maxSpeed) {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + increment, deltaY);
            }
            else if (Projectile.Center.Y > destination.Y && Projectile.velocity.Y > -maxSpeed) {
                Projectile.velocity.Y = Math.Max(Projectile.velocity.Y - increment, deltaY);
            }
        }

        public override bool? CanHitNPC(NPC target) {
            if (Projectile.ai[2] > 0 && Projectile.ai[0] < Inder2) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dayTime) {
                target.AddBuff(BuffID.OnFire, 300);
            }
            else {
                target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.ai[2] > 0) {
                Projectile.damage /= 2;
                Projectile.Explode(300);
                for (int i = 0; i < 6; i++) {
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                }

                for (int i = 0; i < 66; i++) {
                    Vector2 pos = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.Next(-200, 200) + Projectile.Center;
                    int num = Dust.NewDust(pos, 1, 1, DustID.RedTorch, 0f, 0f, 0, default, 2.5f);
                    Main.dust[num].noGravity = true;
                    Main.dust[num].velocity *= 3f;
                    num = Dust.NewDust(pos, 2, 2, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                    Main.dust[num].velocity *= 2f;
                    Main.dust[num].noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }

    internal class WraithKunaiThrowable : BaseThrowable
    {
        public override string Texture => CWRConstant.Item_Rogue + "WraithKunai";
        public override void SetThrowable() {
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            HandOnTwringMode = -36;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            OffsetRoting = MathHelper.PiOver4;
        }

        public override void ThrowOut() {
            if (stealthStrike) {
                for (int i = 0; i < 16; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, UnitToMouseV.RotatedByRandom(0.3f) * 8
                        , ModContent.ProjectileType<WraithKunaiProj>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0, 0, 1);
                }
            }
            else {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, UnitToMouseV.RotatedByRandom(0.2f) * 8
                        , ModContent.ProjectileType<WraithKunaiProj>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }
            SoundEngine.PlaySound(SoundID.Item39, Owner.Center);
            Projectile.soundDelay = 10;
            Projectile.Kill();
        }
    }
}
