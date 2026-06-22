using Terraria;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 玩家克隆/残影统一绘制工具
    /// <br/>用一个复用的傀儡 <see cref="Player"/> 拷贝外观并清空效果，只绘制身体本体；
    /// 不会把真实玩家的 buff/光环/义体特效等 <c>PlayerDrawLayer</c> 钩子结果重放到克隆体上，
    /// 也完全不改动真实玩家字段（旧的"借用本人绘制"方案会两者都出问题）
    /// <br/>调用方须自行管理 SpriteBatch：处于一个已 Begin 的世界批次中，内部由 <see cref="Main.PlayerRenderer"/> 接管
    /// <br/>同源多体（如斯安威斯坦数十个残影）应先 <see cref="Prepare"/> 一次再多次 <see cref="DrawPrepared"/>，
    /// 避免每体重复 <c>CopyVisuals</c>/<c>ResetEffects</c> 的开销
    /// </summary>
    internal class PlayerCloneRenderer : ICWRLoader
    {
        //复用傀儡，避免每帧 new Player() 的数组分配；卸载时清空防止跨重载持有旧实例
        private static Player dummy;

        void ICWRLoader.UnLoadData() => dummy = null;

        /// <summary>拷贝外观并清空效果，准备好傀儡（同源多体只需调用一次）</summary>
        public static void Prepare(Player owner) {
            if (owner == null || Main.dedServ) {
                return;
            }
            dummy ??= new Player();
            //拷外观→清效果：克隆体保留护甲/染料/发型等外观，但不携带真实玩家的 buff/义体/光环状态
            dummy.CopyVisuals(owner);
            dummy.ResetEffects();
            dummy.skinVariant = owner.skinVariant;
        }

        /// <summary>用已 <see cref="Prepare"/> 的傀儡绘制一个克隆体（仅身体本体，统一染色，不持械、不带特效）</summary>
        /// <param name="position">克隆体左上角世界坐标（与 <see cref="Entity.position"/> 同义）</param>
        /// <param name="tint">整体染色（含 alpha）</param>
        /// <param name="direction">朝向 1 / -1</param>
        /// <param name="bodyFrame">身体动画帧</param>
        /// <param name="legFrame">腿部动画帧</param>
        /// <param name="fullRotation">整体旋转</param>
        /// <param name="fullRotationOrigin">旋转原点</param>
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
            //不画手持物/手持弹幕，残留只保留身体本体
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

        /// <summary>绘制单个干净克隆体（<see cref="Prepare"/> + <see cref="DrawPrepared"/> 的便捷封装）</summary>
        public static void Draw(Player owner, Vector2 position, Color tint, int direction,
            Rectangle bodyFrame, Rectangle legFrame,
            float fullRotation = 0f, Vector2 fullRotationOrigin = default) {
            if (owner == null || Main.dedServ) {
                return;
            }
            Prepare(owner);
            DrawPrepared(position, tint, direction, bodyFrame, legFrame, fullRotation, fullRotationOrigin);
        }
    }
}
