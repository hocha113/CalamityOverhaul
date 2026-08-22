using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.Victors.UIs;
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

namespace CalamityOverhaul.Content.NPCs.Victors
{
    /// <summary>
    /// 城镇 Victor；Passive 行走；对话/诊所走 <see cref="VictorTalkUI"/> / <see cref="VictorClinicUI"/>
    /// </summary>
    [AutoloadHead]
    internal class Victor : ModNPC
    {
        /// <summary>Victor.png 共 10 帧，0 站立</summary>
        public const int FrameCount = 10;

        /// <summary>绘制脚部 Y 微调，正值向下</summary>
        private const float DrawVerticalOffset = 2f;

        #region 出场推出

        /// <summary>被门推出时的初速；横向为主，配一记不大的抬腿</summary>
        private static readonly Vector2 EjectVelocity = new(3.8f, -3.2f);
        /// <summary>落地后站稳所需帧数</summary>
        private const int RecoverFrames = 22;
        /// <summary>滞空前倾上限；他是被推出来的，不是被甩出来的，比 TBUG 收敛</summary>
        private const float MaxLeanRadians = 0.30f;

        internal enum EntryPhase : byte
        {
            None,
            /// <summary>离门滞空段，物理交给原版重力</summary>
            Ejected,
            /// <summary>落地站稳，横速衰减 + 前倾回正</summary>
            Recover,
        }

        private EntryPhase entryPhase;
        private int entryTimer;
        private int entryFacing = 1;
        /// <summary>本端只演一次，防门每帧重复触发</summary>
        private bool entryPlayed;

        /// <summary>出场演出进行中；此间不接受交互</summary>
        internal bool InEntry => entryPhase != EntryPhase.None;

        #endregion

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

            //好感度：地下黑诊所出身，低温利于存件；潮气与盐雾锈蚀电路
            NPC.Happiness
                .SetBiomeAffection<UndergroundBiome>(AffectionLevel.Love)
                .SetBiomeAffection<SnowBiome>(AffectionLevel.Like)
                .SetBiomeAffection<JungleBiome>(AffectionLevel.Dislike)
                .SetBiomeAffection<OceanBiome>(AffectionLevel.Hate)
                //机械师是同好，蒸汽朋克人供零件；护士是同行竞争，爆破专家毁他的精细活
                .SetNPCAffection(NPCID.Mechanic, AffectionLevel.Love)
                .SetNPCAffection(NPCID.Steampunker, AffectionLevel.Like)
                .SetNPCAffection(NPCID.Nurse, AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Demolitionist, AffectionLevel.Hate);

            //反向注册，让镇民也对他有态度（赛博格视他为再造恩人）
            NPCHappiness.Get(NPCID.Cyborg).SetNPCAffection<Victor>(AffectionLevel.Love);
            NPCHappiness.Get(NPCID.Mechanic).SetNPCAffection<Victor>(AffectionLevel.Like);
            NPCHappiness.Get(NPCID.Nurse).SetNPCAffection<Victor>(AffectionLevel.Dislike);
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

        /// <summary>
        /// 首次登场走 <see cref="VictorPortalSpawner"/> 的传送门；
        /// 登场过后视作正常城镇 NPC，死后由原版住房系统重生
        /// </summary>
        public override bool CanTownNPCSpawn(int numTownNPCs) => VictorWorldState.HasArrived;

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

