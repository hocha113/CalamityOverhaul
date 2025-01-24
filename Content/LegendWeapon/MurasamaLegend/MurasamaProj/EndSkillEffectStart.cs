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
        public const int CanDamageInNPCCountNum = 30;

        public Player player => Main.player[Projectile.owner];

        private ref float Time => ref Projectile.ai[0];

        /// <summary>
        /// 在场的符合条件的NPC是否小于<see cref="CanDamageInNPCCountNum"/>，如果是，返回<see langword="true"/>
        /// </summary>
        /// <returns></returns>
        public static bool CanDealDamageToNPCs() {
            int count = 0;
            foreach (NPC n in Main.npc) {
                if (!n.active || n.friendly) {
                    continue;
                }
                count++;
            }
            return count <= CanDamageInNPCCountNum;
        }

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
                foreach (var player in Main.player) {
                    if (!player.active) {
                        continue;
                    }
                    player.CWR().TimeFrozenTick = 2;
                }
            }

            if (Time == CanDamageTime) {
                if (!CanDealDamageToNPCs() && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), OrigPos, Vector2.Zero
                        , ModContent.ProjectileType<EndSkillMakeDamage>(), Projectile.damage, 0, Projectile.owner);
                }
            }
            Time++;
        }

        public override void OnKill(int timeLeft) {
            if (MurasamaEcType.NameIsVergil(player)) {
                SoundStyle[] sounds = new SoundStyle[] { CWRSound.V_YouSouDiad, CWRSound.V_ThisThePwero, CWRSound.V_You_Wo_Namges_Is_The_Pwero };
                SoundEngine.PlaySound(sounds[Main.rand.Next(sounds.Length)]);
                if (Main.rand.NextBool(13)) {
                    player.QuickSpawnItem(player.FromObjectGetParent(), ModContent.ItemType<FoodStallChair>());
                }
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<PowerSoundEgg>(), 0, 0, Projectile.owner);
            }
        }
    }
}
