using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids
{
    /// <summary>液体网络中容器对管道暴露的角色,决定管道对本机的输运方向</summary>
    public enum FluidNetRole
    {
        /// <summary>管道(彼此压差均衡)</summary>
        Pipe,
        /// <summary>液源(泵),管道从中抽液</summary>
        Source,
        /// <summary>耗液机(灌注机/瓶装机/岩浆发电机),管道单向灌入</summary>
        Consumer,
        /// <summary>储液罐,按充盈比例差双向流动</summary>
        Storage,
    }

    /// <summary>
    /// 液体容器契约:类型取原版 <see cref="LiquidID"/>,255 单位 = 1 世界格。
    /// <see cref="FluidAmount"/> 为 0 时 <see cref="FluidType"/> 无意义,容器可重绑类型
    /// </summary>
    public interface IFluidContainer
    {
        int FluidType { get; set; }
        int FluidAmount { get; set; }
        int FluidCapacity { get; }
        FluidNetRole FluidRole { get; }
        /// <summary>本容器当前可否接收该类型液体(空容器或同类型且未满)</summary>
        bool CanAcceptFluid(int liquidId);
    }

    /// <summary>液体通用工具:颜色/名称/转移运算/悬停液量条</summary>
    internal static class FluidHelper
    {
        /// <summary>1 世界格的液体单位数,对齐 <see cref="Tile.LiquidAmount"/> 的字节量程</summary>
        public const int UnitsPerTile = 255;

        public static Color GetColor(int liquidId) => liquidId switch {
            LiquidID.Lava => new Color(255, 105, 20),
            LiquidID.Honey => new Color(255, 180, 40),
            LiquidID.Shimmer => new Color(200, 120, 255),
            _ => new Color(60, 130, 230),
        };

        public static string GetName(int liquidId) => liquidId switch {
            LiquidID.Lava => FluidText.LavaName.Value,
            LiquidID.Honey => FluidText.HoneyName.Value,
            LiquidID.Shimmer => FluidText.ShimmerName.Value,
            _ => FluidText.WaterName.Value,
        };

        /// <summary>默认收液判定:空容器或同类型且有余量</summary>
        public static bool DefaultCanAccept(IFluidContainer c, int liquidId) {
            if (c.FluidAmount <= 0) {
                return c.FluidCapacity > 0;
            }
            return c.FluidType == liquidId && c.FluidAmount < c.FluidCapacity;
        }

        /// <summary>向容器注入液体,返回实收量;空容器时重绑类型</summary>
        public static int Give(IFluidContainer c, int liquidId, int amount) {
            if (amount <= 0 || !c.CanAcceptFluid(liquidId)) {
                return 0;
            }
            if (c.FluidAmount <= 0) {
                c.FluidType = liquidId;
            }
            int give = Math.Min(amount, c.FluidCapacity - c.FluidAmount);
            c.FluidAmount += give;
            return give;
        }

        /// <summary>从容器取出液体,返回实取量</summary>
        public static int Take(IFluidContainer c, int amount) {
            if (amount <= 0 || c.FluidAmount <= 0) {
                return 0;
            }
            int take = Math.Min(amount, c.FluidAmount);
            c.FluidAmount -= take;
            return take;
        }

        /// <summary>
        /// 两容器成对压差均衡(镜像 UE 管网的成对形状,严格守恒)。
        /// 类型闸:双方都有液且类型不同则不动;空方从有液方继承类型。
        /// 返回带符号实送量:正=a→b,负=b→a(供流动表现读方向,零副作用)
        /// </summary>
        public static int EqualizePair(IFluidContainer a, IFluidContainer b, int stepLimit) {
            if (a.FluidAmount > 0 && b.FluidAmount > 0 && a.FluidType != b.FluidType) {
                return 0;
            }

            //以充盈比例差定方向,双方容量一致时退化为量差
            float ratioA = a.FluidCapacity > 0 ? a.FluidAmount / (float)a.FluidCapacity : 0f;
            float ratioB = b.FluidCapacity > 0 ? b.FluidAmount / (float)b.FluidCapacity : 0f;

            if (ratioA - ratioB > 0.01f) {
                return MoveFluid(a, b, stepLimit);
            }
            if (ratioB - ratioA > 0.01f) {
                return -MoveFluid(b, a, stepLimit);
            }
            return 0;
        }

        /// <summary>从 from 向 to 输送至多 stepLimit 单位,返回实送量</summary>
        public static int MoveFluid(IFluidContainer from, IFluidContainer to, int stepLimit) {
            if (from.FluidAmount <= 0 || !to.CanAcceptFluid(from.FluidType)) {
                return 0;
            }
            int move = Math.Min(stepLimit, Math.Min(from.FluidAmount, to.FluidCapacity - to.FluidAmount));
            if (move <= 0) {
                return 0;
            }
            if (to.FluidAmount <= 0) {
                to.FluidType = from.FluidType;
            }
            to.FluidAmount += move;
            from.FluidAmount -= move;
            return move;
        }

        /// <summary>
        /// 机器悬停液量条,画在充电条下方一行(yOffset 与 DrawChargeBar 的 20 错开)。
        /// 空容器画灰底;Shift 显示数字与液名
        /// </summary>
        public static void DrawFluidBar(TileProcessor tp, IFluidContainer c, float yOffset = 36) {
            Vector2 drawPos = tp.CenterInWorld + new Vector2(0, tp.Height / 2 + yOffset) - Main.screenPosition;
            Texture2D value = VaultAsset.placeholder2.Value;
            int width = 60;
            int height = 8;
            float ratio = c.FluidCapacity > 0 ? MathHelper.Clamp(c.FluidAmount / (float)c.FluidCapacity, 0f, 1f) : 0f;
            Color fill = c.FluidAmount > 0 ? GetColor(c.FluidType) : new Color(50, 50, 50);

            Main.spriteBatch.Draw(value, drawPos, new Rectangle(0, 0, width + 4, height + 4), Color.Black
                , 0, new Vector2((width + 4) / 2, (height + 4) / 2), 1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(value, drawPos, new Rectangle(0, 0, width, height), new Color(30, 30, 30)
                , 0, new Vector2(width / 2, height / 2), 1f, SpriteEffects.None, 0f);
            if (ratio > 0f) {
                Main.spriteBatch.Draw(value, drawPos - new Vector2(width / 2, height / 2)
                    , new Rectangle(0, 0, (int)(width * ratio), height), fill, 0, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            if (Main.keyState.PressingShift()) {
                string name = c.FluidAmount > 0 ? GetName(c.FluidType) : FluidText.EmptyName.Value;
                string textContent = FluidText.BarFormat.Format(name, c.FluidAmount, c.FluidCapacity);
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(textContent);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, textContent
                    , drawPos.X - textSize.X / 2 + 18, drawPos.Y + 8, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }
        }
    }

    /// <summary>液体系统共用文案(液体名/液量条格式)</summary>
    internal class FluidText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Items";

        internal static LocalizedText WaterName;
        internal static LocalizedText LavaName;
        internal static LocalizedText HoneyName;
        internal static LocalizedText ShimmerName;
        internal static LocalizedText EmptyName;
        internal static LocalizedText BarFormat;

        public override void SetStaticDefaults() {
            WaterName = this.GetLocalization(nameof(WaterName), () => "Water");
            LavaName = this.GetLocalization(nameof(LavaName), () => "Lava");
            HoneyName = this.GetLocalization(nameof(HoneyName), () => "Honey");
            ShimmerName = this.GetLocalization(nameof(ShimmerName), () => "Shimmer");
            EmptyName = this.GetLocalization(nameof(EmptyName), () => "Empty");
            BarFormat = this.GetLocalization(nameof(BarFormat), () => "{0} {1}/{2}");
        }
    }
}
