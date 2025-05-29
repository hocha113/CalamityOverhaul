using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class Collector : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Collector";
        internal static LocalizedText Text1;
        internal static LocalizedText Text2;
        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "Excessive Quantity!");
            Text2 = this.GetLocalization(nameof(Text2), () => "There are no boxes around!");
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<CollectorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }
    }

    internal class CollectorTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/CollectorTile";
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorStartTile")]
        public static Asset<Texture2D> startAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorStartTileGlow")]
        public static Asset<Texture2D> startGlowAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorTileGlow")]
        public static Asset<Texture2D> tileGlowAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<Collector>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(1, 3);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out CollectorTP collector)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += collector.frame * 18 * 5;
            Texture2D tex = collector.workState ? TextureAssets.Tile[Type].Value : startAsset.Value;
            Texture2D glow = collector.workState ? tileGlowAsset.Value : startGlowAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class CollectorTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<CollectorTile>();
        public override int TargetItem => ModContent.ItemType<Collector>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;
        private readonly List<CollectorArm> CollectorArms = [];
        private int textIdleTime;
        internal int frame;
        internal bool workState;
        internal Chest Chest;
        public override void SendData(ModPacket data) {
            data.Write(workState);
            data.Write(CollectorArms.Count);
            foreach (var arm in CollectorArms) {
                data.Write(arm.Projectile.identity);
            }
        }
        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            workState = reader.ReadBoolean();
            int count = reader.ReadInt32();
            CollectorArms.Clear();
            for (int i = 0; i < count; i++) {
                int index = reader.ReadInt32();
                Projectile projectile = Main.projectile[index];
                if (projectile.type == ModContent.ProjectileType<WGGCollectorArm>()) {
                    CollectorArms.Add(projectile.ModProjectile as CollectorArm);
                }
            }
        }
        private void FindFrame() {
            int maxFrame = workState ? 7 : 24;
            if (!workState && frame == 23) {
                frame = 0;//立刻让帧归零防止越界
                workState = true;
                if (Main.dedServ) {
                    SendData();
                }
                SoundEngine.PlaySound(CWRSound.CollectorStart, PosInWorld);
            }
            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        public override void UpdateMachine() {
            FindFrame();

            if (!workState) {
                return;
            }

            Chest = CWRUtils.FindNearestChest(Position.X, Position.Y, 100);
            if (Chest == null && textIdleTime <= 0) {
                CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text2.Value);
                textIdleTime = 300;
            }

            CollectorArms.RemoveAll(p => !p.Projectile.Alives() || p.Type != ModContent.ProjectileType<CollectorArm>());

            if (textIdleTime > 0) {
                textIdleTime--;
            }

            if (CollectorArms.Count < 3) {
                if (VaultUtils.CountProjectilesOfID<CollectorArm>() > 300) {
                    if (textIdleTime <= 0) {
                        CombatText.NewText(HitBox, Color.YellowGreen, Collector.Text1.Value);
                        textIdleTime = 300;
                    }
                    return;
                }

                if (!VaultUtils.isClient) {
                    CollectorArm collectorArm = Projectile.NewProjectileDirect(this.FromObjectGetParent()
                    , CenterInWorld + new Vector2(0, 14), Vector2.Zero
                    , ModContent.ProjectileType<CollectorArm>(), 0, 0, -1).ModProjectile as CollectorArm;
                    CollectorArms.Add(collectorArm);
                    SendData();
                }
            }

            int index = 0;
            foreach (CollectorArm arm in CollectorArms) {
                arm.offsetIndex = index;
                arm.Projectile.timeLeft = 2;
                arm.collectorTP = this;
                index++;
            }
        }

        internal void KillArm() {
            foreach (CollectorArm collectorArm in CollectorArms) {
                collectorArm.Projectile.Kill();
            }
            CollectorArms.Clear();
        }

        public override void MachineKill() => KillArm();

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }

    internal class CollectorArm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalArm")]
        private static Asset<Texture2D> arm;//手臂的体节纹理
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClamp")]
        private static Asset<Texture2D> clamp;//手臂的夹子纹理
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClampGlow")]
        private static Asset<Texture2D> clampGlow;//手臂的夹子的光效纹理
        internal CollectorTP collectorTP;
        internal Vector2 startPos;//记录这个弹幕的起点位置
        internal int offsetIndex;
        private Item graspItem;
        internal bool BatteryPrompt;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.WriteVector2(startPos);
            writer.Write(offsetIndex);
            writer.Write(BatteryPrompt);
            ItemIO.Send(graspItem, writer);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            startPos = reader.ReadVector2();
            offsetIndex = reader.ReadInt32();
            BatteryPrompt = reader.ReadBoolean();
            graspItem = ItemIO.Receive(reader);
        }

        internal Item FindItem() {
            Item item = null;
            float maxFindSQ = 4000000;
            foreach (var i in Main.ActiveItems) {
                if (i.CWR().TargetByCollector >= 0 && i.CWR().TargetByCollector != Projectile.identity) {
                    //如果发现目标物品已经物有所主，就判断是否自己比那个主人更加的近，近的话就换自己上去拿
                    //当然，如果别的手因为某种原因已经不活跃了，就直接换自己上
                    //Projectile otherArm = Main.projectile[i.CWR().TargetByCollector];
                    //if (!otherArm.Alives() || otherArm.DistanceSQ(i.Center) < Projectile.DistanceSQ(i.Center)) {
                    //    i.CWR().TargetByCollector = Projectile.identity;
                    //}
                    //else {
                    //    continue;
                    //}
                    continue;
                }

                float newFindSQ = i.Center.DistanceSQ(Projectile.Center);
                if (newFindSQ < maxFindSQ) {
                    item = i;
                    maxFindSQ = newFindSQ;
                }
            }
            return item;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                startPos = Projectile.Center;
                Projectile.localAI[0] = 1f;
                Projectile.netUpdate = true;
            }

            if (collectorTP == null) {
                return;
            }

            if (Projectile.ai[0] == 1 && graspItem != null && graspItem.type != ItemID.None) {
                if (collectorTP.Chest == null) {
                    Projectile.velocity = Vector2.Zero;
                    if (!VaultUtils.isClient) {
                        int type = Item.NewItem(Projectile.FromObjectGetParent(), Projectile.Hitbox, graspItem.Clone());
                        if (VaultUtils.isServer) {
                            NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                        }
                        SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
                    }
                    Projectile.ai[0] = 0;
                    Projectile.netUpdate = true;
                    return;
                }

                graspItem.CWR().TargetByCollector = Projectile.identity;
                Vector2 chestPos = new Vector2(collectorTP.Chest.x, collectorTP.Chest.y) * 16 + new Vector2(8, 8);
                Projectile.ChasingBehavior(chestPos, 8);
                graspItem.Center = Projectile.Center;

                float toChest = chestPos.Distance(Projectile.Center);
                if (toChest < 60) {//设置一下打开动画
                    collectorTP.Chest.eatingAnimationTime = 20;
                }
                if (toChest < 32) {//将物品放进箱子
                    Projectile.velocity = Vector2.Zero;
                    collectorTP.Chest.AddItem(graspItem);
                    graspItem.TurnToAir();
                    Projectile.ai[0] = 0;
                    Projectile.netUpdate = true;
                }
                return;
            }

            Item item = collectorTP.Chest == null ? null : FindItem();

            if (collectorTP.MachineData.UEvalue < 100) {
                item = null;
                if (!BatteryPrompt) {
                    CombatText.NewText(collectorTP.HitBox, new Color(111, 247, 200), CWRLocText.Instance.Turret_Text1.Value, false);
                    BatteryPrompt = true;
                    Projectile.netUpdate = true;
                }
            }
            else {
                if (BatteryPrompt) {
                    Projectile.netUpdate = true;
                }
                BatteryPrompt = false;
            }

            if (item != null) {
                item.CWR().TargetByCollector = Projectile.identity;
                Projectile.ChasingBehavior(item.Center, 8);
                Projectile.EntityToRot(Projectile.velocity.ToRotation(), 0.1f);
                if (item.Center.Distance(Projectile.Center) < 32) {
                    if (collectorTP.MachineData.UEvalue > 4 && !VaultUtils.isClient) {
                        collectorTP.MachineData.UEvalue -= 4;
                        collectorTP.SendData();
                    }

                    graspItem = item.Clone();
                    item.TurnToAir();
                    graspItem.CWR().TargetByCollector = Projectile.identity;
                    Projectile.ai[0] = 1;
                    Projectile.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
                }
                return;
            }
            else {
                Vector2 offset = new Vector2(0, -120);
                if (offsetIndex == 1) {
                    offset = new Vector2(120, -20);
                }
                if (offsetIndex == 2) {
                    offset = new Vector2(-120, -20);
                }
                Projectile.ChasingBehavior(startPos + offset, 8);
            }

            Projectile.EntityToRot(new Vector2(0, 1).ToRotation(), 0.1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (startPos == Vector2.Zero) {
                return false;
            }

            if (BatteryPrompt) {
                lightColor.R /= 2;
                lightColor.G /= 2;
                lightColor.B /= 2;
                lightColor.A = 255;
            }

            Texture2D tex = arm.Value;
            Vector2 start = startPos;
            Vector2 end = Projectile.Center;

            // 动态控制点偏移
            float dist = Vector2.Distance(start, end);
            float bendHeight = MathHelper.Clamp(dist * 0.5f, 40f, 200f);
            Vector2 midControl = (start + end) / 2 + new Vector2(0, -bendHeight);

            // 估算真实曲线长度
            int sampleCount = 50;
            float curveLength = 0f;
            Vector2 prev = start;
            for (int i = 1; i <= sampleCount; i++) {
                float t = i / (float)sampleCount;
                Vector2 a = Vector2.Lerp(start, midControl, t);
                Vector2 b = Vector2.Lerp(midControl, end, t);
                Vector2 point = Vector2.Lerp(a, b, t);
                curveLength += Vector2.Distance(prev, point);
                prev = point;
            }

            float segmentLength = tex.Height / 2;
            int segmentCount = Math.Max(2, (int)(curveLength / segmentLength));
            Vector2[] points = new Vector2[segmentCount + 1];

            // 构建点位
            for (int i = 0; i <= segmentCount; i++) {
                float t = i / (float)segmentCount;
                Vector2 pos = Vector2.Lerp(
                    Vector2.Lerp(start, midControl, t),
                    Vector2.Lerp(midControl, end, t),
                    t
                );
                points[i] = pos;
            }

            float clampRot = Projectile.rotation;

            for (int i = 0; i < segmentCount; i++) {
                Vector2 pos = points[i];
                Vector2 next = points[i + 1];
                Vector2 direction = next - pos;
                Color color = Lighting.GetColor((pos / 16).ToPoint());
                float rotation = direction.ToRotation() + MathHelper.PiOver2;
                if (i == segmentCount - 1) {
                    clampRot = direction.ToRotation();
                }
                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, color, rotation
                    , new Vector2(tex.Width / 2f, tex.Height), 1f, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(clamp.Value, Projectile.Center - Main.screenPosition
                , clamp.Value.GetRectangle((graspItem == null || graspItem.IsAir) ? 0 : 1, 2)
                , lightColor, clampRot + MathHelper.PiOver2
                , clamp.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(clampGlow.Value, Projectile.Center - Main.screenPosition
                , clampGlow.Value.GetRectangle((graspItem == null || graspItem.IsAir) ? 0 : 1, 2)
                , Color.White, clampRot + MathHelper.PiOver2
                , clampGlow.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);

            if (graspItem != null && !graspItem.IsAir) {
                VaultUtils.SimpleDrawItem(Main.spriteBatch, graspItem.type
                    , Projectile.Center - Main.screenPosition, 1f
                    , clampRot + MathHelper.PiOver2, lightColor);
            }

            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }
    }
}
