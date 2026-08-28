using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 祭火种（稻草人族死亡掉落 / 投火组抛掷落点 / 无头骑士冲锋沿途）。
    /// ai[0]=燃烧存续帧 ai[1]=风味+引燃延长×10（风味：0稻草人/1小木灵/2骑士）。
    /// 生命周期：引燃期（≥30 帧无害，玩家踩上即熄灭=互动）→ 燃烧期（判定窗=火光可见窗）→ 熄灭收场。
    /// 踩灭判定只在权威端执行，Kill 经弹幕原生同步下发；生成位置即锁定（地面静物，预告即承诺）
    /// </summary>
    internal class PmkEmberProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>基准引燃预告帧（公平契约 ≥30，踩灭窗口）</summary>
        internal const int KindleFrames = 34;
        private const int FadeFrames = 16;
        /// <summary>踩灭判定的外扩像素</summary>
        private const float StompInflate = 6f;

        private static readonly Color EmberWarm = new Color(255, 150, 48);
        private static readonly Color EmberDeep = new Color(122, 44, 16);

        private int LitFrames => Math.Max((int)Projectile.ai[0], 30);
        private int Flavor => (int)Projectile.ai[1] % 10;
        /// <summary>实例引燃期：基准之上只允许延长（踩灭窗只放宽不收紧，投火组闷燃型用）</summary>
        private int Kindle => KindleFrames + Math.Max(0, (int)Projectile.ai[1] / 10);
        private int TotalLife => Kindle + LitFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private bool Lit => Elapsed >= Kindle && Elapsed < Kindle + LitFrames;
        /// <summary>燃势 0~1（引燃渐起、熄灭渐落），绘制与灯光共用</summary>
        private float Blaze {
            get {
                int elapsed = Elapsed;
                int kindle = Kindle;
                if (elapsed < kindle) {
                    return 0.25f * (elapsed / (float)kindle);
                }
                if (elapsed < kindle + LitFrames) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - (elapsed - kindle - LitFrames) / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 20;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //寿命由已同步的 ai[0] 各端确定性展开
                Projectile.timeLeft = TotalLife;
            }

            int elapsed = Elapsed;
            //判定窗=火光可见窗
            Projectile.hostile = Lit;

            //踩灭互动：引燃期玩家覆压即熄（权威端裁决，Kill 原生同步；此期无判定零代价）
            if (!VaultUtils.isClient && elapsed < Kindle) {
                Rectangle stompRect = Projectile.Hitbox;
                stompRect.Inflate((int)StompInflate, (int)StompInflate);
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (player.active && !player.dead && !player.ghost && player.Hitbox.Intersects(stompRect)) {
                        Projectile.Kill();
                        return;
                    }
                }
            }

            if (elapsed == Kindle && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 4 }, Projectile.Center);
            }

            if (Main.dedServ) {
                return;
            }

            if (elapsed < Kindle) {
                //引燃期：细烟与零星火星（≤2 粒/帧）
                if (Main.rand.NextBool(3)) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Top + new Vector2(Main.rand.NextFloat(-6f, 6f), 2f),
                        DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f)), 150, default, 0.8f);
                    smoke.noGravity = true;
                }
                if (Main.rand.NextBool(6)) {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.4f, 1f)), 100, default, 0.9f);
                    spark.noGravity = true;
                }
            }
            else if (Lit && Main.rand.NextBool(2)) {
                //燃烧期：稳定火舌（≤2 粒/帧）
                Dust flame = Dust.NewDustPerfect(Projectile.Top + new Vector2(Main.rand.NextFloat(-8f, 8f), 4f),
                    DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.8f, 2.2f)), 90, default,
                    Main.rand.NextFloat(1f, 1.5f));
                flame.noGravity = true;
            }

            float blaze = Blaze;
            if (blaze > 0.05f) {
                Lighting.AddLight(Projectile.Center, EmberWarm.ToVector3() * 0.55f * blaze);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 120);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if (Elapsed < Kindle) {
                //引燃期被踩灭：烟尘一口
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.5f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                        new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.5f, 1.8f)), 140, default,
                        Main.rand.NextFloat(0.9f, 1.4f));
                    smoke.noGravity = Main.rand.NextBool();
                }
                return;
            }
            //烧尽：余烬散落
            for (int i = 0; i < 4; i++) {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.3f, 1.2f)), 110, default, 1f);
                ember.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float blaze = Blaze;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //底部暗焰衬（真 alpha，火种的暗轮廓）
            Texture2D under = CWRAsset.Extra_98.Value;
            Color underColor = EmberDeep * (0.35f + 0.4f * blaze);
            Main.EntitySpriteDraw(under, drawPos + new Vector2(0f, 4f), null, underColor, 0f,
                under.Size() / 2f, new Vector2(0.34f, 0.2f + 0.1f * blaze), SpriteEffects.None, 0);

            //本体：原版南瓜（实体层，有遮挡像素），随燃势向暖色抬升
            Main.instance.LoadItem(ItemID.Pumpkin);
            Texture2D pumpkin = TextureAssets.Item[ItemID.Pumpkin].Value;
            float wobble = Lit ? MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity) * 0.05f : 0f;
            Color body = Color.Lerp(lightColor, EmberWarm, 0.25f + 0.45f * blaze);
            //小木灵风味略偏枯木色，骑士风味略偏亮
            if (Flavor == 1) {
                body = Color.Lerp(body, new Color(150, 110, 70), 0.25f);
            }
            else if (Flavor == 2) {
                body = Color.Lerp(body, Color.White, 0.12f);
            }
            Main.EntitySpriteDraw(pumpkin, drawPos + new Vector2(0f, 2f), null, body, wobble,
                pumpkin.Size() / 2f, 0.85f, SpriteEffects.None, 0);

            //火光敷料（加色只做辉光）
            if (blaze > 0.05f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity * 0.7f);
                Color halo = (EmberWarm with { A = 0 }) * (0.55f * blaze * pulse);
                Main.EntitySpriteDraw(glow, drawPos - new Vector2(0f, 6f * blaze), null, halo, 0f,
                    glow.Size() / 2f, new Vector2(0.5f, 0.62f) * (0.6f + 0.5f * blaze), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
