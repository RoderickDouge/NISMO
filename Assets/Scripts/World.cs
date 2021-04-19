using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World : MonoBehaviour
{
    public int seed;
    public BiomeAttribute biome;

    public Transform player;
    public Vector3 spawnPosition;

   public Material material;
   public Material transparentMaterial;
   public BlockType[] blockTypes;

   Chunk[,] chunks = new Chunk[CubeData.WorldSizeInChunks, CubeData.WorldSizeInChunks];

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    public ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;

    List<ChunkCoord> chunksToCreate = new List<ChunkCoord>();
    List<Chunk> chunksToUpdate = new List<Chunk>(); 
    public Queue<Chunk> chunksToDraw = new Queue<Chunk>();

    bool applyingModifications = false;
    Queue<Queue<CubeMod>> modifications = new Queue<Queue<CubeMod>>();

    public GameObject debugScreen;

    public void Start(){

        Random.InitState(seed);

        spawnPosition = new Vector3((CubeData.WorldSizeInChunks * CubeData.ChunkWidth) / 2f, CubeData.ChunkHeight - 50f, (CubeData.WorldSizeInChunks * CubeData.ChunkWidth) / 2f);
        GenerateWorld();
        playerLastChunkCoord = GetChunkCoordFromVector3(player.position);
        
    }

    private void Update() {
        playerChunkCoord = GetChunkCoordFromVector3(player.position);

        // Only ubpate Chunks when the player moved from the chunk they were previoulsy on.
        if (!playerChunkCoord.Equals(playerLastChunkCoord))
            CheckViewDistance();

        if (!applyingModifications){
            ApplyModifications();
        }
        if (chunksToCreate.Count > 0) {
            CreateChunk();
        }
        if (chunksToUpdate.Count > 0){
            UpdateChunks();
        }
        if (chunksToDraw.Count > 0) {
            lock (chunksToDraw){

                if (chunksToDraw.Peek().isEditable)
                    chunksToDraw.Dequeue().CreateMesh();

            }
        }


        if (Input.GetKeyDown(KeyCode.F3))
            debugScreen.SetActive(!debugScreen.activeSelf);
    }
    
    void GenerateWorld()
    {
        for (int x = (CubeData.WorldSizeInChunks / 2) - CubeData.ViewDistanceInChunks; x < (CubeData.WorldSizeInChunks / 2) + CubeData.ViewDistanceInChunks; x++){
            for (int z = (CubeData.WorldSizeInChunks / 2) - CubeData.ViewDistanceInChunks; z < (CubeData.WorldSizeInChunks / 2) + CubeData.ViewDistanceInChunks; z++){
                chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, true);
                activeChunks.Add (new ChunkCoord(x, z));

            }
        }

        player.position = spawnPosition;

    }

    void CreateChunk (){
        ChunkCoord c = chunksToCreate[0];
        chunksToCreate.RemoveAt(0);
        activeChunks.Add(c);
        chunks[c.x, c.z].Init();
    }

    void UpdateChunks () {
        bool updated = false;
        int index = 0;
        while (!updated && index < chunksToUpdate.Count - 1) 
        {
            if (chunksToUpdate[index].isEditable) {
                chunksToUpdate[index].UpdateChunk();
                chunksToUpdate.RemoveAt(index);
                updated = true;
            } else {
                index++;
            }
        }
    }

    void ApplyModifications() {

        applyingModifications = true;

        while (modifications.Count > 0) {

            Queue<CubeMod> queue = modifications.Dequeue();

            while (queue.Count > 0) {

                CubeMod v = queue.Dequeue();

                ChunkCoord c = GetChunkCoordFromVector3(v.position);

                if (chunks[c.x, c.z] == null) {
                    chunks[c.x, c.z] = new Chunk(c, this, true);
                    activeChunks.Add(c);
                }

                chunks[c.x, c.z].modifications.Enqueue(v);

                if (!chunksToUpdate.Contains(chunks[c.x, c.z]))
                    chunksToUpdate.Add(chunks[c.x, c.z]);
            }

        }

        applyingModifications = false;

    }

    ChunkCoord GetChunkCoordFromVector3 (Vector3 pos){

        int x = Mathf.FloorToInt(pos.x / CubeData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / CubeData.ChunkWidth);
        return new ChunkCoord (x, z);

    }

    public Chunk GetChunkFromVector3 (Vector3 pos){

        int x = Mathf.FloorToInt(pos.x / CubeData.ChunkWidth);
        int z = Mathf.FloorToInt(pos.z / CubeData.ChunkWidth);
        return chunks[x, z];

    }

    void CheckViewDistance () {

        ChunkCoord coord = GetChunkCoordFromVector3(player.position);
        playerLastChunkCoord = playerChunkCoord;

        List<ChunkCoord> previouslyActiveChunks = new List<ChunkCoord>(activeChunks);

        for (int x = coord.x - CubeData.ViewDistanceInChunks; x < coord.x + CubeData.ViewDistanceInChunks; x++){
            for (int z = coord.z - CubeData.ViewDistanceInChunks; z < coord.z + CubeData.ViewDistanceInChunks; z++){
            
                if (IsChunkInWorld (new ChunkCoord (x, z))) {

                    if (chunks[x, z] == null){
                        chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, false);
                        chunksToCreate.Add(new ChunkCoord(x, z));
                    }
                    else if (!chunks[x, z].isActive) {
                        chunks[x, z].isActive = true;
                    }
                    activeChunks.Add(new ChunkCoord(x, z));
                }

                for (int i = 0; i < previouslyActiveChunks.Count; i++) {

                    if (previouslyActiveChunks[i].Equals(new ChunkCoord(x, z)))
                        previouslyActiveChunks.RemoveAt(i);

                }

            }    
        }
        foreach (ChunkCoord c in previouslyActiveChunks)
            chunks[c.x, c.z].isActive = false;
    }

    public bool CheckForCube (Vector3 pos) {

        ChunkCoord thisChunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > CubeData.ChunkHeight)
            return false;

        if (chunks[thisChunk.x,  thisChunk.z] != null && chunks[thisChunk.x,  thisChunk.z].isEditable)
            return blockTypes[chunks[thisChunk.x,  thisChunk.z].GetCubeFromGlobalVector3(pos)].isSolid;

        return blockTypes[GetCube(pos)].isSolid;
      
    }

        public bool CheckIfCubeTransparent (Vector3 pos) {

        ChunkCoord thisChunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > CubeData.ChunkHeight)
            return false;

        if (chunks[thisChunk.x,  thisChunk.z] != null && chunks[thisChunk.x,  thisChunk.z].isEditable)
            return blockTypes[chunks[thisChunk.x,  thisChunk.z].GetCubeFromGlobalVector3(pos)].isTransparent;

        return blockTypes[GetCube(pos)].isTransparent;
      
    }

    public byte GetCube (Vector3 pos)
    {
        int yPos = Mathf.FloorToInt(pos.y);

        /* IMMUTABLE PASS */

        // If Outside world, return air.
        if (!IsCubeInWorld(pos))
            return 0;
  
        // If bottom block of chunk, return Bedrock.
        if (yPos == 0)
            return 1;

        /* BASIC TERRAIN PASS */

        int terrainHeight = Mathf.FloorToInt(biome.terrainHeight * Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.terrainScale)) + biome.solidGroundHeight;
        byte cubeValue = 0;
        


        if (yPos == terrainHeight)
            cubeValue = 3;
        else if (yPos < terrainHeight && yPos > terrainHeight - 4)
            cubeValue = 5;
        else if  (yPos > terrainHeight)
            return 0;
        else 
            cubeValue = 2;

        /* SECOND PASS */

        if (cubeValue == 2){
            foreach (Lode lode in biome.lodes){
                if (yPos > lode.minHeight && yPos < lode.maxHeight)
                    if (Noise.Get3DPerlin(pos, lode.noiseOffset, lode.scale, lode.threshold))
                        cubeValue = lode.blockID;
            }
        }

        /* TREE PASS */

        if (yPos == terrainHeight) {

            if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treeZoneScale) > biome.treeZoneThreshold) {
                if (Noise.Get2DPerlin(new Vector2(pos.x, pos.z), 0, biome.treePlacementScale) > biome.treePlacementThreshold) {
                    modifications.Enqueue(Structure.MakeTree(pos, biome.minTreeHeight, biome.maxTreeHeight));
                }
            }

        }

        return cubeValue;



        
    }

    bool IsChunkInWorld (ChunkCoord coord)
    {
        if(coord.x > 0 && coord.x < CubeData.WorldSizeInChunks - 1 && coord.z > 0 && coord.z < CubeData.WorldSizeInChunks - 1)
            return true;
        else
            return false;
    }

    bool IsCubeInWorld (Vector3 pos)
    {
        if (pos.x >= 0 && pos.x < CubeData.WorldSizeInCubes && pos.y >= 0 && pos.y < CubeData.ChunkHeight && pos.z >= 0 && pos.z < CubeData.WorldSizeInCubes)
            return true;
        else
            return 
                false;
           
    }

}



[System.Serializable]
public class BlockType {

    public string blockName;
    public bool isSolid;
    public bool isTransparent;
    public Sprite icon;

    [Header ("Tesxture Values")]
    public int backFaceTexture;
    public int frontFaceTexture;
    public int topFaceTexture;
    public int bottomFaceTexture;
    public int leftFaceTexture;
    public int rightFaceTexture;

    // Back, Front, Top, Bottom, Left, Right

    public int GetTextureID (int faceIndex){

        switch (faceIndex){
            case 0:
                return backFaceTexture;
            case 1:
                return frontFaceTexture;
            case 2:
                return topFaceTexture;
            case 3:
                return bottomFaceTexture;
            case 4:
                return leftFaceTexture;
            case 5:
                return rightFaceTexture;
            default:
                Debug.Log ("Error in GetTextureID; invalad face index");
                return 0;
        }

    }

}

public class CubeMod {
    public Vector3 position;
    public byte id;

    public CubeMod() {
        position = new Vector3();
        id = 0;
    }

    public CubeMod (Vector3 _position, byte _id) {

        position = _position;
        id = _id; 

    }
}
