using CalamityMod.Items.SummonItems;
using CalamityMod.NPCs.Crabulon;
using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Tools;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.NPCs.Modifys
{
    internal class SleepTruffle : ModNPC
    {
        public const int MaxChatSlot = 12;
        public readonly static List<LocalizedText> Chats = [];
        public override string Texture => CWRConstant.NPC + "SleepTruffle";
        private int frame;
        private int chatEllipsis;
        public override void SetStaticDefaults() {
            for (int i = 0; i < MaxChatSlot; i++) {
                Chats.Add(this.GetLocalization($"Chat{i}", () => ""));
            }

            NPCID.Sets.NPCBestiaryDrawModifiers value = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
            NPCID.Sets.NoTownNPCHappiness[Type] = true;//这个东西可以对话，但是不算城镇NPC
            Main.npcFrameCount[NPC.type] = 2;
        }

        public override void SetDefaults() {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 56;
            NPC.height = 10;//高度稍微矮一些，这样才能接触到地面
            NPC.alpha = 0;
            NPC.npcSlots = 0;
            NPC.aiStyle = -1;
            NPC.lifeMax = 250;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.knockBackResist = 0;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.noGravity = true;
        }

        public override bool CanGoToStatue(bool toKingStatue) => false;

        public override bool UsesPartyHat() => false;

        public override void SetChatButtons(ref string button, ref string button2) => button = "叫醒";

        public override string GetChat() => "Zzzzzz...";

        public override void OnChatButtonClicked(bool firstButton, ref string shopName) {
            if (!firstButton) {
                return;
            }

            if (!NPC.AnyNPCs(NPCID.Truffle)) {
                NPC truffle = NPC.NewNPCDirect(NPC.FromObjectGetParent(), NPC.Center, NPCID.Truffle);
                truffle.velocity = new Vector2(NPC.To(Main.LocalPlayer.Center).UnitVector().X * Main.rand.NextFloat(3), -6);
                truffle.direction = truffle.spriteDirection = Math.Sign(truffle.velocity.X);
            }
            else {
                SoundEngine.PlaySound(SoundID.NPCDeath1);
                for (int i = 0; i < 43; i++) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GlowingMushroom, Main.rand.NextFloat(-2, 2), -2);
                }
            }

            NPC.active = false;
        }

        public override void AI() {
            NPC.velocity.Y += 0.12f;
            NPC.velocity.X *= 0.98f;

            if (NPC.velocity.X > 0.1f) {
                NPC.direction = NPC.spriteDirection = Math.Sign(NPC.velocity.X);
            }
            else {
                NPC.velocity.X = 0;
            }

            VaultUtils.ClockFrame(ref frame, 20, 1);
            VaultUtils.ClockFrame(ref chatEllipsis, 10, 6);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            var effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Texture2D value = TextureAssets.Npc[NPC.type].Value;
            Rectangle rectangle = value.GetRectangle(frame, 2);
            spriteBatch.Draw(TextureAssets.Npc[NPC.type].Value, NPC.Center - screenPos, rectangle
                , NPC.GetAlpha(drawColor), NPC.rotation, rectangle.Size() / 2, NPC.scale, effects, 0);
            return false;
        }
    }

    internal class ModifyTruffle : NPCOverride
    {
        public override int TargetID => NPCID.Truffle;
        public bool FirstChat;
        public override bool CanOverride() => !Main.hardMode;//只在肉前生效
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
            if (!FirstChat) {
                FirstChat = true;

                if (npc.homeless) {//只在没有住房的情况下触发这个台词
                    chat = SleepTruffle.Chats[0].Value;
                    return;
                }
            }

            if (Main.bloodMoon) {
                chat = SleepTruffle.Chats[11].Value;
                return;
            }

            WeightedRandom<string> randomChat = new WeightedRandom<string>();
            for (int i = 0; i < 9; i++) {
                randomChat.Add(SleepTruffle.Chats[i].Value);
            }

            if (ModLoader.HasMod("AAMod")) {
                randomChat.Add(SleepTruffle.Chats[10].Value);
            }

            chat = randomChat;
        }
    }
}
