using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·武僧套 T1】「入定雷禅」：禅雷（蓄于身、发于掌的苍青静电）。
    /// ①站进自己的闪电光环即入定，足下泛开青雷环；②入定中每四次近战/召唤命中
    /// 打出一道贯穿的禅雷掌波；③掌波命中迸出静电残络，余电在原地噼啪片刻。<br/>
    /// 与原版套装技联动：原版强化闪电光环（可暴击、更快），神赋把光环变成入定的「场」，
    /// 光环本体一概不改；层数攻击方端本地，掌波 owner 侧生成，光环与掌波命中不喂层
    /// </summary>
    internal class GsMonkArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsC";

        public override int[] HeadIDs => [ItemID.MonkBrows];

        public override int BodyID => ItemID.MonkShirt;

        public override int LegsID => ItemID.MonkPants;

        protected override string EndowLineFallback =>
            "Zen Thunder: standing within your own Lightning Aura enters zen; every fourth melee or summon strike in zen unleashes a piercing thunder-palm wave";

        //禅雷色板
        internal static readonly Color ZenBright = new(226, 244, 255);
        internal static readonly Color ZenMain = new(96, 172, 255);
        internal static readonly Color ZenDeep = new(20, 42, 84);

        /// <summary>入定判定半径</summary>
        protected virtual float StanceRadius => 150f;

        /// <summary>出掌所需命中数</summary>
        protected virtual int HitsPerPalm => 4;

        /// <summary>掌波是否命中后分侧掌（忍者档）</summary>
        protected virtual bool PalmSplits => false;

        /// <summary>本套的三档闪电光环弹幕</summary>
        private static readonly int[] auraTypes = [
            ProjectileID.DD2LightningAuraT1, ProjectileID.DD2LightningAuraT2, ProjectileID.DD2LightningAuraT3];

        private static bool IsAura(int type) => type == auraTypes[0] || type == auraTypes[1] || type == auraTypes[2];

        private bool InAuraRange(Player player) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || !IsAura(proj.type)) {
                    continue;
                }
                if (proj.Center.Distance(player.Center) < StanceRadius) {
                    return true;
                }
            }
            return false;
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            bool inStance = InAuraRange(player);
            if (inStance != state.EndowFlag) {
                state.EndowFlag = inStance;
                if (!inStance) {
                    state.EndowCharge = 0;//出定散功，计数清零
                }
                else if (!VaultUtils.isServer) {
                    //入定一瞬：足下青雷环荡开
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.45f, Pitch = 0.5f }, player.Center);
                    for (int i = 0; i < 10; i++) {
                        float ang = MathHelper.TwoPi * i / 10f;
                        PRTLoader.NewParticle<PRT_Spark>(player.Bottom + new Vector2(0f, -4f) + ang.ToRotationVector2() * 14f,
                            ang.ToRotationVector2() * 1.8f, ZenMain, 0.38f)?.Configure(false, 14);
                    }
                }
            }
            if (!inStance || VaultUtils.isServer) {
                return;
            }
            //入定驻场：周身静电缓升，微光常明
            Lighting.AddLight(player.Center, ZenMain.ToVector3() * 0.16f);
            if (Main.rand.NextBool(11)) {
                PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(18f, 24f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f)), ZenMain, Main.rand.NextFloat(0.06f, 0.11f))
                    ?.Configure(12, 0.6f);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //掌波自身与光环命中不喂层，防自循环；假人不算数
            if (sourceProj != null && (sourceProj.type == ModContent.ProjectileType<GsMonkZenPalmProj>() || IsAura(sourceProj.type))) {
                return;
            }
            if (target.type == NPCID.TargetDummy || !state.EndowFlag) {
                return;
            }
            //武僧是近战/召唤混行，两系命中都算入定拍子
            if (!hit.DamageType.CountsAsClass(DamageClass.Melee) && !hit.DamageType.CountsAsClass(DamageClass.Summon)) {
                return;
            }

            state.EndowCharge++;
            if (state.EndowCharge < HitsPerPalm) {
                //攒拍读数：目标身上一缕青电
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Unit() * 2f,
                        ZenBright, 0.3f)?.Configure(false, 10);
                }
                return;
            }

            //满拍出掌：自身位向目标推出禅雷掌波
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.75f, Pitch = 0.15f }, player.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                //掌波伤害按触发伤害折算并封顶，收益约为入定期间 +11% 而且有场地条件，处于神赋包络内
                int palmDamage = Math.Clamp((int)(damageDone * 0.45f), 10, 350);
                Vector2 dir = (target.Center - player.Center).SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithMonkEndow"),
                    player.Center + dir * 16f, dir * 15f,
                    ModContent.ProjectileType<GsMonkZenPalmProj>(), palmDamage, 2f, player.whoAmI,
                    0f, PalmSplits ? 1f : 0f);
            }
        }
    }

    /// <summary>
    /// 【神赋·武僧套 T3 忍者渗透装】「入定雷禅·忍」：同一门功夫的高段。
    /// 入定半径更大、三拍即出掌，掌波首次命中后分出两道半伤侧掌
    /// </summary>
    internal class GsMonkShinobiArmor : GsMonkArmor
    {
        public override int[] HeadIDs => [ItemID.MonkAltHead];

        public override int BodyID => ItemID.MonkAltShirt;

        public override int LegsID => ItemID.MonkAltPants;

        protected override string EndowLineFallback =>
            "Zen Thunder, Shinobi: zen palms come every third strike, and each wave splits two side palms on its first hit";

        protected override float StanceRadius => 180f;

        protected override int HitsPerPalm => 3;

        protected override bool PalmSplits => true;
    }

    /// <summary>
    /// 禅雷掌波：一记推出去的静电浪，不是光弹。波面横宽、行进减速（先疾后缓），
    /// 途中甩静电残络；ai[1]=1 时首次命中分出两道侧掌（ai[1]=2 的侧掌不再分）；
    /// 命中噼啪迸络，亡处余电驻留
    /// </summary>
    internal class GsMonkZenPalmProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>分掌模式：0 不分，1 命中分侧掌，2 侧掌本体</summary>
        private ref float SplitMode => ref Projectile.ai[1];

        /// <summary>确定性抖动相位，绘制不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.6173f % 3.13f;

        /// <summary>出生 4 帧淡入</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 55;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            //先疾后缓：掌劲吐尽的减速曲线
            if (Projectile.velocity.Length() > 8.5f) {
                Projectile.velocity *= 0.965f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行相：波缘甩静电残络
            if (!Main.dedServ && Life % 4 == 0) {
                Vector2 side = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float wob = MathF.Sin(Life * 0.8f + Seed * 5f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + side * wob * 14f,
                    Projectile.velocity * 0.1f + side * wob * 1.2f,
                    Main.rand.NextBool(3) ? GsMonkArmor.ZenBright : GsMonkArmor.ZenMain,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(Projectile.Center, GsMonkArmor.ZenMain.ToVector3() * (0.32f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                        Main.rand.NextBool() ? GsMonkArmor.ZenBright : GsMonkArmor.ZenMain,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            //忍者档：首次命中分出两道半伤侧掌，侧掌不再分
            if (SplitMode == 1f && Projectile.owner == Main.myPlayer) {
                SplitMode = 0f;
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = -1; i <= 1; i += 2) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center, dir.RotatedBy(0.5f * i) * 13f,
                        Projectile.type, Projectile.damage / 2, 1.5f, Projectile.owner, 0f, 2f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余痕：原地静电驻留，比掌波本体活得久
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsMonkArmor.ZenBright, 0.13f)?.Configure(8, 0.7f);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.5f, 2f),
                    Main.rand.NextBool() ? GsMonkArmor.ZenMain : GsMonkArmor.ZenDeep,
                    Main.rand.NextFloat(0.24f, 0.42f))?.Configure(false, Main.rand.Next(18, 30));
            }
        }

        //==================== 绘制：三层静电浪 + 波面横宽 + 电芯交叉 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float speed = Projectile.velocity.Length();
            //波面横宽 + 少量纵向速度拉伸：掌浪推着走
            float stretch = MathHelper.Clamp(speed * 0.03f, 0.08f, 0.45f);
            float wob = MathF.Sin(Life * 0.7f + Seed * 6f) * 0.1f;

            //暴雨蓝黑压边
            Main.EntitySpriteDraw(tex, pos, null, GsMonkArmor.ZenDeep * (0.85f * fade), rotation, origin,
                new Vector2(0.52f + wob, 0.30f + stretch * 0.8f), SpriteEffects.None, 0);
            //苍青主体
            Main.EntitySpriteDraw(tex, pos, null, GsMonkArmor.ZenMain * fade, rotation, origin,
                new Vector2(0.42f + wob, 0.24f + stretch * 0.6f), SpriteEffects.None, 0);
            //白电亮芯：加色，交叉双缝拟电弧
            Color core = GsMonkArmor.ZenBright with { A = 0 };
            Main.EntitySpriteDraw(tex, pos, null, core * (0.6f * fade), rotation + 0.35f + wob, origin,
                new Vector2(0.30f, 0.07f + stretch * 0.2f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, core * (0.5f * fade), rotation - 0.35f - wob, origin,
                new Vector2(0.26f, 0.06f + stretch * 0.2f), SpriteEffects.None, 0);
            return false;
        }
    }
}
