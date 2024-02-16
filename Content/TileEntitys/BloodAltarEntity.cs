using CalamityMod;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Tiles;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileEntitys
{
    internal class BloodAltarEntity : ModTileEntity
    {
        public LocalizedText Text1 { get; private set; }

        public Vector2 Center => Position.ToWorldCoordinates(8 * BloodAltar.Width, 8 * BloodAltar.Height);
        public bool OnBoolMoon;
        public bool OldOnBoolMoon;
        public long Time = 0;
        public int frameIndex = 1;
        public float rot;
        public float drawGstPos;

        // 该代码仅在服务器上调用。这是对 MessageID.TileEntityPlacement 的服务端响应
        // 当服务器从客户端收到这样的消息时，它会向所有客户端发送 MessageID.TileEntitySharing
        // 这将导致它们在本地放置瓦片实体在该位置，所有的实体都具有完全相同的ID
        public override void OnNetPlace() => NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);

        public override bool IsTileValidForEntity(int x, int y) {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == ModContent.TileType<BloodAltar>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
        }

        private void SpanDustEfset() {
            if (frameIndex == 3) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vr = new Vector2(Main.rand.Next(-8, 8), Main.rand.Next(-5, -2));
                    Dust.NewDust(Center - new Vector2(16, 16), 32, 32, DustID.Blood, vr.X, vr.Y
                        , Scale: Main.rand.NextFloat(0.7f, 1.3f));
                }
                LightParticle particle = new LightParticle(Center + Main.rand.NextVector2Unit() * Main.rand.Next(0, 15)
                    , new Vector2(0, -1), 0.3f, Color.DarkGreen, 165);
                CWRParticleHandler.AddParticle(particle);
            }
        }

        private void SommonLose(Player player) {
            CWRUtils.Text(CWRLocalizationText.GetTextValue("BloodAltar_Text2"), Color.DarkRed);
            if (player != null) {
                PlayerDeathReason pd = PlayerDeathReason.ByCustomReason(player.name + CWRLocalizationText.GetTextValue("BloodAltar_Text3"));
                player.Hurt(pd, player.statLifeMax2 / 10, 0);
            }
            OldOnBoolMoon = OnBoolMoon = false;
        }

        private bool UseInPlayerBloodOrb(Player player) {
            int maxUseOrbNum = 50;
            int orbNum = 0;

            if (player.whoAmI != Main.myPlayer) {
                return false;
            }

            foreach (Item orb in player.inventory) {
                if (orb.type == ModContent.ItemType<BloodOrb>()) {
                    orbNum += orb.stack;
                }
            }
            if (orbNum < maxUseOrbNum) {
                SommonLose(player);
                return false;
            }
            else {
                foreach (Item orb in player.inventory) {
                    if (orb.type == ModContent.ItemType<BloodOrb>()) {
                        if (orb.stack >= maxUseOrbNum) {
                            orb.stack -= maxUseOrbNum;
                            if (orb.stack == 0) {
                                orb.TurnToAir();
                            }
                            return true;
                        }
                        else {
                            maxUseOrbNum -= orb.stack;
                            orb.TurnToAir();
                            if (maxUseOrbNum <= 0) {
                                return true;
                            }
                        }
                    }
                }
                return true;
            }
        }

        public override void Update() {
            if (OnBoolMoon) {
                Player player = CWRUtils.InPosFindPlayer(Center);
                if (!Main.bloodMoon && !OldOnBoolMoon) {
                    if (player == null) {
                        SommonLose(player);
                        return;
                    }
                    if (!UseInPlayerBloodOrb(player)) {
                        return;
                    }
                    SoundEngine.PlaySound(SoundID.Roar, Center);
                    for (int i = 0; i < 63; i++) {
                        Vector2 vr = new Vector2(Main.rand.Next(-12, 12), Main.rand.Next(-23, -3));
                        Dust.NewDust(Center - new Vector2(16, 16), 32, 32, DustID.Blood, vr.X, vr.Y
                            , Scale: Main.rand.NextFloat(1.2f, 3.1f));
                    }
                    CWRUtils.Text(CWRLocalizationText.GetTextValue("BloodAltar_Text1"), Color.DarkRed);
                }

                Main.dayTime = false;
                Main.bloodMoon = true;
                CalamityNetcode.SyncWorld();

                for (int i = 0; i < Main.item.Length; i++) {
                    Item orb = Main.item[i];
                    if (orb.type == ModContent.ItemType<BloodOrb>()) {
                        Vector2 orbToPos = orb.position.To(Center);
                        if (orbToPos.LengthSquared() > 62 * 62) {
                            Vector2 orbToPosUnit = orbToPos.UnitVector();
                            float leng = orbToPos.Length() / 62f;
                            for (int j = 0; j < 62; j++) {
                                Vector2 spanPos = orb.Center + orbToPosUnit * leng * j;
                                LightParticle particle = new LightParticle(spanPos, Vector2.Zero, 0.3f, Color.DarkRed, 15);
                                CWRParticleHandler.AddParticle(particle);
                            }
                            orb.position = Center;
                        }
                        else {
                            orb.position = Center;
                            Chest chest = CWRUtils.FindNearestChest(Position.X, Position.Y);
                            if (chest != null) {
                                Vector2 chestPos = new Vector2(chest.x, chest.y) * 16;
                                Vector2 PosToChest = Center.To(chestPos);
                                Vector2 PosToChestUnit = PosToChest.UnitVector();
                                float leng = PosToChest.Length() / 32f;
                                for (int j = 0; j < 32; j++) {
                                    Vector2 spanPos = Center + PosToChestUnit * leng * j;
                                    LightParticle particle = new LightParticle(spanPos, Vector2.Zero, 0.3f, Color.DarkGreen, 15);
                                    CWRParticleHandler.AddParticle(particle);
                                }
                                chest.AddItem(orb);
                                orb.TurnToAir();
                            }
                        }
                    }
                }
                SpanDustEfset();
                CWRUtils.ClockFrame(ref frameIndex, 6, 3);
                Lighting.AddLight(Center, Color.DarkRed.ToVector3() * (Math.Abs(MathF.Sin(Time * 0.005f)) * 23 + 2));
                OldOnBoolMoon = OnBoolMoon;
            }
            else {
                if (OldOnBoolMoon) {
                    SoundEngine.PlaySound(CWRSound.Peuncharge, Center);
                    for (int i = 0; i < 133; i++) {
                        Vector2 vr = new Vector2(0, Main.rand.Next(-33, -3));
                        Dust.NewDust(Center - new Vector2(16, 16), 32, 32, DustID.Blood, vr.X, vr.Y
                        , Scale: Main.rand.NextFloat(0.7f, 1.3f));
                    }
                    Main.dayTime = true;
                    if (Main.bloodMoon) {
                        Main.bloodMoon = false;
                    }
                }
                frameIndex = 0;
                Lighting.AddLight(Center, Color.DarkRed.ToVector3());
                OldOnBoolMoon = false;
            }
            Time++;
        }

        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(Main.myPlayer, i, j, BloodAltar.Width, BloodAltar.Height);
                NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, i, j, Type);
                return -1;
            }

            int id = Place(i, j);
            return id;
        }
    }
}
