using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼梦贴地雾：宽扁低雾沿地表缓行，比潮雾（<see cref="PRT_GhostRainMist"/>）更实一档。
    /// 恶犬/玩家/光标是驱散源（<see cref="KikasaDreamFogField"/>）：源内的雾被横推让开、
    /// 透明度向源心让净，离开后自行爬回贴地线聚拢；退梦时全体加速收场。
    /// Masking/Fog 真 alpha，AlphaBlend 直绘，宽低姿态靠小角度旋转+镜像出形变
    /// </summary>
    internal class PRT_KikasaDreamFog : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 260;

        private Color initialColor;
        //贴地基准线（世界 Y），被驱散后向它回落
        private float baseY;
        //横向漫步基速（世界风向定相），驱散推力叠在它上面
        private float wander;
        private float drift;
        private float seed;
        private SpriteEffects mirror;
        //驱散透明度的平滑量，防源边界闪烁
        private float clearFade = 1f;

        public PRT_KikasaDreamFog Configure(int lifetime, float groundY, float wind) {
            Lifetime = lifetime;
            baseY = groundY;
            wander = wind;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            baseY = 0f;
            wander = 0f;
            drift = 0f;
            seed = 0f;
            mirror = SpriteEffects.None;
            clearFade = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            seed = Main.rand.NextFloat(20f);
            drift = Main.rand.NextFloat(-0.003f, 0.003f);
            //贴地雾只许小角度倾侧，宽扁姿态不翻倒；同屏多团的形变靠镜像（Fog 是不对称烟羽）
            Rotation = Main.rand.NextFloat(-0.35f, 0.35f);
            mirror = Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            if (Lifetime <= 0) {
                Lifetime = 150;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //退梦收场：梦侧不再可视时按 4 倍速走完包络，雾不滞留在归返后的真实世界
            if (!KikasaDreamFogField.Active) {
                Time += 3;
            }

            //驱散场：源内横推让开（雾沿地滑，不弹起），透明度向源心让净
            float visTarget = 1f;
            foreach (Vector4 repulsor in KikasaDreamFogField.Repulsors) {
                Vector2 away = Position - new Vector2(repulsor.X, repulsor.Y);
                float dist = away.Length();
                if (dist >= repulsor.Z) {
                    continue;
                }
                float k = 1f - dist / repulsor.Z;
                Vector2 dir = dist > 0.01f ? away / dist : new Vector2(1f, 0f);
                Velocity += new Vector2(dir.X, dir.Y * 0.35f) * (repulsor.W * k);
                //被推开时带一点翻卷，雾是活的
                drift += dir.X * 0.0006f * k;
                float fade = 0.10f + 0.90f * (1f - k) * (1f - k);
                visTarget = MathF.Min(visTarget, fade);
            }
            clearFade = MathHelper.Lerp(clearFade, visTarget, 0.2f);

            //漫步 + 阻尼限速；回落贴地走轻弹簧，带一点呼吸起伏
            Velocity.X = MathHelper.Clamp((Velocity.X + wander * 0.02f) * 0.94f, -2.2f, 2.2f);
            float targetY = baseY - 4f + MathF.Sin(seed + Main.GlobalTimeWrappedHourly * 0.9f) * 3f;
            Velocity.Y = (Velocity.Y + (targetY - Position.Y) * 0.0035f) * 0.90f;

            Rotation += drift;
            drift *= 0.985f;
            Scale += 0.0009f;

            //入出场软包络 × 驱散让位
            float t = LifetimeCompletion;
            float envelope = MathF.Sin(MathHelper.Pi * MathHelper.Clamp(t, 0f, 1f));
            Color = initialColor * (0.42f * envelope * clearFade);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            //宽扁形：横拉纵压，读作贴地雾毯而非烟团
            Vector2 scale = new(Scale * 1.5f, Scale * 0.55f);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                origin, scale, mirror, 0f);
            return false;
        }
    }
}
