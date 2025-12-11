using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    internal class MermanRod : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ADV/Abysse/MermanRod";
        public static LocalizedText Text1;
        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "世界中已经放置了一座鱼人钓!");
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.Green;
            Item.createTile = ModContent.TileType<MermanRodTile>();
        }

        public override bool CanUseItem(Player player) {
            if (TileProcessorLoader.TP_ID_To_InWorld_Count.TryGetValue(TPUtils.GetID<MermanRodTP>(), out var num) && num > 0) {
                if (Main.mouseLeftRelease)
                CombatText.NewText(player.getRect(), Color.Cyan, Text1.Value);
                return false;
            }
            return base.CanUseItem(player);
        }
    }

    internal class MermanRodTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ADV/Abysse/MermanRodTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<MermanRod>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 5;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(2, 4);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
            HitSound = SoundID.Dig;
            MineResist = 4f;
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.WoodFurniture);
            return false;
        }
    }

    internal class MermanRodTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<MermanRodTile>();
        public override void Update() {
            //处理老公爵搬家逻辑
            UpdateOldDukeRelocation();
            //处理渔力加成逻辑
            UpdateFishingBuff();
        }

        private void UpdateOldDukeRelocation() {
            //如果营地位置不在这里，则搬家
            if (OldDukeCampsite.CampsitePosition == PosInWorld) {
                return;
            }
            //检查是否在玩家视野内
            bool inView = false;
            foreach (var player in Main.player) {
                if (!player.active) continue;
                //简单的视野检查，如果在屏幕范围内则认为在视野内
                Rectangle screenRect = new Rectangle((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
                //扩大一点范围，避免边缘闪现
                screenRect.Inflate(200, 200);
                if (screenRect.Contains(PosInWorld.ToPoint())) {
                    inView = true;
                    break;
                }
            }
            //如果不在视野内，则进行搬家
            if (!inView) {
                //先清理旧营地
                if (VaultUtils.isServer) {
                    OldDukeCampsite.ClearCampsite();
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.OldDukeCampsiteSync);
                    packet.Write(false);
                    packet.Send();
                }
                else if (VaultUtils.isSinglePlayer) {
                    OldDukeCampsite.ClearCampsite();
                }
                //设置鱼人钓搬家标记
                OldDukeCampsiteDecoration.mermanRod = true;
                //生成新营地
                if (VaultUtils.isServer) {
                    OldDukeCampsite.GenerateCampsite(PosInWorld);
                    //同步给客户端
                    ModPacket packet = CWRMod.Instance.GetPacket();
                    packet.Write((byte)CWRMessageType.OldDukeCampsiteSync);
                    packet.Write(true);
                    packet.WriteVector2(PosInWorld);
                    packet.Send();
                }
                else if (VaultUtils.isSinglePlayer) {
                    OldDukeCampsite.GenerateCampsite(PosInWorld);
                }
            }
        }

        private void UpdateFishingBuff() {
            foreach (var player in Main.player) {
                if (!player.active || player.dead) continue;
                
                //给周围的玩家提供渔力加成
                if (Vector2.Distance(player.Center, PosInWorld) < 600) {
                    player.fishingSkill += 20;
                    //添加一些有趣的钓鱼效果，比如偶尔生成一些粒子
                    if (Main.rand.NextBool(60)) {
                        Dust.NewDust(player.position, player.width, player.height, DustID.Water, 0, -2, 0, default, 1.2f);
                    }
                }
            }
        }

        public override void OnKill() {
            //当被挖掘时，设置搬家标记
            OldDukeCampsite.MermanRodMoveback = true;
            //如果是在多人模式中，先清理旧营地
            if (VaultUtils.isServer) {
                OldDukeCampsite.ClearCampsite();
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.OldDukeCampsiteSync);
                packet.Write(false);
                packet.Send();
            }
        }
    }
}
