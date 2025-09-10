sampler uImage0 : register(s0);

float m; //亮度阈值
float n; //描边宽度

float uTime;//游戏时间，用于驱动动画
float2 worldSize;//屏幕或渲染目标的分辨率

#define rot(a) float2x2(cos(a), -sin(a), sin(a), cos(a))

float3 mod(float3 x, float y)
{
    return x - y * floor(x / y);
}

//////////////////////////////////////////////////////////////////////////////
// Star Nest by Pablo Roman Andrioli                                        //
// License: MIT                                                             //
// 我改了一些，如果想学习的人最好去原网站找:https://www.shadertoy.com/view/XlfGRj //
//////////////////////////////////////////////////////////////////////////////

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 c = tex2D(uImage0, coords);
    float a = max(c.r, max(c.g, c.b));

    if (a > m) //如果亮度超过阈值，生成星空
    {
        float4 O = float4(0, 0, 0, 0);
        O -= O;
        float t = uTime * 0.01 + 0.25, s = 0.6, f = 2.0, l;

        float3 p,
            D = float3((coords.xy - 0.5) * float2(worldSize.x / worldSize.y, 1.0), 0.5),
            o = float3(1.0, 0.5, 0.5) + float3(t + t, t, -2.0);

        D.xy /= 2.0;

        float2x2 r1 = rot(0.5 + 1.0);
        float2x2 r2 = rot(0.8 + 1.0);
        D.xz = mul(D.xz, r1);
        o.xz = mul(o.xz, r1);
        D.xy = mul(D.xy, r2);
        o.xy = mul(o.xy, r2);

        for (int i, r = 0; r++ < 4; f *= 0.93, s += 0.1)
        {
            p = abs(mod(o + s * D, 1.7) - 0.85);
            a = t = 0.;
            for (i = 0; i++ < 15; t = l)
                l = length(p = abs(p) / dot(p, p) - 0.53),
                a += abs(l - t);

            a *= a * a;
            
            //定义青色基调的背景星云颜色
            //这里的 R G B 分量分别是 (红, 绿, 蓝)，低红、高绿、高蓝混合成青色
            float3 nebulaColor = float3(0.15, 0.6, 0.8);
            
            //定义五彩斑斓的星星颜色
            //使用sin函数和不同的相位偏移(0.0, 1.57, 3.14)来为R,G,B通道生成不断变化的鲜艳色彩
            //p.zxy * 8.0 使用了迭代中的位置向量来确保不同位置的星星颜色不同，增加随机感
            float3 vibrantStarColor = 0.5 + 0.5 * sin(p.zxy * 8.0 + float3(0.0, 1.57, 3.14));
            
            //混合颜色
            //f * nebulaColor * 0.8 是暗淡的青色背景星云
            //vibrantStarColor * a * 0.004 * f 是由亮度'a'驱动的、明亮的彩色星星
            O.rgb += f * nebulaColor * 0.8 + vibrantStarColor * a * 0.004 * f;
        }
        
        float y = 0.0025 * length(O);
        O = 0.012 * O + float4(y, y, y, y);
        
        return float4(O.xyz, 1.0);
    }
    else if (abs(a - m) < n)
    {
        return float4(0.02, 0.1, 0.9, 1.0);
    }
    else
    {
        return c * a;
    }
}

technique Technique1
{
    pass StarsShaderPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}