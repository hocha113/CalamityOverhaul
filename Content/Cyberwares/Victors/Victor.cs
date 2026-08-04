using CalamityOverhaul.Content.Cyberwares.Victors.UIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// 城镇 Victor；Passive 行走；对话/诊所走 <see cref="VictorTalkUI"/> / <see cref="UIs.VictorClinicUI"/>
    /// </summary>
    [AutoloadHead]
    internal class Victor : ModNPC
    {
        /// <summary>Victor.png 共 10 帧，0 站立</summary>
        public const int FrameCount = 10;

        /// <summary>绘制脚部 Y 微调，正值向下</summary>
        private const float DrawVerticalOffset = 2f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = FrameCount;

            //城镇集合，不主动攻击
            NPCID.Sets.ExtraFramesCount[Type] = 0;
            NPCID.Sets.AttackFrameCount[Type] = 0;
            NPCID.Sets.DangerDetectRange[Type] = 200;
            NPCID.Sets.AttackType[Type] = -1;
            NPCID.Sets.AttackTime[Type] = -1;
            NPCID.Sets.AttackAverageChance[Type] = 0;
            NPCID.Sets.HatOffsetY[Type] = 4;
            NPCID.Sets.PrettySafe[Type] = 300;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() {
                Velocity = 1f,
                Direction = 1,
            };
            NPCID.Sets.NPCBestiaryDrawOffset.TryAdd(Type, drawModifiers);
        }

        public override void SetDefaults() {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 36;
            NPC.height = 50;
            NPC.aiStyle = NPCAIStyleID.Passive;//Passive 行走/住房/逃跑
            NPC.damage = 10;
            NPC.defense = 52;
            NPC.lifeMax = 2250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.Victor.Bestiary"),
            ]);
        }

        /// <summary>禁用原版 spawn，改走 <see cref="VictorPortalSpawner"/></summary>
        public override bool CanTownNPCSpawn(int numTownNPCs) => false;

        public override List<string> SetNPCNameList() => [
            Language.GetTextValue("Mods.CalamityOverhaul.NPCs.Victor.Name0"),
        ];

        //禁用原版聊天，交互走 VictorTalkUI 右键
        public override bool CanChat() => false;

        public override void FindFrame(int frameHeight) {
            if (!NPC.IsABestiaryIconDummy && NPC.direction != 0) {
                NPC.spriteDirection = NPC.direction;
            }

            //图鉴木偶循环 1..9
            if (NPC.IsABestiaryIconDummy) {
                NPC.frameCounter += 0.18f;
                NPC.frameCounter %= FrameCount - 1;
                NPC.frame.Y = (1 + (int)NPC.frameCounter) * frameHeight;
                return;
            }

            if (Math.Abs(NPC.velocity.X) < 0.1f) {
                NPC.frameCounter = 0;
                NPC.frame.Y = 0;
                return;
            }

            //移动 1..9，越快越快
            NPC.frameCounter += Math.Abs(NPC.velocity.X) * 0.15f;
            NPC.frameCounter %= FrameCount - 1;
            NPC.frame.Y = (1 + (int)NPC.frameCounter) * frameHeight;
        }

        /// <summary>自定义绘制，返回 false 跳过原版（派对帽会错位）</summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            if (tex == null) {
                return false;
            }

            int frameHeight = tex.Height / Main.npcFrameCount[Type];
            Rectangle source = new(0, NPC.frame.Y, tex.Width, frameHeight);
            Vector2 origin = new(tex.Width / 2f, frameHeight);//底中心

            //贴图默认朝左
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Vector2 footPos = NPC.Bottom - screenPos + new Vector2(0f, NPC.gfxOffY + DrawVerticalOffset);
            Color light = NPC.GetAlpha(drawColor);
            spriteBatch.Draw(tex, footPos, source, light, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        /// <summary>
        /// 出场中 alpha&gt;16 冻 Passive；位姿由 <see cref="VictorRiftPortalProj.UpdateBoundVictor"/> 锚定
        /// </summary>
        public override bool PreAI() {
            if (NPC.alpha > 16) {
                NPC.velocity = Vector2.Zero;
                NPC.frameCounter = 0;
                return false;
            }
            return true;
        }

        public override void AI() {
            //仅本地右键交互
            if (Main.dedServ) {
                return;
            }

            Player local = NPC.Center.FindClosestPlayer();
            if (!local.Alives()) {
                if (VictorTalkUI.Instance.IsOpen) {
                    VictorTalkUI.Instance.Close();
                }
                return;
            }

            if (local.mouseInterface) {
                return;
            }

            bool hover = NPC.Hitbox.Contains(Main.MouseWorld.ToPoint());
            bool inRange = Vector2.Distance(local.Center, NPC.Center) < 200f;

            if (!inRange) {
                if (VictorTalkUI.Instance.IsOpen) {
                    VictorTalkUI.Instance.Close();
                }
                return;
            }

            if (!hover) {
                return;
            }

            local.noThrow = 2;
            local.cursorItemIconEnabled = true;
            local.cursorItemIconID = ItemID.None;
            local.cursorItemIconText = Language.GetTextValue("Mods.CalamityOverhaul.NPCs.Victor.TalkHint");

            if (Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                if (!VictorClinicUI.Instance.IsOpen && !VictorSurgery.Active) {
                    VictorSession.Bind(NPC.whoAmI);
                    if (VictorTalkUI.Instance.IsOpen) {
                        VictorTalkUI.Instance.Close();//OpenSound 已播，勿叠
                    }
                    else {
                        VictorTalkUI.Instance.Open();//OpenSound 已播，勿叠
                    }
                }
            }
        }

        /// <summary>
        /// 交互/手术期间定身朝向玩家；单机/主机本地，非主机可能被同步拉回
        /// </summary>
        public override void PostAI() {
            if (Main.dedServ || VictorSession.BoundWhoAmI != NPC.whoAmI) {
                return;
            }
            if (!VictorSession.IsUIActive && !VictorSurgery.Active) {
                return;
            }

            NPC.velocity.X = 0f;
            Player local = Main.LocalPlayer;
            if (local != null && local.active) {
                //贴图默认朝左
                NPC.direction = NPC.spriteDirection = local.Center.X < NPC.Center.X ? -1 : 1;
            }
        }
    }
}
