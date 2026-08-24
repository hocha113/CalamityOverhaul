using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霎的演出集中处：开伞爆点与三连滴速度线，全部端本地纯表现</summary>
    internal static class FuShaFX
    {
        /// <summary>
        /// 开伞瞬间：脆响+一圈雨环冲击波+伞面上抽残影+碎银珠环甩。
        /// 由 OnRainStart 各端同拍调用，残影贴着 4 帧急升的伞一路上抽
        /// </summary>
        internal static void RainStartBurst(Projectile umbrella, Color accent) {
            if (Main.dedServ) {
                return;
            }
            //急开伞的脆响：比常规甩墨高一截
            KikasaInk.Play(KikasaInk.InkFlick, umbrella.Center, 0.6f, 0.35f, 2);

            //雨环冲击波：复用刻心者脉冲环，染霎银
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(umbrella.Center, Vector2.Zero,
                accent * 0.55f, 0.1f)?.Configure(0.1f, 0.85f, 13);

            //伞面残影：两道渐淡伞影向上抽离，错帧一慢一快
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FuShaUmbrellaGhost>(umbrella.Center,
                    new Vector2(0f, -(2.6f + 1.8f * i)), accent, 0.8f)
                    ?.Configure(10 + i * 4);
            }

            //上抽速度线：卖"被猛地拽上去"的力
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Line>(
                    umbrella.Center + Main.rand.NextVector2Circular(12f, 5f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(6f, 9f)),
                    Color.Lerp(accent, Color.White, 0.4f) * 0.7f,
                    Main.rand.NextFloat(0.4f, 0.6f))?.Configure(false, 10);
            }

            //碎银珠环甩：开伞把水膜整圈崩出去
            for (int i = 0; i < 8; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 8f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_KikasaInkBead>(umbrella.Center + dir * 10f,
                    dir * Main.rand.NextFloat(2.5f, 4.5f) - Vector2.UnitY * 1.4f,
                    Color.Lerp(accent, Color.White, 0.3f), Main.rand.NextFloat(0.3f, 0.44f))
                    ?.Configure(Main.rand.Next(14, 22));
            }
        }

        /// <summary>三连滴的白色速度线：只在急坠段拖线，逐帧短线叠成连续光轨</summary>
        internal static void DropSpeedLine(Projectile drop, Color accent) {
            if (drop.velocity.Y < 9f) {
                return;
            }
            PRTLoader.NewParticle<PRT_Line>(
                drop.Center - drop.velocity * 0.6f + Main.rand.NextVector2Circular(4f, 2f),
                drop.velocity * 0.22f,
                Color.Lerp(accent, Color.White, 0.55f) * 0.8f,
                Main.rand.NextFloat(0.4f, 0.62f))?.Configure(false, 9);
        }
    }

    /// <summary>
    /// 霎·开伞残影：借鬼伞物品贴图画一层加色虚影，越抽越快、越走越薄，
    /// 纵向渐拉横向收窄，读作"被抽走的伞面"。只在开伞拍出生
    /// </summary>
    internal class PRT_FuShaUmbrellaGhost : BasePRT
    {
        //本体直接采鬼伞物品贴图，此纹理仅供加载器占位
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private Color initialColor;

        public PRT_FuShaUmbrellaGhost Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //上抽：越走越快，越走越薄
            Velocity *= 1.06f;
            Opacity = (1f - LifetimeCompletion) * 0.5f;
            Color = initialColor * Opacity;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            int itemType = ModContent.ItemType<KikasaItem>();
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return false;
            }
            Rectangle frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            float t = LifetimeCompletion;
            Vector2 scale = new Vector2(1f - 0.25f * t, 1f + 0.35f * t) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color, Rotation,
                frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
