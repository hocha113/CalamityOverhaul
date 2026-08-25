using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites
{
    /// <summary>
    /// 沉波狱吏（L4 水牢湿舱段，WAVE2-ENEMIES §3.3）：水面下只有一道涟漪跟着你，
    /// 靠近水缘就被它暴起拽向水里；上岸的它锈重迟缓、甲缝大开。
    /// 状态机：0 潜航（舱段内贴水游弋，alpha 200 半隐，涟漪常显）→ 1 暴起（30f 气泡柱+
    /// 三环收束 telegraph → 抛物线跃出扑抱，净空不足改水线横扑）→ 2 压制（玩家同水体：
    /// 游速 ×1.7、接触 44→52）→ 3 搁浅（登干地 240f 累计：移速减半、防御 16→8、
    /// 寻路回水，120s 无水自毁散架）。
    /// 联机：状态/暴起时机/舱段绑定服务器权威；跃出初速一次性写 velocity 乘 SyncNPC，
    /// 客户端本地积分不回卷；涟漪行各端本地扫水面推导（联机客户端无 Compartments 数据）；
    /// 击退拽向水体在受害端本地改向（ModifyHitPlayer 原版路径）。
    /// 材质=浸水锈甲+污水：涟漪随速拉长 / 出水挂水淌滴 / 搁浅甲缝渗水线。
    /// </summary>
    internal class DrownedTurnkey : EliteModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.HellArmoredBones;

        //==================== 参数（建议值，验收再调）====================

        private const int StateLurk = 0;
        private const int StateBurst = 1;
        private const int StateSuppress = 2;
        private const int StateBeached = 3;

        /// <summary>暴起 telegraph / 跃出帧</summary>
        private const int BurstTelegraph = 30;
        private const int BurstAirCap = 150;
        /// <summary>暴起触发：水平距 / 与水面垂距（px）</summary>
        private const float BurstTriggerX = 180f;
        private const float BurstTriggerY = 64f;
        /// <summary>登干地累计 → 搁浅 / 搁浅自毁（2min）</summary>
        private const int BeachAfterFrames = 240;
        private const int BeachSelfDestruct = 7200;

        private const float SwimSpeed = 4.5f;
        private const float SuppressSpeed = 7.6f;
        private const int DefenseWet = 16;
        private const int DefenseBeached = 8;

        /// <summary>沼绿蓝浸水甲（drawColor 乘色）</summary>
        private static readonly Color SwampMul = new(95, 135, 125);
        /// <summary>白沫涟漪</summary>
        private static readonly Color FoamPale = new(200, 228, 222);

        /// <summary>绑定舱段索引（服务器权威决策用；客户端恒 -1 属预期）</summary>
        private int boundIdx = -1;
        /// <summary>涟漪水面行（各端本地扫水面缓存，tile 行；-1=无）</summary>
        private int rippleRow = -1;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.HellArmoredBones];
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 22;
            NPC.height = 42;
            NPC.damage = 44;
            NPC.defense = DefenseWet;
            NPC.lifeMax = 420;
            NPC.knockBackResist = 0.1f;
            NPC.aiStyle = -1;
            //手动重力：水中浮控，干地下坠
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.npcSlots = 2f;
            NPC.value = 50000f;
            NPC.alpha = 200;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath2;
            AnimationType = NPCID.HellArmoredBones;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.TheDungeon,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.DrownedTurnkey.Bestiary"),
            ]);
        }

        //==================== 投放（§4：仅 L4 湿舱段 0.12，每舱段 ≤1）====================

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            if (!DungeonworldEliteDirector.CommonSpawnGate(spawnInfo, Type)) {
                return 0f;
            }
            if (DungeonworldEliteDirector.BandIndexForRow(spawnInfo.SpawnTileY) != 3 || !spawnInfo.Water) {
                return 0f;
            }
            if (!DungeonworldEliteDirector.InWetCompartment(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY,
                out L4WaterWorks.Compartment compartment)) {
                return 0f;
            }
            //每舱段同时 ≤1
            int myType = Type;
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.type != myType) {
                    continue;
                }
                if (compartment.Area.Contains((int)(other.Center.X / 16f), (int)(other.Center.Y / 16f))) {
                    return 0f;
                }
            }
            return 0.12f;
        }

        /// <summary>出生绑定所在舱段（服务器；spawn 包时序无关：boundIdx 只被服务器决策消费）</summary>
        public override void OnSpawn(IEntitySource source) {
            if (VaultUtils.isClient) {
                return;
            }
            RebindCompartment();
        }

        private void RebindCompartment() {
            L4WaterWorks.Compartment c = DungeonworldEliteDirector.CompartmentContaining(
                (int)(NPC.Center.X / 16f), (int)(NPC.Center.Y / 16f));
            if (c != null) {
                boundIdx = L4WaterWorks.Compartments.IndexOf(c);
            }
        }

        private L4WaterWorks.Compartment Bound
            => boundIdx >= 0 && boundIdx < L4WaterWorks.Compartments.Count
                ? L4WaterWorks.Compartments[boundIdx] : null;

        //==================== AI ====================

        /// <summary>alpha 目标=状态函数：潜航 200（水下只余轮廓），其余 0</summary>
        private int AlphaTarget() => (int)State == StateLurk && NPC.wet ? 200 : 0;

        public override void AI() {
            HealAlpha(AlphaTarget(), 10);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateEdgeCue();
            }
            ServerSyncPacer();
            RefreshRippleRow();

            //防御=状态确定函数（各端一致：判伤发生在攻击端本地，运行时改防御必须走 ai 推导）
            NPC.defense = (int)State == StateBeached ? DefenseBeached : DefenseWet;

            //手动重力（干地/空中）
            if (!NPC.wet) {
                NPC.velocity.Y = Math.Min(NPC.velocity.Y + 0.35f, 10f);
            }

            NPC.TargetClosest(faceTarget: false);
            Player target = NPC.HasValidTarget ? Main.player[NPC.target] : null;

            switch ((int)State) {
                case StateLurk:
                    UpdateLurk(target);
                    break;
                case StateBurst:
                    UpdateBurst(target);
                    break;
                case StateSuppress:
                    UpdateSuppress(target);
                    break;
                default:
                    UpdateBeached();
                    break;
            }

            if (NPC.velocity.X != 0f) {
                NPC.direction = Math.Sign(NPC.velocity.X);
                NPC.spriteDirection = NPC.direction;
            }
            DoAmbientWaterFx();
        }

        private void PlayStateEdgeCue() {
            switch ((int)State) {
                case StateSuppress:
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, NPC.Center);
                    break;
                case StateBeached:
                    //搁浅：锈住的铁罐子落地闷响
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.55f, MaxInstances = 2 }, NPC.Center);
                    break;
            }
        }

        //==================== 潜航 ====================

        private void UpdateLurk(Player target) {
            StateTimer++;

            if (NPC.wet) {
                //湿身：干地累计快速消退
                if (!VaultUtils.isClient && StackCount > 0f) {
                    StackCount = Math.Max(0f, StackCount - 5f);
                }
                SwimSteer(target, SwimSpeed);
                ClampInsideBound();
            }
            else {
                //落在干地：走向水（全速），累计搁浅计时
                SeekWaterGait(2.2f);
                if (!VaultUtils.isClient) {
                    if (NPC.velocity.Y == 0f) {
                        StackCount++;
                    }
                    if (StackCount >= BeachAfterFrames) {
                        ChangeState(StateBeached);
                        return;
                    }
                }
            }

            //暴起/压制裁决（服务器；20f 驻留防水缘往复抖动刷包）
            if (VaultUtils.isClient || target == null || target.dead || !NPC.wet || StateTimer < 20f) {
                return;
            }
            if (PlayerInBoundWater(target)) {
                ChangeState(StateSuppress);
                return;
            }
            if (rippleRow > 0) {
                float surfaceY = rippleRow * 16f;
                if (Math.Abs(target.Center.X - NPC.Center.X) < BurstTriggerX
                    && Math.Abs(target.Center.Y - surfaceY) < BurstTriggerY
                    && StateTimer > 90f) {
                    //ai[2]=出水点列（客户端画三环收束的锚）
                    float exitX = MathHelper.Clamp(target.Center.X / 16f,
                        BoundLeftTile() + 2, BoundRightTile() - 2);
                    ChangeState(StateBurst, (int)exitX);
                }
            }
        }

        /// <summary>舱段内贴水游弋：跟随玩家水平位、保持其下方（限速 4.5）</summary>
        private void SwimSteer(Player target, float maxSpeed) {
            Vector2 desired;
            if (target != null && !target.dead) {
                float surfaceY = rippleRow > 0 ? rippleRow * 16f : NPC.Center.Y - 48f;
                float wantY = Math.Max(surfaceY + 56f, target.Center.Y + 48f);
                desired = new Vector2(target.Center.X, wantY) - NPC.Center;
            }
            else {
                desired = new Vector2(MathF.Sin(AmbientClock * 0.02f + Seed) * 40f, 0f);
            }
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.08f);
        }

        /// <summary>活动范围钳在舱段矩形内（服务器权威；客户端无表跳过，靠周期锚校正）</summary>
        private void ClampInsideBound() {
            if (VaultUtils.isClient) {
                return;
            }
            L4WaterWorks.Compartment c = Bound;
            if (c == null) {
                RebindCompartment();
                return;
            }
            float left = c.Area.Left * 16f + 8f;
            float right = c.Area.Right * 16f - 8f - NPC.width;
            float bottom = c.Area.Bottom * 16f - 8f - NPC.height;
            NPC.position.X = MathHelper.Clamp(NPC.position.X, left, right);
            NPC.position.Y = Math.Min(NPC.position.Y, bottom);
        }

        private int BoundLeftTile() => Bound?.Area.Left ?? (int)(NPC.Center.X / 16f) - 10;
        private int BoundRightTile() => Bound?.Area.Right ?? (int)(NPC.Center.X / 16f) + 10;

        /// <summary>玩家是否与它同水体（服务器：湿 + 在绑定舱段矩形内）</summary>
        private bool PlayerInBoundWater(Player player) {
            if (!player.wet) {
                return false;
            }
            L4WaterWorks.Compartment c = Bound;
            return c != null && c.Area.Contains((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
        }

        //==================== 暴起 ====================

        private void UpdateBurst(Player target) {
            StateTimer++;
            int t = (int)StateTimer;
            float exitXpx = StateParam * 16f + 8f;

            if (t <= BurstTelegraph) {
                //蓄势：潜到出水点正下方；气泡柱变密（各端本地，锚在 ai[2] 出水列）
                if (NPC.wet) {
                    Vector2 aim = new(exitXpx, (rippleRow > 0 ? rippleRow * 16f : NPC.Center.Y) + 40f);
                    Vector2 desired = (aim - NPC.Center);
                    if (desired.Length() > SwimSpeed * 1.4f) {
                        desired = desired.SafeNormalize(Vector2.Zero) * SwimSpeed * 1.4f;
                    }
                    NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.12f);
                }
                if (!Main.dedServ && rippleRow > 0 && (int)AmbientClock % 2 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        new Vector2(exitXpx + Main.rand.NextFloat(-6f, 6f), rippleRow * 16f + Main.rand.NextFloat(0f, 20f)),
                        new Vector2(0f, -Main.rand.NextFloat(1.5f, 3f)),
                        FoamPale * 0.5f, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(10, 18));
                }
                //跃出发令（服务器）：净空 ≥5 格抛物线跃出，不足改水线横扑
                if (!VaultUtils.isClient && t == BurstTelegraph && target != null) {
                    int exitTileX = (int)StateParam;
                    int surface = rippleRow > 0 ? rippleRow : (int)(NPC.Center.Y / 16f) - 2;
                    bool headroom = !Collision.SolidTiles(exitTileX - 1, exitTileX + 1, surface - 5, surface - 1);
                    if (headroom) {
                        float vx = MathHelper.Clamp((target.Center.X - NPC.Center.X) / 22f, -8f, 8f);
                        NPC.velocity = new Vector2(vx, -9.5f);
                    }
                    else {
                        NPC.velocity = new Vector2(Math.Sign(target.Center.X - NPC.Center.X) * 8.5f, -2f);
                    }
                    NPC.netUpdate = true;
                }
                return;
            }

            if (t >= BurstTelegraph + 1 && BeatForward(1) && !Main.dedServ) {
                //水炸开
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 1f, Pitch = -0.2f, MaxInstances = 3 }, NPC.Center);
                for (int i = 0; i < 7; i++) {
                    PRTLoader.NewParticle<PRT_SewageGlob>(NPC.Top, VaultUtils.RandVr(2f, 6f) - Vector2.UnitY * 3f,
                        SwampMul, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(28, 44));
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Top, VaultUtils.RandVr(0.5f, 2f),
                        FoamPale * 0.6f, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                }
            }

            //空中段：重力自然抛物线；落定裁决（服务器）
            if (VaultUtils.isClient) {
                return;
            }
            if (t > BurstTelegraph + 6 && NPC.wet) {
                ChangeState(target != null && PlayerInBoundWater(target) ? StateSuppress : StateLurk);
                return;
            }
            if (t > BurstTelegraph + 10 && !NPC.wet && NPC.velocity.Y == 0f) {
                //落干地：回潜航壳（由它的干地分支走搁浅累计）
                ChangeState(StateLurk);
                return;
            }
            if (t > BurstAirCap) {
                ChangeState(StateLurk);
            }
        }

        //==================== 压制（同水体强化）====================

        private void UpdateSuppress(Player target) {
            StateTimer++;
            if (target != null && !target.dead) {
                Vector2 desired = target.Center - NPC.Center;
                if (desired.Length() > SuppressSpeed) {
                    desired = desired.SafeNormalize(Vector2.Zero) * SuppressSpeed;
                }
                NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.11f);
            }
            ClampInsideBound();
            if (VaultUtils.isClient || StateTimer < 20f) {
                return;
            }
            if (target == null || target.dead || !PlayerInBoundWater(target)) {
                ChangeState(StateLurk);
            }
        }

        //==================== 搁浅 ====================

        private void UpdateBeached() {
            StateTimer++;
            SeekWaterGait(1.1f);

            if (VaultUtils.isClient) {
                return;
            }
            if (NPC.wet) {
                //爬回水里：复位
                StackCount = 0f;
                ChangeState(StateLurk);
                return;
            }
            if (StateTimer >= BeachSelfDestruct) {
                //无水可回：散架（服务器裁决死亡，checkDead 内部走 HitEffect+同步）
                NPC.life = 0;
                NPC.checkDead();
            }
        }

        /// <summary>最笨寻水步态：朝绑定舱段中心横移+跳（不做 A*；客户端无表时维持现向）</summary>
        private void SeekWaterGait(float maxSpeed) {
            L4WaterWorks.Compartment c = Bound;
            if (c != null) {
                float centerX = c.Area.Center.X * 16f;
                if (Math.Abs(centerX - NPC.Center.X) > 24f) {
                    NPC.direction = Math.Sign(centerX - NPC.Center.X);
                }
            }
            else if (NPC.direction == 0) {
                NPC.direction = 1;
            }
            NPC.velocity.X += 0.08f * NPC.direction;
            NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -maxSpeed, maxSpeed);
            if (NPC.velocity.Y == 0f && NPC.collideX) {
                NPC.velocity.Y = -6.5f;
            }
        }

        //==================== 命中：拽向水体 + 浸寒 ====================

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            if ((int)State == StateSuppress) {
                //同水体强化：接触 44→52
                modifiers.SourceDamage *= 52f / 44f;
            }
            if ((int)State != StateBeached) {
                //击退方向强制拽向它（=拽向它所在/所来的水体；受害端本地解算，原版路径）
                int dir = Math.Sign(NPC.Center.X - target.Center.X);
                if (dir != 0) {
                    modifiers.HitDirectionOverride = dir;
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.Chilled, 180);
        }

        //==================== 表现：涟漪 / 气泡 / 渗水 ====================

        /// <summary>各端本地扫水面：从体位向上找第一行无水格，其下即水面行（联机客户端无舱段表的替代数据源）</summary>
        private void RefreshRippleRow() {
            if ((int)AmbientClock % 10 != 0 && rippleRow > 0) {
                return;
            }
            rippleRow = -1;
            int x = (int)(NPC.Center.X / 16f);
            int y = (int)(NPC.Center.Y / 16f);
            if (!WorldGen.InWorld(x, y, 10) || Main.tile[x, y].LiquidAmount == 0) {
                return;
            }
            for (int k = 1; k < 60; k++) {
                int yy = y - k;
                if (yy < 10) {
                    return;
                }
                if (Main.tile[x, yy].LiquidAmount == 0) {
                    rippleRow = yy + 1;
                    return;
                }
            }
        }

        private void DoAmbientWaterFx() {
            if (Main.dedServ) {
                return;
            }
            //潜航稀疏气泡列
            if ((int)State == StateLurk && NPC.wet && (int)AmbientClock % 9 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Top + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f),
                    new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.2f)),
                    FoamPale * 0.35f, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(Main.rand.Next(14, 24));
            }
            //搁浅甲缝渗水滴（受伤提示的一半；另一半是防御减半）
            if ((int)State == StateBeached && (int)AmbientClock % 8 == 0) {
                PRTLoader.NewParticle<PRT_SewageGlob>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-14f, 6f)),
                    new Vector2(0f, 0.5f), SwampMul, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(20, 32));
            }
            //出水挂水淌滴
            if (!NPC.wet && (int)State == StateBurst && (int)AmbientClock % 3 == 0) {
                PRTLoader.NewParticle<PRT_SewageGlob>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-16f, 16f)),
                    NPC.velocity * 0.2f + new Vector2(0f, 1f), SwampMul, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(22, 34));
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            int n = NPC.life <= 0 ? 14 : 3;
            for (int i = 0; i < n; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone, hit.HitDirection * 1.5f, -1f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_SewageGlob>(NPC.Center, VaultUtils.RandVr(1.5f, 5f),
                        SwampMul, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(26, 40));
                }
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(new CommonDrop(ItemID.GillsPotion, 3));
            npcLoot.Add(new CommonDrop(ItemID.WaterWalkingPotion, 100, 1, 1, 15));
            npcLoot.Add(new CommonDrop(ItemID.BreathingReed, 10));
        }

        //==================== 绘制：本体原版管线（GetAlpha 沼绿），涟漪/三环/湿光在 PostDraw ====================

        public override Color? GetAlpha(Color drawColor) {
            Color mul = (int)State == StateBeached ? new Color(80, 112, 102) : SwampMul;
            return drawColor.MultiplyRGB(mul) * NPC.Opacity;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态泄漏自愈（netcode 7.2）
            BeginDefault(spriteBatch);
            return true;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DrawWetSheen(spriteBatch, screenPos, drawColor);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            BeginAdditive(spriteBatch);
            DrawRipple(spriteBatch, glow);
            if ((int)State == StateBurst && StateTimer <= BurstTelegraph) {
                DrawConvergeRings(spriteBatch, glow);
            }
            BeginDefault(spriteBatch);
        }

        /// <summary>下半身湿光：默认批里 A=0 加色技法（预乘 AlphaBlend 源因子=One，加亮不遮暗）</summary>
        private void DrawWetSheen(SpriteBatch sb, Vector2 screenPos, Color drawColor) {
            if ((int)State == StateBeached && StateTimer > 3600f) {
                return; //晾干后半段湿光退场
            }
            Texture2D tex = TextureAssets.Npc[Type]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = NPC.frame;
            Rectangle bottomHalf = new(frame.X, frame.Y + frame.Height / 2, frame.Width, frame.Height / 2);
            float frameCenterY = NPC.Bottom.Y - frame.Height * 0.5f + 4f + NPC.gfxOffY;
            Vector2 pos = new(NPC.Center.X - screenPos.X, frameCenterY - screenPos.Y);
            SpriteEffects fx = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color sheen = new Color(170, 205, 210, 0) * (0.26f * NPC.Opacity);
            sb.Draw(tex, pos, bottomHalf, sheen, 0f, new Vector2(frame.Width * 0.5f, 0f), 1f, fx, 0f);
        }

        /// <summary>涟漪：水面行位置的横拉白沫层，速度越快越拉长（各向异性）</summary>
        private void DrawRipple(SpriteBatch sb, Texture2D glow) {
            if (rippleRow <= 0 || !NPC.wet || (int)State == StateBeached) {
                return;
            }
            Vector2 pos = new(NPC.Center.X, rippleRow * 16f + 4f);
            float stretch = 40f + Math.Abs(NPC.velocity.X) * 10f;
            float wobble = 0.85f + 0.15f * MathF.Sin(AmbientClock * 0.2f + Seed);
            Vector2 gOrigin = glow.Size() * 0.5f;
            sb.Draw(glow, pos - Main.screenPosition, null, FoamPale * (0.30f * wobble), 0f,
                gOrigin, new Vector2(stretch * 2f / glow.Width, 5f * 2f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, pos - Main.screenPosition, null, FoamPale * (0.18f * wobble), 0f,
                gOrigin, new Vector2(stretch * 1.4f * 2f / glow.Width, 3f * 2f / glow.Height), SpriteEffects.None, 0f);
        }

        /// <summary>暴起前三环收束（落点预告，锚在 ai[2] 出水列；timer 确定函数各端同拍）</summary>
        private void DrawConvergeRings(SpriteBatch sb, Texture2D glow) {
            if (rippleRow <= 0) {
                return;
            }
            Vector2 anchor = new(StateParam * 16f + 8f, rippleRow * 16f + 4f);
            Vector2 gOrigin = glow.Size() * 0.5f;
            int t = (int)StateTimer;
            for (int i = 0; i < 3; i++) {
                float p = MathHelper.Clamp((t - i * 5) / 22f, 0f, 1f);
                if (p <= 0f) {
                    continue;
                }
                float radius = MathHelper.Lerp(58f, 8f, p);
                float alpha = 0.16f + 0.30f * p;
                sb.Draw(glow, anchor - Main.screenPosition, null, FoamPale * alpha, 0f,
                    gOrigin, new Vector2(radius * 2f / glow.Width, radius * 0.5f * 2f / glow.Height),
                    SpriteEffects.None, 0f);
            }
        }
    }
}
