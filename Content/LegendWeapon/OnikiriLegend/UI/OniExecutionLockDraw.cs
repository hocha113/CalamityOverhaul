using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>本地满架势锁敌预览；目标身份来自 <see cref="OnikiriPlayer"/>，不参与同步</summary>
    internal sealed class OniExecutionLockDraw : GlobalNPC
    {
        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers
                || Main.LocalPlayer.GetModPlayer<OnikiriPlayer>().ExecutionPreviewTargetId != npc.whoAmI) {
                return;
            }

            float breath = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f);
            float pad = 12f + breath * 3f;
            float arm = MathHelper.Clamp(MathF.Min(npc.width, npc.height) * 0.22f, 10f, 20f);
            Rectangle box = npc.Hitbox;
            Vector2 topLeft = new Vector2(box.Left - pad, box.Top - pad) - screenPos;
            Vector2 topRight = new Vector2(box.Right + pad, box.Top - pad) - screenPos;
            Vector2 bottomLeft = new Vector2(box.Left - pad, box.Bottom + pad) - screenPos;
            Vector2 bottomRight = new Vector2(box.Right + pad, box.Bottom + pad) - screenPos;
            Color edge = Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.Seal, 0.22f)
                * (0.38f + breath * 0.16f);
            Color fade = OnikiriUITheme.Paper * 0.04f;

            DrawCorner(spriteBatch, topLeft, Vector2.UnitX, Vector2.UnitY, arm, edge, fade);
            DrawCorner(spriteBatch, topRight, -Vector2.UnitX, Vector2.UnitY, arm, edge, fade);
            DrawCorner(spriteBatch, bottomLeft, Vector2.UnitX, -Vector2.UnitY, arm, edge, fade);
            DrawCorner(spriteBatch, bottomRight, -Vector2.UnitX, -Vector2.UnitY, arm, edge, fade);
        }

        private static void DrawCorner(SpriteBatch spriteBatch, Vector2 corner, Vector2 horizontal
            , Vector2 vertical, float arm, Color edge, Color fade) {
            OniBrush.DrawGradientLine(spriteBatch, corner, corner + horizontal * arm, edge, fade, 1.4f);
            OniBrush.DrawGradientLine(spriteBatch, corner, corner + vertical * arm, edge, fade, 1.4f);
        }
    }
}
