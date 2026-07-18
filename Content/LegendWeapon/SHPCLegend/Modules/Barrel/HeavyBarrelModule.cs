using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>重型枪管：连续命中同一目标叠凿击刻痕，满四道锻入贯体重锤爆击并砸碎护甲</summary>
    internal sealed class HeavyBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //重型炮管赤红
        public override Color TintColor => new(220, 40, 60);

        //═════ 可调参数（平衡位） ═════
        /// <summary>触发重锤所需凿击刻痕数</summary>
        internal const int MaxStacks = 4;
        /// <summary>凿击结算内置冷却（帧），一次多束齐射只计一凿，保持"凿-凿-凿"节奏</summary>
        internal const int GougeIcdTicks = 10;
        /// <summary>刻痕保持窗口（帧），窗口内未续凿则全部消退</summary>
        internal const int GougeWindowTicks = 300;
        /// <summary>重锤伤害 = 窗口内最重一凿 × 此倍率（落锤强制爆击，实际约两倍于面值，故保守取 1）</summary>
        internal const float BurstDamageMul = 1.0f;
        /// <summary>破甲持续（帧）</summary>
        internal const int SunderDurationTicks = 180;
        /// <summary>破甲期间无视防御比例</summary>
        internal const float SunderArmorPen = 0.5f;

        //锻铁配色：白热 → 灼铁橙 → 暗铁
        internal static readonly Color HotColor = new(255, 236, 200);
        internal static readonly Color EmberColor = new(255, 150, 60);
        internal static readonly Color IronColor = new(120, 55, 35);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.3f;
            ctx.AttackSpeedMul += -0.35f;
            ctx.SpreadMul += -0.45f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            //全局约定：派生束（链跳/分裂/齐射子束等）不回喂任何模块机制
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer) return;
            AddGouge(target, beam.Projectile, damageDone, beam.FlightDirection);
        }

        //防御性代码：LaserMode 只能由同槽 Barrel 模块开启，正常装配下激光模式与本模块互斥，
        //此路径仅在模块热切换残窗内可达，保留以保证残窗行为一致
        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            AddGouge(target, laser.Projectile, damageDone, laser.Projectile.rotation.ToRotationVector2());
        }

        /// <summary>凿击结算：叠刻痕、播报节奏反馈，攒满触发贯体重锤；仅拥有者客户端调用</summary>
        private static void AddGouge(NPC target, Projectile source, int damageDone, Vector2 dir) {
            if (!target.active || !target.TryGetGlobalNPC(out SHPCHeavyGougeNPC gouge)) return;
            if (gouge.IcdTimer > 0) return;

            gouge.IcdTimer = GougeIcdTicks;
            gouge.WindowTime = GougeWindowTicks;
            gouge.Stacks++;
            gouge.StoredDamage = Math.Max(gouge.StoredDamage, damageDone);

            if (Main.netMode != NetmodeID.Server) {
                GougeFeedback(target, dir, gouge.Stacks);
            }

            if (gouge.Stacks >= MaxStacks) {
                //轰：贯体重锤，之后该目标从零重凿
                int burst = Math.Max((int)(gouge.StoredDamage * BurstDamageMul), 1);
                Projectile.NewProjectile(source.GetSource_FromThis(),
                    target.Center, Vector2.Zero,
                    ModContent.ProjectileType<SHPCHeavyMaulProj>(),
                    burst, 8f, source.owner,
                    ai0: dir.ToRotation(), ai1: target.whoAmI, ai2: target.type);
                gouge.Stacks = 0;
                gouge.StoredDamage = 0;
                gouge.WindowTime = 0;
            }
        }

        /// <summary>每凿的可读反馈：音高爬升的金属凿音、飞溅剥片与刻痕计数排</summary>
        private static void GougeFeedback(NPC target, Vector2 dir, int stacks) {
            //凿音音高随刻痕爬升，满层前一凿换上膛提示，形成听得见的节奏
            SoundEngine.PlaySound(SoundID.Tink with {
                Volume = 0.5f,
                Pitch = -0.35f + stacks * 0.16f
            }, target.Center);
            if (stacks == MaxStacks - 1) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = -0.1f }, target.Center);
            }

            //凿点反溅的白热剥片：逆着来向弹出，受重力坠落
            Vector2 back = -dir;
            for (int i = 0; i < 5; i++) {
                Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2.5f, 6.5f)
                    + new Vector2(0f, Main.rand.NextFloat(-2.5f, -0.5f));
                PRTLoader.NewParticle<PRT_SHPCHeavySpall>(target.Center + dir * 8f, vel,
                    HotColor, Main.rand.NextFloat(0.5f, 1.0f))
                    ?.Configure(IronColor, Main.rand.Next(20, 38));
            }

            //目标头顶的刻痕计数排：已有几凿亮几格，最新一格白热
            for (int i = 0; i < stacks; i++) {
                Vector2 pipPos = target.Top + new Vector2((i - (stacks - 1) * 0.5f) * 12f, -16f);
                Color pipCol = i == stacks - 1 ? HotColor : EmberColor;
                PRTLoader.NewParticle<PRT_CyberSquare>(pipPos, new Vector2(0f, -0.2f), pipCol,
                    i == stacks - 1 ? 0.8f : 0.6f)?.Configure(IronColor, 26);
            }
        }
    }

    /// <summary>凿击 per-NPC 状态：刻痕计数与窗口、结算冷却、破甲计时；随 NPC 实例自动回收</summary>
    internal sealed class SHPCHeavyGougeNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>当前凿击刻痕数</summary>
        public int Stacks;
        /// <summary>刻痕保持窗口剩余帧数，归零全部消退</summary>
        public int WindowTime;
        /// <summary>凿击结算内置冷却</summary>
        public int IcdTimer;
        /// <summary>窗口内最重一击的实际伤害，作为重锤基数</summary>
        public int StoredDamage;
        /// <summary>破甲剩余帧数</summary>
        public int SunderTime;

        public override bool PreAI(NPC npc) {
            if (IcdTimer > 0) IcdTimer--;

            if (WindowTime > 0) {
                WindowTime--;
                if (WindowTime <= 0) {
                    Stacks = 0;
                    StoredDamage = 0;
                }
                //刻痕余烬：层数越多迸得越密，不看面板也能感知进度
                else if (Stacks > 0 && Main.netMode != NetmodeID.Server
                    && Main.rand.NextBool(Math.Max(12 - Stacks * 3, 3))) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
                    PRTLoader.NewParticle<PRT_Spark>(pos, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.4f, -0.4f)),
                        HeavyBarrelModule.EmberColor, Main.rand.NextFloat(0.35f, 0.7f))
                        ?.Configure(true, Main.rand.Next(10, 20));
                }
            }

            if (SunderTime > 0) {
                SunderTime--;
                if (Main.netMode != NetmodeID.Server) {
                    if (Main.rand.NextBool(4)) {
                        //甲缝漏火：破甲期间裂口持续渗出暗铁碎屑与火线
                        Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                        PRTLoader.NewParticle<PRT_CyberSquare>(pos, Main.rand.NextVector2Circular(1f, 1f),
                            HeavyBarrelModule.IronColor, Main.rand.NextFloat(0.4f, 0.9f))
                            ?.Configure(HeavyBarrelModule.EmberColor, Main.rand.Next(10, 20));
                    }
                    float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f);
                    Lighting.AddLight(npc.Center, HeavyBarrelModule.EmberColor.ToVector3() * 0.35f * pulse);
                }
            }
            return true;
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (SunderTime > 0) {
                modifiers.ScalingArmorPenetration += HeavyBarrelModule.SunderArmorPen;
            }
        }
    }

    /// <summary>贯体重锤：锁定单体的凿击终结爆击，落锤施加破甲；SHPCModHeavyMaul.fx</summary>
    internal sealed class SHPCHeavyMaulProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int Lifetime = 26;
        private const int ExpandFrames = 20;
        private const int DamageWindow = 6;
        private const float MaxRadius = 132f;
        /// <summary>落锤屏震满幅幅度</summary>
        private const float MaulShake = 5f;
        /// <summary>屏震衰减距离（像素），本地玩家距爆点超过此距离不再震动</summary>
        private const float ShakeFalloffDist = 900f;

        private float StrikeRotation => Projectile.ai[0];
        private int MarkedIndex => (int)Projectile.ai[1];
        private int MarkedType => (int)Projectile.ai[2];
        private float Progress => MathHelper.Clamp((Lifetime - Projectile.timeLeft) / (float)ExpandFrames, 0f, 1f);

        /// <summary>标记目标仍有效：槽位未被复用（whoAmI 可能在目标死亡后指向新 NPC，用 type 双重校验）</summary>
        private bool TryGetMarkedNPC(out NPC npc) {
            npc = null;
            if (MarkedIndex < 0 || MarkedIndex >= Main.maxNPCs) return false;
            NPC candidate = Main.npc[MarkedIndex];
            if (!candidate.active || candidate.type != MarkedType) return false;
            npc = candidate;
            return true;
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一记重锤只结算一次
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>重锤只砸被凿满的那一个目标，与范围爆破类改件明确区分</summary>
        public override bool? CanHitNPC(NPC target)
            => target.whoAmI == MarkedIndex && target.type == MarkedType ? null : false;

        /// <summary>伤害窗口只在落锤前几帧，其后是纯余波演出</summary>
        public override bool? CanDamage() => Projectile.timeLeft > Lifetime - DamageWindow ? null : false;

        public override void AI() {
            //贯体：冲击环钉在目标身上随其移动
            if (TryGetMarkedNPC(out NPC npc)) {
                Projectile.Center = npc.Center;
            }

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    //厚重落锤：闷响垫底 + 铁砧金属高频
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.95f, Pitch = -0.3f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.65f, Pitch = -0.45f }, Projectile.Center);
                    SpawnImpactParticles();
                    //屏震随本地玩家与爆点距离线性衰减，远处旁观者不吃满幅震动
                    float falloff = 1f - MathHelper.Clamp(Main.LocalPlayer.Distance(Projectile.Center) / ShakeFalloffDist, 0f, 1f);
                    SHPCNaturalFx.Shake(MaulShake * falloff);
                }
            }

            Lighting.AddLight(Projectile.Center, HeavyBarrelModule.EmberColor.ToVector3() * 0.9f * (1f - Progress));
        }

        private void SpawnImpactParticles() {
            Vector2 dir = StrikeRotation.ToRotationVector2();
            //贯穿向白热剥片喷泉：沿打击方向锥形喷出，重力坠落
            for (int i = 0; i < 14; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.85f, 0.85f)) * Main.rand.NextFloat(4f, 12f)
                    + new Vector2(0f, Main.rand.NextFloat(-3f, -1f));
                PRTLoader.NewParticle<PRT_SHPCHeavySpall>(Projectile.Center, vel,
                    HeavyBarrelModule.HotColor, Main.rand.NextFloat(0.7f, 1.3f))
                    ?.Configure(HeavyBarrelModule.IronColor, Main.rand.Next(26, 46));
            }
            //反冲侧少量碎屑，保证爆点四周都有金属质感
            for (int i = 0; i < 6; i++) {
                Vector2 vel = (-dir).RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_SHPCHeavySpall>(Projectile.Center, vel,
                    HeavyBarrelModule.EmberColor, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(HeavyBarrelModule.IronColor, Main.rand.Next(18, 32));
            }
            //双层脉冲环：白热快环 + 灼铁慢环
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 236, 200, 0), 0.05f)?.Configure(0.05f, 0.5f, 20);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 150, 60, 0), 0.05f)?.Configure(0.05f, 0.38f, 26);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //贯体重锤必定爆击，击退沿贯穿方向
            modifiers.SetCrit();
            modifiers.HitDirectionOverride = StrikeRotation.ToRotationVector2().X >= 0f ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //落锤即破甲：为下一轮凿击垫高收益，闭合循环
            if (target.TryGetGlobalNPC(out SHPCHeavyGougeNPC gouge)) {
                gouge.SunderTime = Math.Max(gouge.SunderTime, HeavyBarrelModule.SunderDurationTicks);
            }
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2CircularEdge(5f, 5f),
                    HeavyBarrelModule.EmberColor, Main.rand.NextFloat(0.6f, 1.1f))
                    ?.Configure(true, Main.rand.Next(12, 22));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.SHPCModHeavyMaul?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)Lifetime * 1.7f, 0f, 1f);
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["progress"]?.SetValue(MathHelper.Lerp(0.06f, 0.95f, Progress));
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["coreColor"]?.SetValue(HeavyBarrelModule.HotColor.ToVector3());
            shader.Parameters["ringColor"]?.SetValue(HeavyBarrelModule.EmberColor.ToVector3());
            shader.Parameters["ironColor"]?.SetValue(HeavyBarrelModule.IronColor.ToVector3());

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawSize = MaxRadius * 2.4f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                StrikeRotation, canvas.Size() * 0.5f,
                new Vector2(drawSize, drawSize), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
