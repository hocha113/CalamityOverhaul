using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using CWRNpcGlobal = CalamityOverhaul.Content.CWRNpc;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EaterOfWorlds
{
    /// <summary>
    /// 酸蚀叠层承载(蚀界之颚)。层数/计时/归属放逐NPC实例，禁static可变状态。<br/>
    /// <b>联机模型(镜像 KikasaTalismanStackNPC)</b>：写入端=效果归属端(命中挂钩在攻击方客户端)，
    /// 写入即广播绝对量；服务端承载(lifeRegen/OnKill 权威)并转播旁观端做表现；
    /// 计时各端本地自走，丢包由下一次写入自愈，多写入者按后到覆盖(可接受的表现级近似)
    /// </summary>
    internal sealed class MawCorrosionNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>当前酸蚀层数，0=无</summary>
        private byte stacks;
        /// <summary>剩余帧，归零清层</summary>
        private ushort timer;
        /// <summary>最后写入者(who+1，0=无)，击杀时幼虫归属此人</summary>
        private byte ownerPlus;

        /// <summary>酸蚀膜强度包络0~1，叠层淡入失效淡出</summary>
        private float etchFade;

        //PreDraw置位、PostDraw消费的批次闩锁(单线程绘制状态，非游戏状态)
        private static bool shaderActive;

        internal int Stacks => timer > 0 ? stacks : 0;
        internal int OwnerWho => ownerPlus - 1;

        #region 承载与计时
        private void ApplyLocal(byte count, ushort time, byte owner) {
            if (count == 0) {
                stacks = 0;
                timer = 0;
                ownerPlus = 0;
                return;
            }
            stacks = count;
            timer = time;
            ownerPlus = owner;
        }

        public override void PostAI(NPC npc) {
            if (timer > 0 && --timer == 0) {
                stacks = 0;
                ownerPlus = 0;
            }

            //酸蚀膜包络：双向逼近目标强度，1层也保底可见
            float target = Stacks > 0
                ? MathF.Max(Stacks / (float)WorldEatersMaw.MaxStacks, 0.25f) : 0f;
            etchFade = etchFade < target
                ? MathF.Min(etchFade + 1f / 18f, target)
                : MathF.Max(etchFade - 1f / 30f, target);

            //体表紫绿酸泡：密度随层数爬升(客户端表现)
            if (Main.dedServ || Stacks <= 0 || !EowMotionFX.OnScreen(npc.Center)) {
                return;
            }
            if (Main.rand.NextBool(Math.Max(3, 16 - Stacks))) {
                Vector2 surfacePos = npc.Center + new Vector2(
                    Main.rand.NextFloat(-0.42f, 0.42f) * npc.width,
                    Main.rand.NextFloat(-0.42f, 0.42f) * npc.height);
                PRTLoader.NewParticle<PRT_ToxicBubble>(surfacePos,
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f), Color.White,
                    Main.rand.NextFloat(0.10f, 0.16f) + Stacks * 0.006f)
                    .Configure(Main.rand.Next(24, 40));
            }
            if (Stacks >= 5 && Main.rand.NextBool(22)) {
                //高层数滴酸
                PRTLoader.NewParticle<PRT_AcidSplash>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)), Color.White,
                    Main.rand.NextFloat(0.3f, 0.5f)).Configure(Main.rand.Next(16, 28));
            }
        }
        #endregion

        #region 数值效果
        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            int s = Stacks;
            if (s > 0) {
                CWRNpcGlobal.DebuffSet(s * WorldEatersMaw.DotPerStack, s * 2, ref npc.lifeRegen, ref damage);
            }
        }

        /// <summary>酸蚀削甲：判伤端(攻击方客户端)按本地层数削减目标防御，全队受益。结算取整，满层-15</summary>
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            int s = Stacks;
            if (s > 0) {
                modifiers.Defense.Flat -= (int)(s * WorldEatersMaw.DefShredPerStack);
            }
        }
        #endregion

        #region 击杀出虫
        /// <summary>击杀带酸蚀的敌人：尸位钻出友方吞世幼虫(OnKill只在权威端触发)</summary>
        public override void OnKill(NPC npc) {
            if (Stacks <= 0 || npc.SpawnedFromStatue) {
                return;
            }
            int owner = OwnerWho;
            if (owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[owner];
            if (player == null || !player.active || player.dead
                || !player.TryGetModPlayer(out WorldEatersMawPlayer mp) || !mp.Equipped) {
                return;
            }
            //出虫内置冷却：冷却期内的酸蚀死亡不出虫(尸位冒泡表现在HitEffect)，斩断击杀链永动
            if (mp.WormSpawnCooldown > 0) {
                return;
            }
            mp.WormSpawnCooldown = WorldEatersMaw.WormSpawnCooldownTicks;

            int wormType = ModContent.ProjectileType<MawWormProj>();
            EnforceWormCap(owner, wormType);

            //出生弧线：向上抛出，横向按NPC槽位定相位(各端一致)
            Vector2 vel = new Vector2((npc.whoAmI % 5 - 2) * 1.4f, -10.5f);
            int damage = (int)player.GetTotalDamage(DamageClass.Generic).ApplyTo(WorldEatersMaw.WormBaseDamage);
            //ai[0]契约是who+1(0=无猎物)，与MawWormProj.TargetWho解码一致
            int targetHint = FindNextTargetHint(npc) + 1;
            int proj = Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, vel,
                wormType, damage, 2f, owner, targetHint, 0f, 0f);
            if (proj < Main.maxProjectiles && VaultUtils.isServer) {
                //服务端代玩家生成的弹幕不会自发同步包，必须显式广播
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, proj);
            }
        }

        /// <summary>
        /// 冷却期内的酸蚀死亡：尸位冒一小撮酸泡但无虫破出，向玩家解释"为什么没出虫"。<br/>
        /// HitEffect各端随伤害包自跑；冷却读数只有权威端与owner镜像持有，故只在owner本机播
        /// </summary>
        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (npc.life > 0 || VaultUtils.isServer || Stacks <= 0) {
                return;
            }
            int owner = OwnerWho;
            if (owner != Main.myPlayer || owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            if (!Main.player[owner].TryGetModPlayer(out WorldEatersMawPlayer mp)
                || mp.WormSpawnCooldown <= 0 || !EowMotionFX.OnScreen(npc.Center)) {
                return;
            }
            int count = Main.rand.Next(3, 6);
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_ToxicBubble>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.35f, npc.height * 0.35f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.4f), Color.White,
                    Main.rand.NextFloat(0.12f, 0.2f)).Configure(Main.rand.Next(20, 34));
            }
        }

        /// <summary>满编时点名最老一条(剩余寿命最短)转入消散，ai[2]=1经同步包送达各端</summary>
        private static void EnforceWormCap(int owner, int wormType) {
            int count = 0;
            int oldest = -1;
            int oldestTime = int.MaxValue;
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type != wormType || p.owner != owner || p.ai[2] == 1f) {
                    continue;
                }
                count++;
                if (p.timeLeft < oldestTime) {
                    oldestTime = p.timeLeft;
                    oldest = p.whoAmI;
                }
            }
            if (count < WorldEatersMaw.WormCap || oldest < 0) {
                return;
            }
            Main.projectile[oldest].ai[2] = 1f;
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, oldest);
            }
        }

        /// <summary>为新生幼虫挑初始猎物：尸体附近最近的可追击敌人</summary>
        private static int FindNextTargetHint(NPC corpse) {
            int best = -1;
            float bestDist = 1100f;
            foreach (var other in Main.ActiveNPCs) {
                if (other.whoAmI == corpse.whoAmI || !other.CanBeChasedBy()) {
                    continue;
                }
                float dist = corpse.Distance(other.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = other.whoAmI;
                }
            }
            return best;
        }
        #endregion

        #region 绘制(酸蚀膜重绘+照明)
        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (Main.dedServ || etchFade <= 0.01f) {
                return;
            }
            Lighting.AddLight(npc.Center, EowMotionFX.AcidGreen.ToVector3() * 0.4f * etchFade);
            //着色器缺席时的回退染色(有着色器时只轻推)
            drawColor = Color.Lerp(drawColor, EowMotionFX.AcidGreen, etchFade * 0.18f);
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上一实体断批自愈：GlobalNPC 的 PostDraw 会被 ModNPC.PreDraw=false 吞掉
            if (shaderActive) {
                shaderActive = false;
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.GameViewMatrix.TransformationMatrix);
            }
            if (etchFade <= 0.02f || npc.IsABestiaryIconDummy) {
                return true;
            }
            Effect fx = EffectLoader.BRelicMawEtch?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return true;
            }

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = tex.Bounds;
            }

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uTexelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            //帧界半像素内缩：采样全数钳回帧内，防精灵表渗色
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                (frame.X + 0.5f) / tex.Width, (frame.Y + 0.5f) / tex.Height,
                (frame.X + frame.Width - 0.5f) / tex.Width, (frame.Y + frame.Height - 0.5f) / tex.Height));
            fx.Parameters["uEtchT"]?.SetValue(etchFade);
            fx.Parameters["uStackT"]?.SetValue(Stacks / (float)WorldEatersMaw.MaxStacks);
            fx.Parameters["uSeed"]?.SetValue(npc.whoAmI * 0.618f % 1f * 8f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique = fx.Techniques["TechEtch"];
            fx.CurrentTechnique.Passes[0].Apply();
            shaderActive = true;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!shaderActive) {
                return;
            }
            shaderActive = false;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
        #endregion

        #region 读写API(写入端=效果归属端)
        internal static int GetStacks(NPC npc) {
            return npc?.active == true && npc.TryGetGlobalNPC(out MawCorrosionNPC host) ? host.Stacks : 0;
        }

        /// <summary>叠加层数并刷新计时+骑原版同步挂酸蚀buff；返回新层数</summary>
        internal static int AddStacks(NPC npc, int delta, int applierWho) {
            if (npc?.active != true || npc.friendly || npc.townNPC || delta <= 0) {
                return 0;
            }
            int buffType = ModContent.BuffType<MawCorrosionBuff>();
            if (npc.buffImmune[buffType]) {
                return 0;
            }
            if (!npc.TryGetGlobalNPC(out MawCorrosionNPC host)) {
                return 0;
            }

            byte next = (byte)Utils.Clamp(host.Stacks + delta, 0, WorldEatersMaw.MaxStacks);
            host.ApplyLocal(next, WorldEatersMaw.BrandDuration, (byte)(applierWho + 1));
            npc.AddBuff(buffType, WorldEatersMaw.BrandDuration);
            Broadcast(npc, next, WorldEatersMaw.BrandDuration, (byte)(applierWho + 1));

            //命中溅酸(写入端本地表现，层数越高越浓)
            if (!VaultUtils.isServer && EowMotionFX.OnScreen(npc.Center)) {
                EowMotionFX.SpawnAcidBurst(npc.Center, 0.35f + next * 0.05f);
            }
            return next;
        }
        #endregion

        #region 联机(紧凑广播：npcWho/npcType/stacks/timer/owner 定长9字节)
        private static void Broadcast(NPC npc, byte count, ushort time, byte owner) {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<MawCorrosionNet>();
            packet.Write((byte)npc.whoAmI);
            packet.Write(npc.type);
            packet.Write(count);
            packet.Write(time);
            packet.Write(owner);
            packet.Send();
        }

        internal static void HandleNet(BinaryReader reader, int whoAmI) {
            //定长负载先读满再校验(流对齐纪律)
            int npcWho = reader.ReadByte();
            int npcType = reader.ReadInt32();
            byte count = reader.ReadByte();
            ushort time = reader.ReadUInt16();
            byte owner = reader.ReadByte();

            if (npcWho >= Main.maxNPCs) {
                return;
            }
            NPC npc = Main.npc[npcWho];
            //槽位+类型双校验：类型不符=槽位已被复用，静默丢弃(计时兜底自清残留)
            if (npc?.active != true || npc.type != npcType) {
                return;
            }
            if (npc.TryGetGlobalNPC(out MawCorrosionNPC host)) {
                host.ApplyLocal(count, time, owner);
            }
            if (Main.netMode == NetmodeID.Server) {
                //服务端校验通过后原样转播给发送者之外的所有端
                ModPacket packet = CWRNetWork.GetPacket<MawCorrosionNet>();
                packet.Write((byte)npcWho);
                packet.Write(npcType);
                packet.Write(count);
                packet.Write(time);
                packet.Write(owner);
                packet.Send(-1, whoAmI);
            }
        }
        #endregion
    }

    /// <summary>酸蚀叠层广播信道(归属端写入，服务端承载并转播旁观端)</summary>
    internal sealed class MawCorrosionNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => MawCorrosionNPC.HandleNet(reader, whoAmI);
    }

    /// <summary>
    /// 酸蚀减益：可见载体与兼容面(免疫语义/其他系统查询)，
    /// 层数与数值效果由 <see cref="MawCorrosionNPC"/> 承载
    /// </summary>
    internal sealed class MawCorrosionBuff : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "MawCorrosionBuff";

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "酸蚀");

        public override LocalizedText Description
            => this.GetLocalization(nameof(Description), () => "护甲正在被酸液啃穿");

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }
    }
}
