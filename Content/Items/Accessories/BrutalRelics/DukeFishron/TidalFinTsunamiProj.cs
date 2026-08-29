using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.DukeFishron
{
    /// <summary>
    /// 潮汐之鳍的海啸尾迹：沿冲刺直线立起的持留水墙，复用 FishronTsunami 浪墙语汇。
    /// 起点=生成位置，velocity=单位冲刺方向（不位移），墙头由主人位置投影推进；
    /// 冲刺是锁向直线，各端从同步的起点+方向确定性重建同一面墙。
    /// ai[0]=段序(1..4) ai[1]=强化(0/1) localAI[0]=寿命计时
    /// </summary>
    internal class TidalFinTsunamiProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>跟录窗口：冲刺+刹车的帧数预算</summary>
        private const int RecordTime = TidalFinPlayer.DashTime + 4;
        private const int HoldTime = 210;
        private const int CollapseTime = 45;
        /// <summary>段间距与段数上限：nominal 冲刺 ~600px</summary>
        private const float Stride = 110f;
        private const int MaxSegments = 8;
        private const float MaxLen = Stride * MaxSegments;
        /// <summary>单段画布：宽 2 倍段距重叠成连续浪列，高含 30% 冠上抛沫留白</summary>
        private const float QuadW = 230f;
        private const float QuadH = 300f;
        /// <summary>裙摆压在冲刺线下方的深度</summary>
        private const float Skirt = 58f;
        /// <summary>全部潮汐墙对同一目标共享的跳伤间隔（帧）</summary>
        internal const int SharedHitCooldown = 12;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        //本帧推弹候选缓存：全弹幕表每帧只扫一趟，其余墙读缓存做各自法线推离。
        //纯本端预演/表现数据（敌弹权威在服务端，客户端本就是预演），static 合法
        private static uint pushScanFrame;
        private static readonly List<int> pushCandidates = new();

        private int Stage => (int)Projectile.ai[0];
        private bool Empowered => Projectile.ai[1] >= 1f;
        private ref float LifeTimer => ref Projectile.localAI[0];

        /// <summary>墙头已推进长度：主人位置在冲刺线上的投影，单调不回退（各端本地重建）</summary>
        private float headDist;

        /// <summary>判定窗口：跟录+持留期激活，溃散期关闭</summary>
        private bool HitActive => Projectile.timeLeft > CollapseTime;

        public override void SetStaticDefaults() {
            //墙体最长 880px + 画布高出命中盒：余量不足会在头部出屏时整墙瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = RecordTime + HoldTime + CollapseTime;
            Projectile.DamageType = DamageClass.Generic;
            //持留伤害区：本地免疫表 12 帧一跳（跨墙共享冷却另见 CanHitNPC）
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = SharedHitCooldown;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>绘制基向：X 非负，保证贴图"上方"恒朝世界上方</summary>
        private static Vector2 AlongOf(Vector2 dir) => dir.X >= 0f ? dir : -dir;

        /// <summary>浪体立起的一侧（屏幕上方向的垂线）</summary>
        private static Vector2 UpOf(Vector2 dir) {
            Vector2 along = AlongOf(dir);
            return new Vector2(along.Y, -along.X);
        }

        public override void AI() {
            LifeTimer++;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 up = UpOf(dir);

            //长寿命弹幕：owner 端逐帧按面板刷新单跳伤害（含雨天强化倍率）
            if (Projectile.owner == Main.myPlayer) {
                float mult = Empowered ? TidalFinPlayer.EmpowerMult : 1f;
                Projectile.damage = (int)Main.player[Projectile.owner]
                    .GetTotalDamage(DamageClass.Generic).ApplyTo(TidalFinPlayer.WallDamage * mult);
            }

            if (LifeTimer == 1f) {
                FirstFrameFX(dir);
            }

            //跟录期：墙头追着主人推进
            if (LifeTimer <= RecordTime) {
                Player owner = Main.player[Projectile.owner];
                if (owner != null && owner.active && !owner.dead) {
                    float proj = Vector2.Dot(owner.Center - Projectile.Center, dir);
                    headDist = Math.Max(headDist, MathHelper.Clamp(proj, 0f, MaxLen));
                }
                //冲刺头甩水：旁观端也由此看到冲刺本身
                if (!VaultUtils.isServer && LifeTimer % 2 == 0 && headDist > 8f) {
                    Vector2 head = Projectile.Center + dir * headDist;
                    FishronMotionFX.SpawnSprayCone(head, -dir, 1, 3f, 8f, 0.55f, 0.85f);
                }
            }

            if (HitActive && headDist > 40f) {
                PushHostileProjectiles(dir, up);
            }

            UpdateAmbience(dir, up);
        }

        /// <summary>爆发帧：正交水环+闷响（各端本地）；强化链首段附带风暴天光一闪</summary>
        private void FirstFrameFX(Vector2 dir) {
            FishronMotionFX.SpawnDashBurst(Projectile.Center, dir, 0.8f + Stage * 0.06f);
            if (Stage == 1 && Empowered && FishronMotionFX.OnScreen(Projectile.Center, 1000f)) {
                TidalFinStormFlashRender.Push(Projectile.Center);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.45f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
            }
        }

        /// <summary>
        /// 推开/减速带内的敌方弹幕。白名单三条（只碰"可清理"级别的普通弹）：
        /// 1. 敌对且带伤害；2. 在动（速度>0.5，静止场/锚定体不碰）；
        /// 3. 小体量（≤80px，巨型激光/墙柱类不碰），另排除吸附式死亡射线(aiStyle 84)。
        /// 规则确定性、无随机，各端同步自算，不发包
        /// </summary>
        private void PushHostileProjectiles(Vector2 dir, Vector2 up) {
            //本帧第一面墙扫全表建候选清单，其余墙直接消费（至多 4 墙并存时省 3 趟全表）
            if (pushScanFrame != Main.GameUpdateCount) {
                pushScanFrame = Main.GameUpdateCount;
                RebuildPushCandidates();
            }

            Vector2 a = Projectile.Center;
            //墙体中面：冲刺线上抬 55px（浪体主体所在）
            Vector2 planeOffset = up * 55f;
            for (int i = 0; i < pushCandidates.Count; i++) {
                Projectile p = Main.projectile[pushCandidates[i]];
                //槽位复用防线：消费前复核白名单核心项（含吸附式死亡射线排除，防缓存帧内变形漏推）
                if (!p.active || !p.hostile || p.friendly || p.damage <= 0 || p.aiStyle == 84) {
                    continue;
                }

                //点到墙段最近点
                float t = MathHelper.Clamp(Vector2.Dot(p.Center - a, dir), 0f, headDist);
                Vector2 closest = a + dir * t + planeOffset;
                Vector2 offset = p.Center - closest;
                if (offset.LengthSquared() > 130f * 130f) {
                    continue;
                }

                //沿墙面法线向外推，同时整体减速
                float side = Vector2.Dot(offset, up) >= 0f ? 1f : -1f;
                p.velocity *= 0.90f;
                p.velocity += up * (side * 0.85f);
                if (p.velocity.LengthSquared() > 24f * 24f) {
                    p.velocity = p.velocity.SafeNormalize(Vector2.Zero) * 24f;
                }
            }
        }

        /// <summary>候选重建：白名单三条原样（敌对带伤/在动/小体量，排除 aiStyle 84），确定性无随机</summary>
        private static void RebuildPushCandidates() {
            pushCandidates.Clear();
            foreach (var p in Main.ActiveProjectiles) {
                if (!p.hostile || p.friendly || p.damage <= 0) {
                    continue;
                }
                if (p.aiStyle == 84 || p.width > 80 || p.height > 80) {
                    continue;
                }
                if (p.velocity.LengthSquared() < 0.25f) {
                    continue;
                }
                pushCandidates.Add(p.whoAmI);
            }
        }

        /// <summary>浪冠喷雾/塌落碎沫/湿光与浪声（纯客户端）</summary>
        private void UpdateAmbience(Vector2 dir, Vector2 up) {
            if (VaultUtils.isServer || headDist < 30f) {
                return;
            }
            float env = HitActive ? 1f : MathHelper.Clamp(Projectile.timeLeft / (float)CollapseTime, 0f, 1f);

            //浪冠沿线随机冒雾与前抛水珠
            if (Main.rand.NextBool(2)) {
                float d = Main.rand.NextFloat(headDist);
                Vector2 crest = Projectile.Center + dir * d + up * Main.rand.NextFloat(90f, 165f);
                FishronMotionFX.SpawnSprayCone(crest, (dir * 0.8f + up * 0.7f).SafeNormalize(up),
                    1, 1.5f, 5f, 0.6f, 0.55f * env);
            }
            //溃散期：塌落的密集碎沫，浪是塌下去的不是淡出去的
            if (!HitActive && Main.rand.NextBool(2)) {
                float d = Main.rand.NextFloat(headDist);
                Vector2 pos = Projectile.Center + dir * d + up * Main.rand.NextFloat(-30f, 120f);
                InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(pos,
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2f)),
                    FishronMotionFX.FoamWhite * 0.45f, Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(Main.rand.Next(20, 36), Main.rand.NextFloat(-0.04f, 0.04f));
            }
            //湿光沿线轮询
            int lightStep = (int)(Main.GameUpdateCount % 4);
            Vector2 lightPos = Projectile.Center + dir * (headDist * (0.2f + lightStep * 0.2f));
            Lighting.AddLight(lightPos, FishronMotionFX.SeaGreen.ToVector3() * 0.4f * env);

            //浪涌低鸣
            if (LifeTimer % 34 == 0 && env > 0.4f) {
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.4f, Pitch = -0.55f, MaxInstances = 3 },
                    Projectile.Center + dir * headDist * 0.5f);
            }
        }

        public override bool? CanHitNPC(NPC target) {
            if (!HitActive || headDist < 40f) {
                return false;
            }
            //跨墙共享跳伤：多面墙叠同一目标时跳伤频率与单墙相同（owner 判定端本地读写）
            if (!target.GetGlobalNPC<TidalFinWallHitNPC>().WallHitReady) {
                return false;
            }
            return null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //任一墙命中即刷新共享冷却
            target.GetGlobalNPC<TidalFinWallHitNPC>().StampWallHit();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!HitActive || headDist < 40f) {
                return false;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 up = UpOf(dir);
            //判定带沿墙体中面铺开：线上 165px / 线下 55px，藏在可见浪体之内
            Vector2 a = Projectile.Center + up * 55f;
            Vector2 b = a + dir * headDist;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                a, b, 220f, ref collisionPoint);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (headDist < 24f) {
                return false;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 along = AlongOf(dir);
            Vector2 up = UpOf(dir);
            float uDir = dir.X >= 0f ? 1f : -1f;
            float rot = along.ToRotation();

            Effect effect = EffectLoader.FishronTsunami?.Value;
            if (effect == null || noiseTex == null) {
                DrawSpriteFallback(dir, up);
                return false;
            }

            int segCount = Math.Clamp((int)MathF.Ceiling(headDist / Stride), 1, MaxSegments);
            float collapseElapsed = Projectile.timeLeft < CollapseTime
                ? CollapseTime - Projectile.timeLeft : 0f;

            //强化链的浪身向雷光青轻推
            Vector3 seaCol = Empowered
                ? Vector3.Lerp(FishronMotionFX.SeaGreen.ToVector3(), FishronMotionFX.StormBolt.ToVector3(), 0.22f)
                : FishronMotionFX.SeaGreen.ToVector3();

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图（合同同 FishronTsunamiWallProj）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new(QuadW / pixel.Width, QuadH / pixel.Height);

            for (int i = 0; i < segCount; i++) {
                //段生长跟随墙头推进：浪在玩家身后一段段立起来
                float grown = (headDist - i * Stride) / (Stride * 1.35f);
                float growth = MathHelper.Clamp(grown, 0f, 1f);
                if (growth <= 0.02f) {
                    continue;
                }
                //溃散自起点端先蚀，逐段错帧
                float segCollapse = MathHelper.Clamp((collapseElapsed - i * 2.5f) / 34f, 0f, 1f);
                if (segCollapse >= 0.99f) {
                    continue;
                }

                float intensity = (0.4f + 0.6f * growth) * (1f - segCollapse * 0.35f);
                //持留期墙头端帽：最前段未长满时按生长度斜降，端面不读斜切
                if (i == segCount - 1 && growth < 1f && LifeTimer > RecordTime) {
                    intensity *= growth;
                }
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uIntensity"]?.SetValue(intensity);
                effect.Parameters["uGrowth"]?.SetValue(growth);
                effect.Parameters["uCollapse"]?.SetValue(segCollapse);
                effect.Parameters["uDir"]?.SetValue(uDir);
                effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.313f + i * 0.71f);
                effect.Parameters["uDeepColor"]?.SetValue(FishronMotionFX.DeepSea.ToVector3());
                effect.Parameters["uFoamColor"]?.SetValue(FishronMotionFX.FoamWhite.ToVector3());
                effect.Parameters["uSeaColor"]?.SetValue(seaCol);
                effect.CurrentTechnique.Passes[0].Apply();

                Vector2 linePos = Projectile.Center + dir * ((i + 0.5f) * Stride);
                Vector2 drawCenter = linePos + up * (QuadH * 0.5f - Skirt);
                sb.Draw(pixel, drawCenter - Main.screenPosition, null, Color.White,
                    rot, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失兜底：真 alpha 雾团沿线堆出暗水墙体，只求带伤害的墙不隐形</summary>
        private void DrawSpriteFallback(Vector2 dir, Vector2 up) {
            Texture2D puff = CWRAsset.Fog?.Value;
            if (puff == null) {
                return;
            }
            float env = HitActive ? 1f : MathHelper.Clamp(Projectile.timeLeft / (float)CollapseTime, 0f, 1f);
            int count = Math.Clamp((int)(headDist / Stride) + 1, 1, MaxSegments);
            float rot = dir.ToRotation();
            for (int i = 0; i < count; i++) {
                Vector2 pos = Projectile.Center + dir * (i + 0.5f) * Stride + up * (QuadH * 0.4f) - Main.screenPosition;
                Color deep = Color.Lerp(FishronMotionFX.DeepSea, FishronMotionFX.SeaGreen, i / (float)count);
                Main.EntitySpriteDraw(puff, pos, null, deep * (0.72f * env), rot, puff.Size() / 2f,
                    new Vector2(Stride / puff.Width * 1.4f, QuadH * 0.8f / puff.Height), SpriteEffects.None, 0);
                //冠线白沫：暗水体顶上一层薄沫，读出"这是水"
                Main.EntitySpriteDraw(puff, pos + up * (QuadH * 0.30f), null,
                    FishronMotionFX.FoamWhite * (0.34f * env), rot, puff.Size() / 2f,
                    new Vector2(Stride / puff.Width * 1.3f, QuadH * 0.18f / puff.Height), SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>
    /// 潮汐墙共享跳伤账本：记录该 NPC 最近一次被任意潮汐墙命中的帧。
    /// 命中判定与结算都在 owner 端，本字段只在同一端读写，无需同步
    /// </summary>
    internal class TidalFinWallHitNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private uint lastWallHitFrame;
        private bool everHit;

        /// <summary>共享冷却已过，允许任意墙结算下一跳</summary>
        public bool WallHitReady
            => !everHit || Main.GameUpdateCount - lastWallHitFrame >= TidalFinTsunamiProj.SharedHitCooldown;

        public void StampWallHit() {
            lastWallHitFrame = Main.GameUpdateCount;
            everHit = true;
        }
    }
}
