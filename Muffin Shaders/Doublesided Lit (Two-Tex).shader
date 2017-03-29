Shader "Noble Muffins/Doublesided (Two-Tex, Lit)" {
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" { }
        _InsideTex ("Insides (RGB)", 2D) = "white" { }
    }

    Category
    {
	    BindChannels {
		   Bind "Vertex", vertex
		   Bind "texcoord", texcoord0
		   Bind "Normal", normal
	   }
        ZWrite On
        SubShader
        {
            Lighting On
            Pass
            {
            	Material {
                	Diffuse (1,1,1,1)
                	Ambient (1,1,1,1)
                }
        		Cull Back
                SetTexture [_MainTex]
                {
                    Combine texture * primary DOUBLE
                } 
            }
            Pass
            {
        		Cull Front
                SetTexture [_InsideTex]
                {
                    Combine texture * primary DOUBLE
                } 
            }
        } 
    }
}
