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
    /// <summary>重型枪管，连中叠凿痕，满四锻入贯体重锤并破甲</summary>
    internal sealed class HeavyBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //重型炮管赤红
        public override Color TintColor => new(220, 40, 60);

        //═════平衡参数═════
        /// <summary>触发重锤所需凿痕数</summary>
        internal const int MaxStacks = 4;
        /// <summary>凿击 ICD 帧，齐射只计一凿</summary>
        internal const int GougeIcdTicks = 10;
        /// <summary>刻痕保持窗口帧，超时清零</summary>
        internal const int GougeWindowTicks = 300;
        /// <summary>重锤伤=窗口最重一凿×此值(落锤强制暴击)</summary>
        internal const float BurstDamageMul = 1.0f;
        /// <summary>破甲持续帧</summary>
        internal const int SunderDurationTicks = 180;
        /// <summary>破甲无视防御比例</summary>
        internal const float SunderArmorPen = 0.5f;

        //锻铁配色，白热→灼铁→暗铁
        internal static readonly Color HotColor = new(255, 236, 200);
        internal static readonly Color EmberColor = new(255, 150, 60);
        internal static readonly Color IronColor = new(120, 55, 35);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.3f;
            ctx.AttackSpeedMul += -0.35f;
            ctx.SpreadMul += -0.45f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            //派生束不回喂
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer) return;
            AddGouge(target, beam.Projectile, damageDone, beam.FlightDirection);
        }

        //同槽互斥，热切换残窗才进激光钩
        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            AddGouge(target, laser.Projectile, damageDone, laser.Projectile.rotation.ToRotationVector2());
        }

        /// <summary>凿击结算，攒满落锤；仅 owner</summary>
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
                //贯体重锤，刻痕归零
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

        /// <summary>凿击反馈，音高爬升+剥片+计数排</summary>
        private static void GougeFeedback(NPC target, Vector2 dir, int stacks) {
            //凿音随层爬升，满层前上膛提示
            SoundEngine.PlaySound(SoundID.Tink with {
                Volume = 0.5f,
                Pitch = -0.35f + stacks * 0.16f
            }, target.Center);
            if (stacks == MaxStacks - 1) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = -0.1f }, target.Center);
            }

            //反溅白热剥片
            Vector2 back = -dir;
            for (int i = 0; i < 5; i++) {
                Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2.5f, 6.5f)
                    + new Vector2(0f, Main.rand.NextFloat(-2.5f, -0.5f));
                PRTLoader.NewParticle<PRT_SHPCHeavySpall>(target.Center + dir * 8f, vel,
                    HotColor, Main.rand.NextFloat(0.5f, 1.0f))
                    ?.Configure(IronColor, Main.rand.Next(20, 38));
            }

            //头顶刻痕计数排
            for (int i = 0; i < stacks; i++) {
                Vector2 pipPos = target.Top + new Vector2((i - (stacks - 1) * 0.5f) * 12f, -16f);
                Color pipCol = i == stacks - 1 ? HotColor : EmberColor;
                PRTLoader.NewParticle<PRT_CyberSquare>(pipPos, new Vector2(0f, -0.2f), pipCol,
                    i == stacks - 1 ? 0.8f : 0.6f)?.Configure(IronColor, 26);
            }
        }
    }

    /// <summary>凿击 per-NPC，刻痕/窗口/ICD/破甲</summary>
    internal sealed class SHPCHeavyGougeNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>凿击刻痕数</summary>
        public int Stacks;
        /// <summary>刻痕窗口剩余帧</summary>
        public int WindowTime;
        /// <summary>凿击 ICD</summary>
        public int IcdTimer;
        /// <summary>窗口最重一击，重锤基数</summary>
        public int StoredDamage;
        /// <summary>破甲剩余帧</summary>
        public int SunderTime;

        public override bool PreAI(NPC npc) {
            if (IcdTimer > 0) IcdTimer--;

            if (WindowTime > 0) {
                WindowTime--;
                if (WindowTime <= 0) {
                    Stacks = 0;
                    StoredDamage = 0;
                }
                //刻痕余烬，层越高越密
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
                        //甲缝漏火
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

    /// <summary>贯体重锤，单体终结+破甲；SHPCModHeavyMaul.fx</summary>
    internal sealed class SHPCHeavyMaulProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 26;
        private const int ExpandFrames = 20;
        private const int DamageWindow = 6;
        private const float MaxRadius = 132f;
        /// <summary>落锤屏震满幅</summary>
        private const float MaulShake = 5f;
        /// <summary>屏震衰减距离px</summary>
        private const float ShakeFalloffDist = 900f;

        private float StrikeRotation => Projectile.ai[0];
        private int MarkedIndex => (int)Projectile.ai[1];
        private int MarkedType => (int)Projectile.ai[2];
        private float Progress => MathHelper.Clamp((Lifetime - Projectile.timeLeft) / (float)ExpandFrames, 0f, 1f);

        /// <summary>标记目标有效，whoAmI+type 双校验防槽复用</summary>
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

        /// <summary>只砸被凿满的那一个</summary>
        public override bool? CanHitNPC(NPC target)
            => target.whoAmI == MarkedIndex && target.type == MarkedType ? null : false;

        /// <summary>伤害窗口仅落锤前几帧</summary>
        public override bool? CanDamage() => Projectile.timeLeft > Lifetime - DamageWindow ? null : false;

        public override void AI() {
            //冲击环钉目标
            if (TryGetMarkedNPC(out NPC npc)) {
                Projectile.Center = npc.Center;
            }

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    //落锤双层音
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.95f, Pitch = -0.3f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.65f, Pitch = -0.45f }, Projectile.Center);
                    SpawnImpactParticles();
                    //屏震距衰
                    float falloff = 1f - MathHelper.Clamp(Main.LocalPlayer.Distance(Projectile.Center) / ShakeFalloffDist, 0f, 1f);
                    SHPCNaturalFx.Shake(MaulShake * falloff);
                }
            }

            Lighting.AddLight(Projectile.Center, HeavyBarrelModule.EmberColor.ToVector3() * 0.9f * (1f - Progress));
        }

        private void SpawnImpactParticles() {
            Vector2 dir = StrikeRotation.ToRotationVector2();
            //贯穿向剥片喷泉
            for (int i = 0; i < 14; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.85f, 0.85f)) * Main.rand.NextFloat(4f, 12f)
                    + new Vector2(0f, Main.rand.NextFloat(-3f, -1f));
                PRTLoader.NewParticle<PRT_SHPCHeavySpall>(Projectile.Center, vel,
                    HeavyBarrelModule.HotColor, Main.rand.NextFloat(0.7f, 1.3f))
                    ?.Configure(HeavyBarrelModule.IronColor, Main.rand.Next(26, 46));
            }
            //反冲侧碎屑
            for (int i = 0; i < 6; i++) {
                Vector2 vel = (-dir).RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_SHPCHeavySpall>(Projectile.Center, vel,
                    HeavyBarrelModule.EmberColor, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(HeavyBarrelModule.IronColor, Main.rand.Next(18, 32));
            }
            //双层脉冲环
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 236, 200, 0), 0.05f)?.Configure(0.05f, 0.5f, 20);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 150, 60, 0), 0.05f)?.Configure(0.05f, 0.38f, 26);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //强制暴击，击退沿贯穿
            modifiers.SetCrit();
            modifiers.HitDirectionOverride = StrikeRotation.ToRotationVector2().X >= 0f ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //落锤破甲
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
            Texture2D canvas = VaultAsset.placeholder2?.Value;
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
