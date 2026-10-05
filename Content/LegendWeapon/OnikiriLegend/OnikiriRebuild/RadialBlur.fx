sampler uImage0 : register(s0);

float strength;
float Iteration;
float2 center;
float4 PixelShaderFunction(float2 coords : TEXCOORD0,float4 color : COLOR0) : COLOR0//以某个圆心往外径向模糊
{
    float2 blurVector = (coords - center) * strength;

    float4 acumulateColor = float4(0, 0, 0, 0);
    [unroll(30)]
    for (int j = 0; j < Iteration; j++)
    {
        acumulateColor += tex2D(uImage0, coords);
        coords -= blurVector;
    }

    return acumulateColor / Iteration * color;
}

technique Technique1
{
    pass Apply
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}