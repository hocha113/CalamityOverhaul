using CalamityMod.NPCs.Crabulon;
using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Tools;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.NPCs.Modifys
{
    internal class ModifyTruffle : NPCOverride, ILocalizedModType
    {
        [VaultLoaden(CWRConstant.NPC + "SleepTruffle")]
        public static Asset<Texture2D> SleepTruffle = null;
        public const int MaxChatSlot = 12;
        public static LocalizedText ButtonText { get; set; }
        public readonly static List<LocalizedText> Chats = [];
        /// <summary>
        /// 全局睡眠设置，在蘑菇人生成时会采用这个的值，用于在生成NPC时临时设置进行赋值，一次生成后自动恢复为false
        /// </summary>
        public static bool GlobalSleepState = false;
        public override int TargetID => NPCID.Truffle;
        public string LocalizationCategory => "NPCModifys";
        private int frame;
        public bool Sleep;
        public bool FirstChat;

        public override void SetStaticDefaults() {
            ButtonText = this.GetLocalization(nameof(ButtonText), () => "Awaken");
            for (int i = 0; i < MaxChatSlot; i++) {
                Chats.Add(this.GetLocalization($"Chat{i}", () => ""));
            }
        }

        public void SetNPCDefault() {
            if (Sleep) {
                npc.townNPC = true;
                npc.friendly = true;
                npc.width = 56;
                npc.height = 10;//高度稍微矮一些，这样才能接触到地面
                npc.aiStyle = -1;
                npc.damage = 0;
                npc.defense = 0;
                npc.lifeMax = 250;
                npc.HitSound = null;
                npc.DeathSound = null;
                npc.knockBackResist = 0;
                npc.npcSlots = 0;
                npc.dontTakeDamage = true;
                npc.immortal = true;
                npc.noGravity = true;
            }
            else {
                npc.townNPC = true;
                npc.friendly = true;
                npc.width = 18;
                npc.height = 40;
                npc.aiStyle = 7;
                npc.damage = 10;
                npc.defense = 15;
                npc.lifeMax = 250;
                npc.HitSound = SoundID.NPCHit1;
                npc.DeathSound = SoundID.NPCDeath1;
                npc.knockBackResist = 0.5f;
                npc.npcSlots = 1;
                npc.dontTakeDamage = false;
                npc.immortal = false;
                npc.noGravity = false;
            }
        }

        public override void SetProperty() {
            Sleep = GlobalSleepState;
            SetNPCDefault();
            GlobalSleepState = false;
            FirstChat = true;//设置为第一次对话的待定
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!Sleep) {
                return null;
            }
            var effects = npc.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D value = SleepTruffle.Value;
            Rectangle rectangle = value.GetRectangle(frame, 2);
            spriteBatch.Draw(value, npc.Center - screenPos, rectangle, npc.GetAlpha(drawColor)
                , npc.rotation, rectangle.Size() / 2, npc.scale, effects, 0);
            return false;
        }

        public override bool? CanGoToStatue(bool toKingStatue) {
            if (Sleep) {
                return false;
            }
            return null;
        }

        public override bool? PreUsesPartyHat() {
            if (Sleep) {
                return false;
            }
            return null;
        }

        public override bool SetChatButtons(ref string button, ref string button2) {
            if (Sleep) {
                button = ButtonText.Value;
            }
            return true;
        }

        public override void OnChatButtonClicked(bool firstButton) {
            if (!Sleep || !firstButton) {
                return;
            }

            npc.direction = ((int)npc.To(Main.LocalPlayer.Center).UnitVector().X);
            npc.velocity = new Vector2(npc.direction * 3, -8);//给NPC一个向上弹起的速度

            Sleep = false;
            SetNPCDefault();
        }

        public override bool AI() {
            if (!Sleep) {
                return true;
            }

            npc.velocity.Y += 0.12f;
            npc.velocity.X *= 0.98f;

            if (npc.velocity.X > 0.1f) {
                npc.direction = npc.spriteDirection = Math.Sign(npc.velocity.X);
            }
            else {
                npc.velocity.X = 0;
            }

            VaultUtils.ClockFrame(ref frame, 20, 1);
            return false;
        }

        public override void ModifyActiveShop(string shopName, Item[] items) {//去他妈的模组兼容性
            List<Item> origItems = [];//原生的物品将被存储于此
            for (int i = 0; i < items.Length; i++) {
                Item item = items[i];
                if (!item.Alives()) {
                    continue;
                }
                origItems.Add(item.Clone());
                item.TurnToAir();
            }

            int index = 0;
            items[index] = new Item(ItemID.GlowingMushroom) {
                value = 1000
            };
            index++;
            items[index] = new Item(ItemID.MushroomGrassSeeds) {
                value = 55000
            };
            index++;
            items[index] = new(ModContent.ItemType<FungalFeed>()) {
                value = 150000
            };
            index++;

            if (Main.LocalPlayer.ZoneGlowshroom) {
                items[index] = new Item(ModContent.ItemType<SporeBubbleBlaster>()) {
                    value = 80000
                };
                index++;
                items[index] = new Item(ModContent.ItemType<TomeofFungalDecay>()) {
                    value = 82000
                };
                index++;
                items[index] = new Item(ModContent.ItemType<SporeboundRoller>()) {
                    value = 70000
                };
                index++;
                if (NPC.downedBoss3) {
                    items[index] = new Item(ModContent.ItemType<FungalRevolver>()) {
                        value = 120000
                    };
                    index++;
                }
                if (Main.LocalPlayer.inventory.Any(item => item.type == ItemID.SlimySaddle)) {
                    items[index] = new Item(ModContent.ItemType<MushroomSaddle>()) {
                        value = 170000
                    };
                    index++;
                }
            }

            if (!Main.hardMode) {
                items[index] = new Item(ItemID.MushroomDye) {
                    value = 10000
                };
                index++;
                return;//在肉后才把原生物品添加回去
            }

            foreach (var item in origItems) {
                items[index] = item.Clone();
                index++;
            }

            items[index] = new Item(ItemID.MushroomDye) {
                value = 10000
            };
            index++;
        }

        public override bool? CanBeHitByNPC(NPC attacker) {
            if (npc.type == ModContent.NPCType<Crabulon>() || npc.type == ModContent.NPCType<CrabShroom>()) {
                return false;
            }
            return null;
        }

        public override bool? CanBeHitByProjectile(Projectile projectile) {
            if (projectile.type == ModContent.ProjectileType<MushBomb>() || projectile.type == ModContent.ProjectileType<MushBombFall>()) {
                return false;
            }

            //下下策，判断生成源
            if (projectile.TryGetGlobalProjectile<CWRProjectile>(out var gProj)
                && gProj.Source != null
                && gProj.Source is EntitySource_Parent entitySource
                && entitySource.Entity is NPC boss
                && boss.type == ModContent.NPCType<Crabulon>()) {
                return false;
            }
            return null;
        }

        public override void GetChat(ref string chat) {
            if (Sleep) {
                chat = "Zzzzzz...";
                return;
            }

            if (Main.hardMode && Main.rand.NextBool()) {
                return;//下面的对话在肉后有50%概率生效
            }

            if (FirstChat) {
                FirstChat = false;//完成了第一次对话

                if (npc.homeless) {//只在没有住房的情况下加第一次对话情况下必定触发这个台词
                    chat = Chats[0].Value;
                    return;
                }
            }

            if (Main.bloodMoon) {
                chat = Chats[11].Value;
                return;
            }

            WeightedRandom<string> randomChat = new();
            for (int i = 0; i < 9; i++) {
                randomChat.Add(Chats[i].Value);
            }

            if (ModLoader.HasMod("AAMod")) {
                randomChat.Add(Chats[10].Value);
            }

            chat = randomChat;
        }
    }
}
