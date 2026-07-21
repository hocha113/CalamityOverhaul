using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Stones;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>热带梭鱼技能，边缘鱼群横穿屏幕，涌动预告 → 高速呼啸横穿 → 水雾气泡余波</summary>
    internal class FishTropicalBarracuda : FishSkill
    {
        public override int UnlockFishID => ItemID.TropicalBarracuda;
        public override int DefaultCooldown => 15;
        public override int ResearchDuration => 60 * 16;

        private int spawnCounter = 0;
        private static int SpawnInterval => 5 - HalibutData.GetDomainLayer() / 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            spawnCounter++;

            if (spawnCounter >= SpawnInterval && Cooldown <= 0) {
                spawnCounter = 0;
                SetCooldown();

                //从屏幕边缘生成鱼群
                SpawnBarracudaSchool(player, source, damage, knockback);
            }

            return null;
        }

        private void SpawnBarracudaSchool(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //鱼群数量
            int schoolSize = 3 + HalibutData.GetDomainLayer() / 2;

            //随机选择一个边缘方向
            int edge = Main.rand.Next(4); //0=左, 1=右, 2=上, 3=下
            Vector2 spawnSide = GetSpawnEdge(edge, player);
            Vector2 targetSide = GetTargetEdge(edge, player);
            Vector2 travelDir = (targetSide - spawnSide).SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < schoolSize; i++) {
                Vector2 spawnPos = GetScatteredPosition(spawnSide, edge, i, schoolSize);
                Vector2 targetPos = GetScatteredPosition(targetSide, edge, i, schoolSize);

                //计算速度方向
                Vector2 direction = (targetPos - spawnPos).SafeNormalize(Vector2.Zero);
                float speed = Main.rand.NextFloat(20f, 28f);

                //预告段慢漂进场，冲刺速度写在 ai2，出闸帧一口气拉满
                int barracudaProj = Projectile.NewProjectile(
                    source,
                    spawnPos,
                    direction * 0.9f,
                    ModContent.ProjectileType<TropicalBarracudaProjectile>(),
                    (int)(damage * (1f + HalibutData.GetDomainLayer() * 0.25f)),
                    knockback * 1.2f,
                    player.whoAmI,
                    ai0: i / (float)schoolSize, //条纹身份与出闸错拍种子
                    ai2: speed
                );

                if (barracudaProj >= 0) {
                    Main.projectile[barracudaProj].netUpdate = true;
                }
            }

            //入场侧屏缘涌动预告
            Vector2 lineCenter = GetVisibleEdgeCenter(edge);
            Vector2 tangent = edge <= 1 ? Vector2.UnitY : Vector2.UnitX;
            FishBarracudaVFX.EdgeTelegraph(lineCenter, tangent, travelDir, 560f, TropicalBarracudaProjectile.TelegraphTicks);

            //低沉水涌
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.55f,
                Pitch = -0.5f
            }, lineCenter);
        }

        /// <summary>入场边缘在屏内的可见锚线中心（内缩 30px）</summary>
        private Vector2 GetVisibleEdgeCenter(int edge) {
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
            const float inset = 30f;

            return edge switch {
                0 => new Vector2(Main.screenPosition.X + inset, screenCenter.Y),
                1 => new Vector2(Main.screenPosition.X + Main.screenWidth - inset, screenCenter.Y),
                2 => new Vector2(screenCenter.X, Main.screenPosition.Y + inset),
                3 => new Vector2(screenCenter.X, Main.screenPosition.Y + Main.screenHeight - inset),
                _ => screenCenter
            };
        }

        private Vector2 GetSpawnEdge(int edge, Player player) {
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
            float offset = 100f;

            return edge switch {
                0 => new Vector2(Main.screenPosition.X - offset, screenCenter.Y), //左
                1 => new Vector2(Main.screenPosition.X + Main.screenWidth + offset, screenCenter.Y), //右
                2 => new Vector2(screenCenter.X, Main.screenPosition.Y - offset), //上
                3 => new Vector2(screenCenter.X, Main.screenPosition.Y + Main.screenHeight + offset), //下
                _ => screenCenter
            };
        }

        private Vector2 GetTargetEdge(int edge, Player player) {
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
            float offset = 100f;

            return edge switch {
                0 => new Vector2(Main.screenPosition.X + Main.screenWidth + offset, screenCenter.Y), //左->右
                1 => new Vector2(Main.screenPosition.X - offset, screenCenter.Y), //右->左
                2 => new Vector2(screenCenter.X, Main.screenPosition.Y + Main.screenHeight + offset), //上->下
                3 => new Vector2(screenCenter.X, Main.screenPosition.Y - offset), //下->上
                _ => screenCenter
            };
        }

        private Vector2 GetScatteredPosition(Vector2 basePos, int edge, int index, int total) {
            float spread = 400f;
            float offset = (index - total / 2f) * (spread / total);

            return edge switch {
                0 or 1 => basePos + new Vector2(0, offset), //垂直散布
                2 or 3 => basePos + new Vector2(offset, 0), //水平散布
                _ => basePos
            };
        }
    }

    /// <summary>
    /// 热带梭鱼弹幕，三拍屏幕级横穿<br/>
    /// 预告段屏外慢漂（无伤害）→ 出闸帧单帧满速、鱼群错拍破水 → 呼啸段条纹残影链 +
    /// 白沫射流尾迹 + 沿途水雾气泡（余波活得比鱼群久）→ 屏内死亡化水收场<br/>
    /// ai[0]=条纹身份/错拍种子 ai[1]=计时（每 update 递增） ai[2]=冲刺速度
    /// </summary>
    internal class TropicalBarracudaProjectile : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.TropicalBarracuda;

        private ref float ColorOffset => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private ref float RushSpeed => ref Projectile.ai[2];

        private float swimWave = 0f;
        private Trail trail;

        //extraUpdates=2 三倍 update，时间常量按 update 计
        private const int UpdatesPerTick = 3;
        /// <summary>预告拍时长（tick），涌动线寿命与出闸时刻共用</summary>
        public const int TelegraphTicks = 22;
        private const int TelegraphUpdates = TelegraphTicks * UpdatesPerTick;
        private const int StaggerUpdates = 2; //鱼群错拍出闸间隔

        //穿梭参数
        private const float MaxSpeed = 30f;
        private const float Acceleration = 0.5f;

        /// <summary>ai0 派生的伪序号，条纹轮换与错拍出闸种子</summary>
        private int PseudoIndex => (int)(ColorOffset * 16f);
        private int ReleaseUpdate => TelegraphUpdates + PseudoIndex * StaggerUpdates;
        private bool Rushing => Timer >= ReleaseUpdate;
        private Color StripeColor => FishBarracudaVFX.Stripe(PseudoIndex);
        private float SpeedT => MathHelper.Clamp((Projectile.velocity.Length() - 6f) / 24f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 9;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        //预告段不参与伤害，鱼还没进场
        public override bool? CanDamage() => Rushing ? null : false;

        public override void AI() {
            Timer++;

            //预告段
            if (Timer < ReleaseUpdate) {
                return;
            }
            if (Timer == ReleaseUpdate) {
                ReleaseRush();
                return;
            }

            swimWave += 0.25f;

            //加速到最大速度
            if (Projectile.velocity.Length() < MaxSpeed) {
                Projectile.velocity *= 1f + Acceleration * 0.01f;
            }

            //轻微波浪游动
            Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
            float wave = (float)Math.Sin(swimWave) * 2f;
            Projectile.velocity += perpendicular * wave * 0.05f;

            //保持速度方向
            if (Projectile.velocity.LengthSquared() > 1f) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            //轻微追踪最近的敌人
            if (Timer % 10 == 0) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.4f;

                    //限制最大速度
                    if (Projectile.velocity.Length() > MaxSpeed) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MaxSpeed;
                    }
                }
            }

            //条纹色光照，亮度随速度门控
            Lighting.AddLight(Projectile.Center, StripeColor.ToVector3() * (0.3f + 0.5f * SpeedT));

            //呼啸甩尾
            //extraUpdates=2 三倍抽签，几率按 update 折算防全群刷屏
            if (!Main.dedServ) {
                if (Main.rand.NextBool(9)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                        Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                        , -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1.2f, 1.2f)
                        , Main.rand.NextBool(3) ? FishBarracudaVFX.Foam : FishBarracudaVFX.Turquoise
                        , Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(20, 34), 0.1f, 0.94f);
                }
                if (Main.rand.NextBool(18)) {
                    PRTLoader.NewParticle<PRT_FishBarracudaBubble>(
                        Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(10f, 10f)
                        , -Projectile.velocity * 0.02f + Main.rand.NextVector2Circular(0.4f, 0.4f)
                        , FishBarracudaVFX.Foam, Main.rand.NextFloat(0.07f, 0.13f))?.Configure(Main.rand.Next(24, 44));
                }
                if (Main.rand.NextBool(20)) {
                    PRTLoader.NewParticle<PRT_Smoke>(
                        Projectile.Center - Projectile.velocity * 0.8f
                        , -Projectile.velocity * 0.015f + Main.rand.NextVector2Circular(0.4f, 0.4f)
                        , Color.Lerp(FishBarracudaVFX.SeaDeep, FishBarracudaVFX.Turquoise, 0.35f), 0.14f)
                        ?.Configure(Main.rand.Next(30, 46), 0.22f, 0.01f);
                }
            }

            //离开屏幕后消失（出闸站稳后才检查，预告段与破水帧免死）
            if (Timer > ReleaseUpdate + 40 && !OnScreen(200f)) {
                Projectile.Kill();
            }
        }

        /// <summary>出闸帧，单帧拉满冲刺速度；头鱼补齐屏幕级三层破水声与一次顺向克制震屏</summary>
        private void ReleaseRush() {
            float speed = MathHelper.Clamp(RushSpeed, 12f, 34f);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity = dir * speed;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //冲刷拖尾缓存
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Projectile.oldPos[i] = Projectile.position;
            }

            //每条鱼自己的破水，小水花
            SoundEngine.PlaySound(SoundID.Item71 with {
                Volume = 0.22f,
                Pitch = 0.5f,
                MaxInstances = 3
            }, Projectile.Center);
            if (!Main.dedServ) {
                FishBarracudaVFX.BurstSplash(Projectile.Center, dir, 0.5f);
                PRTLoader.NewParticle<PRT_FishBarracudaWake>(Projectile.Center, Vector2.Zero, FishBarracudaVFX.Turquoise, 1f)
                    ?.Configure(Projectile.Center + dir * 30f, Projectile.Center - dir * 70f, 9f, 10);
            }

            if (PseudoIndex != 0) {
                return;
            }
            //头鱼代表整群，三层破水声对齐出闸帧
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.75f, Pitch = 0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, Pitch = 0.4f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.25f }, Projectile.Center);
            if (!Main.dedServ) {
                FishBarracudaVFX.BurstSplash(Projectile.Center, dir, 1.1f);
                //顺行进方向的一次克制震屏，只震拥有者视角
                if (Main.myPlayer == Projectile.owner && CWRServerConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        Main.LocalPlayer.Center, dir, 4f, 6f, 9, 1000f, FullName));
                }
            }
        }

        private bool OnScreen(float margin) {
            Rectangle screenRect = new Rectangle(
                (int)(Main.screenPosition.X - margin),
                (int)(Main.screenPosition.Y - margin),
                Main.screenWidth + (int)(margin * 2f),
                Main.screenHeight + (int)(margin * 2f)
            );
            return screenRect.Contains(Projectile.Center.ToPoint());
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //穿体轻顿帧 + 沿冲刺方向的水珠锥
            target.CWR().TimeFrozenTick = 2;
            float ke = MathHelper.Clamp(Projectile.velocity.Length() / MaxSpeed, 0.4f, 1f);
            FishBarracudaVFX.ImpactSpray(Projectile.Center, Projectile.velocity, StripeColor, ke);

            SoundEngine.PlaySound(SoundID.NPCHit25 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.35f,
                Pitch = -0.2f,
                MaxInstances = 4
            }, Projectile.Center);

            //减少穿透次数
            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            //屏外正常离场静默；屏内死亡（穿透耗尽/超时）化水收场，沫痕比鱼身活得久
            if (Main.dedServ || !OnScreen(80f)) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            FishBarracudaVFX.BurstSplash(Projectile.Center, -dir, 0.9f);
            PRTLoader.NewParticle<PRT_FishBarracudaWake>(Projectile.Center, Vector2.Zero, FishBarracudaVFX.Turquoise, 1f)
                ?.Configure(Projectile.Center, Projectile.Center - dir * 110f, 10f, 16);

            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        //==== 绘制，白沫射流条带（primitive）+ 条纹残影链 + 速度涂抹 + 僚机剪影 + 本体 ====

        public float GetJetWidth(float completionRatio) =>
            (1f - completionRatio) * 19f * (0.15f + 0.85f * SpeedT) * Projectile.scale;

        public Color GetJetColor(Vector2 coord) =>
            Color.White * ((0.18f + 0.62f * SpeedT) * (1f - coord.X)) * Projectile.Opacity;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (!Rushing || !Projectile.active) {
                return;
            }
            Effect fx = FishBarracudaAssets.FishBarracudaJet;
            if (fx == null) {
                return;
            }
            FishBarracudaVFX.ApplyJet(fx, Projectile.whoAmI * 0.53f);
            GraniteMarbleVFX.DrawTrailFromOldPos(Projectile, ref trail, GetJetWidth, GetJetColor, fx);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Rushing) {
                return false; //预告段鱼还在屏外水下
            }
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.TropicalBarracuda].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            bool faceRight = Projectile.velocity.X >= 0f;
            SpriteEffects flip = faceRight ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float diag = faceRight ? MathHelper.PiOver4 : -MathHelper.PiOver4;
            float drawRot = Projectile.rotation + diag;
            float speedT = SpeedT;
            float speed = Projectile.velocity.Length();
            Vector2 dirN = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dirN.RotatedBy(MathHelper.PiOver2);

            //游动脉动，尾拍相位按鱼错开
            float pulse = 1f + 0.05f * MathF.Sin(swimWave * 2f + Projectile.identity * 0.9f);
            float bodyScale = Projectile.scale * pulse;

            //速度涂抹（最底层）
            Texture2D streak = CWRAsset.Extra_98?.Value;
            if (streak != null && speed > 8f) {
                float smearLen = MathHelper.Clamp(speed * 4.2f, 50f, 190f);
                sb.Draw(streak, drawPos - Projectile.velocity * 0.8f, null
                    , FishBarracudaVFX.SeaDeep with { A = 0 } * (0.5f * speedT)
                    , Projectile.velocity.ToRotation() + MathHelper.PiOver2, streak.Size() * 0.5f
                    , new Vector2(12f / streak.Width, smearLen / streak.Height), SpriteEffects.None, 0f);
            }

            //僚机剪影（压在主体之下）
            Color escortCol = Color.Lerp(lightColor, FishBarracudaVFX.SeaDeep, 0.4f);
            for (int k = 0; k < 2; k++) {
                float side = k == 0 ? 1f : -1f;
                float bob = MathF.Sin(swimWave * 2f + k * 2.4f + Projectile.identity) * 3.5f;
                Vector2 off = perp * (side * (13f + k * 4f) + bob) - dirN * (14f + k * 12f);
                sb.Draw(fishTex, drawPos + off, null, escortCol * 0.8f, drawRot, origin
                    , bodyScale * (0.62f - k * 0.1f), flip, 0f);
            }

            //热带条纹残影链，旧位置残像逐节换色
            if (speedT > 0.05f) {
                for (int g = 0; g < 3; g++) {
                    int i = 2 + g * 3;
                    if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    float ghostRot = (i < Projectile.oldRot.Length ? Projectile.oldRot[i] : Projectile.rotation) + diag;
                    Color stripe = FishBarracudaVFX.Stripe(PseudoIndex + g) with { A = 0 };
                    sb.Draw(fishTex, ghostPos, null, stripe * ((0.4f - g * 0.11f) * speedT), ghostRot, origin
                        , bodyScale * (0.97f - g * 0.06f), flip, 0f);
                }
            }

            //本体近位残像
            Color bodyGhost = FishBarracudaVFX.Turquoise with { A = 0 };
            sb.Draw(fishTex, drawPos - Projectile.velocity * 0.5f, null, bodyGhost * (0.4f * speedT)
                , drawRot, origin, bodyScale, flip, 0f);
            sb.Draw(fishTex, drawPos - Projectile.velocity * 1.1f, null, bodyGhost * (0.18f * speedT)
                , drawRot, origin, bodyScale * 0.94f, flip, 0f);

            //主体，轻微向绿松石压色
            Color mainColor = Color.Lerp(lightColor, FishBarracudaVFX.Turquoise, 0.2f);
            sb.Draw(fishTex, drawPos, null, mainColor, drawRot, origin, bodyScale, flip, 0f);

            //破水帧头部暖闪
            float burstT = 1f - MathHelper.Clamp((Timer - ReleaseUpdate) / 15f, 0f, 1f);
            Texture2D glint = CWRAsset.StarGlow01?.Value;
            if (glint != null && burstT > 0.02f) {
                Vector2 headPos = drawPos + dirN * 15f * bodyScale;
                sb.Draw(glint, headPos, null, FishBarracudaVFX.Coral with { A = 0 } * (0.85f * burstT), 0f
                    , glint.Size() / 2f, 26f / glint.Width, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
