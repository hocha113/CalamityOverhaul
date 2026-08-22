// ============================================================================
//OniInkThread.fx 蜘蛛切「墨丝」/ 綴樋「缀痕」共用的墨丝介质
//
//ThreadTech：锚间那根丝。丝不是一条均匀亮线，而是一束，带内切 4 根独立子丝，
//  各自有相位与粗细；松弛时子丝散开、走暗墨、低频大摆（垂坠感由 C# 侧的悬链
//  折线给出，着色器只负责"这是一束湿墨纤维"）；绷紧时子丝并拢、带宽收窄、
//  转纸白并起高频细颤（张力越高抖得越快越小，读作真被拉直了）。
//  uSnap 是收紧那一帧的过曝脉冲，沿丝自锚端向中段扫过。
//  末端 uFray 让两端散成毛丝再断，避免"直线贴图突然消失"。
//
//AnchorTech：钉进目标身上的那枚锚。暗墨钻孔 + 四道倒刺，绷紧时孔缘起白热，
//  读作"这根丝真的挂在肉上"。
//
//极角审计：AnchorPS 的 phi=atan2 只进 cos(4*phi) 这类 2π 周期项；
//  噪声一律吃 p/r（连续单位向量）与 quad uv，无裸 phi 进噪声，故无 ±π 接缝。
//  ThreadTech 全程 uc/v 带坐标，不含极角。
//预乘 alpha 输出，配 BlendState.AlphaBlend；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uSeed;        //实例随机相位
float uTension;     //0..1 松弛→绷紧
float uSnap;        //0..1 收紧过曝脉冲
float uFray;        //0..1 端部散毛
float uOpacity;     //整体不透明度
float uLengthScale; //丝长/参考长，用于沿丝方向的噪声频率归一，长短丝纹理密度一致

float3 uColHot;     //纸白高光
float3 uColBright;  //绯红湿墨
float3 uColDark;    //漆黑墨底

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

#define PI 3.14159265

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

// ============================ ThreadTech ============================

float4 ThreadPS(PSInput input) : COLOR0
{
    float uc = saturate(input.TexCoords.x);
    //v: -1 带一侧 → +1 另一侧
    float v = (input.TexCoords.y - 0.5) * 2.0;

    //端部收束：两端并进锚里，不是被裁断的
    float capA = smoothstep(0.0, 0.06, uc);
    float capB = smoothstep(0.0, 0.06, 1.0 - uc);
    float cap = capA * capB;
    if (cap <= 0.001)
        return float4(0, 0, 0, 0);

    //散毛：末段按噪声开洞，先毛后断
    float frayN = tex2D(noiseSamp, float2(uc * 7.0 * uLengthScale + uSeed, 0.31)).r;
    float frayEdge = 1.0 - smoothstep(0.0, 0.55, min(uc, 1.0 - uc) * 2.4);
    float frayCut = step(frayN, 1.0 - uFray * frayEdge * 1.35);

    //绷紧程度决定：带宽、子丝并拢度、颤动频率与幅度
    float taut = saturate(uTension);
    float bandHalf = lerp(0.92, 0.34, taut);      //松弛铺开，绷紧收成一线
    float spread = lerp(0.62, 0.10, taut);        //子丝彼此的横向散度
    float trembleFreq = lerp(3.2, 26.0, taut);    //绷紧才高频
    float trembleAmp = lerp(0.30, 0.07, taut);    //绷紧幅度反而更小

    float acc = 0.0;
    float coreAcc = 0.0;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float fi = (float)i;
        float phase = uSeed * 6.28 + fi * 1.7;
        //子丝在带内的静态偏置 + 沿丝的行波颤动
        float bias = (fi - 1.5) / 1.5 * spread;
        float wob = sin(uc * PI * trembleFreq * (0.8 + fi * 0.14) + uTime * (4.0 + taut * 22.0) + phase);
        //湿墨不是数学正弦：叠一层低频噪声让每根丝各有各的不匀
        float grain = tex2D(noiseSamp, float2(uc * 3.1 * uLengthScale + fi * 0.27 + uSeed, 0.61 + fi * 0.13)).r - 0.5;
        float center = bias + wob * trembleAmp + grain * 0.22 * (1.0 - taut * 0.6);

        //各丝粗细不同，主丝(i=1)最粗
        float thick = (i == 1 ? 0.34 : 0.18) * lerp(1.0, 0.72, taut);
        float d = abs(v - center) / max(thick, 1e-4);
        float fiber = saturate(1.0 - d);
        acc += fiber * fiber;
        if (i == 1)
            coreAcc = fiber * fiber * fiber;
    }
    acc = saturate(acc);

    //带外裁掉，保住"丝"而不是"雾条"
    float band = 1.0 - smoothstep(bandHalf, bandHalf + 0.22, abs(v));
    float alpha = acc * band * cap * frayCut;
    if (alpha <= 0.004)
        return float4(0, 0, 0, 0);

    //收紧脉冲：一道白从两锚端相向扫进中段
    float snapWave = 0.0;
    if (uSnap > 0.001)
    {
        float front = 1.0 - uSnap;
        float dist = abs(min(uc, 1.0 - uc) * 2.0 - front);
        snapWave = saturate(1.0 - dist * 6.0) * uSnap;
    }

    //色：松弛=漆墨压绯红，绷紧=绯红提到纸白，收紧帧全白
    float3 col = lerp(uColDark, uColBright, saturate(0.30 + taut * 0.75));
    col = lerp(col, uColHot, saturate(coreAcc * (0.25 + taut * 0.55) + snapWave));
    //湿墨反光：核心一线更亮，边缘吃暗，别做成均匀发光棒
    col += uColHot * coreAcc * 0.30 * taut;

    alpha *= uOpacity * saturate(0.55 + taut * 0.55 + snapWave * 0.8);
    return float4(col * alpha, alpha);
}

// ============================ AnchorTech ============================

float4 AnchorPS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0)
        return float4(0, 0, 0, 0);

    //单位向量喂噪声：连续，无 ±π 接缝
    float2 dir = r > 1e-4 ? p / r : float2(1, 0);
    float phi = atan2(p.y, p.x);

    //四道倒刺：cos(4φ) 是 2π 周期项，接缝安全
    float barb = pow(saturate(cos(phi * 4.0 + uSeed * 6.28)), 6.0);
    float barbReach = 0.42 + barb * 0.52;

    //钻孔本体：暗心 + 一圈毛边
    float rough = tex2D(noiseSamp, dir * 0.5 + 0.5 + float2(uSeed, uTime * 0.03)).r - 0.5;
    float bore = 1.0 - smoothstep(barbReach * 0.55 + rough * 0.06, barbReach + rough * 0.10, r);
    if (bore <= 0.004)
        return float4(0, 0, 0, 0);

    float taut = saturate(uTension);
    //孔缘：绷紧时被拽得发白，读作"丝在扯这块肉"
    float rim = smoothstep(barbReach * 0.30, barbReach * 0.72, r) * bore;

    float3 col = lerp(uColDark, uColBright, 0.18 + taut * 0.42);
    col = lerp(col, uColHot, saturate(rim * (0.25 + taut * 0.70) + uSnap));

    float alpha = bore * uOpacity * (0.72 + taut * 0.28);
    return float4(col * alpha, alpha);
}

technique ThreadTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 ThreadPS();
    }
}

technique AnchorTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 AnchorPS();
    }
}
