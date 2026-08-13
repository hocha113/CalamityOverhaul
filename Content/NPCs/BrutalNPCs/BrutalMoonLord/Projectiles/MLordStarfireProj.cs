using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>星火余留：彗星落点短驻的星焰区，边缘清晰的小型区域封锁</summary>
    internal class MLordStarfireProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int Life = 96;
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 116;
            Projectile.height = 52;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>生命包络：起燃 12f，末段 22f 熄灭</summary>
        private float Envelope {
            get {
                float rise = MathHelper.Clamp(Timer / 12f, 0f, 1f);
                float fall = MathHelper.Clamp((Life - Timer) / 22f, 0f, 1f);
                return Math.Min(rise, fall);
            }
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, MLordDirector.Phantasmal.ToVector3() * 0.7f * Envelope);

            if (VaultUtils.isServer) {
                return;
            }
            //星焰舌：贴地上蹿的星芒粒
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-Projectile.width * 0.45f, Projectile.width * 0.45f),
                    Main.rand.NextFloat(0f, Projectile.height * 0.3f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.2f, 3.2f)),
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.DeepViolet, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.45f, 0.8f) * Envelope)?.Configure(false, Main.rand.Next(16, 26));
            }
        }

        public override bool? CanDamage() => Envelope > 0.55f ? null : false;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float env = Envelope;
            float wobble = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.whoAmI * 2f);
            //横扁焰床
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.DeepViolet with { A = 0 } * (0.7f * env),
                0f, glow.Size() / 2f, new Vector2(0.62f, 0.2f) * wobble, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.Phantasmal with { A = 0 } * (0.85f * env),
                0f, glow.Size() / 2f, new Vector2(0.4f, 0.13f) * wobble, SpriteEffects.None, 0);
            return false;
        }
    }
}
