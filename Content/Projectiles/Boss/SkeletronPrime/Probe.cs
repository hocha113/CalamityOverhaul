using CalamityMod.Events;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class Probe : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.extraUpdates = 4;
            if (ModGanged.InfernumModeOpenState) {
                Projectile.extraUpdates += 1;
            }
            if (BossRushEvent.BossRushActive || Main.getGoodWorld || Main.zenithWorld) {
                Projectile.extraUpdates += 1;
            }
            Projectile.tileCollide = false;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] < Projectile.ai[1] + 60) {
                Projectile.scale += 0.01f;
                Projectile.velocity *= 0.99f;
            }
            else if (Projectile.ai[0] < Projectile.ai[1] + 320) {
                Projectile.velocity *= 0.96f;
            }
            else {
                if (Projectile.ai[0] == Projectile.ai[1] + 322 && !VaultUtils.isClient) {
                    int type = ModContent.ProjectileType<PrimeCannonOnSpan>();
                    int maxProjSanShootNum = 3;
                    for (int i = 0; i < maxProjSanShootNum; i++) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center
                            , (MathHelper.TwoPi / maxProjSanShootNum * i + Projectile.rotation).ToRotationVector2() * 3
                            , type, Projectile.damage, 0f, Main.myPlayer, -1, -1, 0);
                    }
                }
                if (Projectile.ai[0] == Projectile.ai[1] + 420) {
                    Projectile.Kill();
                }
            }
            Projectile.ai[0]++;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.ai[0] < Projectile.ai[1] + 420 && Projectile.ai[0] > Projectile.ai[1]) {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicWrap
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);
                Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Masking + "DiffusionCircle");
                float sengs = Math.Abs(MathF.Sin(Projectile.ai[0] * 0.04f));
                float slp = sengs * 0.2f + 0.6f;
                Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.Red * (0.3f + sengs)
                , Projectile.rotation, CWRUtils.GetOrig(value), slp, SpriteEffects.FlipHorizontally, 0);
                Main.spriteBatch.ResetBlendState();
            }

            Texture2D mainValue = CWRUtils.GetT2DValue(CWRConstant.NPC + "BTD/Probe");
            if (HeadPrimeAI.DontReform()) {
                Main.instance.LoadNPC(NPCID.Probe);
                mainValue = TextureAssets.Npc[NPCID.Probe].Value;
            }
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, mainValue.Size() / 2, Projectile.scale, SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
