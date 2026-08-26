using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenBee
{
    /// <summary>
    /// 蜂涡本体：绑定目标NPC的玩家弹幕。<br/>
    /// ai[0]=目标槽位 ai[1]=目标类型(槽位复用校验) ai[2]=消散旗，三者全走同步；
    /// 目标死亡→owner就近转移(改ai+netUpdate)，无处可去→消散。<br/>
    /// 伤害每帧按owner加成重算，命中在owner端解算(原版路径)；
    /// 减速走 <see cref="SwarmVortexDebuff"/> 骑原版NPCbuff同步。<br/>
    /// localAI[0]=本地帧龄(迟到端快进跳过成形拍)，localAI[1]=旋涡累计角(渲染用)
    /// </summary>
    internal class SwarmVortexProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>成形收拢拍</summary>
        internal const int FormTicks = 24;
        /// <summary>消散拍</summary>
        internal const int DissolveTicks = 22;

        private int BoundIndex => (int)Projectile.ai[0];
        private int BoundType => (int)Projectile.ai[1];
        private bool Dissolving => Projectile.ai[2] == 1f;
        private float Age => Projectile.localAI[0];
        private bool Formed => Age > FormTicks && !Dissolving;

        //上帧绑定槽位，远端借此侦测转移拍
        private int lastBound = -1;
        //转移流束视觉拍
        private int transferBeat;

        public override LocalizedText DisplayName => this.GetLocalization("DisplayName", () => "蜂涡");

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = SwarmVortexBeacon.VortexBaseTicks;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = SwarmVortexBeacon.VortexHitInterval;
            Projectile.netImportant = true;
        }

        public override void AI() {
            //迟到端(中途入场/晚收包)直接跳过成形拍，不重播收拢
            if (Projectile.localAI[0] == 0f
                && Projectile.timeLeft < SwarmVortexBeacon.VortexBaseTicks - FormTicks) {
                Projectile.localAI[0] = FormTicks + 1;
            }
            Projectile.localAI[0]++;
            //差速旋转累计角
            Projectile.localAI[1] += 0.085f + (Formed ? 0.035f : 0f);

            NPC target = ValidTarget();

            //owner裁决：转移或消散(远端只按ai渲染，等包)
            if (Projectile.owner == Main.myPlayer && target == null && !Dissolving) {
                NPC next = FindTransferTarget();
                if (next != null) {
                    Projectile.ai[0] = next.whoAmI;
                    Projectile.ai[1] = next.type;
                    Projectile.netUpdate = true;
                    target = next;
                }
                else {
                    Projectile.ai[2] = 1f;
                    Projectile.timeLeft = Math.Min(Projectile.timeLeft, DissolveTicks);
                    Projectile.netUpdate = true;
                }
            }

            //转移拍：各端本地由绑定槽位变化沿侦测
            if (lastBound >= 0 && BoundIndex != lastBound && !Dissolving) {
                transferBeat = 14;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
                }
            }
            lastBound = BoundIndex;
            if (transferBeat > 0) {
                transferBeat--;
            }

            if (target != null) {
                //贴附目标：转移时0.55插值读作蜂群流过去，稳态几乎黏死
                Projectile.Center = Vector2.Lerp(Projectile.Center, target.Center, transferBeat > 0 ? 0.28f : 0.55f);
                Projectile.velocity = Vector2.Zero;

                //判定箱随目标体型
                int size = (int)MathHelper.Clamp(Math.Max(target.width, target.height) * 1.3f + 56f, 110f, 300f);
                if (Projectile.width != size) {
                    Projectile.Resize(size, size);
                }

                //减速债务：owner挂buff，原版NPCbuff同步铺到各端
                if (Projectile.owner == Main.myPlayer && (int)Age % 10 == 0) {
                    target.AddBuff(ModContent.BuffType<SwarmVortexDebuff>(), 30);
                }

                //蜂噬伤害逐帧随owner加成刷新(只在owner端解算命中)
                Player owner = Main.player[Projectile.owner];
                if (owner != null && owner.active) {
                    Projectile.damage = SwarmVortexPlayer.ComputeVortexDamage(owner);
                }
            }

            UpdateSoundAndParticles(target);
        }

        /// <summary>绑定目标解析：槽位+类型双校验(netcode §4.1 槽位复用)</summary>
        private NPC ValidTarget() {
            int idx = BoundIndex;
            if (idx < 0 || idx >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[idx];
            return npc.active && npc.type == BoundType && npc.life > 0 ? npc : null;
        }

        /// <summary>就近转移目标：520px内最近可追击敌怪</summary>
        private NPC FindTransferTarget() {
            NPC best = null;
            float bestDist = 520f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        //只咬绑定目标，成形前/消散中不咬
        public override bool? CanHitNPC(NPC target)
            => Formed && target.whoAmI == BoundIndex ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //蜂噬微反馈(owner端命中解算，本地演出)
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                Vector2 pos = target.position + new Vector2(
                    Main.rand.NextFloat(target.width), Main.rand.NextFloat(target.height));
                PRTLoader.NewParticle<PRT_BeeGlint>(pos, Main.rand.NextVector2Circular(1.6f, 1.6f),
                    SwarmVortexPlayer.BeeGold, Main.rand.NextFloat(0.7f, 1.1f));
            }
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_HoneyDrop>(target.Center + Main.rand.NextVector2Circular(12f, 12f),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 2.5f)),
                    SwarmVortexPlayer.Amber * 0.9f, Main.rand.NextFloat(0.6f, 0.9f));
            }
        }

        public override void OnKill(int timeLeft) {
            //收场：残余蜂群外抛散逸(各端OnKill自播)
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.4f, Pitch = -0.5f }, Projectile.Center);
            float radius = Projectile.width * 0.5f;
            for (int i = 0; i < 22; i++) {
                float angle = MathHelper.TwoPi * i / 22f + Main.rand.NextFloat(0.2f);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius * Main.rand.NextFloat(0.5f, 1f);
                PRTLoader.NewParticle<PRT_VortexBee>(pos,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(4f, 7f),
                    Color.Lerp(SwarmVortexPlayer.BeeGold, SwarmVortexPlayer.Amber, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(null, radius, 1f, Main.rand.Next(18, 30), PRT_VortexBee.ModeScatter);
            }
        }

        //==================== 逐帧演出 ====================

        private void UpdateSoundAndParticles(NPC target) {
            if (VaultUtils.isServer) {
                return;
            }

            //成形第一拍：蜂鸣+尖啸，各端确定性同播
            if ((int)Age == 1) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.75f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
            }
            //持续低鸣
            if (Formed && (int)Age % 46 == 0) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.2f, Pitch = -0.55f, MaxInstances = 2 }, Projectile.Center);
            }

            //屏外不铺粒子
            Rectangle view = new((int)Main.screenPosition.X - 220, (int)Main.screenPosition.Y - 220,
                Main.screenWidth + 440, Main.screenHeight + 440);
            if (!view.Contains(Projectile.Center.ToPoint())) {
                return;
            }

            float radius = Projectile.width * 0.5f;
            float spinDir = Projectile.identity % 2 == 0 ? 1f : -1f;

            if (Dissolving) {
                //消散：稀疏外抛
                if ((int)Age % 2 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    PRTLoader.NewParticle<PRT_VortexBee>(
                        Projectile.Center + ang.ToRotationVector2() * radius * 0.7f,
                        ang.ToRotationVector2().RotatedBy(spinDir * MathHelper.PiOver2) * 5f,
                        SwarmVortexPlayer.Amber, Main.rand.NextFloat(0.7f, 1f))
                        ?.Configure(null, radius, spinDir, Main.rand.Next(14, 24), PRT_VortexBee.ModeScatter);
                }
                return;
            }

            if (Age <= FormTicks) {
                //成形拍：外圈大批螺旋收拢(涡旋速度场在PRT内)
                for (int i = 0; i < 3; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 start = Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(190f, 280f);
                    PRTLoader.NewParticle<PRT_VortexBee>(start,
                        Main.rand.NextVector2Circular(2f, 2f),
                        Color.Lerp(SwarmVortexPlayer.BeeGold, SwarmVortexPlayer.Amber, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.85f, 1.25f))
                        ?.Configure(TargetEntity(target), radius * Main.rand.NextFloat(0.55f, 0.95f), spinDir,
                            Main.rand.Next(30, 48), PRT_VortexBee.ModeConverge);
                }
                //收拢丝缕闪点
                if ((int)Age % 3 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    PRTLoader.NewParticle<PRT_BeeGlint>(
                        Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(60f, 200f),
                        -ang.ToRotationVector2() * 3f, SwarmVortexPlayer.BeeGold, 1.1f);
                }
                return;
            }

            //稳态：持续补蜂维持涡群密度(多涡并行时各自限速，PRT池另有600硬顶)
            int activeVortex = CountActiveVortexes();
            int spawnGate = activeVortex >= 3 ? 2 : 1;
            if ((int)Age % spawnGate == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_VortexBee>(
                    Projectile.Center + ang.ToRotationVector2() * radius * Main.rand.NextFloat(0.35f, 1.05f),
                    Main.rand.NextVector2Circular(3f, 3f),
                    Color.Lerp(SwarmVortexPlayer.BeeGold, SwarmVortexPlayer.Amber, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(TargetEntity(target), radius * Main.rand.NextFloat(0.45f, 0.9f), spinDir,
                        Main.rand.Next(26, 44), PRT_VortexBee.ModeOrbit);
            }
            //转移流束：额外一股奔向新目标
            if (transferBeat > 0 && target != null) {
                for (int i = 0; i < 2; i++) {
                    Vector2 start = Projectile.Center + Main.rand.NextVector2CircularEdge(150f, 150f);
                    PRTLoader.NewParticle<PRT_VortexBee>(start, (target.Center - start).SafeNormalize(Vector2.UnitX) * 6f,
                        SwarmVortexPlayer.BeeGold, 1f)
                        ?.Configure(TargetEntity(target), radius * 0.6f, spinDir,
                            Main.rand.Next(24, 36), PRT_VortexBee.ModeConverge);
                }
            }
            //琥珀蜜雾偶发
            if ((int)Age % 24 == 0) {
                PRTLoader.NewParticle<PRT_HoneyMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(radius * 0.6f, radius * 0.6f),
                    Main.rand.NextVector2Circular(0.6f, 0.6f),
                    SwarmVortexPlayer.Amber * 0.35f, Main.rand.NextFloat(0.9f, 1.4f));
            }
        }

        private Entity TargetEntity(NPC target) => target != null && target.active ? target : null;

        private int CountActiveVortexes() {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == Type) {
                    count++;
                }
            }
            return count;
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            float radius = Projectile.width * 0.5f;
            //强度包络：成形爬升→稳态→消散塌落；转移拍轻微失压
            float envelope = MathHelper.Clamp(Age / FormTicks, 0f, 1f);
            if (Dissolving) {
                envelope *= Projectile.timeLeft / (float)DissolveTicks;
            }
            if (transferBeat > 0) {
                envelope *= 0.72f;
            }
            if (envelope <= 0.01f) {
                return false;
            }

            NPC target = ValidTarget();

            Effect vortexFx = EffectLoader.BRelicBeeVortex?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (vortexFx != null && noise != null) {
                DrawVortexShader(vortexFx, noise, radius, envelope);
            }
            else {
                DrawVortexFallback(radius, envelope);
            }

            if (target != null && !Dissolving) {
                DrawBeaconTotem(target, envelope);
            }
            return false;
        }

        /// <summary>涡群流场层：加色批，rgb不预乘、a携带包络(加色批源因子=SourceAlpha)</summary>
        private void DrawVortexShader(Effect fx, Texture2D noise, float radius, float envelope) {
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSpin"]?.SetValue(Projectile.localAI[1]);
            fx.Parameters["uIntensity"]?.SetValue(envelope * 1.05f);
            fx.Parameters["uForm"]?.SetValue(MathHelper.Clamp((Age - 4f) / FormTicks, 0f, 1f));
            fx.Parameters["uHole"]?.SetValue(0.34f);
            fx.Parameters["uColA"]?.SetValue(new Vector3(1f, 0.8f, 0.28f));
            fx.Parameters["uColB"]?.SetValue(new Vector3(0.72f, 0.42f, 0.08f));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float quadSize = radius * 2f * 1.7f;
            Vector2 scale = new(quadSize / pixel.Width, quadSize / pixel.Height);
            sb.Draw(pixel, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>无着色器回退：加色环点脉动，蜂群主体仍由PRT承担，不至无形</summary>
        private void DrawVortexFallback(float radius, float envelope) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float spin = Projectile.localAI[1];
            for (int i = 0; i < 8; i++) {
                float ang = spin + MathHelper.TwoPi * i / 8f;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius * 0.72f - Main.screenPosition;
                Color c = new Color(255, 195, 70, 0) * (0.34f * envelope);
                Main.EntitySpriteDraw(glow, pos, null, c, 0f, glow.Size() * 0.5f,
                    0.5f + 0.12f * (float)Math.Sin(spin * 3f + i), SpriteEffects.None, 0);
            }
        }

        /// <summary>锁定目标头顶的蜂后信标图腾：竖直信标束(复用蜂舞预警线)+冠冕星芒</summary>
        private void DrawBeaconTotem(NPC target, float envelope) {
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
            float beamLen = 104f * pulse;
            const float BeamWidth = 30f;
            Vector2 basePos = new(target.Center.X, target.Top.Y - 12f);

            Effect telegraph = EffectLoader.QueenBeeTelegraph?.Value;
            if (telegraph != null) {
                telegraph.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                telegraph.Parameters["uIntensity"]?.SetValue(0.6f * envelope);
                telegraph.Parameters["uLockProgress"]?.SetValue(1f);
                telegraph.Parameters["uAspect"]?.SetValue(beamLen / BeamWidth);
                telegraph.Parameters["uColor"]?.SetValue(new Vector3(1f, 0.74f, 0.24f));

                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, telegraph, Main.GameViewMatrix.TransformationMatrix);
                telegraph.CurrentTechnique.Passes[0].Apply();

                Texture2D pixel = VaultAsset.placeholder2.Value;
                Vector2 scale = new(beamLen / pixel.Width, BeamWidth / pixel.Height);
                //自头顶向上生长
                sb.Draw(pixel, basePos - Main.screenPosition, null, Color.White,
                    -MathHelper.PiOver2, new Vector2(0f, pixel.Height / 2f), scale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                //回退：竖拉软辉(A=0加色技法进预乘AlphaBlend默认批)
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Color c = new Color(255, 190, 70, 0) * (0.5f * envelope);
                    Main.EntitySpriteDraw(glow, basePos + new Vector2(0f, -beamLen * 0.5f) - Main.screenPosition,
                        null, c, 0f, glow.Size() * 0.5f, new Vector2(0.35f, beamLen / 52f), SpriteEffects.None, 0);
                }
            }

            //冠冕：束顶三芒星错相闪(黑底星图进默认批走A=0加色)
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                Vector2 crownPos = basePos + new Vector2(0f, -beamLen) - Main.screenPosition;
                float t = Main.GlobalTimeWrappedHourly;
                for (int i = 0; i < 3; i++) {
                    float sparkle = 0.55f + 0.45f * (float)Math.Sin(t * 5f + i * 2.09f);
                    Vector2 off = new((i - 1) * 13f, -Math.Abs(i - 1) * 5f);
                    Color c = new Color(255, 216, 110, 0) * (0.55f * envelope * sparkle);
                    Main.EntitySpriteDraw(star, crownPos + off, null, c, t * (i % 2 == 0 ? 0.8f : -0.6f),
                        star.Size() * 0.5f, 0.045f + 0.014f * sparkle + (i == 1 ? 0.02f : 0f), SpriteEffects.None, 0);
                }
            }
        }
    }
}
