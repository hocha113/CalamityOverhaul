using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 热成像瞄具：命中累积热痕（<see cref="SHPCThermalHeatNPC"/>），热度驱动热成像着色渐变；
    /// 满格白热锁定：光束持续强追踪 + 暴击加成 + 周期灼伤，SHPCModThermal.fx
    /// </summary>
    internal sealed class ThermalOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //热成像火粉
        public override Color TintColor => new(255, 90, 110);

        /// <summary>光束单发热痕（约 6 发满格）</summary>
        private const float HeatPerBeamHit = 17f;
        /// <summary>激光单 tick 热痕（高频低量，约 2 秒满格）</summary>
        private const float HeatPerLaserHit = 4.2f;
        /// <summary>白热锁定转向速率（弧度/次 AI 调用，extraUpdates=2 即每帧 3 次）</summary>
        private const float LockTurnRate = 0.085f;
        /// <summary>白热目标搜索半径（像素）</summary>
        private const float LockSeekRange = 1050f;
        /// <summary>锁定准星锥半角（度），准星偏离超过此角即自然脱锁</summary>
        private const float LockConeHalfDeg = 60f;
        /// <summary>共血体节热痕折减（多节齐中的补偿，防蠕虫锁定过快）</summary>
        private const float WormSegmentHeatMul = 0.6f;

        /// <summary>白热目标每帧缓存（whoAmI），-1 无目标；仅 myPlayer 端读写</summary>
        private int lockTargetCache = -1;
        private uint lockCacheFrame = uint.MaxValue;

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 0.35f;
            ctx.CritAdd += 3;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            ApplyHeat(beam.Projectile, target, HeatPerBeamHit, damageDone);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            ApplyHeat(laser.Projectile, target, HeatPerLaserHit, damageDone);
        }

        /// <summary>白热强锁：光束每次 AI 都朝白热目标强力转向，越过背后（&gt;120°）放弃回折</summary>
        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            NPC target = FindWhiteHotTarget(Main.player[beam.Projectile.owner].Center);
            if (target == null) return;

            Vector2 desired = target.Center - beam.Projectile.Center;
            float diff = MathHelper.WrapAngle(desired.ToRotation() - beam.FlightDirection.ToRotation());
            if (Math.Abs(diff) > MathHelper.ToRadians(120f)) return;
            float turn = MathHelper.Clamp(diff, -LockTurnRate, LockTurnRate);
            beam.SetFlightDirection((beam.FlightDirection.ToRotation() + turn).ToRotationVector2());
        }

        /// <summary>
        /// 本帧准星锥内的己方白热目标，按"到瞄准射线的垂距"取最近；
        /// 准星移出 ±<see cref="LockConeHalfDeg"/>° 锥即自然脱锁，玩家可随时转火
        /// </summary>
        private NPC FindWhiteHotTarget(Vector2 from) {
            if (lockCacheFrame != Main.GameUpdateCount) {
                lockCacheFrame = Main.GameUpdateCount;
                lockTargetCache = -1;
                Vector2 aimDir = (Main.MouseWorld - from).SafeNormalize(Vector2.UnitX);
                float cosCone = MathF.Cos(MathHelper.ToRadians(LockConeHalfDeg));
                float best = float.MaxValue;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || !npc.CanBeChasedBy()) continue;
                    if (!npc.TryGetGlobalNPC(out SHPCThermalHeatNPC heat)) continue;
                    if (!heat.IsWhiteHot || heat.HeatOwner != Main.myPlayer) continue;
                    Vector2 toNpc = npc.Center - from;
                    float dist = toNpc.Length();
                    if (dist > LockSeekRange) continue;
                    //准星锥约束：偏离瞄准方向超过锥半角的白热体不参与锁定
                    if (dist > 1f && Vector2.Dot(toNpc / dist, aimDir) < cosCone) continue;
                    float rayDist = MathF.Abs(Vector2.Dot(toNpc, aimDir.GetNormalVector()));
                    if (rayDist < best) {
                        best = rayDist;
                        lockTargetCache = i;
                    }
                }
            }
            if (lockTargetCache < 0) return null;
            //取用时复验：防同帧死亡、槽位复用、他人白热或白热恰好结束
            NPC cached = Main.npc[lockTargetCache];
            if (!cached.active || !cached.TryGetGlobalNPC(out SHPCThermalHeatNPC h)
                || !h.IsWhiteHot || h.HeatOwner != Main.myPlayer) {
                lockTargetCache = -1;
                return null;
            }
            return cached;
        }

        private static void ApplyHeat(Projectile source, NPC target, float amount, int damageDone) {
            if (source.owner != Main.myPlayer) return;
            //蠕虫体节折算到 realLife 头部：全虫一条热痕，白热演出只挂头部
            NPC carrier = SHPCThermalHeatNPC.ResolveHeatCarrier(target);
            if (carrier.whoAmI != target.whoAmI) {
                amount *= WormSegmentHeatMul;
            }
            if (!carrier.TryGetGlobalNPC(out SHPCThermalHeatNPC heat)) return;
            bool wasWhiteHot = heat.IsWhiteHot;
            heat.AddHeat(carrier, amount, source.owner, damageDone);
            //白热目标被咬中：白炽星火强化"火力锁死"的反馈
            if (wasWhiteHot && Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2CircularEdge(4f, 4f),
                        new Color(255, 245, 220), Main.rand.NextFloat(0.5f, 1.0f))
                        .Configure(true, Main.rand.Next(10, 18));
                }
            }
        }
    }

    /// <summary>
    /// 热痕状态机（InstancePerEntity）：分级热度随时间冷却，满格进入白热锁定；
    /// 全部读写发生在施加者客户端（myPlayer 门），灼伤经 SimpleStrikeNPC 同步
    /// </summary>
    internal sealed class SHPCThermalHeatNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>热痕满值</summary>
        internal const float MaxHeat = 100f;
        /// <summary>受击后冷却延迟帧</summary>
        private const int CoolDelayFrames = 45;
        /// <summary>冷却速率（热痕/帧，约 3 秒满格降空）</summary>
        private const float CoolPerFrame = 0.55f;
        /// <summary>白热锁定持续帧（4.5 秒）</summary>
        private const int WhiteHotDuration = 270;
        /// <summary>白热结束余温，鼓励继续咬同一目标</summary>
        private const float RelockHeat = 30f;
        /// <summary>白热期间光束/激光命中的额外暴击率</summary>
        private const float WhiteHotCritChance = 0.25f;
        /// <summary>灼伤 tick 间隔帧</summary>
        private const int BurnInterval = 30;
        /// <summary>灼伤 tick 伤害 = 记录单发伤害基准 × 此比率</summary>
        private const float BurnTickRatio = 0.4f;
        /// <summary>临界预警阈值（占满值比例）</summary>
        private const float CriticalRatio = 0.75f;

        /// <summary>当前热痕 0~MaxHeat</summary>
        public float Heat;
        /// <summary>施加者玩家索引，完全冷却时重置 -1</summary>
        public int HeatOwner = -1;
        /// <summary>白热剩余帧</summary>
        public int WhiteHotTime;
        /// <summary>白热绘制渐变 0~1</summary>
        public float WhiteHotFade;

        private int coolDelay;
        private int burnTimer;
        private int burnTickDmg;

        //PreDraw 设置、PostDraw 消费的批次切换标志，绘制单线程顺序执行
        private static bool _thermalShaderActive;

        //白热入场/泄压演出同帧节流与余烬全局帧配额，纯客户端视觉状态
        private static uint _fxThrottleFrame;
        private static int _fxThrottleCount;
        private const int EntranceFxPerFrame = 2;
        private static uint _emberBudgetFrame;
        private static int _emberBudgetUsed;
        private const int EmberBudgetPerFrame = 10;

        public bool IsWhiteHot => WhiteHotTime > 0;

        /// <summary>热痕宿主：realLife 共血体节折算到头部，全虫一条热痕</summary>
        internal static NPC ResolveHeatCarrier(NPC npc) {
            if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && npc.realLife != npc.whoAmI) {
                NPC head = Main.npc[npc.realLife];
                if (head.active) return head;
            }
            return npc;
        }

        /// <summary>同帧白热入场/泄压演出配额，混战大量目标同帧触发时最多演 <see cref="EntranceFxPerFrame"/> 组</summary>
        private static bool TryConsumeEntranceFxBudget() {
            if (_fxThrottleFrame != Main.GameUpdateCount) {
                _fxThrottleFrame = Main.GameUpdateCount;
                _fxThrottleCount = 0;
            }
            return ++_fxThrottleCount <= EntranceFxPerFrame;
        }

        /// <summary>体表余烬全局帧配额，防群体/蠕虫场景粒子刷屏</summary>
        private static bool TryConsumeEmberBudget() {
            if (_emberBudgetFrame != Main.GameUpdateCount) {
                _emberBudgetFrame = Main.GameUpdateCount;
                _emberBudgetUsed = 0;
            }
            return ++_emberBudgetUsed <= EmberBudgetPerFrame;
        }

        /// <summary>累积热痕并刷新冷却延迟；跨临界播预警，满格触发白热</summary>
        public void AddHeat(NPC npc, float amount, int owner, int damageDone) {
            if (npc.friendly || npc.dontTakeDamage) return;
            if (IsWhiteHot) return;

            HeatOwner = owner;
            burnTickDmg = Math.Max(burnTickDmg, Math.Max((int)(damageDone * BurnTickRatio), 1));
            coolDelay = CoolDelayFrames;
            float old = Heat;
            Heat = MathF.Min(Heat + amount, MaxHeat);

            if (old < MaxHeat * CriticalRatio && Heat >= MaxHeat * CriticalRatio
                && Main.netMode != NetmodeID.Server) {
                //临界预警：升调滴答
                SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.28f, Pitch = 0.85f }, npc.Center);
            }
            if (Heat >= MaxHeat) {
                EnterWhiteHot(npc);
            }
        }

        private void EnterWhiteHot(NPC npc) {
            WhiteHotTime = WhiteHotDuration;
            burnTimer = 0;
            if (Main.netMode == NetmodeID.Server || !TryConsumeEntranceFxBudget()) return;

            SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.5f, Pitch = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.4f, Pitch = 0.65f }, npc.Center);
            //白炽/热橙双脉冲环
            PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                new Color(255, 240, 220, 0), 0.05f).Configure(0.05f, 0.6f, 20);
            PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                new Color(255, 130, 30, 0), 0.05f).Configure(0.05f, 0.42f, 26);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f) - Vector2.UnitY * Main.rand.NextFloat(1.5f);
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    vel, new Color(255, 245, 225), Main.rand.NextFloat(0.8f, 1.5f))
                    .Configure(new Color(140, 35, 20), Main.rand.Next(26, 46));
            }
            SHPCNaturalFx.Shake(3f);
        }

        private void ExitWhiteHot(NPC npc) {
            Heat = RelockHeat;
            coolDelay = CoolDelayFrames;
            if (Main.netMode == NetmodeID.Server || !TryConsumeEntranceFxBudget()) return;

            //泄压嘶声 + 蒸腾余烬
            SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.3f, Pitch = -0.55f }, npc.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-2.4f, -1f)),
                    new Color(255, 150, 60), Main.rand.NextFloat(0.6f, 1.1f))
                    .Configure(new Color(90, 30, 25), Main.rand.Next(22, 40));
            }
        }

        public override bool PreAI(NPC npc) {
            WhiteHotFade = MathHelper.Lerp(WhiteHotFade, IsWhiteHot ? 1f : 0f, 0.12f);
            if (WhiteHotFade < 0.01f) WhiteHotFade = 0f;

            if (WhiteHotTime > 0) {
                WhiteHotTime--;
                Heat = MaxHeat;
                burnTimer++;
                if (burnTimer >= BurnInterval) {
                    burnTimer = 0;
                    //灼伤只由施加者客户端结算，StrikeNPC 自带同步；服务器/旁观端 HeatOwner 恒非 myPlayer
                    if (burnTickDmg > 0 && HeatOwner == Main.myPlayer) {
                        npc.SimpleStrikeNPC(burnTickDmg, 0, false, 0f, null, false, 0f, true);
                    }
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 3; i++) {
                            PRTLoader.NewParticle<PRT_Spark>(
                                npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                                new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1.2f)),
                                new Color(255, 200, 120), Main.rand.NextFloat(0.4f, 0.9f))
                                .Configure(true, Main.rand.Next(12, 22));
                        }
                    }
                }
                if (WhiteHotTime == 0) {
                    ExitWhiteHot(npc);
                }
            }
            else if (Heat > 0f) {
                if (coolDelay > 0) {
                    coolDelay--;
                }
                else {
                    Heat = MathF.Max(Heat - CoolPerFrame, 0f);
                    if (Heat <= 0f) {
                        //完全冷却：施主与灼伤基准一并归零
                        HeatOwner = -1;
                        burnTickDmg = 0;
                    }
                }
            }

            if (Heat > 0.5f) {
                float hr = Heat / MaxHeat;
                Lighting.AddLight(npc.Center, new Vector3(1f, 0.5f, 0.18f) * (0.12f + hr * 0.3f + WhiteHotFade * 0.45f));
                //体表蒸腾余烬，密度随热度攀升，全局帧配额封顶
                if (Main.netMode != NetmodeID.Server) {
                    int chance = IsWhiteHot ? 3 : (int)MathHelper.Lerp(16f, 6f, hr);
                    if (Main.rand.NextBool(Math.Max(chance, 1)) && TryConsumeEmberBudget()) {
                        Color hot = Color.Lerp(new Color(255, 120, 40), new Color(255, 245, 225), hr);
                        PRTLoader.NewParticle<PRT_SHPCThermalEmber>(
                            npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f),
                            new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-2.2f, -0.8f)),
                            hot, Main.rand.NextFloat(0.5f, 1.1f))
                            .Configure(new Color(110, 30, 22), Main.rand.Next(20, 38));
                    }
                }
            }
            return true;
        }

        /// <summary>白热期间被施加者的光束/激光命中，追加暴击 roll（在命中结算端执行，随 StrikeNPC 同步）；蠕虫任意体节读头部状态</summary>
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            SHPCThermalHeatNPC state = this;
            NPC carrier = ResolveHeatCarrier(npc);
            if (carrier.whoAmI != npc.whoAmI && carrier.TryGetGlobalNPC(out SHPCThermalHeatNPC cs)) {
                state = cs;
            }
            if (!state.IsWhiteHot || state.HeatOwner < 0 || projectile.owner != state.HeatOwner) return;
            if (projectile.ModProjectile is not (CyberTraceBeamProj or CyberPrismLaserProj)) return;
            if (Main.rand.NextFloat() < WhiteHotCritChance) {
                modifiers.SetCrit();
            }
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            float drawHeat = Heat;
            float drawFade = WhiteHotFade;
            if (drawHeat < 0.5f && drawFade < 0.01f) {
                //蠕虫体节借用头部热痕做体表着色，全虫一体受热；锁定十字标仍只挂头部
                NPC carrier = ResolveHeatCarrier(npc);
                if (carrier.whoAmI == npc.whoAmI
                    || !carrier.TryGetGlobalNPC(out SHPCThermalHeatNPC cs)) {
                    return true;
                }
                drawHeat = cs.Heat;
                drawFade = cs.WhiteHotFade;
                if (drawHeat < 0.5f && drawFade < 0.01f) return true;
            }
            Effect shader = EffectLoader.SHPCModThermal?.Value;
            if (shader == null) return true;

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["heat"]?.SetValue(MathHelper.Clamp(drawHeat / MaxHeat, 0f, 1f));
            shader.Parameters["whiteHot"]?.SetValue(drawFade);
            shader.Parameters["seed"]?.SetValue(npc.whoAmI * 0.618f % 1f * 8f);
            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _thermalShaderActive = true;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (_thermalShaderActive) {
                _thermalShaderActive = false;
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.GameViewMatrix.TransformationMatrix);
            }
            if (WhiteHotFade > 0.02f && !npc.IsABestiaryIconDummy) {
                DrawThermalReticle(npc, spriteBatch);
            }
        }

        /// <summary>头顶 FLIR 十字标：四角旋转括弧 + 中心十字 + 右侧温度刻度，A=0 颜色实现加法发光</summary>
        private void DrawThermalReticle(NPC npc, SpriteBatch spriteBatch) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 anchor = npc.Top + new Vector2(0f, -30f) - Main.screenPosition;
            float fade = WhiteHotFade;
            float spin = (float)Main.timeForVisualEffects * 0.045f;
            float breathe = 15f + 2.5f * MathF.Sin((float)Main.timeForVisualEffects * 0.14f);
            Color hotWhite = new Color(255, 250, 235, 0) * fade;
            Color hotOrange = new Color(255, 140, 35, 0) * fade;
            Rectangle px = new(0, 0, 1, 1);
            Vector2 lineOrigin = new(0.5f, 0.5f);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, anchor, null, hotOrange * 0.55f, 0f,
                    glow.Size() * 0.5f, 0.62f, SpriteEffects.None, 0f);
            }

            //外圈四角旋转括弧
            for (int i = 0; i < 4; i++) {
                float ang = spin + MathHelper.PiOver2 * i + MathHelper.PiOver4;
                Vector2 corner = anchor + ang.ToRotationVector2() * breathe;
                Vector2 inward = (anchor - corner).SafeNormalize(Vector2.Zero);
                float arm1 = inward.ToRotation() + MathHelper.PiOver4;
                float arm2 = inward.ToRotation() - MathHelper.PiOver4;
                spriteBatch.Draw(pixel, corner, px, hotOrange, arm1, lineOrigin,
                    new Vector2(8f, 1.6f), SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, corner, px, hotOrange, arm2, lineOrigin,
                    new Vector2(8f, 1.6f), SpriteEffects.None, 0f);
            }

            //中心十字准星
            spriteBatch.Draw(pixel, anchor, px, hotWhite, 0f, lineOrigin,
                new Vector2(14f, 1.4f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, anchor, px, hotWhite, MathHelper.PiOver2, lineOrigin,
                new Vector2(14f, 1.4f), SpriteEffects.None, 0f);

            //右侧温度刻度：满温读数装饰
            for (int i = 0; i < 3; i++) {
                Vector2 tick = anchor + new Vector2(20f, 6f - i * 6f);
                spriteBatch.Draw(pixel, tick, px, hotOrange * (0.5f + i * 0.2f), 0f,
                    new Vector2(0f, 0.5f), new Vector2(4f + i * 2f, 1.2f), SpriteEffects.None, 0f);
            }

            //白炽星闪
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star != null) {
                float flash = 0.8f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.3f);
                spriteBatch.Draw(star, anchor, null, hotWhite * (0.7f * flash), spin * 2f,
                    star.Size() * 0.5f, 0.05f + 0.02f * fade, SpriteEffects.None, 0f);
            }
        }
    }
}