            //滞空定格迈步帧，读作被推出去而不是原地站着平移
            if (entryPhase == EntryPhase.Ejected) {
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
            Color light = ComputeEmergeColor(drawColor);
            spriteBatch.Draw(tex, footPos, source, light, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        /// <summary>
        /// 传送门浮现着色：先以近黑剪影整体显形（门内背光），再随走出渐染回受光色；
        /// alpha 仅在浮现期非 0，由 <see cref="VictorRiftPortalProj.UpdateBoundVictor"/> 驱动
        /// </summary>
        private Color ComputeEmergeColor(Color drawColor) {
            if (NPC.alpha <= 0) {
                return NPC.GetAlpha(drawColor);
            }
            float t = 1f - NPC.alpha / 255f;                     //0=门内 1=完全走出
            float opacity = MathHelper.Clamp(t / 0.35f, 0f, 1f); //前 35% 行程完成显形
            float mix = MathHelper.Clamp((t - 0.30f) / 0.55f, 0f, 1f);
            mix = mix * mix * (3f - 2f * mix);
            Color silhouette = new(14, 5, 7);
            return Color.Lerp(silhouette, drawColor, mix) * opacity;
        }

        #region 出场状态机

        /// <summary>
        /// 由 <see cref="VictorRiftPortalProj"/> 在推出帧起逐帧认领（客户端可能晚一两帧才收到 NPC）；
        /// 一次性进入滞空段，之后位移交给原版重力与瓦片碰撞
        /// </summary>
        internal void BeginEntry(int facing) {
            if (entryPlayed) {
                return;
            }
            entryPlayed = true;
            entryPhase = EntryPhase.Ejected;
            entryTimer = 0;
            entryFacing = facing >= 0 ? 1 : -1;
        }

        /// <summary>
        /// 出场期间接管 Passive 决策；返回 false 只跳过 AI，重力与碰撞仍由原版在 AI 之后施加。
        /// 淡入由门驱动 alpha，剪影表现见 <see cref="ComputeEmergeColor"/>
        /// </summary>
        public override bool PreAI() {
            if (entryPhase != EntryPhase.None) {
                entryTimer++;
                if (entryPhase == EntryPhase.Ejected) {
                    UpdateEjected();
                }
                else {
                    UpdateRecover();
                }
                return false;
            }

            //兜底：门未接管却仍处于淡入态（旧档中途读入）时冻住，别让半透明的他走路
            if (NPC.alpha > 16) {
                NPC.velocity = Vector2.Zero;
                NPC.frameCounter = 0;
                return false;
            }
            return true;
        }

        private void UpdateEjected() {
            //轻风阻，别飞得离门太远
            NPC.velocity.X *= 0.985f;
            NPC.direction = NPC.spriteDirection = entryFacing;
            NPC.rotation = MathHelper.Clamp(NPC.velocity.X * 0.06f, -MaxLeanRadians, MaxLeanRadians);

            bool grounded = NPC.collideY && NPC.velocity.Y >= 0f;
            bool splashed = NPC.wet && entryTimer > 10;
            if ((grounded && entryTimer > 6) || splashed || entryTimer > 200) {
                entryPhase = EntryPhase.Recover;
                entryTimer = 0;
                OnEntryLand();
            }
        }

        private void OnEntryLand() {
            //落地卸掉大半横速，剩下的够他迈出一两步收住
            NPC.velocity.X *= 0.45f;
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 dustPos = new(NPC.position.X, NPC.Bottom.Y - 8f);
            for (int i = 0; i < 14; i++) {
                Dust d = Dust.NewDustDirect(dustPos, NPC.width, 8, DustID.Smoke,
                    Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-1.9f, -0.3f),
                    150, new Color(66, 34, 30), Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = false;
            }
            //门的同族余烬，沿地面向外铺
            for (int i = 0; i < 5; i++) {
                Vector2 vel = new(entryFacing * Main.rand.NextFloat(0.6f, 2.2f), -Main.rand.NextFloat(0.4f, 1.4f));
                PRTLoader.NewParticle<PRT_BanishGlitch>(NPC.Bottom - new Vector2(0f, 6f), vel,
                    Color.White, Main.rand.NextFloat(0.4f, 0.9f)).Configure(Main.rand.Next(16, 30), 0.10f);
            }

            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.55f }, NPC.Bottom);
            SoundEngine.PlaySound(CWRSound.ShortCircuit with { Volume = 0.26f, Pitch = 0.15f }, NPC.Bottom);
            ShakeLocalNear(3.4f, 820f);
        }

        private void UpdateRecover() {
            NPC.velocity.X *= 0.82f;
            NPC.direction = NPC.spriteDirection = entryFacing;

            //前倾回正带一次小过冲：他伸手扶了一下，然后站直
            float t = MathHelper.Clamp(entryTimer / (float)RecoverFrames, 0f, 1f);
            float settle = MathF.Exp(-t * 5.5f) * MathF.Cos(t * 9f);
            NPC.rotation = MaxLeanRadians * 0.55f * settle * entryFacing;

            if (entryTimer >= RecoverFrames) {
                entryPhase = EntryPhase.None;
                entryTimer = 0;
                NPC.rotation = 0f;
                NPC.velocity.X = 0f;
                NPC.alpha = 0;
            }
        }

        /// <summary>震动只写本机玩家，并按距离衰减，远处队友不该跟着晃</summary>
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
            //仅本地右键交互；出场演出期间不接客
            if (Main.dedServ || InEntry) {
                return;
            }

            //交互判定只关心本地玩家；FindClosestPlayer 在多人下会拿到别人的状态
            Player local = Main.LocalPlayer;
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
                    //城镇 NPC 图鉴只认交谈记录，登记前先记下是否初见供台词分桶
                    VictorDialogue.NoteFirstMeet(!VictorBestiary.HasMet(NPC));
                    VictorBestiary.RegisterMet(NPC);
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
