using CalamityOverhaul.Common;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 灵液鱼技能，灵液感染与周期性射流
    /// </summary>
    internal class FishIchorn : FishSkill
    {
        public override int UnlockFishID => ItemID.Ichorfish;
        public override int DefaultCooldown => 120 - HalibutData.GetDomainLayer() * 8;
        public override int ResearchDuration => 60 * 16;
        //射流计数器
        private int streamCounter = 0;
        private static int StreamInterval => 18 - HalibutData.GetDomainLayer();

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            streamCounter++;

            //周期性释放灵液射流
            if (streamCounter >= StreamInterval && Cooldown <= 0) {
                streamCounter = 0;
                SetCooldown();

                //发射灵液射流
                Vector2 shootDir = velocity.SafeNormalize(Vector2.Zero);
                float spreadBase = 0.15f;

                //根据领域层数增加射流数量和扩散
                int streamCount = 1 + HalibutData.GetDomainLayer() / 2;

                for (int i = 0; i < streamCount; i++) {
                    float spreadAngle = MathHelper.Lerp(-spreadBase, spreadBase, i / (float)Math.Max(1, streamCount - 1));
                    Vector2 streamVelocity = shootDir.RotatedBy(spreadAngle) * Main.rand.NextFloat(8f, 24f);
                    streamVelocity.Y -= 3;

                    Projectile.NewProjectile(
                        source,
                        position,
                        streamVelocity,
                        ModContent.ProjectileType<IchorStream>(),
                        (int)(damage * (2f + HalibutData.GetDomainLayer() * 0.5f)),
                        knockback * 1.5f,
                        player.whoAmI
                    );
                }

                //出膛液体喷吐
                FishIchornVFX.MuzzleSpray(position, shootDir);

                //发射灵液射流音效
                SoundEngine.PlaySound(SoundID.Item95 with {
                    Volume = 0.6f,
                    Pitch = -0.3f
                }, position);

                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.4f,
                    Pitch = -0.5f
                }, position);
            }

            return null;
        }
    }

    /// <summary>全局钩子，Halibut 攻击附加灵液 debuff 并点亮蚀甲纹</summary>
    internal class FishIchornGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner.TryGetPlayer(out var player)
                && FishSkill.GetT<FishIchorn>().Active(player)) {
                //在这个技能下攻击会附加灵液效果
                int buffDuration = 300 + HalibutData.GetDomainLayer() * 30;
                target.AddBuff(BuffID.Ichor, buffDuration);

                //蚀甲纹标记与轻量命中溅金
                target.GetGlobalNPC<FishIchornErosion>().Tag(buffDuration);
                SpawnIchorInfectionEffect(projectile.Center, hit.HitDirection);
            }
        }

        //高频钩子，粒子量克制，常驻表现交给蚀甲纹脉冲
        private static void SpawnIchorInfectionEffect(Vector2 position, int hitDirection) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 velocity = new(hitDirection * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(-2.5f, -0.5f));
                PRTLoader.NewParticle<PRT_FishIchornDroplet>(position + Main.rand.NextVector2Circular(8f, 8f)
                    , velocity, Main.rand.NextBool(3) ? FishIchornVFX.IchorDeep : FishIchornVFX.IchorGold
                    , Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 28));
            }
        }
    }

    /// <summary>
    /// 灵液射流弹幕：受重力微弯的高压液柱。液柱条带 shader 承担飞行主体，
    /// 沿途甩滴，命中迸溅并留下挂壁金渍与蚀甲纹
    /// </summary>
    internal class IchorStream : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //液体流动状态
        private enum FluidState
        {
            Streaming,  //射流状态
            Splashing   //溅射状态
        }

        private FluidState State {
            get => (FluidState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StreamLife => ref Projectile.ai[1];

        //液体物理参数
        private const float Gravity = 0.35f;

        private Trail trail;

        /// <summary>出生淡入与消散共用的视觉包络</summary>
        private float VisualFade => State == FluidState.Streaming
            ? MathHelper.Clamp(StreamLife / 4f, 0f, 1f)
            : MathHelper.Clamp(1f - Projectile.alpha / 255f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            StreamLife++;

            if (State == FluidState.Streaming) {
                StreamingPhaseAI();
            }
            else if (State == FluidState.Splashing) {
                SplashingPhaseAI();
            }

            //灵液金黄微光照明
            float glow = 0.55f * VisualFade;
            Lighting.AddLight(Projectile.Center, 1.0f * glow, 0.76f * glow, 0.18f * glow);
        }

        //射流 tick
        private void StreamingPhaseAI() {
            //应用重力
            Projectile.velocity.Y += Gravity * 0.3f;

            //粘性阻力
            Projectile.velocity *= 0.995f;

            //液柱表面张力失稳：沿途甩滴，速度越快甩得越勤
            if (!Main.dedServ && StreamLife % 3 == 0) {
                float speed = Projectile.velocity.Length();
                Vector2 spawnPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(8f, 22f);
                Vector2 dropVel = Projectile.velocity * Main.rand.NextFloat(0.25f, 0.5f)
                    + Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.4f, 1.4f);
                PRTLoader.NewParticle<PRT_FishIchornDroplet>(spawnPos, dropVel
                    , Main.rand.NextBool(3) ? FishIchornVFX.IchorDeep : FishIchornVFX.IchorGold
                    , Main.rand.NextFloat(0.45f, 0.85f) * MathHelper.Clamp(speed / 16f, 0.6f, 1.1f))
                    ?.Configure(Main.rand.Next(20, 34));
            }

            //射流音效
            if (StreamLife % 20 == 0) {
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.25f,
                    Pitch = -0.4f
                }, Projectile.Center);
            }

            //超时转换为溅射
            if (StreamLife > 120 || Projectile.velocity.Length() < 2f) {
                EnterSplashState();
            }
        }

        //溅射 tick
        private void SplashingPhaseAI() {
            //快速消散
            Projectile.alpha += 15;
            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }

            Projectile.velocity *= 0.9f;
        }

        //进入溅射状态
        private void EnterSplashState() {
            State = FluidState.Splashing;
            Projectile.velocity *= 0.5f;
            Projectile.timeLeft = 60;
        }

        //碰撞溅射
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == FluidState.Streaming) {
                //贴壁迸溅并留金渍 decal
                FishIchornVFX.SplashBurst(Projectile.Center, oldVelocity, onTile: true);

                //溅射音效
                SoundEngine.PlaySound(SoundID.Item95 with {
                    Volume = 0.5f,
                    Pitch = 0.2f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.6f,
                    Pitch = -0.3f
                }, Projectile.Center);

                EnterSplashState();
                return false;
            }

            return true;
        }

        //击中NPC-迸溅+顿帧
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //液流重击的短顿帧
            target.CWR().TimeFrozenTick = 3;
            FishIchornVFX.SplashBurst(Projectile.Center, Projectile.velocity, onTile: false);

            //附加灵液效果
            target.AddBuff(BuffID.Ichor, 360 + HalibutData.GetDomainLayer() * 40);

            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit13 with {
                Volume = 0.5f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //飞行中死亡（贯穿耗尽/超时）：头部再迸溅一次
            if (State == FluidState.Streaming) {
                FishIchornVFX.SplashBurst(Projectile.Center, Projectile.velocity * 0.5f, onTile: false);
            }
            //液柱失压散珠：旧轨迹上的液体失去动压，就地凝珠坠落，活得比弹体久
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos != null) {
                for (int i = 2; i < oldPos.Length; i += 4) {
                    if (oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 pos = oldPos[i] + Projectile.Size * 0.5f;
                    PRTLoader.NewParticle<PRT_FishIchornDroplet>(pos + Main.rand.NextVector2Circular(4f, 4f)
                        , Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.8f, 0.8f)
                        , Main.rand.NextBool(3) ? FishIchornVFX.IchorDeep : FishIchornVFX.IchorGold
                        , Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 30));
                }
            }
        }

        //==== 绘制 ====

        public float GetWidthFunc(float completionRatio) =>
            MathHelper.Lerp(12f, 2.5f, completionRatio) * VisualFade; //completion 0 = 液锋端最宽

        public Color GetColorFunc(Vector2 coord) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !Projectile.active || VisualFade <= 0.01f) {
                return;
            }
            //液柱条带
            FishIchornVFX.DrawJetTrail(Projectile, ref trail, GetWidthFunc, GetColorFunc, VisualFade);

            //液锋头部：条带之上的领头液团
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawJetHead(sb);
            sb.End();
        }

        //液锋：随速度拉伸的三层液团，暗金压边+饱和金体+极小亮芯
        private void DrawJetHead(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.4f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.2f, 0.9f);

            //暗金压边
            sb.Draw(tex, pos, null, FishIchornVFX.IchorDark * (0.85f * fade), rotation, origin
                , new Vector2(0.5f, 0.55f + stretch * 0.9f), SpriteEffects.None, 0f);
            //饱和金液体
            sb.Draw(tex, pos, null, FishIchornVFX.IchorGold * fade, rotation, origin
                , new Vector2(0.38f, 0.45f + stretch * 0.8f), SpriteEffects.None, 0f);
            //液锋亮芯：极小面积加色
            Color core = FishIchornVFX.IchorBright with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.7f * fade), rotation, origin
                , new Vector2(0.14f, 0.24f + stretch * 0.35f), SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
