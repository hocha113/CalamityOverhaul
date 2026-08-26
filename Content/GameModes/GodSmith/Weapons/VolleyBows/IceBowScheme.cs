using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 寒冰弓（重铸 112%）：原版任意箭转高速霜箭保留。齐射成「冰凌雨」：
    /// 六凌扇形上抛、落于准星区 140px 俯冲成凌（55% each + 霜火）。
    /// 冻标由冰凌自理（叠 3 层再中触发 100px 碎冰爆 80%）。
    /// 期望：齐射 +（6×0.55−1）/21 ≈ +11%，处决 ≈ +2%
    /// </summary>
    internal class GsIceBow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.IceBow;

        protected override string GsDescFallback =>
            "Reforged: volley charge hurls 6 icicles skyward to rain on the cursor, one ammo per volley\nIcicle hits stack frostbrand; at 3 stacks the next icicle shatters into a frost burst";

        protected override int VolleyCount => 6;
        protected override float ChargePerShot => 5f;
        protected override float SideArrowMul => 0.55f;
        protected override int MarksPerVolleyHit => 0;
        protected override Color TrailColor => new(140, 210, 255);

        /// <summary>冰凌雨：全部走自治冰凌弹（Misc 源），上抛扇形与落点散布由 owner 端一次性定参</summary>
        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count) {
            Vector2 aim = Main.MouseWorld;
            int icicleDamage = (int)(damage * SideArrowMul);
            for (int i = 0; i < count; i++) {
                //扇形上抛：左右均分的仰角束
                float lean = (i - (count - 1) * 0.5f) * 1.4f;
                Vector2 vel = new(lean + Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(11.5f, 14f));
                float fallX = aim.X + Main.rand.NextFloat(-70f, 70f);
                Projectile.NewProjectile(player.GetSource_Misc("GsIcicleRain"),
                    position + new Vector2(lean * 4f, -6f), vel,
                    ModContent.ProjectileType<GsIcicleRainProj>(), icicleDamage, knockback * 0.6f,
                    player.whoAmI, fallX);
            }
        }
    }
}
