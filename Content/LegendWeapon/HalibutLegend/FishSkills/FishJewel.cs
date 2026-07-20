using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Stones;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 珠宝鱼技能：六色宝石按序轮换发射，每色一套音高与体色。<br/>
    /// 材质语言：切割宝石（折射体非光源），棱面镜面闪为离散事件，命中碎成同色碎晶
    /// </summary>
    internal class FishJewel : FishSkill
    {
        public override int UnlockFishID => ItemID.Jewelfish;
        public override int DefaultCooldown => (int)(21 - HalibutData.GetDomainLayer() * 1.3f); //更快的射击节奏
        public override int ResearchDuration => 60 * 18;

        private int gemCycle = 0; //宝石循环计数
        private const int GemTypes = 6; //6种宝石类型

        //音乐音阶配置（使用半音阶）
        private static readonly float[] MusicScale = new float[] {
            0.0f,   //C  - 红宝石
            0.1f,   //D  - 蓝宝石
            0.2f,   //E  - 绿宝石
            0.25f,  //F  - 黄玉
            0.35f,  //G  - 紫水晶
            0.45f   //A  - 钻石
        };

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                //循环切换宝石类型
                int currentGemType = gemCycle % GemTypes;
                gemCycle++;

                //重音拍：与音效重音同拍，主宝石带入更重的命中演出
                bool accentBeat = gemCycle % 4 == 0;

                //生成主宝石弹幕
                SpawnMainGem(source, player, position, velocity, damage, knockback, currentGemType, accentBeat);

                //根据领域等级生成额外的宝石碎片
                int fragmentCount = 2 + HalibutData.GetDomainLayer() / 5;
                SpawnGemFragments(source, player, position, velocity, damage, knockback, fragmentCount, currentGemType);

                //播放音乐化的宝石音效
                PlayMusicalGemSound(position, currentGemType);

                //生成节奏化的出膛特效
                SpawnRhythmicEffect(position, velocity, currentGemType);

                SetCooldown();
            }

            return null;
        }

        private void SpawnMainGem(IEntitySource source, Player player, Vector2 position,
            Vector2 velocity, int damage, float knockback, int gemType, bool accentBeat) {

            //稍微加快速度，增加节奏感
            Vector2 boostedVelocity = velocity * 1.15f;

            Projectile.NewProjectile(
                source,
                position,
                boostedVelocity,
                ModContent.ProjectileType<JewelGemProjectile>(),
                (int)(damage * (0.5f + HalibutData.GetDomainLayer() * 0.15f)),
                knockback * 1.2f,
                player.whoAmI,
                ai0: gemType,
                ai2: accentBeat ? 1f : 0f
            );
        }

        private void SpawnGemFragments(IEntitySource source, Player player, Vector2 position,
            Vector2 velocity, int damage, float knockback, int count, int gemType) {

            for (int i = 0; i < count; i++) {
                float angleOffset = MathHelper.Lerp(-0.35f, 0.35f, i / (float)Math.Max(1, count - 1));
                Vector2 fragmentVel = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(1.0f, 1.25f);

                Projectile.NewProjectile(
                    source,
                    position + Main.rand.NextVector2Circular(15f, 15f),
                    fragmentVel,
                    ModContent.ProjectileType<JewelFragmentProjectile>(),
                    (int)(damage * (0.2f + HalibutData.GetDomainLayer() * 0.05f)),
                    knockback * 0.8f,
                    player.whoAmI,
                    ai0: gemType,
                    ai1: i
                );
            }
        }

        /// <summary>按音高播放宝石音效</summary>
        private void PlayMusicalGemSound(Vector2 position, int gemType) {
            //主音符 - 清脆的钟声
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.8f,
                Pitch = MusicScale[gemType],
                PitchVariance = 0.02f
            }, position);

            //和声 - 柔和的共鸣
            SoundEngine.PlaySound(SoundID.Item28 with {
                Volume = 0.5f,
                Pitch = MusicScale[gemType] + 0.5f, //高八度和声
                PitchVariance = 0.02f
            }, position);

            //节奏打击音 - 增加节奏感
            SoundEngine.PlaySound(SoundID.Item37 with {
                Volume = 0.4f,
                Pitch = 0.6f + gemType * 0.08f
            }, position);

            //每隔一段时间添加重音
            if (gemCycle % 4 == 0) {
                //强调音 - 更响亮
                SoundEngine.PlaySound(SoundID.Item4 with {
                    Volume = 0.7f,
                    Pitch = MusicScale[gemType] - 0.2f
                }, position);
            }

            //每完成一个循环（6种宝石）播放特殊音效
            if (gemCycle % GemTypes == 0) {
                SoundEngine.PlaySound(SoundID.MaxMana with {
                    Volume = 0.6f,
                    Pitch = 0.3f
                }, position);
            }
        }

        /// <summary>出膛 VFX：定向压扁环 + 同色碎晶前抛 + 星闪，重音拍加强，六色循环完成时六色星闪扇</summary>
        private void SpawnRhythmicEffect(Vector2 position, Vector2 velocity, int gemType) {
            if (Main.dedServ) {
                return;
            }
            bool accentBeat = gemCycle % 4 == 0;
            FishJewelVFX.LaunchBurst(position, velocity, gemType, accentBeat);

            //每完成一个循环（6种宝石）：序列收束的可视化
            if (gemCycle % GemTypes == 0) {
                FishJewelVFX.SequenceFan(position, velocity);
            }
        }

        /// <summary>宝石颜色</summary>
        public static Color GetGemColor(int gemType) {
            return gemType switch {
                0 => new Color(255, 100, 100),   //红宝石
                1 => new Color(100, 100, 255),   //蓝宝石
                2 => new Color(100, 255, 100),   //绿宝石
                3 => new Color(255, 200, 100),   //黄玉
                4 => new Color(200, 100, 255),   //紫水晶
                5 => new Color(100, 255, 255),   //钻石
                _ => Color.White
            };
        }

        /// <summary>宝石对应物品 ID</summary>
        public static int GetGemItemID(int gemType) {
            int id = gemType switch {
                0 => ItemID.Ruby,
                1 => ItemID.Sapphire,
                2 => ItemID.Emerald,
                3 => ItemID.Topaz,
                4 => ItemID.Amethyst,
                5 => ItemID.Diamond,
                _ => ItemID.Diamond
            };
            Main.instance.LoadItem(id);
            return id;
        }
    }

    /// <summary>
    /// 主宝石弹幕：宝石物品贴图为实体本体（暗体色压底），自旋渐加速，
    /// 棱面随相位打出离散镜面闪；拖尾为同色窄条带。<br/>
    /// ai[0]=宝石类型 ai[1]=计时 ai[2]=重音拍（1 命中附顿帧与小震）
    /// </summary>
    internal class JewelGemProjectile : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float GemType => ref Projectile.ai[0];
        private ref float Time => ref Projectile.ai[1];
        private ref float Accent => ref Projectile.ai[2];

        private float rotationSpeed = 0f;
        private float facetGlint = 0f;    //当前棱面反光强度 0..1，AI 计算 PreDraw 消费
        private int glintDropCooldown = 0;
        private Trail trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 24; //更长的拖尾
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Time++;

            //自旋加速后饱和：棱面闪点节奏随之变密（飞行期演化量）
            rotationSpeed = Math.Min(rotationSpeed + 0.0035f, 0.22f);
            Projectile.rotation += rotationSpeed;

            //轻微的螺旋运动 - 增加视觉动感
            float spiralWave = (float)Math.Sin(Time * 0.2f) * 0.5f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perpendicular * spiralWave;

            //棱面 spec flash：facetCount 个棱面随自旋对准光源，尖锐余弦脉冲 ≤2 帧
            int facetCount = 3 + (int)GemType % 3;
            float facetPhase = facetCount * Projectile.rotation + Projectile.whoAmI * 0.61f;
            float glint = MathF.Pow(MathF.Max(0f, MathF.Cos(facetPhase)), 32f);
            bool glintRise = glint > 0.5f && facetGlint <= 0.5f;
            facetGlint = glint;

            //折射体照明：低幅常亮，闪点瞬间提亮
            FishJewelVFX.JewelPalette pal = FishJewelVFX.Palette((int)GemType);
            Lighting.AddLight(Projectile.Center, pal.Bright.ToVector3() * (0.42f + facetGlint * 0.5f));

            if (glintDropCooldown > 0) {
                glintDropCooldown--;
            }
            if (glintRise && glintDropCooldown <= 0 && !Main.dedServ) {
                //闪点落屑：一枚悬滞光痕挂在闪点处，短暂活过弹体经过
                glintDropCooldown = 10;
                PRTLoader.NewParticle<PRT_FishJewelGlint>(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f)
                    , Vector2.Zero, default, 0.55f)?.Configure((int)GemType, 12, Projectile.rotation + 0.6f, 10f);
            }

            //掉屑频率∝速度：随速度衰减而稀疏
            if (!Main.dedServ && Main.rand.NextFloat() < Projectile.velocity.Length() * 0.022f) {
                Dust trailDust = Dust.NewDustPerfect(
                    Projectile.Center - Projectile.velocity * 1.5f,
                    DustID.GemTopaz + (int)GemType,
                    -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    150, pal.Bright, Main.rand.NextFloat(0.8f, 1.2f));
                trailDust.noGravity = true;
            }

            //速度衰减 - 稍慢一些保持节奏感
            Projectile.velocity *= 0.997f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中音效 - 音乐化
            float[] hitPitches = new float[] { 0.2f, 0.3f, 0.4f, 0.45f, 0.55f, 0.65f };
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.7f,
                Pitch = hitPitches[(int)GemType]
            }, Projectile.Center);

            //和声
            SoundEngine.PlaySound(SoundID.Item28 with {
                Volume = 0.4f,
                Pitch = hitPitches[(int)GemType] + 0.3f
            }, Projectile.Center);

            bool accent = Accent >= 1f;
            //同色碎晶飞溅：顺入射方向迸溅的实体晶片
            FishJewelVFX.ImpactBurst(Projectile.Center, Projectile.velocity, (int)GemType, accent);

            //重音拍命中：短顿帧 + 小幅定向震（手感层，不动数值）
            if (accent) {
                target.CWR().TimeFrozenTick = 2;
                FishJewelVFX.Punch(Projectile.Center, Projectile.velocity, 2.2f, 9f, 6);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //宝石破碎：碎晶四溅活过弹体；条带改铺分段驻留光痕，尾部先蚀
            FishJewelVFX.ShatterBurst(Projectile.Center, Projectile.velocity, (int)GemType, 8);
            FishJewelVFX.RibbonResidue(Projectile, (int)GemType);
        }

        private float RibbonWidth(float completion) => (1f - completion) * 11f + 2f; //completion 0 = 头端最宽

        private Color RibbonColor(Vector2 coord) => Color.White * (1f - coord.X);

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect fx = FishJewelAssets.FishJewelTrail;
            if (fx == null || !Projectile.active) {
                return;
            }
            FishJewelVFX.ApplyTrail(fx, (int)GemType, Projectile.whoAmI * 0.73f);
            GraniteMarbleVFX.DrawTrailFromOldPos(Projectile, ref trail, RibbonWidth, RibbonColor, fx);
        }

        public override bool PreDraw(ref Color lightColor) {
            int gemItemID = FishJewel.GetGemItemID((int)GemType);
            Texture2D gemTex = TextureAssets.Item[gemItemID].Value;
            FishJewelVFX.JewelPalette pal = FishJewelVFX.Palette((int)GemType);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = gemTex.Size() / 2f;
            float fade = Projectile.Opacity;
            float glint = facetGlint;

            //底层内火：小径向底光只作垫层
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Main.EntitySpriteDraw(glow, drawPos, null, (pal.Bright with { A = 0 }) * ((0.3f + glint * 0.25f) * fade)
                    , 0f, glow.Size() / 2f, Projectile.scale * (0.4f + glint * 0.08f), SpriteEffects.None, 0);
            }

            //旋转拖影 + 速度残影：残影同时回退自旋相位与位置，编码自旋与运动方向
            Color smear = pal.Bright with { A = 0 };
            for (int i = 2; i >= 1; i--) {
                Main.EntitySpriteDraw(gemTex, drawPos - Projectile.velocity * (i * 0.9f), null
                    , smear * ((0.32f / i) * fade), Projectile.rotation - rotationSpeed * i * 3.4f, origin
                    , Projectile.scale * (1f - i * 0.07f), SpriteEffects.None, 0);
            }

            //本体：暗宝石体色实体（AlphaBlend），闪点帧整体提亮，平时压暗
            Color bodyCol = Color.Lerp(pal.Deep, pal.Bright, 0.4f + glint * 0.45f);
            Main.EntitySpriteDraw(gemTex, drawPos, null, bodyCol * fade, Projectile.rotation, origin
                , Projectile.scale, SpriteEffects.None, 0);

            //棱面反光：随宝石姿态的窄反光线 + 四向星芒，离散事件仅峰值帧可见
            if (glint > 0.03f) {
                Color gcol = (pal.Glint with { A = 0 }) * (glint * fade);
                Texture2D streak = CWRAsset.Extra_98?.Value;
                if (streak != null) {
                    Main.EntitySpriteDraw(streak, drawPos, null, gcol * 0.85f, Projectile.rotation + 0.6f
                        , streak.Size() / 2f, new Vector2(0.12f, 0.4f) * Projectile.scale, SpriteEffects.None, 0);
                }
                Texture2D cross = FishJewelAssets.RayCross?.Value;
                if (cross != null) {
                    Main.EntitySpriteDraw(cross, drawPos, null, gcol, Projectile.rotation * 0.25f
                        , cross.Size() / 2f, Projectile.scale * (0.14f + glint * 0.14f), SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 宝石碎片弹幕：小晶体单棱翻滚，每转对准一次打出反光；拖尾为更窄的同色条带。<br/>
    /// ai[0]=宝石类型 ai[1]=生成序号 localAI[0]=计时
    /// </summary>
    internal class JewelFragmentProjectile : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float GemType => ref Projectile.ai[0];
        private ref float Time => ref Projectile.localAI[0];

        private float rotationSpeed = 0f;
        private float rhythmPhase = 0f;
        private float facetGlint = 0f;
        private Trail trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            rotationSpeed = Main.rand.NextFloat(0.4f, 0.6f) * Main.rand.NextBool().ToDirectionInt();
            rhythmPhase = Main.rand.NextFloat(MathHelper.TwoPi); //随机节奏相位
        }

        public override void AI() {
            Time++;

            //节奏性旋转
            Projectile.rotation += rotationSpeed;

            //节奏性追踪
            float rhythmPulse = (float)Math.Sin(Time * 0.4f + rhythmPhase) * 0.5f + 0.5f;

            if (Time > 15 && Time < 120) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float homingStrength = 0.02f + rhythmPulse * 0.03f; //节奏性追踪强度
                    Projectile.velocity = Vector2.Lerp(
                        Projectile.velocity,
                        toTarget.SafeNormalize(Vector2.Zero) * Projectile.velocity.Length(),
                        homingStrength
                    );
                }
            }

            //单棱翻滚反光：每转对准一次，尖锐脉冲
            facetGlint = MathF.Pow(MathF.Max(0f, MathF.Cos(Projectile.rotation + rhythmPhase)), 32f);

            //折射体照明：低幅，闪点提亮
            FishJewelVFX.JewelPalette pal = FishJewelVFX.Palette((int)GemType);
            Lighting.AddLight(Projectile.Center, pal.Bright.ToVector3() * (0.28f + facetGlint * 0.35f));

            //稀疏掉屑∝速度
            if (!Main.dedServ && Main.rand.NextFloat() < Projectile.velocity.Length() * 0.012f) {
                Dust trailDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    -Projectile.velocity * 0.3f,
                    150, pal.Bright, Main.rand.NextFloat(0.7f, 1.1f));
                trailDust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //小型击中音效
            float[] fragmentPitches = new float[] { 0.5f, 0.6f, 0.7f, 0.75f, 0.85f, 0.95f };
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.4f,
                Pitch = fragmentPitches[(int)GemType]
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //小晶破碎：命中与撞墙同走此处，碎晶与残痕活过弹体
            FishJewelVFX.ShatterBurst(Projectile.Center, Projectile.velocity, (int)GemType, 4);
            FishJewelVFX.RibbonResidue(Projectile, (int)GemType);
        }

        private float RibbonWidth(float completion) => (1f - completion) * 6f + 1.5f;

        private Color RibbonColor(Vector2 coord) => Color.White * (0.85f - coord.X * 0.85f);

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect fx = FishJewelAssets.FishJewelTrail;
            if (fx == null || !Projectile.active) {
                return;
            }
            FishJewelVFX.ApplyTrail(fx, (int)GemType, Projectile.whoAmI * 0.73f);
            GraniteMarbleVFX.DrawTrailFromOldPos(Projectile, ref trail, RibbonWidth, RibbonColor, fx);
        }

        public override bool PreDraw(ref Color lightColor) {
            int gemItemID = FishJewel.GetGemItemID((int)GemType);
            Texture2D gemTex = TextureAssets.Item[gemItemID].Value;
            FishJewelVFX.JewelPalette pal = FishJewelVFX.Palette((int)GemType);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = gemTex.Size() / 2f;
            float fade = Projectile.Opacity;
            float glint = facetGlint;
            float baseScale = Projectile.scale * 0.75f;

            //旋转拖影：单枚残影回退自旋相位与位置
            Color smear = pal.Bright with { A = 0 };
            Main.EntitySpriteDraw(gemTex, drawPos - Projectile.velocity * 0.9f, null, smear * (0.28f * fade)
                , Projectile.rotation - rotationSpeed * 3.2f, origin, baseScale * 0.92f, SpriteEffects.None, 0);

            //本体：暗体色小晶
            Color bodyCol = Color.Lerp(pal.Deep, pal.Bright, 0.4f + glint * 0.45f);
            Main.EntitySpriteDraw(gemTex, drawPos, null, bodyCol * fade, Projectile.rotation, origin
                , baseScale, SpriteEffects.None, 0);

            //翻滚反光：仅峰值帧的星点
            if (glint > 0.05f) {
                Texture2D cross = FishJewelAssets.RayCross?.Value;
                if (cross != null) {
                    Main.EntitySpriteDraw(cross, drawPos, null, (pal.Glint with { A = 0 }) * (glint * fade)
                        , Projectile.rotation * 0.25f, cross.Size() / 2f, baseScale * 0.16f, SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }
}
