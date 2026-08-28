using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using InnoVault.Trails;
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
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class TeslaElectromagneticTower : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTower";
        public override void SetDefaults() {
            Item.width = 38;
            Item.height = 78;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<TeslaElectromagneticTowerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1200;
        }

        public override void AddRecipes() {
            if (CWRID.Item_AerialiteBar > 0 && CWRID.Item_StormlionMandible > 0) {
                CreateRecipe().
                AddIngredient<CircuitBoard>(15).
                AddIngredient(CWRID.Item_AerialiteBar, 10).
                AddIngredient(CWRID.Item_StormlionMandible, 4).
                AddCondition(CWRRef.ConstructRecipeCondition(1, out Func<bool> condition), condition).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<CircuitBoard>(15).
                AddRecipeGroup(CWRCrafted.TungstenBarGroup, 15).
                AddTile(TileID.Anvils).
                Register();
            }

            CreateRecipe().
                AddIngredient<TeslaElectromagneticTowerAttackMode>().
                Register();
        }
    }

    internal class TeslaElectromagneticTowerAttackMode : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTowerAttackMode";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<TeslaElectromagneticTower>();
        public override LocalizedText Tooltip => ItemLoader.GetItem(ModContent.ItemType<TeslaElectromagneticTower>()).GetLocalization("Tooltip");
        public override void SetDefaults() {
            Item.width = 38;
            Item.height = 78;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<TeslaElectromagneticTowerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1200;
        }

        public override void AddRecipes() {
            if (CWRID.Item_AerialiteBar > 0 && CWRID.Item_StormlionMandible > 0) {
                CreateRecipe().
                AddIngredient<CircuitBoard>(15).
                AddIngredient(CWRID.Item_AerialiteBar, 10).
                AddIngredient(CWRID.Item_StormlionMandible, 4).
                AddCondition(CWRRef.ConstructRecipeCondition(1, out Func<bool> condition), condition).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<CircuitBoard>(15).
                AddRecipeGroup(CWRCrafted.TungstenBarGroup, 15).
                AddTile(TileID.Anvils).
                Register();
            }

            CreateRecipe().
                AddIngredient<TeslaElectromagneticTower>().
                Register();
        }
    }

    internal class TeslaElectromagneticTowerTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTowerTile";
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTowerTileGlow")]
        public static Asset<Texture2D> tileGlowAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<TeslaElectromagneticTower>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(2, 4);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide
                , TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool RightClick(int i, int j) {
            if (!TileProcessorLoader.AutoPositionGetTP<TeslaElectromagneticTowerTP>(i, j, out var tp)) {
                return false;
            }
            tp.RightEvent();
            return base.RightClick(i, j);
        }

        public override void HitWire(int i, int j) {
            if (!TileProcessorLoader.AutoPositionGetTP<TeslaElectromagneticTowerTP>(i, j, out var tp)) {
                return;
            }
            tp.RightEvent();
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out TeslaElectromagneticTowerTP tesla)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += (tesla.AttackPattern ? 1 : 0) * 5 * 18;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Texture2D glow = tileGlowAsset.Value;
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

    internal class TeslaElectromagneticTowerTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<TeslaElectromagneticTowerTile>();
        public override int TargetItem => ModContent.ItemType<TeslaElectromagneticTower>();
        public override bool CanDrop => false;
        public override float MaxUEValue => 1200;
        //索敌/耗电/冷却参数(700px/60UE每发/60t冷却/视线判定/Boss优先)即基类默认值,不再重写
        /// <summary>不启用模块架,网络包序与存档格式和迁移前逐字节一致</summary>
        public override int ModuleSlotCount => 0;
        /// <summary>旧行为:逻辑在所有端同跑,攻击弹幕从最近玩家的客户端生成</summary>
        public override bool SimulateOnAllEndpoints => true;
        /// <summary>旧档缺失模式键时的语义:默认护卫模式</summary>
        protected override bool DefaultModeWhenMissing => false;
        public float GuardValue { get; set; }

        //---- 护卫力场视觉状态：纯客户端表现，不参与判定，判定半径始终是 GuardValue ----
        /// <summary>护卫环的显示半径，欠阻尼弹簧跟随 <see cref="GuardValue"/>，扩张末端自带过冲回稳</summary>
        public float GuardVisualRadius { get; private set; }
        /// <summary>护卫环总体强度包络 0~1</summary>
        public float GuardVisualIntensity { get; private set; }
        /// <summary>半径变化强调量 0~1，喂给着色器的扩张/塌缩前沿</summary>
        public float GuardExpandGlow { get; private set; }
        /// <summary>本 tick 护卫场是否实际运转(护卫模式且有电)</summary>
        public bool GuardActive { get; private set; }
        /// <summary>特斯拉系电青色，与塔的闪电弹同源</summary>
        internal static readonly Color TeslaCyan = new(103, 255, 255);
        private float guardRadiusVel;
        private bool oldGuardActive;
        private int crawlArcTimer;
        private int dischargeTimer;
        private int sparkTimer;

        //模式位序列化(包序:MachineData→AttackPattern)已上移基类,这里只接表现钩子
        /// <summary>网络端判断出切换了形态时生成粒子效果和音效</summary>
        protected override void OnModeChangedByNet() => TeslaOpenEffect();
        /// <summary>本地右键/电线翻转形态时的粒子效果和音效</summary>
        protected override void OnModeToggleEffect() => TeslaOpenEffect();

        public override void SetBattery() {
            IdleDistance = 4000;//玩家远离后停止运行
            DrawExtendMode = 1100;//护卫环最大半径800+外缘辉光，塔出屏后环仍需绘制
            AttackPattern = TrackItem != null && TrackItem.type == ModContent.ItemType<TeslaElectromagneticTowerAttackMode>();
        }

        /// <summary>旧版粒子圆环，仅在 <see cref="EffectLoader.TeslaGuardRing"/> 缺失时作回退</summary>
        private void SpawnGuardEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            //并行阶段随机数(RandVr 内部使用 Main.rand)与Dust生成统一延迟到主线程执行(串行阶段立即执行)
            Defer(() => {
                for (int i = 0; i < 33; i++) {
                    Vector2 pos = CenterInWorld + VaultUtils.RandVr(GuardValue, GuardValue + 2);
                    int dust = Dust.NewDust(pos, 1, 1, DustID.Electric);
                    Main.dust[dust].noGravity = true;
                }
            });
        }

        /// <summary>推进护卫环显示包络：弹簧半径、强度淡入淡出、关闭边沿迸溅，每 tick 调用</summary>
        private void UpdateGuardVisual() {
            float target = GuardActive ? GuardValue : 0f;
            //欠阻尼弹簧：扩张末端轻微过冲回稳；塌缩加刚度快速收拢
            float stiffness = GuardActive ? 0.085f : 0.20f;
            float damping = GuardActive ? 0.85f : 0.66f;
            guardRadiusVel = guardRadiusVel * damping + (target - GuardVisualRadius) * stiffness;
            GuardVisualRadius += guardRadiusVel;
            if (GuardVisualRadius < 0f) {
                GuardVisualRadius = 0f;
                guardRadiusVel = 0f;
            }

            GuardVisualIntensity = MathHelper.Lerp(GuardVisualIntensity, GuardActive ? 1f : 0f, GuardActive ? 0.10f : 0.09f);
            if (!GuardActive && GuardVisualIntensity < 0.015f) {
                GuardVisualIntensity = 0f;
            }

            GuardExpandGlow = MathHelper.Clamp(MathF.Abs(guardRadiusVel) * 0.10f, 0f, 1f);

            //护卫场关闭边沿：环上向内迸一圈微电弧
            if (oldGuardActive && !GuardActive && GuardVisualRadius > 60f && !VaultUtils.isServer) {
                float burstRadius = GuardVisualRadius;
                Defer(() => SpawnCollapseBurst(burstRadius));
            }
            oldGuardActive = GuardActive;
        }

        /// <summary>护卫场运转期的环上表现：着色器缺失回退旧粒子环，否则火花点缀+爬弧+环光</summary>
        private void UpdateGuardEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            if (EffectLoader.TeslaGuardRing?.Value == null) {
                SpawnGuardEffect();
                return;
            }
            if (GuardVisualRadius < 40f) {
                return;
            }

            //环缘微电弧火花，切向初速
            if (++sparkTimer >= 5) {
                sparkTimer = 0;
                Defer(() => {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = CenterInWorld + ang.ToRotationVector2() * GuardVisualRadius;
                    Vector2 tangent = (ang + MathHelper.PiOver2).ToRotationVector2()
                        * Main.rand.NextFloat(1.5f, 4f) * (Main.rand.NextBool() ? 1f : -1f);
                    PRTLoader.NewParticle<PRT_GraniteVolt>(pos, tangent, TeslaCyan
                        , Main.rand.NextFloat(0.22f, 0.4f)).Configure(Main.rand.Next(3, 6));
                });
            }

            //塔顶线圈偶发微弧，标明场源
            if (Rand.NextBool(10)) {
                Defer(() => {
                    Vector2 coil = PosInWorld + new Vector2(Width * 0.5f + Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(2f, 20f));
                    PRTLoader.NewParticle<PRT_GraniteVolt>(coil, Main.rand.NextVector2Circular(1.5f, 1f), TeslaCyan
                        , Main.rand.NextFloat(0.18f, 0.32f)).Configure(Main.rand.Next(2, 5));
                });
            }

            //环面爬弧
            if (--crawlArcTimer <= 0) {
                crawlArcTimer = 20 + Rand.Next(21);
                Defer(SpawnCrawlArc);
            }

            //环上取样点打光，力场照亮场地
            Defer(() => {
                Vector3 lightColor = TeslaCyan.ToVector3() * 0.30f * GuardVisualIntensity;
                for (int i = 0; i < 8; i++) {
                    Vector2 pos = CenterInWorld + (MathHelper.TwoPi * i / 8f).ToRotationVector2() * GuardVisualRadius;
                    Lighting.AddLight(pos, lightColor);
                }
            });
        }

        /// <summary>环面爬弧：沿圆周一段弦弧的 ThunderTrail，读作电在边界上爬行</summary>
        private void SpawnCrawlArc() {
            float radius = GuardVisualRadius;
            if (radius < 90f) {
                return;
            }
            float start = Main.rand.NextFloat(MathHelper.TwoPi);
            float span = Main.rand.NextFloat(0.26f, 0.7f) * (Main.rand.NextBool() ? 1f : -1f);
            int pointCount = Main.rand.Next(6, 10);
            Vector2[] path = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++) {
                float t = i / (float)(pointCount - 1);
                //两端钉在环上，中段径向摆动
                float swing = MathF.Sin(t * MathHelper.Pi) * Main.rand.NextFloat(-12f, 12f);
                path[i] = CenterInWorld + (start + span * t).ToRotationVector2() * (radius + swing);
            }
            PRTLoader.NewParticle<PRT_TeslaArc>(path[pointCount / 2], Vector2.Zero, TeslaCyan, 1f)
                ?.Configure(path, Main.rand.Next(9, 16), Main.rand.NextFloat(6f, 10f), (0f, 6f), 4f);
        }

        /// <summary>对敌放电的节流入口，目标来自 UpdateMachine 的蓄水池抽样</summary>
        private void TryDischarge(NPC target) {
            if (VaultUtils.isServer || target == null) {
                return;
            }
            if (--dischargeTimer > 0) {
                return;
            }
            dischargeTimer = 26 + Rand.Next(20);
            int whoAmI = target.whoAmI;
            Defer(() => {
                if (!Main.npc.IndexInRange(whoAmI)) {
                    return;
                }
                NPC npc = Main.npc[whoAmI];
                if (!npc.active) {
                    return;
                }
                SpawnDischargeArc(npc);
            });
        }

        /// <summary>从环缘最近点向场内敌怪拉一道放电弧+命中迸溅：护卫塔正在干活的直观证据</summary>
        private void SpawnDischargeArc(NPC npc) {
            float radius = GuardVisualRadius;
            if (radius < 90f) {
                return;
            }
            Vector2 dir = CenterInWorld.To(npc.Center).SafeNormalize(Vector2.UnitY);
            Vector2 from = CenterInWorld + dir * radius;
            Vector2 to = npc.Center;
            Vector2 side = dir.RotatedBy(MathHelper.PiOver2);
            int pointCount = Main.rand.Next(7, 10);
            Vector2[] path = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++) {
                float t = i / (float)(pointCount - 1);
                //两端钉死，中段最大摆幅
                float sway = MathF.Sin(t * MathHelper.Pi) * Main.rand.NextFloat(-26f, 26f);
                path[i] = Vector2.Lerp(from, to, t) + side * sway;
            }
            PRTLoader.NewParticle<PRT_TeslaArc>(to, Vector2.Zero, TeslaCyan, 1f)
                ?.Configure(path, Main.rand.Next(12, 19), Main.rand.NextFloat(9f, 13f), (0f, 10f), 5f);

            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(to + Main.rand.NextVector2Circular(8f, 8f)
                    , Main.rand.NextVector2Unit() * 2.5f, TeslaCyan
                    , Main.rand.NextFloat(0.24f, 0.4f)).Configure(Main.rand.Next(3, 6));
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(to, DustID.Electric, VaultUtils.RandVr(3f));
                d.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                Volume = 0.3f,
                Pitch = Main.rand.NextFloat(-0.1f, 0.25f)
            }, to);
        }

        /// <summary>护卫场关闭时环向内迸一圈微电弧</summary>
        private void SpawnCollapseBurst(float radius) {
            for (int i = 0; i < 18; i++) {
                float ang = MathHelper.TwoPi * i / 18f + Main.rand.NextFloat(0.3f);
                Vector2 pos = CenterInWorld + ang.ToRotationVector2() * radius;
                Vector2 vel = ang.ToRotationVector2() * -Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_GraniteVolt>(pos, vel, TeslaCyan
                    , Main.rand.NextFloat(0.24f, 0.44f)).Configure(Main.rand.Next(3, 7));
            }
        }

        protected override void UpdateTurret() {
            bool guardRunning = false;
            if (AttackPattern) {
                GuardValue = 0;
                //攻击帧骨架已提炼进基类:电量门→冷却→索敌→Fire→扣电→冷却归零,数值与迁移前一致
                RunAttackCycle();
            }
            else if (MachineData.UEvalue > 2) {
                guardRunning = true;
                if (GuardValue < 800) {
                    GuardValue += 10;
                }

                UpdateGuardEffect();

                NPC dischargeTarget = null;
                int eligibleCount = 0;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.friendly) {
                        continue;
                    }
                    if (npc.Distance(CenterInWorld) > GuardValue) {
                        continue;
                    }
                    //并行阶段Buff写入延迟到主线程执行(串行阶段立即执行)
                    Defer(() => npc.AddBuff(BuffID.Electrified, 30));
                    //蓄水池抽样：场内敌怪等概率选一个作放电表现目标
                    eligibleCount++;
                    if (Rand.Next(eligibleCount) == 0) {
                        dischargeTarget = npc;
                    }
                }
                TryDischarge(dischargeTarget);

                if (++FireCoolden > 40) {
                    ArcCharging();
                    FireCoolden = 0;
                }

                MachineData.UEvalue -= 0.5f;
            }

            GuardActive = guardRunning;
            UpdateGuardVisual();
        }

        /// <summary>攻击模式开火:塔身高亮+音效,闪电弹从最近玩家的客户端生成</summary>
        protected override void Fire(NPC target) {
            for (int i = 0; i < 6; i++) {
                Vector2 spanPos = PosInWorld + new Vector2(Rand.Next(Width), Rand.Next(Height / 2)) + new Vector2(8, 8);
                //并行阶段粒子生成延迟到主线程执行(串行阶段立即执行)
                Defer(() => PRTLoader.NewParticle<PRT_TileHightlight>(spanPos, Vector2.Zero, Color.White));
            }

            Defer(() => SoundEngine.PlaySound(CWRSound.MagneticBurst, CenterInWorld));
            //从最近玩家的端口上生成弹幕:TeslaBallByAttack 基于 BaseHeldProj,必须有玩家 owner,
            //无法从服务端生成;此旧路径随本类保留,新塔一律在权威端生成普通 ModProjectile
            Player player = VaultUtils.FindClosestPlayer(CenterInWorld);
            if (player != null && player.whoAmI == Main.myPlayer) {
                Vector2 dir = CenterInWorld.To(target.Center).UnitVector();
                //并行阶段弹幕生成延迟到主线程执行(串行阶段立即执行)
                DeferSpawnProjectile(new EntitySource_WorldEvent(), CenterInWorld
                    , dir * 8, ModContent.ProjectileType<TeslaBallByAttack>(), 32, 2, -1);
            }
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            int itemID = AttackPattern ? ModContent.ItemType<TeslaElectromagneticTowerAttackMode>()
                    : ModContent.ItemType<TeslaElectromagneticTower>();
            Item item = new Item(itemID);
            item.CWR().UEValue = MachineData.UEvalue;
            DropItem(item);
        }

        public void ArcCharging() {
            Player player = VaultUtils.FindClosestPlayer(CenterInWorld, 800);
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }

            Item handItem = player.GetItem();
            if (handItem.type <= ItemID.None) {
                return;
            }

            if (!handItem.GetItemUsesCharge() || handItem.GetItemCharge() >= handItem.GetItemMaxCharge()) {
                return;
            }

            Defer(() => SoundEngine.PlaySound(CWRSound.ArcCharging, CenterInWorld));

            Vector2 dir = CenterInWorld.To(player.Center).UnitVector();
            //并行阶段弹幕生成延迟到主线程执行(串行阶段立即执行)
            DeferSpawnProjectile(new EntitySource_WorldEvent(), CenterInWorld
                , dir * 8, ModContent.ProjectileType<TeslaBallByGuard>(), 0, 0, player.whoAmI);
        }

        public void TeslaOpenEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(PosInWorld, Width, Height, DustID.Electric);
                Main.dust[dust].noGravity = true;

            }
            for (int x = 0; x < Width / 16; x++) {
                for (int y = 0; y < Height / 16; y++) {
                    Vector2 spanPos = PosInWorld + new Vector2(x, y) * 16 + new Vector2(8, 8);
                    PRTLoader.NewParticle<PRT_TileHightlight>(spanPos, Vector2.Zero, Color.BlueViolet);
                }
            }
            SoundEngine.PlaySound(CWRSound.TeslaOpen);
        }

        //RightEvent(翻转+SendData+表现)与 FrontDraw(充能条)已由基类等价接管
    }

    /// <summary>
    /// 护卫力场环绘制：<see cref="EffectLoader.TeslaGuardRing"/> 画在归一化圆盘 quad 上。<br/>
    /// PreDrawEverything 时画布尚未开启，自开 Immediate 批合批所有塔；
    /// 位于 PostDrawTiles 层，物块之上、实体之下，正是力场该在的层
    /// </summary>
    internal class TeslaGuardRingDraw : GlobalTileProcessor
    {
        public override bool PreDrawEverything(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return true;
            }
            Effect shader = EffectLoader.TeslaGuardRing?.Value;
            if (shader == null) {
                return true;//着色器缺失，塔侧已回退旧粒子环
            }
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D voro = CWRAsset.Extra_193?.Value;
            if (canvas == null || noise == null || voro == null) {
                return true;
            }

            bool begun = false;
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not TeslaElectromagneticTowerTP tesla || !tesla.Active) {
                    continue;
                }
                if (tesla.GuardVisualIntensity <= 0.02f || tesla.GuardVisualRadius < 8f) {
                    continue;
                }
                if (!VaultUtils.IsPointOnScreen(tesla.PosInWorld - Main.screenPosition, tesla.DrawExtendMode)) {
                    continue;
                }

                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                        SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                    GraphicsDevice gd = Main.instance.GraphicsDevice;
                    gd.Textures[1] = noise;
                    gd.SamplerStates[1] = SamplerState.LinearWrap;
                    gd.Textures[2] = voro;
                    gd.SamplerStates[2] = SamplerState.LinearWrap;
                }

                float radius = tesla.GuardVisualRadius;
                //quad 外留余量装 halo 与节点辉光；小半径时保底 150px
                float quadHalf = MathF.Max(radius * 1.42f, radius + 150f);
                float phase = (tesla.Position.X * 7 + tesla.Position.Y * 13) * 0.173f;
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + phase);
                shader.Parameters["ringProgress"]?.SetValue(radius / quadHalf);
                shader.Parameters["uQuadHalf"]?.SetValue(quadHalf);
                shader.Parameters["intensity"]?.SetValue(tesla.GuardVisualIntensity);
                shader.Parameters["expandGlow"]?.SetValue(tesla.GuardExpandGlow);
                shader.Parameters["seed"]?.SetValue(phase - MathF.Floor(phase));
                shader.CurrentTechnique.Passes[0].Apply();

                float diameter = quadHalf * 2f;
                spriteBatch.Draw(canvas, tesla.CenterInWorld - Main.screenPosition, null, Color.White,
                    0f, canvas.Size() * 0.5f, new Vector2(diameter / canvas.Width, diameter / canvas.Height),
                    SpriteEffects.None, 0f);
            }

            if (begun) {
                spriteBatch.End();
            }
            return true;
        }
    }

    /// <summary>
    /// 特斯拉护卫环电弧：沿给定折线放一道 ThunderTrail，纯表现无判定，
    /// 环面爬弧与对敌放电共用。走 PRT 使绘制落在世界实体批次里（同 <see cref="PRT_SkyBolt"/>）
    /// </summary>
    internal class PRT_TeslaArc : BasePRT
    {
        public override int InGame_World_MaxCount => 24;
        public override bool CanPool => false;
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ThunderTrail trail;
        private float baseWidth;
        //生命包络，AI 里推进、绘制函数里采样
        private float envelope = 1f;

        /// <param name="path">折线路径，至少 3 点，两端不抖动</param>
        /// <param name="lifetime">存活帧数</param>
        /// <param name="width">基础宽度(像素)</param>
        /// <param name="range">RandomThunder 的法向随机偏移范围</param>
        /// <param name="expand">RandomThunder 的额外圆散幅度</param>
        public PRT_TeslaArc Configure(Vector2[] path, int lifetime, float width, (float, float) range, float expand) {
            Lifetime = lifetime;
            baseWidth = width;
            Position = path[path.Length / 2];
            Velocity = Vector2.Zero;
            trail = new ThunderTrail(CWRAsset.ThunderTrail, WidthFunc, ColorFunc, AlphaFunc) {
                CanDraw = true,
                UseNonOrAdd = true,
                PartitionPointCount = 2,
                BasePositions = path,
            };
            trail.SetRange(range);
            trail.SetExpandWidth(expand);
            trail.RandomThunder();
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            //快起慢收：前 15% 满亮，之后三次方衰减
            float t = LifetimeCompletion;
            envelope = t < 0.15f ? 1f : 1f - MathF.Pow((t - 0.15f) / 0.85f, 3f);

            if (trail != null && Time % 2 == 0 && t < 0.6f) {
                trail.RandomThunder();
            }
            Lighting.AddLight(Position, Color.ToVector3() * envelope * 0.6f);
        }

        private float WidthFunc(float factor) => baseWidth * envelope;

        private Color ColorFunc(float factor)
            => Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.4f);

        private float AlphaFunc(float factor)
            => MathHelper.Clamp(envelope * 0.9f, 0f, 1f);

        public override bool PreDraw(SpriteBatch spriteBatch) {
            trail?.DrawThunder(Main.instance.GraphicsDevice);
            return false;
        }
    }

    //来自珊瑚石，谢谢你瓶中微光 :)
    internal class TeslaBallByAttack : BaseHeldProj
    {
        public override string Texture => CWRConstant.Masking + "StarTexture";
        public ref float PointDistance => ref Projectile.ai[2];
        public override bool CanFire => true;
        public ref float ThunderWidth => ref Projectile.localAI[1];
        public ref float ThunderAlpha => ref Projectile.localAI[2];
        public ref float State => ref Projectile.ai[0];
        public ref float Hited => ref Projectile.ai[1];
        public ref float Timer => ref Projectile.localAI[0];
        public int NPCIndex = -1;
        public float Alpha;
        public float FadeValue = 0;
        public Vector2 TargetCenter;
        public ThunderTrail trail;
        public LinkedList<Vector2> trailList = [];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.aiStyle = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool? CanDamage() => State == 1 && Hited == 0;

        public float GetAlpha(float factor) {
            if (factor < FadeValue) {
                return 0;
            }
            return ThunderAlpha * (factor - FadeValue) / (1 - FadeValue);
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write(Timer);
            writer.Write(NPCIndex);
            writer.Write(Alpha);
            writer.Write(FadeValue);
            writer.WriteVector2(TargetCenter);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            Timer = reader.ReadSingle();
            NPCIndex = reader.ReadInt32();
            Alpha = reader.ReadSingle();
            FadeValue = reader.ReadSingle();
            TargetCenter = reader.ReadVector2();
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Color(103, 255, 255).ToVector3());
            //生成后以极快的速度前进
            switch (State) {
                default:
                case 0://刚生成，等待透明度变高后开始寻敌
                    NPC targetNPC = Projectile.Center.FindClosestNPC(800);
                    if (targetNPC != null) {
                        NPCIndex = targetNPC.whoAmI;
                        TargetCenter = Projectile.Center + Projectile.velocity.UnitVector() * 126;
                        StartAttack();
                        Projectile.netUpdate = true;
                    }
                    else {
                        Projectile.Kill();
                    }
                    break;
                case 1://找到敌人，以极快的速度追踪
                    Chase();
                    break;
                case 2://后摇，闪电逐渐消失
                    {
                    //淡出由 Timer 纯本地推导（各端 AI 自行推进），不再逐拍 netUpdate；
                    //入淡出沿已一次性同步（Fade / 守卫球直切处），丢包兜底靠击杀广播
                    Timer++;
                    FadeValue = Smoother((int)Timer, 30);
                    ThunderWidth = Smoother(60 - (int)Timer, 60) * 14;
                    float factor = Timer / 30;
                    float sinFactor = MathF.Sin(factor * MathHelper.Pi);

                    if (Timer > 30) {
                        Projectile.Kill();
                    }
                }
                break;
            }
        }

        public static float Smoother(int timer, int maxTime) {
            float factor = (float)timer / maxTime;
            return factor * factor;
        }

        public virtual float ThunderWidthFunc_Sin(float factor) => MathF.Sin(factor * MathHelper.Pi) * ThunderWidth;
        public virtual Color ThunderColorFunc(float factor) => new Color(103, 255, 255);

        public void StartAttack() {
            Projectile.tileCollide = true;
            State = 1;
            ThunderAlpha = 1;
            ThunderWidth = 14;
            Projectile.extraUpdates = 6;
            Projectile.timeLeft = 10 * 100;
            trailList = new LinkedList<Vector2>();

            Projectile.velocity = (InMousePos - Projectile.Center).SafeNormalize(Vector2.Zero) * 16;

            trail = new ThunderTrail(CWRAsset.ThunderTrail, ThunderWidthFunc_Sin, ThunderColorFunc, GetAlpha) {
                CanDraw = true,
                UseNonOrAdd = true,
                PartitionPointCount = 3,
                BasePositions =
                [
                    Projectile.Center,Projectile.Center,Projectile.Center
                ]
            };
            trail.SetRange((0, 7));
            trail.SetExpandWidth(7);
        }

        public static bool GetNPCOwner(int index, out NPC owner, Action notExistAction = null) {
            if (!Main.npc.IndexInRange(index)) {
                notExistAction?.Invoke();
                owner = null;
                return false;
            }

            NPC npc = Main.npc[index];
            if (!npc.active) {
                notExistAction?.Invoke();
                owner = null;
                return false;
            }

            owner = npc;
            return true;
        }

        public virtual void Chase() {
            Timer++;
            Vector2 targetCenter = TargetCenter;

            if (GetNPCOwner(NPCIndex, out NPC target)) {
                float speed = Projectile.velocity.Length();
                //距离目标点近了就换一个
                if (Projectile.Center.Distance(targetCenter) < speed * 4) {
                    if (Projectile.Center.Distance(target.Center) < speed * 10) {
                        targetCenter = target.Center;
                        TargetCenter = target.Center;
                    }
                    else {
                        Vector2 dir2 = target.Center - Projectile.Center;
                        float length2 = dir2.Length();
                        if (length2 > 100)
                            length2 = 100;
                        dir2 = dir2.SafeNormalize(Vector2.Zero);
                        Vector2 center2 = Projectile.Center + dir2 * length2;
                        Vector2 pos = center2 + dir2.RotatedBy(Main.rand.NextFromList(1.57f, -1.57f)) * length2;//Main.rand.NextVector2Circular(length2,length2);

                        targetCenter = pos;
                        TargetCenter = pos;
                        Projectile.velocity = (targetCenter - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
                    }
                    Projectile.netUpdate = true;
                }
            }
            else {
                Fade();
                return;
            }

            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (targetCenter - Projectile.Center).ToRotation();

            float factor = 1 - Math.Clamp(Vector2.Distance(targetCenter, Projectile.Center) / 500, 0, 1);

            Projectile.velocity = selfAngle.AngleLerp(targetAngle, 0.5f + 0.5f * factor).ToRotationVector2() * 24f;

            if (Main.rand.NextBool(2)) {
                Projectile.SpawnTrailDust(DustID.Electric, Main.rand.NextFloat(0.1f, 0.3f), Scale: Main.rand.NextFloat(0.4f, 0.8f));
            }

            if (trail != null && trailList != null) {
                trailList.AddLast(Projectile.Center);

                if (Timer % Projectile.MaxUpdates == 0) {
                    trail.BasePositions = [.. trailList];//消失的时候不随机闪电
                    trail.RandomThunder();
                }
            }
        }

        public void Fade() {
            if (State == 0) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;
            Projectile.extraUpdates = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Hited = 1;
            Timer = 0;
            State = 2;
            //入淡出沿一次性同步（State/Timer 随包出门），替代原先淡出期的逐拍 netUpdate
            Projectile.netUpdate = true;

            if (trail != null && trailList != null) {
                trail.BasePositions = [.. trailList];
                if (trail.BasePositions.Length > 3)
                    trail.RandomThunder();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Fade();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Fade();

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, VaultUtils.RandVr(5));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //没碰到任何东西就绘制本体
            if (Hited == 0) {
                Texture2D mainTex = TextureAssets.Projectile[Type].Value;

                Color c = Lighting.GetColor(Projectile.Center.ToTileCoordinates(), new Color(103, 255, 255));
                c.A = 0;
                c *= Alpha;

                Vector2 position = Projectile.Center - Main.screenPosition;

                Main.spriteBatch.Draw(mainTex, position, null, c, 0,
                    mainTex.Size() / 2, 0.15f, 0, 0);

                Texture2D exTex = CWRAsset.StarTexture.Value;

                Vector2 origin = exTex.Size() / 2;
                Main.spriteBatch.Draw(exTex, position, null, c, 0, origin, 0.5f, 0, 0);

                c = lightColor;
                c.A = 0;
                c *= Alpha;
                Main.spriteBatch.Draw(exTex, position, null, c, 0, origin, 0.2f, 0, 0);
            }

            if (State > 0) {
                if (State == 1 && Timer < 3) {
                    return false;
                }

                trail?.DrawThunder(Main.instance.GraphicsDevice);
            }

            return false;
        }
    }

    internal class TeslaBallByGuard : TeslaBallByAttack
    {
        private Player TargetPlayer { get; set; }
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void Chase() {
            Timer++;
            Vector2 targetCenter = TargetCenter;

            if (TargetPlayer != null) {
                float speed = Projectile.velocity.Length();
                //距离目标点近了就换一个
                if (Projectile.Center.Distance(targetCenter) < speed * 4) {
                    if (Projectile.Center.Distance(TargetPlayer.Center) < speed) {
                        targetCenter = TargetPlayer.Center;
                        TargetCenter = TargetPlayer.Center;
                        State = 2;
                        //入淡出沿一次性同步，淡出期不再逐拍发包
                        Projectile.netUpdate = true;
                    }
                    else {
                        Vector2 dir2 = TargetPlayer.Center - Projectile.Center;
                        float length2 = dir2.Length();
                        if (length2 > 100) {
                            length2 = 100;
                        }

                        dir2 = dir2.SafeNormalize(Vector2.Zero);
                        Vector2 center2 = Projectile.Center + dir2 * length2;
                        Vector2 pos = center2 + dir2.RotatedBy(Main.rand.NextFromList(1.57f, -1.57f)) * length2;

                        targetCenter = pos;
                        TargetCenter = pos;
                        Projectile.velocity = (targetCenter - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
                    }
                }
            }
            else {
                Fade();
                return;
            }

            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (targetCenter - Projectile.Center).ToRotation();

            float factor = 1 - Math.Clamp(Vector2.Distance(targetCenter, Projectile.Center) / 500, 0, 1);

            Projectile.velocity = selfAngle.AngleLerp(targetAngle, 0.5f + 0.5f * factor).ToRotationVector2() * 24f;

            if (Main.rand.NextBool(6)) {
                Projectile.SpawnTrailDust(DustID.Electric, Main.rand.NextFloat(0.1f, 0.3f), Scale: Main.rand.NextFloat(0.4f, 0.8f));
            }

            if (trail != null && trailList != null) {
                trailList.AddLast(Projectile.Center);

                if (Timer % Projectile.MaxUpdates == 0) {
                    trail.BasePositions = [.. trailList];//消失的时候不随机闪电
                    trail.RandomThunder();
                }
            }
        }

        private void HandlerPlayerCharge() {
            for (int i = 0; i < 3; i++) {
                Vector2 spanPos = Owner.position + new Vector2(Main.rand.Next(Owner.width), Main.rand.Next(Owner.height));
                PRTLoader.NewParticle<PRT_TileHightlight>(spanPos, Vector2.Zero, Color.White);
            }

            float singleCharge = 0.1f;
            Item handItem = Owner.GetItem();
            if (handItem.type > ItemID.None) {
                if (handItem.GetItemUsesCharge() && handItem.GetItemCharge() < handItem.GetItemMaxCharge()) {
                    handItem.SetItemCharge(MathHelper.Clamp(handItem.GetItemCharge() + singleCharge, 0, handItem.GetItemMaxCharge()));
                }
            }
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Color(103, 255, 255).ToVector3());
            //生成后以极快的速度前进
            switch (State) {
                default:
                case 0://刚生成，等待透明度变高后开始寻敌
                    Player player = VaultUtils.FindClosestPlayer(Projectile.Center, 800);
                    if (player != null) {
                        TargetPlayer = player;
                        TargetCenter = Projectile.Center + Projectile.velocity.UnitVector() * 126;
                        StartAttack();
                    }
                    else {
                        Projectile.Kill();
                    }
                    break;
                case 1://找到敌人，以极快的速度追踪
                    Chase();
                    break;
                case 2://后摇，闪电逐渐消失
                    Timer++;
                    FadeValue = Smoother((int)Timer, 30);
                    ThunderWidth = Smoother(60 - (int)Timer, 60) * 14;

                    if (Timer > 30) {
                        HandlerPlayerCharge();
                        Projectile.Kill();
                    }
                    break;
            }
        }
    }
}
