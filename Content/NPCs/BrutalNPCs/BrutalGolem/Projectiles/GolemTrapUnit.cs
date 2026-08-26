using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>神殿机关单元：地板尖刺/火焰喷口/射线口，按乐谱时序起爆
    /// ai[0]=类型, ai[1]=起爆延迟, ai[2]=朝向(0上/1下/2右/3左)</summary>
    internal class GolemTrapUnit : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal enum TrapKind : int
        {
            /// <summary>尖刺柱</summary>
            Spike = 0,
            /// <summary>火焰喷口</summary>
            FlameVent = 1,
            /// <summary>射线口</summary>
            RayPort = 2,
        }

        internal const int DeployFrames = 14;
        internal const int RetractFrames = 20;
        internal const float SpikeLength = 190f;
        internal const float FlameLength = 320f;
        internal const float ColumnWidth = 44f;

        private TrapKind Kind => (TrapKind)(int)Projectile.ai[0];
        private int Delay => (int)Math.Max(Projectile.ai[1], 1f);
        private int DirIndex => (int)Projectile.ai[2];

        private int ActiveFrames => Kind switch {
            TrapKind.Spike => 30,
            TrapKind.FlameVent => 96,
            _ => 90,
        };

        private int TotalFrames => DeployFrames + Delay + ActiveFrames + RetractFrames;
        private int Elapsed => TotalFrames - Projectile.timeLeft;
        private bool Deploying => Elapsed < DeployFrames;
        private bool Armed => Elapsed >= DeployFrames && Elapsed < DeployFrames + Delay;
        private bool Active => Elapsed >= DeployFrames + Delay && Elapsed < DeployFrames + Delay + ActiveFrames;
        private int ActiveTime => Elapsed - DeployFrames - Delay;
        private float ArmProgress => Armed ? (Elapsed - DeployFrames) / (float)Delay : (Elapsed >= DeployFrames + Delay ? 1f : 0f);

        /// <summary>发射方向单位向量</summary>
        private Vector2 Dir => DirIndex switch {
            1 => Vector2.UnitY,
            2 => Vector2.UnitX,
            3 => -Vector2.UnitX,
            _ => -Vector2.UnitY,
        };

        private bool initialized;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //哨兵值：首帧若未被网络校时则本地归位
            Projectile.timeLeft = 60000;
            Projectile.netImportant = true;
        }

        /// <summary>中途加入校时：同步已流逝帧数</summary>
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            writer.Write((short)Math.Max(Elapsed, 0));
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            short elapsed = reader.ReadInt16();
            Projectile.timeLeft = Math.Max(TotalFrames - elapsed, 1);
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                if (Projectile.timeLeft > TotalFrames) {
                    Projectile.timeLeft = TotalFrames;
                }
                if (!Main.dedServ && Elapsed < DeployFrames) {
                    SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.3f, Volume = 0.75f }, Projectile.Center);
                }
            }

            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.36f, 0.1f) * (0.3f + 0.7f * ArmProgress));

            //部署期出土尘
            if (Deploying && !Main.dedServ && Elapsed % 3 == 0) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Stone, 0f, -1f, 60, default, 1.2f);
                dust.velocity *= 0.5f;
            }

            //预警临界拍
            if (Armed && !Main.dedServ && Elapsed == DeployFrames + (int)(Delay * 0.78f)) {
                SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.3f, Volume = 0.55f }, Projectile.Center);
            }

            if (Active) {
                UpdateActive();
            }
        }

        private void UpdateActive() {
            switch (Kind) {
                case TrapKind.Spike: {
                    if (ActiveTime == 0 && !Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item69 with { Pitch = -0.25f, Volume = 1f }, Projectile.Center);
                        Core.GolemScreenEffects.Shake(1.6f);
                        //破土碎屑 + 熔火迸溅（喷发瞬间要有爆点，不只有柱体本身）
                        for (int i = 0; i < 8; i++) {
                            PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center + Dir * 10f,
                                Dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 7f),
                                new Color(122, 104, 78), Main.rand.NextFloat(0.7f, 1.2f)).Configure(40);
                        }
                        for (int i = 0; i < 6; i++) {
                            PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Dir * 14f,
                                Dir.RotatedByRandom(0.55f) * Main.rand.NextFloat(4f, 9f),
                                new Color(255, 190, 80), Main.rand.NextFloat(0.8f, 1.2f)).Configure(true, 18);
                        }
                    }
                    //缩回余韵：熔缝冷却余烬沿柱身逸散（贴图消失了热还在）
                    if (ActiveTime >= 22 && ActiveTime % 3 == 0 && !Main.dedServ) {
                        PRTLoader.NewParticle<PRT_Spark>(
                            Projectile.Center + Dir * Main.rand.NextFloat(8f, SpikeLength * 0.6f),
                            -Dir * Main.rand.NextFloat(0.4f, 1.2f) + Main.rand.NextVector2Circular(0.7f, 0.7f),
                            new Color(255, 140, 50), Main.rand.NextFloat(0.5f, 0.8f)).Configure(true, 26);
                    }
                    break;
                }
                case TrapKind.FlameVent: {
                    if (ActiveTime == 0 && !Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.2f, Volume = 0.9f }, Projectile.Center);
                        //点火环：喷口先炸开一圈火星再成柱
                        for (int i = 0; i < 10; i++) {
                            Dust ignite = Dust.NewDustPerfect(Projectile.Center + Dir * 12f, DustID.Torch,
                                Dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(3f, 8f), 0, default, 1.8f);
                            ignite.noGravity = true;
                        }
                    }
                    //喷焰粒子流
                    if (!Main.dedServ && ActiveTime % 2 == 0) {
                        float jet = JetProgress();
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + Dir * 16f, DustID.Torch,
                            Dir.RotatedByRandom(0.24f) * Main.rand.NextFloat(6f, 13f) * jet, 0, default, Main.rand.NextFloat(1.6f, 2.4f));
                        dust.noGravity = true;
                        if (Main.rand.NextBool(3)) {
                            Dust smoke = Dust.NewDustPerfect(Projectile.Center + Dir * (FlameLength * 0.7f * jet),
                                DustID.Smoke, Dir * 2f, 120, default, 1.4f);
                            smoke.noGravity = true;
                        }
                    }
                    break;
                }
                case TrapKind.RayPort: {
                    //按拍发射两道横掠射线（服务端）
                    if (!VaultUtils.isClient && (ActiveTime == 0 || ActiveTime == 38) && Projectile.localAI[0] < 2f) {
                        Projectile.localAI[0]++;
                        GolemEyeRay.Fire(FindOwnerNpc(), Projectile.Center + Dir * 14f, Dir.ToRotation(),
                            Core.GolemDirector.RayTelegraph - 12, Projectile.damage);
                    }
                    break;
                }
            }
        }

        /// <summary>喷焰生长包络：起势10帧长满，尾段收口</summary>
        private float JetProgress() {
            float grow = MathHelper.Clamp(ActiveTime / 10f, 0f, 1f);
            float fade = MathHelper.Clamp((ActiveFrames - ActiveTime) / 12f, 0f, 1f);
            return grow * fade;
        }

        /// <summary>尖刺伸出包络：8帧暴出，14帧驻留，8帧缩回</summary>
        private float SpikeProgress() {
            if (ActiveTime < 8) {
                float t = ActiveTime / 8f;
                //20次幂缓出：一瞬暴出
                return 1f - MathF.Pow(1f - t, 3.4f);
            }
            if (ActiveTime < 22) {
                return 1f;
            }
            return MathHelper.Clamp((30 - ActiveTime) / 8f, 0f, 1f);
        }

        private NPC FindOwnerNpc() {
            if (NPC.golemBoss >= 0 && NPC.golemBoss < Main.maxNPCs && Main.npc[NPC.golemBoss].active) {
                return Main.npc[NPC.golemBoss];
            }
            return null;
        }

        public override bool? CanDamage() {
            if (!Active || Kind == TrapKind.RayPort) {
                return false;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Active || Kind == TrapKind.RayPort) {
                return false;
            }

            float length = Kind == TrapKind.Spike ? SpikeLength * SpikeProgress() : FlameLength * JetProgress();
            if (length < 8f) {
                return false;
            }

            //沿朝向的柱状判定
            Vector2 start = Projectile.Center;
            Vector2 end = start + Dir * length;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, ColumnWidth, ref collisionPoint);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //归寂碎屑 + 余温火星（机关沉回地里，热气比贴图多活一拍）
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Stone, 0f, -0.5f, 80, default, 1f);
                dust.velocity *= 0.4f;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(14f, 8f),
                    -Dir * Main.rand.NextFloat(0.3f, 1f) + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    new Color(255, 150, 60), Main.rand.NextFloat(0.4f, 0.7f)).Configure(true, 30);
            }
        }

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            float plateRot = Dir.ToRotation() + MathHelper.PiOver2;
            float retractFade = Elapsed > TotalFrames - RetractFrames
                ? MathHelper.Clamp((TotalFrames - Elapsed) / (float)RetractFrames, 0f, 1f)
                : 1f;

            Effect shader = EffectLoader.GolemTrapWork?.Value;
            if (shader != null) {
                DrawWithShader(shader, plateRot, retractFade);
            }
            else {
                DrawFallback(plateRot, retractFade);
            }
            return false;
        }

        private void DrawWithShader(Effect shader, float plateRot, float retractFade) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D quad = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            float seed = Projectile.whoAmI * 0.137f % 1f;
            float columnRot = Dir.ToRotation() + MathHelper.PiOver2;
            Vector2 columnOrigin = new(quad.Width / 2f, quad.Height);

            //机关基座
            shader.CurrentTechnique = shader.Techniques["PlateTech"];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(ArmProgress);
            shader.Parameters["uIntensity"]?.SetValue(retractFade);
            shader.Parameters["uKind"]?.SetValue((float)(int)Kind);
            shader.Parameters["uSeed"]?.SetValue(seed);
            shader.CurrentTechnique.Passes[0].Apply();
            float plateSize = 64f;
            sb.Draw(quad, drawPos, null, Color.White, plateRot, quad.Size() / 2f,
                new Vector2(plateSize / quad.Width, plateSize * 0.5f / quad.Height), SpriteEffects.None, 0f);

            //待命预警柱：淡轮廓画满最终喷发footprint，热浪填充随进度升起（危险区先可读，再喷发）
            if (Armed && Kind != TrapKind.RayPort) {
                float warnLen = Kind == TrapKind.Spike ? SpikeLength : FlameLength;
                shader.CurrentTechnique = shader.Techniques["WarnTech"];
                shader.Parameters["uProgress"]?.SetValue(ArmProgress);
                shader.Parameters["uIntensity"]?.SetValue(1f);
                shader.CurrentTechnique.Passes[0].Apply();
                Vector2 warnScale = new(ColumnWidth * 1.5f / quad.Width, warnLen / quad.Height);
                sb.Draw(quad, drawPos, null, Color.White, columnRot,
                    columnOrigin, warnScale, SpriteEffects.None, 0f);
            }

            //柱体（尖刺/火焰）
            if (Active && Kind != TrapKind.RayPort) {
                float length = Kind == TrapKind.Spike ? SpikeLength * SpikeProgress() : FlameLength * JetProgress();
                if (length > 6f) {
                    shader.CurrentTechnique = shader.Techniques[Kind == TrapKind.Spike ? "SpikeTech" : "FlameTech"];
                    shader.Parameters["uProgress"]?.SetValue(Kind == TrapKind.Spike ? SpikeProgress() : JetProgress());
                    shader.Parameters["uIntensity"]?.SetValue(1f);
                    shader.CurrentTechnique.Passes[0].Apply();

                    //柱体 quad：origin 在底边中点，沿 Dir 延伸（局部上方向旋到 Dir）
                    Vector2 columnScale = new(ColumnWidth * 1.5f / quad.Width, length / quad.Height);
                    sb.Draw(quad, drawPos, null, Color.White, columnRot,
                        columnOrigin, columnScale, SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>无着色器兜底：亮度贴图拼装</summary>
        private void DrawFallback(float plateRot, float retractFade) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //基座光钉
            Color plateCol = Color.Lerp(new Color(180, 120, 40, 0), new Color(255, 200, 90, 0), ArmProgress) * retractFade;
            Main.EntitySpriteDraw(glow, drawPos, null, plateCol, plateRot,
                glow.Size() / 2f, new Vector2(0.9f, 0.4f), SpriteEffects.None, 0);

            if (Active && Kind != TrapKind.RayPort) {
                float length = Kind == TrapKind.Spike ? SpikeLength * SpikeProgress() : FlameLength * JetProgress();
                Color columnCol = Kind == TrapKind.Spike
                    ? new Color(230, 180, 110, 0)
                    : new Color(255, 140, 40, 0);
                Main.EntitySpriteDraw(line, drawPos, null, columnCol * 0.9f, Dir.ToRotation(),
                    new Vector2(0f, line.Height / 2f), new Vector2(length / line.Width, 0.5f), SpriteEffects.None, 0);
            }
            else if (Armed) {
                //预警缝隙线
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f);
                Main.EntitySpriteDraw(line, drawPos, null, new Color(255, 170, 60, 0) * (0.35f + 0.4f * ArmProgress * pulse),
                    Dir.ToRotation(), new Vector2(0f, line.Height / 2f),
                    new Vector2((Kind == TrapKind.Spike ? SpikeLength : FlameLength) / line.Width * 0.9f, 0.1f), SpriteEffects.None, 0);
            }
        }
        #endregion

        #region 布设助手（服务端）
        private static void Plant(NPC owner, Vector2 pos, TrapKind kind, int dirIndex, int delay, int damage) {
            if (VaultUtils.isClient || owner == null) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<GolemTrapUnit>(), damage, 0f, Main.myPlayer,
                (int)kind, delay, dirIndex);
            if (id >= 0 && id < Main.maxProjectiles) {
                Main.projectile[id].netUpdate = true;
            }
        }

        /// <summary>落地布设：从参考高度向下找地表；无地表时悬浮</summary>
        internal static void PlantOnGround(NPC owner, float x, float nearY, TrapKind kind, int delay, int damage) {
            int tileX = (int)(x / 16f);
            int startY = (int)(nearY / 16f) - 20;
            for (int y = Math.Max(startY, 10); y < startY + 70 && y < Main.maxTilesY - 10; y++) {
                if (WorldGen.SolidTile(tileX, y)) {
                    Vector2 pos = new(x, y * 16f - 8f);
                    Plant(owner, pos, kind, 0, delay, damage);
                    return;
                }
            }
            //无地表：悬浮机关，向上喷发
            Plant(owner, new Vector2(x, nearY + 170f), kind, 0, delay, damage);
        }

        /// <summary>侧壁布设：向侧向找墙面；无墙时悬浮于侧翼</summary>
        internal static void PlantOnSide(NPC owner, Player target, int side, TrapKind kind, int delay, int damage) {
            int tileY = (int)(target.Center.Y / 16f);
            int startX = (int)(target.Center.X / 16f) + side * 10;
            int endX = startX + side * 60;
            for (int x = startX; side > 0 ? x < endX : x > endX; x += side) {
                if (x < 10 || x > Main.maxTilesX - 10) {
                    break;
                }
                if (WorldGen.SolidTile(x, tileY)) {
                    //贴内壁：发射方向朝向玩家
                    Vector2 pos = new(x * 16f + (side > 0 ? -8f : 24f), target.Center.Y);
                    Plant(owner, pos, kind, side > 0 ? 3 : 2, delay, damage);
                    return;
                }
            }
            //无墙：悬浮侧翼
            Vector2 hover = target.Center + new Vector2(side * 560f, -40f);
            Plant(owner, hover, kind, side > 0 ? 3 : 2, delay, damage);
        }

        /// <summary>顶部布设：向上找天花板；无顶时悬浮上空</summary>
        internal static void PlantOnCeiling(NPC owner, Player target, TrapKind kind, int delay, int damage) {
            int tileX = (int)(target.Center.X / 16f);
            int startY = (int)(target.Center.Y / 16f) - 6;
            for (int y = startY; y > startY - 50 && y > 10; y--) {
                if (WorldGen.SolidTile(tileX, y)) {
                    Vector2 pos = new(target.Center.X, y * 16f + 24f);
                    Plant(owner, pos, kind, 1, delay, damage);
                    return;
                }
            }
            Plant(owner, target.Center + new Vector2(0f, -430f), kind, 1, delay, damage);
        }
        #endregion
    }
}
