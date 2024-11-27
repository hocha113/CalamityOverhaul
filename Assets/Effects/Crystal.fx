sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
//缩放矩阵
matrix transformMatrix;
float2 basePos;
float2 scale;
//texture noiseTexture;
float uTime;
float lightRange;
float lightLimit;
float addC;
//高光颜色
float4 highlightC;
//亮部颜色
float4 brightC;
//暗部颜色
float4 darkC;

struct VertexShaderInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

const float Epsilon = 1e-10;

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    output.Position = mul(input.Position, transformMatrix);
    return output;
}

float4 BLerp(float4 a, float4 b, float f)
{
    return (a * (1.0f - f)) + (b * f);
}

float3 HueToRGB(float H)
{
    float R = abs(H * 6 - 3) - 1;
    float G = 2 - abs(H * 6 - 1);
    float B = 2 - abs(H * 6 - 4);
    return saturate(float3(R, G, B));
}

float3 HSVtoRGB(float3 HSV)
{
    float3 RGB = HueToRGB(HSV.x);
    return ((RGB - float3(1, 1, 1)) * HSV.y + float3(1, 1, 1)) * HSV.z;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float2 coords = input.TexCoords;

    float4 tc = tex2D(uImage0, coords);
    
    if (!any(tc))
        return float4(0, 0, 0, 0);
    
    //return tc;

    //获取灰度图中的灰度
    float2 nCoords = (input.Position.xy - basePos) * scale % float2(256.0, 256.0) / float2(256.0, 256.0);
    float4 c2 = tex2D(uImage1, nCoords);
    
    float gray = c2.r - 0.0001;

    //计算基本颜色
    float4 c1 = BLerp(darkC, brightC, sin((gray + uTime) * 3.141f - 1.57f) / 2 + 0.5f);
    
    //计算最终混合后的颜色
    float4 fc = (c1 * tc + c1 * addC) * tc.a;
    fc *= input.Color;
    
    //按时间变亮
    float t = frac(sin(uTime) / 2 + 0.5f);
    float a = abs(t - gray);
    float y = 0.299 * fc.r + 0.587 * fc.g + 0.114 * fc.b;
    if (a < lightRange && y > lightLimit)
    {
        fc = BLerp(fc, highlightC * tc.a, (lightRange - a) / lightRange);
    }
    
    return fc;
}

technique Technique1
{
    pass CrystalPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
};