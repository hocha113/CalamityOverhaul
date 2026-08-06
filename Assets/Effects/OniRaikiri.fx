// ============================================================================
//OniRaikiri.fx 雷切「斩雷」：自天顶落下的垂直雷柱
//
//BoltTech：柱体不是一根发光棒，而是一条被劈开的空气。
//  1) 主干走"折线"而非直线：沿 v(自上而下)累积两层反向低频噪声位移，
//     位移量在落点处收敛为 0（雷是钉在目标上的，不能飘）；
//  2) 刃状分叉：按 v 切若干段，每段以噪声阈值决定是否甩出一条侧枝，
//     侧枝随 uAge 先长后焦，读作"电离通道烧完了"；
//  3) 三层色阶：芯=纸白过曝、体=旧金、缘=绯红洇边，禁纯白棒；
//  4) uAge 驱动整体：闪现(0~0.15)全宽过曝 → 主放电(~0.5)收窄见分叉
//     → 余辉(→1)只剩绯红残像上飘。
//
//GlowTech：落点地面的一圈焦痕辉光，压在柱底，给雷一个"落在哪"的着地感。
//
//极角审计：本文件无 atan2/极角，全部为 quad uv 的 u/v 带坐标，无接缝风险。
//预乘 alpha 输出，配 BlendState.AlphaBlend；ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uSeed;        //实例随机相位
float uAge;         //0..1 生命进度
float uJitter;      //折线位移强度(px 归一到 uv)
float uBranch;      //0..1 侧枝密度
float uOpacity;     //整体不透明度

float3 uColHot;     //纸白过曝芯
float3 uColBright;  //旧金体
float3 uColDeep;    //绯红洇边

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

//主干在高度 v 处的横向偏移；落点(v=1)收敛为 0
float TrunkOffset(float v)
{
    float n1 = tex2D(noiseSamp, float2(v * 2.3 + uSeed, 0.13 + uSeed * 0.5)).r - 0.5;
    float n2 = tex2D(noiseSamp, float2(v * 6.1 - uSeed * 0.7, 0.62)).r - 0.5;
    //越靠落点越收敛：雷钉在目标上，不许在脚下乱飘
    float converge = 1.0 - smoothstep(0.62, 1.0, v);
    return (n1 * 0.62 + n2 * 0.30) * uJitter * converge;
}

float4 BoltPS(PSInput input) : COLOR0
{
    float u = input.TexCoords.x;
    float v = saturate(input.TexCoords.y);
    float x = (u - 0.5) * 2.0;

    //三段生命：闪现 → 主放电 → 余辉
    float flash = 1.0 - smoothstep(0.0, 0.15, uAge);
    float decay = smoothstep(0.45, 1.0, uAge);
    float live = 1.0 - decay;

    float trunk = TrunkOffset(v);
    float d = abs(x - trunk);

    //柱宽：闪现期铺满，主放电收成一线，余辉再散一点
    float halfWidth = lerp(0.30, 0.085, smoothstep(0.0, 0.35, uAge)) + flash * 0.34 + decay * 0.06;
    float core = saturate(1.0 - d / max(halfWidth * 0.34, 1e-4));
    float body = saturate(1.0 - d / max(halfWidth, 1e-4));

    //侧枝：按段甩出斜向短枝，先长后焦
    float branch = 0.0;
    if (uBranch > 0.001)
    {
        [unroll]
        for (int i = 0; i < 3; i++)
        {
            float fi = (float)i;
            float seg = frac(v * (3.0 + fi) + uSeed * 3.1 + fi * 0.37);
            float pick = tex2D(noiseSamp, float2(floor(v * (3.0 + fi)) * 0.21 + uSeed, 0.4 + fi * 0.2)).r;
            if (pick < 0.52)
                continue;
            //枝自主干斜出，长度随 uAge 先增后灭
            float grow = saturate(uAge / 0.35) * (1.0 - decay);
            float reach = (pick - 0.5) * 1.7 * grow;
            float along = seg;
            float bx = trunk + reach * along;
            float bw = 0.05 * (1.0 - along) + 0.008;
            branch += saturate(1.0 - abs(x - bx) / max(bw, 1e-4)) * (1.0 - along) * uBranch;
        }
        branch = saturate(branch);
    }

    float alpha = saturate(body * body + core + branch * 0.85);
    //顶端没入天，底端钉在落点：两端不同的收法
    alpha *= smoothstep(0.0, 0.10, v);
    if (alpha <= 0.004)
        return float4(0, 0, 0, 0);

    //三层色阶：绯红洇边 → 旧金体 → 纸白芯，禁纯白棒
    float3 col = uColDeep;
    col = lerp(col, uColBright, saturate(body * 1.25));
    col = lerp(col, uColHot, saturate(core * 1.4 + flash));
    //余辉期整体退回绯红，只剩残像
    col = lerp(col, uColDeep, decay * 0.72);

    alpha *= uOpacity * saturate(live * 0.85 + flash * 0.9 + 0.10);
    return float4(col * alpha, alpha);
}

//落点辉光：贴地的一圈焦痕，给雷一个着地感
float4 GlowPS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    //压扁成贴地椭圆，而不是浮空的圆球
    p.y *= 2.6;
    float r = length(p);
    float ring = saturate(1.0 - r);
    if (ring <= 0.004)
        return float4(0, 0, 0, 0);

    float decay = smoothstep(0.35, 1.0, uAge);
    float3 col = lerp(uColBright, uColDeep, decay);
    col = lerp(col, uColHot, saturate(pow(ring, 5.0) * (1.0 - decay)));
    float alpha = pow(ring, 2.2) * uOpacity * (1.0 - decay * 0.85);
    return float4(col * alpha, alpha);
}

technique BoltTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 BoltPS();
    }
}

technique GlowTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 GlowPS();
    }
}
