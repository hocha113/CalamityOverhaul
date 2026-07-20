// ============================================================================
//FishAmanitaMist.fx 毒孢鱼泡浓稠孢子云（毒雾蘑菇部署态）
//瘴紫语系：uColDense 暗部压底、uColMist 主色、uColGlow 孢光亮斑（小面积）。
//体量骨架：域扭曲双频噪声密度 × 径向衰减，边缘由噪声撕裂（禁平滑收口）；
//uReveal 入场自中心长出，uErode 消散时低密度处先蚀。
//内部孢子亮斑：两层高阈值噪声点向上漂移，模拟云中悬浮发光孢子。
//极角审计：无 atan2/theta/phi 消费，全部为笛卡尔 uv + 贴图采样，无缝隙风险。
//premultiplied 输出（调用方设 BlendState.AlphaBlend，One/InvSrcAlpha）
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;          //全局时间
float uSeed;          //每实例噪声相位
float uReveal;        //0-1 入场展开
float uErode;         //0-1 消散侵蚀
float2 uSizePx;       //quad 像素尺寸
float3 uColDense;     //瘴紫暗部
float3 uColMist;      //瘴紫主色
float3 uColGlow;      //孢光青白，仅亮斑

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
    float2 uv = input.TexCoords;
    float aspect = uSizePx.x / max(uSizePx.y, 1.0);
    float2 p = (uv - 0.5) * float2(aspect, 1.0);
    float t = uTime;

    //域扭曲：低频噪声推挤采样域，云体缓慢翻卷
    float2 w = tex2D(noiseSamp, uv * 0.7 + float2(uSeed + t * 0.016, uSeed * 1.7 - t * 0.011)).rg;
    float2 uvw = uv + (w - 0.5) * 0.22;

    //双频密度场
    float n1 = tex2D(noiseSamp, uvw * 1.1 + float2(uSeed, -t * 0.028)).r;
    float n2 = tex2D(noiseSamp, uvw * 2.3 + float2(t * 0.021, uSeed * 2.3)).r;
    float density = n1 * 0.62 + n2 * 0.38;

    //径向衰减：入场时云自中心长出
    float grow = lerp(0.42, 1.0, uReveal);
    float d = length(p) / (0.5 * grow);
    float body = saturate(1.0 - d);

    //噪声撕裂边缘：密度调制阈值，边界永远参差
    float mask = smoothstep(0.12, 0.55, body * (0.45 + 0.9 * density));

    //消散侵蚀：低密度与边缘先蚀掉
    float life = density * 0.6 + body * 0.5;
    mask *= smoothstep(uErode * 1.3 - 0.1, uErode * 1.3 + 0.18, life);

    //调色：暗缘压底 → 密度处提到主色，饱和先于明度
    float3 col = lerp(uColDense * 0.55, uColDense, saturate(body * 1.6));
    col = lerp(col, uColMist, saturate(density * body * 1.9));

    //内部孢子亮斑：高阈值噪声点向上漂移，小面积孢光
    float m1 = tex2D(noiseSamp, uv * 3.6 + float2(t * 0.012 + uSeed, t * 0.05)).r;
    float m2 = tex2D(noiseSamp, uv * 5.8 + float2(-t * 0.017, t * 0.075 + uSeed * 3.1)).r;
    float motes = smoothstep(0.78, 0.93, m1) + smoothstep(0.82, 0.95, m2) * 0.7;
    col += uColGlow * motes * mask * 0.30;

    float alpha = mask * (0.72 + 0.12 * n2) * saturate(uReveal * 2.5);

    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
