using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    /// <summary>
    /// 澶ф瘮鐩奔鐨勪娇鐢ㄥ姩鐢伙細鏈濋紶鏍囨柟鍚戞寔鎻?+ 澶嶅悎鍓嶈噦璺熼殢 + 杞诲井璧锋墜鎽嗗姩<br/>
    /// 杩滅▼鐜╁鐨勭瀯鍑嗘湞鍚戠敱妗嗘灦榛樿鐨勭帺瀹剁綉缁滃悓姝ワ紙InnoVault PlayerNetwork锛夐┍鍔紝鏃犻渶鏈鍣ㄨ嚜琛岃仈缃?
    /// </summary>
    internal class HalibutUseAnimation : AimedHoldAnimation
    {
        public override int TargetID => HalibutOverride.ID;
        /// <summary>姝﹀櫒涓績娌挎寔鎻℃柟鍚戣窛鐜╁绋冲畾涓績鐨勮窛绂?/summary>
        public override float HoldDistance => 7f;
        /// <summary>鎸佹彙绮剧伒鐨勫師鐐瑰亸绉伙紝浣挎彙鎶婂鍑嗘墜閮?/summary>
        public override Vector2 HoldOrigin => new Vector2(-40, 6);
        /// <summary>璧锋墜鏃舵墜鑷傜殑杞诲井鎽嗗姩骞呭害锛堝姬搴︼級</summary>
        public override float SwingStrength => 0.06f;
        /// <summary>鎽嗗姩鍙戠敓鍦ㄤ娇鐢ㄥ姩鐢荤殑鍓?40%</summary>
        public override float SwingPhase => 0.4f;
        /// <summary>涓?Terraria Overhaul 鐨勬寔鎻℃牱寮忓啿绐佹椂璁╀綅锛岀敱鏈ā缁勫垽鏂€岄潪妗嗘灦鍐呯疆</summary>
        public override bool Active(Item item, Player player) => CWRMod.Instance.terrariaOverhaul == null;
    }
}
