using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.RemakeItems.Core
{
    //老实说我没想好是否开放这个功能，所以将相关的代码集中到一起，方便决定是否加载到线上版本里面去
    internal class HandlerCanOverride
    {
        /// <summary>
        /// 是否受到修改实例的影响，在<see cref="CWRServerConfig.Instance.ModifiIntercept"/>启用后生效
        /// </summary>
        public static Dictionary<int, bool> CanOverrideByID { get; internal set; } = [];
        /// <summary>
        /// 这个功能是否启用
        /// </summary>
        public static bool CanLoad => false;//在线上版本中设置为false
        #region NetWork
        public static void ResetValueByWorld(int type, Player player) {
            //重新设置一次物品的属性
            foreach (var item in player.inventory) {
                if (item.type != type) {
                    continue;
                }
                item.SetDefaults(type);
            }

            //清理掉可能的手持弹幕
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.hostile || proj.ModProjectile == null || proj.owner != player.whoAmI) {
                    continue;
                }
                if (proj.ModProjectile is BaseHeldProj held) {
                    held.Projectile.Kill();
                    held.Projectile.netUpdate = true;
                }
            }
        }

        public static void SendModifiIntercept(Item handItem, Player player) {
            int type = handItem.type;
            CanOverrideByID[type] = !CanOverrideByID[type];

            SoundEngine.PlaySound(SoundID.DD2_BetsySummon);

            ResetValueByWorld(type, player);

            if (VaultUtils.isClient) {
                if (CanOverrideByID[type]) {
                    VaultUtils.Text(player.name + " Modify item enabled " + handItem.ToString(), Color.Goldenrod);
                }
                else {
                    VaultUtils.Text(player.name + " The modified item was blocked " + handItem.ToString(), Color.Red);
                }

                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.ModifiIntercept_InGame);
                modPacket.Write(type);
                modPacket.Write(CanOverrideByID[type]);
                modPacket.Send();
            }
        }

        public static void NetModifiIntercept_InGame(BinaryReader reader, int whoAmI) {
            int key = reader.ReadInt32();
            bool value = reader.ReadBoolean();

            if (CanOverrideByID.ContainsKey(key)) {
                CanOverrideByID[key] = value;
            }

            ResetValueByWorld(key, Main.LocalPlayer);

            if (VaultUtils.isServer) {
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.ModifiIntercept_InGame);
                modPacket.Write(key);
                modPacket.Write(value);
                modPacket.Write(whoAmI);//在服务端上的whoAmI指向发生改动的玩家索引，所以这里自然要记录一下
                modPacket.Send(-1, whoAmI);
            }
            else {//如果是客户端，则打印来源端的消息
                int fromePlayer = reader.ReadInt32();//这里接收来自服务端记录的来源玩家索引
                if (CanOverrideByID[key]) {
                    VaultUtils.Text(Main.player[fromePlayer].name + " Modify item enabled " + new Item(key).ToString(), Color.Goldenrod);
                }
                else {
                    VaultUtils.Text(Main.player[fromePlayer].name + " The modified item was blocked " + new Item(key).ToString(), Color.Red);
                }
            }
        }

        public static void NetModifiInterceptEnterWorld_Server(BinaryReader reader, int whoAmI) {
            if (!CanLoad && !VaultUtils.isServer) {
                return;
            }
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.ModifiIntercept_EnterWorld_ToClient);
            modPacket.Write(CanOverrideByID.Count);
            foreach (var pair in CanOverrideByID) {
                modPacket.Write(pair.Key);
                modPacket.Write(pair.Value);
            }
            modPacket.Send(whoAmI);
        }

        public static void NetModifiInterceptEnterWorld_Client(BinaryReader reader, int whoAmI) {
            if (!VaultUtils.isClient) {
                return;
            }
            CanOverrideByID = [];
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                CanOverrideByID.Add(reader.ReadInt32(), reader.ReadBoolean());
            }
        }

        public static void ModifiIntercept_OnEnterWorld() {
            if (!CanLoad || !VaultUtils.isClient) {
                return;
            }

            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.ModifiIntercept_EnterWorld_Request);
            modPacket.Send();
        }
        #endregion
    }

    internal class CanOverrideByItemUI : UIHandle
    {
        internal static CanOverrideByItemUI Instance => UIHandleLoader.GetUIHandleOfType<CanOverrideByItemUI>();
        public override bool Active => HandlerCanOverride.CanLoad && ItemOverride.ByID.ContainsKey(player.GetItem().type);
        public bool onDrag;
        public Vector2 dragOffsetPos;
        public override void Update() {
            if (DrawPosition == Vector2.Zero) {
                DrawPosition = new Vector2(Main.screenWidth, Main.screenHeight) / 2;
            }

            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 110, Main.screenWidth - 110);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 110, Main.screenHeight - 110);

            UIHitBox = DrawPosition.GetRectangle(80, 30);
            hoverInMainPage = MouseHitBox.Intersects(UIHitBox);

            if (hoverInMainPage && keyLeftPressState == KeyPressState.Pressed) {
                HandlerCanOverride.SendModifiIntercept(player.GetItem(), player);
            }

            if (hoverInMainPage) {
                if (keyRightPressState == KeyPressState.Held && !onDrag) {
                    if (!onDrag) {
                        dragOffsetPos = DrawPosition - MousePosition;
                    }
                    onDrag = true;
                }
            }

            if (onDrag) {
                player.mouseInterface = true;
                DrawPosition = MousePosition + dragOffsetPos;
                if (keyRightPressState == KeyPressState.Released) {
                    onDrag = false;
                }
            }
        }
        public override void SaveUIData(TagCompound tag) {
            tag["CanOverrideByItemUI_DrawPos_X"] = DrawPosition.X;
            tag["CanOverrideByItemUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("CanOverrideByItemUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = 500;
            }

            if (tag.TryGet("CanOverrideByItemUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
            else {
                DrawPosition.Y = 300;
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 2, UIHitBox, Color.AliceBlue
                , HandlerCanOverride.CanOverrideByID[player.GetItem().type] ? Color.Goldenrod : Color.White * 0.1f);
            VaultUtils.SimpleDrawItem(spriteBatch, player.GetItem().type, DrawPosition + UIHitBox.Size() / 2, 40);
            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, "左键点击切换拦截状态，右键拖动按钮位置"
                        , MousePosition.X + 0, MousePosition.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
            }
        }
    }
}
