using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Renders
{
    /// <summary>
    /// 帧末绘制状态清扫。全库大量效果把噪声绑进 GraphicsDevice.Textures[1..3] 后不还原，
    /// 残留会让下一帧不自绑槽位的着色器错采样（Boss 贴图偶发花屏一族，反馈十四·#64/五·#63）。
    /// 逐处补还原既碎又漏（现存 30+ 处），这里在每帧绘制收尾统一交还；
    /// 帧内的相邻泄漏仍靠各绘制点自律，新增效果照旧应当用后归还
    /// </summary>
    internal class DrawStateJanitor : ModSystem
    {
        public override void Load() {
            if (!Main.dedServ) {
                Main.OnPostDraw += ClearTextureSlots;
            }
        }

        public override void Unload() {
            if (!Main.dedServ) {
                Main.OnPostDraw -= ClearTextureSlots;
            }
        }

        private static void ClearTextureSlots(GameTime gameTime) {
            var device = Main.instance?.GraphicsDevice;
            if (device == null) {
                return;
            }
            //槽 0 归 SpriteBatch 自管；1~3 是效果侧惯用的噪声/密度位
            device.Textures[1] = null;
            device.Textures[2] = null;
            device.Textures[3] = null;
        }
    }
}
