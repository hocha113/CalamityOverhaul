using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class DragButton : CWRUIPanel
    {
        public static DragButton Instance { get; private set; }

        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/HotbarRadial_0");

        public Vector2 supPos => SupertableUI.Instance.DrawPos;

        public Rectangle mainRec;

        public Vector2 InPosOffsetDragToPos;

        public Vector2 DragVelocity;

        public bool onMain;

        public bool onDrag;

        public Vector2 InSupPosOffset => new Vector2(550, 365);

        public override void Load() {
            Instance = this;
        }

        public override void Initialize() {
            DrawPos = SupertableUI.Instance.DrawPos + InSupPosOffset;
            mainRec = new Rectangle((int)DrawPos.X, (int)DrawPos.Y, 48, 48);
            onMain = mainRec.Intersects(new Rectangle((int)MouPos.X, (int)MouPos.Y, 1, 1));
        }

        public override void Update(GameTime gameTime) {
            if (SupertableUI.Instance == null) {
                return;
            }

            Initialize();

            int museS = DownStartL();//获取按钮点击状态

            if (onMain) {
                if (museS == 1 && !onDrag) {//如果玩家刚刚按下鼠标左键，并且此时没有开启拖拽状态
                    onDrag = true;
                    InPosOffsetDragToPos = DrawPos.To(MouPos);//记录此时的偏移向量
                    if (Main.myPlayer == player.whoAmI)
                        SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }

            if (onDrag) {
                if (museS == 2) {
                    onDrag = false;
                }
                DragVelocity = (DrawPos + InPosOffsetDragToPos).To(MouPos);//更新拖拽的速度
                SupertableUI.Instance.DrawPos += DragVelocity;
            }
            else {
                DragVelocity = Vector2.Zero;
            }

            Prevention();
        }

        public void Prevention() {
            if (SupertableUI.Instance.DrawPos.X < 0) {
                SupertableUI.Instance.DrawPos.X = 0;
            }
            if (SupertableUI.Instance.DrawPos.X + SupertableUI.Instance.Texture.Width > Main.screenWidth) {
                SupertableUI.Instance.DrawPos.X = Main.screenWidth - SupertableUI.Instance.Texture.Width;
            }
            if (SupertableUI.Instance.DrawPos.Y < 0) {
                SupertableUI.Instance.DrawPos.Y = 0;
            }
            if (SupertableUI.Instance.DrawPos.Y + SupertableUI.Instance.Texture.Height > Main.screenHeight) {
                SupertableUI.Instance.DrawPos.Y = Main.screenHeight - SupertableUI.Instance.Texture.Height;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (onDrag) {//为了防止拖拽状态下出现位置更新的延迟所导致的果冻感，这里用于在拖拽状态下进行一次额外的更新
                Initialize();
            }
            Texture2D value = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/TexturePackButtons");
            spriteBatch.Draw(Texture, DrawPos, null, Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0);//绘制出UI主体
            Rectangle r1 = new Rectangle(0, 0, 32, 32);
            Rectangle r2 = new Rectangle(32, 0, 32, 32);
            Rectangle r3 = new Rectangle(0, 32, 32, 32);
            Rectangle r4 = new Rectangle(32, 32, 32, 32);
            Color dragColor = Color.Red;
            Color c1 = DragVelocity.Y >= 0 ? Color.White : dragColor;
            Color c2 = DragVelocity.Y <= 0 ? Color.White : dragColor;
            Color c3 = DragVelocity.X >= 0 ? Color.White : dragColor;
            Color c4 = DragVelocity.X <= 0 ? Color.White : dragColor;
            spriteBatch.Draw(value, DrawPos + new Vector2(16.5f, 0), r1, c1, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0);//上
            spriteBatch.Draw(value, DrawPos + new Vector2(16.5f, 32), r2, c2, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0);//下
            spriteBatch.Draw(value, DrawPos + new Vector2(0, 16.5f), r3, c3, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0);//左
            spriteBatch.Draw(value, DrawPos + new Vector2(32, 16.5f), r4, c4, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0);//右
            if (onMain) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, CWRUtils.Translation("左键拖动", "left-drag"), DrawPos.X - 8, DrawPos.Y - 16, Color.BlueViolet, Color.Black, Vector2.Zero, 0.8f);
            }
        }
    }
}
