using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles
{
    /// <summary>
    /// 法师族吟唱法阵预告体：立定吟唱 ≥34 帧，法阵小圈渐亮，提交帧按型齐射。
    /// ai[0]=来源打包（槽位+1|类型&lt;&lt;8，参数行由来源类型反查） ai[1]=锁定瞄角（生成帧锁死，预告即承诺） ai[2]=档位。
    /// 模式：水矢 4 槽扇面跳过走廊缺口槽（幽灵预览与发射同判）/ 咒焰双发缓追（限转率+截止帧）/
    /// 影束单发+吟唱期军仪光环（周围甲骨 10% 减伤，300px 暗纹可见）/ 火柱（落点实体由 NPC 侧同帧点下）。
    /// 吟唱期施法者死亡/槽位复用即取消发射（击杀施法者=有效反制）
    /// </summary>
    internal class DdCastOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==== 模式 ====
        internal const byte ModeWater = 0;
        internal const byte ModeCursed = 1;
        internal const byte ModeShadow = 2;
        internal const byte ModePillar = 3;

        /// <summary>吟唱帧数（契约 ≥34，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 34;
        private const int FadeFrames = 10;

        //==== 水矢扇面（DarkCaster） ====
        /// <summary>扇面槽位数：4 槽跳 1 = 三连水矢</summary>
        private const int WaterFanSlots = 4;
        /// <summary>走廊缺口槽位：发射循环真正跳过的槽（幽灵预览同判，所见即所射）</summary>
        private const int WaterGapSlot = 2;
        private const float WaterHalfSpread = 0.34f;
        private const float WaterSpeed = 7.8f;

        //==== 咒焰双发（Ragged 系） ====
        /// <summary>双发相对瞄线的张角</summary>
        private const float CursedPairOffset = 0.20f;
        private const float CursedSpeed = 6.4f;

        //==== 影束（Necromancer 系） ====
        private const float ShadowSpeed = 13f;
        /// <summary>军仪光环半径：吟唱期周围此距离内的三系甲骨承伤 ×0.9（暗纹可见）</summary>
        internal const float AuraRadius = 300f;
        /// <summary>光环暗纹的旋转纹点数</summary>
        private const int AuraMoteCount = 12;

        private static readonly Color[] ModeColor = [
            new Color(90, 160, 255), //水矢
            new Color(150, 255, 60), //咒焰
            new Color(150, 90, 230), //影束
            new Color(255, 130, 50), //火柱
        ];

        /// <summary>幽灵预览用的原版贴图（即各自要发射的弹体贴图）</summary>
        private static readonly int[] GhostDonor = [
            ProjectileID.WaterBolt, ProjectileID.CursedFlameHostile,
            ProjectileID.ShadowBeamHostile, ProjectileID.InfernoHostileBolt,
        ];

        private int SourcePacked => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private int SourceType => SourcePacked >> 8;
        private float Aim => Projectile.ai[1];
        private int TotalLife => TelegraphFrames + FadeFrames;
        internal int Elapsed => TotalLife - Projectile.timeLeft;

        internal bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            private set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>本法阵的模式与参数行（由来源类型反查；查不到视为已取消）</summary>
        private bool TryGetRow(out DungeonDeepNPC.DdCastRow row) => DungeonDeepNPC.TryGetCastRow(SourceType, out row);

        /// <summary>
        /// 军仪光环覆盖判定：某点是否被任一吟唱中的影束法阵覆盖。
        /// 承伤门在命中计算端调用，法阵实体已同步（netImportant），各端结论一致
        /// </summary>
        internal static bool ShadowAuraCovers(Vector2 pos) {
            int type = ModContent.ProjectileType<DdCastOmen>();
            float radiusSq = AuraRadius * AuraRadius;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != type || proj.ModProjectile is not DdCastOmen omen) {
                    continue;
                }
                if (omen.Cancelled || omen.Elapsed >= TelegraphFrames) {
                    continue;
                }
                if (!DungeonDeepNPC.TryGetCastRow(omen.SourceType, out DungeonDeepNPC.DdCastRow row)
                    || row.Mode != ModeShadow) {
                    continue;
                }
                if (Vector2.DistanceSquared(proj.Center, pos) <= radiusSq) {
                    return true;
                }
            }
            return false;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害（水矢/咒焰/影束的伤害走各自弹体）</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //来源校验：施法者死亡/槽位复用则取消提交；各端读同步的 npc.active，结论一致
            if (!Cancelled && Elapsed < TelegraphFrames) {
                if (AnchorIndex < 0 || AnchorIndex >= Main.maxNPCs || !Main.npc[AnchorIndex].active
                    || Main.npc[AnchorIndex].type != SourceType || !TryGetRow(out _)) {
                    Cancelled = true;
                }
            }
            if (!Cancelled && AnchorIndex >= 0 && AnchorIndex < Main.maxNPCs) {
                //法阵贴施法者脚下
                Projectile.Center = Main.npc[AnchorIndex].Bottom - Vector2.UnitY * 4f;
            }

            if (!Cancelled && Elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                //法阵沿圈凝尘（≤2 粒/帧）
                if (TryGetRow(out DungeonDeepNPC.DdCastRow dustRow)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * 24f,
                        ModeDust(dustRow.Mode), ang.ToRotationVector2() * 0.5f, 130, default, 0.9f);
                    dust.noGravity = true;
                }
            }

            if (Elapsed == TelegraphFrames && !Cancelled && TryGetRow(out DungeonDeepNPC.DdCastRow row)) {
                if (!VaultUtils.isClient) {
                    Fire(row);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound((row.Mode switch {
                        ModeCursed => SoundID.Item20 with { Volume = 0.6f },
                        ModeShadow => SoundID.Item8 with { Volume = 0.7f, Pitch = -0.3f },
                        ModePillar => SoundID.Item20 with { Volume = 0.5f, Pitch = -0.5f },
                        _ => SoundID.Item34 with { Volume = 0.6f, Pitch = 0.2f },
                    }) with { MaxInstances = 5 }, Projectile.Center);
                }
            }

            if (TryGetRow(out DungeonDeepNPC.DdCastRow lightRow)) {
                Color glow = ModeColor[lightRow.Mode];
                Lighting.AddLight(Projectile.Center, glow.ToVector3() * 0.22f);
            }
        }

        private static int ModeDust(byte mode) => mode switch {
            ModeCursed => DustID.CursedTorch,
            ModeShadow => DustID.Shadowflame,
            ModePillar => DustID.Torch,
            _ => DustID.DungeonSpirit,
        };

        /// <summary>水矢第 i 槽相对瞄线的偏角；走廊缺口槽返回 null（发射与预览共用同一判定）</summary>
        private static float? WaterOffset(int i) {
            if (i == WaterGapSlot) {
                return null;
            }
            return MathHelper.Lerp(-WaterHalfSpread, WaterHalfSpread, i / (float)(WaterFanSlots - 1));
        }

        /// <summary>提交帧齐射（仅权威端）：瞄角为生成帧锁死的承诺</summary>
        private void Fire(DungeonDeepNPC.DdCastRow row) {
            int boltType = ModContent.ProjectileType<DdBoltProj>();
            switch (row.Mode) {
                case ModeWater:
                    //三连水矢：4 槽扇面跳过走廊缺口槽
                    for (int i = 0; i < WaterFanSlots; i++) {
                        float? offset = WaterOffset(i);
                        if (offset == null) {
                            continue;//走廊缺口：预览里空着的方向就是安全方向
                        }
                        Vector2 vel = (Aim + offset.Value).ToRotationVector2() * WaterSpeed;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                            boltType, Projectile.damage, 0f, Main.myPlayer, DdBoltProj.ModeWater);
                    }
                    break;
                case ModeCursed:
                    //双发缓追咒焰：限转率与追踪截止帧随弹体带走
                    for (int s = -1; s <= 1; s += 2) {
                        Vector2 vel = (Aim + CursedPairOffset * s).ToRotationVector2()
                            * (CursedSpeed + row.SpeedBonus);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                            boltType, Projectile.damage, 0f, Main.myPlayer,
                            DdBoltProj.ModeCursed, row.AuxA, row.AuxB);
                    }
                    break;
                case ModeShadow: {
                    Vector2 vel = Aim.ToRotationVector2() * (ShadowSpeed + row.SpeedBonus);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        boltType, Projectile.damage, 0f, Main.myPlayer, DdBoltProj.ModeShadow);
                    break;
                }
                //火柱模式无射出物：落点火柱实体由 NPC 侧在吟唱起手帧点下并自带 ≥34 帧地面预告
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!TryGetRow(out DungeonDeepNPC.DdCastRow row)) {
                return false;
            }
            int elapsed = Elapsed;
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - elapsed / (float)TelegraphFrames, 0f, 1f);
            }
            else if (elapsed >= TelegraphFrames) {
                fade = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)FadeFrames, 0f, 1f);
            }
            else {
                fade = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            }
            if (fade <= 0.02f) {
                return false;
            }

            float progress = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity);
            Color warn = ModeColor[row.Mode];
            Vector2 center = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D core = CWRAsset.Extra_98.Value;

            //法阵座：真透暗底（有遮挡像素）+ 渐亮加色芯
            Main.EntitySpriteDraw(core, center, null, new Color(30, 24, 40, 220) * (0.7f * fade), 0f,
                core.Size() / 2f, new Vector2(0.30f, 0.10f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, (warn with { A = 0 }) * (0.35f + 0.45f * progress) * fade * pulse,
                0f, glow.Size() / 2f, new Vector2(0.30f, 0.12f), SpriteEffects.None, 0);

            //法阵小圈：绕圈旋转的光点，随吟唱渐亮
            float spin = Main.GlobalTimeWrappedHourly * 2.4f + Projectile.identity;
            for (int i = 0; i < 6; i++) {
                float ang = spin + MathHelper.TwoPi * i / 6f;
                Vector2 dotPos = center + ang.ToRotationVector2() * 24f;
                Main.EntitySpriteDraw(glow, dotPos, null,
                    (warn with { A = 0 }) * ((0.25f + 0.55f * progress) * fade * pulse),
                    0f, glow.Size() / 2f, 0.05f, SpriteEffects.None, 0);
            }

            if (!Cancelled && elapsed < TelegraphFrames) {
                DrawModeHints(row, center, warn, fade, progress, pulse);
            }
            return false;
        }

        /// <summary>各模式的齐射预览：幽灵弹位与发射走同一角度判定，看到什么就来什么</summary>
        private void DrawModeHints(DungeonDeepNPC.DdCastRow row, Vector2 center, Color warn, float fade, float progress, float pulse) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            int donor = GhostDonor[row.Mode];
            Main.instance.LoadProjectile(donor);
            Texture2D ghostTex = TextureAssets.Projectile[donor].Value;
            int donorFrames = Math.Max(1, Main.projFrames[donor]);
            Rectangle frameRect = new(0, 0, ghostTex.Width, ghostTex.Height / donorFrames);
            Vector2 ghostOrigin = frameRect.Size() / 2f;
            float ghostDist = 24f + 28f * progress;
            float ghostAlpha = (0.30f + 0.35f * progress) * fade * pulse;

            switch (row.Mode) {
                case ModeWater:
                    for (int i = 0; i < WaterFanSlots; i++) {
                        float? offset = WaterOffset(i);
                        if (offset == null) {
                            continue;
                        }
                        float ang = Aim + offset.Value;
                        Main.EntitySpriteDraw(ghostTex, center + ang.ToRotationVector2() * ghostDist, frameRect,
                            warn * ghostAlpha, ang, ghostOrigin, 0.9f, SpriteEffects.None, 0);
                    }
                    //走廊缺口亮巷（指示安全方向）
                    float gapAng = Aim + MathHelper.Lerp(-WaterHalfSpread, WaterHalfSpread,
                        WaterGapSlot / (float)(WaterFanSlots - 1));
                    Main.EntitySpriteDraw(glow, center + gapAng.ToRotationVector2() * (ghostDist + 26f), null,
                        new Color(255, 244, 200, 0) * (0.45f * fade), gapAng, glow.Size() / 2f,
                        new Vector2(2.2f, 0.4f), SpriteEffects.None, 0);
                    break;
                case ModeCursed:
                    for (int s = -1; s <= 1; s += 2) {
                        float ang = Aim + CursedPairOffset * s;
                        Main.EntitySpriteDraw(ghostTex, center + ang.ToRotationVector2() * ghostDist, frameRect,
                            warn * ghostAlpha, ang, ghostOrigin, 0.9f, SpriteEffects.None, 0);
                    }
                    break;
                case ModeShadow: {
                    //瞄线提示
                    Main.EntitySpriteDraw(glow, center + Aim.ToRotationVector2() * (ghostDist + 30f), null,
                        (warn with { A = 0 }) * (0.4f * fade * pulse), Aim, glow.Size() / 2f,
                        new Vector2(2.6f, 0.32f), SpriteEffects.None, 0);
                    //军仪光环暗纹：300px 环上缓转的暗纹点（真透暗底=可见暗纹），环内甲骨承伤 ×0.9
                    Texture2D core = CWRAsset.Extra_98.Value;
                    float auraSpin = Main.GlobalTimeWrappedHourly * 0.8f + Projectile.identity * 0.7f;
                    for (int i = 0; i < AuraMoteCount; i++) {
                        float ang = auraSpin + MathHelper.TwoPi * i / AuraMoteCount;
                        Vector2 motePos = center + ang.ToRotationVector2() * AuraRadius;
                        Main.EntitySpriteDraw(core, motePos, null, new Color(26, 18, 44, 215) * (0.8f * fade),
                            ang, core.Size() / 2f, 0.11f, SpriteEffects.None, 0);
                        Main.EntitySpriteDraw(glow, motePos, null,
                            new Color(150, 110, 255, 0) * (0.30f * fade * pulse),
                            0f, glow.Size() / 2f, 0.05f, SpriteEffects.None, 0);
                    }
                    break;
                }
                case ModePillar:
                    //火柱模式：法阵上方升腾预热光（落点预告由火柱实体自画）
                    Main.EntitySpriteDraw(glow, center - Vector2.UnitY * (14f + 10f * progress), null,
                        (warn with { A = 0 }) * (0.4f * fade * pulse), 0f, glow.Size() / 2f,
                        new Vector2(0.24f, 0.4f + 0.3f * progress), SpriteEffects.None, 0);
                    break;
            }
        }
    }
}
