using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    internal class EndSkillEffectStart : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public const int CanDamageTime = 140;
        public Player player => Main.player[Projectile.owner];
        private ref float Time => ref Projectile.ai[0];

        public override void SetStaticDefaults() => CWRLoad.ProjValue.ImmuneFrozen[Type] = true;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 200;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool? CanDamage() => false;

        public Vector2 OrigPos {
            get => new Vector2(Projectile.ai[1], Projectile.ai[2]);
            set {
                Projectile.ai[1] = value.X;
                Projectile.ai[2] = value.Y;
            }
        }

        public override void AI() {
            Main.LocalPlayer.CWR().EndSkillEffectStartBool = true;
            Projectile.Center = Main.player[Projectile.owner].Center;

            if (Time < CanDamageTime + 10) {
                CWRWorld.TimeFrozenTick = 2;
            }

            Time++;
        }

        public override void OnKill(int timeLeft) {
            if (MurasamaOverride.NameIsVergil(player)) {
                SoundStyle[] sounds = [CWRSound.V_YouSouDiad, CWRSound.V_ThisThePwero, CWRSound.V_You_Wo_Namges_Is_The_Pwero];
                SoundEngine.PlaySound(sounds[Main.rand.Next(sounds.Length)]);
                if (Main.rand.NextBool(13)) {
                    player.QuickSpawnItem(player.FromObjectGetParent(), ModContent.ItemType<FoodStallChair>());
                }
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<PowerSoundEgg>(), 0, 0, Projectile.owner);
            }
        }
    }
}
