using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 弓族弦锚库：21 把蓄力弓 + 幻影弓的静态弦逐图像素实测锚（2026-08-31，
    /// 源 D:\Mod_Resource\ImageResources\原版贴图参照\Item，测量脚本 .vissandbox/s4_measure_bowstrings.py）。<br/>
    /// Top/Bottom = 弦两端锚点（贴图像素坐标）；Cut = DeductDraw 抠除静态弦的矩形；
    /// Inset = 弦列相对贴图中轴的沿轴内缩 px（搭箭点几何用，AI 侧零贴图依赖）。<br/>
    /// 拉弓期抠掉静态弦、只画实测锚动态弦，消双弦伪影（镜像 BarrenBow 范式）
    /// </summary>
    internal static class GsBowStringLib
    {
        private static readonly Dictionary<int, (Vector2 Top, Vector2 Bottom, Rectangle Cut, float Inset)> anchors = new() {
            //木弓组（16×32，弦列 x=2..3）
            [ItemID.WoodenBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.BorealWoodBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.PalmWoodBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.RichMahoganyBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.EbonwoodBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.ShadewoodBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.AshWoodBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 33.5f), new Rectangle(2, 0, 2, 34), 5f),
            //矿弓组（16×32，弦列 x=2..3）
            [ItemID.CopperBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.TinBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.IronBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.LeadBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.SilverBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.TungstenBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.GoldBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            [ItemID.PlatinumBow] = (new Vector2(3f, 0.5f), new Vector2(3f, 31.5f), new Rectangle(2, 0, 2, 32), 5f),
            //特色弓
            [ItemID.DemonBow] = (new Vector2(3f, 2.5f), new Vector2(3f, 37.5f), new Rectangle(2, 2, 2, 36), 6f),
            [ItemID.TendonBow] = (new Vector2(5f, 0.5f), new Vector2(5f, 39.5f), new Rectangle(4, 0, 2, 40), 6f),
            [ItemID.BeesKnees] = (new Vector2(3f, 2.5f), new Vector2(3f, 53.5f), new Rectangle(2, 2, 2, 52), 12f),
            //血雨弓 y=38..41 为弦下渐暗垂滴尾（157→67 红），属余韵装饰：锚与抠除都止于 37
            [ItemID.BloodRainBow] = (new Vector2(7f, 0.5f), new Vector2(7f, 37.5f), new Rectangle(6, 0, 2, 38), 3f),
            [ItemID.MoltenFury] = (new Vector2(3f, 0.5f), new Vector2(3f, 35.5f), new Rectangle(2, 0, 2, 36), 6f),
            [ItemID.HellwingBow] = (new Vector2(1f, 2.5f), new Vector2(1f, 43.5f), new Rectangle(0, 2, 2, 42), 8f),
            //幻影弓（GsPhantasmHeld 同构消费）
            [ItemID.Phantasm] = (new Vector2(9f, 0.5f), new Vector2(9f, 53.5f), new Rectangle(8, 0, 2, 54), 7f),
        };

        internal static bool TryGet(int itemType, out (Vector2 Top, Vector2 Bottom, Rectangle Cut, float Inset) entry)
            => anchors.TryGetValue(itemType, out entry);

        /// <summary>弦列沿轴内缩 px（未录锚回退 5，即 16 宽标准弓量级）</summary>
        internal static float StringInset(int itemType)
            => anchors.TryGetValue(itemType, out var entry) ? entry.Inset : 5f;

        /// <summary>
        /// 持弓距离：标准弓维持既审的 13px，深弓身贴图（蜂弓/狱蝠）按弦内缩前送，
        /// 保证弦静止位距稳心 ≥6px、拉弓有可见行程（镜像 BarrenBow 的按图折算持距）
        /// </summary>
        internal static float HoldDistance(int itemType)
            => MathF.Max(13f, StringInset(itemType) + 6f);

        /// <summary>贴图像素坐标 → 世界坐标（中心原点，含旋转与 DirSign 垂直翻转；两 held 均 scale=1）</summary>
        internal static Vector2 TexPosToWorld(Vector2 drawCenter, float rotation, int dirSign, Vector2 texSize, Vector2 texPos) {
            Vector2 offset = texPos - texSize / 2f;
            if (dirSign < 0) {
                offset.Y = -offset.Y;
            }
            return drawCenter + offset.RotatedBy(rotation);
        }

        /// <summary>
        /// 应用抠弦着色器。hide=true 的 heldProj 只在玩家渲染批里画（LegacyPlayerRenderer 的
        /// SpriteSortMode.Immediate 批），直接 Apply 即可生效，画完必须调 <see cref="RestoreDefaultShader"/>
        /// 复位默认精灵着色器（镜像 PlayerDrawLayers 染料链的复位姿势）
        /// </summary>
        internal static void ApplyDeduct(Rectangle cut, Color drawColor, Vector2 texSize) {
            Effect effect = EffectLoader.DeductDraw.Value;
            effect.Parameters["topLeft"].SetValue(new Vector2(cut.X, cut.Y));
            effect.Parameters["width"].SetValue((float)cut.Width);
            effect.Parameters["height"].SetValue((float)cut.Height);
            effect.Parameters["drawColor"].SetValue(drawColor.ToVector4());
            effect.Parameters["textureSize"].SetValue(texSize);
            effect.CurrentTechnique.Passes[0].Apply();
        }

        internal static void RestoreDefaultShader() => Main.pixelShader.CurrentTechnique.Passes[0].Apply();
    }
}
