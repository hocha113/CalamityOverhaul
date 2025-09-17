using CalamityMod.NPCs.Crabulon;
using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Tools;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
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
        public static Asset<Texture2D> SleepTruffle;
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
        public override bool CanOverride() => !Main.hardMode;//只在肉前生效

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
            if (Sleep) {
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
            return true;
        }

        public override void ModifyActiveShop(string shopName, Item[] items) {//去他妈的模组兼容性
            for (int i = 0; i < items.Length; i++) {
                Item item = items[i];
                if (!item.Alives()) {
                    continue;
                }
                item.TurnToAir();

                if (i == 0) {
                    items[i] = new Item(ItemID.GlowingMushroom) {
                        value = 1000
                    };
                }
                else if (i == 1) {
                    items[i] = new Item(ItemID.MushroomGrassSeeds) {
                        value = 55000
                    };
                }
                else if (i == 2) {
                    items[i] = new Item(ItemID.MushroomDye) {
                        value = 10000
                    };
                }
                else if (i == 3) {
                    items[i] = new(ModContent.ItemType<FungalFeed>()) {
                        value = 150000
                    };
                }

                if (!Main.LocalPlayer.ZoneGlowshroom) {
                    continue;
                }

                if (i == 4) {
                    items[i] = new Item(ModContent.ItemType<SporeBubbleBlaster>()) {
                        value = 80000
                    };
                }
            }
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

            WeightedRandom<string> randomChat = new WeightedRandom<string>();
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
