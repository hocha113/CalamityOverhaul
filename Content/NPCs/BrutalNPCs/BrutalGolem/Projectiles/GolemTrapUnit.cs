using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>神殿机关单元：刺矛机关/火焰机关/射线口/尖刺球机关，按乐谱时序起爆。
    /// 本体全部复用原版神庙资产：机关砖（Tiles_137 蜥蜴人机关行）三联成 48×16 基座从墙面滑出，
    /// 刺矛=原版刺矛机关的矛杆（Chain17）+矛头（Projectile_186），尖刺球=原版 185，
    /// 射线口=蜥蜴砖人面刻纹（Tiles_226）；着色器只保留喷焰柱与预警脚印这两种纯光效。
    /// ai[0]=类型, ai[1]=起爆延迟, ai[2]=朝向(0上/1下/2右/3左)</summary>
    internal class GolemTrapUnit : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal enum TrapKind : int
        {
            /// <summary>刺矛机关：三联矛杆暴出</summary>
            Spike = 0,
            /// <summary>火焰机关：三联喷口成柱</summary>
            FlameVent = 1,
            /// <summary>射线口：人面砖发眼光</summary>
            RayPort = 2,
            /// <summary>尖刺球机关：吊顶落球滚地</summary>
            SpikyBall = 3,
        }

        internal const int DeployFrames = 14;
        internal const int RetractFrames = 20;
        internal const float SpikeLength = 190f;
        internal const float FlameLength = 320f;
        internal const float ColumnWidth = 44f;
        /// <summary>基座厚度（凸出墙面的像素），与单格砖同厚</summary>
        internal const float BlockThickness = 16f;
        /// <summary>单矛判定宽：矛杆 10px 加两侧余量</summary>
        private const float SpearHitWidth = 14f;
        /// <summary>三条机关道的横向偏移（每格一门）</summary>
        private static readonly float[] LaneOffsets = [-16f, 0f, 16f];
        /// <summary>喷焰柱里滚动的火球块数量（奇偶分两层夹住喷焰）</summary>
        private const int FireChunkCount = 6;

        private TrapKind Kind => (TrapKind)(int)Projectile.ai[0];
        private int Delay => (int)Math.Max(Projectile.ai[1], 1f);
        private int DirIndex => (int)Projectile.ai[2];

        private int ActiveFrames => Kind switch {
            TrapKind.Spike => 30,
            TrapKind.FlameVent => 96,
            TrapKind.SpikyBall => 24,
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

        /// <summary>横向单位向量（机关道排布轴）</summary>
        private Vector2 Across => new(-Dir.Y, Dir.X);

        /// <summary>基座外沿中点：矛/焰/球都从这里出来</summary>
        private Vector2 Mouth => Projectile.Center + Dir * (BlockThickness * 0.5f);

        /// <summary>基座露出墙面的厚度：部署期滑出，缩回期沉回</summary>
        private float Thickness {
            get {
                if (Deploying) {
                    float t = Elapsed / (float)DeployFrames;
                    return BlockThickness * (1f - MathF.Pow(1f - t, 2.6f));
                }
                int retractStart = TotalFrames - RetractFrames;
                if (Elapsed >= retractStart) {
                    float t = (Elapsed - retractStart) / (float)RetractFrames;
                    return BlockThickness * (1f - MathHelper.Clamp(t, 0f, 1f));
                }
                return BlockThickness;
            }
        }

        /// <summary>各端一致的单元种子（identity 全端同值，whoAmI 不同步）</summary>
        private float Seed => Projectile.identity * 0.137f % 1f;

        /// <summary>机关道长度系数：中道满长，两侧矛略短，整排不复制粘贴</summary>
        private float LaneScale(int lane) {
            if (lane == 1) {
                return 1f;
            }
            return 0.86f + 0.12f * Hash01(Projectile.identity, lane);
        }

        private static float Hash01(int a, int b) {
            unchecked {
                int h = a * 374761393 + b * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

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

            //部署期出土尘：沿基座外沿整排冒石粉，顺着滑出方向飘
            if (Deploying && !Main.dedServ && Elapsed % 2 == 0) {
                Vector2 spot = Projectile.Center - Dir * (BlockThickness * 0.5f - Thickness) + Across * Main.rand.NextFloat(-22f, 22f);
                Dust dust = Dust.NewDustPerfect(spot, DustID.Stone, Dir * Main.rand.NextFloat(0.5f, 1.5f), 60, default, 1.2f);
                dust.velocity += Across * Main.rand.NextFloat(-0.4f, 0.4f);
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
                        //三门齐出：每个矛口各自一撮石屑 + 金属擦火（爆点跟着门走，不在基座中心糊一团）
                        foreach (float lane in LaneOffsets) {
                            Vector2 port = Mouth + Across * lane;
                            for (int i = 0; i < 3; i++) {
                                PRTLoader.NewParticle<PRT_MarbleChip>(port,
                                    Dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 7f),
                                    new Color(122, 104, 78), Main.rand.NextFloat(0.7f, 1.1f)).Configure(40);
                            }
                            for (int i = 0; i < 2; i++) {
                                PRTLoader.NewParticle<PRT_Spark>(port + Dir * 6f,
                                    Dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 9f),
                                    new Color(255, 190, 80), Main.rand.NextFloat(0.8f, 1.2f)).Configure(true, 18);
                            }
                        }
                    }
                    //缩回余韵：矛口余火沿矛杆逸散（矛缩回去了热还在）
                    if (ActiveTime >= 22 && ActiveTime % 3 == 0 && !Main.dedServ) {
                        float lane = LaneOffsets[Main.rand.Next(LaneOffsets.Length)];
                        PRTLoader.NewParticle<PRT_Spark>(
                            Mouth + Across * lane + Dir * Main.rand.NextFloat(4f, SpikeLength * 0.5f),
                            -Dir * Main.rand.NextFloat(0.4f, 1.2f) + Main.rand.NextVector2Circular(0.7f, 0.7f),
                            new Color(255, 140, 50), Main.rand.NextFloat(0.5f, 0.8f)).Configure(true, 26);
                    }
                    break;
                }
                case TrapKind.SpikyBall: {
                    if (ActiveTime == 0) {
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.45f, Volume = 0.8f }, Projectile.Center);
                            for (int i = 0; i < 6; i++) {
                                Dust dust = Dust.NewDustPerfect(Mouth + Across * Main.rand.NextFloat(-20f, 20f), DustID.Stone,
                                    Dir * Main.rand.NextFloat(1f, 3f) + Across * Main.rand.NextFloat(-1f, 1f), 60, default, 1.1f);
                                dust.velocity *= 0.6f;
                            }
                        }
                        //两侧门各落一球（服务端一次性）；缺口：仅机关正下方 48px 落道为即时危险，
                        //球落地后缓速弹跳可跳越，中门留空免三球并排封路
                        if (!VaultUtils.isClient && Projectile.localAI[0] < 1f) {
                            Projectile.localAI[0] = 1f;
                            NPC owner = FindOwnerNpc();
                            //无 Boss 时（/vlab 快照）退回弹幕自身为源，球照落
                            Terraria.DataStructures.IEntitySource source = owner != null
                                ? owner.GetSource_FromAI()
                                : Projectile.GetSource_FromThis();
                            for (int lane = 0; lane < LaneOffsets.Length; lane += 2) {
                                Vector2 port = Mouth + Across * LaneOffsets[lane];
                                Vector2 vel = Dir * 3.2f + Across * (lane == 0 ? -0.7f : 0.7f);
                                Projectile.NewProjectile(source, port, vel,
                                    ModContent.ProjectileType<GolemSpikyBall>(), Projectile.damage, 0f, Main.myPlayer);
                            }
                        }
                    }
                    break;
                }
                case TrapKind.FlameVent: {
                    if (ActiveTime == 0 && !Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.2f, Volume = 0.9f }, Projectile.Center);
                        //点火：三个喷口各炸开一撮火星再成柱（同原版火焰机关的 Torch 尘语言）
                        foreach (float lane in LaneOffsets) {
                            for (int i = 0; i < 4; i++) {
                                Dust ignite = Dust.NewDustPerfect(Mouth + Across * lane, DustID.Torch,
                                    Dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(3f, 8f), 0, default, 1.8f);
                                ignite.noGravity = true;
                            }
                        }
                    }
                    //喷焰粒子流：轮换喷口出尘
                    if (!Main.dedServ && ActiveTime % 2 == 0) {
                        float jet = JetProgress();
                        float lane = LaneOffsets[(ActiveTime / 2) % LaneOffsets.Length];
                        Dust dust = Dust.NewDustPerfect(Mouth + Across * lane + Dir * 4f, DustID.Torch,
                            Dir.RotatedByRandom(0.24f) * Main.rand.NextFloat(6f, 13f) * jet, 0, default, Main.rand.NextFloat(1.6f, 2.4f));
                        dust.noGravity = true;
                        if (Main.rand.NextBool(3)) {
                            Dust smoke = Dust.NewDustPerfect(Mouth + Dir * (FlameLength * 0.7f * jet),
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
                        GolemEyeRay.Fire(FindOwnerNpc(), Mouth + Dir * 4f, Dir.ToRotation(),
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

        /// <summary>射线口与尖刺球机关本体不伤人，伤害由射线/球承担</summary>
        private bool BodyHarmless => Kind is TrapKind.RayPort or TrapKind.SpikyBall;

        public override bool? CanDamage() {
            if (!Active || BodyHarmless) {
                return false;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Active || BodyHarmless) {
                return false;
            }
            float collisionPoint = 0f;

            if (Kind == TrapKind.Spike) {
                //逐矛判定：每根矛按自身长度算，判定藏在可见矛杆内，不给"空气戳人"
                float progress = SpikeProgress();
                for (int lane = 0; lane < LaneOffsets.Length; lane++) {
                    float length = SpikeLength * LaneScale(lane) * progress;
                    if (length < 8f) {
                        continue;
                    }
                    Vector2 start = Mouth + Across * LaneOffsets[lane];
                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                        start, start + Dir * length, SpearHitWidth, ref collisionPoint)) {
                        return true;
                    }
                }
                return false;
            }

            //喷焰：沿朝向的柱状判定（窄于可见焰体）
            float jetLength = FlameLength * JetProgress();
            if (jetLength < 8f) {
                return false;
            }
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Mouth, Mouth + Dir * jetLength, ColumnWidth, ref collisionPoint);
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
        /// <summary>层序：基座下层（矛/球探头、火球下层、口部底光）→ 着色器光效（预警脚印、喷焰柱）
        /// → 基座砖体 → 充能刻纹加色 → 火球上层。砖体压在矛根上，矛才读作"从槛里出来"</summary>
        public override bool PreDraw(ref Color lightColor) {
            //出生帧哨兵：未校时的端本帧 Elapsed 为大负数，先不画
            if (Projectile.timeLeft > TotalFrames) {
                return false;
            }
            float thickness = Thickness;

            DrawUnderLayer(lightColor);

            Effect shader = EffectLoader.GolemTrapWork?.Value;
            if (shader != null && ((Armed && Kind is TrapKind.Spike or TrapKind.FlameVent)
                || (Active && Kind == TrapKind.FlameVent))) {
                DrawShaderColumns(shader);
            }

            DrawBlock(lightColor, thickness);
            DrawBlockGlow(thickness);

            if (Active && Kind == TrapKind.FlameVent) {
                DrawFireChunks(over: true);
            }
            return false;
        }

        /// <summary>着色器只画两样纯光效：待命预警脚印（刺矛/喷焰的最终危险区）与喷焰柱本体。
        /// 两者都从基座外沿起画，quad 局部上方向旋到 Dir</summary>
        private void DrawShaderColumns(Effect shader) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D quad = VaultAsset.placeholder2.Value;
            Vector2 mouthPos = Mouth - Main.screenPosition;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            float columnRot = Dir.ToRotation() + MathHelper.PiOver2;
            Vector2 columnOrigin = new(quad.Width / 2f, quad.Height);
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uKind"]?.SetValue((float)(int)Kind);
            shader.Parameters["uSeed"]?.SetValue(Seed);

            //待命预警脚印：淡轮廓画满最终喷发 footprint，热浪填充随进度升起（危险区先可读，再喷发）
            if (Armed) {
                float warnLen = Kind == TrapKind.Spike ? SpikeLength : FlameLength;
                shader.CurrentTechnique = shader.Techniques["WarnTech"];
                shader.Parameters["uProgress"]?.SetValue(ArmProgress);
                shader.Parameters["uIntensity"]?.SetValue(1f);
                shader.CurrentTechnique.Passes[0].Apply();
                Vector2 warnScale = new(ColumnWidth * 1.5f / quad.Width, warnLen / quad.Height);
                sb.Draw(quad, mouthPos, null, Color.White, columnRot, columnOrigin, warnScale, SpriteEffects.None, 0f);
            }

            //喷焰柱
            if (Active && Kind == TrapKind.FlameVent) {
                float length = FlameLength * JetProgress();
                if (length > 6f) {
                    shader.CurrentTechnique = shader.Techniques["FlameTech"];
                    shader.Parameters["uProgress"]?.SetValue(JetProgress());
                    shader.Parameters["uIntensity"]?.SetValue(1f);
                    shader.CurrentTechnique.Passes[0].Apply();
                    Vector2 columnScale = new(ColumnWidth * 1.5f / quad.Width, length / quad.Height);
                    sb.Draw(quad, mouthPos, null, Color.White, columnRot, columnOrigin, columnScale, SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>基座三格的贴图与源帧。机关砖（Tiles_137）按朝向取原版方向帧：
        /// 火焰行(frameY 36) 左0/右18/上36/下72；尖刺球行(54) 与刺矛行(72) 下0/上36/左54/右72。
        /// 射线口用蜥蜴砖（Tiles_226）：中格人面刻纹 (36,18)，两翼素砖 (18,18)/(54,18)，人面永远正立不随朝向转</summary>
        private (Texture2D tex, Rectangle src) CellSprite(int lane) {
            if (Kind == TrapKind.RayPort) {
                Main.instance.LoadTiles(TileID.LihzahrdBrick);
                Texture2D brick = TextureAssets.Tile[TileID.LihzahrdBrick].Value;
                int fx = lane switch { 0 => 18, 2 => 54, _ => 36 };
                return (brick, new Rectangle(fx, 18, 16, 16));
            }

            Main.instance.LoadTiles(TileID.Traps);
            Texture2D traps = TextureAssets.Tile[TileID.Traps].Value;
            int frameY = Kind == TrapKind.FlameVent ? 36 : (Kind == TrapKind.SpikyBall ? 54 : 72);
            int frameX;
            if (Kind == TrapKind.FlameVent) {
                frameX = DirIndex switch { 1 => 72, 2 => 18, 3 => 0, _ => 36 };
            }
            else {
                frameX = DirIndex switch { 1 => 0, 2 => 72, 3 => 54, _ => 36 };
            }
            return (traps, new Rectangle(frameX, frameY, 16, 16));
        }

        /// <summary>画一格 16px 砖：只画露出墙面的 thickness 部分（外沿先出来），源帖按朝向从外沿一侧裁</summary>
        private void DrawCell(Texture2D tex, Rectangle cell, int lane, float thickness, Color color) {
            int h = (int)MathF.Round(MathHelper.Clamp(thickness, 0f, BlockThickness));
            if (h <= 0) {
                return;
            }
            //表面点：该格在墙面上的锚点（基座内沿中点沿横向偏到本格）
            Vector2 surface = Projectile.Center - Dir * (BlockThickness * 0.5f) + Across * LaneOffsets[lane];
            Rectangle src;
            Vector2 topLeft;
            switch (DirIndex) {
                case 1: //下：外沿是砖底边
                    src = new Rectangle(cell.X, cell.Y + 16 - h, 16, h);
                    topLeft = surface + new Vector2(-8f, 0f);
                    break;
                case 2: //右：外沿是砖右边
                    src = new Rectangle(cell.X + 16 - h, cell.Y, h, 16);
                    topLeft = surface + new Vector2(0f, -8f);
                    break;
                case 3: //左
                    src = new Rectangle(cell.X, cell.Y, h, 16);
                    topLeft = surface + new Vector2(-h, -8f);
                    break;
                default: //上
                    src = new Rectangle(cell.X, cell.Y, 16, h);
                    topLeft = surface + new Vector2(-8f, -h);
                    break;
            }
            Main.EntitySpriteDraw(tex, topLeft - Main.screenPosition, src, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0);
        }

        /// <summary>基座砖体：三格原版机关砖，逐格乘本地光照（漫反射材质，不走加色）</summary>
        private void DrawBlock(Color lightColor, float thickness) {
            for (int lane = 0; lane < LaneOffsets.Length; lane++) {
                (Texture2D tex, Rectangle src) = CellSprite(lane);
                Vector2 cellCenter = Projectile.Center + Across * LaneOffsets[lane];
                Color light = Lighting.GetColor((int)(cellCenter.X / 16f), (int)(cellCenter.Y / 16f));
                //基座自身发热照亮：暗庙里机关不能黑成一块
                light = Color.Lerp(light, Color.White, 0.25f + 0.2f * ArmProgress);
                DrawCell(tex, src, lane, thickness, light);
            }
        }

        /// <summary>充能刻纹：把砖自己的像素再加色叠一遍，橙纹越充越亮，临爆整块频闪；
        /// 射线口另加人面双眼点光。加色层只叠在实体砖上，不是本体</summary>
        private void DrawBlockGlow(float thickness) {
            float charge = Armed ? ArmProgress : (Active ? 1f : 0f);
            if (charge <= 0.02f || thickness < 2f) {
                return;
            }
            Color glowCol = Kind switch {
                TrapKind.FlameVent => new Color(255, 120, 30, 0),
                TrapKind.RayPort => new Color(255, 235, 150, 0),
                _ => new Color(255, 185, 70, 0),
            };
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * (5f + 10f * charge) + Seed * 7f);
            float crit = Armed ? MathHelper.Clamp((ArmProgress - 0.85f) / 0.15f, 0f, 1f) : 0f;
            float flicker = crit * (0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 30f + Seed * 9f));
            float strength = (0.25f + 0.65f * charge * charge) * pulse + flicker * 0.5f;

            for (int lane = 0; lane < LaneOffsets.Length; lane++) {
                (Texture2D tex, Rectangle src) = CellSprite(lane);
                DrawCell(tex, src, lane, thickness, glowCol * strength);
            }

            if (Kind == TrapKind.RayPort) {
                //人面双眼：眼窝在格内 (4,9)/(12,9)（2px 像素块的中心），点光随充能张开，射线出口那几帧爆亮
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float fire = Active && (ActiveTime < 8 || (ActiveTime >= 38 && ActiveTime < 46)) ? 1f : 0f;
                float eye = 0.35f + 0.65f * charge + fire * 0.8f;
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 eyePos = Projectile.Center + new Vector2(side * 4f, 1f) - Main.screenPosition;
                    Main.EntitySpriteDraw(glow, eyePos, null, new Color(255, 220, 120, 0) * (0.9f * eye), 0f,
                        glow.Size() / 2f, 0.1f + 0.08f * charge + 0.14f * fire, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>压在基座下面的层：口部底光、探头的矛头/球、伸出的矛、喷焰下层火球</summary>
        private void DrawUnderLayer(Color lightColor) {
            if (Armed) {
                //口部底光：整块基座下垫一层暖光（下层，不当本体）
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Main.EntitySpriteDraw(glow, Mouth - Main.screenPosition, null,
                    new Color(255, 170, 70, 0) * (0.35f * ArmProgress), Dir.ToRotation(),
                    glow.Size() / 2f, new Vector2(0.55f, 1.1f), SpriteEffects.None, 0);

                //探头：矛头/球从槛里冒出一点，越临爆冒越多（读招用的实体前兆）
                float peek = 4f + 7f * ArmProgress;
                if (Kind == TrapKind.Spike) {
                    DrawSpears(lightColor, lane => peek, flash: 0f);
                }
                else if (Kind == TrapKind.SpikyBall) {
                    DrawPeekBalls(lightColor, peek);
                }
                return;
            }

            if (!Active) {
                return;
            }
            if (Kind == TrapKind.Spike) {
                float progress = SpikeProgress();
                float flash = MathHelper.Clamp(1f - ActiveTime / 3f, 0f, 1f);
                DrawSpears(lightColor, lane => SpikeLength * LaneScale(lane) * progress, flash);
            }
            else if (Kind == TrapKind.FlameVent) {
                DrawFireChunks(over: false);
            }
        }

        /// <summary>三联刺矛：原版刺矛机关的画法——矛杆取 Chain17 底部 len 行从口沿向外铺，矛头 186 顶在杆端。
        /// 186 的针尖在贴图底端，rot 取 Dir−π/2 让贴图局部 +Y 对准 Dir（与原版 aiStyle 37 同规约）</summary>
        private void DrawSpears(Color lightColor, Func<int, float> laneLength, float flash) {
            Main.instance.LoadProjectile(ProjectileID.SpearTrap);
            Texture2D shaft = TextureAssets.Chain17.Value;
            Texture2D head = TextureAssets.Projectile[ProjectileID.SpearTrap].Value;
            float rot = Dir.ToRotation() - MathHelper.PiOver2;
            Vector2 shaftOrigin = new(shaft.Width / 2f, 0f);
            Vector2 headOrigin = head.Size() / 2f;

            for (int lane = 0; lane < LaneOffsets.Length; lane++) {
                float length = laneLength(lane);
                if (length < 1f) {
                    continue;
                }
                Vector2 port = Mouth + Across * LaneOffsets[lane];

                //矛杆：到矛头根部为止（矛头 16px，中心在 len−8，根部 len−16，多铺 4px 让矛头套住杆端）
                int shaftLen = (int)MathHelper.Clamp(length - 12f, 0f, shaft.Height);
                if (shaftLen > 0) {
                    Vector2 mid = port + Dir * (shaftLen * 0.5f);
                    Color shaftLight = Lighting.GetColor((int)(mid.X / 16f), (int)(mid.Y / 16f));
                    Rectangle src = new(0, shaft.Height - shaftLen, shaft.Width, shaftLen);
                    Main.EntitySpriteDraw(shaft, port - Main.screenPosition, src, shaftLight, rot,
                        shaftOrigin, 1f, SpriteEffects.None, 0);
                }

                Vector2 headPos = port + Dir * (length - 8f);
                Color headLight = Lighting.GetColor((int)(headPos.X / 16f), (int)(headPos.Y / 16f));
                headLight = Color.Lerp(headLight, lightColor, 0.3f);
                Main.EntitySpriteDraw(head, headPos - Main.screenPosition, null, headLight, rot,
                    headOrigin, 1f, SpriteEffects.None, 0);
                //暴出瞬间矛头过曝白闪（≤3 帧的爆点，不常驻）
                if (flash > 0f) {
                    Main.EntitySpriteDraw(head, headPos - Main.screenPosition, null,
                        new Color(255, 240, 200, 0) * (0.85f * flash), rot, headOrigin, 1f, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>尖刺球机关待命：两侧门里的球探出半个身子</summary>
        private void DrawPeekBalls(Color lightColor, float peek) {
            Main.instance.LoadProjectile(ProjectileID.SpikyBallTrap);
            Texture2D ball = TextureAssets.Projectile[ProjectileID.SpikyBallTrap].Value;
            for (int lane = 0; lane < LaneOffsets.Length; lane += 2) {
                Vector2 pos = Mouth + Across * LaneOffsets[lane] + Dir * (peek - 8f);
                Main.EntitySpriteDraw(ball, pos - Main.screenPosition, null, lightColor,
                    Seed * 6f + lane, ball.Size() / 2f, 1f, SpriteEffects.None, 0);
            }
        }

        /// <summary>喷焰柱里滚动的石巨人火球（原版 258）：奇偶两层夹住着色器喷焰，
        /// 沿柱外飘、越远越小越淡、自转；喷焰不再是一条光滑噪声条，而是有块状实体在里面翻</summary>
        private void DrawFireChunks(bool over) {
            float jet = JetProgress();
            if (jet < 0.05f) {
                return;
            }
            Main.instance.LoadProjectile(ProjectileID.Fireball);
            Texture2D fire = TextureAssets.Projectile[ProjectileID.Fireball].Value;
            float reach = FlameLength * jet;
            for (int k = 0; k < FireChunkCount; k++) {
                if (((k & 1) == 1) != over) {
                    continue;
                }
                float along = (ActiveTime * 9f + k * 53f + Seed * 97f) % FlameLength;
                if (along > reach) {
                    continue;
                }
                float t = along / FlameLength;
                float lane = LaneOffsets[k % LaneOffsets.Length] * (1f - t * 0.5f);
                float wobble = MathF.Sin(ActiveTime * 0.3f + k * 1.7f) * 5f * t;
                Vector2 pos = Mouth + Dir * along + Across * (lane + wobble);
                float scale = MathHelper.Lerp(1.15f, 0.5f, t);
                float alpha = (1f - t * 0.65f) * MathHelper.Clamp((reach - along) / 40f, 0f, 1f);
                float rot = ActiveTime * 0.22f * ((k & 1) == 0 ? 1f : -1f) + k;
                Main.EntitySpriteDraw(fire, pos - Main.screenPosition, null,
                    new Color(255, 235, 205, 200) * alpha, rot, fire.Size() / 2f, scale, SpriteEffects.None, 0);
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

        /// <summary>落地布设：从参考高度向下找地表；无地表时悬浮。
        /// 横坐标吸附砖格中心：基座是三块砖，对齐神庙砌缝才像庙里长出来的</summary>
        internal static void PlantOnGround(NPC owner, float x, float nearY, TrapKind kind, int delay, int damage) {
            int tileX = (int)(x / 16f);
            float snapX = tileX * 16f + 8f;
            int startY = (int)(nearY / 16f) - 20;
            for (int y = Math.Max(startY, 10); y < startY + 70 && y < Main.maxTilesY - 10; y++) {
                if (WorldGen.SolidTile(tileX, y)) {
                    Vector2 pos = new(snapX, y * 16f - 8f);
                    Plant(owner, pos, kind, 0, delay, damage);
                    return;
                }
            }
            //无地表：悬浮机关，向上喷发
            Plant(owner, new Vector2(snapX, nearY + 170f), kind, 0, delay, damage);
        }

        /// <summary>侧壁布设：向侧向找墙面；无墙时悬浮于侧翼</summary>
        internal static void PlantOnSide(NPC owner, Player target, int side, TrapKind kind, int delay, int damage) {
            int tileY = (int)(target.Center.Y / 16f);
            float snapY = tileY * 16f + 8f;
            int startX = (int)(target.Center.X / 16f) + side * 10;
            int endX = startX + side * 60;
            for (int x = startX; side > 0 ? x < endX : x > endX; x += side) {
                if (x < 10 || x > Main.maxTilesX - 10) {
                    break;
                }
                if (WorldGen.SolidTile(x, tileY)) {
                    //贴内壁：发射方向朝向玩家
                    Vector2 pos = new(x * 16f + (side > 0 ? -8f : 24f), snapY);
                    Plant(owner, pos, kind, side > 0 ? 3 : 2, delay, damage);
                    return;
                }
            }
            //无墙：悬浮侧翼
            Vector2 hover = new(target.Center.X + side * 560f, snapY - 40f);
            Plant(owner, hover, kind, side > 0 ? 3 : 2, delay, damage);
        }

        /// <summary>顶部布设：在 x 列向上找天花板；无顶时悬浮上空</summary>
        internal static void PlantOnCeiling(NPC owner, float x, float nearY, TrapKind kind, int delay, int damage) {
            int tileX = (int)(x / 16f);
            float snapX = tileX * 16f + 8f;
            int startY = (int)(nearY / 16f) - 6;
            for (int y = startY; y > startY - 50 && y > 10; y--) {
                if (WorldGen.SolidTile(tileX, y)) {
                    Vector2 pos = new(snapX, y * 16f + 24f);
                    Plant(owner, pos, kind, 1, delay, damage);
                    return;
                }
            }
            Plant(owner, new Vector2(snapX, nearY - 430f), kind, 1, delay, damage);
        }
        #endregion
    }
}
