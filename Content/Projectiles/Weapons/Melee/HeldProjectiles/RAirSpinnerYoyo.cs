using CalamityMod;
using CalamityOverhaul.Content.Items.Melee;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RAirSpinnerYoyo : ModProjectile
    {
        public const int MaxUpdates = 2;

        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<AirSpinnerEcType>();

        public override string Texture => CWRConstant.Cay_Proj_Melee + "Yoyos/" + "AirSpinnerYoyo";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 60f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 320f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 16f;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.aiStyle = 99;
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        private int Time;
        private int Time2;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(Time);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Time = reader.ReadInt32();
        }

        public override void AI() {
            if ((Projectile.position - Main.player[Projectile.owner].position).Length() > 3200f) {
                Projectile.Kill();
            }

            NPC target = Projectile.Center.FindClosestNPC(600);
            int types = ModContent.ProjectileType<Feathers>();
            if (target != null && Time % 30 == 0 && Projectile.IsOwnedByLocalPlayer() && CWRUtils.GetPlayerInstance(Projectile.owner)?.ownedProjectileCounts[types] < 12) {
                Time2++;
                if (Time2 > 5) {
                    for (int i = 0; i < 6; i++) {
                        int proj = Projectile.NewProjectile(
                            Projectile.parent(),
                            Projectile.Center,
                            (MathHelper.TwoPi / 6f * i).ToRotationVector2() * 13,
                            types,
                            Projectile.damage / 2,
                            2,
                            Projectile.owner,
                            2
                            );
                        Main.projectile[proj].localAI[0] = 1;
                        Main.projectile[proj].tileCollide = false;
                    }
                    Time2 = 0;
                }
                else {
                    Projectile.NewProjectile(
                            Projectile.parent(),
                            Projectile.Center,
                            Projectile.Center.To(target.Center).UnitVector() * 15,
                            types,
                            Projectile.damage / 2,
                            0,
                            Projectile.owner
                            );
                }
                Projectile.netUpdate = true;
            }

            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor);
            return false;
        }
    }
}
