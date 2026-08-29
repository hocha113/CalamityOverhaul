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
    /// ai[0]=色相种子；出膛短促复合加速，命中或撞地碎裂。
    /// 本体是硬边棱面片(实体批)，柔光只做底衬
    /// </summary>
    internal class RefractionWaltzShardProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override Terraria.Localization.LocalizedText DisplayName => this.GetLocalization("DisplayName", () => "折光碎晶");

        /// <summary>碎晶基伤(挂通用加成，生成时折算)</summary>
        internal const int ShardDamage = 40;

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

        /// <summary>
        /// 本体：硬边棱面碎片(实体批不透明)。三片错角窄面拼出多边形残片，
        /// 面间明暗差读作棱面，白色窄缝读作晶棱高光；速度取向+缓慢翻滚
        /// </summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = pixel.Size() / 2f;
            Color hue = QueenMotion.PrismHue(HueSeed);
            float rot = Projectile.rotation;
            //翻滚：绕行进轴的棱面明暗摆动
            float tumble = (float)Math.Sin(Timer * 0.22f + Projectile.identity);

            Color faceDark = Color.Lerp(hue, Color.Black, 0.5f);
            Color faceMid = Color.Lerp(hue, Color.Black, 0.22f + 0.12f * tumble);
            Color faceLit = Color.Lerp(hue, Color.White, 0.5f - 0.15f * tumble);

            //背衬暗面(最大)→侧面→受光主面，长轴都顺速度
            DrawFacet(pixel, drawPos, faceDark, rot + 0.42f * tumble, origin, new Vector2(15f, 6.4f));
            DrawFacet(pixel, drawPos, faceMid, rot - 0.36f, origin, new Vector2(12.5f, 5f));
            DrawFacet(pixel, drawPos, faceLit, rot + 0.18f, origin, new Vector2(11f, 3.6f));
            //晶棱高光窄缝
            DrawFacet(pixel, drawPos, Color.Lerp(Color.White, hue, 0.2f),
                rot + 0.05f + 0.1f * tumble, origin, new Vector2(9f, 1.2f));
            return false;
        }

        //实体批画窄面片(placeholder2 为纯白quad，sizePx 即目标像素尺寸)
        private static void DrawFacet(Texture2D tex, Vector2 pos, Color color, float rotation,
            Vector2 origin, Vector2 sizePx) {
            Main.EntitySpriteDraw(tex, pos, null, color, rotation, origin,
                new Vector2(sizePx.X / tex.Width, sizePx.Y / tex.Height), SpriteEffects.None, 0);
        }

        /// <summary>残影链+柔光底衬+晶面星芒(真 Additive 批；柔光只做底衬不再当本体)</summary>
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

            //底衬柔光：衬亮棱面，弱于本体
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.05f, 0f, 0.8f);
            Vector2 underScale = new Vector2(0.16f, 0.16f + stretch * 0.2f);
            spriteBatch.Draw(glow, drawPos, null, hue * 0.45f, Projectile.rotation - MathHelper.PiOver2,
                glow.Size() / 2f, underScale, SpriteEffects.None, 0f);
            //晶面星芒
            spriteBatch.Draw(star, drawPos, null, hue * 0.7f,
                Projectile.rotation + Timer * 0.06f, star.Size() / 2f, 0.24f, SpriteEffects.None, 0f);
        }
    }
}
