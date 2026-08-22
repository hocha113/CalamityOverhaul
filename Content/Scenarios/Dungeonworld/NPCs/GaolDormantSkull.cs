using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
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
    /// 蛰伏的枯颅：深牢禁室祭坛上的怨灵遗蜕。静止免伤，靠微光呼吸、垂链轻响与
    /// 眼窝余烬告诉玩家"这东西会醒"。玩家进入触发半径 → 46 帧激活演出
    /// （眼窝点燃 → 锁链绷断 → 战栗）→ 服务器同位换体为 DeepGaolWraith 并走
    /// 骷髅头入场变体（ai[2]=1，怨灵接手骷髅头的位置与视觉，衔接无缝）。
    /// 联机契约：触发裁决与换体只在服务器，phase/timer 乘 ai[0..1] 过线，
    /// 各端本地跑同一演出时间线；换体= NewNPC(带 ai) + SyncNPC + 原地静默移除
    /// </summary>
    internal class GaolDormantSkull : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 参数（建议值，验收再调）====================

        /// <summary>激活触发半径（像素）：约 21 格，玩家必然已看清祭坛才触发</summary>
        internal const float TriggerRadius = 340f;
        /// <summary>激活演出总帧：眼窝点燃→锁链绷断→战栗→换体</summary>
        internal const int ActivateFrames = 46;
        private const int EyeIgniteAt = 2;
        private const int ChainSnapAt = 16;
        private const int RattleAt = 34;

        private const float DrawScale = 1.7f;

        /// <summary>ai[0]：0=蛰伏 1=激活演出</summary>
        private ref float Phase => ref NPC.ai[0];
        /// <summary>ai[1]：激活演出计时</summary>
        private ref float ActTimer => ref NPC.ai[1];
        /// <summary>本地环境音/呼吸计时（不入同步）</summary>
        private ref float AmbientClock => ref NPC.localAI[0];

        private float Seed => NPC.whoAmI * 0.7391f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 30;
            NPC.height = 30;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 200;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.dontTakeDamage = true;
            NPC.value = 0;
            NPC.npcSlots = 1f;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        public override void AI() {
            NPC.velocity = Vector2.Zero;
            AmbientClock++;

            if ((int)Phase == 0) {
                UpdateDormant();
            }
            else {
                UpdateActivate();
            }

            //眼窝余烬照亮祭坛一角
            float glow = EyeGlowLevel();
            if (glow > 0.03f) {
                Lighting.AddLight(NPC.Center, 0.26f * glow, 0.09f * glow, 0.16f * glow);
            }
        }

        //==================== 蛰伏 ====================

        private void UpdateDormant() {
            //低成本 telegraph：链声轻响 + 偶发怨魂雾滴（各端本地，无需同步）
            if (!Main.dedServ) {
                if ((int)AmbientClock % 300 == 299) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.16f, Pitch = -0.75f, MaxInstances = 2 }, NPC.Center);
                }
                if (Main.rand.NextBool(90)) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        NPC.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(4f, 14f)),
                        new Vector2(0f, -Main.rand.NextFloat(0.25f, 0.5f)),
                        DeepGaolWraith.MistTint * 0.45f, Main.rand.NextFloat(0.25f, 0.4f))
                        ?.Configure(Main.rand.Next(30, 50));
                }
            }

            //触发裁决只在服务器：玩家踏入半径即点火，结果乘 ai+netUpdate 过线
            if (VaultUtils.isClient) {
                return;
            }
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && Vector2.Distance(player.Center, NPC.Center) < TriggerRadius) {
                    Phase = 1;
                    ActTimer = 0;
                    NPC.netUpdate = true;
                    break;
                }
            }
        }

        //==================== 激活演出（30-60f 预告缓冲，入场节拍纪律）====================

        private void UpdateActivate() {
            ActTimer++;
            int t = (int)ActTimer;

            if (t == EyeIgniteAt) {
                //第一拍：眼窝点燃，低鸣起
                SoundEngine.PlaySound(SoundID.DD2_DarkMageAttack with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
            }

            if (t == ChainSnapAt) {
                //第二拍：镣链绷断，铁屑迸溅
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.05f, MaxInstances = 2 }, NPC.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 2 }, NPC.Center);
                if (!Main.dedServ) {
                    for (int k = 0; k < 6; k++) {
                        PRTLoader.NewParticle<PRT_Spark>(NPC.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), 10f),
                            new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(0.5f, 2.4f)),
                            Color.Lerp(DeepGaolWraith.GaolPink, Color.White, Main.rand.NextFloat(0.4f)),
                            Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(10, 16));
                    }
                }
            }

            if (t == RattleAt) {
                //第三拍：整颅战栗，近处震屏，最后的逃跑窗口
                SoundEngine.PlaySound(SoundID.NPCHit36 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 2 }, NPC.Center);
                ShakeNearby(2f);
            }

            //战栗期怨雾向心收拢（怨灵正在凝形）
            if (!Main.dedServ && t > ChainSnapAt && t % 2 == 0) {
                Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 80f);
                PRTLoader.NewParticle<PRT_GhostRainMist>(from, (NPC.Center - from) * 0.07f,
                    Color.Lerp(DeepGaolWraith.MistTint, DeepGaolWraith.GaolPink, 0.35f) * 0.7f,
                    Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(16, 28));
            }

            if (t >= ActivateFrames && !VaultUtils.isClient) {
                TransformToWraith();
            }
        }

        /// <summary>同位换体（服务器裁决）：带变体 ai 的怨灵落场 + 枯颅静默移除。
        /// NewNPC 的 ai 随首个 SyncNPC 原子过线，远端不会看到缺参帧</summary>
        private void TransformToWraith() {
            NPC.TargetClosest(faceTarget: false);
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                ModContent.NPCType<DeepGaolWraith>(),
                ai2: DeepGaolWraith.EmergeVariantSkull, Target: NPC.target);
            if (idx >= 0 && idx < Main.maxNPCs) {
                Main.npc[idx].Center = NPC.Center;
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
                AnnounceAwaken(Main.npc[idx]);
            }

            NPC.active = false;
            NPC.life = 0;
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        /// <summary>Boss 苏醒播报（NewNPC 不走 SpawnOnPlayer，需自己广播）</summary>
        private static void AnnounceAwaken(NPC wraith) {
            if (VaultUtils.isServer) {
                ChatHelper.BroadcastChatMessage(
                    NetworkText.FromKey("Announcement.HasAwoken", wraith.GetTypeNetName()),
                    new Color(175, 75, 255));
            }
            else {
                Main.NewText(Language.GetTextValue("Announcement.HasAwoken", wraith.TypeName), 175, 75, 255);
            }
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

        /// <summary>眼窝亮度：蛰伏微光呼吸，激活后 24 帧内攀到全亮</summary>
        private float EyeGlowLevel() {
            if ((int)Phase == 0) {
                return 0.14f + 0.08f * MathF.Sin(AmbientClock * 0.045f + Seed);
            }
            return MathHelper.Clamp(0.25f + (float)ActTimer / 24f, 0f, 1f);
        }

        /// <summary>悬浮呼吸位移（纯绘制，判定框不动）</summary>
        private float BobOffset()
            => MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + Seed) * 3f;

        //==================== 绘制：垂链 → 暗缘 → 颅体 → 眼窝/底光 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadItem(ItemID.Skull);
            Texture2D skullTex = TextureAssets.Item[ItemID.Skull]?.Value;
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            if (skullTex == null || chainTex == null) {
                return false;
            }

            Vector2 center = NPC.Center + new Vector2(0f, BobOffset());
            int t = (int)ActTimer;
            //战栗期高频抖动
            if ((int)Phase == 1 && t >= RattleAt) {
                center += new Vector2(MathF.Sin(t * 2.9f + Seed) * 1.6f, MathF.Sin(t * 3.7f) * 1.1f);
            }

            //镣链：从颅底垂到祭坛，绷断拍后不再画（对应迸溅火花）
            if ((int)Phase == 0 || t < ChainSnapAt) {
                DrawShackleChains(spriteBatch, chainTex, center, drawColor);
            }

            Vector2 origin = skullTex.Size() * 0.5f;
            Vector2 drawPos = center - Main.screenPosition;
            float scale = DrawScale;
            if ((int)Phase == 1) {
                scale *= 1f + 0.08f * MathHelper.Clamp(t / (float)ActivateFrames, 0f, 1f);
            }

            //暗缘压边 + 冷灰骨色主体（旧骨不该是纯白）
            spriteBatch.Draw(skullTex, drawPos, null, DeepGaolWraith.IronDeep * 0.8f, 0f, origin, scale * 1.1f, SpriteEffects.None, 0f);
            Color boneCol = Color.Lerp(drawColor, new Color(198, 204, 202), 0.45f);
            spriteBatch.Draw(skullTex, drawPos, null, boneCol, 0f, origin, scale, SpriteEffects.None, 0f);

            DrawGlow(spriteBatch, center);
            return false;
        }

        /// <summary>祭坛镣链：两股短链自颅底垂向镣铐台，蛰伏期轻轻摆</summary>
        private void DrawShackleChains(SpriteBatch sb, Texture2D chainTex, Vector2 center, Color lightColor) {
            Vector2 origin = chainTex.Size() * 0.5f;
            Color tint = lightColor.MultiplyRGB(DeepGaolWraith.IronMul) * 0.85f;
            for (int i = 0; i < 2; i++) {
                float side = i == 0 ? -1f : 1f;
                Vector2 prev = center + new Vector2(side * 7f, 8f);
                for (int k = 0; k < 3; k++) {
                    Vector2 p = prev + new Vector2(
                        side * 2f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + Seed + i * 2.3f + k) * 1.6f,
                        11f);
                    sb.Draw(chainTex, (prev + p) * 0.5f - Main.screenPosition, null,
                        tint * (1f - k * 0.2f), (p - prev).ToRotation() + MathHelper.PiOver2,
                        origin, 0.85f, SpriteEffects.None, 0f);
                    prev = p;
                }
            }
        }

        /// <summary>加色层：眼窝双点 + 底部呼吸底光；Additive 批内强度写进色值</summary>
        private void DrawGlow(SpriteBatch sb, Vector2 center) {
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
            //眼窝双点
            for (int side = -1; side <= 1; side += 2) {
                Vector2 eye = center + new Vector2(side * 5.5f * DrawScale * 0.6f, -3f);
                sb.Draw(glow, eye - Main.screenPosition, null, DeepGaolWraith.GaolPink * (0.55f * level), 0f,
                    gOrigin, new Vector2(6f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            //底光：祭坛上的一汪冷粉
            sb.Draw(glow, center + new Vector2(0f, 20f) - Main.screenPosition, null,
                DeepGaolWraith.GaolPinkDeep * (0.3f * level), 0f, gOrigin,
                new Vector2(46f * 2f / glow.Width, 14f / glow.Height), SpriteEffects.None, 0f);
            //激活期全颅罩光
            if ((int)Phase == 1) {
                float k = MathHelper.Clamp((float)ActTimer / ActivateFrames, 0f, 1f);
                sb.Draw(glow, center - Main.screenPosition, null, DeepGaolWraith.GaolPink * (0.35f * k), 0f,
                    gOrigin, new Vector2(30f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
