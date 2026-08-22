using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaHalibut
{
    /// <summary>
    /// 械奴的小海：比目鱼械奴施放的定点小型海洋领域。与玩家的 <see cref="SeaDomainProj"/>
    /// 不同，不跟随玩家、不读 HalibutPlayer 状态、有限寿命自走完展开-存续-坍缩三段；
    /// 视觉复用 SeaDomainField 场着色器（单层规格）+ 气泡链 + 一小群环游鱼。
    /// 场内敌人叠湿 + 每半秒一记水压（owner 端结算，伤害烘焙在 Projectile.damage），
    /// 弱小生物被水体缓拽向心。ai0 = 领域半径 px
    /// </summary>
    internal class KikasaHalibutDomain : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 时序 ====================

        private const int ExpandFrames = 40;
        private const int ActiveFrames = 430;
        private const int CollapseFrames = 45;
        private const int TotalFrames = ExpandFrames + ActiveFrames + CollapseFrames;

        /// <summary>水压结算节拍：每半秒一记</summary>
        private const int PressureTick = 30;

        /// <summary>各端本地计帧：三段生命周期确定性推进</summary>
        private int timer;

        /// <summary>提前坍缩闩（主人死亡/离场时从存续段直接跳坍缩）</summary>
        private bool collapsing;
        private int collapseStart = ExpandFrames + ActiveFrames;

        /// <summary>领域半径 px（生成包自带）</summary>
        private float Radius => Projectile.ai[0] > 10f ? Projectile.ai[0] : 225f;

        //==================== 纯表现（客户端各自演）====================

        private readonly List<BubbleChain> bubbles = [];
        private readonly List<DomainFishBoid> fish = [];
        private bool fishInit;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1500;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = TotalFrames + 20;
        }

        public override bool? CanDamage() => false;

        //==================== 生命周期几何 ====================

        private float DomainAlpha() {
            if (collapsing || timer >= collapseStart) {
                int ct = timer - collapseStart;
                return MathHelper.Clamp(1f - ct / (float)CollapseFrames, 0f, 1f);
            }
            return MathHelper.Clamp(timer / (float)ExpandFrames, 0f, 1f);
        }

        private float CurrentRadius() {
            if (collapsing || timer >= collapseStart) {
                int ct = timer - collapseStart;
                return MathHelper.Lerp(Radius, 80f, EaseInCubic(MathHelper.Clamp(ct / (float)CollapseFrames, 0f, 1f)));
            }
            float expandT = MathHelper.Clamp(timer / (float)ExpandFrames, 0f, 1f);
            //存续期水面轻呼吸
            float breathe = timer > ExpandFrames
                ? MathF.Sin((timer - ExpandFrames) * 0.05f) * Radius * 0.015f
                : 0f;
            return MathHelper.Lerp(80f, Radius, EaseOutCubic(expandT)) + breathe;
        }

        private static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - x, 3f);

        private static float EaseInCubic(float x) => x * x * x;

        //==================== 推进 ====================

        public override void AI() {
            timer++;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = 2 + TotalFrames;

            //主人离场：从存续段提前跳坍缩（各端本地同判，owner.active 是同步态）
            Player owner = Owner;
            if (!collapsing && (owner == null || !owner.active || owner.dead) && timer < collapseStart) {
                collapsing = true;
                collapseStart = timer;
            }

            float alpha = DomainAlpha();
            float radius = CurrentRadius();

            //开幕拍：小一号的雷鸣 + 深海回响（生成帧，各端就地演）
            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.28f, Pitch = -0.5f, MaxInstances = 1 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.42f, Pitch = -0.8f, MaxInstances = 1 }, Projectile.Center);
            }
            //展开期的水波爬升音
            if (timer < ExpandFrames + 10 && timer % 18 == 0) {
                SoundEngine.PlaySound(SoundID.Item85 with {
                    Volume = 0.18f,
                    Pitch = -0.4f + timer / (float)ExpandFrames * 0.3f,
                    MaxInstances = 3
                }, Projectile.Center);
            }
            //展开完成的一声定音
            if (timer == ExpandFrames) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 1 }, Projectile.Center);
            }
            //坍缩起点：水体消退
            if (timer == collapseStart + 1) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 1 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.3f, Pitch = 0.2f, MaxInstances = 1 }, Projectile.Center);
            }

            UpdatePressure(alpha, radius);
            UpdateVisuals(alpha, radius);

            Lighting.AddLight(Projectile.Center, 0.25f * alpha, 0.5f * alpha, 0.8f * alpha);

            if (timer >= collapseStart + CollapseFrames) {
                Projectile.Kill();
            }
        }

        /// <summary>水压与湿身：owner 端结算伤害（伤害归属玩家），弱小生物被水体缓拽向心</summary>
        private void UpdatePressure(float alpha, float radius) {
            if (alpha < 0.55f) {
                return;
            }
            bool strikeTick = timer % PressureTick == 0 && Projectile.IsOwnedByLocalPlayer();
            Player owner = Owner;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 1) {
                    continue;
                }
                //小动物友谊指南：主人不伤小动物就跳过
                if (npc.CountsAsACritter && owner?.dontHurtCritters == true) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist >= radius) {
                    continue;
                }

                //弱小生物：水体缓拽向心（服务器也跑此弹幕 AI，联机下由服务器的写入生效）
                if (SeaDomainProj.IsWeakEntity(npc)) {
                    Vector2 drag = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero)
                        * MathHelper.Clamp(dist / radius, 0.2f, 1f) * 0.14f;
                    npc.velocity += drag;
                    Lighting.AddLight(npc.Center, TorchID.Blue);
                }

                if (strikeTick) {
                    int damage = Math.Max(Projectile.damage, 1);
                    if (npc.boss) {
                        damage = (int)(damage * 1.5f);
                    }
                    damage += Main.rand.Next(-1, 2);
                    if (damage < 1) {
                        damage = 1;
                    }
                    npc.SimpleStrikeNPC(damage, npc.direction);
                    npc.AddBuff(BuffID.Wet, 120);
                    if (!VaultUtils.isServer) {
                        SpawnPressureDust(npc);
                    }
                }
            }
        }

        private static void SpawnPressureDust(NPC npc) {
            for (int d = 0; d < 4; d++) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 120, new Color(90, 180, 255), 1.1f);
                Main.dust[dust].velocity = (pos - npc.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.5f, 3f);
                Main.dust[dust].noGravity = true;
            }
        }

        /// <summary>气泡与鱼群：纯客户端表现，简约偏好下全部跳过</summary>
        private void UpdateVisuals(float alpha, float radius) {
            if (Main.dedServ || DomainVisuals.Concise) {
                return;
            }

            //存续期起鱼：一小群就够，这是械奴的小海，不是玩家的十层深渊
            if (!fishInit && timer >= ExpandFrames) {
                fishInit = true;
                const int fishCount = 10;
                for (int i = 0; i < fishCount; i++) {
                    float angle = i / (float)fishCount * MathHelper.TwoPi;
                    fish.Add(new DomainFishBoid(Projectile.Center, radius * 0.72f, angle));
                }
            }
            foreach (DomainFishBoid boid in fish) {
                boid.Update(Projectile.Center, radius * 0.72f, alpha);
            }

            //气泡链
            if (timer % 16 == 0 && timer >= ExpandFrames && timer < collapseStart) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(radius * 0.35f, radius * 0.85f);
                bubbles.Add(new BubbleChain(Projectile.Center + angle.ToRotationVector2() * dist));
            }
            for (int i = bubbles.Count - 1; i >= 0; i--) {
                bubbles[i].Update();
                if (bubbles[i].ShouldRemove()) {
                    bubbles.RemoveAt(i);
                }
            }

            //展开/坍缩期的边缘水尘
            if (timer < ExpandFrames && timer % 5 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius * Main.rand.NextFloat(0.6f, 1f);
                int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.4f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (pos - Projectile.Center).SafeNormalize(Vector2.Zero) * 2.4f;
            }
            else if (timer >= collapseStart && timer % 4 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;
                int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 3.2f;
            }

            //存续期偶发滴响
            if (timer % 60 == 0 && timer >= ExpandFrames && timer < collapseStart && Main.rand.NextBool(2)) {
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.1f, Pitch = 0.6f, MaxInstances = 3 }, Projectile.Center);
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            float alpha = DomainAlpha();
            if (alpha <= 0.01f) {
                return false;
            }
            float radius = CurrentRadius();

            DrawField(alpha, radius);

            if (DomainVisuals.Concise) {
                return false;
            }

            foreach (BubbleChain bubble in bubbles) {
                bubble.Draw(alpha);
            }
            foreach (DomainFishBoid boid in fish) {
                boid.DrawTrail(alpha * 0.8f);
            }
            foreach (DomainFishBoid boid in fish) {
                DrawFish(boid, alpha);
            }
            return false;
        }

        /// <summary>海洋场：SeaDomainField 着色器的单层规格</summary>
        private void DrawField(float alpha, float radius) {
            Effect shader = EffectLoader.SeaDomainField?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) {
                return;
            }

            const float extraScale = 1.1f;
            float drawRadius = radius * extraScale;
            float drawDiameter = drawRadius * 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float[] radii = new float[10];
            radii[0] = radius / drawRadius;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["fadeAlpha"]?.SetValue(alpha);
            shader.Parameters["contentFade"]?.SetValue(1f);
            shader.Parameters["layerCount"]?.SetValue(1f);
            shader.Parameters["layerRadii"]?.SetValue(radii);
            shader.Parameters["deepColor"]?.SetValue(new Vector3(0.02f, 0.06f, 0.15f));
            shader.Parameters["shallowColor"]?.SetValue(new Vector3(0.12f, 0.30f, 0.50f));
            shader.Parameters["causticColor"]?.SetValue(new Vector3(0.35f, 0.75f, 1.0f));
            shader.Parameters["ringInnerColor"]?.SetValue(new Color(70, 180, 255).ToVector3());
            shader.Parameters["ringOuterColor"]?.SetValue(new Color(120, 230, 255).ToVector3());
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>环游鱼：原版鱼贴图染色（与玩家领域同一套视觉语言，密度收小）</summary>
        private static void DrawFish(DomainFishBoid boid, float alpha) {
            int itemType = boid.FishType switch {
                0 => ItemID.Tuna,
                1 => ItemID.Bass,
                2 => ItemID.Trout,
                _ => ItemID.Tuna,
            };
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType].Value;
            SpriteEffects fx = boid.Velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float rot = boid.Velocity.ToRotation() + (boid.Velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            float fade = 0.75f + MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + boid.Frame) * 0.2f;
            Color c = boid.TintColor * fade * MathF.Pow(alpha, 0.8f);
            Main.spriteBatch.Draw(tex, boid.Position - Main.screenPosition, null, c, rot,
                tex.Size() * 0.5f, boid.Scale * 0.62f, fx, 0f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //谢幕：水体散成一圈回落的水尘
            for (int k = 0; k < 14; k++) {
                float angle = k / 14f * MathHelper.TwoPi;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * 70f;
                int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 110, new Color(100, 200, 255), 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f
                    + new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f));
            }
        }
    }
}
