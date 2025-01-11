using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Tiles;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModules
{
    internal class BloodAltarTP : TileProcessor, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<BloodAltar>();
        public Vector2 Center => PosInWorld + new Vector2(BloodAltar.Width * 18, BloodAltar.Height * 18) / 2;
        public static Asset<Texture2D> BloodAltarEffect;
        public static int targetFuncsWhoAmi;
        public static int startPlayerWhoAmI;
        public static bool OnBoolMoon;
        public static bool Old_OnBoolMoon;
        public bool mouseOnTile;
        public long Time = 0;
        public int frameIndex = 1;
        internal bool drawGlow;
        internal Color gloaColor;
        private int gloawTime;
        void ICWRLoader.LoadAsset() => BloodAltarEffect = CWRUtils.GetT2DAsset(CWRConstant.Asset + "TileModules/BloodAltarEffect");
        void ICWRLoader.UnLoadData() => BloodAltarEffect = null;
        public override void SetProperty() {
            LoadenWorldSendData = false;
        }
        public override void OnKill() {
            OnBoolMoon = false;
            Old_OnBoolMoon = false;
            Main.dayTime = true;
            if (Main.bloodMoon) {
                Main.bloodMoon = false;
            }
            if (VaultUtils.isClient) {
                SendData();
            }
        }

        private void SpanDustEfset() {
            if (frameIndex == 3) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vr = new Vector2(Main.rand.Next(-8, 8), Main.rand.Next(-5, -2));
                    Dust.NewDust(Center - new Vector2(16, 16), 32, 32, DustID.Blood, vr.X, vr.Y
                        , Scale: Main.rand.NextFloat(0.7f, 1.3f));
                }
                PRT_Light particle = new PRT_Light(Center + Main.rand.NextVector2Unit() * Main.rand.Next(0, 15)
                    , new Vector2(0, -1), 0.3f, Color.DarkGreen, 165);
                PRTLoader.AddParticle(particle);
            }
        }

        private void SommonLose(Player player) {
            CWRUtils.Text(CWRLocText.GetTextValue("BloodAltar_Text2"), Color.DarkRed);
            if (player != null) {
                PlayerDeathReason pd = PlayerDeathReason.ByCustomReason(player.name + CWRLocText.GetTextValue("BloodAltar_Text3"));
                player.Hurt(pd, player.statLifeMax2 / 10, 0);
            }
            Old_OnBoolMoon = OnBoolMoon = false;
        }

        private void FindOrb() {
            for (int i = 0; i < Main.item.Length; i++) {
                Item orb = Main.item[i];
                if (orb.type == ModContent.ItemType<BloodOrb>()) {
                    Vector2 orbToPos = orb.position.To(Center);
                    if (orbToPos.LengthSquared() > 62 * 62) {
                        Vector2 orbToPosUnit = orbToPos.UnitVector();
                        float leng = orbToPos.Length() / 62f;
                        if (!VaultUtils.isServer) {
                            for (int j = 0; j < 62; j++) {
                                Vector2 spanPos = orb.Center + orbToPosUnit * leng * j;
                                PRT_Light particle = new PRT_Light(spanPos, Vector2.Zero, 0.3f, Color.DarkRed, 15);
                                PRTLoader.AddParticle(particle);
                            }
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
                            if (!VaultUtils.isServer) {
                                for (int j = 0; j < 32; j++) {
                                    Vector2 spanPos = Center + PosToChestUnit * leng * j;
                                    PRT_Light particle = new PRT_Light(spanPos, Vector2.Zero, 0.3f, Color.DarkGreen, 15);
                                    PRTLoader.AddParticle(particle);
                                }
                            }

                            chest.AddItem(orb);
                            orb.TurnToAir();
                        }
                    }
                }
            }
        }

        internal bool UseInPlayerBloodOrb(Player player) {
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

        public void UpdateGlow() {
            drawGlow = mouseOnTile && !OnBoolMoon;//如果开启了就不要显示描边
            if (drawGlow) {
                gloawTime++;
                gloaColor = Color.Red * MathF.Abs(MathF.Sin(gloawTime * 0.04f));
            }
            else {
                gloawTime = 0;
            }
        }

        public override void Update() {
            Rectangle tileRec = new Rectangle(Position.X * 16, Position.Y * 16, BloodAltar.Width * 18, BloodAltar.Height * 18);
            mouseOnTile = tileRec.Intersects(new Rectangle((int)Main.MouseWorld.X, (int)Main.MouseWorld.Y, 1, 1));
            UpdateGlow();

            if (OnBoolMoon) {
                if (targetFuncsWhoAmi == WhoAmI && !Old_OnBoolMoon && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar, Center);
                    for (int i = 0; i < 63; i++) {
                        Vector2 vr = new Vector2(Main.rand.Next(-12, 12), Main.rand.Next(-23, -3));
                        Dust.NewDust(Center - new Vector2(16, 16), 32, 32, DustID.Blood, vr.X, vr.Y
                            , Scale: Main.rand.NextFloat(1.2f, 3.1f));
                    }
                    CWRUtils.Text(CWRLocText.GetTextValue("BloodAltar_Text1"), Color.DarkRed);
                }

                Main.dayTime = false;
                Main.bloodMoon = true;
                Main.moonPhase = 5;

                FindOrb();

                if (!VaultUtils.isServer) {
                    SpanDustEfset();
                    CWRUtils.ClockFrame(ref frameIndex, 6, 3);
                    Lighting.AddLight(Center, Color.DarkRed.ToVector3() * (Math.Abs(MathF.Sin(Time * 0.005f)) * 23 + 2));
                }

                if (targetFuncsWhoAmi == WhoAmI) {
                    Old_OnBoolMoon = OnBoolMoon;
                }
            }
            else {
                if (Old_OnBoolMoon) {
                    if (targetFuncsWhoAmi == WhoAmI) {
                        SoundEngine.PlaySound(CWRSound.Peuncharge, Center);
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 133; i++) {
                                Vector2 vr = new Vector2(0, Main.rand.Next(-33, -3));
                                Dust.NewDust(Center - new Vector2(16, 16), 32, 32, DustID.Blood, vr.X, vr.Y
                                , Scale: Main.rand.NextFloat(0.7f, 1.3f));
                            }
                        }
                    }
                    Main.dayTime = true;
                    if (Main.bloodMoon) {
                        Main.bloodMoon = false;
                    }
                }
                frameIndex = 0;
                Lighting.AddLight(Center, Color.DarkRed.ToVector3());
                if (targetFuncsWhoAmi == WhoAmI) {
                    Old_OnBoolMoon = false;
                }
            }
            Time++;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Vector2 drawPos;

            if (OnBoolMoon) {
                drawPos = Center + new Vector2(-8, -64) - Main.screenPosition;
                float slp = MathF.Abs(MathF.Sin(Time * 0.03f) * 0.3f) + 1;
                VaultUtils.SimpleDrawItem(spriteBatch, ModContent.ItemType<BloodOrb>(), drawPos, slp, 0, Color.White);
            }
            else if (mouseOnTile) {
                drawPos = Center + new Vector2(-8, -32) - Main.screenPosition;
                float slp = MathF.Abs(MathF.Sin(Time * 0.03f) * 0.3f) + 1;
                VaultUtils.SimpleDrawItem(spriteBatch, ModContent.ItemType<BloodOrb>(), drawPos, slp, 0, Color.White);
            }
        }

        public override void SendData(ModPacket data) {
            data.Write(targetFuncsWhoAmi);
            data.Write(startPlayerWhoAmI);
            data.Write(OnBoolMoon);
            data.Write(Old_OnBoolMoon);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            targetFuncsWhoAmi = reader.ReadInt32();
            startPlayerWhoAmI = reader.ReadInt32();
            OnBoolMoon = reader.ReadBoolean();
            Old_OnBoolMoon = reader.ReadBoolean();
        }
    }
}
