// ============================================================================
//LampeaterVeil.fx 噬灯魂吞光域（全屏后效，EndCaptureDraw 拷屏回写）
//「食灯与无光」的屏幕语言：妖火周身一圈光被吃掉的暗带——
//  暗带=对拷屏乘法压暗+抽色（屏幕后效天然能变暗，不踩加色暗层陷阱），
//  中心留豁口给烬芯（暗漂态的下限保险丝：它永远是自己黑暗里唯一的火星）；
//  光被拉进去：暗带内沿径向向外采样拷屏亮度、叠回当前像素=亮部内移错觉，
//  倒吸期（uInhale）拉痕增密加速；
//  咬中坍缩（uPulse*）：亮缘从外向内塌的收环+环内瞬时加深；
//  死亡释放（uBurst*）：吞下的光炸开的外扩暖环+中心余晖。
//联机：本层纯本地表现，输入全部来自各端同步的 NPC ai 状态与本地前进沿观察，
//  服务器不跑（RenderHandle 客户端管线）。
//坐标全笛卡尔；径向流噪声输入=单位方向向量×标量（NeutronWarp 修法），无 atan2 接缝。
//绑定噪声 PerlinNoise 实测值域 0.227~0.776，阈值过 nrm() 归一。
//消费入口 Content/Scenarios/Dungeonworld/NPCs/Elites/LampeaterWispRendering.cs
// ============================================================================

sampler uScreen : register(s0);   //拷屏
sampler uNoiseTex : register(s1); //PerlinNoise，LinearWrap

float uTime;    //秒
float uAspect;  //屏宽/屏高

//每只妖火一个域槽（场上单型上限 2）：xy=屏幕uv中心 z=半径(屏高归一) w=暗带强度0~1
float4 uWisp0;
float4 uWisp1;
//x=槽0倒吸0~1 y=槽1倒吸
float2 uInhalePair;
//x=槽0烬芯亮度0~1 y=槽1（豁口里透出的暖意）
float2 uEmberPair;

//咬中坍缩脉冲：xy=uv z=起始半径(屏高归一) w=进度0~1（半径收向0）
float4 uPulse0;
float4 uPulse1;
//死亡释放：xy=uv z=最大半径(屏高归一) w=进度0~1（半径向外扩）
float4 uBurst0;
float4 uBurst1;

static const float3 LUMW = float3(0.299, 0.587, 0.114);
static const float3 SUCK_WARM = float3(0.95, 0.82, 0.58);   //被拉走的光偏暖（灯火色）
static const float3 EMBER_WARM = float3(1.00, 0.72, 0.36);  //豁口透出的烬色
static const float3 RELEASE_GOLD = float3(1.00, 0.86, 0.55);//死亡释放的灯火金

float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

float EaseOutCubic(float t) {
    float k = 1.0 - t;
    return 1.0 - k * k * k;
}

//单只妖火的域贡献：veil=暗带强度，suck=拉光强度带
void WispField(float2 uv, float4 wisp, float inhale,
               out float veil, out float suck, out float2 dvOut, out float dOut) {
    veil = 0.0;
    suck = 0.0;
    dvOut = float2(0.0, 0.0);
    dOut = 1e5;
    if (wisp.w <= 0.001 || wisp.z <= 0.001) {
        return;
    }
    float2 dv = (uv - wisp.xy) * float2(uAspect, 1.0);
    float d = length(dv) / wisp.z;   //以域半径归一
    dvOut = dv;
    dOut = d;

    //缘口呼吸：暗带外沿被噪声啃出参差（域不是硬圆；单位方向向量喂噪声，无接缝）
    float2 nd = dv / max(length(dv), 1e-4);
    float edgeN = nrm(tex2D(uNoiseTex, nd * 0.85 + 0.5 + uTime * 0.03).r);
    float dJ = d + (edgeN - 0.5) * 0.16;

    //环带形暗层：中心豁口（烬芯保险丝）→ 中带最暗 → 外沿渐没
    float belt = smoothstep(1.05, 0.46, dJ) * smoothstep(0.05, 0.30, dJ);
    //倒吸时域收紧变深
    belt *= 1.0 + 0.35 * inhale * smoothstep(0.9, 0.4, dJ);
    veil = saturate(wisp.w * belt);

    //拉光带：中外圈才有拉痕（豁口附近让给烬芯）
    suck = smoothstep(1.10, 0.55, dJ) * smoothstep(0.16, 0.42, dJ) * wisp.w;
}

//咬中坍缩：亮缘收环 + 环内瞬时加深
//收环用 ease-in（慢起步、加速塌向中心）——ease-out 会让环在前 1/4 进度就塌完，
//剩余时间全是不可见的残影；加速吸入也更贴"光被咬走"的读感
float3 ApplyPulse(float3 col, float2 uv, float4 pulse) {
    if (pulse.w <= 0.001 || pulse.w >= 0.999 || pulse.z <= 0.001) {
        return col;
    }
    float2 dv = (uv - pulse.xy) * float2(uAspect, 1.0);
    float d = length(dv);
    float pr = pulse.z * (1.0 - pulse.w * pulse.w * pulse.w);
    float fade = 1.0 - pulse.w;
    float ndr = (d - pr) / max(pulse.z, 0.001);
    float rim = exp2(-ndr * ndr * 180.0) * fade;
    float inside = smoothstep(pr, pr * 0.45, d) * fade;
    col *= 1.0 - inside * 0.22;
    col += SUCK_WARM * rim * 0.85;
    return col;
}

