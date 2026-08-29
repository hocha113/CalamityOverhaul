using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.LunaticCultist
{
    /// <summary>
    /// 镜像仪式的苍白镜身：绕身列阵的假身幻影，齐射三轮玩家当前武器的弹幕；<br/>
    /// ai[0]=槽位(0..2)；本体无判定纯演出，弹幕由 owner 端解析武器后生成<br/>
    /// 排除规则：召唤/鞭、手持类(CWR held 与 channel)、矛/悠悠球/链枷、爆炸物不复制，改射符文弹
    /// </summary>
    internal class RiteMirrorPhantom : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.localAI[0];
        private int Slot => (int)Projectile.ai[0];

        private const int EmergeFrames = 14;
        private const int DissolveStart = 106;
        private const int LifeFrames = 126;
        /// <summary>三轮齐射拍点</summary>
        private static readonly int[] VolleyTicks = [30, 58, 86];
        private const float OrbitRadius = 92f;
        /// <summary>镜射伤害系数（复制攻击梯度：Prime 0.24 T3b &lt; 本件 0.40 T4b &lt; 克脑 0.5 T1 窗口）</summary>
        private const float MirrorDamageFactor = 0.40f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Timer++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //绕身列阵：槽位相位+缓转+呼吸，位置是 owner 状态的确定函数，各端一致
            float angle = Slot * MathHelper.TwoPi / 3f - MathHelper.PiOver2
                + Main.GlobalTimeWrappedHourly * 0.55f;
            float radius = OrbitRadius + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Slot * 2.1f) * 8f;
            Projectile.Center = owner.Center + angle.ToRotationVector2() * radius
                + CultistMotion.BreathingOffset(Slot * 2.4f, 6f);
            Projectile.velocity = Vector2.Zero;

            //面向最近敌人
            NPC target = FindTarget(1200f);
            Projectile.spriteDirection = target != null
                ? Math.Sign(target.Center.X - Projectile.Center.X) : owner.direction;

            if ((int)Timer == 2) {
                CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PaleClone, 8, 5f);
                if (!VaultUtils.isServer && CultistMotion.OnScreen(Projectile.Center, 200f)) {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.1f },
                        Projectile.Center);
                }
            }
            //消散符雨
            if (Timer >= DissolveStart && Timer % 5 == 0) {
                CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PaleClone, 1, 3f);
            }

            //齐射拍：owner 端出弹，各端同拍施法闪
            for (int i = 0; i < VolleyTicks.Length; i++) {
                if ((int)Timer == VolleyTicks[i]) {
                    Vector2 aim = target != null
                        ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX * Projectile.spriteDirection)
                        : new Vector2(Projectile.spriteDirection, -0.05f).SafeNormalize(Vector2.UnitX);
                    CultistMotion.CastFlash(Projectile.Center + aim * 22f, CultistMotion.PaleClone, 0.65f);
                    if (Projectile.owner == Main.myPlayer) {
                        FireVolley(owner, aim);
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PaleClone.ToVector3() * 0.3f);
        }

        /// <summary>owner 端：解析当前武器弹幕并镜射（伤害折 40%），不可镜射时改射符文弹</summary>
        private void FireVolley(Player owner, Vector2 aim) {
            if (TryResolveWeaponShot(owner, out int shoot, out float speed, out int damage, out float kb)) {
                int mirrored = Math.Max(1, (int)(damage * MirrorDamageFactor));
                Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-0.04f, 0.04f)) * speed;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center + aim * 22f, vel, shoot, mirrored, kb, Projectile.owner);
            }
            else {
                int boltDamage = (int)owner.GetTotalDamage(DamageClass.Generic).ApplyTo(120f);
                Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-0.05f, 0.05f)) * 13f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center + aim * 22f, vel, ModContent.ProjectileType<RiteRuneBolt>(),
                    boltDamage, 3f, Projectile.owner);
            }
        }

        /// <summary>
        /// 武器弹幕解析。排除：CWR 手持类、channel 持续武器、召唤系与鞭、
        /// 矛/悠悠球/链枷/手持钻头、爆炸物、敌对样本；弹药武器经 PickAmmo(不消耗)取真实弹种
        /// </summary>
        internal static bool TryResolveWeaponShot(Player player, out int shoot, out float speed,
            out int damage, out float kb) {
            shoot = 0;
            speed = 11f;
            damage = 0;
            kb = 2f;

            Item held = player.HeldItem;
            if (held == null || held.IsAir || held.damage <= 0) {
                return false;
            }
            if (held.CWR()?.isHeldItem == true || held.channel) {
                return false;
            }
            if (held.DamageType.CountsAsClass(DamageClass.Summon)) {
                return false;
            }

            shoot = held.shoot;
            if (held.useAmmo != AmmoID.None) {
                //不消耗地解析弹药，取真实弹种与合算伤害
                if (!player.PickAmmo(held, out shoot, out speed, out damage, out kb, out _, true)) {
                    return false;
                }
            }
            else {
                damage = player.GetWeaponDamage(held);
                kb = held.knockBack;
                speed = held.shootSpeed;
            }

            if (shoot <= ProjectileID.None
                || !ContentSamples.ProjectilesByType.TryGetValue(shoot, out Projectile sample)) {
                return false;
            }
            if (sample.minion || sample.sentry || ProjectileID.Sets.IsAWhip[shoot]) {
                return false;
            }
            if (sample.aiStyle == ProjAIStyleID.Spear || sample.aiStyle == ProjAIStyleID.HeldProjectile
                || sample.aiStyle == ProjAIStyleID.Yoyo || sample.aiStyle == ProjAIStyleID.Flail) {
                return false;
            }
            if (ProjectileID.Sets.Explosive[shoot] || sample.aiStyle == ProjAIStyleID.Explosive) {
                return false;
            }
            if (sample.hostile) {
                return false;
            }

            if (speed <= 0.01f) {
                speed = 11f;
            }
            if (damage <= 0) {
                damage = player.GetWeaponDamage(held);
            }
            return true;
        }

        private NPC FindTarget(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Projectile.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.CultistBossClone);
            Texture2D tex = TextureAssets.Npc[NPCID.CultistBossClone].Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.CultistBossClone], 1);
            int frameHeight = tex.Height / frameCount;
            Rectangle frame = new(0, 0, tex.Width, frameHeight);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //透明度包络：聚形→驻场→散形
            float alpha = MathHelper.Clamp(Timer / EmergeFrames, 0f, 1f);
            if (Timer > DissolveStart) {
                alpha *= MathHelper.Clamp(1f - (Timer - DissolveStart) / (LifeFrames - DissolveStart), 0f, 1f);
            }

            SpriteEffects flip = Projectile.spriteDirection == 1
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //苍白假身：无光照依赖的幽灵体
            Color pale = Color.Lerp(Color.White, CultistMotion.PaleClone, 0.75f);
            if (glow != null) {
                Main.EntitySpriteDraw(glow, pos, null, CultistMotion.PaleClone with { A = 0 } * (0.35f * alpha),
                    0f, glow.Size() * 0.5f, 1.3f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, pos, frame, pale * (0.8f * alpha), 0f, origin, 1f, flip, 0);
            //同帧加色复写：镜身微微透光
            Main.EntitySpriteDraw(tex, pos, frame, CultistMotion.PaleClone with { A = 0 } * (0.3f * alpha),
                0f, origin, 1f, flip, 0);
            return false;
        }
    }

    /// <summary>
    /// 符文弹：镜身无法镜射武器时的回退弹，苍金符屑，轻微追踪
    /// </summary>
    internal class RiteRuneBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 160;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //12 帧后轻追踪：转向角限幅，速度恒定（各端从同步的 NPC 位置一致推导）
            if (Projectile.timeLeft < 148) {
                NPC target = null;
                float bestDist = 900f;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy()) {
                        continue;
                    }
                    float dist = Projectile.Distance(npc.Center);
                    if (dist < bestDist) {
                        bestDist = dist;
                        target = npc;
                    }
                }
                if (target != null) {
                    float speed = Projectile.velocity.Length();
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 current = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    //叉积/点积取带符号夹角，限幅转向
                    float delta = (float)Math.Atan2(current.X * desired.Y - current.Y * desired.X,
                        Vector2.Dot(current, desired));
                    Projectile.velocity = current.RotatedBy(MathHelper.Clamp(delta, -0.045f, 0.045f)) * speed;
                }
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_CultistRune>(
                    Projectile.Center, -Projectile.velocity * 0.06f,
                    Color.Lerp(CultistMotion.RuneGold, CultistMotion.PaleClone, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24));
            }
            Lighting.AddLight(Projectile.Center, CultistMotion.RuneGold.ToVector3() * 0.35f);
        }

        public override void OnKill(int timeLeft) {
            CultistMotion.ImpactBurst(Projectile.Center, 1, 0.65f, playSound: false);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D stroke = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (stroke == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color gold = CultistMotion.RuneGold with { A = 0 };
            Color pale = CultistMotion.PaleClone with { A = 0 };

            //残影链
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 ghost = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(stroke, ghost, null, pale * (0.28f * k), Projectile.rotation,
                    stroke.Size() * 0.5f, new Vector2(0.09f, 0.30f) * (0.7f + 0.3f * k), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(glow, pos, null, gold * 0.5f, 0f, glow.Size() * 0.5f, 0.34f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(stroke, pos, null, gold * 0.95f, Projectile.rotation,
                stroke.Size() * 0.5f, new Vector2(0.12f, 0.40f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(stroke, pos, null, Color.White with { A = 0 } * 0.6f, Projectile.rotation,
                stroke.Size() * 0.5f, new Vector2(0.06f, 0.28f), SpriteEffects.None, 0);
            return false;
        }
    }
}
