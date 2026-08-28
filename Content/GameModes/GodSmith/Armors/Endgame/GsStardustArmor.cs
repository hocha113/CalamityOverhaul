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
    /// 【神赋·星尘套】「守望星尘」：星陨彗屑（拖着星尘彗尾的天坠碎晶）。
    /// ①星尘守卫击打过的敌人带上星尘印，印记淡蓝闪烁；②佩戴者亲手命中带印目标时
    /// 消耗印记，一颗星陨彗屑自天顶加速坠下砸向它；③落点星爆，星屑飘返佩戴者。<br/>
    /// 与原版套装技联动而非覆盖：原版星尘守卫（含双击下键指挥）照常运作，
    /// 神赋只监听守卫的拳击命中（弹幕 623/624）；印记表是攻击方端本地量
    /// （守卫命中本就在 owner 端结算），彗屑 owner 侧生成，彗屑命中不再触发
    /// </summary>
    internal class GsStardustArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsC";

        public override int[] HeadIDs => [ItemID.StardustHelmet];

        public override int BodyID => ItemID.StardustBreastplate;

        public override int LegsID => ItemID.StardustLeggings;

        protected override string EndowLineFallback =>
            "Stardust Vigil: enemies punched by your Stardust Guardian bear its seal; strike a sealed enemy yourself and a comet shard falls upon it from above";

        //星尘色板
        internal static readonly Color StarBright = new(242, 250, 255);
        internal static readonly Color StarMain = new(124, 192, 255);
        internal static readonly Color StarDeep = new(30, 52, 112);

        /// <summary>星尘印持续帧数</summary>
        private const int SealFrames = 420;

        /// <summary>守卫的拳与指挥爆破</summary>
        private static bool IsGuardian(int type) =>
            type == ProjectileID.StardustGuardian || type == ProjectileID.StardustGuardianExplosion;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;//印记只在攻击方端存在，读数也只画给本人
            }
            var seals = player.GetModPlayer<GsStardustArmorPlayer>();
            uint now = Main.GameUpdateCount;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!seals.IsSealed(npc.whoAmI, now)) {
                    continue;
                }
                //星尘印读数：目标身周淡蓝星点明灭
                if (Main.rand.NextBool(8)) {
                    PRTLoader.NewParticle<PRT_Light>(npc.Center + Main.rand.NextVector2CircularEdge(npc.width * 0.5f + 6f, npc.height * 0.5f + 6f),
                        new Vector2(0f, -0.4f), StarMain, 0.08f)?.Configure(11, 0.8f);
                }
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //彗屑自身命中不标记也不触发，防自循环；假人不算数
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsStardustFallProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }
            var seals = player.GetModPlayer<GsStardustArmorPlayer>();
            uint now = Main.GameUpdateCount;

            //守卫拳击命中：烙下星尘印
            if (sourceProj != null && IsGuardian(sourceProj.type)) {
                seals.Seal(target.whoAmI, now + SealFrames);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.35f, Pitch = 0.7f, MaxInstances = 3 }, target.Center);
                    for (int i = 0; i < 5; i++) {
                        float ang = MathHelper.TwoPi * i / 5f;
                        PRTLoader.NewParticle<PRT_Spark>(target.Center + ang.ToRotationVector2() * 12f,
                            ang.ToRotationVector2() * 1.5f, StarBright, 0.32f)?.Configure(false, 14);
                    }
                }
                return;
            }

            //佩戴者亲手命中带印目标：消耗印记，天顶降星
            if (!seals.IsSealed(target.whoAmI, now)) {
                return;
            }
            seals.Clear(target.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.75f, Pitch = -0.1f }, target.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                //彗屑伤害按触发伤害折算并封顶；需守卫先烙印且一印一星，收益在神赋包络内
                int fallDamage = Math.Clamp((int)(damageDone * 0.35f), 12, 400);
                Vector2 spawn = target.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), -430f);
                Vector2 vel = (target.Center - spawn).SafeNormalize(Vector2.UnitY) * 7f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithStardustEndow"),
                    spawn, vel, ModContent.ProjectileType<GsStardustFallProj>(),
                    fallDamage, 3f, player.whoAmI, 0f, target.Center.Y);
            }
        }

        public override void OnEndowLost(Player player, GodSmithArmorPlayer state) {
            base.OnEndowLost(player, state);
            player.GetModPlayer<GsStardustArmorPlayer>().ClearAll();
        }
    }

    /// <summary>
    /// 星尘印记录本：每玩家一份、纯攻击方端本地的 NPC 印记到期表；
    /// 不进存档不联网，换装或换方案时由 OnEndowLost 清空
    /// </summary>
    internal class GsStardustArmorPlayer : ModPlayer
    {
        private uint[] sealExpiry;

        public override void Initialize() => sealExpiry = new uint[Main.maxNPCs];

        internal void Seal(int npcIndex, uint expiry) => sealExpiry[npcIndex] = expiry;

        internal bool IsSealed(int npcIndex, uint now) => sealExpiry[npcIndex] > now;

        internal void Clear(int npcIndex) => sealExpiry[npcIndex] = 0;

        internal void ClearAll() => Array.Clear(sealExpiry, 0, sealExpiry.Length);
    }

    /// <summary>
    /// 星陨彗屑：一粒自天顶坠落的星尘碎晶，不是流星贴图。全程加速俯冲、
    /// 彗体沿速度大幅拉伸、四芒星冠随速旋闪；越过标的高度（ai[1]）后恢复碰撞、
    /// 触地即碎；落点星爆，星屑飘返佩戴者，余辉比彗体活得久
    /// </summary>
    internal class GsStardustFallProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>标的高度：低于此高度后恢复地形碰撞</summary>
        private ref float TargetLineY => ref Projectile.ai[1];

        private float Seed => Projectile.identity * 0.8791f % 3.07f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 110;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //天坠加速：一路提速到俯冲极速
            if (Projectile.velocity.Length() < 23f) {
                Projectile.velocity *= 1.055f;
            }
            //越过标的高度后恢复碰撞，触地即碎（高空生成时穿透天花板）
            if (!Projectile.tileCollide && Projectile.Center.Y > TargetLineY - 60f) {
                Projectile.tileCollide = true;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行相：彗尾星屑逐粒剥落
            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.7f,
                    Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Main.rand.NextBool(3) ? GsStardustArmor.StarDeep : GsStardustArmor.StarMain,
                    Main.rand.NextFloat(0.24f, 0.42f))?.Configure(false, Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, GsStardustArmor.StarMain.ToVector3() * (0.4f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //落点星爆 + 星屑飘返佩戴者，余辉驻留
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsStardustArmor.StarBright, 0.16f)?.Configure(10, 0.8f);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    Main.rand.NextBool() ? GsStardustArmor.StarBright : GsStardustArmor.StarMain,
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(true, Main.rand.Next(16, 26));
            }
            //侍星归还：两缕星屑朝佩戴者飘回
            Player owner = Main.player[Projectile.owner];
            if (owner.active) {
                Vector2 toOwner = (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                        toOwner * Main.rand.NextFloat(3f, 5f),
                        GsStardustArmor.StarBright, Main.rand.NextFloat(0.26f, 0.4f))
                        ?.Configure(false, Main.rand.Next(24, 36));
                }
            }
        }

        //==================== 绘制：三层彗体 + 大幅速度拉伸 + 四芒星冠 ====================

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
            //彗体沿速度大幅拉伸：坠得越快尾越长
            float stretch = MathHelper.Clamp(speed * 0.05f, 0.2f, 1.25f);
            float wob = MathF.Sin(Life * 0.6f + Seed * 5f) * 0.05f;

            //星海蓝压边
            Main.EntitySpriteDraw(tex, pos, null, GsStardustArmor.StarDeep * (0.85f * fade), rotation, origin,
                new Vector2(0.22f + wob, 0.32f + stretch), SpriteEffects.None, 0);
            //淡青主体
            Main.EntitySpriteDraw(tex, pos, null, GsStardustArmor.StarMain * fade, rotation, origin,
                new Vector2(0.16f + wob, 0.25f + stretch * 0.8f), SpriteEffects.None, 0);
            //白亮芯：加色，前置彗头
            Color core = GsStardustArmor.StarBright with { A = 0 };
            Vector2 headPos = pos + Projectile.velocity.SafeNormalize(Vector2.UnitY) * (8f + stretch * 22f);
            Main.EntitySpriteDraw(tex, headPos, null, core * (0.75f * fade), rotation, origin,
                new Vector2(0.09f, 0.15f + stretch * 0.3f), SpriteEffects.None, 0);
            //四芒星冠：黑底星贴图走加色（A=0），随速旋闪
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                float spin = Life * 0.18f + Seed;
                Main.EntitySpriteDraw(star, headPos, null, core * (0.55f * fade), spin, star.Size() * 0.5f,
                    new Vector2(0.09f + stretch * 0.02f, 0.13f + stretch * 0.05f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
