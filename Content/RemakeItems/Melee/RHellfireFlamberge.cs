using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RHellfireFlamberge : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<HellfireFlamberge>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<HellfireFlambergeHeld>();
    }

    internal class HellfireFlambergeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<HellfireFlamberge>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "RedSun_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = -20;
            drawTrailBtommWidth = 20;
            drawTrailTopWidth = 40;
            drawTrailCount = 13;
            Length = 62;
            SwingData.starArg = 68;
            SwingData.baseSwingSpeed = 3.5f;
            ShootSpeed = 20;
        }

        public override void Shoot() {
            SoundEngine.PlaySound(SoundID.Item20, Owner.Center);
            Vector2 velocity = ShootVelocity;
            Vector2 position = ShootSpanPos;
            int type = ModContent.ProjectileType<VolcanicFireball>();
            for (int index = 0; index < 3; ++index) {
                float SpeedX = velocity.X + Main.rand.Next(-40, 41) * 0.05f;
                float SpeedY = velocity.Y + Main.rand.Next(-40, 41) * 0.05f;
                float damageMult = 0.5f;

                switch (index) {
                    case 0:
                    case 1:
                        type = ModContent.ProjectileType<VolcanicFireball>();
                        break;
                    case 2:
                        type = ModContent.ProjectileType<VolcanicFireballLarge>();
                        damageMult = 0.75f;
                        break;
                    default:
                        break;
                }
                Projectile.NewProjectile(Source, position.X, position.Y, SpeedX, SpeedY
                    , type, (int)(Projectile.damage * damageMult)
                    , Projectile.knockBack, Owner.whoAmI, 0f, 0f);
            }
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, Main.rand.NextBool(3) ? 16 : 174);
            }
            if (Main.rand.NextBool(5) && Main.netMode != NetmodeID.Server) {
                int smoke = Gore.NewGore(Owner.GetSource_ItemUse(Item), Projectile.position, default, Main.rand.Next(375, 378), 0.75f);
                Main.gore[smoke].behindTiles = true;
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 300);
        }
    }
}
