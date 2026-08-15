using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 忍者斩波：顶点条带刀光(BKSNinjaSlash 着色器，冷白/钢青)，非灰度贴图。<br/>
    /// 三连斩几何各异：0左袭正手月牙 1右袭反手月牙(镜像) 2天袭直线突刺；
    /// 揭开 3 帧内砸满(出生暴烈)，暗侧噪声溶解收尾(消散温和)。<br/>
    /// ai[0]=斩向弧度 ai[1]=连段序号；判定 2~9 帧；服务端生成
    /// </summary>
    internal class BKSNinjaSlashProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxLife = 18;
        /// <summary>揭开帧数：1~3 帧砸满，无减速尾巴</summary>
        private const int SweepFrames = 3;
        private const float SlashLength = 230f;
        private const float SlashHalfWidth = 44f;

        private float SlashDir => Projectile.ai[0];
        private int ComboIndex => (int)Projectile.ai[1];

        /// <summary>已流逝帧(0..MaxLife)</summary>
        private int LifeTime => MaxLife - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Pitch = 0.2f + ComboIndex * 0.12f,
                    Volume = 0.85f,
                    MaxInstances = 4
                }, Projectile.Center);
            }
            Projectile.rotation = SlashDir;

            //爆发帧钢星迸射(命中反馈提前到视觉爆点，而非死亡帧)
            if (LifeTime == SweepFrames && !VaultUtils.isServer) {
                Vector2 dir = SlashDir.ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGoldSpark>(
                        Projectile.Center + dir * Main.rand.NextFloat(30f, 130f),
                        dir.RotatedByRandom(0.5) * Main.rand.NextFloat(3f, 6f),
                        new Color(190, 220, 255), Main.rand.NextFloat(0.6f, 1f))?.Configure(12);
                }
            }
        }

        //斩击判定只在挥出帧(与揭开爆点对齐)
        public override bool? CanDamage() {
            int t = LifeTime;
            return t is >= 2 and <= 9 ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            Vector2 dir = SlashDir.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - dir * SlashLength * 0.2f,
                Projectile.Center + dir * SlashLength * 0.8f,
                SlashHalfWidth * 2f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect fx = EffectLoader.BKSNinjaSlash?.Value;
            Texture2D brush = CWRAsset.SlashBrush01?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (fx == null || brush == null || noise == null) {
                DrawFallback();
                return false;
            }

            int lt = LifeTime;
            //几何按连段分表：0/1 左右夹击月牙(压扁纵横比~0.6，腹面推向目标)，2 天袭直线
            bool line = ComboIndex == 2;
            float flip = ComboIndex == 1 ? -1f : 1f;
            float span = 1.85f;
            float halfX = line ? 150f : ComboIndex == 1 ? 162f : 150f;
            float halfY = line ? 42f : ComboIndex == 1 ? 100f : 94f;
            float thick = line ? 0.44f : 0.34f;

            //生命周期：揭开 3 帧砸满 → 白闪 1~2 帧速落 → 暗侧溶解，锋线最后死
            float sweep = VaultUtils.EaseOutQuad(MathHelper.Clamp(lt / (float)SweepFrames, 0f, 1f));
            float flash = lt < SweepFrames ? 0f : MathF.Pow(0.55f, lt - SweepFrames);
            float erode = MathHelper.Clamp((lt - 7) / 9f, 0f, 1f);
            float cool = MathHelper.Clamp((lt - 4) / 11f, 0f, 1f);
            float front = lt <= SweepFrames + 1 ? 1.25f : MathF.Max(0f, 1.25f - (lt - SweepFrames - 1) * 0.3f);
            float opacity = 1f - MathHelper.Clamp((lt - (MaxLife - 5)) / 5f, 0f, 1f);
            if (opacity <= 0.01f) {
                return false;
            }

            //quad 位姿：月牙带在 quad 本地 +x 轴半径 0.9 处，把腹面推到弹幕中心前方
            Vector2 dir = SlashDir.ToRotationVector2();
            Vector2 center = line
                ? Projectile.Center + dir * 40f
                : Projectile.Center - dir * (halfX * 0.52f);

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            //采样器合同：显式绑 s1/s2(shader 内 register)，参数式绑定禁用
            device.Textures[1] = brush;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.Textures[2] = noise;
            device.SamplerStates[2] = SamplerState.LinearWrap;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uMode"]?.SetValue(line ? 1f : 0f);
            fx.Parameters["uFlip"]?.SetValue(flip);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.173f % 1f + ComboIndex * 0.31f);
            fx.Parameters["uArcSpan"]?.SetValue(span);
            fx.Parameters["uSweep"]?.SetValue(sweep);
            fx.Parameters["uErode"]?.SetValue(erode);
            fx.Parameters["uFlash"]?.SetValue(flash);
            fx.Parameters["uCool"]?.SetValue(cool);
            fx.Parameters["uFrontGlow"]?.SetValue(front);

            //双层异步：钢青主体 → 贴锋线的白热薄芯
            DrawBandLayer(device, fx, center, dir, halfX, halfY, thick, opacity, 0f);
            DrawBandLayer(device, fx, center, dir, halfX, halfY, thick * 0.5f, opacity * 0.92f, 1f);

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
            return false;
        }

        /// <summary>单层 quad 提交，uCore 区分主体/白热芯</summary>
        private static void DrawBandLayer(GraphicsDevice device, Effect fx, Vector2 center, Vector2 dir,
            float halfX, float halfY, float thick, float opacity, float core) {
            fx.Parameters["uThick"]?.SetValue(thick);
            fx.Parameters["uOpacity"]?.SetValue(opacity);
            fx.Parameters["uCore"]?.SetValue(core);

            Vector2 axisX = dir;
            Vector2 axisY = dir.RotatedBy(MathHelper.PiOver2);
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((center - axisX * halfX - axisY * halfY).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((center + axisX * halfX - axisY * halfY).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((center - axisX * halfX + axisY * halfY).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((center + axisX * halfX + axisY * halfY).ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        /// <summary>着色器缺失回退：细冷白线示意斩向，绝不许无形判定</summary>
        private void DrawFallback() {
            Texture2D pixel = InnoVault.VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            int lt = LifeTime;
            float fade = 1f - MathHelper.Clamp((lt - 8) / 8f, 0f, 1f);
            Vector2 dir = SlashDir.ToRotationVector2();
            Vector2 start = Projectile.Center - dir * SlashLength * 0.2f - Main.screenPosition;
            Main.spriteBatch.Draw(pixel, start, null, new Color(210, 235, 255, 0) * (0.8f * fade), SlashDir,
                new Vector2(0f, pixel.Height * 0.5f),
                new Vector2(SlashLength / pixel.Width, 3f / pixel.Height), SpriteEffects.None, 0f);
        }
    }
}
