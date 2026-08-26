using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Ambience;
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
    /// 300px 环距）→ 1 缝合（channel 90f，缝线三拍逐件锁针脚；期间承伤 ≥12% 最大生命=流产）
    /// → 2 退避（骨雾遁小跳，非传送）→ 3 流产踉跄（30f 硬直，断线反噬的惩罚窗）。
    /// 联机：记录/认领/缝合计时/生成全服务器；ai[3] 囤积数过线驱动各端骨件公转与取材动画
    /// （前进沿触发防回卷）；ai[2] 分状态语义——巡骨=走向目标列，缝合=锚点打包
    /// （tileX*8192+tileY，见 StitcherVfx.PackAnchor），锚与槽位几何是服务器/各端共用纯函数，
    /// 生成点与骨堆预告必然重合（预告即承诺）。
    /// 材质=陈骨+缝线（StitcherBoneDress/StitcherThread 双 shader 复合）：骨件公转数量即
    /// 弹药表 / 缝合收架震颤对齐 / 缝合位骨堆实体化长成 / 承伤外显为线身磨损缺口。
    /// </summary>
    internal class BoneStitcher : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Necromancer;

        //==================== 参数（建议值，验收再调）====================

        private const int StateScavenge = 0;
        private const int StateStitch = 1;
        private const int StateRetreat = 2;
        private const int StateInterrupted = 3;

        /// <summary>缝合 channel 帧 / 打断承伤阈值（12% 最大生命）</summary>
        private const int StitchFrames = 90;
        private const float InterruptRatio = 0.12f;
        /// <summary>取材认领半径（px）/ 骨料上限</summary>
        private const float ClaimRange = 400f;
        private const int StockCap = 3;
        /// <summary>退避帧 / 流产踉跄帧 / 冷却</summary>
        private const int RetreatFrames = 24;
        private const int InterruptFrames = 30;
        private const int RetreatCooldownFrames = 90;
        /// <summary>取材飞行动画帧（各端本地）</summary>
        private const int TakeFlightFrames = 22;

        private const float WalkSpeed = 1.0f;

        /// <summary>channel 承伤累计（服务器裁决用；各端 HitEffect 同源累计，本地磨损外显）</summary>
        private float channelDamage;
        /// <summary>退避冷却（服务器）</summary>
        private int retreatCooldown;
        /// <summary>取材动画计时（各端本地，ai[3] 前进沿点燃）</summary>
        private int takeAnim;
        /// <summary>取材来源点（前进沿时按 ai[2] 目标列地表解析）</summary>
        private Vector2 takeFrom;
        /// <summary>已观察囤积数（各端本地）</summary>
        private int seenStock;
        private int turnLock;
        /// <summary>针闪余辉（节拍/完成沿点燃，加色批）</summary>
        private int glintFlash;
        /// <summary>本 tick 骨料清空已由完成/流产沿接管读感（跳过通用散落尘）</summary>
        private bool stockDropHandled;
        /// <summary>缝合锚各端缓存（ChangeState 清 ai[2]，完成/流产沿要用）</summary>
        private Point cachedAnchor;
        private bool hasAnchor;

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
            stockDropHandled = false;
            int prevState = (int)NPC.localAI[2] - 1;
            if (StateEdge()) {
                PlayStateEdgeCue(prevState);
            }
            ServerSyncPacer();
            ObserveStock();
            if (takeAnim > 0) {
                takeAnim--;
            }
            if (glintFlash > 0) {
                glintFlash--;
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
                case StateInterrupted:
                    UpdateInterrupted();
                    break;
                default:
                    UpdateRetreat();
                    break;
            }
        }

        //==================== 状态沿读感（各端本地重放）====================

        private void PlayStateEdgeCue(int prevState) {
            switch ((int)State) {
                case StateScavenge:
                    if (prevState == StateStitch) {
                        CompletionRead();
                    }
                    break;
                case StateStitch:
                    channelDamage = 0f;
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 }, NPC.Center);
                    break;
                case StateRetreat:
                    RetreatRead();
                    break;
                case StateInterrupted:
                    InterruptRead();
                    break;
            }
        }

        /// <summary>
        /// 缝合完成沿：鞭响收线，骨件沿线轨甩向缝合位（PRT_StitcherBoneCast）。
        /// 骨堆位与服务器生成点同函数（StitcherVfx.SlotGround）推导——甩到哪，骨架就在哪起身
        /// </summary>
        private void CompletionRead() {
            stockDropHandled = true;
            glintFlash = 6;
            if (Main.dedServ || !hasAnchor) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.55f, Pitch = -0.1f, MaxInstances = 2 }, NPC.Center);
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 3 }, NPC.Center);
            int count = Math.Min(StockCap, seenStock);
            for (int i = 0; i < count; i++) {
                Vector2 rack = RackPos(i);
                Vector2 slot = StitcherVfx.SlotGround(cachedAnchor, i, count) + new Vector2(0f, -14f);
                PRTLoader.NewParticle<PRT_StitcherBoneCast>(rack, Vector2.Zero, StitcherVfx.BoneDust, 0.95f)
                    ?.Configure(rack, slot, 8 + i * 3);
            }
        }

        /// <summary>流产沿：断线反噬（鞭抽+断线头迸散）+ 囤骨哗啦全洒——浪费要有痛感</summary>
        private void InterruptRead() {
            stockDropHandled = true;
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 2 }, NPC.Center);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 3 }, NPC.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_StitcherThreadSnip>(
                    NPC.Center + new Vector2(NPC.direction * 18f, -26f + i * 10f),
                    new Vector2(NPC.direction * Main.rand.NextFloat(0.5f, 2f), Main.rand.NextFloat(-2.5f, -0.5f)),
                    StitcherVfx.ThreadGold)?.Configure(Main.rand.Next(22, 32), Main.rand.NextFloat(10f, 20f));
            }
            int waste = Math.Min(StockCap, seenStock);
            for (int i = 0; i < waste * 2 + 2; i++) {
                PRTLoader.NewParticle<PRT_SkeleBoneChip>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), -20f),
                    new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-3.5f, -1f)),
                    StitcherVfx.BoneDust, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(26, 40));
            }
        }

        /// <summary>退避沿：骨雾遁——尘扑 + 骨白雾团（非传送，与 Boss 背袭传送读感彻底区分）</summary>
        private void RetreatRead() {
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 3 }, NPC.Center);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0.5f));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_DwMist>(NPC.Center + VaultUtils.RandVr(2f, 12f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.5f, -0.1f)),
                    StitcherVfx.BoneDust, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(Main.rand.Next(34, 50), 0.02f, 0.008f, 0.45f);
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
            //缝合条件：≥2 且玩家 >200px；囤满 3 无视距离强制缝合（防风筝到永不缝合）。
            //锚（脚底列+行）冻结入 ai[2]，此后骨堆预告与生成点全由锚推导
            if (NPC.velocity.Y == 0f
                && (StackCount >= StockCap || (StackCount >= 2f && playerDist > 200f))) {
                ChangeState(StateStitch, StitcherVfx.PackAnchor(NPC.Bottom.ToTileCoordinates()));
            }
        }

        //==================== 缝合（channel 90f，承伤 12% 流产）====================

        private void UpdateStitch() {
            StateTimer++;
            NPC.velocity.X *= 0.8f;
            int t = (int)StateTimer;
            //锚各端缓存（ChangeState 会清 ai[2]，完成/流产沿要用）
            cachedAnchor = StitcherVfx.UnpackAnchor(StateParam);
            hasAnchor = true;
            int stock = Math.Min(StockCap, (int)StackCount);

            //缝线绷紧音三拍（严格前进沿）：每拍一根骨件锁定针脚，针闪一粒
            for (int i = 0; i < 3; i++) {
                if (t >= 15 + i * 30 && BeatForward(i + 1)) {
                    glintFlash = 5;
                    SoundEngine.PlaySound(SoundID.Item37 with {
                        Volume = 0.45f,
                        Pitch = -0.3f + i * 0.25f,
                        MaxInstances = 3,
                    }, NPC.Center);
                }
            }

            //缝合位骨堆尘雾（表现端预告伴奏；末 6f 预静默，全场屏息）
            if (!Main.dedServ && stock > 0 && t < StitchFrames - 6) {
                if ((int)AmbientClock % 5 == 0) {
                    for (int s = 0; s < stock; s++) {
                        Vector2 g = StitcherVfx.SlotGround(cachedAnchor, s, stock);
                        Dust d = Dust.NewDustDirect(g - new Vector2(10f, 4f), 20, 4, DustID.Bone,
                            0f, Main.rand.NextFloat(-1.2f, -0.4f));
                        d.noGravity = true;
                        d.scale *= 0.9f;
                    }
                }
                Lighting.AddLight(NPC.Center, StitcherVfx.ThreadGold.ToVector3() * 0.14f);
            }

            if (VaultUtils.isClient) {
                return;
            }
            //channel 承伤流产：转入踉跄硬直（断线反噬），骨料全废
            if (channelDamage >= NPC.lifeMax * InterruptRatio) {
                StackCount = 0f;
                NPC.velocity.X = -NPC.direction * 1.6f;
                retreatCooldown = RetreatCooldownFrames;
                ChangeState(StateInterrupted);
                return;
            }
            if (t < StitchFrames) {
                return;
            }
            //缝合完成：锚在 ChangeState 清参前取出；生成点与各端骨堆预告同函数推导（预告即承诺）
            Point anchor = StitcherVfx.UnpackAnchor(StateParam);
            int count = Math.Min(StockCap, (int)StackCount);
            for (int i = 0; i < count; i++) {
                Vector2 g = StitcherVfx.SlotGround(anchor, i, count);
                NPC.NewNPC(NPC.GetSource_FromAI(), (int)g.X, (int)g.Y, ModContent.NPCType<RestitchedBones>());
            }
            StackCount = 0f;
            ChangeState(StateScavenge);
        }

        //==================== 流产踉跄（惩罚窗）====================

        private void UpdateInterrupted() {
            StateTimer++;
            NPC.velocity.X *= 0.88f;
            if (!Main.dedServ && StateTimer < 18f && (int)StateTimer % 3 == 0) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0.2f));
            }
            if (!VaultUtils.isClient && StateTimer >= InterruptFrames) {
                ChangeState(StateScavenge);
            }
        }

        //==================== 退避 ====================

        private void UpdateRetreat() {
            StateTimer++;
            //落地拍：尘起 + 闷响（余波相，严格前进沿防回卷）
            if (StateTimer > 4f && NPC.velocity.Y == 0f && BeatForward(1) && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 3 }, NPC.Bottom);
                for (int i = 0; i < 6; i++) {
                    Dust.NewDust(NPC.Bottom - new Vector2(12f, 4f), 24, 4, DustID.Bone,
                        Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(-1f, 0f));
                }
            }
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
            //channel 承伤累计（打端/服务器都会进；裁决只消费服务器侧的值，各端本地值驱动线身磨损外显）
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
            if (NPC.life <= 0) {
                DeathRead(hit);
            }
        }

        /// <summary>死亡读感：缝匠死了线就全断——断线头迸散 + 囤骨与袍骨崩落 + 骨尘雾</summary>
        private void DeathRead(NPC.HitInfo hit) {
            int stock = (int)StackCount;
            for (int i = 0; i < 3 + stock; i++) {
                PRTLoader.NewParticle<PRT_StitcherThreadSnip>(
                    NPC.Center + VaultUtils.RandVr(4f, 18f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(0.5f, 2.5f), Main.rand.NextFloat(-3f, -0.5f)),
                    StitcherVfx.ThreadGold)?.Configure(Main.rand.Next(24, 36), Main.rand.NextFloat(9f, 18f));
            }
            for (int i = 0; i < 5 + stock * 2; i++) {
                PRTLoader.NewParticle<PRT_SkeleBoneChip>(
                    NPC.Center + VaultUtils.RandVr(2f, 12f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(0f, 2f) + Main.rand.NextFloat(-1.5f, 1.5f),
                        Main.rand.NextFloat(-4f, -1f)),
                    StitcherVfx.BoneDust, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(28, 44));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_DwMist>(NPC.Center + VaultUtils.RandVr(2f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.4f, -0.1f)),
                    StitcherVfx.BoneDust, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(36, 52), 0.018f, 0.007f, 0.4f);
            }
        }

        /// <summary>ai[3] 前进沿：取材飞行动画（来源点按目标列地表解析）；清空沿由状态沿接管或通用散落</summary>
        private void ObserveStock() {
            int stock = (int)StackCount;
            if (AmbientClock <= 1f) {
                //迟入端首帧静默对齐（netcode 7.5/7.9）
                seenStock = stock;
                return;
            }
            if (stock > seenStock) {
                takeAnim = TakeFlightFrames;
                takeFrom = ResolveTakeSource();
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.35f, Pitch = 0.6f, MaxInstances = 3 }, NPC.Center);
                }
            }
            else if (stock < seenStock && !stockDropHandled && !Main.dedServ) {
                //非沿接管的清空（如死亡帧）：骨料散落
                for (int i = 0; i < 8; i++) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                        Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2f, 0f));
                }
            }
            seenStock = stock;
        }

        /// <summary>取材来源：巡骨目标列的地表（骨从尸骨点飞来，不是凭空冒出）；无记录退化为朝向前方</summary>
        private Vector2 ResolveTakeSource() {
            if ((int)State == StateScavenge && StateParam > 0f) {
                Point col = new((int)StateParam, (int)(NPC.Bottom.Y / 16f));
                return StitcherVfx.SlotGround(col, 0, 1) + new Vector2(0f, -8f);
            }
            return NPC.Center + new Vector2(NPC.direction * 60f, -8f);
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
            int st = (int)State;
            if (st == StateStitch) {
                //缝合锁帧：引线姿态
                NPC.frame.Y = frameHeight * Math.Min(1, count - 1);
                return;
            }
            if (st == StateInterrupted) {
                NPC.frame.Y = 0;
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
            int stock = Math.Min(StockCap, (int)StackCount);
            bool stitching = (int)State == StateStitch;
            //下层线（图元即时落地 → 压在本批后续贴图之下）：携行牵线 / 缝合位长线
            DrawThreadWeb(spriteBatch, stock, stitching, underLayer: true);
            DrawDustRobeLift(spriteBatch, screenPos);
            //骨件与骨堆（内部切干骨批再还原，顺带冲刷上面两层贴图）
            DrawBones(spriteBatch, screenPos, drawColor, stock, stitching);
            //上层线：手→架骨操纵短线（压在骨件之上，织机读法）
            DrawThreadWeb(spriteBatch, stock, stitching, underLayer: false);
            //加色亮件：针闪（节拍/完成沿的一粒星；加色批强度乘进色值）
            if (glintFlash > 0) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    BeginAdditive(spriteBatch);
                    float k = glintFlash / 6f;
                    Vector2 hand = HandPos - screenPos;
                    Vector2 gOrigin = glow.Size() * 0.5f;
                    spriteBatch.Draw(glow, hand, null, StitcherVfx.ThreadCore * (0.8f * k), 0f,
                        gOrigin, 0.08f + 0.30f * k, SpriteEffects.None, 0f);
                    spriteBatch.Draw(glow, hand, null, StitcherVfx.ThreadGold * (0.5f * k), 0f,
                        gOrigin, 0.1f + 0.7f * k, SpriteEffects.None, 0f);
                    BeginDefault(spriteBatch);
                }
            }
        }

        /// <summary>引线手位（缝线源头）</summary>
        private Vector2 HandPos => NPC.Center + new Vector2(NPC.direction * 10f, -2f);

        /// <summary>收架位：面前竖列如织机</summary>
        private Vector2 RackPos(int index)
            => NPC.Center + new Vector2(NPC.direction * 22f, -30f + index * 15f);

        /// <summary>channel 承伤外显：线身磨损缺口随累计承伤加深（打断进度可读）</summary>
        private float ChannelFray
            => MathHelper.Clamp(channelDamage / (NPC.lifeMax * InterruptRatio), 0f, 1f) * 0.65f;

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
            //流产踉跄期袍光抽搐（断线反噬的余痛）
            float lift = (int)State == StateInterrupted
                ? 0.30f * (0.5f + 0.5f * MathF.Abs(MathF.Sin(AmbientClock * 0.9f)))
                : 0.30f;
            Color liftCol = new Color(StitcherVfx.BoneDust.R, StitcherVfx.BoneDust.G, StitcherVfx.BoneDust.B, 0)
                * (lift * NPC.Opacity);
            sb.Draw(tex, pos, frame, liftCol, 0f, frame.Size() * 0.5f, 1f, fx, 0f);
        }

        /// <summary>
        /// 骨件位置统一函数（公转/收架两态 + 取材飞入插值）：绘制、线锚、完成沿甩件共用一份
        /// 几何，防层间错位。收架 12f 内从公转位收拢；未锁件震颤随进度加深，末 6f 预静默冻结
        /// </summary>
        private Vector2 BonePos(int index, int stock, bool stitching) {
            Vector2 basePos;
            if (stitching) {
                float form = MathHelper.Clamp(StateTimer / 12f, 0f, 1f);
                form = 1f - (1f - form) * (1f - form) * (1f - form);
                basePos = Vector2.Lerp(OrbitSlot(index, stock), RackPos(index), form);
                int locked = Math.Min((int)NPC.localAI[3], stock);
                if (index >= locked && StateTimer < StitchFrames - 6f) {
                    float tr = Math.Min(1f, StateTimer / StitchFrames);
                    basePos += new Vector2(MathF.Sin(AmbientClock * 1.9f + index * 2.1f),
                        MathF.Sin(AmbientClock * 2.5f + index)) * (0.6f + tr * 1.6f);
                }
            }
            else {
                basePos = OrbitSlot(index, stock);
            }
            //取材飞入：最新件沿上拱弧线从尸骨点收进轨道（收线读感）
            if (index == stock - 1 && takeAnim > 0) {
                float e = 1f - takeAnim / (float)TakeFlightFrames;
                e = e * e * (3f - 2f * e);
                Vector2 ctrl = (takeFrom + basePos) * 0.5f + new Vector2(0f, -46f);
                basePos = Vector2.Lerp(Vector2.Lerp(takeFrom, ctrl, e), Vector2.Lerp(ctrl, basePos, e), e);
            }
            return basePos;
        }

        /// <summary>公转槽：腰际压扁椭圆（骨件像挂着的货，不是光环）</summary>
        private Vector2 OrbitSlot(int index, int stock) {
            float angle = AmbientClock * 0.05f + Seed + index * MathHelper.TwoPi / Math.Max(1, stock);
            return NPC.Center + new Vector2(0f, -6f)
                + new Vector2(MathF.Cos(angle) * 27f, MathF.Sin(angle) * 16f);
        }

        private float BoneRot(int index, int stock, bool stitching, int locked) {
            float orbitRot = (AmbientClock * 0.05f + Seed + index * MathHelper.TwoPi / Math.Max(1, stock)) * 2.4f;
            if (!stitching) {
                return orbitRot;
            }
            float form = MathHelper.Clamp(StateTimer / 12f, 0f, 1f);
            //收架对齐竖列；已锁件严格竖直（针脚落定），未锁件残留小晃
            float rackRot = MathHelper.PiOver2
                + (index < locked ? 0f : MathF.Sin(AmbientClock * 0.31f + index * 1.7f) * 0.14f);
            return MathHelper.Lerp(orbitRot % MathHelper.TwoPi, rackRot, form);
        }

        /// <summary>
        /// 骨件本体（干骨批复合：干白重调+裂纹+缝金接缝）+ 缝合位骨堆实体化预告
        /// （堆在长成、随进度立起——预告是实体不是贴花）
        /// </summary>
        private void DrawBones(SpriteBatch sb, Vector2 screenPos, Color drawColor, int stock, bool stitching) {
            if (stock <= 0) {
                return;
            }
            Main.instance.LoadItem(ItemID.Bone);
            Texture2D bone = TextureAssets.Item[ItemID.Bone]?.Value;
            if (bone == null) {
                return;
            }
            Vector2 origin = bone.Size() * 0.5f;
            float fray = ChannelFray;
            int locked = Math.Min((int)NPC.localAI[3], stock);
            float prog = stitching ? MathHelper.Clamp(StateTimer / StitchFrames, 0f, 1f) : 0f;
            bool dress = StitcherVfx.BeginDress(sb);

            for (int i = 0; i < stock; i++) {
                Vector2 pos = BonePos(i, stock, stitching);
                float rot = BoneRot(i, stock, stitching, locked);
                if (dress) {
                    StitcherVfx.SetDressParams(bone, bone.Bounds, 0.85f, 0.10f + fray * 0.35f,
                        stitching && i < locked ? 0.9f : 0.3f, new Vector2(0.34f, 0.66f), Seed + i * 0.171f);
                }
                Color col = (dress ? drawColor : drawColor.MultiplyRGB(StitcherVfx.BoneDust)) * NPC.Opacity;
                sb.Draw(bone, pos - screenPos, null, col, rot, origin, 0.9f, SpriteEffects.None, 0f);
            }

            //缝合位骨堆：交叉双骨随进度立起、缝金渐亮（与生成点同函数推导）
            if (stitching && hasAnchor) {
                float rise = 1f - (1f - prog) * (1f - prog);
                for (int s = 0; s < stock; s++) {
                    Vector2 g = StitcherVfx.SlotGround(cachedAnchor, s, stock);
                    Color lit = Lighting.GetColor(g.ToTileCoordinates());
                    for (int k = 0; k < 2; k++) {
                        float ang = (k == 0 ? 1.15f : -0.95f) * (1f - rise * 0.35f);
                        Vector2 p = g + new Vector2((k * 2 - 1) * 5f, -5f - rise * 4f);
                        if (dress) {
                            StitcherVfx.SetDressParams(bone, bone.Bounds, 1f, 0.4f - prog * 0.25f,
                                prog * 0.9f, new Vector2(0.3f, 0.7f), Seed + s * 0.313f + k * 0.531f);
                        }
                        Color hc = (dress ? lit : lit.MultiplyRGB(StitcherVfx.BoneDust)) * (0.35f + 0.65f * prog);
                        sb.Draw(bone, p - screenPos, null, hc, ang, origin, 0.72f + rise * 0.16f, SpriteEffects.None, 0f);
                    }
                }
            }

            if (dress) {
                StitcherVfx.EndDress(sb);
            }
        }

        /// <summary>
        /// 骨线网三层语义：巡骨=手→骨件松弛携行线（取材件收线绷紧）；缝合下层=架骨→缝合位
        /// 长线（进度绷紧，玻点奔向骨堆=承诺方向）；缝合上层=手→架骨操纵短线（节拍逐根绷紧）
        /// </summary>
        private void DrawThreadWeb(SpriteBatch sb, int stock, bool stitching, bool underLayer) {
            if (stock <= 0) {
                return;
            }
            float fray = ChannelFray;
            Vector2 hand = HandPos;
            if (underLayer) {
                if (stitching && hasAnchor) {
                    float prog = MathHelper.Clamp(StateTimer / (float)StitchFrames, 0f, 1f);
                    for (int i = 0; i < stock; i++) {
                        Vector2 slot = StitcherVfx.SlotGround(cachedAnchor, i, stock) + new Vector2(0f, -6f);
                        float tension = MathHelper.Clamp(prog * 1.15f - i * 0.06f, 0.12f, 0.95f);
                        StitcherVfx.DrawThread(sb, BonePos(i, stock, true), slot, tension, fray,
                            0.85f * NPC.Opacity, Seed + i * 0.311f);
                    }
                }
                else if (!stitching) {
                    for (int i = 0; i < stock; i++) {
                        float tension = i == stock - 1 && takeAnim > 0
                            ? MathHelper.Clamp(1.4f - takeAnim / (float)TakeFlightFrames, 0f, 0.9f)
                            : 0.18f;
                        StitcherVfx.DrawThread(sb, hand, BonePos(i, stock, false), tension, fray * 0.3f,
                            0.55f * NPC.Opacity, Seed + i * 0.311f);
                    }
                }
            }
            else if (stitching) {
                int locked = Math.Min((int)NPC.localAI[3], stock);
                for (int i = 0; i < stock; i++) {
                    float tension = i < locked ? 0.95f : 0.35f;
                    StitcherVfx.DrawThread(sb, hand, BonePos(i, stock, true), tension, fray,
                        0.9f * NPC.Opacity, Seed + 7f + i * 0.173f, 2.2f);
                }
            }
        }
    }

    /// <summary>
    /// 重拼骨（缝匠伴生怪）：以"拼错了"的姿势重新站起来的骷髅。三段切片（头/胸/腿=缝合件
    /// 粒度）是绘制真值：拼装期切片从骨堆升起合拢猛合（0.55s 反应窗，期间不咬人=公平阀），
    /// 追猎期头段常抖+接缝火花随寿命变密，18s 后散架期切片下坠散开、磨损蚀尽（压力有界）。
    /// 状态机：0 拼装 34f → 1 追猎 1080f → 2 散架 26f；转移全服务器。不入图鉴、无掉落。
    /// </summary>
    internal class RestitchedBones : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.AngryBones;

        private const int StateAssemble = 0;
        private const int StateHunt = 1;
        private const int StateCollapse = 2;

        private const int AssembleFrames = 34;
        /// <summary>追猎存活帧：18s 散架</summary>
        private const int LifeFrames = 1080;
        private const int CollapseFrames = 26;
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

        /// <summary>拼装/散架期不咬人：起身 0.55s 是玩家的处决窗（公平阀）</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
            => (int)State == StateHunt;

        public override void AI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateEdgeCue();
            }
            ServerSyncPacer(36);
            switch ((int)State) {
                case StateAssemble:
                    UpdateAssemble();
                    break;
                case StateHunt:
                    UpdateHunt();
                    break;
                default:
                    UpdateCollapse();
                    break;
            }
            NPC.spriteDirection = NPC.direction != 0 ? NPC.direction : NPC.spriteDirection;
        }

        private void PlayStateEdgeCue() {
            if (Main.dedServ) {
                return;
            }
            switch ((int)State) {
                case StateAssemble:
                    //骨堆开始蠕动：干响低语（起身重拍在拼装内的 BeatForward 上，此处不抢戏）
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.3f, Pitch = -0.4f, MaxInstances = 3 }, NPC.Center);
                    break;
                case StateCollapse:
                    //线松了：缝匠的针脚到期，骨架自己知道
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.35f, Pitch = -0.55f, MaxInstances = 3 }, NPC.Center);
                    break;
            }
        }

        //==================== 拼装（骨堆→合拢→猛合起身）====================

        private void UpdateAssemble() {
            StateTimer++;
            NPC.velocity.X = 0f;
            float t = StateTimer;
            if (!Main.dedServ) {
                //聚拢期地面骨尘上涌
                if (t < 22f && (int)AmbientClock % 3 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Bottom - new Vector2(14f, 6f), 28, 6, DustID.Bone,
                        0f, Main.rand.NextFloat(-1.4f, -0.4f));
                    d.noGravity = true;
                }
                //猛合拍：缝线绷紧 + 骨件咔哒对齐（严格前进沿）
                if (t >= 28f && BeatForward(1)) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.45f, Pitch = 0.15f, MaxInstances = 3 }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 3 }, NPC.Center);
                    for (int i = 0; i < 6; i++) {
                        Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                            Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-1.5f, 0f));
                    }
                }
            }
            if (!VaultUtils.isClient && t >= AssembleFrames) {
                ChangeState(StateHunt);
            }
        }

        //==================== 追猎 ====================

        private void UpdateHunt() {
            StateTimer++;
            if (!VaultUtils.isClient && StateTimer >= LifeFrames) {
                ChangeState(StateCollapse);
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

            //接缝火花：越近寿命末端越密（针脚在崩，散架的诚实预告）
            float age = Age01;
            int sparkRate = Math.Max(4, (int)MathHelper.Lerp(26f, 8f, age));
            if (!Main.dedServ && Main.rand.NextBool(sparkRate)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-14f, 0f)),
                    VaultUtils.RandVr(0.5f, 1.5f), StitcherVfx.ThreadGold,
                    Main.rand.NextFloat(0.2f, 0.35f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        //==================== 散架 ====================

        private void UpdateCollapse() {
            StateTimer++;
            NPC.velocity.X *= 0.8f;
            if (!Main.dedServ && (int)AmbientClock % 3 == 0) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone,
                    Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            if (!VaultUtils.isClient && StateTimer >= CollapseFrames) {
                NPC.life = 0;
                NPC.checkDead();
            }
        }

        /// <summary>寿命进度（追猎 0~1；散架恒 1）：驱动火花密度、磨损、针脚闪烁告警</summary>
        private float Age01 => (int)State switch {
            StateHunt => MathHelper.Clamp(StateTimer / (float)LifeFrames, 0f, 1f),
            StateCollapse => 1f,
            _ => 0f,
        };

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
                        StitcherVfx.ThreadGold, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(8, 14));
                }
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_StitcherThreadSnip>(NPC.Center + VaultUtils.RandVr(2f, 10f),
                        new Vector2(hit.HitDirection * Main.rand.NextFloat(0.5f, 2f), Main.rand.NextFloat(-2.5f, -0.5f)),
                        StitcherVfx.ThreadGold)?.Configure(Main.rand.Next(20, 30), Main.rand.NextFloat(8f, 14f));
                }
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_SkeleBoneChip>(NPC.Center + VaultUtils.RandVr(2f, 8f),
                        new Vector2(hit.HitDirection * Main.rand.NextFloat(0f, 2f) + Main.rand.NextFloat(-1.5f, 1.5f),
                            Main.rand.NextFloat(-3.5f, -1f)),
                        StitcherVfx.BoneDust, Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(24, 40));
                }
            }
        }

        //==================== 帧与绘制 ====================

        public override void FindFrame(int frameHeight) {
            //拼装/散架锁首帧：AnimationType 的行走循环只在追猎期跑
            if ((int)State != StateHunt) {
                NPC.frame.Y = 0;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态泄漏自愈（netcode 7.2）
            BeginDefault(spriteBatch);
            Texture2D tex = TextureAssets.Npc[Type]?.Value;
            if (tex == null) {
                return true;
            }
            //拼装期：缝匠牵线可见（600px 内寻主，线随合拢绷紧——供货关系可读）
            if ((int)State == StateAssemble) {
                NPC master = FindMaster();
                if (master != null) {
                    float tighten = MathHelper.Clamp(StateTimer / (float)AssembleFrames, 0f, 1f);
                    StitcherVfx.DrawThread(spriteBatch,
                        master.Center + new Vector2(master.direction * 10f, -2f),
                        NPC.Center + new Vector2(0f, -8f),
                        0.25f + tighten * 0.7f, 0.1f, 0.75f, Seed);
                }
            }
            DrawAssembly(spriteBatch, screenPos, drawColor, tex);
            return false;
        }

        /// <summary>
        /// 三段切片绘制（头/胸/腿=缝合件粒度，切口边即缝线接缝）：拼装期从骨堆升起合拢猛合；
        /// 追猎期合拢为整身（头段常抖=缝歪身份，寿命末端针脚闪烁告警）；散架期切片下坠散开、
        /// 磨损蚀尽。干骨批给每段独立干白重调与缝金接缝；着色器缺失回退苍白平染
        /// </summary>
        private void DrawAssembly(SpriteBatch sb, Vector2 screenPos, Color drawColor, Texture2D tex) {
            Rectangle frame = NPC.frame;
            int h1 = Math.Min(14, frame.Height);
            int h2 = Math.Min(28, frame.Height);
            Rectangle[] slices = [
                new(frame.X, frame.Y, frame.Width, h1),
                new(frame.X, frame.Y + h1, frame.Width, h2 - h1),
                new(frame.X, frame.Y + h2, frame.Width, frame.Height - h2),
            ];
            //各段接缝位（段内 uv）：贴着切口边
            Vector2[] seams = [new(0.92f, 0.92f), new(0.06f, 0.94f), new(0.08f, 0.08f)];

            int st = (int)State;
            float ap = 0f, cp = 0f, aIn = 1f;
            if (st == StateAssemble) {
                ap = SnapProgress(StateTimer);
                aIn = MathHelper.Clamp(StateTimer / 8f, 0f, 1f);
            }
            else if (st == StateCollapse) {
                cp = MathHelper.Clamp(StateTimer / CollapseFrames, 0f, 1f);
            }
            float age = Age01;
            //磨损：拼装期从噪声蚀块中"长实"（ap→0 收敛 0.10）；追猎期终末 0.38≈蚀损 1/4
            //（0.55 会蚀掉四成像素，行走体碎过头）；散架期蚀尽
            float wear = st switch {
                StateCollapse => 0.42f + 0.58f * cp,
                StateAssemble => 0.10f + 0.35f * ap,
                _ => 0.08f + age * 0.30f,
            };
            //末程针脚闪烁告警（散架前的诚实预告）
            float seamGlow = age > 0.85f
                ? 0.45f + 0.55f * MathF.Abs(MathF.Sin(AmbientClock * 0.55f))
                : 0.55f;

            float frameTopY = NPC.Bottom.Y - frame.Height + 4f + NPC.gfxOffY;
            SpriteEffects fx = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            bool dress = StitcherVfx.BeginDress(sb);

            for (int i = 0; i < 3; i++) {
                if (slices[i].Height <= 0) {
                    continue;
                }
                float sliceY = i == 0 ? 0f : i == 1 ? h1 : h2;
                Vector2 pos = new(NPC.Center.X, frameTopY + sliceY + slices[i].Height * 0.5f);
                float rot = 0f;
                //头段常抖：缝歪的身份签名（整数像素，寿命末端加剧）
                if (i == 0 && st != StateCollapse) {
                    pos.X += (int)MathF.Round(MathF.Sin(AmbientClock * 0.55f + Seed) * (1.6f + age * 1.2f));
                    pos.Y += (int)MathF.Round(MathF.Sin(AmbientClock * 0.83f + Seed * 2f) * 0.9f);
                }
                if (st == StateAssemble) {
                    //从骨堆升起：全段起于脚底，横向散开、带转，随 ap→0 归位
                    float toGround = NPC.Bottom.Y - 6f - pos.Y;
                    pos.Y += toGround * ap;
                    pos.X += (i - 1) * 9f * ap;
                    rot = (i - 1) * 0.85f * ap;
                }
                else if (st == StateCollapse) {
                    float e = cp * cp;
                    pos.Y += e * (8f + i * 7f);
                    pos.X += (i - 1) * 10f * e;
                    rot = (i - 1) * 1.15f * e;
                }
                if (dress) {
                    StitcherVfx.SetDressParams(tex, slices[i], 0.6f, wear, seamGlow, seams[i], Seed + i * 0.29f);
                }
                Color col = (dress ? drawColor : drawColor.MultiplyRGB(PaleBone))
                    * (NPC.Opacity * aIn * (st == StateCollapse ? 1f - cp * 0.6f : 1f));
                sb.Draw(tex, pos - screenPos, slices[i], col, rot,
                    new Vector2(frame.Width * 0.5f, slices[i].Height * 0.5f), 1f, fx, 0f);
            }
            if (dress) {
                StitcherVfx.EndDress(sb);
            }
        }

        /// <summary>拼装曲线：0~22f 聚拢到 0.3 → 22~27f 悬停屏息（预静默）→ 27f 起 4f 猛合归零</summary>
        private static float SnapProgress(float t) {
            if (t <= 22f) {
                float e = t / 22f;
                return 1f - e * e * 0.7f;
            }
            if (t <= 27f) {
                return 0.3f;
            }
            return 0.3f * (1f - MathHelper.Clamp((t - 27f) / 4f, 0f, 1f));
        }

        /// <summary>寻主：600px 内最近缝匠（拼装牵线用，纯表现）</summary>
        private NPC FindMaster() {
            int type = ModContent.NPCType<BoneStitcher>();
            NPC best = null;
            float bestDist = 600f;
            foreach (NPC n in Main.ActiveNPCs) {
                if (n.type != type) {
                    continue;
                }
                float d = Vector2.Distance(n.Center, NPC.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = n;
                }
            }
            return best;
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
