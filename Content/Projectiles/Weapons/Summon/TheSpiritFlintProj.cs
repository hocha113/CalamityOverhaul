using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityOverhaul.Content.Buffs.Summon;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon
{
    internal class TheSpiritFlintProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "TheSpiritFlintProj";

        private Player Owner => Main.player[Projectile.owner];

        private ref float Time => ref Projectile.ai[0];
        public NPC Target { get; set; }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1;
            Projectile.minion = true;
            Projectile.friendly = true;
            Projectile.timeLeft = 660;
            Projectile.tileCollide = false;
        }

        public override bool? CanDamage() => false;

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }

        public override void AI() {
            Target = Owner.Center.MinionHoming(1600, Owner, false || CalamityPlayer.areThereAnyDamnBosses);

            if (Projectile.WithinRange(Owner.Center, 1200) && !Projectile.WithinRange(Owner.Center, 300f)) {
                Projectile.velocity = (Owner.Center - Projectile.Center) / 20f;
            }

            else if (!Projectile.WithinRange(Owner.Center, 160f)) {
                Projectile.velocity = (Projectile.velocity * 37f + Projectile.SafeDirectionTo(Owner.Center) * 17f) / 40f;
            }

            if (!Projectile.WithinRange(Owner.Center, 1200)) {
                Projectile.position = Owner.Center;
                Projectile.velocity *= 0.3f;
            }

            if (Target != null) {
                Projectile.velocity.Y -= MathHelper.Lerp(0, 0.005f, Time % 90);

                Time++;
                if (Time == 90 && Main.myPlayer == Projectile.owner) {
                    Vector2 vr = Projectile.Center.To(Target.Center).UnitVector().RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(5.2f, 7.1f);
                    Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, vr,
                        ProjectileID.WandOfSparkingSpark, Projectile.damage, Projectile.knockBack, Projectile.owner);
                    proj.timeLeft *= 3;
                    proj.DamageType = DamageClass.Summon;
                    proj.MaxUpdates = 2;
                    Time = 0f;
                }
            }

            Owner.AddBuff(ModContent.BuffType<SummonInFlint>(), 3600);
            if (Owner.dead) {
                Owner.CWR().FlintSummonBool = false;
            }
            if (Owner.CWR().FlintSummonBool) {
                Projectile.timeLeft = 2;
            }

            Projectile.MinionAntiClump();
            Projectile.spriteDirection = Target == null ? MathF.Sign(Projectile.velocity.X) : MathF.Sign(Target.Center.X - Projectile.Center.X);

            Projectile.netUpdate = true;
            if (Projectile.netSpam >= 10)
                Projectile.netSpam = 9;
        }
    }
}
