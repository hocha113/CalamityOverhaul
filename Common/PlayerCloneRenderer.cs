using Terraria;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 玩家克隆/残影绘制，复用傀儡 <see cref="Player"/>，只画身体
    /// <br/>调用方已 Begin 世界批次；同源多体先 <see cref="Prepare"/> 再多次 <see cref="DrawPrepared"/>
    /// </summary>
    internal class PlayerCloneRenderer : ICWRLoader
    {
        //复用傀儡，Unload 清空防跨重载
        private static Player dummy;

        void ICWRLoader.UnLoadData() => dummy = null;

        /// <summary>拷外观并清效果（同源多体只调一次）</summary>
        public static void Prepare(Player owner) {
            if (owner == null || Main.dedServ) {
                return;
            }
            dummy ??= new Player();
            //陈列体，防 ResetEffects 在 whoAmI==myPlayer 时把全局 tileRange 刷回 5/4
            dummy.isDisplayDollOrInanimate = true;
            //拷外观→清效果
            dummy.CopyVisuals(owner);
            dummy.ResetEffects();
            dummy.skinVariant = owner.skinVariant;
        }

        /// <summary>用已 Prepare 的傀儡画一体，统一染色，不持械</summary>
        public static void DrawPrepared(Vector2 position, Color tint, int direction,
            Rectangle bodyFrame, Rectangle legFrame,
            float fullRotation = 0f, Vector2 fullRotationOrigin = default) {
            if (dummy == null || Main.dedServ) {
                return;
            }

            dummy.position = position;
            dummy.velocity = Vector2.Zero;
            dummy.direction = direction;
            dummy.bodyFrame = bodyFrame;
            dummy.legFrame = legFrame;
            dummy.fullRotation = fullRotation;
            dummy.fullRotationOrigin = fullRotationOrigin;
            dummy.heldProj = -1;
            dummy.itemAnimation = 0;
            dummy.itemTime = 0;

            dummy.skinColor = tint;
            dummy.shirtColor = tint;
            dummy.underShirtColor = tint;
            dummy.pantsColor = tint;
            dummy.shoeColor = tint;
            dummy.hairColor = tint;
            dummy.eyeColor = tint;

            Main.PlayerRenderer.DrawPlayer(Main.Camera, dummy, dummy.position, dummy.fullRotation, dummy.fullRotationOrigin);
        }

        /// <summary>Prepare + DrawPrepared 便捷封装</summary>
        public static void Draw(Player owner, Vector2 position, Color tint, int direction,
            Rectangle bodyFrame, Rectangle legFrame,
            float fullRotation = 0f, Vector2 fullRotationOrigin = default) {
            if (owner == null || Main.dedServ) {
                return;
            }
            Prepare(owner);
            DrawPrepared(position, tint, direction, bodyFrame, legFrame, fullRotation, fullRotationOrigin);
        }

        /// <summary>本色绘制（保留 CopyVisuals 肤色/服色，不统一染色）</summary>
        public static void DrawPreparedNatural(Vector2 position, int direction, float gravDir,
            Rectangle bodyFrame, Rectangle legFrame) {
            if (dummy == null || Main.dedServ) {
                return;
            }

            dummy.position = position;
            dummy.velocity = Vector2.Zero;
            dummy.direction = direction;
            dummy.gravDir = gravDir;
            dummy.bodyFrame = bodyFrame;
            dummy.legFrame = legFrame;
            dummy.fullRotation = 0f;
            dummy.fullRotationOrigin = default;
            dummy.heldProj = -1;
            dummy.itemAnimation = 0;
            dummy.itemTime = 0;

            Main.PlayerRenderer.DrawPlayer(Main.Camera, dummy, dummy.position, 0f, default);
        }
    }
}
