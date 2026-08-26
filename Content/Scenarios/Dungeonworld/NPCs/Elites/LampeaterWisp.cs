using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
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
    /// 噬灯魂（L3 大档案馆 / L5 深巷，WAVE2-ENEMIES §3.2）：咬一口吹熄你的视野（黑暗减益），
    /// 吃饱三口后它自己亮起来，黑暗里唯一的光既是猎人也是路标。
    /// 状态机：0 暗漂（环距 260~420px 游弋，高 alpha 半隐）→ 1 扑灯（22f 战栗+增亮+倒吸
    /// 三重奏 telegraph → 26f 直线冲刺 → 45f 硬直漂回）。ai[3]=进食计数（≤3，3=饱食常驻）。
    /// 伤害窗声明：接触伤害只在冲刺 26f 内成立，暗漂贴脸不掉血——预告即承诺的反面也成立。
    /// 材质身份=吞灯的烛烟：墨烟壳（LampeaterWisp.fx TechSmokeBody，唯一暗层，画在精灵图下）
    /// 裹粒状烬芯（TechEmberFlame，画在精灵图上）；签名行为=速度拉伸烟体 / 进食分级点亮
    /// 烬芯+灯魂珠可数 / 扑食前整团向心倒吸。屏幕语言=吞光域（LampeaterVeil.fx，
    /// LampeaterVeilRender 拷屏后效）：周身亮度被吃出一圈暗带，咬中坍缩、死亡释放。
    /// 联机：掷点/扑灯时机/进食计数全服务器；减益经原版 buff 同步；出生 alpha 目标=状态函数，
    /// 任何端进场即收敛；光照变化全部走本地 AddLight 与屏幕后效（各端读同一份 ai 推同一份
    /// 画面，权威在状态不在演出），不写 tile。
    /// 暗漂态保留 0.06 强度呼吸微光（全黑挫败感的下限保险丝）。
    /// </summary>
    internal class LampeaterWisp : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.DungeonSpirit;

        //==================== 参数（建议值，验收再调）====================

        private const int StateDrift = 0;
        private const int StatePounce = 1;

        /// <summary>扑灯 telegraph / 冲刺 / 硬直帧</summary>
        private const int TelegraphFrames = 22;
        private const int DashFrames = 26;
        private const int RecoverFrames = 45;
        /// <summary>环距带 [260,420]，中点 340</summary>
        private const float RingMid = 340f;
        /// <summary>饱食判定口数</summary>
        private const int SatiatedBites = 3;
        /// <summary>烟壳 quad 基准尺寸（世界 px，含撕散余量）</summary>
        private const float QuadW = 92f;
        private const float QuadH = 132f;

        /// <summary>烛橙烬芯</summary>
        internal static readonly Color EmberOrange = new(255, 190, 110);
        /// <summary>墨褐近黑烟体（L3 纸墨褐强调色系）</summary>
        private static readonly Color InkBody = new(70, 58, 48);
        /// <summary>被吞的灯火色（灯魂珠/拉光痕同源）</summary>
        private static readonly Color LampGold = new(255, 226, 158);

        /// <summary>拖影环形缓冲（各端本地表现）</summary>
        private readonly Vector2[] trail = new Vector2[6];
        private int trailWritten;
        /// <summary>已观察进食数（各端本地，前进沿触发进食演出）</summary>
        private int seenBites;
        /// <summary>进食胀亮脉冲（本地表现，吃到一口置 1 后衰减）</summary>
        private float swell;
        /// <summary>烟壳朝向平滑角（本地表现）</summary>
        private float visRot;

        private bool Satiated => StackCount >= SatiatedBites;

        /// <summary>telegraph 倒吸进度 0~1（各端由 ai 推导）</summary>
        private float InhaleProgress
            => (int)State == StatePounce && StateTimer <= TelegraphFrames
                ? StateTimer / TelegraphFrames
                : 0f;

        /// <summary>冲刺窗口内（伤害窗与拖影/落烬窗同源）</summary>
        private bool InDashWindow {
            get {
                int t = (int)StateTimer;
                return (int)State == StatePounce && t > TelegraphFrames && t <= TelegraphFrames + DashFrames;
            }
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.DungeonSpirit];
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 22;
            NPC.height = 30;
            NPC.damage = 38;
            NPC.defense = 4;
            NPC.lifeMax = 170;
            NPC.knockBackResist = 0.6f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            //不穿墙：穿墙魂读感廉价，书架迷宫里会失去追逐张力
            NPC.noTileCollide = false;
            NPC.npcSlots = 1f;
            NPC.value = 0f;
            NPC.alpha = 180;
            NPC.HitSound = SoundID.NPCHit36;
            NPC.DeathSound = SoundID.NPCDeath39;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.LampeaterWisp.Bestiary"),
            ]);
        }

        //==================== 投放（§4：L3 下三分带 0.12 / 上中带 0.06；L4 干段 0.03；L5 深巷 0.08）====================

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            if (!DungeonworldEliteDirector.CommonSpawnGate(spawnInfo, Type)) {
                return 0f;
            }
            int band = DungeonworldEliteDirector.BandIndexForRow(spawnInfo.SpawnTileY);
            float depth = DungeonworldEliteDirector.BandDepth01(spawnInfo.SpawnTileY);
            switch (band) {
                case 2:
                    return depth >= 2f / 3f ? 0.12f : 0.06f;
                case 3:
                    //干段稀客：湿舱段水域让位给沉波狱吏
                    bool wet = spawnInfo.Water
                        || DungeonworldEliteDirector.InWetCompartment(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY, out _);
                    return wet ? 0f : 0.03f;
                case 4:
                    return depth >= 2f / 3f ? 0.08f : 0f;
                default:
                    return 0f;
            }
        }

        //==================== AI ====================

        /// <summary>alpha 目标=状态的确定函数：暗漂 180 / 扑灯 60 / 饱食 0</summary>
        private int AlphaTarget() {
            if (Satiated) {
                return 0;
            }
            return (int)State == StatePounce ? 60 : 180;
        }

        public override void AI() {
            HealAlpha(AlphaTarget(), 8);
            AmbientClock++;
            if (StateEdge() && (int)State == StatePounce) {
                //绷紧战栗的起手气音
                SoundEngine.PlaySound(SoundID.NPCHit36 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 3 }, NPC.Center);
            }
            ServerSyncPacer();
            PushTrail();
            ObserveBites();
            swell *= 0.93f;

            NPC.TargetClosest(faceTarget: false);
            Player target = NPC.HasValidTarget ? Main.player[NPC.target] : null;

            if ((int)State == StateDrift) {
                UpdateDrift(target);
            }
            else {
                UpdatePounce(target);
            }

            DoGlowLight();
            ReportVeil();
            UpdateAmbientFx();
        }

        //==================== 暗漂 ====================

        private void UpdateDrift(Player target) {
            StateTimer++;
            float maxSpeed = Satiated ? 3.0f : 2.2f;

            if (target != null) {
                Vector2 toPlayer = target.Center - NPC.Center;
                float dist = Math.Max(1f, toPlayer.Length());
                Vector2 radial = toPlayer / dist;
                Vector2 tangent = new(-radial.Y, radial.X);
                float phase = AmbientClock * 0.017f + Seed;
                //环距软弹簧 + 每怪相位噪声切向漂移（法 9.1 连续值：确定相位，不掷 Main.rand）
                Vector2 desired = radial * MathHelper.Clamp((dist - RingMid) * 0.015f, -2.2f, 2.2f)
                    + tangent * (1.2f * MathF.Sin(phase))
                    + new Vector2(0f, MathF.Sin(phase * 1.7f) * 0.4f);
                if (desired.Length() > maxSpeed) {
                    desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
                }
                NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.045f);
            }
            else {
                NPC.velocity *= 0.97f;
            }
            BounceOffWalls();

            //服务器裁决：扑灯时机（间隔掷点存 ai[2]，进场首拍补掷）
            if (VaultUtils.isClient || target == null) {
                return;
            }
            if (StateParam <= 0f) {
                RollPounceInterval();
            }
            //饱食受击硬直 +50%：狂而不稳，被打断节奏（服务器读 justHit，计时回拨）
            if (Satiated && NPC.justHit) {
                StateTimer = Math.Max(0f, StateTimer - 20f);
            }
            if (StateTimer >= StateParam) {
                ChangeState(StatePounce);
            }
        }

        /// <summary>间隔 140~200f，饱食减半（服务器掷点乘 ai 过线）</summary>
        private void RollPounceInterval() {
            float interval = Main.rand.Next(140, 201);
            if (Satiated) {
                interval *= 0.5f;
            }
            StateParam = interval;
            NPC.netUpdate = true;
        }

        //==================== 扑灯（telegraph 22f + 冲刺 26f + 硬直 45f）====================

        private void UpdatePounce(Player target) {
            StateTimer++;
            int t = (int)StateTimer;

            //扑出气声（阈值+前进沿：客户端计时跳帧也不漏拍）
            if (t >= TelegraphFrames && BeatForward(1)) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.45f, Pitch = 0.4f, MaxInstances = 3 }, NPC.Center);
            }

            if (t <= TelegraphFrames) {
                //起手相：刹住漂移，周围的光与烟一起被向心倒吸（各端本地）
                NPC.velocity *= 0.86f;
                if (!Main.dedServ) {
                    if ((int)AmbientClock % 2 == 0) {
                        Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(28f, 60f);
                        PRTLoader.NewParticle<PRT_GhostRainMist>(from, (NPC.Center - from) * 0.1f,
                            InkBody * 0.65f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
                    }
                    //灯魂珠自外圈被拽进体内：倒吸的「光被拉走」可见沿
                    if ((int)AmbientClock % 4 == 0) {
                        Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(64f, 118f);
                        PRTLoader.NewParticle<PRT_LampeaterLightMote>(from,
                            (NPC.Center - from).SafeNormalize(Vector2.Zero) * 0.8f,
                            LampGold * 0.85f, Main.rand.NextFloat(0.45f, 0.75f))
                            ?.Configure(46, NPC, Main.rand.NextFloat(0.55f, 0.85f));
                    }
                }
                //冲刺发令只在服务器（velocity 一次性写入乘 SyncNPC，客户端本地积分不回卷；
                //服务器计时逐帧单调，等值判定安全）
                if (!VaultUtils.isClient && t == TelegraphFrames && target != null) {
                    float speed = Satiated ? 13.5f : 11f;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * speed;
                    NPC.netUpdate = true;
                }
                return;
            }

            if (t <= TelegraphFrames + DashFrames) {
                BounceOffWalls();
                //冲刺相：身后簌簌掉落冷却的余烬屑（飞行相的物质证据，各端本地）
                if (!Main.dedServ && (int)AmbientClock % 2 == 0) {
                    Vector2 shed = NPC.Center - NPC.velocity * Main.rand.NextFloat(0.5f, 1.5f)
                        + Main.rand.NextVector2Circular(8f, 8f);
                    PRTLoader.NewParticle<PRT_LampeaterEmber>(shed,
                        -NPC.velocity * 0.04f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        EmberOrange, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(18, 30), 0.3f);
                }
                //进食裁决只在服务器：冲刺窗口内碰撞盒相交=咬中（每次扑灯至多记一口）
                if (!VaultUtils.isClient && StateParam < 1f && target != null
                    && !target.dead && NPC.Hitbox.Intersects(target.Hitbox)) {
                    StateParam = 1f;
                    StackCount = Math.Min(SatiatedBites, StackCount + 1);
                    NPC.netUpdate = true;
                }
                return;
            }

            //硬直入拍：一蓬余烬与墨烟卸力（扑空/扑中都有的落势收相）
            if (BeatForward(2) && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCHit36 with { Volume = 0.3f, Pitch = -0.3f, MaxInstances = 3 }, NPC.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_LampeaterEmber>(NPC.Center + Main.rand.NextVector2Circular(10f, 10f),
                        VaultUtils.RandVr(0.8f, 2.2f), EmberOrange, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(Main.rand.Next(20, 34), 0.7f);
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center, VaultUtils.RandVr(0.4f, 1.4f),
                        InkBody * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(18, 30));
                }
            }
            //硬直漂回
            NPC.velocity *= 0.88f;
            if (!VaultUtils.isClient && t >= TelegraphFrames + DashFrames + RecoverFrames) {
                ChangeState(StateDrift);
                RollPounceInterval();
            }
        }

        /// <summary>贴墙滑行：法线反弹 + 确定性小偏航（12° 内，哈希相位，不掷 rand）</summary>
        private void BounceOffWalls() {
            bool bounced = false;
            if (NPC.collideX) {
                NPC.velocity.X = -NPC.oldVelocity.X * 0.55f;
                bounced = true;
            }
            if (NPC.collideY) {
                NPC.velocity.Y = -NPC.oldVelocity.Y * 0.55f;
                bounced = true;
            }
            if (bounced) {
                float yaw = MathF.Sin(NPC.whoAmI * 3.7f + AmbientClock * 0.13f) * 0.21f;
                NPC.velocity = NPC.velocity.RotatedBy(yaw);
            }
        }

        //==================== 伤害窗声明 + 咬中减益 ====================

        /// <summary>接触伤害只在冲刺窗口成立：暗漂是威胁的预告，不是伤害本体</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => InDashWindow;

        //咬中减益（受害端本地，原版 buff 自带同步，netcode 2.2 规避通道）
        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            //第二口升级判定用玩家身上 Darkness 剩余（各端可读）：8s 内再咬=致盲 4s
            bool second = target.HasBuff(BuffID.Darkness);
            target.AddBuff(BuffID.Darkness, 480);
            if (second) {
                target.AddBuff(BuffID.Blackout, 240);
            }
        }

        //==================== 进食观察（ai[3] 前进沿，各端一致演出）====================

        private void ObserveBites() {
            int bites = (int)StackCount;
            if (AmbientClock <= 1f) {
                //迟入端首帧静默对齐，不把既有进食数当新事件重播（netcode 7.5/7.9）
                seenBites = bites;
                return;
            }
            if (bites <= seenBites) {
                seenBites = Math.Min(seenBites, bites);
                return;
            }
            seenBites = bites;
            if (Main.dedServ) {
                return;
            }
            //进食四相之三（咬中冲击）：坍缩收环+胀亮+灯魂珠涌入+余烬迸散
            swell = 1f;
            LampeaterScreenEffects.TriggerPulse(NPC.Center, 150f + 30f * bites);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = -0.4f + bites * 0.25f, MaxInstances = 3 }, NPC.Center);
            for (int i = 0; i < 7; i++) {
                Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(70f, 150f);
                PRTLoader.NewParticle<PRT_LampeaterLightMote>(from,
                    (NPC.Center - from).SafeNormalize(Vector2.Zero) * 1.5f,
                    LampGold, Main.rand.NextFloat(0.55f, 0.9f))
                    ?.Configure(40, NPC, Main.rand.NextFloat(0.9f, 1.3f));
            }
            int n = bites >= SatiatedBites ? 10 : 5;
            for (int i = 0; i < n; i++) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center, VaultUtils.RandVr(1.5f, 4f),
                    EmberOrange, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 18));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_LampeaterEmber>(NPC.Center, VaultUtils.RandVr(1f, 3f),
                    EmberOrange, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(22, 36), 0.8f);
            }
            if (bites >= SatiatedBites) {
                //烧成一盏游动的灯
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 3 }, NPC.Center);
            }
        }

        //==================== 发光与屏幕层（叙事与表现层，AI 决策不读光照）====================

        /// <summary>烬芯亮度分级：0.25/0.5/0.75/1.0，telegraph 期再增亮</summary>
        private float EmberLevel() {
            float level = 0.25f + 0.25f * StackCount;
            if ((int)State == StatePounce && StateTimer <= TelegraphFrames) {
                level = MathHelper.Lerp(level, 1.3f, StateTimer / TelegraphFrames);
            }
            return level;
        }

        private void DoGlowLight() {
            //下限保险丝：暗漂态 0.06 强度呼吸微光，全黑也永远有一粒可循的火星
            float breath = 0.06f + 0.02f * MathF.Sin(AmbientClock * 0.07f + Seed);
            float level = Satiated ? 0.95f : Math.Max(breath, 0.14f * StackCount);
            level += swell * 0.4f;
            Lighting.AddLight(NPC.Center, 1.0f * level, 0.74f * level, 0.43f * level);
        }

        /// <summary>吞光域上报（纯客户端；半径与深度随进食数长大——吃得越多，暗得越大）</summary>
        private void ReportVeil() {
            if (Main.dedServ) {
                return;
            }
            int bites = (int)StackCount;
            float radius = 130f + 36f * bites + (Satiated ? 30f : 0f);
            float strength = Math.Min(0.30f + 0.16f * bites, 0.80f) * NPC.Opacity;
            float ember = Math.Min(EmberLevel() + swell * 0.3f, 1.05f);
            LampeaterScreenEffects.ReportWisp(NPC.whoAmI, NPC.Center, radius,
                strength, InhaleProgress, ember);
        }

        /// <summary>常驻氛围粒子（暗漂落烬：烛烟总在掉渣）</summary>
        private void UpdateAmbientFx() {
            if (Main.dedServ || (int)State != StateDrift) {
                return;
            }
            if ((int)AmbientClock % 26 == 0) {
                PRTLoader.NewParticle<PRT_LampeaterEmber>(
                    NPC.Center + Main.rand.NextVector2Circular(9f, 12f),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(0.1f, 0.4f)),
                    EmberOrange * (Satiated ? 0.9f : 0.55f), Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(24, 40), 0.25f);
            }
        }

        //==================== 死亡：吞的光全数吐还 ====================

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            if (NPC.life > 0) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Smoke, hit.HitDirection, -0.5f, 120, default, 0.9f);
                return;
            }
            //死亡四相：光脉冲（发）→ 释放金环扩散+灯魂珠逸散（飞）→ 星屑迸散（中）→ 墨烟余渣（余）
            Lighting.AddLight(NPC.Center, 2.2f, 1.6f, 0.9f);
            LampeaterScreenEffects.TriggerBurst(NPC.Center, 220f + 40f * StackCount);
            int motes = 4 + 3 * (int)StackCount;
            for (int i = 0; i < motes; i++) {
                //吞下的灯魂被吐还：减速上漂散进黑暗
                PRTLoader.NewParticle<PRT_LampeaterLightMote>(NPC.Center + Main.rand.NextVector2Circular(6f, 6f),
                    VaultUtils.RandVr(1.5f, 4f) - new Vector2(0f, 1.2f),
                    LampGold, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(46, 80));
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center, VaultUtils.RandVr(2f, 8f),
                    Color.Lerp(EmberOrange, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(16, 30));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_LampeaterEmber>(NPC.Center, VaultUtils.RandVr(1f, 5f),
                    EmberOrange, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(24, 44), 0.5f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center, VaultUtils.RandVr(0.5f, 2f),
                    InkBody * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(20, 32));
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(new CommonDrop(ItemID.FallenStar, 3, 2, 2, 2));
            npcLoot.Add(new CommonDrop(ItemID.ShinePotion, 3));
            npcLoot.Add(new CommonDrop(ItemID.SpelunkerPotion, 10));
        }

        public override void OnKill() {
            //饱食态被杀：星屑保底 ×3（奖励敢追杀满编的；服务器钩子，NewItem 自同步）
            if (StackCount >= SatiatedBites) {
                Item.NewItem(NPC.GetSource_Loot(), NPC.getRect(), ItemID.FallenStar, 3);
            }
        }

        //==================== 帧与绘制：烟壳 quad（下）→ 精灵图剪影 + 拖影 → 烬芯 quad（上）====================

        public override void FindFrame(int frameHeight) {
            NPC.frameCounter += 0.16 + NPC.velocity.Length() * 0.012;
            int idx = (int)NPC.frameCounter % Main.npcFrameCount[Type];
            NPC.frame.Y = frameHeight * idx;
        }

        private void PushTrail() {
            for (int i = trail.Length - 1; i > 0; i--) {
                trail[i] = trail[i - 1];
            }
            trail[0] = NPC.Center;
            if (trailWritten < trail.Length) {
                trailWritten++;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态泄漏自愈（netcode 7.2）
            BeginDefault(spriteBatch);

            Texture2D tex = TextureAssets.Npc[Type]?.Value;
            if (tex == null) {
                return false;
            }
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() * 0.5f;

            //速度拉伸（法 5）：快时躯干转向运动方向并沿其拉长
            float speed = NPC.velocity.Length();
            float align = MathHelper.Clamp((speed - 2f) / 6f, 0f, 1f);
            float stretch = 1f + MathHelper.Clamp(speed / 16f, 0f, 0.9f) * align;
            float spriteRot = align > 0.01f ? NPC.velocity.ToRotation() + MathHelper.PiOver2 : 0f;
            //烟壳朝向平滑：头端随速度转向，静止回正头上尾下（本地表现角）
            float targetRot = align > 0.01f ? spriteRot : 0f;
            visRot = visRot.AngleLerp(targetRot, 0.18f);
            Vector2 spriteScale = new(1f - align * 0.25f, 1f + speed / 16f * align);

            Color body = drawColor.MultiplyRGB(InkBody) * NPC.Opacity;
            //telegraph 战栗抖动（本地表现）
            Vector2 jitter = InhaleProgress > 0f
                ? new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f))
                : Vector2.Zero;
            Vector2 drawPos = NPC.Center + jitter - screenPos;

            Effect fx = EffectLoader.LampeaterWisp?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderReady = fx != null && white != null && noise != null;

            //烟壳（唯一暗层，画在一切之下：预乘实 alpha 真遮挡）
            if (shaderReady) {
                FillBodyParams(fx, stretch);
                DrawBodyQuad(spriteBatch, fx, white, noise, "TechSmokeBody", drawPos, visRot,
                    new Vector2(QuadW * (1f - align * 0.20f), QuadH * stretch));
            }

            //拖影渐暗（各端本地缓冲）
            for (int i = trailWritten - 1; i >= 1; i--) {
                float fade = (1f - i / (float)trail.Length) * 0.30f;
                spriteBatch.Draw(tex, trail[i] - screenPos, frame, body * fade, spriteRot,
                    origin, spriteScale * (1f - i * 0.06f), SpriteEffects.None, 0f);
            }
            //精灵图收为烟内剪影（形体识别锚，材质让给烟壳）
            spriteBatch.Draw(tex, drawPos, frame, body * (shaderReady ? 0.62f : 1f), spriteRot,
                origin, spriteScale, SpriteEffects.None, 0f);

            //烬芯亮层（画在精灵图上：粒状余烬+火舌+灯魂珠+收拢环）
            if (shaderReady) {
                DrawBodyQuad(spriteBatch, fx, white, noise, "TechEmberFlame", drawPos, visRot,
                    new Vector2(QuadW * (1f - align * 0.20f), QuadH * stretch));
            }
            else {
                DrawEmberFallback(spriteBatch, drawPos);
            }
            return false;
        }

        /// <summary>本体着色器参数（两 technique 共享一份）</summary>
        private void FillBodyParams(Effect fx, float stretch) {
            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uFeed"]?.SetValue(StackCount / (float)SatiatedBites);
            fx.Parameters["uEmber"]?.SetValue(Math.Min(EmberLevel(), 1.05f));
            fx.Parameters["uInhale"]?.SetValue(InhaleProgress);
            fx.Parameters["uStretch"]?.SetValue(stretch);
            //烟受光：环境光亮度喂给烟壳（烬自发光不受此项）
            Vector3 env = Lighting.GetSubLight(NPC.Center);
            fx.Parameters["uEnvLight"]?.SetValue(MathHelper.Clamp(env.X * 0.30f + env.Y * 0.59f + env.Z * 0.11f, 0f, 1f));
            fx.Parameters["uFade"]?.SetValue(NPC.Opacity);
            fx.Parameters["uSwell"]?.SetValue(swell);
        }

        /// <summary>白像素 quad 过指定 technique（Immediate 批，画完还原默认批）</summary>
        private static void DrawBodyQuad(SpriteBatch sb, Effect fx, Texture2D white, Texture2D noise,
            string tech, Vector2 drawPos, float rotation, Vector2 quadSize) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique = fx.Techniques[tech];
            fx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, drawPos, null, Color.White, rotation, white.Size() * 0.5f,
                quadSize / white.Size(), SpriteEffects.None, 0f);
            BeginDefault(sb);
        }

        /// <summary>着色器缺失的克制回退：双层软辉烬芯（旧路径，不模拟烟壳）</summary>
        private void DrawEmberFallback(SpriteBatch sb, Vector2 drawPos) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float level = EmberLevel();
            float breath = 0.06f + 0.02f * MathF.Sin(AmbientClock * 0.07f + Seed);
            level = Math.Max(level * (Satiated ? 1f : 0.85f), breath * 3f);

            BeginAdditive(sb);
            Vector2 gOrigin = glow.Size() * 0.5f;
            float halo = Satiated ? 30f : 16f;
            sb.Draw(glow, drawPos, null, EmberOrange * (0.40f * level), 0f,
                gOrigin, new Vector2(halo * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, drawPos, null, Color.Lerp(EmberOrange, Color.White, 0.35f) * (0.5f * level), 0f,
                gOrigin, new Vector2(7f * 2f / glow.Width), SpriteEffects.None, 0f);
            BeginDefault(sb);
        }
    }
}
