using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class FoodStallChair : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "FoodStallChair";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;
            TileID.Sets.HasOutlines[Type] = true;
            TileID.Sets.CanBeSatOnForNPCs[Type] = true; //供 ModifySittingTargetInfo 读取
            TileID.Sets.CanBeSatOnForPlayers[Type] = true; //供 ModifySittingTargetInfo 读取
            TileID.Sets.DisableSmartCursor[Type] = true;
            AddToArray(ref TileID.Sets.RoomNeeds.CountsAsChair);
            AddMapEntry(new Color(200, 200, 200), Language.GetText("MapObject.Chair"));
            AdjTiles = [TileID.Chairs];
            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
            TileObjectData.newTile.CoordinatePaddingFix = new Point16(0, 2);
            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
            TileObjectData.addAlternate(1);
            TileObjectData.addTile(Type);
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) {//距离门限内才允许智能交互
            return settings.player.IsWithinSnappngRangeToTile(i, j, 180);//禁止远距离触发
        }

        public override void ModifySittingTargetInfo(int i, int j, ref TileRestingInfo info) {
            Tile tile = Framing.GetTileSafely(i, j);
            info.ExtraInfo.IsAToilet = true;
            //落座实体可能是任意玩家或 NPC，取乘坐者本身的朝向而不是本地玩家的
            if (info.RestingEntity is not null) {
                info.TargetDirection = info.RestingEntity.direction;
            }
            int xPos = tile.TileFrameX / 18;
            if (xPos == 1) {
                i--;
            }
            if (xPos == 2) {
                i++;
            }

            info.AnchorTilePosition.X = i;
            info.AnchorTilePosition.Y = j;

            if (tile.TileFrameY % 40 == 0) {
                info.AnchorTilePosition.Y++;
            }
        }

        public override bool RightClick(int i, int j) {
            Player player = Main.LocalPlayer;
            if (player.IsWithinSnappngRangeToTile(i, j, 180)) {
                player.GamepadEnableGrappleCooldown();
                player.sitting.SitDown(player, i, j);
            }
            return true;
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;

            if (!player.IsWithinSnappngRangeToTile(i, j, 180)) { //与 RightClick 同距，超距不显示交互
                return;
            }

            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<Items.Placeable.FoodStallChair>();//悬停显示对应物品图标

            if (Main.tile[i, j].TileFrameX / 18 < 1) {
                player.cursorItemIconReversed = true;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0)
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            else if (t.IsHalfBlock)
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            return false;
        }

        /// <summary>
        /// 玩家当前是否坐在大排档塑料椅上。
        /// 与原版 <see cref="PlayerSittingHelper.UpdateSitting"/> 同源：由玩家脚底锚点反查落座物块，
        /// 而不是拿玩家与椅子的距离做模糊判定，多把椅子相邻或旁人路过都不会误触
        /// </summary>
        internal static bool IsSeatedOn(Player player) {
            if (!player.active || player.dead || !player.sitting.isSitting) {
                return false;
            }
            Point seatPoint = (player.Bottom + new Vector2(0f, -2f)).ToTileCoordinates();
            Tile seatTile = Framing.GetTileSafely(seatPoint.X, seatPoint.Y);
            return seatTile.HasTile && seatTile.TileType == ModContent.TileType<FoodStallChair>();
        }
    }

    /// <summary>
    /// 大排档塑料椅彩蛋场景：落座即入魔人雨夜——Bury The Light、暴雨幻象、魔人蓝辉光与低频震感。
    /// 音乐走场景效果优先级体系，与生态、Boss 等其他音乐正常竞争，不再直写 <see cref="Main.newMusic"/>
    /// </summary>
    internal class FoodStallChairScene : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Sounds/Music/BuryTheLight");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override bool IsSceneEffectActive(Player player) => FoodStallChair.IsSeatedOn(player);

        public override void SpecialVisuals(Player player, bool isActive) {
            //天气幻象与光效只属于客户端；服务端改 Main.raining 会变成真实天气广播给所有人
            if (Main.dedServ) {
                return;
            }

            if (isActive) {
                //座上的任何玩家(包括联机中的旁人)都在本地客户端发出脉动的魔人蓝辉光
                float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi * 0.75f);
                Lighting.AddLight(player.Center, new Vector3(0.32f, 0.24f, 0.9f) * (0.7f + pulse * 0.5f));
            }

            //天气幻象与震感只跟随本地玩家自己的落座状态
            if (player.whoAmI != Main.myPlayer) {
                return;
            }

            FoodStallChairAmbience.UpdateIllusion(isActive);

            if (isActive) {
                //低频持续震感走统一屏幕震动通道，遵循 ScreenVibration 配置并自然衰减
                player.CWR().GetScreenShake(0.9f + 0.6f * MathF.Sin(Main.GameUpdateCount * 0.11f));
            }
        }
    }

    /// <summary>
    /// 雨夜幻象的全局单份状态：落座沿记录真实天气，离座恢复。
    /// 仅本地客户端改动 <see cref="Main"/> 天气字段，属纯视觉幻象，不参与网络同步
    /// </summary>
    internal class FoodStallChairAmbience : ModSystem
    {
        private static bool illusionActive;
        private static bool realRaining;
        private static float realMaxRaining;
        private static float realCloudAlpha;
        private static float realWindSpeedTarget;

        internal static void UpdateIllusion(bool seated) {
            if (seated) {
                if (!illusionActive) {
                    //先快照后覆写，绝不把幻象值当真实天气记录下来
                    realRaining = Main.raining;
                    realMaxRaining = Main.maxRaining;
                    realCloudAlpha = Main.cloudAlpha;
                    realWindSpeedTarget = Main.windSpeedTarget;
                    illusionActive = true;
                    //远处滚过一声闷雷，宣告雨夜开场
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.55f, Pitch = -0.55f });
                }
                Main.raining = true;
                Main.maxRaining = 0.99f;
                Main.cloudAlpha = 0.99f;
                Main.windSpeedTarget = 0.8f;
                return;
            }
            RestoreIllusion();
        }

        private static void RestoreIllusion() {
            if (!illusionActive) {
                return;
            }
            illusionActive = false;
            Main.raining = realRaining;
            Main.maxRaining = realMaxRaining;
            Main.cloudAlpha = realCloudAlpha;
            Main.windSpeedTarget = realWindSpeedTarget;
        }

        //存档与退出路径上兜底恢复，避免把幻象暴雨写进世界档或带进下一个世界
        public override void PreSaveAndQuit() => RestoreIllusion();
        public override void OnWorldUnload() => RestoreIllusion();
        public override void OnWorldLoad() => illusionActive = false;
        public override void Unload() => illusionActive = false;
    }
}
