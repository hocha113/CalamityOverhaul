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
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D node = CWRAsset.StarGlow01?.Value;
            if (glow == null || streak == null || node == null) {
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

            //星焰舌：确定性排布的窜升光舌（尖端羽化），高度随相位呼吸
            Vector2 up = new(0f, -1f);
            for (int i = 0; i < 5; i++) {
                float hash = MLordConstellationProj.Hash01(Projectile.whoAmI * 13 + 7, i);
                float x = (i - 2f) * Projectile.width * 0.2f + (hash - 0.5f) * 16f;
                float lick = 0.55f + 0.45f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (9f + hash * 5f) + i * 2.3f);
                float tall = (34f + hash * 40f) * lick * env;
                Vector2 basePos = screenPos + new Vector2(x, Projectile.height * 0.32f);
                float sway = 0.28f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + i * 1.7f + hash * 9f);
                float rot = (up.RotatedBy(sway)).ToRotation();
                Main.EntitySpriteDraw(streak, basePos, null,
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.DeepViolet, hash) with { A = 0 } * (0.6f * env * lick),
                    rot, new Vector2(0f, streak.Height * 0.5f),
                    new Vector2(tall / streak.Width, (10f + hash * 7f) / streak.Height), SpriteEffects.None, 0);
            }

            //焰心星核：判定生效期提示（亮=烫）
            if (env > 0.55f) {
                float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.whoAmI);
                Main.EntitySpriteDraw(node, screenPos + new Vector2(0f, 4f), null,
                    MLordDirector.MoonWhite with { A = 0 } * (0.7f * env * pulse),
                    Main.GlobalTimeWrappedHourly * 2f, node.Size() / 2f, 0.3f * pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
