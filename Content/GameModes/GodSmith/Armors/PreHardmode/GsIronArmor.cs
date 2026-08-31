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
    /// 【神赋·盔甲】铁套「回火锻打」：材质=淬火锻铁，挨打记仇的防具金属。<br/>
    /// ①受击积攒回火（上限 4 层，不衰减）②满层炉温灼身，橙火星偶发上飘（个人读数）
    /// ③满层后的下一击自目标上空砸落一柄淬火锻锤，重坠加速、触地即碎④落点顿响迸火星起烟。<br/>
    /// 原版套装奖励（+2 防御）保留，神赋是叠加层；层数是受击方端本地量，
    /// 跨端可见的部分是锻锤实体；本套 OnEndowHurt 是攒层不是掉层（反向闭环）
    /// </summary>
    internal class GsIronArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.IronHelmet, ItemID.AncientIronHelmet];

        public override int BodyID => ItemID.IronChainmail;

        public override int LegsID => ItemID.IronGreaves;

        protected override string EndowLineFallback =>
            "Tempered Rebuke: taking hits builds temper; at 4 stacks your next strike calls down a quenched forge hammer";

        //淬火锻铁色板
        internal static readonly Color SteelGray = new(140, 146, 152);
        internal static readonly Color IronBlueGray = new(96, 110, 124);
        internal static readonly Color QuenchOrange = new(255, 140, 50);

        /// <summary>锻打所需回火层数</summary>
        private const int FullTemper = 4;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullTemper) {
                return;
            }
            //满层炉温：橙火星偶发上飘（个人读数，层数只在受击方端存在）
            Lighting.AddLight(player.Center, QuenchOrange.ToVector3() * 0.18f);
            if (Main.rand.NextBool(7)) {
                Vector2 at = player.Center + Main.rand.NextVector2Circular(14f, 20f);
                PRTLoader.NewParticle<PRT_Spark>(at, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.8f, 1.6f)),
                    QuenchOrange, Main.rand.NextFloat(0.2f, 0.35f))?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //锻锤自身命中不再唤锤，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsIronQuenchHammerProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }
            //本套命中不攒层（回火只靠挨打），未满层这一击什么都不做
            if (state.EndowCharge < FullTemper) {
                return;
            }

            //锻打：满层后这一击自目标上空唤落淬火锻锤
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = 0.3f }, target.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + new Vector2(0f, -20f),
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)),
                        QuenchOrange, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(14, 22));
                }
            }
            //proc 弹幕 owner 侧生成；单发重击按触发伤害 45% 折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int hammerDamage = Math.Clamp((int)(damageDone * 0.45f), 12, 150);
                //生成前探顶棚收缩高度，锤带标的线（ai[1]）免碰撞降到线再恢复，洞顶不再吞锤
                Vector2 spawn = GsArmorTerrainProbe.SkySpawnAbove(target.Center, 0f, 260f);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithIronEndow"),
                    spawn, new Vector2(0f, 4f),
                    ModContent.ProjectileType<GsIronQuenchHammerProj>(), hammerDamage, 3f, player.whoAmI,
                    0f, target.Center.Y);
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //反向闭环：挨打攒回火，上限 4 层不衰减
            if (state.EndowCharge >= FullTemper) {
                return;
            }
            state.EndowCharge++;
            if (!VaultUtils.isServer) {
                //回火入层：铁屑与余烬同溅，满层一响
                if (state.EndowCharge >= FullTemper) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.35f, Pitch = -0.2f }, player.Center);
                }
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Iron, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
                PRTLoader.NewParticle<PRT_Spark>(player.Center,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 2f)),
                    QuenchOrange, Main.rand.NextFloat(0.2f, 0.32f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }
    }

    /// <summary>
    /// 淬火锻锤：一柄还带炉温的实心铁锤，不是光块。自目标上空重坠加速（每帧 +0.9 封顶 26），
    /// 出生免地形碰撞、越过标的线（ai[1]）才恢复、触地即碎（Stardust 式高度门，洞顶不吞锤）；
    /// 三层紧实叠色（铁蓝灰压边/钢灰主体/淬橙热芯余热明灭）+ 下坠速度拉伸
    /// + 短拖尾残影，途中甩橙火星，落点顿响火星扇起烟
    /// </summary>
    internal class GsIronQuenchHammerProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>标的高度线：低于此线才恢复地形碰撞</summary>
        private ref float TargetLineY => ref Projectile.ai[1];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 5 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 90;
            //出生免碰撞，越过标的线由高度门恢复（见 AI）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //高度门：越过标的线才恢复地形碰撞
            GsArmorTerrainProbe.UpdateFallGate(Projectile, TargetLineY);
            //重坠加速，落速封顶
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.9f, 26f);
            //锤体微晃，确定性相位
            Projectile.rotation = MathF.Sin(Life * 0.2f + Seed * 4f) * 0.06f;

            //下坠途中甩橙火星
            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(6f, 4f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.3f, 1f)),
                    Main.rand.NextBool(3) ? GsIronArmor.SteelGray : GsIronArmor.QuenchOrange,
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(true, Main.rand.Next(10, 18));
            }
            Lighting.AddLight(Projectile.Center, GsIronArmor.QuenchOrange.ToVector3() * (0.24f * VisualFade));
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //砸中即顿响迸火星
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f)),
                    Main.rand.NextBool() ? GsIronArmor.QuenchOrange : GsIronArmor.SteelGray,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //落点：低音铁砧顿响 + 火星扇 + 烟团一朵，余痕比锤体活得久
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = -0.4f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsIronArmor.QuenchOrange, 0.16f)?.Configure(9, 0.7f);
            for (int i = 0; i < 9; i++) {
                //火星扇：向上半圆迸开后受重力回落
                float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.1f, 1.1f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool(3) ? GsIronArmor.SteelGray : GsIronArmor.QuenchOrange,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(18, 30));
            }
            //Fog 真 alpha 烟团（PRT_FluidSteam 承体），铁灰余烟缓升
            PRTLoader.NewParticle<PRT_FluidSteam>(Projectile.Center + new Vector2(0f, -6f),
                new Vector2(0f, -0.4f), GsIronArmor.IronBlueGray * 0.7f, Main.rand.NextFloat(0.4f, 0.55f))
                ?.Configure(36, 0.04f, 0.015f);
        }

        //==================== 绘制：三层紧实锻铁 + 下坠拉伸 + 短拖尾残影 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            //下坠速度拉伸 Y，落越快锤影越长
            float stretch = MathHelper.Clamp(Projectile.velocity.Y * 0.02f, 0f, 0.5f);

            //短拖尾残影：旧位置的褪色锤影（Extra_98 真 alpha，直接淡画）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.25f * fade;
                Vector2 gpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, gpos, null, GsIronArmor.IronBlueGray * ghost,
                    Projectile.rotation, origin, new Vector2(0.24f, 0.30f + stretch), SpriteEffects.None, 0);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //铁蓝灰压边
            Main.EntitySpriteDraw(tex, pos, null, GsIronArmor.IronBlueGray * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.28f, 0.34f + stretch * 1.1f), SpriteEffects.None, 0);
            //钢灰主体，紧实方块感
            Main.EntitySpriteDraw(tex, pos, null, GsIronArmor.SteelGray * fade, Projectile.rotation, origin,
                new Vector2(0.24f, 0.30f + stretch), SpriteEffects.None, 0);
            //淬橙热芯：确定性 sin 呼吸模拟余热明灭，加色观感
            float ember = 0.45f + MathF.Sin(Life * 0.35f + Seed * 6f) * 0.2f;
            Main.EntitySpriteDraw(tex, pos, null, (GsIronArmor.QuenchOrange with { A = 0 }) * (ember * fade),
                Projectile.rotation, origin, new Vector2(0.11f, 0.15f + stretch * 0.4f), SpriteEffects.None, 0);
            return false;
        }
    }
}
