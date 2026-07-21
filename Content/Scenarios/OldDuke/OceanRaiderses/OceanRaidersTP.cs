using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Scenarios.OldDuke.OceanRaiderses.OceanRaidersUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OceanRaiderses
{
    internal class OceanRaidersTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<OceanRaidersTile>();
        public override int TargetItem => ModContent.ItemType<OceanRaiders>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 1200;

        internal const int consumeUE = 8;
        internal const int fishingTime = 12;
        internal const int maxStorageSlots = 340;

        internal int frame;
        internal int fishingTimer;
        internal int particleTimer;
        internal int textTimer;
        internal bool isWorking;
        internal bool hasWater;
        internal float glowIntensity;
        internal Vector2 intakeCenter;

        internal List<Item> storedItems = new();

        internal List<FishingParticle> fishingParticles = new();

        private OceanRaidersVortexEffect vortexEffect;

        private SlotId vortexSoundSlot;
        private SoundStyle vortexSoundStyle = new SoundStyle(CWRConstant.Asset + "Sounds/RollingMERoer") {
            IsLooped = true,
            MaxInstances = 8,
            Volume = 0.6f
        };

        /// <summary>过滤名单；历史=黑名单，旧档迁移保持</summary>
        internal ItemFilterSet Filter = new();
        internal float hoverSengs;

        public override void SetBattery() {
            storedItems = new List<Item>();
            fishingParticles = new List<FishingParticle>();
            vortexEffect = new OceanRaidersVortexEffect(this);
            Filter = new ItemFilterSet();
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }
            storedItems ??= [];
            foreach (var i in storedItems) {
                if (i.Alives()) {
                    VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox, i);
                }
            }
            storedItems.Clear();
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(isWorking);
            data.Write(hasWater);
            data.Write(fishingTimer);
            Filter.Write(data);

            data.Write(storedItems.Count);
            foreach (var item in storedItems) {
                if (item == null) {
                    ItemIO.Send(new Item(), data, true);
                }
                else {
                    ItemIO.Send(item, data, true);
                }
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            isWorking = reader.ReadBoolean();
            hasWater = reader.ReadBoolean();
            fishingTimer = reader.ReadInt32();
            Filter.Read(reader);

            int count = reader.ReadInt32();
            storedItems.Clear();
            for (int i = 0; i < count; i++) {
                Item item = ItemIO.Receive(reader, true);
                storedItems.Add(item);
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            try {
                Filter.Save(tag, "_Filter");

                List<TagCompound> itemTags = new();
                foreach (var item in storedItems) {
                    if (item == null) {
                        itemTags.Add(ItemIO.Save(new Item()));
                    }
                    else {
                        itemTags.Add(ItemIO.Save(item));
                    }
                }
                tag["itemTags"] = itemTags;
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"OceanRaidersTP.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                //旧档过滤卡→黑名单
                if (!Filter.TryLoad(tag, "_Filter")) {
                    Item legacyCard = CWRSaveData.LoadItemFromTag(tag, "_ItemFilter", nameof(OceanRaidersTP));
                    if (legacyCard.ModItem is ItemFilter card) {
                        Filter.CopyFrom(card.Filter.OrderedItems, ItemFilterMode.Blacklist);
                    }
                }

                if (!tag.TryGet("itemTags", out List<TagCompound> itemTags)) {
                    return;
                }

                storedItems.Clear();
                foreach (var itemTag in itemTags) {
                    storedItems.Add(CWRSaveData.LoadItemTag(itemTag, $"{nameof(OceanRaidersTP)}:itemTags"));
                }
            } catch (Exception ex) {
                VaultMod.Instance.Logger.Error($"OceanRaidersTP.LoadData Error: {ex.Message}");
            }
        }

        private bool CheckWaterBelow() {
            int checkDistance = 32;
            Point startPoint = (Position + new Point16(3, 6)).ToPoint();

            for (int y = 0; y < checkDistance; y++) {
                for (int x = -2; x <= 2; x++) {
                    Tile tile = Framing.GetTileSafely(startPoint.X + x, startPoint.Y + y);
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsItemFiltered(int itemType) => !Filter.Matches(itemType);

        private void PerformFishing() {
            if (VaultUtils.isClient) return;

            //渔力=能量+波动
            int power = 50 + Main.rand.Next(30);
            if (MachineData.UEvalue > MaxUEValue * 0.8f) {
                power += 20;
            }

            int caughtItem = GetFishingLoot(power);
            if (caughtItem <= ItemID.None) return;

            //过滤器命中则丢
            if (IsItemFiltered(caughtItem)) return;

            int stack;

            //箱子只给1
            if (ItemID.Sets.IsFishingCrate[caughtItem]) {
                stack = 1;
            }
            //鱼1-3
            else {
                stack = Main.rand.Next(1, 4);
            }

            if (ContentSamples.ItemsByType.TryGetValue(caughtItem, out var item)) {
                stack = (int)MathHelper.Clamp(stack, 1, item.maxStack);
            }

            AddItemToStorage(caughtItem, stack);
        }

        private static int GetFishingLoot(int fishingPower) {
            //箱子 基础10%+渔力
            int crateChance = 10;
            if (fishingPower > 100) crateChance += 10;
            if (Main.rand.Next(100) < crateChance) {
                if (fishingPower > 90 && Main.rand.NextBool(5)) return ItemID.GoldenCrate;
                if (fishingPower > 50 && Main.rand.NextBool(3)) return ItemID.IronCrate;
                return ItemID.WoodenCrate;
            }

            //稀有 渔力>80
            if (fishingPower > 80 && Main.rand.NextBool(25)) {
                int[] rares = {
                    ItemID.Swordfish,
                    ItemID.Sextant,
                    ItemID.ReaverShark,
                    ItemID.SawtoothShark,
                    ItemID.Rockfish,
                    ItemID.PurpleClubberfish
                };
                return rares[Main.rand.Next(rares.Length)];
            }

            if (Main.rand.NextBool(12)) {
                return ModContent.ItemType<Oceanfragments>();
            }

            List<int> commons = new() {
                ItemID.Bass,
                ItemID.Trout,
                ItemID.AtlanticCod,
                ItemID.RedSnapper,
                ItemID.Tuna,
                ItemID.Shrimp,
                ItemID.Flounder
            };

            if (fishingPower > 30) {
                commons.Add(ItemID.NeonTetra);
                commons.Add(ItemID.ArmoredCavefish);
                commons.Add(ItemID.DoubleCod);
                commons.Add(ItemID.Damselfish);
                commons.Add(ItemID.FrostMinnow);
            }

            //垃圾 渔力越低越高
            if (fishingPower < 50 && Main.rand.NextBool(10)) {
                int[] junk = { ItemID.OldShoe, ItemID.TinCan, ItemID.FishingSeaweed };
                return junk[Main.rand.Next(junk.Length)];
            }

            return commons[Main.rand.Next(commons.Count)];
        }

        private void AddItemToStorage(int itemType, int stack) {
            foreach (var item in storedItems) {
                if (item.type == itemType && item.stack < item.maxStack) {
                    int addAmount = Math.Min(stack, item.maxStack - item.stack);
                    item.stack += addAmount;
                    stack -= addAmount;
                    if (stack <= 0) {
                        SendData();
                        return;
                    }
                }
            }

            if (storedItems.Count < maxStorageSlots && stack > 0) {
                Item newItem = new Item();
                newItem.SetDefaults(itemType);
                newItem.stack = stack;
                storedItems.Add(newItem);
                SendData();
            }
        }

        private void TransferItemsToChest() {
            if (storedItems.Count == 0) return;

            Chest chest = Position.FindClosestChest(800, false);
            if (chest != null) {
                Vector2 chestPos = new Vector2(chest.x * 16 + 16, chest.y * 16 + 16);

                for (int i = storedItems.Count - 1; i >= 0; i--) {
                    Item item = storedItems[i];
                    if (!chest.CanItemBeAddedToChest(item)) {
                        continue;
                    }

                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(
                            this.FromObjectGetParent(),
                            intakeCenter,
                            Vector2.Zero,
                            ModContent.ProjectileType<TransferItemProj>(),
                            0, 0, -1, item.type, chestPos.X, chestPos.Y
                        );
                    }
                    chest.eatingAnimationTime = 30;
                    chest.AddItem(item, true);
                    storedItems.RemoveAt(i);
                    if (storedItems.Count == 0) {
                        break;
                    }
                }

                if (storedItems.Count == 0) {
                    SendData();
                }
            }
        }

        private void UpdateParticles() {
            if (VaultUtils.isServer) return;

            for (int i = fishingParticles.Count - 1; i >= 0; i--) {
                fishingParticles[i].Update(intakeCenter);
                if (fishingParticles[i].ShouldRemove()) {
                    fishingParticles.RemoveAt(i);
                }
            }

            if (isWorking && particleTimer++ % 8 == 0 && fishingParticles.Count < 20) {
                SpawnFishingParticle();
            }
        }

        private void SpawnFishingParticle() {
            Vector2 waterSurfacePos = FindWaterSurface();
            if (waterSurfacePos == Vector2.Zero) return;

            Vector2 spawnPos = waterSurfacePos + new Vector2(
                Main.rand.NextFloat(-70f, 70f),
                Main.rand.NextFloat(40f, 120f)
            );

            //须在水中
            Tile tile = Framing.GetTileSafely(spawnPos);
            if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                FishingParticle particle = new FishingParticle {
                    Position = spawnPos,
                    Type = (FishingParticleType)Main.rand.Next(3),
                    Scale = Main.rand.NextFloat(0.6f, 1.2f),
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    Life = Main.rand.Next(260, 300)
                };

                fishingParticles.Add(particle);
            }
        }

        private Vector2 FindWaterSurface() {
            Point startPoint = (Position + new Point16(3, 6)).ToPoint();

            for (int y = 0; y < 32; y++) {
                for (int x = -2; x <= 2; x++) {
                    Tile tile = Framing.GetTileSafely(startPoint.X + x, startPoint.Y + y);
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                        return new Vector2(
                            (startPoint.X + x) * 16 + 8,
                            (startPoint.Y + y) * 16 + 8
                        );
                    }
                }
            }

            return Vector2.Zero;
        }

        private bool LoopingSoundUpdate(ActiveSound soundInstance) {
            float workIntensity = isWorking ? glowIntensity : 0f;
            soundInstance.Pitch = (-0.3f + workIntensity * 0.6f) * 1.8f;
            soundInstance.Position = intakeCenter;
            soundInstance.Volume = workIntensity * 0.8f;
            return Active && hasWater;
        }

        private void UpdateSoundEffects() {
            if (VaultUtils.isServer) return;

            if (isWorking && hasWater) {
                if (!SoundEngine.TryGetActiveSound(vortexSoundSlot, out var activeSound)) {
                    vortexSoundSlot = SoundEngine.PlaySound(vortexSoundStyle, intakeCenter, LoopingSoundUpdate);
                }
            }
        }

        public override void UpdateMachine() {
            intakeCenter = CenterInWorld + new Vector2(0, 32);

            hoverSengs = HoverTP
                ? Math.Min(hoverSengs + 0.1f, 1f)
                : Math.Max(hoverSengs - 0.1f, 0f);

            frame = 0;
            hasWater = CheckWaterBelow();

            if (MachineData.UEvalue < consumeUE || !hasWater) {
                isWorking = false;
                glowIntensity = Math.Max(0, glowIntensity - 0.05f);

                if (!VaultUtils.isServer && ++textTimer > 180) {
                    string text = !hasWater ? OceanRaiders.NoWaterText.Value : OceanRaiders.NoEnergyText.Value;
                    CombatText.NewText(HitBox, Color.Orange, text, false);
                    textTimer = 0;
                }

                UpdateParticles();
                vortexEffect?.Update();
                UpdateSoundEffects();
                return;
            }

            isWorking = true;
            glowIntensity = Math.Min(1f, glowIntensity + 0.05f);

            if (++fishingTimer >= fishingTime) {
                fishingTimer = 0;
                MachineData.UEvalue -= consumeUE;

                PerformFishing();

                if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                    SoundEngine.PlaySound(SoundID.Splash with {
                        Pitch = -0.2f,
                        Volume = 0.5f
                    }, intakeCenter);

                    if (Main.rand.NextBool(3)) {
                        SoundEngine.PlaySound(SoundID.Item21 with {
                            Pitch = Main.rand.NextFloat(-0.3f, 0.1f),
                            Volume = 0.3f
                        }, intakeCenter);
                    }
                }
            }

            if (storedItems.Count > 0 && Main.GameUpdateCount % 120 == 0) {
                TransferItemsToChest();
            }

            UpdateParticles();

            vortexEffect?.Update();

                UpdateSoundEffects();
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            vortexEffect?.DrawVortex(spriteBatch);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (VaultUtils.isServer) return;

            foreach (var particle in fishingParticles) {
                particle.Draw(spriteBatch);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();

            if (!Filter.IsEmpty && hoverSengs > 0.01f) {
                IReadOnlyList<int> filterItems = Filter.OrderedItems;
                const float maxRadius = 150f;
                float currentRadius = maxRadius * hoverSengs;
                float angleIncrement = MathHelper.TwoPi / filterItems.Count;

                Vector2 drawCenter = CenterInWorld - Main.screenPosition + new Vector2(0, 32);
                //黑名单警示红
                Color modeTint = Filter.Mode == ItemFilterMode.Whitelist
                    ? Color.White
                    : ItemFilterTheme.AccentBlacklist;

                for (int i = 0; i < filterItems.Count; i++) {
                    int itemType = filterItems[i];
                    if (itemType <= ItemID.None) continue;

                    float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                    Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * currentRadius;
                    Vector2 itemPos = drawCenter + offset;

                    Color drawColor = VaultUtils.MultiStepColorLerp(hoverSengs, Lighting.GetColor(Position.ToPoint()), modeTint);
                    float scale = hoverSengs * 1.25f;

                    VaultUtils.SafeLoadItem(itemType);
                    VaultUtils.SimpleDrawItem(Main.spriteBatch, itemType, itemPos, itemWidth: 32, scale, 0, drawColor);
                }

                VaultUtils.SimpleDrawItem(Main.spriteBatch, ModContent.ItemType<ItemFilter>()
                    , CenterInWorld - Main.screenPosition, itemWidth: 32, 1f, 0, Color.White);
            }
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            Item item = player.GetItem();

            //手持过滤卡→装名单
            if (item.ModItem is ItemFilter card) {
                Filter.CopyFrom(card.Filter);

                SoundEngine.PlaySound(CWRSound.Select);
                if (!VaultUtils.isServer) {
                    CombatText.NewText(HitBox, ItemFilterTheme.Gold, ItemFilterEditorUI.InstalledText.Value);
                }
                SendData();
                return true;
            }

            if (player.whoAmI == Main.myPlayer) {
                OceanRaidersUI.Instance.Interactive(this);
            }
            return true;
        }
    }

    internal class TransferItemProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 120;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Vector2 targetPos = new Vector2(Projectile.ai[1], Projectile.ai[2]);

            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = 1;
                Projectile.alpha = 0;
                Projectile.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -4f);
            }

            Vector2 toTarget = targetPos - Projectile.Center;
            float dist = toTarget.Length();

            if (dist < 16f) {
                Projectile.Kill();
                return;
            }

            float speed = Math.Min(dist / 5f, 20f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * speed, 0.1f);

            Projectile.rotation += 0.2f;
        }

        public override bool PreDraw(ref Color lightColor) {
            int itemType = (int)Projectile.ai[0];
            if (itemType <= 0) return false;

            Main.instance.LoadItem(itemType);
            Texture2D texture = TextureAssets.Item[itemType].Value;

            if (texture != null) {
                Rectangle rect = Main.itemAnimations[itemType] != null
                    ? Main.itemAnimations[itemType].GetFrame(texture)
                    : texture.Frame();

                Vector2 origin = rect.Size() / 2f;
                float scale = 0.7f;

                Main.EntitySpriteDraw(
                    texture,
                    Projectile.Center - Main.screenPosition,
                    rect,
                    lightColor,
                    Projectile.rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }
    }
}