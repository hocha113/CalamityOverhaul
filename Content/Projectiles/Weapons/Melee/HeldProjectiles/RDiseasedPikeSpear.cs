using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles.BaseProjectiles;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PlagueProj;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RDiseasedPikeSpear : BaseSpearProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "Spears/DiseasedPikeSpear";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<DiseasedPike>();
        Player Owner => CWRUtils.GetPlayerInstance(Projectile.owner);
        Item diseasedPike => Owner.ActiveItem();

        private int Time {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override float InitialSpeed => 3f;
        public override float ReelbackSpeed => 2.4f;
        public override float ForwardSpeed => 0.95f;
        public override Action<Projectile> EffectBeforeReelback => delegate {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity * 0.5f, Projectile.velocity, ModContent.ProjectileType<PlagueBee>(), (int)(Projectile.damage * 0.75f), Projectile.knockBack * 0.85f, Projectile.owner);
        };
        public override void ExtraBehavior() {
            if (Main.rand.NextBool(4))
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, 107, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
        }

        public override void AI() {
            if (Projectile.ai[1] == 0) {
                base.AI();
            }
            if (Projectile.ai[1] == 1) {
                Projectile.velocity = Vector2.Zero;
                if (Owner == null) {
                    Projectile.Kill();
                    return;
                }
                if (diseasedPike == null || diseasedPike?.type != ModContent.ItemType<DiseasedPike>()
                    && diseasedPike.type != ModContent.ItemType<CalamityMod.Items.Weapons.Melee.DiseasedPike>()) {
                    Projectile.Kill();
                    return;
                }//因为需要替换原模组的内容，所以这里放弃了直接访问类型来获取属性，作为补救，禁止其余物品发射该弹幕，即使这种情况不应该出现
                Projectile.localAI[1]++;

                Owner.heldProj = Projectile.whoAmI;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    float frontArmRotation = (MathHelper.PiOver2 - 0.31f) * -Owner.direction;
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);
                    if (PlayerInput.Triggers.Current.MouseRight) Projectile.timeLeft = 2;
                    Owner.direction = Owner.Center.To(Main.MouseWorld).X > 0 ? 1 : -1;
                }

                if (Projectile.ai[2] == 0) {
                    Projectile.Center = Owner.Center;
                    Projectile.rotation += MathHelper.ToRadians(25);

                    if (Projectile.localAI[1] > 60) {
                        diseasedPike.CWR().MeleeCharge = 500;
                        Projectile.ai[2] = 1;
                        Projectile.localAI[1] = 0;
                    }
                }

                if (Projectile.ai[2] == 1) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Vector2 toMous = Owner.Center.To(Main.MouseWorld).UnitVector();
                        Vector2 topos = toMous * 56 + Owner.Center;
                        Projectile.Center = Vector2.Lerp(topos, Projectile.Center, 0.01f);
                        Projectile.rotation = toMous.ToRotation();
                        Vector2 vr = Projectile.rotation.ToRotationVector2();
                        if (Time % 30 == 0) {
                            for (int i = 0; i < 4; i++) {
                                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vr * 3
                                    , ModContent.ProjectileType<PlagueSeeker>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                                Main.projectile[proj].extraUpdates += i;

                                int proj2 = Projectile.NewProjectile(Projectile.GetSource_FromThis()
                                    , Main.MouseWorld + new Vector2(Main.rand.Next(-133, 133), -320), new Vector2(Main.rand.Next(-3, 3), 13)
                                    , ModContent.ProjectileType<PlagueSeeker>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                                Main.projectile[proj2].extraUpdates += i;
                            }
                        }

                        int dustType = 89;
                        int plague = Dust.NewDust(Projectile.Center + vr * 58, 26, 16, dustType, 0, Main.rand.Next(-5, -2), 100, default, 1.6f);
                        Main.dust[plague].noGravity = true;
                    }
                }

                Time++;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Plague>(), 300);
            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 4; i++) {
                    int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity * 0.5f, ModContent.ProjectileType<PlagueSeeker>(), (int)(Projectile.damage * 0.75), Projectile.knockBack, Projectile.owner);
                    Main.projectile[proj].extraUpdates += i;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.ai[1] == 1) {
                Texture2D value = CWRUtils.GetT2DValue(Texture);
                Main.EntitySpriteDraw(
                    value, CWRUtils.WDEpos(Projectile.Center), null, lightColor,
                    Projectile.rotation + MathHelper.PiOver4 + (Owner.direction > 0 ? MathHelper.PiOver2 : MathHelper.Pi)
                    , CWRUtils.GetOrig(value), Projectile.scale, Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
                return false;
            }
            return base.PreDraw(ref lightColor);
        }
    }
}