//死亡释放：外扩暖环 + 中心余晖
float3 ApplyBurst(float3 col, float2 uv, float4 burst) {
    if (burst.w <= 0.001 || burst.w >= 0.999 || burst.z <= 0.001) {
        return col;
    }
    float2 dv = (uv - burst.xy) * float2(uAspect, 1.0);
    float d = length(dv);
    float br = burst.z * EaseOutCubic(burst.w);
    float fade = 1.0 - burst.w;
    float ndr = (d - br) / max(burst.z, 0.001);
    float rim = exp2(-ndr * ndr * 90.0) * fade;
    float dn = d / max(burst.z, 0.001);
    float wash = exp2(-dn * dn * 8.0) * fade;
    col += RELEASE_GOLD * (rim * 0.90 + wash * 0.30);
    return col;
}

float4 PSVeil(float2 coords : TEXCOORD0) : COLOR0 {
    float3 scene = tex2D(uScreen, coords).rgb;

    float veil0, suck0, veil1, suck1;
    float2 dv0, dv1;
    float d0, d1;
    WispField(coords, uWisp0, uInhalePair.x, veil0, suck0, dv0, d0);
    WispField(coords, uWisp1, uInhalePair.y, veil1, suck1, dv1, d1);
    float veil = 1.0 - (1.0 - veil0) * (1.0 - veil1);

    //吃光=先抽色再压暗（色彩也被吞掉）；沙盒实测 0.38/0.52 在暗场景里几乎不可读，加深
    float lum = dot(scene, LUMW);
    float3 col = lerp(scene, float3(lum, lum, lum), veil * 0.52);
    col *= 1.0 - veil * 0.66;

    //---- 拉光：径向向外采样亮度叠回来，亮部读作被拽向妖火 ----
    if (suck0 > 0.003) {
        float inh = uInhalePair.x;
        float2 nd = dv0 / max(length(dv0), 1e-4);
        //径向流噪声：单位方向×(标量-时间) → 纹样沿径向内涌
        float streakN = nrm(tex2D(uNoiseTex, nd * (0.70 + d0 * 1.9 - uTime * (0.55 + 1.3 * inh))).r);
        float streak = saturate((streakN - 0.58) * 3.4) * suck0;
        float2 stepUv = nd * float2(1.0 / uAspect, 1.0) * uWisp0.z * (0.10 + 0.10 * inh);
        float3 tapA = tex2D(uScreen, coords + stepUv).rgb;
        float3 tapB = tex2D(uScreen, coords + stepUv * 2.1).rgb;
        float pulled = max(dot(tapA, LUMW), dot(tapB, LUMW));
        col += SUCK_WARM * pulled * streak * (0.34 + 0.55 * inh);
    }
    if (suck1 > 0.003) {
        float inh = uInhalePair.y;
        float2 nd = dv1 / max(length(dv1), 1e-4);
        float streakN = nrm(tex2D(uNoiseTex, nd * (0.70 + d1 * 1.9 - uTime * (0.55 + 1.3 * inh))).r);
        float streak = saturate((streakN - 0.58) * 3.4) * suck1;
        float2 stepUv = nd * float2(1.0 / uAspect, 1.0) * uWisp1.z * (0.10 + 0.10 * inh);
        float3 tapA = tex2D(uScreen, coords + stepUv).rgb;
        float3 tapB = tex2D(uScreen, coords + stepUv * 2.1).rgb;
        float pulled = max(dot(tapA, LUMW), dot(tapB, LUMW));
        col += SUCK_WARM * pulled * streak * (0.34 + 0.55 * inh);
    }

    //---- 豁口烬芯暖意：域中心不但不暗，还有一点火色透出 ----
    if (veil0 > 0.001 || suck0 > 0.001) {
        col += EMBER_WARM * exp2(-d0 * d0 * 14.0) * 0.28 * uEmberPair.x * uWisp0.w;
    }
    if (veil1 > 0.001 || suck1 > 0.001) {
        col += EMBER_WARM * exp2(-d1 * d1 * 14.0) * 0.28 * uEmberPair.y * uWisp1.w;
    }

    //---- 咬中坍缩 / 死亡释放 ----
    col = ApplyPulse(col, coords, uPulse0);
    col = ApplyPulse(col, coords, uPulse1);
    col = ApplyBurst(col, coords, uBurst0);
    col = ApplyBurst(col, coords, uBurst1);

    return float4(col, 1.0);
}

technique TechVeil {
    pass P0 {
        PixelShader = compile ps_3_0 PSVeil();
    }
}
