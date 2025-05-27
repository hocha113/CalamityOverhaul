using CalamityOverhaul.Content.Industrials.MaterialFlow;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.GameContent.ObjectInteractions;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class WGGCollector : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/WGGCollector";
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
            Item.createTile = ModContent.TileType<WGGCollectorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }
    }

    internal class WGGCollectorTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/WGGCollectorTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<WGGCollector>());

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
            //设置声音和挖掘强度，毕竟属于敌对建筑
            HitSound = SoundID.Item14;
            MineResist = 4f;
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) {
            num = fail ? 1 : 3;
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out WGGCollectorTP collector)) {
                return;
            }
            collector.wGGCollectorArm.byHitSyncopeTime = 60;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out WGGCollectorTP collector)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += (collector.altState ? 0 : 1) * 18 * 5;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class WGGCollectorTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<WGGCollectorTile>();
        public override int TargetItem => ModContent.ItemType<WGGCollector>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 800;
        internal WGGCollectorArm wGGCollectorArm;
        internal bool altState;
        public override void SetBattery() => PlaceNet = true;
        public override void Initialize() => altState = Main.rand.NextBool();
        public override void UpdateMachine() {
            if (!VaultUtils.isClient && (wGGCollectorArm == null || !wGGCollectorArm.Projectile.Alives())) {
                wGGCollectorArm = Projectile.NewProjectileDirect(this.FromObjectGetParent()
                    , CenterInWorld + new Vector2(0, 14), Vector2.Zero
                    , ModContent.ProjectileType<WGGCollectorArm>(), 10, 2, -1).ModProjectile as WGGCollectorArm;
            }

            if (wGGCollectorArm != null && wGGCollectorArm.Projectile.Alives()) {
                wGGCollectorArm.Projectile.timeLeft = 2;
            }
        }
        public override void MachineKill() {
            if (!VaultUtils.isClient) {
                DropItem(ModContent.ItemType<Collector>());
            }
        }
    }

    internal class WGGCollectorArm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/WGGMechanicalArm")]
        private static Asset<Texture2D> arm;//手臂的体节纹理
        [VaultLoaden("CalamityOverhaul/Assets/ElectricPowers/WGGMechanicalClamp")]
        private static Asset<Texture2D> clamp;//手臂的夹子纹理
        private Player player;
        internal Vector2 startPos;//记录这个弹幕的起点位置
        enum ArmState
        {
            Idle,
            Searching,
            ApproachingTarget,
            Attacking,
            Retreating
        }
        private ArmState currentState = ArmState.Idle;
        private int attackTimer;
        private int idleWiggleTime;
        private float syncopeRotAngle;
        internal int byHitSyncopeTime;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 10086;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                startPos = Projectile.Center;
                Projectile.localAI[0] = 1f;
                currentState = ArmState.Searching;
            }

            Projectile.damage = Projectile.originalDamage;

            if (byHitSyncopeTime > 0) {
                byHitSyncopeTime--;
                DoSyncopeMotion(); // 受到攻击后的晕厥
                return;
            }

            if (player?.Alives() != true) {
                player = startPos.FindClosestPlayer(600);
                if (player == null) {
                    Projectile.damage = 0;
                    DoIdleMotion(); // 无目标时的摆动待机状态
                    return;
                }
                else {
                    currentState = ArmState.ApproachingTarget;
                }
            }

            switch (currentState) {
                case ArmState.ApproachingTarget:
                case ArmState.Searching:
                    ApproachTarget();
                    break;
                case ArmState.Attacking:
                    AttackTarget();
                    break;
                case ArmState.Retreating:
                    Projectile.damage = 0;
                    Retreat();
                    break;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        private void ApproachTarget() {
            float dist = Vector2.Distance(Projectile.Center, player.Center);
            if (dist > 600f) {
                currentState = ArmState.Searching;
                player = null;
                return;
            }

            if (dist < 100f) {
                attackTimer = 0;
                currentState = ArmState.Attacking;
                return;
            }

            Projectile.ChasingBehavior(player.Center, 8f);
        }

        private void AttackTarget() {
            attackTimer++;

            if (attackTimer == 1) {
                // 发起冲刺
                Vector2 dashDir = (player.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dashDir * 18f;
            }
            else if (attackTimer > 16) {
                Projectile.velocity /= 2;
                currentState = ArmState.Retreating;
            }
        }

        private void Retreat() {
            Vector2 retreatPos = startPos + new Vector2(0, -120);
            if (Projectile.Center.Distance(retreatPos) < 10f) {
                currentState = ArmState.Searching;
                return;
            }

            Projectile.ChasingBehavior(retreatPos, 6f);
        }

        private void DoSyncopeMotion() {
            // 每帧递增计时器
            idleWiggleTime++;

            // 抖动半径 + 衰减效果
            float shakeRadius = MathHelper.Lerp(24f, 6f, 1f - byHitSyncopeTime / 60f); // 随时间减弱
            float angle = idleWiggleTime * 0.3f;
            Vector2 offset = new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle * 1.3f)) * shakeRadius;

            // 设置旋转角度偏移（模拟旋转晕头感）
            syncopeRotAngle += (float)Math.Sin(idleWiggleTime / 5f) * 0.05f;
            Projectile.rotation = syncopeRotAngle;

            // 目标点偏移
            Vector2 syncopeTarget = startPos + offset + new Vector2(0, -60f);
            Projectile.ChasingBehavior(syncopeTarget, 2.5f); // 晕厥时移动缓慢
        }

        private void DoIdleMotion() {
            idleWiggleTime++;
            float xOffset = (float)Math.Sin(idleWiggleTime / 30f) * 40f;
            Vector2 idleTarget = startPos + new Vector2(xOffset, -60f + (float)Math.Sin(idleWiggleTime / 45f) * 12f);
            Projectile.ChasingBehavior(idleTarget, 4f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Main.rand.NextBool()) {
                return;
            }

            // 获取一个非空物品槽（排除空格、空气物品）
            List<int> validSlots = new();
            for (int i = 0; i < 59; i++) { // 只检查背包部分（0~58）
                if (i == player.selectedItem) {
                    continue;//手上拿的不要丢
                }
                if (!target.inventory[i].IsAir && target.inventory[i].stack > 0) {
                    validSlots.Add(i);
                }
            }

            // 没有可丢弃物品
            if (validSlots.Count == 0) {
                return;
            }

            // 随机选择一个非空格子
            int slot = Main.rand.Next(validSlots);
            Item item = target.inventory[slot];

            // 生成掉落物
            int newItemIndex = Item.NewItem(
                Projectile.GetSource_OnHit(target), Projectile.Hitbox, item.type, item.stack,
                noBroadcast: false, prefixGiven: item.prefix
            );

            if (!VaultUtils.isSinglePlayer && newItemIndex >= 0) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, newItemIndex);
            }

            // 让物品从背包中消失
            item.TurnToAir();

            SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
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
                , clamp.Value.GetRectangle(), lightColor, clampRot + MathHelper.PiOver2
                , clamp.Value.GetOrig(), 1f, SpriteEffects.None, 0f);

            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }
    }
}
