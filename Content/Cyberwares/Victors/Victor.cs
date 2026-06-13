using CalamityOverhaul.Content.Cyberwares.Victors.UIs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// 义体医生 Victor —— 克苏鲁之眼被击败后到来的城镇NPC
    /// <br/>仅复用原版城镇NPC的基础行走AI(<see cref="NPCAIStyleID.Passive"/>)与住房系统，
    /// 交谈与义体诊所全部走完全自定义的 <see cref="VictorTalkUI"/> / <see cref="UIs.VictorClinicUI"/>
    /// </summary>
    internal class Victor : ModNPC
    {
        /// <summary>
        /// 与 Victor.png 一致的行走动画帧数，第 0 帧作为站立帧
        /// </summary>
        public const int FrameCount = 10;

        /// <summary>
        /// 绘制时脚部相对碰撞箱底边的垂直微调（正值向下），用于让脚踩实地面
        /// </summary>
        private const float DrawVerticalOffset = 2f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = FrameCount;

            //城镇NPC通用集合：纯医生，不主动攻击，仅在危险时逃跑
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
            NPC.aiStyle = NPCAIStyleID.Passive;//原版城镇NPC行走 / 住房 / 逃跑AI
            NPC.damage = 10;
            NPC.defense = 22;
            NPC.lifeMax = 350;
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

        //克苏鲁之眼被击败后，Victor 才会在有空房时入住
        public override bool CanTownNPCSpawn(int numTownNPCs) => NPC.downedBoss1;

        public override List<string> SetNPCNameList() => [
            Language.GetTextValue("Mods.CalamityOverhaul.NPCs.Victor.Name0"),
            Language.GetTextValue("Mods.CalamityOverhaul.NPCs.Victor.Name1"),
            Language.GetTextValue("Mods.CalamityOverhaul.NPCs.Victor.Name2"),
        ];

        //禁用原版聊天框，交互完全交给自定义的右键检测 + VictorTalkUI
        public override bool CanChat() => false;

        public override void FindFrame(int frameHeight) {
            //朝向跟随面向 / 移动方向
            if (!NPC.IsABestiaryIconDummy && NPC.direction != 0) {
                NPC.spriteDirection = NPC.direction;
            }

            //图鉴里的展示木偶持续播放行走动画
            if (NPC.IsABestiaryIconDummy) {
                NPC.frameCounter += 0.18f;
                NPC.frameCounter %= FrameCount - 1;
                NPC.frame.Y = (1 + (int)NPC.frameCounter) * frameHeight;
                return;
            }

            //静止 = 第 0 帧站立
            if (Math.Abs(NPC.velocity.X) < 0.1f) {
                NPC.frameCounter = 0;
                NPC.frame.Y = 0;
                return;
            }

            //移动时在 1..9 帧间循环，速度越快帧推进越快
            NPC.frameCounter += Math.Abs(NPC.velocity.X) * 0.15f;
            NPC.frameCounter %= FrameCount - 1;
            NPC.frame.Y = (1 + (int)NPC.frameCounter) * frameHeight;
        }

        /// <summary>
        /// 完全自定义绘制：手动处理贴图帧、朝向翻转、地面对齐、光照、落地软阴影与克制的赛博红边缘辉光。
        /// <br/>返回 <see langword="false"/> 跳过原版城镇NPC绘制，避免错位、派对帽等默认装饰带来的观感问题
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            if (tex == null) {
                return false;
            }

            int frameHeight = tex.Height / Main.npcFrameCount[Type];
            Rectangle source = new(0, NPC.frame.Y, tex.Width, frameHeight);
            Vector2 origin = new(tex.Width / 2f, frameHeight);//底部中心：脚部贴合碰撞箱底边

            //贴图默认朝左，朝右移动时水平翻转
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Vector2 footPos = NPC.Bottom - screenPos + new Vector2(0f, NPC.gfxOffY + DrawVerticalOffset);
            Color light = NPC.GetAlpha(drawColor);
            float fade = drawColor.A / 255f;//随夜间 / 隐身整体淡入淡出

            Texture2D soft = CWRAsset.SoftGlow?.Value;

            //落地软阴影
            if (soft != null) {
                float lum = (drawColor.R + drawColor.G + drawColor.B) / 765f;
                Color shadow = Color.Black * (0.30f * MathHelper.Clamp(lum + 0.25f, 0f, 1f));
                spriteBatch.Draw(soft, footPos + new Vector2(0f, -3f), null, shadow, 0f,
                    soft.Size() / 2f, new Vector2(NPC.width / 24f, 0.16f), SpriteEffects.None, 0f);
            }

            //克制的赛博红边缘辉光：本体后方画一圈略微偏移的红色剪影
            float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f);
            Color rim = new Color(255, 45, 45) * ((0.14f + 0.10f * pulse) * fade);
            for (int i = 0; i < 4; i++) {
                Vector2 offset = (MathHelper.PiOver2 * i + MathHelper.PiOver4).ToRotationVector2() * 2.2f;
                spriteBatch.Draw(tex, footPos + offset, source, rim, NPC.rotation, origin, NPC.scale, effects, 0f);
            }

            //本体
            spriteBatch.Draw(tex, footPos, source, light, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        public override void AI() {
            //移动 / 住房由 aiStyle=7 的原版逻辑自动驱动，这里只追加本地客户端的右键交互
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

            //悬停提示
            local.noThrow = 2;
            local.cursorItemIconEnabled = true;
            local.cursorItemIconID = ItemID.None;
            local.cursorItemIconText = Language.GetTextValue("Mods.CalamityOverhaul.NPCs.Victor.TalkHint");

            if (Main.mouseRight && Main.mouseRightRelease && !VictorTalkUI.Instance.IsOpen) {
                Main.mouseRightRelease = false;
                VictorTalkUI.Instance.Open();
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
        }
    }
}
