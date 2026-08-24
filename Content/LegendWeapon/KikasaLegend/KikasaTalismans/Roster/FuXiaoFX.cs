using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霄的演出集中处：高坠速度线与落点雨影预告，全部端本地纯表现</summary>
    internal static class FuXiaoFX
    {
        /// <summary>雨影基准宽（px），比滴的判定略宽一圈读作"落点范围"</summary>
        private const float ShadowWidthPx = 56f;

        /// <summary>雨影刷新节拍（帧）；短于雨影寿命，环环相接读作常驻预告</summary>
        private const int ShadowCadence = 9;

        /// <summary>
        /// 急坠速度线：比霎的更长更密——霄滴从霄位下来，长线是"高"的证词。
        /// 只在坠速起来之后拖线，逐帧短线叠成贯天光轨
        /// </summary>
        internal static void DropSpeedLine(Projectile drop, Color accent) {
            if (drop.velocity.Y < 11f) {
                return;
            }
            PRTLoader.NewParticle<PRT_Line>(
                drop.Center - drop.velocity * Main.rand.NextFloat(0.5f, 1.1f)
                    + Main.rand.NextVector2Circular(5f, 3f),
                drop.velocity * 0.26f,
                Color.Lerp(accent, Color.White, 0.5f) * 0.75f,
                Main.rand.NextFloat(0.5f, 0.8f))?.Configure(false, 12);
        }

        /// <summary>
        /// 落点雨影：按滴的锁定目标/坠落列预测落点，向下探地后压一圈淡影。
        /// 节拍错开逐滴刷新，环寿命长于节拍，读作持续浮现的预告圈；
        /// 临落（距地很近）停发，让位给溅斑
        /// </summary>
        internal static void TickRainShadow(Projectile drop, Color accent) {
            if (((int)Main.GameUpdateCount + drop.whoAmI * 37) % ShadowCadence != 0) {
                return;
            }
            if (!TryPredictLanding(drop, out Vector2 landing)) {
                return;
            }
            //滴已贴近落点：雨影退场，把视线交给落地溅斑
            if (drop.Center.Y > landing.Y - 90f) {
                return;
            }
            PRTLoader.NewParticle<PRT_FuXiaoRainShadow>(landing, Vector2.Zero,
                accent, 1f)?.Configure(16, ShadowWidthPx * drop.scale);
        }

        /// <summary>预测滴的落点：锁定目标取其脚下，无目标取坠落列，向下探实心地表</summary>
        private static bool TryPredictLanding(Projectile drop, out Vector2 landing) {
            landing = default;
            int who = (int)drop.ai[0];
            float x;
            float probeY;
            if (who >= 0 && who < Main.maxNPCs && Main.npc[who]?.active == true) {
                NPC target = Main.npc[who];
                x = target.Center.X + target.velocity.X * 8f;
                probeY = target.Bottom.Y - 8f;
            }
            else {
                //无目标：坠落列 X（生成包 ai[1]），从滴的当前高度向下探
                x = drop.ai[1];
                probeY = drop.Center.Y;
            }
            if (!TryFindGroundBelow(new Vector2(x, probeY), 640f, out float surfaceY)) {
                return false;
            }
            landing = new Vector2(x, surfaceY - 2f);
            return true;
        }

        /// <summary>自探针点向下逐格找实心地表</summary>
        private static bool TryFindGroundBelow(Vector2 probe, float maxDown, out float surfaceY) {
            int x = (int)(probe.X / 16f);
            int startY = (int)(probe.Y / 16f);
            int endY = (int)((probe.Y + maxDown) / 16f);
            for (int y = startY; y <= endY; y++) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    surfaceY = y * 16f;
                    return true;
                }
            }
            surfaceY = 0f;
            return false;
        }
    }

    /// <summary>
    /// 霄·落点雨影：贴地一枚扁暗影圈+靛色细缘，浮现后缓涨轻淡。
    /// 暗体走真透明的 Extra_98（影必须能压暗），缘光 A=0 加色；
    /// 寿命短、按节拍连发，叠成"雨要落在这里"的常驻预告
    /// </summary>
    internal class PRT_FuXiaoRainShadow : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 120;

        private Color accent;
        private float widthPx;

        public PRT_FuXiaoRainShadow Configure(int lifetime, float width) {
            Lifetime = lifetime;
            accent = Color;
            widthPx = width;
            return this;
        }

        public override void Reset() {
            base.Reset();
            accent = default;
            widthPx = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Velocity = Vector2.Zero;
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //浮现-消隐的呼吸包络，中段最实
            Opacity = MathF.Sin(t * MathHelper.Pi) * 0.55f;
            Scale = 0.86f + 0.2f * t;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //Extra_98 可见幅约 0.65 画布，1.5 倍补偿到目标宽
            float w = widthPx * Scale * 1.5f / tex.Width;
            float h = 13f * Scale * 1.5f / tex.Height;

            //暗影体：高空靛压深近黑，读作"影"
            Color shade = new Color(10, 12, 26) * (0.8f * Opacity);
            spriteBatch.Draw(tex, pos, null, shade, 0f, origin,
                new Vector2(w, h), SpriteEffects.None, 0f);
            //靛缘：A=0 加色细环，略宽一圈提示范围
            Color rim = (accent with { A = 0 }) * (0.5f * Opacity);
            spriteBatch.Draw(tex, pos, null, rim, 0f, origin,
                new Vector2(w * 1.18f, h * 0.5f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
