using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霞的演出集中处：滴尾霞光与霞焰余烬，全部端本地纯表现</summary>
    internal static class FuXiaFX
    {
        //霞焰暖底：比 accent 沉一档的燃橙
        private static readonly Color EmberDeep = new(150, 70, 26);

        /// <summary>
        /// 滴尾霞光：霞标滴身后曳一缕暖雾，偶尔迸一粒金星。
        /// 频率压低靠节拍抽签，滴多也不糊屏
        /// </summary>
        internal static void DropGlowTail(Projectile drop, Color accent) {
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(
                    drop.Center - drop.velocity * 0.5f + Main.rand.NextVector2Circular(4f, 4f),
                    -drop.velocity * 0.04f,
                    Color.Lerp(EmberDeep, accent, Main.rand.NextFloat(0.4f, 0.8f)),
                    Main.rand.NextFloat(0.5f, 0.75f) * drop.scale)
                    ?.Configure(Main.rand.Next(16, 26));
            }
            if (Main.rand.NextBool(8)) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    drop.Center - drop.velocity * Main.rand.NextFloat(0.3f, 0.9f),
                    -drop.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Color.Lerp(accent, Color.White, 0.4f), Main.rand.NextFloat(0.2f, 0.32f))
                    ?.Configure(accent * 0.55f, Main.rand.Next(10, 16), 0.08f, 0.7f);
            }
        }

        /// <summary>
        /// 霞焰余烬：按层数在身上钉几粒暖芒，快闪呼吸读作"还在烧"。
        /// SoftGlow 黑底图走 A=0 加色，位置散列钉死不逐帧跳
        /// </summary>
        internal static void DrawEmberFlecks(SpriteBatch spriteBatch, NPC npc,
            int stacks, Vector2 screenPos, Color accent) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || stacks <= 0) {
                return;
            }
            Vector2 origin = glow.Size() * 0.5f;
            int count = Math.Min(stacks, 3);
            int seed = npc.whoAmI * 197 + npc.type;
            for (int i = 0; i < count; i++) {
                float u = KikasaInk.Hash(seed, i * 2 + 11);
                float v = KikasaInk.Hash(seed, i * 2 + 12);
                Vector2 pos = npc.Hitbox.TopLeft() - screenPos + new Vector2(
                    npc.width * (0.2f + 0.6f * u), npc.height * (0.16f + 0.62f * v));
                //火苗式快闪：比霉的慢呼吸急一拍
                float flick = 0.72f + 0.28f * MathF.Sin(
                    Main.GlobalTimeWrappedHourly * 9f + i * 2.6f + seed * 0.21f);
                float size = (10f + 4f * KikasaInk.Hash(seed, i + 30)) * flick * npc.scale;
                spriteBatch.Draw(glow, pos, null, (EmberDeep with { A = 0 }) * (0.55f * flick),
                    0f, origin, size * 2f / glow.Width, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, pos, null, (accent with { A = 0 }) * (0.6f * flick),
                    0f, origin, size / glow.Width, SpriteEffects.None, 0f);
            }
        }
    }
}
