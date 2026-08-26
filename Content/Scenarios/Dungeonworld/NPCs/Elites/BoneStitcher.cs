using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 拾骨缝匠（L5 万骨窖，WAVE2-ENEMIES §3.4）：你杀掉的骷髅会被它拾回去重新缝起来，
    /// 不先杀缝匠，战场永远打不空。
    /// 状态机：0 巡骨（走向最近尸骨记录，400px 内取材入手 ai[3]++，无记录时与玩家保持
    /// 300px 环距）→ 1 缝合（channel 90f，缝线三拍；期间承伤 ≥12% 最大生命=流产，骨料浪费）
    /// → 2 退避（骨雾遁小跳，非传送，与 Boss 背袭传送读感彻底区分）。
    /// 联机：记录/认领/缝合计时/生成全服务器；ai[3] 囤积数过线驱动各端骨件公转与取材动画
    /// （前进沿触发防回卷）；ai[2]=当前走向目标列（客户端对称步态用）。
    /// 材质=陈骨+缝线：骨件公转数量可读 / 缝合震颤对齐 / 重拼骨接缝火花。
    /// </summary>
    internal class BoneStitcher : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Necromancer;

        //==================== 参数（建议值，验收再调）====================

        private const int StateScavenge = 0;
        private const int StateStitch = 1;
        private const int StateRetreat = 2;

        /// <summary>缝合 channel 帧 / 打断承伤阈值（12% 最大生命）</summary>
        private const int StitchFrames = 90;
        private const float InterruptRatio = 0.12f;
        /// <summary>取材认领半径（px）/ 骨料上限</summary>
        private const float ClaimRange = 400f;
        private const int StockCap = 3;
        /// <summary>退避帧 / 冷却</summary>
        private const int RetreatFrames = 24;
        private const int RetreatCooldownFrames = 90;

        private const float WalkSpeed = 1.0f;

        /// <summary>骨白（L5 强调色系）与缝线暖黄</summary>
        private static readonly Color BoneDust = new(222, 215, 200);
        internal static readonly Color ThreadGold = new(255, 225, 140);

        /// <summary>channel 承伤累计（服务器裁决用）</summary>
        private float channelDamage;
        /// <summary>退避冷却（服务器）</summary>
        private int retreatCooldown;
        /// <summary>取材动画计时（各端本地，ai[3] 前进沿点燃）</summary>
        private int takeAnim;
        /// <summary>已观察囤积数（各端本地）</summary>
        private int seenStock;
        private int turnLock;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Necromancer];
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 18;
            NPC.height = 44;
            NPC.damage = 20;
            NPC.defense = 8;
            NPC.lifeMax = 300;
            NPC.knockBackResist = 0.4f;
            NPC.aiStyle = -1;
            NPC.npcSlots = 2f;
            NPC.value = 40000f;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.BoneStitcher.Bestiary"),
            ]);
        }

        //==================== 投放（§4：L5 上中带 0.09 / 深巷 0.05；L2 稀客 0.02）====================

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            if (!DungeonworldEliteDirector.CommonSpawnGate(spawnInfo, Type)) {
                return 0f;
            }
            int band = DungeonworldEliteDirector.BandIndexForRow(spawnInfo.SpawnTileY);
            if (band == 1) {
                //押送尸体的杂役（叙事彩蛋）
                return 0.02f;
            }
            if (band != 4) {
                return 0f;
            }
            float depth = DungeonworldEliteDirector.BandDepth01(spawnInfo.SpawnTileY);
            return depth >= 2f / 3f ? 0.05f : 0.09f;
        }

        //==================== AI ====================

        public override void AI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateEdgeCue();
            }
            ServerSyncPacer();
            ObserveStock();
            if (takeAnim > 0) {
                takeAnim--;
            }
            if (!VaultUtils.isClient && retreatCooldown > 0) {
                retreatCooldown--;
            }

            NPC.TargetClosest(faceTarget: false);
            Player target = NPC.HasValidTarget ? Main.player[NPC.target] : null;

            switch ((int)State) {
                case StateScavenge:
                    UpdateScavenge(target);
                    break;
                case StateStitch:
                    UpdateStitch();
                    break;
                default:
                    UpdateRetreat();
                    break;
            }
        }

        private void PlayStateEdgeCue() {
            switch ((int)State) {
                case StateStitch:
                    channelDamage = 0f;
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 }, NPC.Center);
                    break;
                case StateRetreat:
                    //骨雾遁：一蓬骨尘扑（非传送）
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 3 }, NPC.Center);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 10; i++) {
                            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                                Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0.5f));
                        }
                    }
                    break;
            }
        }

        //==================== 巡骨 ====================

        private void UpdateScavenge(Player target) {
            StateTimer++;

            //服务器：锁定最近尸骨记录（目标列乘 ai[2] 过线，客户端对称步态）
            if (!VaultUtils.isClient && (int)AmbientClock % 10 == 0) {
                if (DungeonworldEliteDirector.TryPeekNearestBone(NPC.Center, out Vector2 recordPos)) {
                    int tileX = (int)(recordPos.X / 16f);
                    if ((int)StateParam != tileX) {
                        StateParam = tileX;
                        NPC.netUpdate = true;
                    }
                    //取材：认领半径内原子出表（两只缝匠不重复吃）
                    if (StackCount < StockCap
                        && Vector2.Distance(NPC.Center, recordPos) < ClaimRange
                        && DungeonworldEliteDirector.TryClaimBone(NPC.Center, ClaimRange, out _)) {
                        StackCount++;
                        NPC.netUpdate = true;
                    }
                }
                else if (StateParam != 0f) {
                    StateParam = 0f;
                    NPC.netUpdate = true;
                }
            }

            //步态：有记录走向记录列；无记录与玩家保持环距
            if (StateParam > 0f) {
                float targetX = StateParam * 16f + 8f;
                if (Math.Abs(targetX - NPC.Center.X) > 20f) {
                    NPC.direction = Math.Sign(targetX - NPC.Center.X);
                    WalkGait(WalkSpeed, 0.05f);
                }
                else {
                    NPC.velocity.X *= 0.85f;
                }
            }
            else if (target != null && !target.dead) {
                float dist = Vector2.Distance(target.Center, NPC.Center);
                if (dist < 240f) {
                    NPC.direction = -Math.Sign(target.Center.X - NPC.Center.X);
                    WalkGait(WalkSpeed, 0.05f);
                }
                else if (dist > 380f) {
                    NPC.direction = Math.Sign(target.Center.X - NPC.Center.X);
                    WalkGait(WalkSpeed, 0.05f);
                }
                else {
                    NPC.velocity.X *= 0.9f;
                }
            }
            NPC.spriteDirection = NPC.direction != 0 ? NPC.direction : NPC.spriteDirection;

            //转移裁决（服务器）
            if (VaultUtils.isClient) {
                return;
            }
            float playerDist = target != null && !target.dead
                ? Vector2.Distance(target.Center, NPC.Center) : float.MaxValue;
            //被近身压迫：骨雾遁（囤满时优先完成缝合而非保命）
            if (playerDist < 120f && retreatCooldown <= 0 && StackCount < StockCap) {
                ChangeState(StateRetreat);
                int away = playerDist == float.MaxValue ? NPC.direction : -Math.Sign(target.Center.X - NPC.Center.X);
                NPC.velocity = new Vector2((away == 0 ? 1 : away) * 5.5f, -4.2f);
                return;
            }
            //缝合条件：≥2 且玩家 >200px；囤满 3 无视距离强制缝合（防风筝到永不缝合）
            if (NPC.velocity.Y == 0f
                && (StackCount >= StockCap || (StackCount >= 2f && playerDist > 200f))) {
                ChangeState(StateStitch);
            }
        }

        //==================== 缝合（channel 90f，承伤 12% 流产）====================

        private void UpdateStitch() {
            StateTimer++;
            NPC.velocity.X *= 0.8f;
            int t = (int)StateTimer;

            //缝线绷紧音三拍（严格前进沿）
            for (int i = 0; i < 3; i++) {
                if (t >= 15 + i * 30 && BeatForward(i + 1)) {
                    SoundEngine.PlaySound(SoundID.Item37 with {
                        Volume = 0.45f,
                        Pitch = -0.3f + i * 0.25f,
                        MaxInstances = 3,
                    }, NPC.Center);
                }
            }

            if (VaultUtils.isClient) {
                return;
            }
            //channel 承伤流产（HitEffect 服务器累计）
            if (channelDamage >= NPC.lifeMax * InterruptRatio) {
                StackCount = 0f;
                ChangeState(StateScavenge);
                return;
            }
            if (t < StitchFrames) {
                return;
            }
            //缝合完成：每件骨料立起一只重拼骨（NewNPC 服务器发起自同步）
            int count = Math.Min(StockCap, (int)StackCount);
            for (int i = 0; i < count; i++) {
                NPC.NewNPC(NPC.GetSource_FromAI(),
                    (int)NPC.Center.X + Main.rand.Next(-48, 49), (int)NPC.Bottom.Y,
                    ModContent.NPCType<RestitchedBones>());
            }
            StackCount = 0f;
            ChangeState(StateScavenge);
        }

        //==================== 退避 ====================

        private void UpdateRetreat() {
            StateTimer++;
            if (!VaultUtils.isClient && StateTimer >= RetreatFrames) {
                retreatCooldown = RetreatCooldownFrames;
                ChangeState(StateScavenge);
            }
        }

        private void WalkGait(float maxSpeed, float accel) {
            if (turnLock > 0) {
                turnLock--;
            }
            NPC.velocity.X += accel * NPC.direction;
            NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -maxSpeed, maxSpeed);
            if (NPC.velocity.Y == 0f && NPC.collideX && turnLock <= 0) {
                //撞墙小跳翻越，翻不过去自然回头
                NPC.velocity.Y = -6f;
                turnLock = 20;
            }
        }

        //==================== 承伤累计与观察 ====================

        public override void HitEffect(NPC.HitInfo hit) {
            //channel 承伤累计（打端/服务器都会进；裁决只消费服务器侧的值）
            if ((int)State == StateStitch) {
                channelDamage += hit.Damage;
            }
            if (Main.dedServ) {
                return;
            }
            int n = NPC.life <= 0 ? 12 : 3;
            for (int i = 0; i < n; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone, hit.HitDirection * 1.2f, -1f);
            }
        }

        /// <summary>ai[3] 前进沿：取材飞行动画 + 骨件清空（流产/缝完）时散落读感</summary>
        private void ObserveStock() {
            int stock = (int)StackCount;
            if (AmbientClock <= 1f) {
                //迟入端首帧静默对齐（netcode 7.5/7.9）
                seenStock = stock;
                return;
            }
            if (stock > seenStock) {
                takeAnim = 20;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.35f, Pitch = 0.6f, MaxInstances = 3 }, NPC.Center);
                }
            }
            else if (stock < seenStock && !Main.dedServ) {
                //骨料散落（缝完立骨 / 流产浪费共用一副读感，状态沿区分语气）
                for (int i = 0; i < 8; i++) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                        Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2f, 0f));
                }
            }
            seenStock = stock;
        }

        //==================== 掉落 ====================

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.Bone, 1, 12, 12));
            npcLoot.Add(new CommonDrop(ItemID.BoneWand, 5));
            npcLoot.Add(new CommonDrop(ItemID.HealingPotion, 2, 2, 2));
        }

        public override void OnKill() {
            //囤积的骨料哗啦散一地：+ai[3]×4（服务器钩子，NewItem 自同步）
            int extra = (int)StackCount * 4;
            if (extra > 0) {
                Item.NewItem(NPC.GetSource_Loot(), NPC.getRect(), ItemID.Bone, extra);
            }
        }

        //==================== 帧与绘制 ====================

        public override void FindFrame(int frameHeight) {
            int count = Main.npcFrameCount[Type];
            if ((int)State == StateStitch) {
                //缝合锁帧：引线姿态
                NPC.frame.Y = frameHeight * Math.Min(1, count - 1);
                return;
            }
            if (Math.Abs(NPC.velocity.X) > 0.1f) {
                NPC.frameCounter += 0.12;
                NPC.frame.Y = frameHeight * ((int)NPC.frameCounter % count);
            }
            else {
                NPC.frame.Y = 0;
            }
        }

        public override Color? GetAlpha(Color drawColor)
            => drawColor.MultiplyRGB(new Color(215, 210, 205)) * NPC.Opacity;

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态泄漏自愈（netcode 7.2）
            BeginDefault(spriteBatch);
            return true;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DrawDustRobeLift(spriteBatch, screenPos);
            DrawOrbitBones(spriteBatch, screenPos, drawColor);

            //缝线：缝合期骨件间的细亮线（加色批，强度乘进色值）
            if ((int)State == StateStitch && (int)StackCount > 1) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    BeginAdditive(spriteBatch);
                    DrawThreads(spriteBatch, glow);
                    BeginDefault(spriteBatch);
                }
            }
        }

        /// <summary>尘白袍提亮：默认批 A=0 加色技法把本家紫袍拉向 L5 骨白，与死灵法师彻底切割</summary>
        private void DrawDustRobeLift(SpriteBatch sb, Vector2 screenPos) {
            Texture2D tex = TextureAssets.Npc[Type]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = NPC.frame;
            float frameCenterY = NPC.Bottom.Y - frame.Height * 0.5f + 4f + NPC.gfxOffY;
            Vector2 pos = new(NPC.Center.X - screenPos.X, frameCenterY - screenPos.Y);
            SpriteEffects fx = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color lift = new Color(BoneDust.R, BoneDust.G, BoneDust.B, 0) * (0.30f * NPC.Opacity);
            sb.Draw(tex, pos, frame, lift, 0f, frame.Size() * 0.5f, 1f, fx, 0f);
        }

        /// <summary>公转骨件 ×ai[3]（数量即弹药表）：缝合期收拢加速震颤对齐；取材新件从朝向前方飞入</summary>
        private void DrawOrbitBones(SpriteBatch sb, Vector2 screenPos, Color drawColor) {
            int stock = (int)StackCount;
            if (stock <= 0) {
                return;
            }
            Main.instance.LoadItem(ItemID.Bone);
            Texture2D bone = TextureAssets.Item[ItemID.Bone]?.Value;
            if (bone == null) {
                return;
            }
            bool stitching = (int)State == StateStitch;
            float t = StateTimer;
            float radius = stitching ? MathHelper.Lerp(26f, 13f, Math.Min(1f, t / StitchFrames)) : 26f;
            float speed = stitching ? 0.13f : 0.05f;
            float tremble = stitching ? Math.Min(1f, t / StitchFrames) * 1.8f : 0f;
            Vector2 origin = bone.Size() * 0.5f;

            for (int i = 0; i < stock; i++) {
                float angle = AmbientClock * speed + Seed + i * MathHelper.TwoPi / Math.Max(1, stock);
                Vector2 pos = OrbitPos(i, stock, radius, angle);
                if (tremble > 0f) {
                    pos += new Vector2(MathF.Sin(AmbientClock * 1.7f + i * 2.1f), MathF.Sin(AmbientClock * 2.3f + i)) * tremble;
                }
                //取材飞行：最新一件从朝向前方 60px 处 20f 内插入轨道（起点用朝向近似，误差无碍读感）
                if (i == stock - 1 && takeAnim > 0) {
                    Vector2 from = NPC.Center + new Vector2(NPC.direction * 60f, -8f);
                    pos = Vector2.Lerp(pos, from, takeAnim / 20f);
                }
                sb.Draw(bone, pos - screenPos, null, drawColor.MultiplyRGB(BoneDust) * NPC.Opacity,
                    angle * 2.4f, origin, 0.9f, SpriteEffects.None, 0f);
            }
        }

        private Vector2 OrbitPos(int index, int stock, float radius, float angle)
            => NPC.Center + new Vector2(0f, -6f) + angle.ToRotationVector2() * radius;

        /// <summary>缝线三根：相邻骨件间的横拉细光条，随 channel 进度绷亮</summary>
        private void DrawThreads(SpriteBatch sb, Texture2D glow) {
            int stock = (int)StackCount;
            float t = StateTimer;
            float bright = MathHelper.Clamp(t / StitchFrames, 0f, 1f) * 0.5f + 0.15f;
            float radius = MathHelper.Lerp(26f, 13f, Math.Min(1f, t / StitchFrames));
            Vector2 gOrigin = glow.Size() * 0.5f;
            for (int i = 0; i < stock; i++) {
                float a1 = AmbientClock * 0.13f + Seed + i * MathHelper.TwoPi / stock;
                float a2 = AmbientClock * 0.13f + Seed + (i + 1) % stock * MathHelper.TwoPi / stock;
                Vector2 p1 = OrbitPos(i, stock, radius, a1);
                Vector2 p2 = OrbitPos((i + 1) % stock, stock, radius, a2);
                Vector2 mid = (p1 + p2) * 0.5f;
                float len = Vector2.Distance(p1, p2);
                float rot = (p2 - p1).ToRotation();
                sb.Draw(glow, mid - Main.screenPosition, null, ThreadGold * bright, rot,
                    gOrigin, new Vector2(len * 0.6f * 2f / glow.Width, 2.4f * 2f / glow.Height), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 重拼骨（缝匠伴生怪）：以"拼错了"的姿势重新站起来的骷髅。头层偏移抖动 + 整体苍白 +
    /// 接缝火花即身份；比原版更快也更脆，18s 后自行散架（压力有界）。不入图鉴、无掉落。
    /// </summary>
    internal class RestitchedBones : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.AngryBones;

        /// <summary>存活帧：18s 散架</summary>
        private const int LifeFrames = 1080;
        private const float ChaseSpeed = 2.4f;

        private static readonly Color PaleBone = new(216, 214, 206);
        private int turnLock;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.AngryBones];
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 18;
            NPC.height = 40;
            NPC.damage = 30;
            NPC.defense = 0;
            NPC.lifeMax = 70;
            NPC.knockBackResist = 0.8f;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0.5f;
            NPC.value = 0f;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;
            AnimationType = NPCID.AngryBones;
        }

        public override void AI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge() && !Main.dedServ) {
                //缝线绷紧、骨件咔哒对齐的起身声
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 3 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 3 }, NPC.Center);
            }
            ServerSyncPacer(36);

            //18s 散架（服务器裁决；ai[1] 各端同步计数）
            StateTimer++;
            if (!VaultUtils.isClient && StateTimer >= LifeFrames) {
                NPC.life = 0;
                NPC.checkDead();
                return;
            }

            NPC.TargetClosest(faceTarget: true);
            if (NPC.HasValidTarget) {
                Player target = Main.player[NPC.target];
                float dx = target.Center.X - NPC.Center.X;
                if (Math.Abs(dx) > 8f) {
                    NPC.direction = Math.Sign(dx);
                }
                if (turnLock > 0) {
                    turnLock--;
                }
                NPC.velocity.X += 0.14f * NPC.direction;
                NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -ChaseSpeed, ChaseSpeed);
                if (NPC.velocity.Y == 0f && turnLock <= 0
                    && (NPC.collideX || (target.Bottom.Y < NPC.Top.Y - 48f && Math.Abs(dx) < 120f))) {
                    NPC.velocity.Y = -7f;
                    turnLock = 16;
                }
            }
            NPC.spriteDirection = NPC.direction != 0 ? NPC.direction : NPC.spriteDirection;

            //接缝火花（低频，各端本地）
            if (!Main.dedServ && Main.rand.NextBool(24)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-14f, 0f)),
                    VaultUtils.RandVr(0.5f, 1.5f), BoneStitcher.ThreadGold,
                    Main.rand.NextFloat(0.2f, 0.35f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            int n = NPC.life <= 0 ? 14 : 3;
            for (int i = 0; i < n; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone, hit.HitDirection * 1.6f, -1.2f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(NPC.Center, VaultUtils.RandVr(1f, 3f),
                        BoneStitcher.ThreadGold, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(8, 14));
                }
            }
        }

        public override Color? GetAlpha(Color drawColor)
            => drawColor.MultiplyRGB(PaleBone) * NPC.Opacity;

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态泄漏自愈（netcode 7.2）
            BeginDefault(spriteBatch);
            return true;
        }

        /// <summary>"拼错"画风：头部区域二次绘制并偏移抖动（整数像素，脑袋像没缝正）</summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = NPC.frame;
            int headH = Math.Min(14, frame.Height);
            Rectangle head = new(frame.X, frame.Y, frame.Width, headH);
            float frameTopY = NPC.Bottom.Y - frame.Height + 4f + NPC.gfxOffY;
            int jx = (int)MathF.Round(MathF.Sin(AmbientClock * 0.55f + Seed) * 1.6f);
            int jy = (int)MathF.Round(MathF.Sin(AmbientClock * 0.83f + Seed * 2f) * 0.9f);
            Vector2 pos = new(NPC.Center.X - screenPos.X + jx, frameTopY - screenPos.Y + jy);
            SpriteEffects fx = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //苍白盖色（保留一点原光照），头层错位即缝错的读法
            Color pale = drawColor.MultiplyRGB(PaleBone) * (0.9f * NPC.Opacity);
            spriteBatch.Draw(tex, pos, head, pale, 0f, new Vector2(frame.Width * 0.5f, 0f), 1f, fx, 0f);
        }
    }

    /// <summary>
    /// 尸骨记录（服务器专属）：子世界内白名单骨系死者入 Director 表（容量 32、60s 过期）。
    /// 零缝匠在场不记录，系统零成本；重拼骨是我方类型不在原版白名单内，天然防回收循环，
    /// Boss 走白名单正表排除。
    /// </summary>
    internal class BoneHarvestGlobal : GlobalNPC
    {
        public override bool IsLoadingEnabled(Mod mod) => DungeonworldEliteGate.Enabled;

        //手写白名单兜底（原版骨系：AngryBones 族 / DarkCaster / CursedSkull / 三族装甲骨）；
        //NPCID.Sets.Skeletons 作扩展并集（隔离编译验证符号，TML 源码参考在本机缺失）
        private static readonly HashSet<int> boneWhitelist = [
            NPCID.AngryBones, NPCID.AngryBonesBig, NPCID.AngryBonesBigMuscle, NPCID.AngryBonesBigHelmet,
            NPCID.DarkCaster, NPCID.CursedSkull, NPCID.Skeleton,
            NPCID.RustyArmoredBonesAxe, NPCID.RustyArmoredBonesFlail,
            NPCID.RustyArmoredBonesSword, NPCID.RustyArmoredBonesSwordNoArmor,
            NPCID.BlueArmoredBones, NPCID.BlueArmoredBonesMace,
            NPCID.BlueArmoredBonesNoPants, NPCID.BlueArmoredBonesSword,
            NPCID.HellArmoredBones, NPCID.HellArmoredBonesSpikeShield,
            NPCID.HellArmoredBonesMace, NPCID.HellArmoredBonesSword,
        ];

        internal static bool IsBoneStock(int type) {
            if (boneWhitelist.Contains(type)) {
                return true;
            }
            return type >= 0 && type < NPCID.Sets.Skeletons.Length && NPCID.Sets.Skeletons[type];
        }

        public override void OnKill(NPC npc) {
            //OnKill 本就服务器专属钩子；条件再拦一层保平
            if (VaultUtils.isClient || !Dungeonworld.Active) {
                return;
            }
            if (npc.boss || !IsBoneStock(npc.type)) {
                return;
            }
            if (NPC.CountNPCS(ModContent.NPCType<BoneStitcher>()) <= 0) {
                return;
            }
            DungeonworldEliteDirector.RecordBoneCorpse(npc.Center);
        }
    }
}
