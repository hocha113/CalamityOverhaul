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
    /// 义体医生 Victor，克苏鲁之眼击败后入住的城镇 NPC
    /// <br/>行走/住房走 <see cref="NPCAIStyleID.Passive"/>；对话与诊所走 <see cref="VictorTalkUI"/> / <see cref="UIs.VictorClinicUI"/>
    /// </summary>
    internal class Victor : ModNPC
    {
        /// <summary>与 Victor.png 一致共 10 帧，第 0 帧站立</summary>
        public const int FrameCount = 10;

        /// <summary>绘制脚部垂直微调，正值向下贴地</summary>
        private const float DrawVerticalOffset = 2f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = FrameCount;

            //城镇 NPC 集合：危险时逃跑，不主动攻击
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
            NPC.width = 24;
            NPC.height = 46;
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

        /// <summary>
        /// 禁用原版 town NPC spawn 路径，统一走 <see cref="VictorPortalSpawner"/> 的传送门出场
        /// <br/>原版 spawn 依赖"空房+随机帧"会不稳定，自定义系统在玩家旁找开放地面后用 <see cref="VictorRiftPortalProj"/> 演出生成
        /// </summary>
        public override bool CanTownNPCSpawn(int numTownNPCs) => false;

        public override List<string> SetNPCNameList() => [
            Language.GetTextValue("Mods.CalamityOverhaul.NPCs.Victor.Name0"),
        ];

        //禁用原版聊天，交互走 VictorTalkUI 右键
        public override bool CanChat() => false;

        public override void FindFrame(int frameHeight) {
            //朝向跟 direction
            if (!NPC.IsABestiaryIconDummy && NPC.direction != 0) {
                NPC.spriteDirection = NPC.direction;
            }

            //图鉴木偶循环行走 1..9 帧
            if (NPC.IsABestiaryIconDummy) {
                NPC.frameCounter += 0.18f;
                NPC.frameCounter %= FrameCount - 1;
                NPC.frame.Y = (1 + (int)NPC.frameCounter) * frameHeight;
                return;
            }

            //静止用第 0 帧
            if (Math.Abs(NPC.velocity.X) < 0.1f) {
                NPC.frameCounter = 0;
                NPC.frame.Y = 0;
                return;
            }

            //移动 1..9 帧，速度越快推进越快
            NPC.frameCounter += Math.Abs(NPC.velocity.X) * 0.15f;
            NPC.frameCounter %= FrameCount - 1;
            NPC.frame.Y = (1 + (int)NPC.frameCounter) * frameHeight;
        }

        /// <summary>
        /// 自定义绘制：帧/翻转/地面对齐/光照/阴影/红边辉光
        /// <br/>返回 false 跳过原版绘制，避免派对帽等装饰错位
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            if (tex == null) {
                return false;
            }

            int frameHeight = tex.Height / Main.npcFrameCount[Type];
            Rectangle source = new(0, NPC.frame.Y, tex.Width, frameHeight);
            Vector2 origin = new(tex.Width / 2f, frameHeight);//底部中心锚点

            //贴图默认朝左，右移时水平翻转
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Vector2 footPos = NPC.Bottom - screenPos + new Vector2(0f, NPC.gfxOffY + DrawVerticalOffset);
            Color light = NPC.GetAlpha(drawColor);
            spriteBatch.Draw(tex, footPos, source, light, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        /// <summary>
        /// 传送门出场期间冻结原版 Passive AI：alpha 较高（仍未完全淡入）就跳过移动
        /// <br/>位置/朝向由 <see cref="VictorRiftPortalProj.UpdateBoundVictor"/> 每帧锚定
        /// </summary>
        public override bool PreAI() {
            if (NPC.alpha > 16) {
                NPC.velocity = Vector2.Zero;
                //保持站立帧
                NPC.frameCounter = 0;
                return false;
            }
            return true;
        }

        public override void AI() {
            //仅本地客户端追加右键交互
            if (Main.dedServ) {
                return;
            }

            Player local = Main.LocalPlayer;
            if (local == null || !local.active || local.dead || local.mouseInterface) {
                return;
            }

            bool hover = NPC.Hitbox.Contains(Main.MouseWorld.ToPoint());
            bool inRange = Vector2.Distance(local.Center, NPC.Center) < 130f;
            if (!hover || !inRange) {
                return;
            }

            //右键悬停提示
            local.noThrow = 2;
            local.cursorItemIconEnabled = true;
            local.cursorItemIconID = ItemID.None;
            local.cursorItemIconText = Language.GetTextValue("Mods.CalamityOverhaul.NPCs.Victor.TalkHint");

            if (Main.mouseRight && Main.mouseRightRelease && !VictorTalkUI.Instance.IsOpen
                && !VictorClinicUI.Instance.IsOpen && !VictorSurgery.Active) {
                Main.mouseRightRelease = false;
                VictorSession.Bind(NPC.whoAmI);
                VictorTalkUI.Instance.Open();//开启音由 OpenSound 播，避免重复
            }
        }

        /// <summary>
        /// 交互/手术期间定身面向玩家，Passive AI 后归零水平速度
        /// <br/>单机/主机本地覆盖；多人非主机可能被服务器同步拉回
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
                //贴图默认朝左：玩家在左 spriteDirection=-1
                NPC.direction = NPC.spriteDirection = local.Center.X < NPC.Center.X ? -1 : 1;
            }
        }
    }
}
