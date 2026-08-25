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
    /// 沉锚的死囚：泄洪堂王座壁龛里的蛰伏体。静止免伤，靠胸廓呼吸起伏、锚链偶响与
    /// 眼缝微光告诉玩家"这东西还活着"。房间不攻击玩家，玩家必须亲手转阀：
    /// 任一玩家在阀台 3×3 区连续站立 30t（意图门槛，扫过不触发）→ 96f 仪式
    /// （阀轮三响 → 双门封砖落锁 → 立管轰鸣喷雾 → 王座链爆断）→ 服务器同位换体
    /// 为 Undrowned 并走王座入场变体（ai[2]=1）。
    /// 联机契约：触发裁决/封门事务/换体只在服务器，phase/timer 乘 ai[0..1] 过线，
    /// 房间坐标经 SendExtraAI 过线，各端本地跑同一演出时间线；
    /// 换体 = NewNPC(带 ai) + 字段写入 + SyncNPC + 原地静默移除（原子过线，无缺参帧）
    /// </summary>
    internal class UndrownedThrone : UndrownedModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 参数 ====================

        /// <summary>阀台连续站立门槛（tick）</summary>
        internal const int ValveStandTicks = 30;
        /// <summary>仪式总帧与节拍</summary>
        internal const int RiteFrames = 96;
        private static readonly int[] ValveClickBeats = [2, 18, 34];
        private const int DoorSealAt = 48;
        private const int ChainBurstAt = 88;

        private const float DrawScale = 1.85f;

        /// <summary>ai[0]：0=蛰伏 1=仪式</summary>
        private ref float Phase => ref NPC.ai[0];
        /// <summary>ai[1]：仪式计时</summary>
        private ref float RiteTimer => ref NPC.ai[1];
        /// <summary>本地环境音/呼吸计时（不入同步）</summary>
        private ref float AmbientClock => ref NPC.localAI[0];

        /// <summary>房间坐标（看守布置时写入，SendExtraAI 过线；&lt;0=无房测试落场）</summary>
        internal int roomOriginX = -1;
        internal int roomOriginY = -1;
        internal bool HasRoom => roomOriginX >= 0;
        internal Point RoomOrigin => new(roomOriginX, roomOriginY);

        /// <summary>阀台站立累计（服务器裁决用，客户端不消费）</summary>
        private int standTicks;
        //仪式节拍闩（本地表现，换场清零无需：仪式一生一次）
        private int lastValveClick = -1;
        private bool doorCuePlayed;
        private bool chainBurstPlayed;

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
            NPC.width = 40;
            NPC.height = 54;
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
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
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

            float glow = EyeGlowLevel();
            if (glow > 0.03f) {
                Lighting.AddLight(NPC.Center, 0.08f * glow, 0.2f * glow, 0.16f * glow);
            }
        }

        //==================== 蛰伏 ====================

        private void UpdateDormant() {
            //低成本 telegraph：锚链偶响 + 泡胀躯体缘滴（各端本地，无需同步）
            if (!Main.dedServ) {
                if ((int)AmbientClock % 320 == 319) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.15f, Pitch = -0.8f, MaxInstances = 2 }, NPC.Center);
                }
                if (Main.rand.NextBool(70)) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        NPC.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-16f, 18f)),
                        new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)),
                        Undrowned.BogWater * 0.6f, Main.rand.NextFloat(0.25f, 0.4f))
                        ?.Configure(Main.rand.Next(20, 34), 0.1f);
                }
                //本机玩家站上阀台的即时反馈：阀杆吱呀（意图被房间听见了）
                if (HasRoom && Main.LocalPlayer.Alives()
                    && FloodGalleryRoom.ValveZoneWorld(RoomOrigin).Contains(Main.LocalPlayer.Center.ToPoint())
                    && (int)AmbientClock % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.3f, Pitch = -0.3f, MaxInstances = 2 },
                        ValveWorldPos());
                }
            }

            //触发裁决只在服务器：阀台 3×3 区连续站立 30t，结果乘 ai+netUpdate 过线
            if (VaultUtils.isClient) {
                return;
            }
            bool standing = false;
            if (HasRoom) {
                Rectangle zone = FloodGalleryRoom.ValveZoneWorld(RoomOrigin);
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && zone.Contains(player.Center.ToPoint())) {
                        standing = true;
                        break;
                    }
                }
            }
            else {
                //无房测试落场：贴近即触发（降级路径）
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && Vector2.Distance(player.Center, NPC.Center) < 120f) {
                        standing = true;
                        break;
                    }
                }
            }
            if (standing) {
                if (++standTicks >= ValveStandTicks) {
                    Phase = 1;
                    RiteTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else {
                standTicks = 0;
            }
        }

        //==================== 仪式（96f：阀轮→封门→立管→链爆断→换体）====================

        private void UpdateRite() {
            RiteTimer++;
            int t = (int)RiteTimer;

            //阀轮咔哒三响（0/16/32f 起拍，逐响爬调）
            for (int i = 0; i < ValveClickBeats.Length; i++) {
                if (t == ValveClickBeats[i] && lastValveClick < i) {
                    lastValveClick = i;
                    SoundEngine.PlaySound(SoundID.Mech with {
                        Volume = 0.6f,
                        Pitch = -0.5f + i * 0.22f,
                        MaxInstances = 2
                    }, ValveWorldPos());
                    if (!Main.dedServ) {
                        PRTLoader.NewParticle<PRT_Spark>(ValveWorldPos() + Main.rand.NextVector2Circular(8f, 8f),
                            new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.6f, 1.8f)),
                            Color.Lerp(Undrowned.RustOrange, Color.White, 0.4f),
                            Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(8, 14));
                    }
                }
            }

            if (t == DoorSealAt) {
                //封门拍：服务器只在室内仍有存活玩家时落锁（防锁空房）；
                //音效各端照播（若被跳过，读作闸门空转卡壳）
                if (!VaultUtils.isClient && HasRoom
                    && FloodGalleryWatcher.AnyAlivePlayerInRoom(RoomOrigin)) {
                    FloodGalleryWatcher.SealDoors(RoomOrigin, true);
                    FloodGalleryWatcher.AnnounceSealed();
                }
                if (!doorCuePlayed) {
                    doorCuePlayed = true;
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.7f, MaxInstances = 2 }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.7f, Pitch = -0.6f, MaxInstances = 2 }, NPC.Center);
                    ShakeNearby(2f);
                }
            }

            //立管轰鸣喷雾（48~96f，涨水系统的预告在前）
            if (!Main.dedServ && HasRoom && t > DoorSealAt && t % 3 == 0) {
                for (int side = 0; side < 2; side++) {
                    int col = side == 0 ? FloodGalleryRoom.PipeLeftCol : FloodGalleryRoom.PipeRightCol;
                    float x = (roomOriginX + col + 1f) * 16f;
                    float y = (roomOriginY + FloodGalleryRoom.PipeBottomRel - Main.rand.Next(0, 14)) * 16f;
                    PRTLoader.NewParticle<PRT_GhostRainMist>(new Vector2(x, y),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.6f, 1.6f)),
                        Undrowned.FoamWhite * 0.5f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(20, 34));
                }
            }

            if (t == ChainBurstAt && !chainBurstPlayed) {
                //王座链爆断
                chainBurstPlayed = true;
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 2 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.55f, Pitch = -0.3f, MaxInstances = 2 }, NPC.Center);
                if (!Main.dedServ) {
                    for (int k = 0; k < 7; k++) {
                        PRTLoader.NewParticle<PRT_Spark>(NPC.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-8f, 16f)),
                            new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 2.4f)),
                            Color.Lerp(Undrowned.RustOrange, Color.White, Main.rand.NextFloat(0.4f)),
                            Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(10, 16));
                    }
                }
            }

            if (t >= RiteFrames && !VaultUtils.isClient) {
                TransformToUndrowned();
            }
        }

        /// <summary>同位换体（服务器裁决）：带变体 ai 的不溺者落场 + 蛰伏体静默移除。
        /// 房间坐标字段先写后 SyncNPC，与 ai 一并原子过线，远端不会看到缺参帧</summary>
        private void TransformToUndrowned() {
            NPC.TargetClosest(faceTarget: false);
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                ModContent.NPCType<Undrowned>(),
                ai2: Undrowned.EmergeVariantThrone, Target: NPC.target);
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC boss = Main.npc[idx];
                boss.Center = NPC.Center + new Vector2(0f, -14f);
                if (boss.ModNPC is Undrowned undrowned) {
                    undrowned.roomOriginX = roomOriginX;
                    undrowned.roomOriginY = roomOriginY;
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

        /// <summary>Boss 苏醒播报（NewNPC 不走 SpawnOnPlayer，需自己广播）</summary>
        private static void AnnounceAwaken(NPC boss) {
            if (VaultUtils.isServer) {
                ChatHelper.BroadcastChatMessage(
                    NetworkText.FromKey("Announcement.HasAwoken", boss.GetTypeNetName()),
                    new Color(88, 154, 148));
            }
            else {
                Main.NewText(Language.GetTextValue("Announcement.HasAwoken", boss.TypeName), 88, 154, 148);
            }
        }

        private Vector2 ValveWorldPos() {
            if (!HasRoom) {
                return NPC.Center;
            }
            Rectangle zone = FloodGalleryRoom.ValveZoneWorld(RoomOrigin);
            return zone.Center.ToVector2();
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

        /// <summary>眼缝亮度：蛰伏微光呼吸，仪式期攀到全亮</summary>
        private float EyeGlowLevel() {
            if ((int)Phase == 0) {
                return 0.12f + 0.07f * MathF.Sin(AmbientClock * 0.04f + Seed);
            }
            return MathHelper.Clamp(0.25f + (float)RiteTimer / 40f, 0f, 1f);
        }

        /// <summary>胸廓呼吸（纯绘制，判定框不动）：泡胀躯体的慢呼吸</summary>
        private float BreathScale()
            => 1f + 0.02f * MathF.Sin(AmbientClock * 0.035f + Seed);

        //==================== 绘制：链缚 → 靠锚 → 坐姿躯体 → 眼缝 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.CreatureFromTheDeep);
            Main.instance.LoadItem(ItemID.Anchor);
            Texture2D bodyTex = TextureAssets.Npc[NPCID.CreatureFromTheDeep]?.Value;
            Texture2D anchorTex = TextureAssets.Item[ItemID.Anchor]?.Value;
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            if (bodyTex == null || anchorTex == null || chainTex == null) {
                return false;
            }

            int t = (int)RiteTimer;
            Vector2 center = NPC.Center;
            //仪式后段战栗
            if ((int)Phase == 1 && t >= ChainBurstAt - 20) {
                center += new Vector2(MathF.Sin(t * 2.8f + Seed) * 1.5f, MathF.Sin(t * 3.6f) * 1f);
            }

            //倚在龛边的巨锚（他睡着也抱着刑具）
            Vector2 anchorPos = center + new Vector2(-26f, 16f);
            Undrowned.DrawAnchor(spriteBatch, anchorTex, anchorPos, -0.55f, drawColor, 1f);

            //坐姿躯体：帧 0，前倾坐相；暗缘压边 + 尸青主体（呼吸缩放）
            int count = Math.Max(1, Main.npcFrameCount[NPCID.CreatureFromTheDeep]);
            Rectangle frame = new(0, 0, bodyTex.Width, bodyTex.Height / count);
            Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.5f);
            float breath = BreathScale();
            const float slump = 0.34f;
            spriteBatch.Draw(bodyTex, center + new Vector2(0f, 3f) - Main.screenPosition, frame,
                Undrowned.CorpseDeep * 0.75f, -slump, origin, DrawScale * 1.07f * breath, SpriteEffects.FlipHorizontally, 0f);
            Color body = Color.Lerp(drawColor, Undrowned.CorpseTeal, 0.6f);
            spriteBatch.Draw(bodyTex, center - Main.screenPosition, frame,
                body, -slump, origin, DrawScale * breath, SpriteEffects.FlipHorizontally, 0f);

            //缚身锚链：两股横缠，链爆断拍后不再画（对应迸溅火花）
            if ((int)Phase == 0 || t < ChainBurstAt) {
                DrawBindChains(spriteBatch, chainTex, center, drawColor);
            }

            DrawEyeGlow(spriteBatch, center);
            return false;
        }

        /// <summary>缚身锚链：胸腹两道横链，蛰伏期随呼吸轻晃</summary>
        private void DrawBindChains(SpriteBatch sb, Texture2D chainTex, Vector2 center, Color lightColor) {
            Vector2 origin = chainTex.Size() * 0.5f;
            Color tint = lightColor.MultiplyRGB(new Color(120, 142, 128)) * 0.9f;
            for (int strand = 0; strand < 2; strand++) {
                float yOff = -8f + strand * 18f;
                Vector2 from = center + new Vector2(-30f, yOff);
                Vector2 to = center + new Vector2(30f, yOff + 6f);
                Vector2 prev = from;
                for (int i = 1; i <= 5; i++) {
                    float k = i / 5f;
                    Vector2 p = Vector2.Lerp(from, to, k);
                    p.Y += MathF.Sin(k * MathHelper.Pi) * 3f
                        + MathF.Sin(AmbientClock * 0.05f + Seed + strand * 2f) * 0.8f;
                    sb.Draw(chainTex, (prev + p) * 0.5f - Main.screenPosition, null, tint,
                        (p - prev).ToRotation() + MathHelper.PiOver2, origin, 0.8f, SpriteEffects.None, 0f);
                    prev = p;
                }
            }
        }

        /// <summary>眼缝加色层（Additive 批内强度写进色乘，永不 A=0 染色）</summary>
        private void DrawEyeGlow(SpriteBatch sb, Vector2 center) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float level = EyeGlowLevel();
            if (level < 0.03f) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 gOrigin = glow.Size() * 0.5f;
            Vector2 eye = center + new Vector2(10f, -22f);
            sb.Draw(glow, eye - Main.screenPosition, null, Undrowned.EyePale * (0.5f * level), 0f,
                gOrigin, new Vector2(7f * 2f / glow.Width), SpriteEffects.None, 0f);
            //座下底光：一汪沼靛
            sb.Draw(glow, center + new Vector2(0f, 26f) - Main.screenPosition, null,
                Undrowned.BogWater * (0.25f * level), 0f, gOrigin,
                new Vector2(40f * 2f / glow.Width, 12f / glow.Height), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
