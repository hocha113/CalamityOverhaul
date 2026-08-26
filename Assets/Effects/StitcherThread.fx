// ============================================================================
//StitcherThread.fx 缝匠骨线材质（顶点条带）
//UV.x 0~1 沿线（0=手/源头 1=货端），UV.y 0/1 横向；预乘输出，AlphaBlend
//顶点色：R=张力(0松弛~1绷紧) G=磨损/将断(0~1) A=不透明度包络
//材质：打蜡骨线（腱线）。签名行为：
//①张力改变截面——松弛=宽软暗+低频摆晃；绷紧=窄亮芯+高频微颤
//②双股捻纹沿线慢滚（结构纹理，不是能量流光）
//③绷紧时亮点沿线奔向 u=1（能量方向=货物方向）
//④两端噪声撕散无平切；磨损把线身咬出缺口、毛边加剧
//宏观垂弧由 C# 顶点几何承担，shader 只做小尺度摆动
//噪声 s1=PerlinNoise，实测值域 0.227~0.776，阈值一律过 nrm() 归一
//无极角运算、无动态分支；标量参数逐线设置（每根线单独 DrawUserPrimitives）
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;      //实例相位
float uLenPx;     //线长（px），捻纹/玻点按像素定节距
float3 uThreadCol; //线主色（缝线金）
float3 uCoreCol;   //绷紧亮芯（近白暖金）
float3 uDarkCol;   //松弛暗侧（琥珀褐）

sampler noiseSamp : register(s1);

//实测值域归一（0.227~0.776 → 0~1）
float nrm(float raw)
{
    return saturate((raw - 0.227) / 0.549);
}

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
    float u = input.TexCoords.x;
    float x = input.TexCoords.y * 2.0 - 1.0; //-1~1 横截面
    float tension = input.Color.r;
    float fray = input.Color.g;

    //---- 松弛双频摆晃 + 绷紧高频微颤（中线偏移）----
    float slack = 1.0 - tension;
    float sway = (nrm(tex2D(noiseSamp, float2(u * 2.6 + uSeed, uTime * 0.11 + uSeed * 3.0)).r) - 0.5) * 0.9
               + (nrm(tex2D(noiseSamp, float2(u * 7.4 + uSeed * 5.0, uTime * 0.07 + uSeed)).r) - 0.5) * 0.5;
    sway *= slack;
    float quiver = sin(u * 34.0 + uTime * 26.0 + uSeed * 12.0) * tension * 0.10;
    float xc = x + sway + quiver;

    //---- 张力改变截面：松宽暗 / 紧窄亮 ----
    float wHalf = lerp(1.0, 0.52, tension);
    float q = xc / wHalf;
    float body = exp2(-6.5 * q * q);
    float core = exp2(-24.0 * q * q) * (0.30 + 0.70 * tension);

    //---- 双股捻纹：约 9px 一捻，慢滚 ----
    float tw = sin(u * uLenPx * 0.70 + xc * 2.4 - uTime * 2.2 + uSeed * 6.0) * 0.5 + 0.5;
    body *= 0.80 + 0.20 * tw;

    //---- 行进玻点（绷紧才有，奔向 u=1）----
    float gpos = frac(uTime * 0.9 + uSeed);
    float du = (u - gpos) * uLenPx / 9.0;
    float glint = exp2(-du * du) * tension;

    //---- 端口撕散 + 磨损缺口 ----
    float er = nrm(tex2D(noiseSamp, float2(u * 8.0 + uSeed * 7.0, xc * 0.6 + uSeed)).r);
    float endMask = smoothstep(0.0, 0.05 + 0.09 * er, u)
                  * smoothstep(1.0, 0.95 - 0.09 * er, u);
    float hole = nrm(tex2D(noiseSamp, float2(u * 13.0 + uSeed * 2.0, uTime * 0.23 + uSeed)).r);
    float holes = step(fray * 0.80, hole + 0.02);
    //磨损毛边：线身发虚
    float wear = 1.0 - fray * 0.35;

    float alpha = body * endMask * holes * wear * input.Color.a;

    //---- 色：暗琥珀→缝线金→亮芯（芯用 lerp 替换不叠加，暖材质不白热截断）----
    float3 col = lerp(uDarkCol, uThreadCol, saturate(0.30 + 0.50 * tension + 0.20 * tw));
    col = lerp(col, uCoreCol, core * (0.25 + 0.40 * tension));
    col = lerp(col, float3(1.0, 0.985, 0.90), glint * 0.75);

    //预乘输出
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass StitcherThreadPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
