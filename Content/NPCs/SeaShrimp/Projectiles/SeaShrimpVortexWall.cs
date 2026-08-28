using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 封场巨型水龙卷：双渊柱之一，钉死在生成点限制场地宽度（场内即安全声明）。
    /// 40f 生长期无伤（生长即预告），本体持续到阶段转换/死亡清弹或宿主消失。
    /// 绘制复用 FishronTornado.fx 换深渊色板（消费合同同 EverdeepMaelstrom）。
    /// ai[0]=可见高度，ai[1]=宿主 npc.whoAmI；Center=柱底地面点
    /// </summary>
    internal class SeaShrimpVortexWall : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        /// <summary>名义可见宽 px（quad 按 3× 折算，撕裂轮廓留在画布内侧）</summary>
        private const float VisualWidth = 170f;
        private const int GrowFrames = 40;
        private const int FadeFrames = 30;

        private float Height => Projectile.ai[0];
        private int OwnerIndex => (int)Projectile.ai[1];

        /// <summary>本地帧龄（生长包络，迟入端从收包起自演）</summary>
        private int Age => (int)Projectile.localAI[0];
        /// <summary>宿主消失后的淡出计数</summary>
        private int FadeAge => (int)Projectile.localAI[1];

        private float Grow01 => MathHelper.Clamp(Age / (float)GrowFrames, 0f, 1f);
        private float Fade01 => 1f - MathHelper.Clamp(FadeAge / (float)FadeFrames, 0f, 1f);

        public override void SetStaticDefaults() {
            //quad 远宽于命中盒，近出屏不许整柱瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;
        }

        public override void SetDefaults() {
            Projectile.width = 104;
            Projectile.height = 104;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            //迟入场的客户端必须看到封场柱
            Projectile.netImportant = true;
            Projectile.timeLeft = 60 * 180;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.localAI[0]++;
            int owner = OwnerIndex;
            bool ownerAlive = owner >= 0 && owner < Main.maxNPCs && Main.npc[owner].active
                && Main.npc[owner].ModNPC is SeaShrimpBoss;
            if (!ownerAlive) {
                //宿主没了：撤柱淡出，不留孤儿封场
                Projectile.localAI[1]++;
                if (FadeAge >= FadeFrames) {
                    Projectile.Kill();
                }
            }

            float env = Grow01 * Fade01;
            Lighting.AddLight(Projectile.Center - new Vector2(0f, Height * 0.5f * env),
                0.10f * env, 0.22f * env, 0.4f * env);

            if (Main.dedServ || env < 0.2f) {
                return;
            }
            //柱脚踢水 + 柱身上升飞沫（预算克制：常驻物走低频）
            if (Main.GameUpdateCount % 5 == 0) {
                Vector2 foot = Projectile.Center + new Vector2(Main.rand.NextFloat(-VisualWidth * 0.4f, VisualWidth * 0.4f), 0f);
                EverdeepVFX.ShedDroplet(foot,
                    new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(1f, 3f)), 0.9f);
            }
            if (Main.GameUpdateCount % 9 == 0) {
                Vector2 rim = Projectile.Center - new Vector2(
                    Main.rand.NextFloat(-VisualWidth * 0.5f, VisualWidth * 0.5f),
                    Main.rand.NextFloat(0.2f, 0.95f) * Height * env);
                EverdeepVFX.ShedDroplet(rim,
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 1.6f)), 0.7f);
            }
        }

        /// <summary>伤害窗=可见窗：长成 60% 才咬人，淡出期无害</summary>
        public override bool? CanDamage() => Grow01 >= 0.6f && Fade01 > 0.6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //判定芯：窄于可见柱（可见半宽 ~85，判定半宽 52）
            float h = Height * Grow01;
            Rectangle column = new(
                (int)(Projectile.Center.X - SeaShrimpDirector.VortexWallCoreHalfWidth),
                (int)(Projectile.Center.Y - h),
                (int)(SeaShrimpDirector.VortexWallCoreHalfWidth * 2f), (int)h);
            return column.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Grow01 * Fade01;
            if (env <= 0.02f) {
                return false;
            }
            //柱高吃起身缓动，底锚地面——柱从地里长出来，不是凭空淡入
            float riseT = 1f - (1f - Grow01) * (1f - Grow01);
            float drawW = VisualWidth * 3.0f;
            float drawH = Height * 1.30f * MathHelper.Lerp(0.30f, 1f, riseT) * MathHelper.Lerp(0.6f, 1f, Fade01);
            Vector2 bottom = Projectile.Center;
            Vector2 drawCenter = bottom - new Vector2(0f, drawH * 0.5f);

            Effect effect = EffectLoader.FishronTornado?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (effect == null || noiseTex == null || pixel == null) {
                DrawFallback(bottom, drawH, env);
                return false;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            //浓度增益：巨柱要读得厚实（shader 内 saturate 收顶）
            effect.Parameters["uIntensity"]?.SetValue(env * 1.45f);
            effect.Parameters["uGrade"]?.SetValue(1f);
            effect.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.61f);
            effect.Parameters["uDeepColor"]?.SetValue(SeaShrimpVFX.Deep.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(SeaShrimpVFX.Body.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(SeaShrimpVFX.Foam.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            sb.Draw(pixel, drawCenter - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, new Vector2(drawW / pixel.Width, drawH / pixel.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失回退：暗柱+亮芯双竖条（placeholder2 真alpha实体像素）</summary>
        private void DrawFallback(Vector2 bottom, float drawH, float env) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 basePos = bottom - new Vector2(0f, drawH) - Main.screenPosition;
            Main.spriteBatch.Draw(pixel, basePos, src, SeaShrimpVFX.Deep * (0.7f * env), 0f,
                new Vector2(0.5f, 0f), new Vector2(VisualWidth * 0.9f, drawH), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, basePos, src, SeaShrimpVFX.Body * (0.8f * env), 0f,
                new Vector2(0.5f, 0f), new Vector2(VisualWidth * 0.4f, drawH), SpriteEffects.None, 0f);
        }
    }
}
