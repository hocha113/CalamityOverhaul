using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Projectiles.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class GuardianTerra : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "GuardianTerra";
        public override void SetDefaults() {
            Item.width = 62;
            Item.height = 76;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(gold: 15);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 25;
            Item.useTime = 25;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.damage = 227;
            Item.knockBack = 6;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.channel = true;
            Item.shootSpeed = 5f;
            Item.shoot = ModContent.ProjectileType<GuardianTerraHeld>();
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit += 16;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }

        public override bool? UseItem(Player player) {
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.shootSpeed = 15f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noUseGraphic = false;
            Item.noMelee = false;
            Item.UseSound = SoundID.Item60 with { Pitch = 0.2f, MaxInstances = 2 };
            if (player.altFunctionUse == 2) {
                Item.useAnimation = 25;
                Item.useTime = 25;
                Item.shootSpeed = 5f;
                Item.UseSound = SoundID.Item1;
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.noUseGraphic = true;
                Item.noMelee = true;
            }
            return base.UseItem(player);
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) {
            player.itemLocation = player.GetPlayerStabilityCenter();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Item.initialize();
                Item.CWR().ai[0]++;
                Projectile.NewProjectile(source, position, velocity, type, damage * 3, knockback, player.whoAmI, Item.CWR().ai[0] % 2 == 0 ? 1 : 0);
                return false;
            }
            for (int i = 0; i < Main.rand.Next(2, 4); i++) {
                Projectile.NewProjectile(source, position + velocity * 2, velocity.RotatedByRandom(0.45f)
                , ModContent.ProjectileType<GuardianTerraBeam>(), damage, knockback, player.whoAmI);
            }
            return false;
        }
    }

    internal class GuardianTerraBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 160;
        }

        public override void AI() {
            if (Projectile.timeLeft < 122) {
                NPC target = Projectile.Center.FindClosestNPC(900);
                if (target != null) {
                    Vector2 idealVelocity = Projectile.SafeDirectionTo(target.Center) * (Projectile.velocity.Length() + 6.5f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, idealVelocity, 0.08f);
                }
            }
            
            if (++Projectile.ai[0] >= 2 && Projectile.Distance(Main.LocalPlayer.Center) < 1200) {
                for (int i = 0; i < 3; i++) {
                    Color color = Color.Blue;
                    int id = DustID.FireworkFountain_Blue;
                    if (i == 1) {
                        color = Color.Green;
                        id = DustID.FireworkFountain_Green;
                    }
                    else if (i == 2) {
                        color = Color.Yellow;
                        id = DustID.FireworkFountain_Yellow;
                    }
                    if (Main.rand.NextBool(6)) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + CWRUtils.randVr(6), id
                        , Projectile.velocity, 56, Main.DiscoColor, Main.rand.NextFloat(0.6f, 1.6f));
                        dust.noGravity = true;
                        dust.color = color;
                    }
                    if (Projectile.ai[1] > 6) {
                        CWRParticle spark = new GuardianTerraStar(Projectile.Center
                            , Projectile.velocity / 10, false, 12, Main.rand.NextFloat(1.2f, 2.3f), color);
                        CWRParticleHandler.AddParticle(spark);
                    }
                }
                Projectile.ai[0] = 0;
            }
            Projectile.ai[1]++;
        }
    }

    internal class GuardianTerraHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "GuardianTerra";
        public float[] oldrot = new float[7];
        private Vector2 startVector;
        private Vector2 vector;
        public ref float Length => ref Projectile.localAI[0];
        public ref float Rot => ref Projectile.localAI[1];
        public float Timer;
        private float speed;
        private float SwingSpeed;
        private float glow;
        private bool lifeDrained;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }
        public override bool ShouldUpdatePosition() => false;
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.width = 46;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Rot = MathHelper.ToRadians(2);
            Length = 152;
        }

        public float SetSwingSpeed(float speed) => speed / Owner.GetAttackSpeed(DamageClass.Melee);

        public static Vector2 PolarVector(float radius, float theta) => new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * radius;

        public void InOnwer() {
            if (Projectile.ai[0] == 0) {
                if (Timer++ == 0) {
                    speed = MathHelper.ToRadians(1);
                    startVector = PolarVector(1, Projectile.velocity.ToRotation() - ((MathHelper.PiOver2 + 0.6f) * Projectile.spriteDirection));
                    vector = startVector * Length;
                    SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                }
                if (Timer < 6 * SwingSpeed) {
                    Rot += speed / SwingSpeed * Projectile.spriteDirection;
                    speed += 0.15f;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                else {
                    Rot += speed / SwingSpeed * Projectile.spriteDirection;
                    speed *= 0.7f;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                if (Timer >= 25 * SwingSpeed) {
                    Projectile.Kill();
                }
            }
            else if (Projectile.ai[0] == 1) {
                if (Timer++ == 0) {
                    speed = MathHelper.ToRadians(1);
                    Projectile.velocity = PolarVector(5, (Main.MouseWorld - Owner.Center).ToRotation());
                    startVector = PolarVector(1, (Main.MouseWorld - Owner.Center).ToRotation() + ((MathHelper.PiOver2 + 0.6f) * Owner.direction));
                    vector = startVector * Length;
                    SoundEngine.PlaySound(SoundID.Item71, Projectile.position);
                }
                if (Timer < 6 * SwingSpeed) {
                    Rot -= speed / SwingSpeed * Projectile.spriteDirection;
                    speed += 0.15f;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                else {
                    Rot -= speed / SwingSpeed * Projectile.spriteDirection;
                    speed *= 0.7f;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                if (Timer >= 25 * SwingSpeed) {
                    Projectile.Kill();
                }
            }
        }

        public override void AI() {
            if (Owner.noItems || Owner.CCed || Owner.dead || !Owner.active) {
                Projectile.Kill();
            }
            SetHeld();
            SwingSpeed = SetSwingSpeed(1.2f);
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            Projectile.spriteDirection = Owner.direction;
            if (Projectile.ai[0] < 2) {
                if (Projectile.spriteDirection == 1)
                    Projectile.rotation = (Projectile.Center - Owner.Center).ToRotation() + MathHelper.PiOver4;
                else
                    Projectile.rotation = (Projectile.Center - Owner.Center).ToRotation() - MathHelper.Pi - MathHelper.PiOver4;
                glow += 0.03f;
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, (Owner.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2);
            if (Projectile.IsOwnedByLocalPlayer()) {
                InOnwer();
            }
            if (Timer > 1) {
                Projectile.alpha = 0;
            }
            Projectile.scale = 1.5f;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + vector;
            for (int k = Projectile.oldPos.Length - 1; k > 0; k--) {
                oldrot[k] = oldrot[k - 1];
            }                
            oldrot[0] = Projectile.rotation;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.ActiveItem().type == ModContent.ItemType<GuardianTerra>() && Projectile.numHits == 0) {
                int proj = Projectile.NewProjectile(new EntitySource_ItemUse(Owner, Owner.ActiveItem()), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TerratomereSlashCreator>(),
                Projectile.damage, 0, Projectile.owner, target.whoAmI, Main.rand.NextFloat(MathHelper.TwoPi));
                Main.projectile[proj].timeLeft = 130;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteEffects spriteEffects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = new(texture.Width / 2f, texture.Height / 2f);
            Vector2 trialOrigin = new(texture.Width / 2f - 36, Projectile.height / 2f);
            int shader = ContentSamples.CommonlyUsedContentSamples.ColorOnlyShaderIndex;
            Vector2 tovmgs = PolarVector(20, (Projectile.Center - Owner.Center).ToRotation());

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            GameShaders.Armor.ApplySecondary(shader, Owner, null);

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - tovmgs - Main.screenPosition + trialOrigin + new Vector2(0f, Projectile.gfxOffY);
                Color color = Color.LimeGreen * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, color * Projectile.Opacity * glow, oldrot[k], origin, Projectile.scale, spriteEffects, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, Projectile.Center - tovmgs - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY, null
                , Projectile.GetAlpha(CWRUtils.MultiStepColorLerp(0.6f, Color.White, lightColor)), Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);
            return false;
        }
    }
}
