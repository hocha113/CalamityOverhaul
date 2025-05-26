using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class Collector : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Collector";
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
            Item.CWR().ConsumeUseUE = 1200;
        }
    }

    internal class CollectorTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/CollectorTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<Collector>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(2, 2);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
    }

    internal class CollectorTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<CollectorTile>();
        private List<CollectorArm> collectorArms = [];
        public override void Update() {
            if (VaultUtils.isClient) {
                return;
            }
            collectorArms.RemoveAll(p => p == null || !p.Projectile.Alives());
            if (collectorArms.Count < 3) {
                CollectorArm collectorArm = Projectile.NewProjectileDirect(this.FromObjectGetParent(), CenterInWorld, Vector2.Zero
                    , ModContent.ProjectileType<CollectorArm>(), 0, 0, -1).ModProjectile as CollectorArm;
                collectorArm.offsetIndex = collectorArms.Count;
                collectorArms.Add(collectorArm);
            }
            foreach (CollectorArm arm in collectorArms) {
                arm.Projectile.timeLeft = 10086;
            }
        }
        public override void OnKill() {
            foreach (CollectorArm collectorArm in collectorArms) {
                collectorArm.Projectile.Kill();
            }
            collectorArms.Clear();
        }
    }

    internal class CollectorArm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalArm")]
        private static Asset<Texture2D> arm;//手臂的体节纹理
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/MechanicalClamp")]
        private static Asset<Texture2D> clamp;//手臂的夹子纹理
        internal Vector2 startPos;//记录这个弹幕的起点位置
        internal int offsetIndex;
        private Item graspItem;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 10086;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                startPos = Projectile.Center;
                Projectile.localAI[0] = 1f;
            }

            if (Projectile.ai[0] == 1) {
                graspItem.CWR().TargetByCollector = Projectile.identity;
                Projectile.ChasingBehavior(startPos, 8);
                graspItem.Center = Projectile.Center;
                if (startPos.Distance(Projectile.Center) < 32) {
                    Projectile.velocity = Vector2.Zero;
                    graspItem.TurnToAir();
                    Projectile.ai[0] = 0;
                }
                return;
            }

            Item item = null;
            float maxFind = 2000;
            foreach (var i in Main.ActiveItems) {
                if (i.CWR().TargetByCollector >= 0 && i.CWR().TargetByCollector != Projectile.identity) {
                    continue;
                }
                float newFind = i.Center.Distance(Projectile.Center);
                if (newFind < maxFind) {
                    item = i;
                    maxFind = newFind;
                }
            }

            if (item != null) {
                item.CWR().TargetByCollector = Projectile.identity;
                Projectile.ChasingBehavior(item.Center, 8);
                Projectile.EntityToRot(Projectile.velocity.ToRotation(), 0.1f);
                if (item.Center.Distance(Projectile.Center) < 32) {
                    graspItem = item;
                    graspItem.CWR().TargetByCollector = Projectile.identity;
                    Projectile.ai[0] = 1;
                }
                return;
            }
            else {
                Vector2 offset = new Vector2(0, -40);
                if (offsetIndex == 1) {
                    offset = new Vector2(20, -20);
                }
                if (offsetIndex == 2) {
                    offset = new Vector2(-20, -20);
                }
                Projectile.ChasingBehavior(startPos + offset, 8);
            }

            Projectile.EntityToRot(new Vector2(0, 1).ToRotation(), 0.1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (startPos == Vector2.Zero) {
                return false;
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

            float segmentLength = tex.Height;
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

            for (int i = 0; i < segmentCount; i++) {
                Vector2 pos = points[i];
                Vector2 next = points[i + 1];
                Vector2 direction = next - pos;
                float rotation = direction.ToRotation() + MathHelper.PiOver2;
                Color color = Lighting.GetColor((pos / 16).ToPoint());
                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, color, rotation
                    , new Vector2(tex.Width / 2f, tex.Height / 2f), 1f, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(clamp.Value, Projectile.Center - Main.screenPosition
                , clamp.Value.GetRectangle((graspItem == null || graspItem.IsAir) ? 0 : 1, 2)
                , lightColor, Projectile.rotation + MathHelper.PiOver4
                , clamp.Value.GetOrig(2), 1f, SpriteEffects.None, 0f);

            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }
    }
}
