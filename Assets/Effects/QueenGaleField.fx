// ============================================================================
// QueenGaleField.fx 皇后翼压风道
// UV.x 0→1 沿风向 UV.y 0~1 横截面；Additive
// 材质=受压气流+凝胶微沫：各向异性拉丝流线两层视差+珠光微粒
// 极角审计：全程笛卡尔UV，无 atan2
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uStrength;   //风场强度包络 0~1
float uHueSeed;
float seed;

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

float3 PrismHue(float t)
{
    return 0.72 + 0.28 * cos(6.28318 * (t + float3(0.0, 0.35, 0.68)));
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;   //-1~1 横截面

    //====================================================
    //两层各向异性流线：沿风向重度拉伸的噪声，异速视差
    //====================================================
    //层1：主流线(快)
    float n1 = tex2D(noiseSamp, float2(uv.x * 2.6 - uTime * 1.9 + seed * 5.0, uv.y * 9.0 + seed)).r;
    float streak1 = smoothstep(0.56, 0.88, n1);
    //层2：细拉丝(更快更细)
    float n2 = tex2D(noiseSamp, float2(uv.x * 4.4 - uTime * 3.1 + seed * 9.0, uv.y * 16.0 + seed * 2.0)).r;
    float streak2 = smoothstep(0.68, 0.94, n2);

    //风纹起伏：横截面被大尺度噪声轻推(风道不是死直的)
    float sway = tex2D(noiseSamp, float2(uv.x * 1.1 - uTime * 0.6, seed * 3.0)).r - 0.5;
    float crossShift = cross_ + sway * 0.24;

    //====================================================
    //珠光微沫：小亮点顺流漂移
    //====================================================
    float mist = tex2D(noiseSamp, float2(uv.x * 6.0 - uTime * 2.4 + seed, uv.y * 5.0 + seed * 4.0)).r;
    float motes = smoothstep(0.86, 0.97, mist);

    //====================================================
    //遮罩：横向羽化+两端渐没
    //====================================================
    float edgeMask = smoothstep(1.0, 0.55, abs(crossShift));
    float endMask = smoothstep(0.0, 0.14, uv.x) * smoothstep(1.0, 0.86, uv.x);
    float mask = edgeMask * endMask;

    //====================================================
    //调色：珠光白为主，淡棱彩点缀
    //====================================================
    float3 hue = PrismHue(uHueSeed);
    float3 cPearl = float3(0.92, 0.96, 1.0);

    float3 color = float3(0.0, 0.0, 0.0);
    color += cPearl * streak1 * 0.5;
    color += cPearl * streak2 * 0.75;
    color += hue * streak1 * 0.22;
    color += cPearl * motes * 0.9;
    color += hue * 0.06;                 //底雾
    color *= mask;

    float alpha = saturate((streak1 * 0.4 + streak2 * 0.5 + motes * 0.6 + 0.05) * mask);
    alpha *= uStrength;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass QueenGaleFieldPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
