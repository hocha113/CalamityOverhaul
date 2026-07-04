// ============================================================================
//OniKamuiFlow.fx 鬼切神威流带（冲刺路径的黑红墨流）
//路径三角带：uv.x=u 沿冲刺轨迹 0=起点(尾) 1=玩家(头)，uv.y 横向 0..1
//
//与刀光/雷光的分野：这不是"扫掠揭开"的一次性形体，而是一条持续涌动的墨绸——
//  1) 双八度流动噪声沿 u 以不同速度回卷（带内视差），再叠低频 domain warp
//     让流丝打卷涂抹，"神威在涌"的关键；
//  2) 轮廓是低频大舌 + 高频细齿的撕裂绸缎（非电弧的高频抖动），撕裂度向尾端递增，
//     尾端常态细碎剥落；
//  3) 色带沿 u 从头端白热→亮绯红→深红→黑红，暗涡处压向近黑（AlphaBlend 才画得出
//     "比背景更黑"），白热中脊只属于头段——图一那道横光的对应物；
//  4) uRetract 止步回抽：起点端先蒸发、向玩家端推进，蒸发前沿带烧蚀橙边
//
//噪声坐标全部由 (s=u*uLenScale, cy, uTime, uSeed) 笛卡尔构成，无 atan2/极角消费链
//——极角审计合规。s 以世界路径长度归一（uLenScale=路径px/噪声瓦片px），
//冲刺途中路径变长时墨纹钉在世界空间不随拉伸游动。
//预乘 alpha 输出，配 BlendState.AlphaBlend；核心/白闪走 alpha 之外的加色余量
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uOpacity;     //整体不透明度
float uRetract;     //0..1 起点端定向蒸发进度（止步回抽）
float uLenScale;    //路径长 / 噪声瓦片长（沿带噪声重复次数）
float uSeed;        //实例/子带随机相位
float uFlowMul;     //流速倍率（子带各不同 → 层间视差）
float uTearAmp;     //轮廓撕裂幅度（0=光滑绸带 1+=碎舌）
float uHeadBoost;   //头段白热中脊强度
float uFlash;       //0..1 全形过曝帧（起步/引爆瞬间）

float3 uColHot;     //白热
float3 uColBright;  //亮绯红
float3 uColDeep;    //深红
float3 uColDark;    //暗酒红（黑红底）

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float u = input.TexCoords.x;               //0=尾(起点) 1=头(玩家)
    float cy = (input.TexCoords.y - 0.5) * 2.0; //-1..+1 横向
    float s = u * uLenScale;                   //世界稳定的沿带坐标

    //---- domain warp：低频卷曲场，后续所有流动采样共享 ----
    //噪声贴图为亮度型(r=g=b)，两分量须错位采样取得独立通道
    float2 wUV = float2(s * 0.16 - uTime * 0.055 * uFlowMul, cy * 0.22 + uSeed * 3.1);
    float2 warp = float2(tex2D(noiseSamp, wUV).r
        , tex2D(noiseSamp, wUV + float2(0.31, 0.47)).r) - 0.5;
    warp *= 0.34;

    //---- 双八度流动：不同流速的视差层 ----
    float2 f1UV = float2(s * 0.42 - uTime * 0.85 * uFlowMul, cy * 0.30 + uSeed) + warp;
    float2 f2UV = float2(s * 1.15 - uTime * 1.65 * uFlowMul + 0.37, cy * 0.55 + uSeed * 1.7) + warp * 1.8;
    float n1 = tex2D(noiseSamp, f1UV).r;
    float n2 = tex2D(noiseSamp, f2UV).r;
    float flow = n1 * 0.62 + n2 * 0.38;

    //---- 头端收束：末 ~26% 边界向中脊聚拢成尖（彗星鼻形，pow<1 → 尖端快张缓平）----
    float taper = pow(saturate((1.0 - u) / 0.26), 0.58);

    //---- 撕裂轮廓：大舌 + 细齿，向尾端递增；收束段轮廓同步免撕（尖要干净） ----
    float tear = uTearAmp * (0.35 + 0.65 * (1.0 - u)) * saturate(taper * 1.6);
    float bN = tex2D(noiseSamp, float2(s * 0.30 - uTime * 0.45 * uFlowMul, 0.15 + uSeed * 0.53)).r;
    float bN2 = tex2D(noiseSamp, float2(s * 0.95 - uTime * 0.90 * uFlowMul, 0.66 + uSeed)).r;
    float boundary = (0.98 - tear * (0.62 * bN + 0.30 * bN2)) * taper;
    //羽化宽度随收束缩窄，尖端不糊
    float aEdge = smoothstep(boundary, boundary - (0.26 * taper + 0.035), abs(cy));
    if (aEdge < 0.004)
        return float4(0, 0, 0, 0);

    //---- 存活/蒸发：回抽从尾端推进 + 尾端常态剥碎，前沿烧蚀 ----
    //斜率 2.3：uRetract=1 时头端阈值 1.15 > 噪声上界，保证擦净不留残膜
    float eTh = uRetract * 2.3 - u * 1.15 + (1.0 - u) * 0.16 * uTearAmp;
    float survive = smoothstep(eTh - 0.03, eTh + 0.16, flow);
    float burn = smoothstep(eTh - 0.18, eTh - 0.03, flow) * (1.0 - survive);

    //---- 尾端羽化（头端交给收束尖，不再平切） ----
    float capA = smoothstep(0.0, 0.055, u);

    //---- alpha：流丝透密调制 ----
    float body = saturate(0.30 + flow * 1.05);
    float alpha = aEdge * capA * survive * body;
    alpha = saturate(alpha * lerp(1.0, 1.35, saturate(uFlash)));
    alpha *= uOpacity;
    if (alpha < 0.004 && burn < 0.05)
        return float4(0, 0, 0, 0);

    //---- 色带：头白热 → 亮绯红 → 深红 → 黑红尾 ----
    float heat = saturate(pow(u, 1.55));
    float3 col = lerp(uColDark, uColDeep, smoothstep(0.0, 0.45, heat));
    col = lerp(col, uColBright, smoothstep(0.45, 0.86, heat));
    col = lerp(col, uColHot, smoothstep(0.86, 1.0, heat) * 0.85);

    //内部亮丝：高频流层的高值段拉出绯红流线
    float filament = smoothstep(0.55, 0.95, n2);
    col += uColBright * filament * (0.55 + heat * 0.9) * 0.60;

    //暗涡：低值处压向近黑（墨的身体）
    col = lerp(col, uColDark * 0.65, smoothstep(0.42, 0.05, flow) * (1.0 - heat) * 0.85);

    //白热中脊：只属于头段，向尾迅速让位给墨；宽度随收束聚拢，
    //亮度向尖端增益——能量收进那一点
    float coreW = 0.24 * max(taper, 0.07);
    float core = exp(-pow(cy / coreW, 2.0)) * smoothstep(0.45, 0.95, u) * uHeadBoost;
    core *= 1.0 + (1.0 - taper) * 0.75;
    col += uColHot * core * 1.35;

    //蒸发前沿烧蚀橙边
    col += float3(1.30, 0.44, 0.20) * burn * 2.2;

    //全形白闪：提亮一拍而非擦掉重画
    col = lerp(col, col + uColHot * 0.60, saturate(uFlash));

    //预乘输出 + 核心/闪光的加色余量（半加法辉光）
    float3 extra = uColHot * (core * 0.35 + saturate(uFlash) * 0.12) * capA * survive * uOpacity;
    return float4(col * alpha + extra, alpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
