using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Stealth;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 井手：村井里的湿黑巨手，锚点听觉伏击者（垂直镜像 OldNetLurkerICE 的四态语法）。<br/>
    /// 慢走过井台绝对安全；井口半径内的动静（奔跑/落地/开火/噪声场事件）累计入警觉，
    /// 过阈即 14t 前摇（水声+怨雾，公平阀）→ 8t 上冲 52px 抓一口（唯一接触伤窗）→
    /// 30t 缩回（可打，处决窗）→ 冷却 480t。击杀=本井会话静默（导演井表）。<br/>
    /// 联机：ai[0..1]=井口锚（世界 px，导演布防写入）ai[2]=状态 ai[3]=状态计时；
    /// 听觉裁决只在服务器（警觉为实例字段不入同步），时序转移各端确定重放、
    /// 服务器 netUpdate 校正；音效/粒子走状态沿。<br/>
    /// 视觉：原版 SkeletronHand 单帧（指尖朝上）过 KikasaHound.fx uMode=1 湿墨链，
    /// 零新 shader；强袭叠 3 帧速度残影（Warden 同款）
    /// </summary>
    internal class WellHandYokai : KiyumeYokaiNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[2] 状态位（ai 布局按计划书 §2.2 冻结：[0..1]=井口锚，绕开基类默认槽位命名）
        private const int StateDormant = 0;
        private const int StateArm = 1;
        private const int StateStrike = 2;
        private const int StateReel = 3;

        //强袭/回收速度：行程与用时之商（数值全在 KiyumeYokaiMetrics，此处只做换算）
        private const float StrikeSpeed =
            KiyumeYokaiMetrics.WellStrikeRise / KiyumeYokaiMetrics.WellStrikeTicks;
        private const float ReelSpeed =
            KiyumeYokaiMetrics.WellStrikeRise / KiyumeYokaiMetrics.WellReelTicks;
        //并联响度积分：满响一档每拍进 阈值/回退累计时长（金标：满速冲刺 20t 惊井）
        private const float LoudGainPerTick =
            KiyumeYokaiMetrics.WellHearThreshold / KiyumeYokaiMetrics.WellFallbackChargeTicks;
        //绘制缩放（§2.2 冻结视觉值）
        private const float HandScale = 1.15f;

        //湿墨轮廓缘色（§2.2 冻结值）与怨雾色
        private static readonly Color InkEdge = new(60, 18, 20);
        private static readonly Color MistTint = new(96, 78, 84);

        /// <summary>ai[0]：井口锚世界 X（导演布防写入）</summary>
        private ref float AnchorX => ref NPC.ai[0];
        /// <summary>ai[1]：井口锚世界 Y</summary>
        private ref float AnchorY => ref NPC.ai[1];
        /// <summary>ai[2]：状态（休眠/前摇/强袭/回收）</summary>
        private ref float HandState => ref NPC.ai[2];
        /// <summary>ai[3]：状态计时（休眠期=再触发冷却倒数，前摇倒数，强袭/回收递增）</summary>
        private ref float HandTimer => ref NPC.ai[3];

        //警觉累计（服务器实例字段，裁决量不入同步；客户端只从 ai 状态重放表现）
        private float alert;

        /// <summary>井口锚（世界 px）</summary>
        private Vector2 MouthPos => new(AnchorX, AnchorY);
        /// <summary>藏身位：井口下两格（休眠/前摇驻位，也是强袭起点与回收终点）</summary>
        private Vector2 HidePos => new(AnchorX, AnchorY + KiyumeYokaiMetrics.WellHideRowsBelow * 16f);

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        protected override void SetYokaiDefaults() {
            NPC.width = 30;
            NPC.height = 44;
            //平时零伤害，仅强袭窗口设回（Lurker 门控惯例）
            NPC.damage = 0;
            NPC.defense = KiyumeYokaiMetrics.WellDefense;
            NPC.lifeMax = KiyumeYokaiMetrics.WellLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            //强袭要吃 collideY：玩家站井口正上封盖时撞顶即转回收
            NPC.noTileCollide = false;
            NPC.npcSlots = 0.25f;
            NPC.HitSound = SoundID.NPCHit1 with { Pitch = -0.35f };
            NPC.DeathSound = SoundID.NPCDeath1 with { Pitch = -0.5f };
            //出生即藏井壁之内（打不着，合理化休眠无敌）
            NPC.dontTakeDamage = true;
        }

        protected override void YokaiAI() {
            //出生透明度自愈：全接管绘制，NPC.alpha 恒收敛 0，可见度由状态透明度显式给
            HealAlpha(0);
            //锚点兜底：导演正常写井口锚，调试裸生成用当前位置自锚（镜像 Lurker）
            if (AnchorX <= 0f) {
                AnchorX = NPC.Center.X;
                AnchorY = NPC.Center.Y - KiyumeYokaiMetrics.WellHideRowsBelow * 16f;
            }

            bool edge = HandStateEdge();
            switch ((int)HandState) {
                case StateArm:
                    UpdateArm(edge);
                    break;
                case StateStrike:
                    UpdateStrike(edge);
                    break;
                case StateReel:
                    UpdateReel();
                    break;
                default:
                    UpdateDormant();
                    break;
            }

            //现身期微光：贴脸伏击的可读性兜底（休眠不发光，伏击者不自曝）
            if ((int)HandState != StateDormant) {
                Lighting.AddLight(NPC.Center, 0.10f, 0.03f, 0.03f);
            }
        }

        //状态沿：镜像基类 StateEdge，槽位改挂 ai[2]（本怪 ai 布局自定义，基类默认命名不适用）；
        //迟入端首帧也得一次沿（localAI[2] 初值 0 ≠ 任何状态+1）
        private bool HandStateEdge() {
            if ((int)NPC.localAI[2] == (int)HandState + 1) {
                return false;
            }
            NPC.localAI[2] = (int)HandState + 1;
            NPC.localAI[3] = 0f;
            return true;
        }

        //状态转移：听觉触发只在服务器；时序转移各端确定重放（免强袭迟半拍），
        //服务器仍置 netUpdate 补发权威快照校正漂移
        private void SetHandState(int state, float timer = 0f) {
            HandState = state;
            HandTimer = timer;
            NPC.netUpdate = true;
        }

        //──── 休眠：藏井口下两格，听觉积分 ────

        private void UpdateDormant() {
            NPC.damage = 0;
            NPC.dontTakeDamage = true;
            NPC.noTileCollide = false;
            NPC.velocity = Vector2.Zero;
            NPC.Center = HidePos;
            AmbientMurmur();

            //再触发冷却（各端确定重放）
            if (HandTimer > 0f) {
                HandTimer--;
                return;
            }
            //听觉裁决只在权威端：噪声场客户端恒 0，警觉是服务器实例量
            if (VaultUtils.isClient) {
                return;
            }
            float gain = HearingGain();
            if (gain > 0f) {
                alert += gain;
            }
            else {
                //静场衰减借恶犬巡逻档（同一套听觉物理的冷却率）
                alert = MathF.Max(0f, alert - KiyumeHoundMetrics.DecayPatrol);
            }
            if (alert >= CurrentAlertThreshold()) {
                alert = 0f;
                SetHandState(StateArm, KiyumeYokaiMetrics.WellArmTicks);
            }
        }

        /// <summary>
        /// 单帧听觉增益（服务器）：正式口=噪声场 NoiseAt(井口)（事件噪声，裁决11）；
        /// 并联口=玩家移动响度。StealthPlayer 的速度/脉冲不经 ReportNoise 入噪声场
        /// （实查 ReportNoise 零调用方），故井口为收听点逐玩家复刻 SoundExposure 的响度项：
        /// 对玩家取 max 不求和，防环境项重复计入（P2 报告口径）；
        /// 井喉朝天收声，不做隔实心闷响折减（手体藏地下，LOS 折减会系统性漏听）
        /// </summary>
        private float HearingGain() {
            Vector2 mouth = MouthPos;
            float gain = KiyumeStealthSense.NoiseAt(mouth, KiyumeYokaiMetrics.WellHearRadius)
                * KiyumeHoundMetrics.GainHear;
            float loudest = 0f;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float dist = Vector2.Distance(player.Center, mouth);
                if (dist >= KiyumeYokaiMetrics.WellHearRadius) {
                    continue;
                }
                KiyumeStealthPlayer state = player.GetModPlayer<KiyumeStealthPlayer>();
                //慢=安全：速度门下移动响度恒零；落地/开火脉冲不受速度门约束
                float level = player.velocity.Length() > KiyumeYokaiMetrics.WellFallbackSpeedGate
                    ? 1f : 0f;
                level += state.LandPulse * KiyumeHoundMetrics.LandImpulse
                    + state.FirePulse * KiyumeHoundMetrics.WeaponImpulse;
                if (level <= 0f) {
                    continue;
                }
                loudest = MathF.Max(loudest,
                    level * (1f - dist / KiyumeYokaiMetrics.WellHearRadius));
            }
            return gain + loudest * LoudGainPerTick;
        }

        //警觉阈：涨潮增益读服务器权威潮汐钟（P2-A 已权威化），TideGateEnabled 总开关门控
        private static float CurrentAlertThreshold() {
            float threshold = KiyumeYokaiMetrics.WellHearThreshold;
            if (KiyumeYokaiMetrics.TideGateEnabled
                && KiyumeFogTide.LineWorldY <= KiyumeYokaiMetrics.WellFloodGate * 16f) {
                threshold *= KiyumeYokaiMetrics.WellFloodAlertMul;
            }
            return threshold;
        }

        //休眠氛围：井底偶尔一声水响（纯本地表现，与状态机无涉，各端独立；
        //手死井空，这声环境响也随实体一起消失，静默自然成立）
        private void AmbientMurmur() {
            if (Main.dedServ || !Main.rand.NextBool(720)) {
                return;
            }
            Player viewer = Main.LocalPlayer;
            if (viewer?.active != true || Vector2.Distance(viewer.Center, MouthPos) > 560f) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = -0.85f }, MouthPos);
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                MouthPos + new Vector2(Main.rand.NextFloat(-6f, 6f), 4f),
                new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.8f)),
                MistTint * 0.45f, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(24, 40));
        }

        //──── 前摇：14t 公平阀，井口水声 + 怨雾上涌 ────

        private void UpdateArm(bool edge) {
            NPC.damage = 0;
            //前摇仍在井壁之内，维持不可打
            NPC.dontTakeDamage = true;
            NPC.velocity = Vector2.Zero;
            //蓄力颤抖：绕藏身位横向微抖（Lurker 同款语法）
            NPC.Center = HidePos + new Vector2(MathF.Sin(HandTimer * 2.9f + Seed) * 1.5f, 0f);

            if (edge && !Main.dedServ) {
                SoundEngine.PlaySound(
                    SoundID.SplashWeak with { Volume = 0.85f, Pitch = -0.6f }, MouthPos);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        MouthPos + new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-2f, 10f)),
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.8f, 1.6f)),
                        MistTint * 0.7f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(26, 44));
                }
            }
            if (--HandTimer <= 0f) {
                SetHandState(StateStrike);
                NPC.velocity = new Vector2(0f, -StrikeSpeed);
            }
        }

        //──── 强袭：8t 沿井轴上冲，唯一接触伤窗 ────

        private void UpdateStrike(bool edge) {
            //各端每拍从状态重放伤害与可打性，不依赖同步包字段
            NPC.damage = KiyumeYokaiMetrics.WellBiteDamage;
            NPC.dontTakeDamage = false;
            NPC.noTileCollide = false;

            if (edge && !Main.dedServ) {
                //出水拍：短水花 + 两缕怨雾（无声出水读假）
                SoundEngine.PlaySound(
                    SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.1f }, MouthPos);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        MouthPos + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(1.2f, 2f)),
                        MistTint * 0.6f, Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(18, 30));
                }
            }

            //撞地判定必须在覆写速度之前：collideY 由上帧移动结算写入，
            //覆写后再读永远是假（Lurker 审查实锤的死码教训）；玩家站井口正上封盖 → 撞顶即回收
            bool blocked = NPC.collideY;
            bool spent = HidePos.Y - NPC.Center.Y >= KiyumeYokaiMetrics.WellStrikeRise;
            if (blocked || spent || ++HandTimer > KiyumeYokaiMetrics.WellStrikeTicks) {
                BeginReel();
                return;
            }
            NPC.velocity = new Vector2(0f, -StrikeSpeed);
            ServerSyncPacer();
        }

        private void BeginReel() {
            SetHandState(StateReel);
            NPC.damage = 0;
            //回收穿行免卡台沿（Lurker 同款）；全程可打=处决窗口
            NPC.noTileCollide = true;
        }

        //──── 回收：30t 缩回藏身位，可打的处决窗 ────

        private void UpdateReel() {
            NPC.damage = 0;
            NPC.dontTakeDamage = false;
            NPC.noTileCollide = true;
            Vector2 toHide = HidePos - NPC.Center;
            if (toHide.Length() < 4f || ++HandTimer > KiyumeYokaiMetrics.WellReelTicks) {
                NPC.Center = HidePos;
                NPC.velocity = Vector2.Zero;
                NPC.noTileCollide = false;
                SetHandState(StateDormant, KiyumeYokaiMetrics.WellCooldown);
                return;
            }
            NPC.velocity = toHide.SafeNormalize(Vector2.UnitY) * ReelSpeed;
            ServerSyncPacer();
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            //迟缓走原版被打端本地语义（无额外包）
            target.AddBuff(BuffID.Slow, KiyumeYokaiMetrics.WellBiteSlowTicks);
            //单口即回收（不做多目标）：MP 下本钩只在被打端跑，不夺权改状态，8t 窗自然收口
            if (Main.netMode == NetmodeID.SinglePlayer && (int)HandState == StateStrike) {
                BeginReel();
            }
        }

        public override void OnKill() {
            //击杀=本井会话静默（服务器侧井表，裁决量客户端无需知道）
            KiyumeHauntDirector.MarkWellSilenced(
                new Point((int)(AnchorX / 16f), (int)(AnchorY / 16f)));
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            if (NPC.life <= 0) {
                //死亡：井口冒最后一串怨雾 + 一声闷水响
                SoundEngine.PlaySound(
                    SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.8f }, MouthPos);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        MouthPos + new Vector2(Main.rand.NextFloat(-10f, 10f),
                            Main.rand.NextFloat(-6f, 6f) - i * 6f),
                        new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.6f, 1.3f)),
                        MistTint * 0.75f, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(40, 70));
                }
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + Main.rand.NextVector2Circular(12f, 14f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(0.4f, 1f), -Main.rand.NextFloat(0.3f, 0.8f)),
                    MistTint * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(18, 30));
            }
        }

        //──── 绘制：SkeletronHand 单帧（指尖朝上）过 KikasaHound.fx uMode=1 湿墨链 ────
        //全接管绘制：透明度全程显式给值（状态包络），不依赖 NPC.alpha

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.SkeletronHand);
            Texture2D tex = TextureAssets.Npc[NPCID.SkeletronHand].Value;
            if (tex == null) {
                return false;
            }

            int state = (int)HandState;
            //状态透明度：休眠 0.10 井底暗影，前摇渐显，强袭全亮，回收微降
            float bodyAlpha = state switch {
                StateArm => MathHelper.Lerp(0.12f, 0.8f,
                    1f - HandTimer / KiyumeYokaiMetrics.WellArmTicks),
                StateStrike => 1f,
                StateReel => 0.85f,
                _ => 0.10f,
            };
            //出井进度：藏身位为 0，升满行程为 1；uDissolve 0.3→0（§2.2 冻结）
            float emerge = MathHelper.Clamp(
                (HidePos.Y - NPC.Center.Y) / KiyumeYokaiMetrics.WellStrikeRise, 0f, 1f);
            float dissolve = MathHelper.Lerp(0.3f, 0f, emerge);

            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = new(tex.Width * 0.5f, tex.Height * 0.5f);

            //强袭速度残影：3 帧拖影（Warden 同款），画在本体之前=层序在下
            float vel = NPC.velocity.Length();
            if (state == StateStrike && vel > 3f) {
                for (int g = 1; g <= 3; g++) {
                    Vector2 back = drawPos - NPC.velocity * (g * 1.9f);
                    spriteBatch.Draw(tex, back, null, InkEdge * (bodyAlpha * (0.30f - g * 0.08f)),
                        0f, origin, new Vector2(HandScale, HandScale * (1f + vel * 0.03f)),
                        SpriteEffects.None, 0f);
                }
            }

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (hound == null || noise == null) {
                //shader 缺编：湿墨近黑剪影回退（HoundShade 同款）
                spriteBatch.Draw(tex, drawPos, null, new Color(10, 5, 8) * (bodyAlpha * 0.9f),
                    0f, origin, HandScale, SpriteEffects.None, 0f);
                return false;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //参数链照抄 KiyumeHoundShade 实体态：单帧全图 uUvRect，手无目 uEyeGlow=0
            hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            hound.Parameters["uSeed"]?.SetValue(Seed);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)tex.Height);
            hound.Parameters["uFlipH"]?.SetValue(0f);
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
                0f, origin, HandScale, SpriteEffects.None, 0f);

            BeginDefault(spriteBatch);
            gd.Textures[1] = null;
            return false;
        }
    }
}
