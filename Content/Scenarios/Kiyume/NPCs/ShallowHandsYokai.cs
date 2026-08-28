using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 水中手（P4 点子6，R2-A）：涨潮期滩涂湿滩带立起的湿黑手，纯危害体零 AI。<br/>
    /// 规矩：涨潮（潮位归一 ≥0.6）导演泵一茬 3-5 只沿水线立起；站进抓距（48px）
    /// 被咬一口（45 伤 + 迟缓 120t，唯一伤害窗，主动点名结算非接触伤）后缩回冷却再立；
    /// 退潮全部沉回泥里（Fade），打死的这一茬不补，下个涨潮窗才有新手。<br/>
    /// 联机合同：ai[0]=状态 ai[1]=状态计时 ai[2]=出没起点（状态参数）；实体永不位移
    /// （出没全是绘制包络），转移全服务器、各端由 ai 确定重放；咬合走服务器 Hurt +
    /// SendPlayerHurt、迟缓走服务器 AddBuff + 55 号 AddPlayerBuff（二审正字样板照抄，
    /// 对源：两者发包分支都只在 netMode==1，服务器直调只写本端镜像）。<br/>
    /// 视觉：SkeletronHand 单帧指尖朝上过 KikasaHound.fx uMode=1 湿墨链（井手同链，
    /// 零新 shader）；uDissolve 走满的那一帧实体已退场（残斑同帧退场约定）
    /// </summary>
    internal class ShallowHandsYokai : KiyumeYokaiNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[0] 状态位：立起 → 站桩 → 咬合 → 缩回冷却；沉泥退场（导演退潮回收）
        private const int StateRise = 0;
        private const int StateStand = 1;
        private const int StateSnap = 2;
        private const int StateRetract = 3;
        private const int StateSink = 4;

        //──── 纯演出常量（机制量在 KiyumeYokaiMetrics.Shallow*）────

        /// <summary>缩回冷却期沉到的出没度（也是再立起的起点，包络连续无跳变）</summary>
        private const float RetractDepth = 0.25f;
        /// <summary>缩回下沉用时（tick，其后维持 RetractDepth 到冷却结束）</summary>
        private const float RetractSinkTicks = 20f;
        /// <summary>全沉时相对站姿的下沉绘制偏移（px）</summary>
        private const float SunkOffsetPx = 34f;
        /// <summary>咬合扬起偏移（px）与纵向拉伸峰值</summary>
        private const float SnapRearPx = 6f;
        private const float SnapStretch = 0.10f;
        /// <summary>绘制缩放（井手 1.15 的近亲，滩涂手略小）</summary>
        private const float HandScale = 1.1f;
        /// <summary>现形语法（可读性极性）：抓距规矩必须近处可读——
        /// 近处强制现形，远且雾浓才沉入雾影，遮蔽上限 0.7</summary>
        private const float FogHideNearPx = 200f;
        private const float FogHideFarPx = 560f;
        private const float FogHideMax = 0.7f;

        //湿墨轮廓缘色与怨雾色（井手同族）
        private static readonly Color InkEdge = new(60, 18, 20);
        private static readonly Color MistTint = new(96, 78, 84);

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        protected override void SetYokaiDefaults() {
            NPC.width = 28;
            NPC.height = 42;
            NPC.damage = 0;          //无接触伤：咬合是主动点名结算（唯一伤害窗）
            NPC.defense = KiyumeYokaiMetrics.ShallowDefense;
            NPC.lifeMax = KiyumeYokaiMetrics.ShallowLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;   //植根泥里的静桩，永不位移
            NPC.npcSlots = 0.25f;
            NPC.alpha = 0;   //出生透明度显式（VFX 高复发缺陷②）
            NPC.HitSound = SoundID.NPCHit1 with { Pitch = -0.35f };
            NPC.DeathSound = SoundID.NPCDeath1 with { Pitch = -0.5f };
        }

        //接触判定永不伤人：贴着站到咬合拍之前都只是吓人
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        //==================== 行为 ====================

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateCue();
            }
            NPC.velocity = Vector2.Zero;
            //沉泥退场不吃补刀（其余全程可打：打死这一茬不补）
            NPC.dontTakeDamage = (int)State == StateSink;

            switch ((int)State) {
                case StateStand:
                    UpdateStand();
                    break;
                case StateSnap:
                    UpdateSnap();
                    break;
                case StateRetract:
                    UpdateRetract();
                    break;
                case StateSink:
                    UpdateSink();
                    break;
                default:
                    UpdateRise();
                    break;
            }

            //咬合拍微光：贴脸危害的可读性兜底（站桩不发光，滩上不自曝）
            if ((int)State == StateSnap) {
                Lighting.AddLight(NPC.Center, 0.10f, 0.03f, 0.03f);
            }
        }

        //立起：出没度自起点走满（计时各端确定重放，转移服务器裁决）
        private void UpdateRise() {
            StateTimer++;
            if (!VaultUtils.isClient && StateTimer >= KiyumeYokaiMetrics.ShallowRiseTicks) {
                ChangeState(StateStand);
            }
        }

        //站桩：唯一的"AI"就是量抓距（服务器）
        private void UpdateStand() {
            StateTimer++;
            if (VaultUtils.isClient) {
                return;
            }
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                if (Vector2.Distance(player.Center, NPC.Center)
                    <= KiyumeYokaiMetrics.ShallowGrabRange) {
                    ChangeState(StateSnap);
                    return;
                }
            }
        }

        //咬合：短前摇是唯一的可读拍（冲刺可脱），拍尾服务器结算一口
        private void UpdateSnap() {
            StateTimer++;
            if (!VaultUtils.isClient && StateTimer >= KiyumeYokaiMetrics.ShallowSnapTicks) {
                SettleBite();
                ChangeState(StateRetract);
            }
        }

        //缩回冷却：沉低不可咬（可打），冷却尽再立起（起点=RetractDepth，包络连续）
        private void UpdateRetract() {
            StateTimer++;
            if (!VaultUtils.isClient && StateTimer >= KiyumeYokaiMetrics.ShallowRetractTicks) {
                ChangeState(StateRise, RetractDepth);
            }
        }

        //沉泥退场：各端确定推演，走满那一帧实体已不在（uDissolve=1 残斑不上屏）
        private void UpdateSink() {
            StateTimer++;
            if (StateTimer >= KiyumeYokaiMetrics.ShallowSinkTicks) {
                NPC.active = false;
            }
        }

        /// <summary>
        /// 咬合结算（服务器，单口不做多目标）：结算距内最近活人挨 45 + 迟缓 120t。
        /// 服务器 Hurt 只写本端镜像（对源：发包分支仅 netMode==1 且 myPlayer）——
        /// 显式广播 HurtInfo；服务器 AddBuff 同理不自发包，显式补 55 号（二审正字）
        /// </summary>
        private void SettleBite() {
            Player victim = null;
            float best = KiyumeYokaiMetrics.ShallowSnapSettleRange;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float d = Vector2.Distance(player.Center, NPC.Center);
                if (d < best) {
                    best = d;
                    victim = player;
                }
            }
            if (victim == null) {
                return;   //前摇里退出了抓距，这一口落空
            }
            int dir = victim.Center.X < NPC.Center.X ? -1 : 1;
            double dealt = victim.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI),
                KiyumeYokaiMetrics.ShallowBiteDamage, dir, out Player.HurtInfo info);
            if (dealt > 0.0 && VaultUtils.isServer) {
                NetMessage.SendPlayerHurt(victim.whoAmI, info);
            }
            victim.AddBuff(BuffID.Slow, KiyumeYokaiMetrics.ShallowBiteSlowTicks);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.AddPlayerBuff, -1, -1, null,
                    victim.whoAmI, BuffID.Slow, KiyumeYokaiMetrics.ShallowBiteSlowTicks);
            }
        }

        /// <summary>沉泥回收（导演退潮泵调，服务器）：从当前出没度连续沉到零</summary>
        internal void BeginSink() {
            if ((int)State == StateSink) {
                return;
            }
            ChangeState(StateSink, Emergence01());
        }

        //==================== 出没包络（各端由 ai 确定重放，实体位置恒定） ====================

        /// <summary>出没度 0=全沉泥里 1=站满；状态×计时的确定函数，跨态起点走 ai[2] 保连续</summary>
        private float Emergence01() {
            float t = StateTimer;
            return (int)State switch {
                StateStand or StateSnap => 1f,
                StateRetract => MathHelper.Lerp(1f, RetractDepth,
                    Math.Min(1f, t / RetractSinkTicks)),
                StateSink => MathHelper.Lerp(StateParam, 0f,
                    Math.Min(1f, t / KiyumeYokaiMetrics.ShallowSinkTicks)),
                _ => MathHelper.Lerp(StateParam, 1f,
                    Math.Min(1f, t / KiyumeYokaiMetrics.ShallowRiseTicks)),
            };
        }

        //==================== 表现（状态沿音画，各端本地重放） ====================

        private void PlayStateCue() {
            if (Main.dedServ) {
                return;
            }
            switch ((int)State) {
                case StateRise:
                    //立起（出生首帧也吃到一次沿）：湿泥翻涌的一声轻响
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.5f,
                        Pitch = -0.7f,
                        MaxInstances = 3
                    }, NPC.Center);
                    EmitMist(2, 0.45f);
                    break;
                case StateSnap:
                    //扬起：出水急响 + 泥点（可读拍的声画都在这）
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.9f,
                        Pitch = -0.1f,
                        MaxInstances = 3
                    }, NPC.Center);
                    for (int i = 0; i < 5; i++) {
                        Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                            DustID.Mud, Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1.2f, 2.6f));
                        dust.scale = Main.rand.NextFloat(0.8f, 1.2f);
                    }
                    break;
                case StateRetract:
                    //合口拍（命中与落空同声：钳口拍上就是这一响）
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.8f,
                        Pitch = -0.45f,
                        MaxInstances = 3
                    }, NPC.Center);
                    EmitMist(3, 0.6f);
                    break;
                case StateSink:
                    //沉泥：一声很低的闷咕
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.4f,
                        Pitch = -0.8f,
                        MaxInstances = 3
                    }, NPC.Center);
                    EmitMist(2, 0.5f);
                    break;
            }
        }

        private void EmitMist(int count, float tint) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(0f, 10f)),
                    new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.5f, 1.1f)),
                    MistTint * tint, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(24, 44));
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            int count = NPC.life > 0 ? 2 : 8;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + Main.rand.NextVector2Circular(10f, 14f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(0.3f, 0.9f), -Main.rand.NextFloat(0.3f, 0.9f)),
                    MistTint * 0.65f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(20, 40));
            }
        }

        //==================== 布防/回收静态面（导演泵专用，全部服务器侧调用） ====================

        /// <summary>
        /// 涨潮布防：湿滩列窗内均布一茬 3-5 只（落点探地；贴脸/挨着蓑翁的点弃权不补，
        /// 缺一两只无碍——这茬手本就该稀稀拉拉）。生成走受控出口 SpawnYokai
        /// </summary>
        internal static void SeedFlat() {
            int count = Main.rand.Next(KiyumeYokaiMetrics.ShallowCountMin,
                KiyumeYokaiMetrics.ShallowCountMax + 1);
            int span = KiyumeYokaiMetrics.ShallowColMax - KiyumeYokaiMetrics.ShallowColMin;
            for (int i = 0; i < count; i++) {
                float t = (i + 0.5f) / count;
                int col = KiyumeYokaiMetrics.ShallowColMin + (int)(t * span) + Main.rand.Next(-6, 7);
                col = Math.Clamp(col, KiyumeYokaiMetrics.ShallowColMin, KiyumeYokaiMetrics.ShallowColMax);
                int row = KiyumePlans.ProbeGround(col, KiyumePlans.FloorTopAt(col) - 6);
                var pos = new Vector2(col * 16f + 8f, row * 16f);
                if (!SpotClear(pos)) {
                    continue;
                }
                KiyumeHauntDirector.SpawnYokai(ModContent.NPCType<ShallowHandsYokai>(), pos);
            }
        }

        //落点合法：不在人眼前立起 + 不挤进蓑翁的座位（两个遭遇不糊在一起）
        private static bool SpotClear(Vector2 pos) {
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && player.Distance(pos) < 400f) {
                    return false;
                }
            }
            int minoType = ModContent.NPCType<MinoFisherYokai>();
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == minoType && Vector2.Distance(npc.Center, pos) < 200f) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>退潮回收（导演泵调，服务器）：在场全员沉回泥里</summary>
        internal static void RecallAll() {
            int type = ModContent.NPCType<ShallowHandsYokai>();
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == type && npc.ModNPC is ShallowHandsYokai hand) {
                    hand.BeginSink();
                }
            }
        }

        //==================== 绘制：SkeletronHand 单帧过 KikasaHound.fx uMode=1 湿墨链 ====================
        //全接管绘制：透明度全程显式给值（出没包络），不依赖 NPC.alpha

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游实体批状态泄漏自愈
            BeginDefault(spriteBatch);
            Main.instance.LoadNPC(NPCID.SkeletronHand);
            Texture2D tex = TextureAssets.Npc[NPCID.SkeletronHand].Value;
            if (tex == null) {
                return false;
            }

            float emergence = Emergence01();
            if (emergence <= 0.01f) {
                return false;
            }
            //透明度：出没包络 × 现形语法（近处强制可读，远且雾浓沉入雾影）
            float dist = NearestLiveDistance();
            float fogHide = FogRevealTerm(NPC.Center)
                * DistanceRevealTerm(dist, FogHideNearPx, FogHideFarPx);
            float bodyAlpha = MathHelper.Clamp(emergence * 1.15f, 0f, 1f)
                * 0.92f * (1f - FogHideMax * fogHide);
            //湿墨蚀散：全沉=1（同帧退场约定），站满余 0.10 湿边碎蚀
            float dissolve = 1f - emergence * 0.90f;

            //咬合扬起（可读拍）：抬高 + 纵向拉伸
            float snapT = (int)State == StateSnap
                ? Math.Min(1f, StateTimer / KiyumeYokaiMetrics.ShallowSnapTicks) : 0f;
            float sway = MathF.Sin(AmbientClock * 0.033f + Seed) * 0.055f * emergence;
            Vector2 drawPos = NPC.Center - screenPos
                + new Vector2(0f, (1f - emergence) * SunkOffsetPx - snapT * SnapRearPx);
            Vector2 origin = new(tex.Width * 0.5f, tex.Height * 0.5f);
            var scale = new Vector2(HandScale, HandScale * (1f + snapT * SnapStretch));

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (hound == null || noise == null) {
                //shader 缺编：湿墨近黑剪影回退（井手同款）
                spriteBatch.Draw(tex, drawPos, null, new Color(10, 5, 8) * (bodyAlpha * 0.9f),
                    sway, origin, scale, SpriteEffects.None, 0f);
                return false;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //参数链照抄井手实体态：单帧全图 uUvRect，手无目 uEyeGlow=0；奇偶左右手镜像
            hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            hound.Parameters["uSeed"]?.SetValue(Seed);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)tex.Height);
            hound.Parameters["uFlipH"]?.SetValue((NPC.whoAmI & 1) == 1 ? 1f : 0f);
            hound.Parameters["uFlipV"]?.SetValue(0f);
            hound.Parameters["uMode"]?.SetValue(1f);
            hound.Parameters["uSeamGate"]?.SetValue(0f);
            hound.Parameters["uWobble"]?.SetValue(0.010f);
            hound.Parameters["uEyeGlow"]?.SetValue(0f);
            hound.Parameters["uEyeAnchor"]?.SetValue(new Vector2(0.5f, 0.5f));
            hound.Parameters["uDissolve"]?.SetValue(dissolve);
            hound.Parameters["uEdgeTint"]?.SetValue(InkEdge.ToVector3());
            hound.CurrentTechnique = hound.Techniques["TechHound"];
            hound.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(tex, drawPos, null,
                Color.White * MathHelper.Clamp(bodyAlpha * 1.25f, 0f, 1f),
                sway, origin, scale, SpriteEffects.None, 0f);

            BeginDefault(spriteBatch);
            gd.Textures[1] = null;
            return false;
        }

        private float NearestLiveDistance() {
            float best = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float d = player.Distance(NPC.Center);
                if (d < best) {
                    best = d;
                }
            }
            return best;
        }
    }
}
