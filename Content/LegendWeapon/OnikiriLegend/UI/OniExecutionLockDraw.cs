using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
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

            OniBrush.DrawCornerBracket(spriteBatch, topLeft, Vector2.UnitX, Vector2.UnitY, arm, edge, fade);
            OniBrush.DrawCornerBracket(spriteBatch, topRight, -Vector2.UnitX, Vector2.UnitY, arm, edge, fade);
            OniBrush.DrawCornerBracket(spriteBatch, bottomLeft, Vector2.UnitX, -Vector2.UnitY, arm, edge, fade);
            OniBrush.DrawCornerBracket(spriteBatch, bottomRight, -Vector2.UnitX, -Vector2.UnitY, arm, edge, fade);
        }
    }

    /// <summary>
    /// 里世界肢解落点预览。画出的方框就是点选判定框本身（碰撞箱按体型外扩后的那个），
    /// 玩家据此知道这一刀点得到谁、从哪儿过；目标来自 <see cref="OnikiriPlayer"/>，纯本地
    /// </summary>
    internal sealed class OniDismemberAimDraw : GlobalNPC
    {
        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player.GetModPlayer<OnikiriPlayer>().DismemberPreviewTargetId != npc.whoAmI) {
                return;
            }

            float breath = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f);
            float pad = OniDismember.SelectionPad(npc);
            Rectangle box = npc.Hitbox;
            Vector2 center = npc.Center - screenPos;

            //落刀切线、玩家→目标的那一刀，中段最亮两端化开
            Vector2 aim = npc.Center - player.Center;
            Vector2 dir = aim.LengthSquared() > 1f ? Vector2.Normalize(aim) : Vector2.UnitX;
            float reach = (MathF.Max(box.Width, box.Height) * 0.5f + pad) * 0.95f;
            Color cut = OnikiriUITheme.Seal * (0.34f + breath * 0.18f);
            Color cutFade = OnikiriUITheme.Seal * 0f;
            OniBrush.DrawGradientLine(spriteBatch, center, center + dir * reach, cut, cutFade, 1.8f);
            OniBrush.DrawGradientLine(spriteBatch, center, center - dir * reach, cut, cutFade, 1.8f);

            //判定框四角
            float arm = MathHelper.Clamp(MathF.Min(box.Width, box.Height) * 0.2f + pad * 0.25f, 10f, 28f);
            Vector2 topLeft = new Vector2(box.Left - pad, box.Top - pad) - screenPos;
            Vector2 topRight = new Vector2(box.Right + pad, box.Top - pad) - screenPos;
            Vector2 bottomLeft = new Vector2(box.Left - pad, box.Bottom + pad) - screenPos;
            Vector2 bottomRight = new Vector2(box.Right + pad, box.Bottom + pad) - screenPos;
            Color edge = OnikiriUITheme.Paper * (0.24f + breath * 0.10f);
            Color fade = OnikiriUITheme.Paper * 0.03f;

            OniBrush.DrawCornerBracket(spriteBatch, topLeft, Vector2.UnitX, Vector2.UnitY, arm, edge, fade, 1.2f);
            OniBrush.DrawCornerBracket(spriteBatch, topRight, -Vector2.UnitX, Vector2.UnitY, arm, edge, fade, 1.2f);
            OniBrush.DrawCornerBracket(spriteBatch, bottomLeft, Vector2.UnitX, -Vector2.UnitY, arm, edge, fade, 1.2f);
            OniBrush.DrawCornerBracket(spriteBatch, bottomRight, -Vector2.UnitX, -Vector2.UnitY, arm, edge, fade, 1.2f);
        }
    }
}
