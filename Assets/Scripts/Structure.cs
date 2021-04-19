using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Structure {
   public static Queue<CubeMod> MakeTree (Vector3 position, int minTrunkHeight, int maxTrunkHeight) {

       Queue<CubeMod> queue = new Queue<CubeMod>();

       int height = (int)(maxTrunkHeight * Noise.Get2DPerlin(new Vector2(position.x, position.z), 250f, 3f));
       if (height < minTrunkHeight) {
           height = minTrunkHeight;
       }

       for (int i = 1; i < height; i++)
       {
           queue.Enqueue(new CubeMod(new Vector3(position.x, position.y + i, position.z), 6));
       }

       for (int x = -3; x < 4; x++){
           for (int y = 0; y < 7; y++){
               for (int z = -3; z < 4; z++){
                   queue.Enqueue(new CubeMod(new Vector3(position.x + x, position.y + y + height, position.z + z), 11));
                }
            }
       }
        return queue;
   }
}
