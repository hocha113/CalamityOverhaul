using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.TBUGs.UIs;
using CalamityOverhaul.Content.NPCs.Victors;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>
    /// 城镇 TBUG；从骷髅王后的夜晚裂缝里被吐出来的漏洞贩子。
    /// Passive 行走；登场抛射由 <see cref="TBUGRiftPortalProj"/> 触发本类的入场状态机
    /// </summary>
    [AutoloadHead]
    internal class TBUG : ModNPC
    {
        /// <summary>Victor.png 共 10 帧，0 站立</summary>
        public const int FrameCount = 10;

        /// <summary>绘制脚部 Y 微调，正值向下</summary>
        private const float DrawVerticalOffset = 2f;

        /// <summary>落地踉跄帧数</summary>
        private const int StaggerFrames = 26;

        internal enum EntryPhase : byte
        {
            None,
            /// <summary>被裂缝吐出后的抛物线段，物理交给原版重力</summary>
            Airborne,
            /// <summary>落地踉跄，X 速衰减 + 晃动</summary>
            Stagger,
        }

        private EntryPhase entryPhase;
        private int entryTimer;
        private int entryFacing = 1;
        /// <summary>本端只演一次，防裂缝重复触发</summary>
        private bool entryPlayed;

        internal bool InEntry => entryPhase != EntryPhase.None;

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

            //好感度：地下凉快安静像机房；雪原低温对硬件好；丛林湿度报警；海边设备进过水
            NPC.Happiness
                .SetBiomeAffection<UndergroundBiome>(AffectionLevel.Love)
                .SetBiomeAffection<SnowBiome>(AffectionLevel.Like)
                .SetBiomeAffection<JungleBiome>(AffectionLevel.Dislike)
                .SetBiomeAffection<OceanBiome>(AffectionLevel.Hate)
                //机械师是同好；哥布林工匠不问来历；Victor 总想把她抬上手术台；税收官的账本比病毒毒
                .SetNPCAffection(NPCID.Mechanic, AffectionLevel.Love)
                .SetNPCAffection(NPCID.GoblinTinkerer, AffectionLevel.Like)
                .SetNPCAffection<Victor>(AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.TaxCollector, AffectionLevel.Hate);

            //反向注册：Victor 视她为难得的研究对象，机械师喜欢有人聊电路
            NPCHappiness.Get(ModContent.NPCType<Victor>()).SetNPCAffection<TBUG>(AffectionLevel.Like);
            NPCHappiness.Get(NPCID.Mechanic).SetNPCAffection<TBUG>(AffectionLevel.Like);
        }

        public override void SetDefaults() {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 36;
            NPC.height = 50;
            NPC.aiStyle = NPCAIStyleID.Passive;//Passive 行走/住房/逃跑
            NPC.damage = 10;
            NPC.defense = 40;
            NPC.lifeMax = 2500;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.TBUG.Bestiary"),
            ]);
        }

        /// <summary>
        /// 首次登场走 <see cref="TBUGRiftSpawner"/> 的裂缝；
        /// 登场过后视作正常城镇 NPC，死后由原版住房系统重生
        /// </summary>
        public override bool CanTownNPCSpawn(int numTownNPCs) => TBUGWorldState.HasArrived;

        public override List<string> SetNPCNameList() => [
            Language.GetTextValue("Mods.CalamityOverhaul.NPCs.TBUG.Name0"),
        ];

        //禁用原版聊天，交互走 TBUGTalkUI 右键
        public override bool CanChat() => false;

        #region 入场抛射

        /// <summary>
        /// 裂缝在吐出帧起对所有端调用；一次性进入抛射段，
        /// 之后物理由原版重力驱动（PreAI 返回 false 只拦 AI 决策不拦位移）
        /// </summary>
        internal void BeginEntry(int facing) {
            if (entryPlayed) {
                return;
            }
            entryPlayed = true;
            entryPhase = EntryPhase.Airborne;
            entryTimer = 0;
            entryFacing = facing >= 0 ? 1 : -1;
        }

        public override bool PreAI() {
            if (entryPhase == EntryPhase.None) {
                return true;
            }
            entryTimer++;
            if (entryPhase == EntryPhase.Airborne) {
                UpdateEntryAirborne();
            }
            else {
                UpdateEntryStagger();
            }
            return false;
        }

        private void UpdateEntryAirborne() {
            NPC.alpha = Math.Max(0, NPC.alpha - 26);
            //轻风阻，别飘得离裂缝太远
            NPC.velocity.X *= 0.988f;
            NPC.direction = NPC.spriteDirection = entryFacing;
            //前倾随水平速度，落地前自然回正一部分
            NPC.rotation = MathHelper.Clamp(NPC.velocity.X * 0.07f, -0.55f, 0.55f);

            bool grounded = NPC.collideY && NPC.velocity.Y >= 0f;
            bool splashed = NPC.wet && entryTimer > 10;
            if ((grounded && entryTimer > 4) || splashed || entryTimer > 240) {
                entryPhase = EntryPhase.Stagger;
                entryTimer = 0;
                OnEntryLand();
            }
        }

        private void OnEntryLand() {
            NPC.velocity.X *= 0.5f;
            if (VaultUtils.isServer) {
                return;
            }

            //扬尘 + 几片故障渣 + 闷响短路，近距离小震
            Vector2 dustPos = new(NPC.position.X, NPC.Bottom.Y - 8f);
            for (int i = 0; i < 16; i++) {
                Dust d = Dust.NewDustDirect(dustPos, NPC.width, 8, DustID.Smoke,
                    Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-2.2f, -0.4f),
                    120, default, Main.rand.NextFloat(0.8f, 1.4f));
                d.noGravity = false;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_TBUGGlitch>(NPC.Center,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -1f)),
                    Color.White, Main.rand.NextFloat(0.5f, 1f)).Configure(Main.rand.Next(16, 30));
            }

            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.6f }, NPC.Bottom);
            SoundEngine.PlaySound(CWRSound.ShortCircuit with { Volume = 0.3f, Pitch = 0.2f }, NPC.Bottom);
            ShakeLocalNear(4.5f, 900f);
        }

        private void UpdateEntryStagger() {
            NPC.velocity.X *= 0.8f;
            NPC.direction = NPC.spriteDirection = entryFacing;
            //踉跄晃动随时间收敛
            float settle = MathF.Max(0f, 1f - entryTimer / (float)StaggerFrames);
            NPC.rotation = MathF.Sin(entryTimer * 0.55f) * 0.15f * settle;

            if (entryTimer >= StaggerFrames) {
                entryPhase = EntryPhase.None;
                entryTimer = 0;
                NPC.rotation = 0f;
                NPC.alpha = 0;
                NPC.velocity.X = 0f;
            }
        }

        private void ShakeLocalNear(float strength, float maxDist) {
            Player lp = Main.LocalPlayer;
            if (lp?.active != true || lp.dead) {
                return;
            }
            float dist = lp.Distance(NPC.Bottom);
            if (dist >= maxDist) {
                return;
            }
            lp.CWR().GetScreenShake(strength * (1f - dist / maxDist));
        }

        #endregion

        public override void AI() {
            //仅本地右键交互
            if (Main.dedServ) {
                return;
            }

            //交互判定只关心本地玩家；FindClosestPlayer 在多人下会拿到别人的状态
            Player local = Main.LocalPlayer;
            if (!local.Alives()) {
                if (TBUGTalkUI.Instance.IsOpen) {
                    TBUGTalkUI.Instance.Close();
                }
                return;
            }

            if (local.mouseInterface) {
                return;
            }

            bool hover = NPC.Hitbox.Contains(Main.MouseWorld.ToPoint());
            bool inRange = Vector2.Distance(local.Center, NPC.Center) < 200f;

            if (!inRange) {
                if (TBUGTalkUI.Instance.IsOpen) {
                    TBUGTalkUI.Instance.Close();
                }
                if (TBUGShopUI.Instance.IsOpen) {
                    TBUGShopUI.Instance.Close();
                }
                return;
            }

            if (!hover) {
                return;
            }

            local.noThrow = 2;
            local.cursorItemIconEnabled = true;
            local.cursorItemIconID = ItemID.None;
            local.cursorItemIconText = Language.GetTextValue("Mods.CalamityOverhaul.NPCs.TBUG.TalkHint");

            if (Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                if (!TBUGShopUI.Instance.IsOpen) {
                    TBUGSession.Bind(NPC.whoAmI);
                    //城镇 NPC 图鉴只认交谈记录，登记前先记下是否初见供台词分桶
                    TBUGDialogue.NoteFirstMeet(!TBUGBestiary.HasMet(NPC));
                    TBUGBestiary.RegisterMet(NPC);
                    if (TBUGTalkUI.Instance.IsOpen) {
                        TBUGTalkUI.Instance.Close();//OpenSound 已播，勿叠
                    }
                    else {
                        TBUGTalkUI.Instance.Open();//OpenSound 已播，勿叠
                    }
                }
            }
        }

        /// <summary>
        /// 交互期间定身朝向玩家；单机/主机本地，非主机可能被同步拉回
        /// </summary>
        public override void PostAI() {
            if (Main.dedServ || TBUGSession.BoundWhoAmI != NPC.whoAmI) {
                return;
            }
            if (!TBUGSession.IsUIActive) {
                return;
            }

            NPC.velocity.X = 0f;
            Player local = Main.LocalPlayer;
            if (local != null && local.active) {
                //贴图默认朝左
                NPC.direction = NPC.spriteDirection = local.Center.X < NPC.Center.X ? -1 : 1;
            }
        }

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

            //滞空段定格迈步帧，读作跌落而不是原地跑
            if (entryPhase == EntryPhase.Airborne) {
                NPC.frameCounter = 0;
                NPC.frame.Y = 5 * frameHeight;
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
    }
}
