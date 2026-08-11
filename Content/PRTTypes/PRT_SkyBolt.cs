using CalamityOverhaul.Common;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 天幕落雷：一根从高空劈到落点的 ThunderTrail 光柱，纯表现无判定。<br/>
    /// 走 PRT 而不走弹幕——落雷视觉每端自绘（权威端结算伤害后各端
    /// 用同一确定性选靶各自生成），粒子天生端本地，正合此用。<br/>
    /// 绘制复用 <see cref="Lightning"/> 同款的 ThunderTrail 管线，
    /// 在 PRT 的 PreDraw 里出图（与弹幕 PreDraw 同为世界层批次）
    /// </summary>
    internal class PRT_SkyBolt : BasePRT
    {
        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> boltTex = null;

        public override int InGame_World_MaxCount => 16;
        public override bool CanPool => false;
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int PathPoints = 12;
        private const float BaseWidth = 34f;

        private ThunderTrail trail;
        private Vector2 strikeTo;
        //生命包络，AI 里推进、绘制函数里采样
        private float envelope = 1f;

        public void Configure(Vector2 from, Vector2 to, int lifetime = 26) {
            Lifetime = lifetime;
            strikeTo = to;
            Position = to;
            Velocity = Vector2.Zero;
            BuildTrail(from, to);
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        //路径的横向抖动是纯装饰，各端不同无妨（pitfalls §9.1：
        //不喂判定的粒子发散可以接受）
        private void BuildTrail(Vector2 from, Vector2 to) {
            if (boltTex == null) return;
            Vector2[] points = new Vector2[PathPoints];
            Vector2 dir = to - from;
            Vector2 side = dir.SafeNormalize(Vector2.UnitY)
                .RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < PathPoints; i++) {
                float t = i / (float)(PathPoints - 1);
                //两端钉死，中段最大摆幅
                float sway = MathF.Sin(t * MathHelper.Pi)
                    * Main.rand.NextFloat(-46f, 46f);
                points[i] = Vector2.Lerp(from, to, t) + side * sway;
            }
            points[^1] = to;

            trail = new ThunderTrail(boltTex, WidthFunc, ColorFunc, AlphaFunc) {
                CanDraw = true,
                UseNonOrAdd = true,
                PartitionPointCount = 2,
                BasePositions = points,
            };
            trail.SetRange((0f, 12f));
            trail.SetExpandWidth(5f);
            trail.RandomThunder();
        }

        public override void AI() {
            //快起慢收：前 20% 满亮，之后三次方衰减
            float t = LifetimeCompletion;
            envelope = t < 0.2f ? 1f : 1f - MathF.Pow((t - 0.2f) / 0.8f, 3f);

            if (trail != null && Time % 3 == 0 && t < 0.55f) {
                trail.RandomThunder();
            }
            Lighting.AddLight(strikeTo, Color.ToVector3() * envelope * 1.2f);
        }

        private float WidthFunc(float factor)
            => BaseWidth * (0.5f + 0.5f * (1f - factor)) * envelope;

        private Color ColorFunc(float factor)
            => Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.35f);

        private float AlphaFunc(float factor)
            => MathHelper.Clamp(envelope * (0.55f + 0.45f * factor), 0f, 1f);

        public override bool PreDraw(SpriteBatch spriteBatch) {
            trail?.DrawThunder(Main.instance.GraphicsDevice);

            //落点辉光，读作雷落在了那里而不是一条线凭空亮起
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Microsoft.Xna.Framework.Color c = Color;
                c.A = 0;
                spriteBatch.Draw(glow, strikeTo - Main.screenPosition, null,
                    c * (envelope * 0.85f), 0f, glow.Size() * 0.5f,
                    new Vector2(0.6f, 0.4f) * envelope, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
