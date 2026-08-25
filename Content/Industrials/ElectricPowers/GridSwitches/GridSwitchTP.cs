using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches
{
    /// <summary>
    /// 电网总闸TP:嵌进管道线路的电流开关。<br/>
    /// 闭合时依赖基类 <see cref="MachineTP.UpdateConductive"/> 在两侧管道间做压差均衡,
    /// 本机即一段可断开的"导体"(Efficiency=10,对齐管道侧传输速率,不成为瓶颈);<br/>
    /// 断开=基类待机位 <see cref="MachineTP.Disabled"/>:Update 短路后均衡路径物理消失。
    /// 本机不是 BaseUEPipelineTP 也不是 BaseBattery,管道侧连接判定对它 default 直落,
    /// 不建连不喂电,断开状态零泄漏;通断状态随基类包尾/存档键自动同步,本类零序列化代码。<br/>
    /// 右键走 TP 总线(所有端各自翻转+权威端推包),机关线走 HitWire(权威端翻转+推包),两路收敛
    /// </summary>
    internal class GridSwitchTP : MachineTP
    {
        public override int TargetTileID => ModContent.TileType<GridSwitchTile>();
        public override int TargetItem => ModContent.ItemType<GridSwitch>();
        /// <summary>形式容量:管道不识别非电池 TP,基类均衡也不动本机自身电量,恒为空</summary>
        public override float MaxUEValue => 100;

        /// <summary>右键节流时间戳;断开时 UpdateMachine 停摆,不能用 per-tick 递减的冷却</summary>
        private uint lastInteractTime;
        /// <summary>闸刀视觉行程 0=闭合 1=断开;在 Draw 里推进,待机中依旧动画</summary>
        private float leverVisual;

        public override void SetMachine() {
            //过闸吞吐:两侧管道经本机均衡的每刻步长,对齐管道间传输速率上限
            Efficiency = 10;
        }

        /// <summary>TP右键经InnoVault总线在所有端各自执行,推送收敛权威端(镜像伐木者)</summary>
        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (Main.GameUpdateCount - lastInteractTime < 15) {
                return false;
            }
            lastInteractTime = Main.GameUpdateCount;
            Toggle();
            return true;
        }

        /// <summary>
        /// 通断翻转。TP 总线路径:所有端各自执行,推包收敛 !isClient;
        /// HitWire 路径:只在服务器/单人执行(天然权威),同一收敛口径
        /// </summary>
        internal void Toggle() {
            Disabled = !Disabled;
            if (!VaultUtils.isClient) {
                SendData();
            }

            CombatText.NewText(HitBox, GridSwitch.Tint,
                Disabled ? GridSwitch.OpenText.Value : GridSwitch.CloseText.Value);
            SoundEngine.PlaySound(SoundID.Unlock with {
                Volume = 0.7f,
                Pitch = Disabled ? -0.35f : 0.2f
            }, CenterInWorld);

            //拉闸电弧:纯客户端表现
            if (!VaultUtils.isServer) {
                for (int n = 0; n < 10; n++) {
                    Dust dust = Dust.NewDustPerfect(CenterInWorld + Main.rand.NextVector2Circular(10f, 8f),
                        DustID.Electric, VaultUtils.RandVr(2.5f));
                    dust.noGravity = true;
                }
            }
        }

        /// <summary>程序化本体:配电箱+闸刀+端子排;贴图后补,加载零资产</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            //视觉包络放绘制帧推进:断开(待机)状态下 UpdateMachine 停摆,Draw 仍每帧执行
            leverVisual = MathHelper.Lerp(leverVisual, Disabled ? 1f : 0f, 0.15f);

            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToPoint());
            Rectangle src = new(0, 0, 1, 1);
            int x = (int)drawPos.X;
            int y = (int)drawPos.Y;

            //箱体外壳/顶檐/内板
            spriteBatch.Draw(px, new Rectangle(x + 1, y + 2, 30, 30), src, new Color(56, 54, 60).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x, y, 32, 3), src, new Color(70, 68, 76).MultiplyRGB(light));
            spriteBatch.Draw(px, new Rectangle(x + 4, y + 6, 24, 23), src, new Color(30, 29, 34).MultiplyRGB(light));

            //左右端子排:管道接入位的视觉暗示
            Color post = new Color(150, 108, 66).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(x, y + 10, 4, 5), src, post);
            spriteBatch.Draw(px, new Rectangle(x, y + 19, 4, 5), src, post);
            spriteBatch.Draw(px, new Rectangle(x + 28, y + 10, 4, 5), src, post);
            spriteBatch.Draw(px, new Rectangle(x + 28, y + 19, 4, 5), src, post);

            //上触点块
            spriteBatch.Draw(px, new Rectangle(x + 13, y + 7, 6, 4), src, new Color(180, 140, 80).MultiplyRGB(light));

            //闸刀:支点在下,闭合竖直搭上触点,断开甩开约 50 度
            Vector2 pivot = drawPos + new Vector2(16f, 25f);
            float angle = leverVisual * 0.88f;
            Color blade = Color.Lerp(new Color(214, 176, 108), new Color(140, 120, 92), leverVisual).MultiplyRGB(light);
            spriteBatch.Draw(px, pivot, src, blade, angle, new Vector2(0.5f, 1f), new Vector2(3f, 15f), SpriteEffects.None, 0);
            //支点螺帽
            spriteBatch.Draw(px, new Rectangle((int)pivot.X - 2, (int)pivot.Y - 2, 5, 5), src, new Color(90, 86, 94).MultiplyRGB(light));

            //状态灯:通=绿常亮,断=红呼吸
            Color lampColor;
            if (Disabled) {
                float breathe = 0.55f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.3f;
                lampColor = new Color(235, 84, 74) * breathe;
            }
            else {
                lampColor = new Color(110, 220, 130);
            }
            lampColor.A = 255;
            spriteBatch.Draw(px, new Rectangle(x + 24, y + 8, 3, 3), src, lampColor);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            if (!HoverTP) {
                return;
            }
            //本体不持电,悬停只报通断
            string text = Disabled ? GridSwitch.OpenText.Value : GridSwitch.CloseText.Value;
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.7f;
            Vector2 drawPos = CenterInWorld + new Vector2(0, Height / 2 + 14) - Main.screenPosition;
            Utils.DrawBorderString(spriteBatch, text, drawPos - textSize * 0.5f, GridSwitch.Tint, 0.7f);
        }
    }
}
