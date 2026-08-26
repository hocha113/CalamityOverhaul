using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenSlime
{
    /// <summary>
    /// 碎晶：水晶被敌人撞碎时朝来敌迸出的锋利残片。
    /// ai[0]=色相种子；出膛短促复合加速，命中或撞地碎裂
    /// </summary>
    internal class RefractionWaltzShardProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override Terraria.Localization.LocalizedText DisplayName => this.GetLocalization("DisplayName", () => "折光碎晶");

        internal const int ShardDamage = 60;

        private float HueSeed => Projectile.ai[0];
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Timer++;
            //出膛复合加速，随后微坠(碎片有重量)
            if (Timer < 9) {
                Projectile.velocity *= 1.055f;
            }
            else {
                Projectile.velocity.Y += 0.09f;
            }
            Projectile.tileCollide = Timer > 6;
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, QueenMotion.PrismHue(HueSeed).ToVector3() * 0.3f);

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDust,
                    -Projectile.velocity * 0.12f, 150, QueenMotion.GetQueenDustColor(), 0.9f);
                d.noGravity = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.HasBuff(ModContent.BuffType<RefractionTag>())) {
                modifiers.FinalDamage *= RefractionTag.DamageTakenMult;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<RefractionTag>(), RefractionTag.TagFrames);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            //碎裂余韵：PRT 残片活过弹体
            if (VaultUtils.isServer) {
                return;
            }
            QueenMotion.CrystalShatterBurst(Projectile.Center, 0.3f, HueSeed, playSound: false);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.6f, MaxInstances = 5 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>速度拉伸光核+晶面星芒+残影链(真 Additive 批，染色带 alpha)</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);

            //残影链
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float fade = (1f - i / (float)Projectile.oldPos.Length) * 0.35f;
                spriteBatch.Draw(glow, ghostPos, null, hue * fade, 0f,
                    glow.Size() / 2f, 0.24f * fade + 0.07f, SpriteEffects.None, 0f);
            }

            //本体：速度拉伸
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.05f, 0f, 0.8f);
            Vector2 bodyScale = new Vector2(0.2f - stretch * 0.06f, 0.2f + stretch * 0.3f);
            float bodyRot = Projectile.rotation - MathHelper.PiOver2;

            spriteBatch.Draw(glow, drawPos, null, hue * 0.9f, bodyRot, glow.Size() / 2f, bodyScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, Color.White * 0.6f, bodyRot, glow.Size() / 2f, bodyScale * 0.48f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, drawPos, null, hue * 0.8f,
                Projectile.rotation + Timer * 0.06f, star.Size() / 2f, 0.26f, SpriteEffects.None, 0f);
        }
    }
}
