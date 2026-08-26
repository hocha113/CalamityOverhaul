using CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen.Projectiles
{
    /// <summary>
    /// 血露雨帘（环境驱动，非 NPC 弹幕）。ai[0]=帘半宽（像素）。
    /// 生成位置即锁定落点（预告即承诺）：空中血珠凝聚 75 帧（滴答声渐密+闪烁渐急，双通道预告）
    /// → 雨帘落下 200 帧（触帘者短暂流血+微量伤害，檐下/室内被瓦面物理挡住即免疫）
    /// → 血渍余韵 80 帧（地面血渍蒸散收场）。
    /// 伤害不走 hostile 碰撞管线：各端只判定本机玩家并本机结算 Hurt（受击端权威，原生同步）；
    /// 弹体恒 damage=0，不存在预热清零陷阱。Boss 在场时判伤停摆，视觉照常
    /// </summary>
    internal class FleshfenDewRainProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>凝聚预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int CondenseFrames = 75;
        /// <summary>雨帘落下帧数</summary>
        private const int FallFrames = 200;
        /// <summary>余韵蒸散帧数</summary>
        private const int AfterFrames = 80;
        /// <summary>触帘伤害 = 群系代表性原版敌怪接触伤害 × 此系数（镜像 DamageFrac 写法）</summary>
        private const float DamageFrac = 0.45f;
        /// <summary>本机玩家两次触帘结算的最小间隔</summary>
        private const int HitIntervalFrames = 55;
        /// <summary>触帘流血时长（短暂，原版减益）</summary>
        private const int BleedFrames = 240;
        /// <summary>向下寻找地面的最大瓦格数（找不到按此深度收口）</summary>
        private const int GroundSearchTiles = 70;

        /// <summary>猩红风味色（只读引用共用风味表，禁改源文件）</summary>
        private static readonly Color DeepBlood = EvilBiomeFX.Deep(EvilBiomeFX.FlavorCrimson);
        private static readonly Color BrightBlood = EvilBiomeFX.Bright(EvilBiomeFX.FlavorCrimson);

        private float HalfWidth => Projectile.ai[0];
        private int TotalLife => CondenseFrames + FallFrames + AfterFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>帘底地面 Y（各端对同一地形独立求得，结论一致）</summary>
        private float groundY;
        /// <summary>本机玩家触帘结算间隔（每端只管自己的本机玩家，实例字段即可）</summary>
        private int localHitCooldown;
        /// <summary>预告滴答声计时</summary>
        private int dripTimer;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1280;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//伤害走本机 Hurt 结算，不走碰撞管线
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>群系代表性接触伤害：血爬怪/血泊怪一线，随世界难度取敌伤倍率（档位只调频率不调伤害）</summary>
        private static int ComputeTouchDamage() {
            int baseContact = Main.hardMode ? 34 : 28;
            int mult = Main.masterMode ? 3 : Main.expertMode ? 2 : 1;
            return Math.Max(6, (int)(baseContact * mult * DamageFrac));
        }

        /// <summary>从帘顶向下找地面（帘底收口与溅带位置）</summary>
        private void RefreshGroundY() {
            int tileX = (int)(Projectile.Center.X / 16f);
            int startY = (int)(Projectile.Center.Y / 16f) + 1;
            for (int dy = 0; dy < GroundSearchTiles; dy++) {
                int tileY = startY + dy;
                if (!WorldGen.InWorld(tileX, tileY, 10)) {
                    break;
                }
                if (WorldGen.SolidTile(tileX, tileY)) {
                    groundY = tileY * 16f;
                    return;
                }
            }
            groundY = Projectile.Center.Y + GroundSearchTiles * 16f;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //时间轴由常量决定，两端各自展开（镜像 WastesIceSlickZone 的 timeLeft 惯例）
                Projectile.timeLeft = TotalLife;
                RefreshGroundY();
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            if (elapsed % 8 == 0) {
                RefreshGroundY();
            }
            if (localHitCooldown > 0) {
                localHitCooldown--;
            }

            bool falling = elapsed >= CondenseFrames && elapsed < CondenseFrames + FallFrames;

            //==== 本机玩家判伤（仅雨帘期；受击端本机结算，Boss 在场停摆）====
            if (falling && !Main.dedServ && !CWRWorld.HasBoss) {
                TryHurtLocalPlayer();
            }

            if (Main.dedServ) {
                return;
            }

            //==== 客户端演出 ====
            if (elapsed < CondenseFrames) {
                UpdateCondenseFx(elapsed);
            }
            else if (elapsed == CondenseFrames) {
                //落帘帧：层叠水声 + 首波血珠
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    SpawnDrop();
                }
            }
            else if (falling) {
                UpdateFallFx(elapsed);
            }
            else if (elapsed == CondenseFrames + FallFrames) {
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.5f, MaxInstances = 4 }, GroundPoint(0f));
            }
            else {
                UpdateAfterFx(elapsed);
            }
        }

        /// <summary>触帘判定：X 在帘宽内、人在帘顶之下，且从帘顶到头顶无瓦面遮挡（檐下/室内即免疫）</summary>
        private void TryHurtLocalPlayer() {
            Player player = Main.LocalPlayer;
            if (!player.active || player.dead || player.ghost || localHitCooldown > 0) {
                return;
            }
            if (Math.Abs(player.Center.X - Projectile.Center.X) > HalfWidth + player.width * 0.5f) {
                return;
            }
            if (player.Center.Y <= Projectile.Center.Y || player.Center.Y > groundY + 48f) {
                return;
            }
            Vector2 skyPoint = new(player.Center.X, Projectile.Center.Y);
            Vector2 headPoint = new(player.Center.X, player.position.Y);
            if (!Collision.CanHitLine(skyPoint, 1, 1, headPoint, 1, 1)) {
                return;
            }

            localHitCooldown = HitIntervalFrames;
            player.Hurt(PlayerDeathReason.ByProjectile(-1, Projectile.whoAmI), ComputeTouchDamage(), 0);
            player.AddBuff(BuffID.Bleeding, BleedFrames);
            //受击反馈：血花 + 湿滑闷响
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, player.Center);
            for (int i = 0; i < 6; i++) {
                Dust splat = Dust.NewDustPerfect(player.Top + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f),
                    DustID.Blood, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(0.5f, 2.4f)),
                    60, default, Main.rand.NextFloat(1f, 1.4f));
                splat.noGravity = false;
            }
        }

        /// <summary>凝聚期：血珠向凝核收拢 + 滴答声渐密 + 微光渐急（视觉/听觉双通道）</summary>
        private void UpdateCondenseFx(int elapsed) {
            float progress = elapsed / (float)CondenseFrames;
            //收拢粉尘（≤2 粒/帧）
            for (int i = 0; i < 2; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                float radius = MathHelper.Lerp(96f, 26f, progress);
                Vector2 offset = Main.rand.NextVector2CircularEdge(radius, radius * 0.6f);
                Dust gather = Dust.NewDustPerfect(Projectile.Center + offset, DustID.CrimsonTorch,
                    -offset * 0.045f, 120, default, 0.9f + 0.4f * progress);
                gather.noGravity = true;
            }
            //滴答渐密：间隔 15 帧收紧到 4 帧
            if (--dripTimer <= 0) {
                dripTimer = 15 - (int)(11f * progress);
                SoundEngine.PlaySound(SoundID.Drip with {
                    Volume = 0.26f + 0.22f * progress,
                    Pitch = -0.35f + 0.45f * progress,
                    MaxInstances = 5,
                }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.24f, 0.04f, 0.05f) * (0.4f + 0.6f * progress));
        }

        /// <summary>雨帘期：持续血珠 + 地面溅答声 + 凝核渐排空</summary>
        private void UpdateFallFx(int elapsed) {
            //血珠 1~2 滴/帧（PRT 池有 240 上限兜底）
            SpawnDrop();
            if (Main.rand.Next(5) < 3) {
                SpawnDrop();
            }
            //地面溅答（约 1 次/7 帧，散布在帘宽内）
            if (elapsed % 7 == 0) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.13f,
                    Pitch = Main.rand.NextFloat(-0.25f, 0.1f),
                    MaxInstances = 4,
                }, GroundPoint(Main.rand.NextFloat(-0.8f, 0.8f)));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.2f, 0.035f, 0.04f));
        }

        /// <summary>余韵期：帘停渍留，地面血渍蒸散上浮（≤1 粒/6 帧）</summary>
        private void UpdateAfterFx(int elapsed) {
            if (elapsed % 6 != 0) {
                return;
            }
            Vector2 spot = GroundPoint(Main.rand.NextFloat(-0.9f, 0.9f)) - new Vector2(0f, 2f);
            Dust vapor = Dust.NewDustPerfect(spot, DustID.CrimsonTorch,
                new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.5f, 1.1f)),
                150, default, 0.8f);
            vapor.noGravity = true;
        }

        /// <summary>在帘顶随机横位放一滴血珠（客户端）</summary>
        private void SpawnDrop() {
            float wind = MathHelper.Clamp(Main.windSpeedCurrent * 1.5f, -0.8f, 0.8f);
            Vector2 pos = new(Projectile.Center.X + Main.rand.NextFloat(-HalfWidth, HalfWidth),
                Projectile.Center.Y + Main.rand.NextFloat(0f, 10f));
            PRTLoader.NewParticle<FleshfenPRT_BloodDrop>(pos, new Vector2(wind, 2.5f),
                new Color(132, 22, 24), Main.rand.NextFloat(0.75f, 1.15f))
                ?.Configure(Main.rand.Next(110, 150), wind);
        }

        /// <summary>帘宽内某比例位置的地面点（k∈[-1,1]）</summary>
        private Vector2 GroundPoint(float k) => new(Projectile.Center.X + HalfWidth * k, groundY);

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float condenseK = MathHelper.Clamp(elapsed / (float)CondenseFrames, 0f, 1f);
            float fallT = MathHelper.Clamp((elapsed - CondenseFrames) / (float)FallFrames, 0f, 1f);
            float afterT = MathHelper.Clamp((elapsed - CondenseFrames - FallFrames) / (float)AfterFrames, 0f, 1f);

            Texture2D fog = CWRAsset.Fog?.Value;
            Texture2D sheet = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (fog == null || sheet == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;

            //==== 凝核血云：凝聚期充盈，雨帘期排空，余韵期散尽（宽度有生命周期）====
            float cloudFill = elapsed < CondenseFrames
                ? 0.35f + 0.65f * condenseK
                : MathHelper.Lerp(1f, 0.3f, fallT) * (1f - afterT);
            if (cloudFill > 0.02f) {
                float cloudW = 170f * cloudFill;
                float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + Projectile.identity) * 0.12f;
                //暗红外层双片错位（真 alpha，承担轮廓与实体感）
                for (int i = 0; i < 2; i++) {
                    float side = i == 0 ? 1f : -1f;
                    Vector2 offset = new(side * cloudW * 0.16f, MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + i * 2.4f + Projectile.identity) * 4f);
                    Vector2 scale = new(cloudW / fog.Width * (1f + wobble * side),
                        cloudW * 0.55f / fog.Height);
                    Main.EntitySpriteDraw(fog, center + offset, null, DeepBlood * (0.62f * cloudFill),
                        side * 0.2f + wobble, fog.Size() / 2f, scale, SpriteEffects.None, 0);
                }
                //亮芯闪烁：凝聚期越临期越急（听觉滴答的视觉对拍）
                float flickerRate = elapsed < CondenseFrames ? 8f + 18f * condenseK : 6f;
                float flicker = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * flickerRate + Projectile.identity);
                Color core = BrightBlood with { A = 0 };
                Main.EntitySpriteDraw(glow, center, null, core * (0.42f * cloudFill * flicker), 0f,
                    glow.Size() / 2f, new Vector2(1.5f * cloudFill, 0.9f * cloudFill), SpriteEffects.None, 0);

                //凝聚期悬珠：云底攒出的血珠渐大渐坠（结出实体感的锚点）
                if (elapsed < CondenseFrames) {
                    for (int i = 0; i < 4; i++) {
                        float ox = (i - 1.5f) * cloudW * 0.2f + MathF.Sin(Projectile.identity * 1.3f + i * 2.6f) * 6f;
                        float jig = MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 1.9f + Projectile.identity) * 1.5f;
                        float bead = condenseK * (0.6f + 0.4f * MathF.Sin(Projectile.identity + i * 2.2f));
                        Vector2 beadScale = new(0.10f * bead, 0.18f * bead * (1f + condenseK));
                        Main.EntitySpriteDraw(sheet, center + new Vector2(ox, cloudW * 0.24f + jig + condenseK * 6f),
                            null, new Color(132, 22, 24) * (0.85f * condenseK), 0f,
                            sheet.Size() / 2f, beadScale, SpriteEffects.None, 0);
                    }
                }
            }

            //==== 雨帘：三段透明度阶梯收口（禁整条拉伸硬切），宽度展开→维持→收束 ====
            if (elapsed >= CondenseFrames && afterT <= 0f) {
                float widthEnv = MathHelper.Clamp((elapsed - CondenseFrames) / 18f, 0f, 1f);
                int fallLeft = CondenseFrames + FallFrames - elapsed;
                if (fallLeft < 30) {
                    widthEnv *= fallLeft / 30f;
                }
                if (widthEnv > 0.02f) {
                    float sheetH = groundY - Projectile.Center.Y;
                    float sheetW = HalfWidth * 2f * widthEnv;
                    //段长比 / 段透明度 / 段加宽（下坠散开）
                    ReadOnlySpan<float> segFrac = [0.34f, 0.36f, 0.30f];
                    ReadOnlySpan<float> segAlpha = [1f, 0.78f, 0.6f];
                    ReadOnlySpan<float> segWide = [1f, 1.07f, 1.15f];
                    float cum = 0f;
                    for (int s = 0; s < 3; s++) {
                        float segLen = sheetH * segFrac[s];
                        Vector2 top = center + new Vector2(0f, cum + segLen * 0.5f);
                        Vector2 scale = new(sheetW * segWide[s] / sheet.Width, segLen * 1.1f / sheet.Height);
                        Main.EntitySpriteDraw(sheet, top, null,
                            new Color(88, 14, 16) * (0.30f * widthEnv * segAlpha[s]), 0f,
                            sheet.Size() / 2f, scale, SpriteEffects.None, 0);
                        cum += segLen * 0.94f;//段间 6% 重叠防露缝
                    }
                    //帘底溅带：落点的物理答案（溅开翻起的血雾）
                    Vector2 splashPos = new(center.X, groundY - Main.screenPosition.Y - 4f);
                    Main.EntitySpriteDraw(sheet, splashPos, null, new Color(140, 26, 26) * (0.5f * widthEnv),
                        0f, sheet.Size() / 2f, new Vector2(sheetW * 1.22f / sheet.Width, 11f / sheet.Height),
                        SpriteEffects.None, 0);
                    Color splashGlow = BrightBlood with { A = 0 };
                    Main.EntitySpriteDraw(glow, splashPos, null, splashGlow * (0.26f * widthEnv), 0f,
                        glow.Size() / 2f, new Vector2(sheetW / glow.Width * 1.1f, 0.3f), SpriteEffects.None, 0);
                }
            }

            //==== 余韵：帘底残渍带渐蒸散 ====
            if (afterT > 0f) {
                float linger = 1f - afterT;
                Vector2 splashPos = new(center.X, groundY - Main.screenPosition.Y - 3f);
                Main.EntitySpriteDraw(sheet, splashPos, null, new Color(110, 20, 20) * (0.4f * linger),
                    0f, sheet.Size() / 2f, new Vector2(HalfWidth * 2.1f / sheet.Width, 8f / sheet.Height),
                    SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //收场：残存血汽散尽
            for (int i = 0; i < 4; i++) {
                Dust vapor = Dust.NewDustPerfect(GroundPoint(Main.rand.NextFloat(-0.8f, 0.8f)) - new Vector2(0f, 2f),
                    DustID.CrimsonTorch, new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f)), 160, default, 0.7f);
                vapor.noGravity = true;
            }
        }
    }
}
