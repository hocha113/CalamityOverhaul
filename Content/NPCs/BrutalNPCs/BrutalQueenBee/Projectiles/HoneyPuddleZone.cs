using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles
{
    /// <summary>
    /// 蜂蜜黏滞洼：区域控制，不造成伤害；本地玩家浸入减速+获得蜂蜜回复(甜蜜的代价)<br/>
    /// ai[0]=宽度px；生成后向下吸附地表
    /// </summary>
    internal class HoneyPuddleZone : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>总存活帧</summary>
        internal const int LifeTime = 540;
        /// <summary>铺开帧</summary>
        private const int SpreadTime = 22;
        /// <summary>收干帧</summary>
        private const int DrainTime = 55;
        /// <summary>洼体高度px</summary>
        private const float ZoneHeight = 44f;

        private float Width => Projectile.ai[0] > 0f ? Projectile.ai[0] : 220f;
        private bool anchored;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.aiStyle = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧向下吸附地表
            if (!anchored) {
                anchored = true;
                Vector2 probe = Projectile.Center;
                for (int i = 0; i < 60; i++) {
                    if (Collision.SolidCollision(probe - new Vector2(4f, 0f), 8, 8)) {
                        break;
                    }
                    probe.Y += 16f;
                }
                Projectile.Center = new Vector2(Projectile.Center.X, probe.Y - 4f);
            }

            //黏滞判定区
            Rectangle zone = GetZoneRect();

            //本地玩家浸入：黏滞减速+蜂蜜回复(区域效果本地判定本地施加)
            Player local = Main.LocalPlayer;
            if (!Main.dedServ && local.active && !local.dead && zone.Intersects(local.Hitbox)) {
                local.AddBuff(BuffID.Honey, 4);
                local.velocity.X *= 0.88f;
                if (local.velocity.Y > 0.5f) {
                    local.velocity.Y *= 0.92f;
                }
                //跳跃被蜜黏住一半
                if (local.velocity.Y < -6.5f) {
                    local.velocity.Y = -6.5f;
                }
            }

            //蜜面缓泡
            if (!VaultUtils.isServer && Main.rand.NextBool(14) && Projectile.timeLeft > DrainTime) {
                Vector2 pos = new Vector2(
                    zone.Left + Main.rand.NextFloat() * zone.Width,
                    zone.Top + Main.rand.NextFloat(6f));
                PRTLoader.NewParticle<PRT_HoneyMist>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.5f),
                    QueenBeeMotion.HoneyGold * 0.35f, Main.rand.NextFloat(0.4f, 0.7f));
            }
            Lighting.AddLight(Projectile.Center, QueenBeeMotion.HoneyGold.ToVector3() * 0.35f);
        }

        private Rectangle GetZoneRect() {
            float spread = SpreadProgress();
            int w = (int)(Width * spread);
            return new Rectangle(
                (int)(Projectile.Center.X - w * 0.5f),
                (int)(Projectile.Center.Y - ZoneHeight * 0.72f),
                w, (int)ZoneHeight);
        }

        private float SpreadProgress() {
            float lived = LifeTime - Projectile.timeLeft;
            return MathHelper.Clamp(lived / SpreadTime, 0f, 1f);
        }

        private float DrainProgress() {
            return MathHelper.Clamp(1f - Projectile.timeLeft / (float)DrainTime, 0f, 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float spread = SpreadProgress();
            float drain = DrainProgress();
            float width = Width * 1.06f;
            float height = ZoneHeight * 1.5f;

            if (EffectLoader.QueenHoneyPool?.Value != null) {
                DrawShaderPool(EffectLoader.QueenHoneyPool.Value, width, height, spread, drain);
                return false;
            }
            DrawSpriteFallback(width, spread, drain);
            return false;
        }

        /// <summary>着色器蜜面(预乘输出走AlphaBlend)</summary>
        private void DrawShaderPool(Effect effect, float width, float height, float spread, float drain) {
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(1f);
            effect.Parameters["uProgress"]?.SetValue(spread);
            effect.Parameters["uDrain"]?.SetValue(drain);
            effect.Parameters["uAspect"]?.SetValue(width / height);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(width / pixel.Width, height / pixel.Height);
            //蜜丘坐在地表线上：quad底缘只留少量没入地面
            Vector2 drawPos = new Vector2(Projectile.Center.X, Projectile.Center.Y - height + 10f) - Main.screenPosition;
            sb.Draw(pixel, drawPos, null, Color.White, 0f,
                new Vector2(pixel.Width * 0.5f, 0f), scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>无着色器回退：双层扁蜜渍</summary>
        private void DrawSpriteFallback(float width, float spread, float drain) {
            Texture2D tex = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            if (tex == null) {
                return;
            }
            float alpha = (1f - drain) * 0.8f;
            Vector2 pos = Projectile.Center - Main.screenPosition - new Vector2(0f, ZoneHeight * 0.4f);
            Vector2 baseScale = new Vector2(width * spread / tex.Width, ZoneHeight * 1.2f / tex.Height);
            Main.EntitySpriteDraw(tex, pos, null, QueenBeeMotion.AmberDeep * alpha, 0f,
                tex.Size() * 0.5f, baseScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos - new Vector2(0f, 4f), null, QueenBeeMotion.HoneyGold * (alpha * 0.55f), 0f,
                tex.Size() * 0.5f, baseScale * new Vector2(0.9f, 0.55f), SpriteEffects.None, 0);
        }
    }
}
