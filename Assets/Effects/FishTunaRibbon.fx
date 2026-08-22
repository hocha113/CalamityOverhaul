// ============================================================================
//FishTunaRibbon.fx 激流一闪水绸带（突刺路径的青蓝液体条带）
//路径三角带：uv.x=u 沿突刺轨迹 0=起点(尾) 1=头(刃尖)，uv.y 横向 0..1
//
//与神威墨绸(OniKamuiFlow)同构不同料：那边是涌动的墨，这里是被刃体犁开的水
//  1) 双八度流动噪声沿 u 向尾端回卷（水甩在身后），低频 warp 让水丝打旋；
//  2) 轮廓大舌+细齿撕裂向尾端递增（尾散成飞沫），头端收束成刃尖；
//  3) 色带沿 u 头端亮水青→饱和青蓝→深海暗蓝，整体半透明（液体非能量），
//     白沫只作头段窄脊+零星转瞬亮斑，无常驻纯白大面积；
//  4) uRetract 余韵消散：尾端先化雾，消散前沿泛白沫碎
//
//噪声坐标全部由 (s=u*uLenScale, cy, uTime, uSeed) 笛卡尔构成，无极角消费链
//预乘 alpha 输出，配 BlendState.AlphaBlend；白沫走 alpha 之外的加色余量
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uOpacity;     //整体不透明度
float uRetract;     //0..1 尾端向头端的消散进度
float uLenScale;    //路径长/噪声瓦片长（沿带噪声重复次数）
float uSeed;        //实例/子带随机相位
float uFlowMul;     //流速倍率（子带各异 → 层间视差）
float uTearAmp;     //轮廓撕裂幅度（0=光滑绸带 1+=碎舌）
float uHeadBoost;   //头段白沫窄脊强度
float uFlash;       //0..1 出手过曝帧（≤2帧）

float3 uColDeep;    //深海暗蓝
float3 uColMid;     //饱和青蓝
float3 uColBright;  //亮水青
float3 uColFoam;    //白沫（偏青近白，仅小面积）

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
    float u = input.TexCoords.x;                //0=尾(起点) 1=头(刃尖)
    float cy = (input.TexCoords.y - 0.5) * 2.0; //-1..+1 横向
    float s = u * uLenScale;                    //世界稳定的沿带坐标

    //---- domain warp：低频卷曲场，水丝打旋 ----
    //噪声贴图为亮度型(r=g=b)，两分量错位采样取独立通道
    float2 wUV = float2(s * 0.14 + uTime * 0.05 * uFlowMul, cy * 0.20 + uSeed * 2.7);
    float2 warp = float2(tex2D(noiseSamp, wUV).r
        , tex2D(noiseSamp, wUV + float2(0.37, 0.53)).r) - 0.5;
    warp *= 0.30;

    //---- 双八度回卷：+uTime → 纹理向尾端退（水甩在身后），两速视差 ----
    float2 f1UV = float2(s * 0.40 + uTime * 0.70 * uFlowMul, cy * 0.28 + uSeed) + warp;
    float2 f2UV = float2(s * 1.10 + uTime * 1.45 * uFlowMul + 0.41, cy * 0.52 + uSeed * 1.6) + warp * 1.7;
    float n1 = tex2D(noiseSamp, f1UV).r;
    float n2 = tex2D(noiseSamp, f2UV).r;
    float flow = n1 * 0.60 + n2 * 0.40;

    //---- 头端收束成刃尖（pow<1 → 尖端快张缓平）----
    float taper = pow(saturate((1.0 - u) / 0.22), 0.60);

    //---- 撕裂轮廓：大舌+细齿，向尾端递增，收束段免撕（尖要干净） ----
    float tear = uTearAmp * (0.30 + 0.70 * (1.0 - u)) * saturate(taper * 1.6);
    float bN = tex2D(noiseSamp, float2(s * 0.30 + uTime * 0.40 * uFlowMul, 0.18 + uSeed * 0.49)).r;
    float bN2 = tex2D(noiseSamp, float2(s * 0.92 + uTime * 0.85 * uFlowMul, 0.63 + uSeed)).r;
    float boundary = (0.97 - tear * (0.60 * bN + 0.30 * bN2)) * taper;
    float aEdge = smoothstep(boundary, boundary - (0.28 * taper + 0.04), abs(cy));
    if (aEdge < 0.004)
        return float4(0, 0, 0, 0);

    //---- 消散：尾端先化，前沿泛白沫 ----
    //斜率 2.3：uRetract=1 时头端阈值 1.15 > 噪声上界，擦净不留残膜
    float eTh = uRetract * 2.3 - u * 1.15 + (1.0 - u) * 0.14 * uTearAmp;
    float survive = smoothstep(eTh - 0.03, eTh + 0.15, flow);
    float froth = smoothstep(eTh - 0.17, eTh - 0.03, flow) * (1.0 - survive);

    //---- 尾端羽化（头端交给收束尖） ----
    float capA = smoothstep(0.0, 0.06, u);

    //---- alpha：半透明液体，密度随流丝起伏，整体压在 ~0.9 以下 ----
    float body = saturate(0.22 + flow * 0.80);
    float alpha = aEdge * capA * survive * body;
    alpha = saturate(alpha * lerp(1.0, 1.30, saturate(uFlash))) * uOpacity * 0.92;
    if (alpha < 0.004 && froth < 0.05)
        return float4(0, 0, 0, 0);

    //---- 色带：尾深头亮 ----
    float heat = saturate(pow(u, 1.35));
    float3 col = lerp(uColDeep, uColMid, smoothstep(0.0, 0.50, heat));
    col = lerp(col, uColBright, smoothstep(0.50, 0.92, heat) * 0.85);

    //内部亮水丝：高频流层高值段拉出水线
    float filament = smoothstep(0.58, 0.95, n2);
    col += uColBright * filament * (0.30 + heat * 0.55);

    //暗涡：低流处沉向深蓝（水的身体）
    col = lerp(col, uColDeep * 0.72, smoothstep(0.40, 0.06, flow) * (1.0 - heat) * 0.80);

    //零星白沫亮斑：双八度高值交集，小面积、随流转瞬
    float fleck = smoothstep(0.86, 0.97, n1 * 0.45 + n2 * 0.55) * (0.20 + heat * 0.80);
    col += uColFoam * fleck * 0.55;

    //头段白沫窄脊：只属于头段，向尾迅速让位；宽度随收束聚拢
    float coreW = 0.22 * max(taper, 0.08);
    float cyN = cy / coreW;
    float core = exp(-cyN * cyN) * smoothstep(0.50, 0.94, u) * uHeadBoost;
    core *= 1.0 + (1.0 - taper) * 0.6;
    col += uColFoam * core * 0.85;

    //消散前沿白沫碎
    col += uColFoam * froth * 1.5;

    //出手过曝一拍：提亮而非擦掉重画
    col = lerp(col, col + uColFoam * 0.45, saturate(uFlash));

    //预乘输出 + 白沫的少量加色余量（半加法水光）
    float3 extra = uColFoam * (core * 0.22 + fleck * 0.08 + saturate(uFlash) * 0.10) * capA * survive * uOpacity;
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
