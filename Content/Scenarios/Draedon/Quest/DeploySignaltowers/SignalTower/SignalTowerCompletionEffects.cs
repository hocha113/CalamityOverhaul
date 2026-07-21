using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower
{
    internal static class SignalTowerCompletionEffects
    {
        public static void PlayCompletionEffect(Vector2 worldPosition, int pointIndex) {
            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 1.2f,
                Pitch = 0.3f,
                PitchVariance = 0.1f
            }, worldPosition);

            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);

                Dust dust = Dust.NewDustPerfect(worldPosition, DustID.Electric, velocity, 0, default, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * Main.rand.NextFloat();
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                Dust dust = Dust.NewDustPerfect(worldPosition, DustID.TreasureSparkle, velocity, 0, new Color(100, 220, 255), Main.rand.NextFloat(1f, 2f));
                dust.noGravity = true;
            }

            string completionText = SignalTowerTargetRenderer.TargetCompletedText.Value.Replace("[NUM]", (pointIndex + 1).ToString());
            CombatText.NewText(new Rectangle((int)worldPosition.X - 50, (int)worldPosition.Y - 100, 100, 50),
                new Color(100, 220, 255), completionText, true, false);
        }

        public static void PlayAllCompletionEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            Player player = Main.LocalPlayer;

            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 1.5f,
                Pitch = 0.5f
            }, player.Center);

            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi * i / 50f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);

                Dust dust = Dust.NewDustPerfect(player.Center, DustID.Electric, velocity, 0, default, Main.rand.NextFloat(2f, 3f));
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            CombatText.NewText(new Rectangle((int)player.Center.X - 100, (int)player.Center.Y - 100, 200, 50),
                Color.Gold, SignalTowerTargetRenderer.AllCompletedText.Value, true, true);
        }
    }
}
