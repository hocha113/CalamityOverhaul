using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>
    /// 主题隐身声明缝：显形强度读不出的"它在但不该被看见"（潜壁未破壁一类），
    /// 实体自报期间扫描器不受理悬停锁定。纯 UI 层门——吞没回执（WraithSwallow）刻意不查它：
    /// 对着墙里隐形位置挥击冒烟+闷响是"墙里有东西"的环境先兆彩蛋，与传闻文案呼应（设计定夺保留）
    /// </summary>
    internal interface IWraithHoverConcealed
    {
        /// <summary>为真时扫描器跳过本实体的悬停锁定</summary>
        bool HoverConcealed { get; }
    }

    /// <summary>
    /// 灵异目标工厂（框架级通用件，覆盖全部厉鬼含调试件）：可扫不可骇，
    /// 科技视角遇鬼必须失效（鬼律 14 的 ERR 范式）。悬停优先级低于 NPC。
    /// 未现身者不受锁定：显形强度低于可见阈（同 WraithSwallow 的 0.35 语义）
    /// 或实体自报主题隐身——扫描不许剧透还没登场的东西
    /// </summary>
    internal class WraithTargetType : HackTargetType
    {
        /// <summary>悬停可见阈：低于此显形强度的虚影不受锁定（对照 WraithSwallow.MinStrength）</summary>
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

    /// <summary>
    /// 骇客时间的灵异高亮层：悬停/选中的厉鬼以 <c>HackWraithHighlight.fx</c> 重绘本体
    /// （紫红撕裂/冷紫魂光）。接管绘制而非叠批：死机提示等 PostDraw 文字不吃着色器
    /// </summary>
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
            //本体已由高亮层画过,拦下默认绘制;PostDraw(死机提示)照常走干净批次
            return false;
        }
    }
}
