using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 蒙尘的吊臂：验收堂天轨右端挂着的验收机件遗骸。静止免伤，靠积尘齿轮的
    /// 偶发咔嗒与将熄未熄的炉芯告诉玩家"这东西还通着电"。
    /// 玩家在点检台 3×3 区连续站立 30t（意图门槛）→ 80f 仪式（齿轮闸门落锁 →
    /// 轨灯 8 盏逐亮爬向大厅深处 → 两次点火失败的咳嗽式火花 → 第三次通电成功）
    /// → 服务器同位换体为 FoundryOverseer（ai[2]=1 吊臂入场变体，衔接教学空锤）。
    /// 联机契约同 UndrownedThrone：触发裁决/封门事务/换体只在服务器，
    /// phase/timer 乘 ai[0..1] 过线，房间坐标经 SendExtraAI 过线，
    /// 换体 = NewNPC(带 ai) + 字段写入 + SyncNPC + 原地静默移除
    /// </summary>
    internal class OverseerDormantRig : OverseerModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 参数 ====================

        internal const int DaisStandTicks = 30;
        internal const int RiteFrames = 80;
        private const int DoorSealAt = 20;
        private const int LampStartAt = 16;
        private const int LampStepFrames = 8;
        private static readonly int[] IgnitionFailBeats = [40, 56];
        private const int IgnitionOkAt = 72;

        /// <summary>ai[0]：0=蛰伏 1=仪式</summary>
        private ref float Phase => ref NPC.ai[0];
        private ref float RiteTimer => ref NPC.ai[1];
        private ref float AmbientClock => ref NPC.localAI[0];

        internal int roomOriginX = -1;
        internal int roomOriginY = -1;
        internal bool HasRoom => roomOriginX >= 0;
        internal Point RoomOrigin => new(roomOriginX, roomOriginY);

        private int standTicks;
        private int lastLampLit = -1;
        private int lastFailBeat = -1;
        private bool doorCuePlayed;
        private bool ignitionOkPlayed;

        private float Seed => NPC.whoAmI * 0.7391f;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(roomOriginX);
            writer.Write(roomOriginY);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            roomOriginX = reader.ReadInt32();
            roomOriginY = reader.ReadInt32();
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 44;
            NPC.height = 44;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 300;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.dontTakeDamage = true;
            NPC.value = 0;
            NPC.npcSlots = 1f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        public override void AI() {
            NPC.velocity = Vector2.Zero;
            AmbientClock++;

            if ((int)Phase == 0) {
                UpdateDormant();
            }
            else {
                UpdateRite();
            }

            float glow = CoreGlowLevel();
            if (glow > 0.03f) {
                Lighting.AddLight(NPC.Center, 0.24f * glow, 0.14f * glow, 0.05f * glow);
            }
        }

        //==================== 蛰伏 ====================

        private void UpdateDormant() {
            //低成本 telegraph：积尘齿轮偶发咔嗒 + 落尘（各端本地，无需同步）
            if (!Main.dedServ) {
                if ((int)AmbientClock % 340 == 339) {
                    SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.14f, Pitch = -0.8f, MaxInstances = 2 }, NPC.Center);
                }
                if (Main.rand.NextBool(80)) {
                    //落尘从毂顶边缘剥落（尘从上边缘来，不是从体心冒）
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        NPC.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), -Main.rand.NextFloat(14f, 26f)),
                        new Vector2(0f, Main.rand.NextFloat(0.4f, 1f)),
                        FoundryOverseer.IronDeep * 0.6f, Main.rand.NextFloat(0.25f, 0.4f))
                        ?.Configure(Main.rand.Next(18, 30), 0.1f);
                }
                //本机玩家站上点检台的即时反馈：接触器嗡鸣
                if (HasRoom && Main.LocalPlayer.Alives()
                    && ProofingHallRoom.DaisZoneWorld(RoomOrigin).Contains(Main.LocalPlayer.Center.ToPoint())
                    && (int)AmbientClock % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.3f, Pitch = -0.2f, MaxInstances = 2 },
                        DaisWorldPos());
                }
            }

            //触发裁决只在服务器：点检台 3×3 区连续站立 30t
            if (VaultUtils.isClient) {
                return;
            }
            bool standing = false;
            if (HasRoom) {
                Rectangle zone = ProofingHallRoom.DaisZoneWorld(RoomOrigin);
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && zone.Contains(player.Center.ToPoint())) {
                        standing = true;
                        break;
                    }
                }
            }
            else {
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && Vector2.Distance(player.Center, NPC.Center) < 120f) {
                        standing = true;
                        break;
                    }
                }
            }
            if (standing) {
                if (++standTicks >= DaisStandTicks) {
                    Phase = 1;
                    RiteTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                standTicks = 0;
            }
        }

        //==================== 仪式（80f：闸门→轨灯逐亮→两次点火失败→通电→换体）====================

        private void UpdateRite() {
            RiteTimer++;
            int t = (int)RiteTimer;

            if (t == DoorSealAt) {
                //齿轮闸门落锁：只在室内仍有存活玩家时（防锁空房）；音效各端照播
                if (!VaultUtils.isClient && HasRoom
                    && ProofingHallWatcher.AnyAlivePlayerInRoom(RoomOrigin)) {
                    ProofingHallWatcher.SealDoors(RoomOrigin, true);
                    ProofingHallWatcher.AnnounceSealed();
                }
                if (!doorCuePlayed) {
                    doorCuePlayed = true;
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.7f, MaxInstances = 2 }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
                    ShakeNearby(2f);
                }
            }

            //轨灯逐亮：8 盏以 8f 间隔爬向大厅深处（充能可读）
            if (t >= LampStartAt) {
                int lit = Math.Min((t - LampStartAt) / LampStepFrames, 7);
                if (lit > lastLampLit) {
                    lastLampLit = lit;
                    SoundEngine.PlaySound(SoundID.Mech with {
                        Volume = 0.3f,
                        Pitch = -0.4f + lit * 0.08f,
                        MaxInstances = 3
                    }, LampWorldPos(lit));
                }
            }

            //两次点火失败：咳嗽式火花（机件性格）
            for (int i = 0; i < IgnitionFailBeats.Length; i++) {
                if (t == IgnitionFailBeats[i] && lastFailBeat < i) {
                    lastFailBeat = i;
                    SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 2 }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.35f, Pitch = 0.2f, MaxInstances = 2 }, NPC.Center);
                    if (!Main.dedServ) {
                        for (int k = 0; k < 5; k++) {
                            PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(16f, 16f),
                                Main.rand.NextVector2Circular(2f, 2f),
                                Color.Lerp(FoundryOverseer.FurnaceOrange, Color.White, Main.rand.NextFloat(0.4f)),
                                Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(8, 14));
                        }
                    }
                }
            }

            if (t == IgnitionOkAt && !ignitionOkPlayed) {
                //第三次通电成功：齿轮由慢到快，积尘一次性抖落 + 齿缝迸热屑
                ignitionOkPlayed = true;
                SoundEngine.PlaySound(SoundID.Mech with { Volume = 0.8f, Pitch = -0.15f, MaxInstances = 1 }, NPC.Center);
                ShakeNearby(1.5f);
                if (!Main.dedServ) {
                    for (int k = 0; k < 10; k++) {
                        PRTLoader.NewParticle<PRT_OverseerIronChip>(
                            NPC.Center + Main.rand.NextVector2Circular(28f, 28f),
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2.5f)),
                            FoundryOverseer.IronDeep, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 28));
                    }
                    for (int k = 0; k < 6; k++) {
                        PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(20f, 20f),
                            Main.rand.NextVector2Circular(1.8f, 1.8f),
                            Color.Lerp(FoundryOverseer.FurnaceOrange, Color.White, Main.rand.NextFloat(0.4f)),
                            Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
                    }
                }
            }

            if (t >= RiteFrames && !VaultUtils.isClient) {
                TransformToOverseer();
            }
        }

        /// <summary>同位换体（服务器裁决）：房间坐标字段先写后 SyncNPC，与 ai 一并原子过线</summary>
        private void TransformToOverseer() {
            NPC.TargetClosest(faceTarget: false);
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                ModContent.NPCType<FoundryOverseer>(),
                ai2: FoundryOverseer.EmergeVariantRig, Target: NPC.target);
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC boss = Main.npc[idx];
                boss.Center = NPC.Center;
                if (boss.ModNPC is FoundryOverseer overseer) {
                    overseer.roomOriginX = roomOriginX;
                    overseer.roomOriginY = roomOriginY;
                }
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
                AnnounceAwaken(boss);
            }

            NPC.active = false;
            NPC.life = 0;
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        private static void AnnounceAwaken(NPC boss) {
            if (VaultUtils.isServer) {
                ChatHelper.BroadcastChatMessage(
                    NetworkText.FromKey("Announcement.HasAwoken", boss.GetTypeNetName()),
                    new Color(222, 138, 58));
            }
            else {
                Main.NewText(Language.GetTextValue("Announcement.HasAwoken", boss.TypeName), 222, 138, 58);
            }
        }

        private Vector2 DaisWorldPos() {
            if (!HasRoom) {
                return NPC.Center;
            }
            return ProofingHallRoom.DaisZoneWorld(RoomOrigin).Center.ToVector2();
        }

        private Vector2 LampWorldPos(int lampIndex) {
            if (!HasRoom) {
                return NPC.Center;
            }
            return new Vector2((roomOriginX + 8 + lampIndex * 9) * 16f,
                (roomOriginY + ProofingHallRoom.RailRel) * 16f + 8f);
        }

        private void ShakeNearby(float amount, float range = 1000f) {
            if (Main.dedServ || Main.LocalPlayer == null) {
                return;
            }
            if (Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) > range) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(amount);
        }

        //==================== 表现参数 ====================

        /// <summary>炉芯亮度：蛰伏将熄未熄，仪式期随点火节拍攀升</summary>
        private float CoreGlowLevel() {
            if ((int)Phase == 0) {
                return 0.1f + 0.06f * MathF.Sin(AmbientClock * 0.03f + Seed);
            }
            return MathHelper.Clamp(0.2f + (float)RiteTimer / 60f, 0f, 1f);
        }

        //==================== 绘制：链柱 → 积尘毂体 → 轨灯 → 炉芯 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadItem(ItemID.Cog);
            Texture2D cogTex = TextureAssets.Item[ItemID.Cog]?.Value;
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            if (cogTex == null || chainTex == null) {
                return false;
            }

            int t = (int)RiteTimer;
            Vector2 center = NPC.Center;
            //通电后战栗
            if ((int)Phase == 1 && t >= IgnitionOkAt) {
                center += new Vector2(MathF.Sin(t * 2.6f + Seed) * 1.4f, 0f);
            }

            //吊链：轨到毂（蛰伏松垂；通电后受载微颤）
            float railY = HasRoom ? (roomOriginY + ProofingHallRoom.RailRel) * 16f + 8f : center.Y - 60f;
            float shiver = (int)Phase == 1 && t >= IgnitionOkAt ? 1.2f : 0f;
            OverseerVfx.DrawChain(spriteBatch, new Vector2(center.X, railY),
                center + new Vector2(0f, -16f), drawColor, 1f, 1f, shiver);

            //积尘毂体：转速=仪式进度（蛰伏全静止）；铸铁材质走重锈蛰伏档
            //（uRust 0.85 锈透、uHeat 随炉芯将熄未熄→点火攀升，材质自己讲"废弃已久却还通电"）
            float spin = (int)Phase == 1 && t >= IgnitionOkAt ? t * 0.06f
                : (int)Phase == 1 && t >= LampStartAt ? MathF.Sin(t * 0.2f) * 0.05f : 0f;
            Vector2 cogOrigin = cogTex.Size() * 0.5f;
            float heat = CoreGlowLevel() * 0.55f;
            bool ironOn = OverseerVfx.BeginIronCast(spriteBatch);
            (float scale, float dir)[] cogs = [(2.1f, 1f), (1.45f, -1.6f), (0.85f, 2.4f)];
            foreach ((float scale, float dir) in cogs) {
                OverseerVfx.DrawIronPart(spriteBatch, ironOn, cogTex, center - Main.screenPosition,
                    cogTex.Bounds, drawColor, spin * dir, cogOrigin, scale, SpriteEffects.None,
                    heat, 0.85f, Seed + scale, 1f);
            }
            OverseerVfx.EndIronCast(spriteBatch, ironOn);

            DrawGlow(spriteBatch, center);
            return false;
        }

        /// <summary>加色层：炉芯 + 仪式期轨灯逐亮（强度写进色乘）</summary>
        private void DrawGlow(SpriteBatch sb, Vector2 center) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float level = CoreGlowLevel();
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 gOrigin = glow.Size() * 0.5f;
            sb.Draw(glow, center - Main.screenPosition, null,
                FoundryOverseer.FurnaceOrange * (0.5f * level), 0f, gOrigin,
                new Vector2(12f * 2f / glow.Width), SpriteEffects.None, 0f);
            //仪式期轨灯逐亮
            if ((int)Phase == 1 && HasRoom && lastLampLit >= 0) {
                for (int i = 0; i <= lastLampLit && i < 8; i++) {
                    sb.Draw(glow, LampWorldPos(i) - Main.screenPosition, null,
                        FoundryOverseer.FurnaceOrange * 0.5f, 0f, gOrigin,
                        new Vector2(6f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
