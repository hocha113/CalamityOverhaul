using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】铅套「铅雨沉降」：材质=哑光沉铅，重而闷，无高光。<br/>
    /// ①命中积攒铅坠，满 6 层后下一击自目标上方泼下三滴沉铅②铅滴砸中或触地溅裂成两枚低抛铅珠
    /// ③铅滴与铅珠命中皆挂铅毒（Poisoned）④受击崩落 2 层铅坠。<br/>
    /// 原版套装奖励保留，神赋是叠加层；层数是攻击方端本地量，跨端可见的部分是铅滴/铅珠实体
    /// </summary>
    internal class GsLeadArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.LeadHelmet];

        public override int BodyID => ItemID.LeadChainmail;

        public override int LegsID => ItemID.LeadGreaves;

        protected override string EndowLineFallback =>
            "Lead Downpour: strikes build lead; at 6 stacks the next strike pours three heavy drops that splash into beads and poison what they touch";

        //哑光沉铅色板：刻意无亮芯，第三层是压暗的闷白微光
        internal static readonly Color LeadEdge = new(58, 64, 78);
        internal static readonly Color LeadBody = new(112, 118, 132);
        internal static readonly Color LeadSheen = new(190, 195, 205);

        /// <summary>泼雨所需铅坠层数</summary>
        private const int FullCharge = 6;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //就绪态：铅灰微尘缓缓下沉（个人读数，哑光材质不发光只压尘）
            if (Main.rand.NextBool(10)) {
                Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2CircularEdge(18f, 24f),
                    DustID.Lead, new Vector2(0f, Main.rand.NextFloat(0.3f, 0.8f)), 120, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //铅滴与铅珠自身命中不喂层，防自循环
            if (sourceProj != null && (sourceProj.type == ModContent.ProjectileType<GsLeadDropProj>()
                || sourceProj.type == ModContent.ProjectileType<GsLeadBeadProj>())) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //满层：这一击自目标上方泼下三滴沉铅
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = -0.6f }, target.Center);
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(target.Top, DustID.Lead,
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)),
                        100, default, Main.rand.NextFloat(0.9f, 1.3f));
                    d.noGravity = true;
                }
            }
            //proc 弹幕 owner 侧生成；滴伤 18% 封 6..80，珠伤在铅滴溅裂时按滴伤 40% 折算
            if (player.whoAmI == Main.myPlayer) {
                int dropDamage = Math.Clamp((int)(damageDone * 0.18f), 6, 80);
                for (int i = 0; i < 3; i++) {
                    Vector2 at = target.Center + new Vector2(Main.rand.NextFloat(-60f, 60f), -240f);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithLeadEndow"),
                        at, new Vector2(0f, 5f),
                        ModContent.ProjectileType<GsLeadDropProj>(), dropDamage, 1f, player.whoAmI);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击崩落两层铅坠
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Lead, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 沉铅雨滴：一滴有重量的哑光铅液，不是光点。自高处加速坠落，砸中 NPC 或触地
    /// 溅裂成两枚低抛铅珠；泪滴形三层正常 alpha 叠色（暗蓝灰边/铅灰体/闷白微光压到 0.3），
    /// 无亮芯无加色，命中挂铅毒，落地沉闷噗声
    /// </summary>
    internal class GsLeadDropProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //沉铅只认重力：持续加速下坠，封顶 18
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.35f, 18f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行相：滴身剥落铅灰微尘
            if (!Main.dedServ && Life % 5 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.4f,
                    DustID.Lead, Projectile.velocity * 0.08f, 130, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 120);

        public override void OnKill(int timeLeft) {
            //命中/触地共用：溅裂成两枚低抛铅珠 + 灰溅尘 + 沉闷噗声
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 4f),
                        DustID.Lead, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.5f, 2.5f)),
                        100, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = false;
                }
            }
            //铅珠 owner 侧生成，珠伤 = 滴伤 40%
            if (Projectile.owner == Main.myPlayer) {
                int beadDamage = Math.Max(1, (int)(Projectile.damage * 0.4f));
                for (int i = 0; i < 2; i++) {
                    float dir = i == 0 ? -1f : 1f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        Projectile.Center, new Vector2(2.5f * dir, -3.5f),
                        ModContent.ProjectileType<GsLeadBeadProj>(), beadDamage, 0.5f, Projectile.owner);
                }
            }
        }

        //==================== 绘制：泪滴形三层哑光铅，正常 alpha，无亮芯 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            //下坠速度拉伸 Y：越快滴形越长
            float stretch = MathHelper.Clamp(Projectile.velocity.Y * 0.018f, 0f, 0.3f);

            //液滴张力微抖，确定性相位
            float wob = MathF.Sin(Life * 0.5f + Seed * 6f) * 0.08f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.7f);

            //暗蓝灰压边
            Main.EntitySpriteDraw(tex, pos, null, GsLeadArmor.LeadEdge * (0.85f * fade), rotation, origin,
                new Vector2(0.19f, 0.27f + stretch) * jiggle, SpriteEffects.None, 0);
            //铅灰滴体
            Main.EntitySpriteDraw(tex, pos, null, GsLeadArmor.LeadBody * fade, rotation, origin,
                new Vector2(0.15f, 0.22f + stretch * 0.85f) * jiggle, SpriteEffects.None, 0);
            //闷白微光：正常 alpha 压到 0.3，哑光质感（刻意不加色不提亮）
            Main.EntitySpriteDraw(tex, pos, null, GsLeadArmor.LeadSheen * (0.3f * fade), rotation, origin,
                new Vector2(0.07f, 0.11f + stretch * 0.4f) * jiggle, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 铅珠：铅滴溅裂出的小珠，低抛坠地即碎；缩小版两层哑光叠色（暗蓝灰边/铅灰体），
    /// 命中挂铅毒，触地灭
    /// </summary>
    internal class GsLeadBeadProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //低抛：重力 0.3
            Projectile.velocity.Y += 0.3f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 120);

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //碎珠余痕：几粒铅灰尘落地
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Lead,
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.3f, 1.5f)),
                    110, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = false;
            }
        }

        //==================== 绘制：缩小版两层哑光铅珠 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.015f, 0f, 0.18f);
            float wob = MathF.Sin(Life * 0.55f + Seed * 6f) * 0.07f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.7f);

            //暗蓝灰压边
            Main.EntitySpriteDraw(tex, pos, null, GsLeadArmor.LeadEdge * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.12f, 0.15f + stretch) * jiggle, SpriteEffects.None, 0);
            //铅灰珠体
            Main.EntitySpriteDraw(tex, pos, null, GsLeadArmor.LeadBody * fade, Projectile.rotation, origin,
                new Vector2(0.09f, 0.12f + stretch * 0.8f) * jiggle, SpriteEffects.None, 0);
            return false;
        }
    }
}
