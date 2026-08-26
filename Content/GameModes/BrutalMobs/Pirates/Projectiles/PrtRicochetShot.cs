using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles
{
    /// <summary>
    /// 神射手跳弹：单实体两相位。ai[0]=射手NPC索引 ai[1]=风味(0神射手/1弩手) ai[2]=相位(0预演/1飞行，服务端翻转并同步)。<br/>
    /// 预演期原地冻结、永不判定，沿出生即锁死的方向画"直段-反弹点-折段"折线弹道预演
    /// （几何由各端从同步的出生位置+velocity对同步物块确定性重算，预告即承诺，全程不再重瞄）；<br/>
    /// 飞行期沿承诺方向出膛，墙面一次反弹，行程与预演等长，越线即消——危险只存在于画过的折线上
    /// </summary>
    internal class PrtRicochetShot : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==== 预演（公平阀门：预告时长 + 出生锁向不追踪 + 行程封顶=预演线长）====
        /// <summary>预演帧数（风味 0 神射手 / 1 弩手，一律 ≥30 帧契约）</summary>
        internal static readonly int[] TelegraphFramesByFlavor = [36, 44];
        /// <summary>直段最大行程（预演与飞行读同一常量）</summary>
        internal const float MaxTravel = 720f;
        /// <summary>反弹后最大行程（预演与飞行读同一常量）</summary>
        internal const float RicochetTravel = 520f;
        /// <summary>折线扫描步长</summary>
        private const float ScanStep = 8f;
        /// <summary>几何重算间隔帧（预演期间物块可能被挖改）</summary>
        private const int RecalcInterval = 8;

        //==== 弹体 ====
        /// <summary>预演用的原版弹体贴图（风味 0 铅弹 / 1 弩矢），同时就是飞行期弹体</summary>
        private static readonly int[] DonorProj = [ProjectileID.BulletDeadeye, ProjectileID.WoodenArrowHostile];
        /// <summary>弩矢命中的流血时长（风味差异，不随档位变）</summary>
        private const int BoltBleedTicks = 90;

        private static readonly Color PowderGold = new Color(255, 208, 120);
        private static readonly Color BounceRed = new Color(255, 96, 72);

        private int ShooterIndex => (int)Projectile.ai[0];
        private int Flavor => (int)Projectile.ai[1];
        private bool InFlight => Projectile.ai[2] == 1f;
        private int ExpectedShooterType => Flavor == 1 ? NPCID.PirateCrossbower : NPCID.PirateDeadeye;
        private int TelegraphFrames => TelegraphFramesByFlavor[Flavor == 1 ? 1 : 0];

        private ref float Age => ref Projectile.localAI[0];

        //折线几何缓存（各端本地重算，输入全是同步原语，跨端确定一致）
        private Vector2 bouncePoint;
        private Vector2 bounceDir;
        private Vector2 endPoint;
        private bool hasBounce;
        /// <summary>折线缓存是否已解算过（迟入端可能直接进入飞行相位，从未算过折线）</summary>
        private bool pathValid;
        private int recalcTimer;
        //飞行行程记录（各端本地推进，速度与相位同步保证一致）
        private float traveled;
        private float traveledAfterBounce;
        private bool bounced;
        private bool flightWitnessed;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 960;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 320;
            Projectile.netImportant = true;
        }

        /// <summary>预演期冻结在枪口（几何承诺不随射手走位漂移），飞行期才吃 velocity</summary>
        public override bool ShouldUpdatePosition() => InFlight;

        /// <summary>伤害窗=可见窗：只有飞行期的实体弹有杀伤，预演线永不判定</summary>
        public override bool? CanDamage() => InFlight ? null : false;

        public override void AI() {
            Age++;

            if (!InFlight) {
                //预演相位。服务端负责射手校验与相位翻转；折线几何各端自行重算
                if (!VaultUtils.isClient) {
                    if (!(ShooterIndex.TryGetNPC(out NPC shooter) && shooter.type == ExpectedShooterType)) {
                        //射手没了（死亡/槽位易主）：这一枪不会发生，预演消散
                        Projectile.Kill();
                        return;
                    }
                    if (Age >= TelegraphFrames) {
                        Projectile.ai[2] = 1f;
                        Projectile.tileCollide = true;
                        Projectile.netUpdate = true;
                    }
                }

                if (--recalcTimer <= 0) {
                    recalcTimer = RecalcInterval;
                    ComputePath();
                }

                //反弹点火花：预演期低频勾点（≤2 粒/帧）
                if (!Main.dedServ && hasBounce && Main.rand.NextBool(3)) {
                    Dust spark = Dust.NewDustPerfect(bouncePoint + Main.rand.NextVector2Circular(5f, 5f),
                        DustID.Torch, bounceDir * Main.rand.NextFloat(0.4f, 1.2f), 120, default, 0.8f);
                    spark.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, PowderGold.ToVector3() * 0.14f);
                return;
            }

            //飞行相位入口帧：各端本地检测相位翻转补出膛表现；
            //迟入端首帧即已在飞（未目击翻转沿），跳过音效防错播
            if (!flightWitnessed) {
                flightWitnessed = true;
                Projectile.tileCollide = true;
                if (Age > 1f && !Main.dedServ) {
                    SoundEngine.PlaySound((Flavor == 1 ? SoundID.Item5 : SoundID.Item11)
                        with { Volume = 0.5f, MaxInstances = 5 }, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Dust muzzle = Dust.NewDustPerfect(Projectile.Center,
                            DustID.Torch, Projectile.velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.1f, 0.35f),
                            100, default, Main.rand.NextFloat(0.8f, 1.3f));
                        muzzle.noGravity = true;
                    }
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //行程封顶：危险不越过预演画到的位置（直段没撞墙则到 MaxTravel 即消散）
            float step = Projectile.velocity.Length();
            traveled += step;
            if (bounced) {
                traveledAfterBounce += step;
                if (traveledAfterBounce >= RicochetTravel) {
                    Projectile.Kill();
                    return;
                }
            }
            else if (traveled >= MaxTravel + ScanStep * 2f) {
                Projectile.Kill();
                return;
            }

            //火药余迹（≤1 粒/帧）
            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    -Projectile.velocity * 0.05f, 160, default, 0.7f);
                trail.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, PowderGold.ToVector3() * 0.18f);
        }

        /// <summary>一次反弹：镜面反射后继续飞，第二次触墙即碎（行程另由 RicochetTravel 封顶）</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (bounced) {
                return true;
            }
            bounced = true;
            traveledAfterBounce = 0f;
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y;
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 4; i++) {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Iron,
                        Projectile.velocity.RotatedByRandom(0.6f) * Main.rand.NextFloat(0.2f, 0.5f), 60, default, 1f);
                    spark.noGravity = true;
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Flavor == 1) {
                target.AddBuff(BuffID.Bleeding, BoltBleedTicks);
            }
        }

        /// <summary>
        /// 折线弹道解算：从当前冻结位置沿承诺方向步进扫描首个实心物块，
        /// 按试探步判定反弹轴做镜面反射，再扫折段终点。输入只有同步位置/速度与同步物块，跨端一致
        /// </summary>
        private void ComputePath() {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 pos = Projectile.Center;
            hasBounce = false;
            pathValid = true;
            bounceDir = dir;

            float scanned = 0f;
            while (scanned < MaxTravel) {
                Vector2 next = pos + dir * ScanStep;
                if (Collision.SolidCollision(next - new Vector2(2f, 2f), 4, 4)) {
                    hasBounce = true;
                    bouncePoint = pos;
                    //反弹轴：沿单轴试探哪个方向撞实
                    bool hitX = Collision.SolidCollision(pos + new Vector2(dir.X * ScanStep, 0f) - new Vector2(2f, 2f), 4, 4);
                    bool hitY = Collision.SolidCollision(pos + new Vector2(0f, dir.Y * ScanStep) - new Vector2(2f, 2f), 4, 4);
                    Vector2 reflect = dir;
                    if (hitX) {
                        reflect.X = -reflect.X;
                    }
                    if (hitY) {
                        reflect.Y = -reflect.Y;
                    }
                    if (!hitX && !hitY) {
                        reflect = -dir;
                    }
                    bounceDir = reflect;
                    break;
                }
                pos = next;
                scanned += ScanStep;
            }

            if (!hasBounce) {
                //直段打满没撞墙：没有折段，预演只画直线（飞行期同样在 MaxTravel 消散）
                bouncePoint = pos;
                endPoint = pos;
                return;
            }

            Vector2 tail = bouncePoint;
            float scanned2 = 0f;
            while (scanned2 < RicochetTravel) {
                Vector2 next = tail + bounceDir * ScanStep;
                if (Collision.SolidCollision(next - new Vector2(2f, 2f), 4, 4)) {
                    break;
                }
                tail = next;
                scanned2 += ScanStep;
            }
            endPoint = tail;
        }

        public override bool PreDraw(ref Color lightColor) {
            int donor = DonorProj[Flavor == 1 ? 1 : 0];
            Main.instance.LoadProjectile(donor);
            Texture2D bulletTex = TextureAssets.Projectile[donor].Value;
            Vector2 bulletOrigin = bulletTex.Size() * 0.5f;

            if (!InFlight) {
                DrawPolyline();
                //反弹点节点：幽灵弹体停在折点指向折段，宣告"弹会从这里拐"
                if (hasBounce) {
                    float urgency = MathHelper.Clamp(Age / TelegraphFrames, 0f, 1f);
                    float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);
                    Main.EntitySpriteDraw(bulletTex, bouncePoint - Main.screenPosition, null,
                        BounceRed * (0.55f * urgency * pulse), bounceDir.ToRotation() + MathHelper.PiOver2,
                        bulletOrigin, 0.9f, SpriteEffects.None, 0);
                }
                return false;
            }

            //飞行期：残留预演线极淡托底（反弹可读），同材质拖尾 + 弹体
            DrawPolyline(residual: true);
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(bulletTex, oldDrawPos, null,
                    Color.Lerp(PowderGold, lightColor, 0.4f) * (0.5f * t),
                    Projectile.rotation, bulletOrigin, 0.85f * (0.5f + 0.5f * t), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(bulletTex, Projectile.Center - Main.screenPosition, null,
                Color.Lerp(lightColor, Color.White, 0.35f), Projectile.rotation,
                bulletOrigin, 1f, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>折线预演：直段亮、折段偏红且渐弱，两段中间以反弹节点衔接</summary>
        private void DrawPolyline(bool residual = false) {
            if (!pathValid) {
                return;
            }
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            float fadeIn = MathHelper.Clamp(Age / 10f, 0f, 1f);
            float urgency = MathHelper.Clamp(Age / TelegraphFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity * 0.8f);
            float strength = residual ? 0.1f : fadeIn * (0.4f + 0.45f * urgency) * pulse;
            if (strength <= 0.01f) {
                return;
            }

            Vector2 origin = new Vector2(0f, line.Height / 2f);
            //直段
            Vector2 segA = bouncePoint - Projectile.Center;
            if (segA.Length() > 4f) {
                Main.EntitySpriteDraw(line, Projectile.Center - Main.screenPosition, null,
                    PowderGold with { A = 0 } * strength, segA.ToRotation(), origin,
                    new Vector2(segA.Length() / line.Width, 14f / line.Height), SpriteEffects.None, 0);
            }
            //折段：这是本机制的招牌，颜色偏红提示"反弹后仍是威胁"
            if (hasBounce) {
                Vector2 segB = endPoint - bouncePoint;
                if (segB.Length() > 4f) {
                    Main.EntitySpriteDraw(line, bouncePoint - Main.screenPosition, null,
                        BounceRed with { A = 0 } * (strength * 0.85f), segB.ToRotation(), origin,
                        new Vector2(segB.Length() / line.Width, 12f / line.Height), SpriteEffects.None, 0);
                }
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            if (!InFlight) {
                behindNPCsAndTiles.Add(index);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust chip = Dust.NewDustPerfect(Projectile.Center, DustID.Iron,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.8f) * Main.rand.NextFloat(1f, 3f),
                    80, default, 0.9f);
                chip.noGravity = Main.rand.NextBool();
            }
        }
    }
}
