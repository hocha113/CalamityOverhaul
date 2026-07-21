using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>实体自报隐身，扫描器跳过悬停（WraithSwallow 不查）</summary>
    internal interface IWraithHoverConcealed
    {
        /// <summary>为真时跳过悬停锁定</summary>
        bool HoverConcealed { get; }
    }

    /// <summary>厉鬼目标，可扫不可骇，悬停优先低于 NPC，未现身不锁</summary>
    internal class WraithTargetType : HackTargetType
    {
        /// <summary>悬停可见阈，对照 WraithSwallow.MinStrength</summary>
        private const float MinHoverStrength = 0.35f;

        public override HackTargetKind Kind => HackTargetKind.Wraith;

        public override int HoverPriority => 90;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            const float expandMargin = 16f;
            WraithActor best = null;
            float bestDistSq = float.MaxValue;
            foreach (WraithActor wraith in ActorLoader.GetActiveActors<WraithActor>()) {
                if (wraith.PresenceStrength < MinHoverStrength
                    || wraith is IWraithHoverConcealed { HoverConcealed: true }) {
                    continue;
                }
                Rectangle box = wraith.HitBox;
                box.Inflate((int)expandMargin, (int)expandMargin);
                if (!box.Contains((int)mouseWorld.X, (int)mouseWorld.Y)) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(wraith.Center, mouseWorld);
                if (distSq < bestDistSq) {
                    bestDistSq = distSq;
                    best = wraith;
                }
            }
            return best == null ? null : new WraithScannable(best.WhoAmI, best.Generation);
        }
    }

    /// <summary>悬停/选中厉鬼用 HackWraithHighlight.fx 重绘，拦默认 PreDraw，PostDraw 照常</summary>
    internal sealed class WraithHighlightDraw : GlobalActor
    {
        public override bool PreDraw(SpriteBatch spriteBatch, Actor actor, Color drawColor) {
            if (actor is not WraithActor wraith || (!HackTime.Active && HackTime.Intensity < 0.01f)) {
                return true;
            }
            bool selected = HackTime.CurrentScanTarget is WraithScannable sel && sel.Matches(wraith);
            bool hovered = HackTimeTargeting.HoveredTarget is WraithScannable hov && hov.Matches(wraith);
            if (!selected && !hovered) {
                return true;
            }
            Effect shader = HackTimeAssets.HackWraithHighlight;
            if (shader == null) {
                return true;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / pixel.Width, 1f / pixel.Height));
            shader.Parameters["intensity"]?.SetValue(HackTime.Intensity);
            shader.Parameters["isSelected"]?.SetValue(selected ? 1f : 0f);
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            wraith.DrawBody(spriteBatch, drawColor);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            //拦默认绘制，PostDraw(死机提示)走干净批次
            return false;
        }
    }
}
