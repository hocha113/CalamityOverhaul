using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Stones;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>霓虹脂鲤技能，移动时周期生成照明鱼</summary>
    internal class FishNeonTetra : FishSkill
    {
        public override int UnlockFishID => ItemID.NeonTetra;
        public override int DefaultCooldown => 10;
        public override int ResearchDuration => 60 * 12;

        private int walkTimer = 0;
        private const int WalkInterval = 18; //每18帧生成一次
        private Vector2 lastPlayerPosition = Vector2.Zero;
        private int hueQueue = 0; //视觉队列号，色相相位沿生成顺序偏移

        public void UpdatePlayer(Player player) {
            if (Cooldown > 0) {
                return;
            }
            //检测玩家是否在移动
            float moveDistance = Vector2.Distance(player.Center, lastPlayerPosition);

            if (moveDistance > 1f) { //玩家正在移动
                walkTimer++;

                if (walkTimer >= WalkInterval) {
                    walkTimer = 0;
                    SetCooldown();
                    SpawnNeonTetra(player);
                }
            }
            else {
                //不移动时重置计时
                walkTimer = 0;
            }

            lastPlayerPosition = player.Center;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (Active(player)) {
                UpdatePlayer(player);
            }

            return base.UpdateCooldown(halibutPlayer, player);
        }

        private void SpawnNeonTetra(Player player) {
            if (Main.myPlayer != player.whoAmI) return;

            //在玩家周围随机位置生成霓虹脂鲤
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(40f, 80f);
            Vector2 spawnPos = player.Center + angle.ToRotationVector2() * distance;
            ShootState shootState = player.GetShootState();
            hueQueue = (hueQueue + 1) % 6;
            int neonProj = Projectile.NewProjectile(
                shootState.Source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<NeonTetraLightProjectile>(),
                (int)(shootState.WeaponDamage * (0.6f + HalibutData.GetDomainLayer() * 0.2f)),
                2f,
                player.whoAmI,
                0f,
                hueQueue
            );

            if (neonProj >= 0) {
                Main.projectile[neonProj].netUpdate = true;
            }
        }
    }

    /// <summary>
    /// 霓虹脂鲤发光弹幕：深海生物荧光，青-品红呼吸脉动 + 缎带尾迹 + 浮游光斑
    /// </summary>
    internal class NeonTetraLightProjectile : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable, IOverlayDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.NeonTetra;

        private ref float Timer => ref Projectile.ai[0];
        /// <summary>生成队列号 0..5，色相相位沿队列偏移</summary>
        private ref float HueQueue => ref Projectile.ai[1];

        private float glowIntensity = 0f;
        private float hitPulse = 0f; //命中提亮 0..1，每帧衰减
        private Trail trail;

        //光照参数
        private const float LightRadius = 200f;
        private const int LifeTime = 120;

        /// <summary>每鱼相位：identity 派生，各客户端一致</summary>
        private float Phase => Projectile.identity * 2.399f;
        /// <summary>呼吸 0..1：慢周期正弦，生物发光节律（周期约84帧）</summary>
        private float Breath => 0.5f + 0.5f * MathF.Sin(Timer * 0.075f + Phase);
        /// <summary>当前色相 0=青 1=品红：每鱼慢速滑移，队列错相</summary>
        private float HueT => 0.5f + 0.5f * MathF.Sin(Timer * 0.02f + HueQueue * 1.05f + Phase * 0.5f);

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 36;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 224;
            Projectile.height = 224;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            Timer++;

            //生命包络：化现淡入 → 稳态 → 消散淡出
            float lifeProgress = Timer / (float)LifeTime;
            if (lifeProgress < 0.2f) {
                float fadeIn = lifeProgress / 0.2f;
                Projectile.alpha = (int)(255 * (1f - fadeIn));
                glowIntensity = fadeIn;
            }
            else if (lifeProgress > 0.7f) {
                float fadeOut = (lifeProgress - 0.7f) / 0.3f;
                Projectile.alpha = (int)(255 * fadeOut);
                glowIntensity = 1f - fadeOut;
            }
            else {
                Projectile.alpha = 0;
                glowIntensity = 1f;
            }

            if (hitPulse > 0f) {
                hitPulse -= 0.12f;
            }

            //化现一拍：光斑外扩 + 微暗环 + 水滴声
            if (Timer == 1f) {
                FishNeonTetraVFX.MaterializeBurst(Projectile.Center, HueT);
            }

            //慢速利萨茹巡游：荧光鱼绕生成点游弋（漂移半径约30px），尾迹画出缎带环
            Projectile.velocity = new Vector2(
                MathF.Sin(Timer * 0.062f + Phase) * 1.9f,
                MathF.Cos(Timer * 0.048f + Phase * 1.37f) * 1.4f);

            //朝向沿泳向：贴图头朝右上，+PiOver4 校正；叠尾摆微振
            float heading = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            float wiggle = MathF.Sin(Timer * 0.31f + Phase) * 0.09f;
            Projectile.rotation = Timer <= 1f ? heading
                : Projectile.rotation.AngleLerp(heading + wiggle, 0.14f);

            //呼吸同步照明：饱和低明度的青-品红光，节律起伏
            Vector3 lightHue = Vector3.Lerp(new Vector3(0.10f, 0.44f, 0.54f)
                , new Vector3(0.44f, 0.07f, 0.36f), HueT);
            float breathMul = (0.7f + 0.3f * Breath) * glowIntensity * (1f + hitPulse * 0.4f);
            Lighting.AddLight(Projectile.Center, lightHue * breathMul);

            //浮游光斑：巡游期缓吐，消散期加速散逸（上浮）
            bool dissolving = lifeProgress > 0.7f;
            if (dissolving) {
                if (Timer % 5 == 0) {
                    FishNeonTetraVFX.AmbientMote(Projectile.Center, Projectile.velocity - new Vector2(0f, 0.8f), HueT);
                }
            }
            else if (Timer % 20 == 0 && glowIntensity > 0.4f) {
                FishNeonTetraVFX.AmbientMote(Projectile.Center, Projectile.velocity, HueT);
            }

            //照亮路径上的敌人：荧光渗染光照（描边叠层在绘制层）
            IlluminateEnemies();
        }

        private void IlluminateEnemies() {
            Vector3 hue = Vector3.Lerp(new Vector3(0.10f, 0.40f, 0.50f)
                , new Vector3(0.40f, 0.06f, 0.32f), HueT) * ((0.55f + 0.45f * Breath) * glowIntensity);
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly) {
                    continue;
                }
                float distance = Vector2.Distance(Projectile.Center, npc.Center);
                if (distance < LightRadius) {
                    Lighting.AddLight(npc.Center, hue);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //触碰节拍：体表荧光短促提亮 + 沿命中方向 squirt，无白闪
            hitPulse = 1f;
            FishNeonTetraVFX.TouchBurst(Projectile.Center, target.Center, HueT);
        }

        public override void OnKill(int timeLeft) {
            FishNeonTetraVFX.DissolveBurst(Projectile.Center, HueT);

            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.3f,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        //==== 绘制：缎带(图元层) → 底晕+敌人描边(加色层) → 鱼体+侧线(遮挡层) ====

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>化现过冲落定，消散微缩</summary>
        private float ScaleEnvelope() {
            float lifeProgress = Timer / (float)LifeTime;
            if (lifeProgress < 0.2f) {
                return 0.4f + 0.6f * FishNeonTetraVFX.EaseOutBack(lifeProgress / 0.2f);
            }
            if (lifeProgress > 0.7f) {
                return MathHelper.Lerp(1f, 0.78f, (lifeProgress - 0.7f) / 0.3f);
            }
            return 1f;
        }

        public float GetTrailWidth(float completionRatio) {
            //头宽尾尖，随呼吸与生命包络缩放
            float w = MathF.Pow(1f - completionRatio, 0.85f) * (9f + 3.5f * Breath);
            return w * (0.35f + 0.65f * glowIntensity);
        }

        public Color GetTrailColor(Vector2 coord) => Color.White * MathF.Pow(1f - coord.X, 1.15f);

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect fx = FishNeonTetraAssets.FishNeonTrail;
            if (fx == null || !Projectile.active || glowIntensity <= 0.02f) {
                return;
            }
            FishNeonTetraVFX.ApplyTrail(fx, Phase, Breath, glowIntensity * (1f + hitPulse * 0.3f));
            GraniteMarbleVFX.DrawTrailFromOldPos(Projectile, ref trail, GetTrailWidth, GetTrailColor, fx);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (!Projectile.active || glowIntensity <= 0.01f) {
                return;
            }
            float breath = Breath;
            Color hue = FishNeonTetraVFX.HueColor(HueT);
            Color hueAlt = FishNeonTetraVFX.HueColor(1f - HueT);

            //底层水晕：SoftGlow 仅作垫底（暗渊宽晕 + 饱和窄晕），非效果 body
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Vector2 gpos = Projectile.Center - Main.screenPosition;
                float gs = Projectile.scale * (0.9f + 0.12f * breath) * glowIntensity * ScaleEnvelope();
                spriteBatch.Draw(glow, gpos, null, (FishNeonTetraVFX.Abyss with { A = 0 }) * (0.5f * glowIntensity)
                    , 0f, glow.Size() * 0.5f, gs * 2.2f, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, gpos, null, (hue with { A = 0 })
                    * ((0.22f + 0.16f * breath) * glowIntensity * (1f + hitPulse * 0.5f))
                    , 0f, glow.Size() * 0.5f, gs * 1.2f, SpriteEffects.None, 0f);
            }

            //荧光照亮敌人：沿身形的加色渗染描边（放大晕轮 + 原尺寸补色低染），非白闪
            DrawEnemyRims(spriteBatch, hue, hueAlt, breath);
        }

        private void DrawEnemyRims(SpriteBatch spriteBatch, Color hue, Color hueAlt, float breath) {
            int budget = 12; //单鱼描边上限，防蠕虫类多节 NPC 撑爆绘制量
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly) {
                    continue;
                }
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist >= LightRadius) {
                    continue;
                }
                float rim = (1f - dist / LightRadius) * glowIntensity * (0.55f + 0.45f * breath);
                if (rim <= 0.03f) {
                    continue;
                }

                Main.instance.LoadNPC(npc.type);
                Texture2D tex = TextureAssets.Npc[npc.type].Value;
                Vector2 pos = npc.Center - Main.screenPosition + new Vector2(0f, npc.gfxOffY);
                Vector2 origin = npc.frame.Size() * 0.5f;
                SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                spriteBatch.Draw(tex, pos, npc.frame, (hue with { A = 0 }) * (0.20f * rim)
                    , npc.rotation, origin, npc.scale * 1.07f, flip, 0f);
                spriteBatch.Draw(tex, pos, npc.frame, (hueAlt with { A = 0 }) * (0.11f * rim)
                    , npc.rotation, origin, npc.scale, flip, 0f);

                if (--budget <= 0) {
                    break;
                }
            }
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (!Projectile.active) {
                return;
            }
            Texture2D fishTex = TextureAssets.Item[ItemID.NeonTetra].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            float alpha = (255f - Projectile.alpha) / 255f;
            float breath = Breath;
            Color hue = FishNeonTetraVFX.HueColor(HueT);

            //呼吸挤压拉伸：±4% 反相，游弋的活物感
            float squash = MathF.Sin(Timer * 0.075f + Phase) * 0.04f;
            Vector2 scale = new Vector2(1f + squash, 1f - squash) * Projectile.scale * ScaleEnvelope();

            //自发光体色：暗冷底与荧光色相融合，环境光只占小头（黑暗中仍可见）
            Color lightColor = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Color body = Color.Lerp(Color.Lerp(lightColor, FishNeonTetraVFX.AbyssBody, 0.55f)
                , hue, (0.30f + 0.25f * breath) * glowIntensity);
            spriteBatch.Draw(fishTex, drawPos, null, body * alpha, Projectile.rotation
                , origin, scale, SpriteEffects.None, 0f);

            //霓虹侧线：沿体轴的加色细条（脂鲤标志性荧光带），呼吸+命中提亮，A=0 走预乘加色
            Texture2D streak = CWRAsset.Extra_98?.Value;
            if (streak != null) {
                float bodyAxis = Projectile.rotation - MathHelper.PiOver4;
                Vector2 perp = (bodyAxis + MathHelper.PiOver2).ToRotationVector2();
                float stripeGlow = (0.30f + 0.38f * breath) * glowIntensity * (1f + hitPulse * 0.8f) * alpha;
                Vector2 stripeScale = new Vector2(0.065f, 0.32f) * scale; //72px 贴图 → 约4.7×23px 细条
                //上侧当前色相、下侧补色暗一档：双色渐变在体表相接
                spriteBatch.Draw(streak, drawPos - perp * 1.5f, null, (hue with { A = 0 }) * stripeGlow
                    , bodyAxis + MathHelper.PiOver2, streak.Size() * 0.5f, stripeScale, SpriteEffects.None, 0f);
                spriteBatch.Draw(streak, drawPos + perp * 1.5f, null
                    , (FishNeonTetraVFX.HueColor(1f - HueT) with { A = 0 }) * (stripeGlow * 0.55f)
                    , bodyAxis + MathHelper.PiOver2, streak.Size() * 0.5f, stripeScale * new Vector2(0.8f, 0.9f), SpriteEffects.None, 0f);
            }
        }
    }
}
