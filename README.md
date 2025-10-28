# Hackathon Prototype: Memory Lane Ponderings

## Prerequisites

1. **Unity 6.0**  (https://unity.com/download)
2. **LM Studio** ([https://lmstudio.ai](https://lmstudio.ai))  
3. A downloaded model in LM Studio: **gemma-3-4b**
   - Open LM Studio’s **Discover** tab, find Gemma 3 4B, click **Download**
   - In the **Developer** tab, select the model and click **Start Server**  
   - Note the generated URL (default: `http://127.0.0.1:1234/v1/completions`)
4. **ComfyUI** (https://www.comfy.org/download)
   - Make sure that the exposed Port is 8188 (top left -> Settings -> Server Config) 
5. A custom node for ComfyUI: **ComfyUI-BRIA_AI-RMBG**
   - Open ComfyUI's Manager (top right)
   - Click Custom Nodes Manager
   - Search for ComfyUI-BRIA_AI-RMBG and install (restart of ComfyUI required)
   
   
## How to run

1. Download source, e.g. with `git clone https://github.com/YenR/hackathon-pond.git`
2. Start LLM Server (see above)
3. Start ComfyUI
4. Start Unity and open the project
5. Optionally, adjust model names and system ports 
7. Run the game in Unity


## Used assets: 

- [Kenney's roguelike RPG Pack](https://kenney.nl/assets/roguelike-rpg-pack)
- [Motley Forces Font](https://www.fontspace.com/motley-forces-font-f87817)







