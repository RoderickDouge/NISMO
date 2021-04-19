using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class Chunk  
{
    public ChunkCoord coord;

    GameObject chunkObject;
    MeshRenderer meshRenderer;
    MeshFilter meshFilter;

    int vertexIndex = 0;
    List<Vector3> vertices = new List<Vector3> ();
    List<int> triangles = new List<int> ();
    List<int> transparentTriangles = new List<int>();
    Material[] materials = new Material[2];
    List<Vector2> uvs = new List<Vector2> ();

    public Vector3 position;

    public byte [,,] cubeMap = new byte[CubeData.ChunkWidth, CubeData.ChunkHeight, CubeData.ChunkWidth];

    public Queue<CubeMod> modifications = new Queue<CubeMod>();

    World world;

    private bool _isActive;
    private bool isCubeMapPopulated = false;
    private bool threadLocked = false;

   public Chunk (ChunkCoord _coord, World _world, bool generateOnLoad)
    {
        coord = _coord;
        world = _world;
        isActive = true;

        if (generateOnLoad)
            Init();

    }

    public void Init (){
        chunkObject = new GameObject();
        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        materials[0] = world.material;
        materials[1] = world.transparentMaterial;
        meshRenderer.materials = materials;

        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(coord.x * CubeData.ChunkWidth, 0f, coord.z * CubeData.ChunkWidth);
        chunkObject.name = "Chunk " + coord.x + ", " + coord.z;
        position = chunkObject.transform.position;

        Thread myThread = new Thread(new ThreadStart(PopulateCubeMap));
        myThread.Start();
 
    } 

    void PopulateCubeMap ()
    {
        for (int y =0; y < CubeData.ChunkHeight; y++) {
            for (int x =0; x < CubeData.ChunkWidth; x++) {
                for (int z =0; z < CubeData.ChunkWidth; z++) {

                    cubeMap[x, y, z] = world.GetCube(new Vector3(x, y, z) + position);

                }
            }
        }
        _updateChunk();
        isCubeMapPopulated = true;

    }

    public void UpdateChunk() {
        Thread myThread = new Thread (new ThreadStart(_updateChunk));
        myThread.Start();
    }

    private void _updateChunk()
    {

        threadLocked = true;

        while (modifications.Count > 0)
        {
            CubeMod v = modifications.Dequeue();
            Vector3 pos = v.position -= position;
            cubeMap[(int)pos.x, (int)pos.y, (int)pos.z] = v.id;
        }

        ClearMeshData();

        for (int y =0; y < CubeData.ChunkHeight; y++) {
            for (int x =0; x < CubeData.ChunkWidth; x++) {
                for (int z = 0; z < CubeData.ChunkWidth; z++) {

                    if (world.blockTypes[cubeMap[x, y, z]].isSolid)
                        UpdateMeshData (new Vector3(x, y, z));
                }
            }
        }
        lock (world.chunksToDraw){
            world.chunksToDraw.Enqueue(this);
        }
        threadLocked = false;
    }

    void ClearMeshData () {

        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        transparentTriangles.Clear();
        uvs.Clear();
    }

    public bool isActive 
    {
        get { return _isActive; }
        set { 
            _isActive = value;
            if (chunkObject != null)
                chunkObject.SetActive(value);
            }
    }

    public bool isEditable {
        get {
            if (!isCubeMapPopulated || threadLocked)
                return false;
            else
                return true;
        }
    } 

    bool IsCubeInChunk (int x, int y, int z)
    {
        if (x < 0 || x > CubeData.ChunkWidth - 1 || y < 0 || y > CubeData.ChunkHeight - 1 || z < 0 || z > CubeData.ChunkWidth - 1)
            return false;
        else 
            return true;
    }

    public void EditCube (Vector3 pos, byte newID) {

        int xCheck = Mathf.FloorToInt (pos.x);
        int yCheck = Mathf.FloorToInt (pos.y);
        int zCheck = Mathf.FloorToInt (pos.z);      

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);  
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);  

        cubeMap[xCheck, yCheck, zCheck] = newID;

        UpdateSurroundingCubes(xCheck, yCheck, zCheck);

        _updateChunk();
    }

    void UpdateSurroundingCubes (int x, int y, int z) {

        Vector3 thisCube = new Vector3(x, y, z);

        for (int p = 0; p < 6; p++){

            Vector3 currentCube = thisCube + CubeData.faceChecks[p];

            if (!IsCubeInChunk((int)currentCube.x, (int)currentCube.y, (int)currentCube.z)) {
                
                world.GetChunkFromVector3(currentCube + position)._updateChunk();

            } 
        }

    }

    bool CheckCube (Vector3 pos)
    {
        int x = Mathf.FloorToInt (pos.x);
        int y = Mathf.FloorToInt (pos.y);
        int z = Mathf.FloorToInt (pos.z);

        if (!IsCubeInChunk(x, y, z))
            return world.CheckIfCubeTransparent(pos + position);
        
        return world.blockTypes[cubeMap [x, y, z]].isTransparent;
    }

    public byte GetCubeFromGlobalVector3 (Vector3 pos) {
        int xCheck = Mathf.FloorToInt (pos.x);
        int yCheck = Mathf.FloorToInt (pos.y);
        int zCheck = Mathf.FloorToInt (pos.z);      

        xCheck -= Mathf.FloorToInt(position.x);  
        zCheck -= Mathf.FloorToInt(position.z);  

        return cubeMap[xCheck, yCheck, zCheck];

    }

    void UpdateMeshData (Vector3 pos) 
    {
        byte blockID = cubeMap[(int)pos.x, (int)pos.y, (int)pos.z];
        bool isTransparent = world.blockTypes[blockID].isTransparent;

        for (int p = 0; p < 6; p++){

            if (CheckCube(pos + CubeData.faceChecks[p])) {  

                vertices.Add (pos + CubeData.cubeVerts [CubeData.cubeTris [p, 0]]);
                vertices.Add (pos + CubeData.cubeVerts [CubeData.cubeTris [p, 1]]);
                vertices.Add (pos + CubeData.cubeVerts [CubeData.cubeTris [p, 2]]);
                vertices.Add (pos + CubeData.cubeVerts [CubeData.cubeTris [p, 3]]);
              
                AddTexture(world.blockTypes[blockID].GetTextureID(p));
              
                if (!isTransparent){
                    triangles.Add (vertexIndex);
                    triangles.Add (vertexIndex + 1);
                    triangles.Add (vertexIndex + 2);
                    triangles.Add (vertexIndex + 2);
                    triangles.Add (vertexIndex + 1);
                    triangles.Add (vertexIndex + 3);
                } else {
                    transparentTriangles.Add (vertexIndex);
                    transparentTriangles.Add (vertexIndex + 1);
                    transparentTriangles.Add (vertexIndex + 2);
                    transparentTriangles.Add (vertexIndex + 2);
                    transparentTriangles.Add (vertexIndex + 1);
                    transparentTriangles.Add (vertexIndex + 3);
                }

                vertexIndex += 4;

            }
        }
    }
    public void CreateMesh () 
    {
        Mesh mesh = new Mesh ();
        mesh.vertices = vertices.ToArray ();

        mesh.subMeshCount = 2;
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetTriangles(transparentTriangles.ToArray(), 1);

        mesh.uv = uvs.ToArray ();

        mesh.RecalculateNormals ();
        
        meshFilter.mesh = mesh;
    }

    void AddTexture (int textureID){
        
        float y = textureID / CubeData.TextureAtlasSizeInBlocks;
        float x = textureID - (y * CubeData.TextureAtlasSizeInBlocks);

        x *= CubeData.NormalizedBlockTextureSize;
        y *= CubeData.NormalizedBlockTextureSize;

        y = 1f - y - CubeData.NormalizedBlockTextureSize;

        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y + CubeData.NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x + CubeData.NormalizedBlockTextureSize, y));
        uvs.Add(new Vector2(x + CubeData.NormalizedBlockTextureSize, y + CubeData.NormalizedBlockTextureSize));

    }

};


public class ChunkCoord {

    public int x;
    public int z;

    public ChunkCoord () {

        x = 0;
        z = 0;

    }

    public ChunkCoord (int _x, int _z){
    
        x = _x;
        z = _z;
    }

    public ChunkCoord (Vector3 pos) {
        int xCheck = Mathf.FloorToInt(pos.x);
        int zCheck = Mathf.FloorToInt(pos.z);

        x = xCheck / CubeData.ChunkWidth;
        z = zCheck / CubeData.ChunkWidth;

    }

    public bool Equals (ChunkCoord other) {
        
        if (other == null)
            return false;
        else if (other.x == x && other.z == z)
            return true;
        else 
            return false;
    }
    
}