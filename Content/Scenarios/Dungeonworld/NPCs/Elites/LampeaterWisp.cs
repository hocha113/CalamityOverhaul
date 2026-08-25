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
    /// 联机：掷点/扑灯时机/进食计数全服务器；减益经原版 buff 同步；出生 alpha 目标=状态函数，
    /// 任何端进场即收敛。材质=烛烟+余烬：速度拉伸烟体 / 进食分级点亮烬芯 / 死亡星屑迸散+光脉冲。
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

        /// <summary>烛橙烬芯</summary>
        internal static readonly Color EmberOrange = new(255, 190, 110);
        /// <summary>墨褐近黑烟体（L3 纸墨褐强调色系）</summary>
        private static readonly Color InkBody = new(70, 58, 48);

        /// <summary>拖影环形缓冲（各端本地表现）</summary>
        private readonly Vector2[] trail = new Vector2[6];
        private int trailWritten;
        /// <summary>已观察进食数（各端本地，前进沿触发进食演出）</summary>
        private int seenBites;

        private bool Satiated => StackCount >= SatiatedBites;

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

            NPC.TargetClosest(faceTarget: false);
            Player target = NPC.HasValidTarget ? Main.player[NPC.target] : null;

            if ((int)State == StateDrift) {
                UpdateDrift(target);
            }
            else {
                UpdatePounce(target);
            }

            DoGlowLight();
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
                //绷紧：刹住漂移，烟雾向心倒吸（GhostRainMist 反向，各端本地）
                NPC.velocity *= 0.86f;
                if (!Main.dedServ && (int)AmbientClock % 2 == 0) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(28f, 60f);
                    PRTLoader.NewParticle<PRT_GhostRainMist>(from, (NPC.Center - from) * 0.1f,
                        InkBody * 0.65f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
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
                //进食裁决只在服务器：冲刺窗口内碰撞盒相交=咬中（每次扑灯至多记一口）
                if (!VaultUtils.isClient && StateParam < 1f && target != null
                    && !target.dead && NPC.Hitbox.Intersects(target.Hitbox)) {
                    StateParam = 1f;
                    StackCount = Math.Min(SatiatedBites, StackCount + 1);
                    NPC.netUpdate = true;
                }
                return;
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

        //==================== 咬中减益（受害端本地，原版 buff 自带同步，netcode 2.2 规避通道）====================

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
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = -0.4f + bites * 0.25f, MaxInstances = 3 }, NPC.Center);
            int n = bites >= SatiatedBites ? 10 : 5;
            for (int i = 0; i < n; i++) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center, VaultUtils.RandVr(1.5f, 4f),
                    EmberOrange, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 18));
            }
            if (bites >= SatiatedBites) {
                //烧成一盏游动的灯
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 3 }, NPC.Center);
            }
        }

        //==================== 发光（叙事与表现层，AI 决策不读光照）====================

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
            Lighting.AddLight(NPC.Center, 1.0f * level, 0.74f * level, 0.43f * level);
        }

        //==================== 死亡：吞的光炸成星屑落回脚边 ====================

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            if (NPC.life > 0) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Smoke, hit.HitDirection, -0.5f, 120, default, 0.9f);
                return;
            }
            //星屑迸散 + 一拍光脉冲（AddLight 单帧强闪，非屏幕滤镜，不越 E 路界）
            Lighting.AddLight(NPC.Center, 2.2f, 1.6f, 0.9f);
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center, VaultUtils.RandVr(2f, 8f),
                    Color.Lerp(EmberOrange, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(16, 30));
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

        //==================== 帧与绘制：速度拉伸烟体 + 拖影 + 烬芯 ====================

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
            float rotation = align > 0.01f
                ? NPC.velocity.ToRotation() + MathHelper.PiOver2
                : 0f;
            Vector2 scale = new(1f - align * 0.25f, 1f + speed / 16f * align);

            Color body = drawColor.MultiplyRGB(InkBody) * NPC.Opacity;
            //telegraph 战栗抖动（本地表现）
            Vector2 jitter = (int)State == StatePounce && StateTimer <= TelegraphFrames
                ? new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f))
                : Vector2.Zero;
            Vector2 drawPos = NPC.Center + jitter - screenPos;

            //拖影渐暗（各端本地缓冲）
            for (int i = trailWritten - 1; i >= 1; i--) {
                float fade = (1f - i / (float)trail.Length) * 0.30f;
                spriteBatch.Draw(tex, trail[i] - screenPos, frame, body * fade, rotation,
                    origin, scale * (1f - i * 0.06f), SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(tex, drawPos, frame, body, rotation, origin, scale, SpriteEffects.None, 0f);

            DrawEmber(spriteBatch, drawPos);
            return false;
        }

        /// <summary>烬芯双层（加色批：强度乘进色值，A 同步收缩）</summary>
        private void DrawEmber(SpriteBatch sb, Vector2 drawPos) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float level = EmberLevel();
            //暗漂微光下限：即便 0 口进食也留 0.06 呼吸残影
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
