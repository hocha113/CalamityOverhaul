using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class StarMyriadChangesProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.penetrate = -1;
            Projectile.hostile = true;
            Projectile.friendly = true;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3() * (1 + Math.Abs(MathF.Sin(Projectile.timeLeft * 0.1f) * 3)));
        }

        public override void OnKill(int timeLeft) {
            string langOverContent = CWRLocText.GetTextValue("StarMyriadChanges_TextContent");
            string[] Text = langOverContent.Split("\n");
            if (Text.Length > 0) {
                for (int i = 0; i < 33; i++) {
                    float slp = Main.rand.NextFloat(0.5f, 1.2f);
                    DRKLoader.AddParticle(new StarPulseRing(Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(13, 330)
                        , Vector2.Zero, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(1), HeavenfallLongbow.rainbowColors), 0.05f * slp, 0.8f * slp, 8));
                }
                List<int> rands = CWRUtils.GenerateUniqueNumbers(16, 0, Text.Length - 1);
                for (int i = 0; i < rands.Count; i++) {
                    Vector2 topL = Projectile.Hitbox.TopLeft() + new Vector2(Main.rand.Next(-500, 500), Main.rand.Next(-300, 300));
                    Rectangle rectangle = new Rectangle((int)topL.X, (int)topL.Y, 30, 30);
                    CombatText.NewText(rectangle, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(1), HeavenfallLongbow.rainbowColors), Text[rands[i]], true);
                }
                SoundEngine.PlaySound(SoundID.Lavafall);
                SoundEngine.PlaySound(CWRSound.BlackHole);
            }
        }
    }
}
