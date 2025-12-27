using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    /// <summary>
    /// 村正终结技起始弹幕，负责触发次元斩演出效果
    /// </summary>
    internal class EndSkillEffectStart : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public const int CanDamageTime = 140;
        public Player player => Main.player[Projectile.owner];
        private ref float Time => ref Projectile.ai[0];
        private bool hasSpawnedDimensionSlash = false;
        private bool sound;
        public override void SetStaticDefaults() => CWRLoad.ProjValue.ImmuneFrozen[Type] = true;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 400;//增加时间以配合新演出
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

        private bool spwan;

        public override void AI() {
            if (!spwan) {
                spwan = true;
                //生成次元斩主控弹幕
                if (Projectile.IsOwnedByLocalPlayer() && CWRServerConfig.Instance.MurasamaSpaceFragmentationBool) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center
                        , Vector2.Zero, ModContent.ProjectileType<MuraDimensionSlash>(), 0, 0, Projectile.owner);
                    hasSpawnedDimensionSlash = true;
                }
            }
            Main.LocalPlayer.CWR().EndSkillEffectStartBool = true;

            //如果已经生成了次元斩演出，让次元斩弹幕控制时间冻结
            if (!hasSpawnedDimensionSlash) {
                if (Time < CanDamageTime + 10) {
                    CWRWorld.TimeFrozenTick = 2;
                }
            }

            if (!sound) {
                if (Projectile.owner.TryGetPlayer(out var player) && player.CountProjectilesOfID<MuraExecutionCut>() > 0) {
                    sound = true;
                    SoundEngine.PlaySound(CWRSound.INeedMorePower);
                }
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
