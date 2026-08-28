using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】木套「橡实迸发」：材质=胀满树液的生木橡实。<br/>
    /// ①命中积攒树液，满 6 层后下一击自目标崩出三枚弹跳橡实②橡实受重力落地弹跳，
    /// 第三次触地/命中/超时炸裂成四枚木刺③木刺直线穿刺，伤害为橡实一半④受击崩落 1 层草屑。<br/>
    /// 原版套装奖励（+1 防御）保留，神赋是叠加层；层数是攻击方端本地量，
    /// 就绪芽光只对佩戴者自己可见（个人读数），跨端可见的部分是橡实与木刺实体
    /// </summary>
    internal class GsWoodArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.WoodHelmet];

        public override int BodyID => ItemID.WoodBreastplate;

        public override int LegsID => ItemID.WoodGreaves;

        protected override string EndowLineFallback =>
            "Acorn Burst: strikes build sap; at 6 stacks the next strike bursts into three bouncing acorns that shatter into wooden splinters";

        //生木色板
        internal static readonly Color BudGreen = new(158, 199, 86);
        internal static readonly Color WoodBrown = new(122, 86, 46);
        internal static readonly Color DeepBark = new(70, 48, 26);

        /// <summary>引爆所需树液层数</summary>
        private const int FullCharge = 6;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //就绪态：芽绿微光绕身（个人读数，层数只在攻击方端存在）
            Lighting.AddLight(player.Center, BudGreen.ToVector3() * 0.16f);
            if (Main.rand.NextBool(9)) {
                Vector2 at = player.Center + Main.rand.NextVector2CircularEdge(20f, 26f);
                PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f))
                    , BudGreen, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(14, 0.65f);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //橡实与木刺自身命中不喂层，防自循环
            if (sourceProj != null && (sourceProj.type == ModContent.ProjectileType<GsWoodAcornProj>()
                || sourceProj.type == ModContent.ProjectileType<GsWoodSplinterProj>())) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //引爆：满层后这一击崩出三枚橡实
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.2f }, target.Center);
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.WoodFurniture,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 60, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = false;
                }
            }
            //proc 弹幕 owner 侧生成；橡实伤害按触发伤害 18% 折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int acornDamage = Math.Clamp((int)(damageDone * 0.18f), 5, 60);
                for (int i = 0; i < 3; i++) {
                    //初速向上扇形 ±0.6 rad，速度 7~9
                    float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.6f, 0.6f);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithWoodEndow"),
                        target.Center + new Vector2(0f, -8f),
                        ang.ToRotationVector2() * Main.rand.NextFloat(7f, 9f),
                        ModContent.ProjectileType<GsWoodAcornProj>(), acornDamage, 1f, player.whoAmI);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击崩落一层树液，攒层有张力
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge--;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Grass, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 迸发橡实：一粒有重量的胀液生木果，不是光点。受重力抛落、落地弹跳（触地一瞬压扁回弹），
    /// 第三次触地/命中 NPC/超时炸裂成四枚木刺；三层叠色（深褐压边/木棕主体/芽绿亮芽芯）
    /// + 旋转随速度，亡处木屑迸溅
    /// </summary>
    internal class GsWoodAcornProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>已弹跳次数</summary>
        private ref float Bounces => ref Projectile.ai[1];

        /// <summary>触地压扁闪帧倒计时（本地视觉量）</summary>
        private ref float SquashFrames => ref Projectile.localAI[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //重力抛落，落速封顶
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.3f, 16f);
            //旋转随水平速度滚动
            Projectile.rotation += Projectile.velocity.X * 0.06f;
            if (SquashFrames > 0f) {
                SquashFrames--;
            }

            //飞行相：偶发木屑剥落
            if (!Main.dedServ && Life % 5 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    80, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsWoodArmor.BudGreen.ToVector3() * (0.12f * VisualFade));
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Bounces++;
            //第三次触地：就地炸裂
            if (Bounces >= 3f) {
                return true;
            }
            //落地弹跳：Y 反向衰减，X 撞墙同理
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.75f;
            }
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.75f;
            }
            SquashFrames = 8f;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.3f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Bottom, DustID.WoodFurniture,
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)),
                        60, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = false;
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            //炸裂成四枚木刺；木刺伤害 = 橡实的 50%（生成时算好传入）
            if (Projectile.owner == Main.myPlayer) {
                int splinterDamage = Math.Max(1, (int)(Projectile.damage * 0.5f));
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.4f, 0.4f);
                    Projectile.NewProjectile(Main.player[Projectile.owner].GetSource_Misc("GodSmithWoodEndow"),
                        Projectile.Center,
                        ang.ToRotationVector2() * Main.rand.NextFloat(6f, 8f),
                        ModContent.ProjectileType<GsWoodSplinterProj>(), splinterDamage, 0.5f, Projectile.owner);
                }
            }
            if (Main.dedServ) {
                return;
            }
            //木质闷响 + 木屑迸溅，余痕比果体活得久
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsWoodArmor.BudGreen, 0.12f)?.Configure(8, 0.6f);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 60, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
        }

        //==================== 绘制：三层生木叠色 + 滚动旋转 + 触地压扁回弹 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            //果皮张力呼吸，确定性相位
            float wob = MathF.Sin(Life * 0.5f + Seed * 5f) * 0.08f;
            //触地压扁：闪帧内 Y 压缩至 0.7 后回弹
            float sq = MathHelper.Clamp(SquashFrames / 8f, 0f, 1f);
            Vector2 squash = new(1f + wob + sq * 0.25f, 1f - wob * 0.7f - sq * 0.3f);

            //深褐压边
            Main.EntitySpriteDraw(tex, pos, null, GsWoodArmor.DeepBark * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.30f, 0.34f) * squash, SpriteEffects.None, 0);
            //木棕主体
            Main.EntitySpriteDraw(tex, pos, null, GsWoodArmor.WoodBrown * fade, Projectile.rotation, origin,
                new Vector2(0.24f, 0.28f) * squash, SpriteEffects.None, 0);
            //芽绿亮芽芯，加色湿亮
            Color core = GsWoodArmor.BudGreen with { A = 0 };
            Main.EntitySpriteDraw(tex, pos, null, core * (0.55f * fade), Projectile.rotation, origin,
                new Vector2(0.09f, 0.13f) * squash, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 迸发木刺：橡实炸裂崩出的细针，直线穿刺短命即逝。
    /// Extra_98 拉成细针双层（深褐压边/木棕针体）+ 尖端芽绿小亮点 + 速度拉伸
    /// </summary>
    internal class GsWoodSplinterProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 22;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            //飞行相：针尾剥落细屑
            if (!Main.dedServ && Life % 4 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                    -Projectile.velocity * 0.08f, 100, default, Main.rand.NextFloat(0.5f, 0.8f));
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 80, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        //==================== 绘制：双层细针 + 尖端小亮点 + 速度拉伸 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.2f);

            //深褐压边
            Main.EntitySpriteDraw(tex, pos, null, GsWoodArmor.DeepBark * (0.8f * fade), Projectile.rotation, origin,
                new Vector2(0.08f, 0.34f + stretch), SpriteEffects.None, 0);
            //木棕针体
            Main.EntitySpriteDraw(tex, pos, null, GsWoodArmor.WoodBrown * fade, Projectile.rotation, origin,
                new Vector2(0.06f, 0.30f + stretch), SpriteEffects.None, 0);
            //尖端芽绿小亮点（黑底贴图走 A=0 加色）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Vector2 tip = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitY) * 10f - Main.screenPosition;
                Main.EntitySpriteDraw(glow, tip, null, (GsWoodArmor.BudGreen with { A = 0 }) * (0.6f * fade),
                    0f, glow.Size() * 0.5f, 0.16f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
